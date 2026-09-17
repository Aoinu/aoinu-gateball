using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GateballNetworkState : UdonSharpBehaviour
    {
        public const int PhaseWaiting = 0;
        public const int PhaseSimulating = 1;
        public const int PhaseSettled = 2;

        [Header("References")]
        public GateballCourt Court;
        public GateballTelemetry Telemetry;

        [Header("Settlement")]
        public int MinimumSimulationSteps = 2;
        public float SettleDelay = 0.15f;
        public float MaximumSimulationTime = 30f;

        [Header("Synced authoritative state")]
        [UdonSynced] public int ShotId;
        [UdonSynced] public int Phase = PhaseWaiting;
        [UdonSynced] public Vector3[] AuthoritativeBallPositions = new Vector3[GateballGeometry.BallCount];
        [UdonSynced] public Vector3[] InitialBallPositions = new Vector3[GateballGeometry.BallCount];
        [UdonSynced] public int StrokeBallId = -1;
        [UdonSynced] public Vector3 StrokeDirection;
        [UdonSynced] public float StrokeImpulse;
        [UdonSynced] public Vector3[] FinalBallPositions = new Vector3[GateballGeometry.BallCount];
        [UdonSynced] public int ShotAuthorityPlayerId = -1;
        [UdonSynced] public int ShotEndSimulationStep;
        [UdonSynced] public float ShotEndElapsedSimulationTime;
        [UdonSynced] public bool ShotEndWasForced;
        [UdonSynced] public int[] ShotEndEventSequence = new int[GateballTelemetry.MaxEventCount];
        [UdonSynced] public int ShotEndEventCount;

        [Header("Local telemetry summary")]
        [System.NonSerialized] public int LocalSimulationStep;
        [System.NonSerialized] public float LocalElapsedSimulationTime;
        [System.NonSerialized] public float LocalFixedDeltaTime;
        [System.NonSerialized] public float LastMaxPositionError;
        [System.NonSerialized] public float LastMeanPositionError;
        [System.NonSerialized] public float LastMaxTrajectoryPositionError;
        [System.NonSerialized] public float LastMeanTrajectoryPositionError;
        [System.NonSerialized] public float LastMaxFinalPositionError;
        [System.NonSerialized] public float LastMeanFinalPositionError;
        [System.NonSerialized] public float LastFinalPositionError;
        [System.NonSerialized] public float LastSettleTimeError;
        [System.NonSerialized] public int LastSettleStepError;
        [System.NonSerialized] public float LastFinalCorrectionDistance;
        [System.NonSerialized] public int LastEventDivergenceCount;
        [System.NonSerialized] public bool LastShotEndWasForced;

        private int _lastAppliedShotId = -1;
        private int _lateJoinShotId = -1;
        private int _activeLocalShotId = -1;
        private int _lastObservedPhase = PhaseWaiting;
        private bool _initialized;
        private bool _pendingStroke;
        private bool _pendingShotStart;
        private bool _pendingStrokeApply;
        private int _pendingStrokeBallId;
        private Vector3 _pendingStrokeDirection;
        private float _pendingStrokeImpulse;
        private float _settledTime;
        private bool _observedMotion;

        private void Start()
        {
            _EnsureArrays();
            if (Telemetry == null)
            {
                Telemetry = GetComponent<GateballTelemetry>();
            }

            if (Court != null && Phase != PhaseWaiting)
            {
                Court._ApplyBallPositions(AuthoritativeBallPositions);
            }

            if (Phase == PhaseSimulating)
            {
                _lateJoinShotId = ShotId;
                _activeLocalShotId = -1;
            }

            _lastAppliedShotId = ShotId;
            _lastObservedPhase = Phase;
            _initialized = true;

            if (ShotId == 0 && Phase == PhaseWaiting && _IsLocalOwner())
            {
                if (Court != null)
                {
                    Court._CaptureBallPositions(AuthoritativeBallPositions);
                    _RequestSerializationIfOwner();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            _TryApplyPendingShotStart();
            _TryApplyPendingStroke();

            if (Phase != PhaseSimulating || _activeLocalShotId != ShotId || Court == null)
            {
                return;
            }

            LocalSimulationStep++;
            LocalElapsedSimulationTime += Time.fixedDeltaTime;
            LocalFixedDeltaTime = Time.fixedDeltaTime;

            bool timedOut = MaximumSimulationTime > 0f
                && LocalElapsedSimulationTime >= MaximumSimulationTime;

            if (Court._HasAnyBallMotion())
            {
                _observedMotion = true;
            }

            if (LocalSimulationStep == 1 || LocalSimulationStep == 10 || LocalSimulationStep == 100
                || LocalSimulationStep % 500 == 0)
            {
                Debug.Log("[Gateball v0.2] SimCheckpoint shot=" + ShotId.ToString()
                    + " role=" + (_IsLocalOwner() ? "Owner" : "Remote")
                    + " step=" + LocalSimulationStep.ToString()
                    + " fixedDelta=" + LocalFixedDeltaTime.ToString()
                    + " observed=" + _observedMotion.ToString()
                    + " moving=" + Court._GetMovingBallCount().ToString()
                    + " maxSpeed=" + Court._GetMaxLinearSpeed().ToString()
                    + " maxAngularTip=" + Court._GetMaxAngularTipSpeed().ToString()
                    + " stopped=" + Court._AreAllBallsStopped().ToString()
                    + " settledTime=" + _settledTime.ToString());
            }

            bool settled = LocalSimulationStep >= MinimumSimulationSteps
                && _observedMotion
                && Court._AreAllBallsStopped();
            if (!settled && !timedOut)
            {
                _settledTime = 0f;
                return;
            }

            if (settled)
            {
                _settledTime += Time.fixedDeltaTime;
            }

            if ((timedOut || _settledTime >= SettleDelay) && _IsLocalOwner())
            {
                _FinishShot(timedOut);
            }
        }

        public void _RequestStroke(int ballId, Vector3 direction, float impulse)
        {
            if (Phase == PhaseSimulating || !GateballGeometry.IsValidBallId(ballId) || Court == null)
            {
                return;
            }

            if (!_IsLocalOwner())
            {
                if (Networking.LocalPlayer == null)
                {
                    return;
                }

                _pendingStroke = true;
                _pendingStrokeBallId = ballId;
                _pendingStrokeDirection = direction;
                _pendingStrokeImpulse = impulse;
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                return;
            }

            _BeginShot(ballId, direction, impulse);
        }

        public override void OnDeserialization()
        {
            if (!_initialized)
            {
                return;
            }

            if (ShotId < _lastAppliedShotId)
            {
                return;
            }

            if (Phase == PhaseSimulating && ShotId != _lastAppliedShotId)
            {
                _pendingShotStart = true;
            }
            else if (Phase == PhaseSettled
                && ShouldApplyShotEnd(Phase, ShotId, _lastAppliedShotId, _lateJoinShotId, _activeLocalShotId))
            {
                _ApplyShotEnd();
            }
            else if (Phase == PhaseWaiting)
            {
                _ApplyAuthoritativePositions();
                _activeLocalShotId = -1;
            }

            _lastAppliedShotId = ShotId;
            _lastObservedPhase = Phase;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player == null || !player.isLocal || !_pendingStroke || Phase == PhaseSimulating)
            {
                return;
            }

            _pendingStroke = false;
            _BeginShot(_pendingStrokeBallId, _pendingStrokeDirection, _pendingStrokeImpulse);
        }

        public string _GetPhaseName()
        {
            if (Phase == PhaseSimulating)
            {
                return "Simulating";
            }

            if (Phase == PhaseSettled)
            {
                return "Settled";
            }

            return "Waiting";
        }

        public bool _IsLocalOwner()
        {
            if (Networking.LocalPlayer == null)
            {
                return true;
            }

            return Networking.IsOwner(gameObject);
        }

        public static void CopyPositions(Vector3[] source, Vector3[] destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int count = Mathf.Min(GateballGeometry.BallCount, Mathf.Min(source.Length, destination.Length));
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }

        public static bool IsNewShot(int observedShotId, int incomingShotId)
        {
            return incomingShotId > observedShotId;
        }

        public static bool CanStartShot(int phase)
        {
            return phase != PhaseSimulating;
        }

        public static bool ShouldLateJoinUseAuthoritativeState(int phase)
        {
            return phase == PhaseWaiting || phase == PhaseSimulating || phase == PhaseSettled;
        }

        public static bool ShouldApplyShotEnd(
            int phase,
            int shotId,
            int lastAppliedShotId,
            int lateJoinShotId,
            int activeLocalShotId)
        {
            if (phase != PhaseSettled)
            {
                return false;
            }

            if (shotId < lastAppliedShotId)
            {
                return false;
            }

            return activeLocalShotId == shotId
                || shotId != lastAppliedShotId
                || lateJoinShotId == shotId;
        }

        public static float CalculateMaxPositionError(Vector3[] localPositions, Vector3[] ownerPositions)
        {
            return GateballTelemetry.CalculateMaxPositionError(localPositions, ownerPositions);
        }

        private void _BeginShot(int ballId, Vector3 direction, float impulse)
        {
            if (!_IsLocalOwner() || Phase == PhaseSimulating || Court == null)
            {
                return;
            }

            _EnsureArrays();
            ShotId++;
            Phase = PhaseSimulating;
            StrokeBallId = ballId;
            StrokeDirection = GateballGeometry.NormalizeStrokeDirection(direction);
            StrokeImpulse = Mathf.Max(0f, impulse);
            ShotAuthorityPlayerId = Networking.LocalPlayer == null ? -1 : Networking.LocalPlayer.playerId;
            ShotEndSimulationStep = 0;
            ShotEndElapsedSimulationTime = 0f;
            ShotEndWasForced = false;
            ShotEndEventCount = 0;
            _ClearEventSequence();

            Court._CaptureBallPositions(InitialBallPositions);
            _activeLocalShotId = ShotId;
            _lastAppliedShotId = ShotId;
            LocalSimulationStep = 0;
            LocalElapsedSimulationTime = 0f;
            LocalFixedDeltaTime = Time.fixedDeltaTime;
            _settledTime = 0f;
            _observedMotion = false;
            _pendingStrokeApply = false;

            if (Telemetry != null)
            {
                Telemetry._BeginShot(ShotId, true, StrokeBallId);
            }

            Debug.Log("[Gateball v0.2] ShotStart shot=" + ShotId.ToString()
                + " ball=" + StrokeBallId.ToString()
                + " impulse=" + StrokeImpulse.ToString()
                + " frame=" + Time.frameCount.ToString()
                + " fixedDelta=" + Time.fixedDeltaTime.ToString());

            _RequestSerializationIfOwner();
            _pendingShotStart = true;
        }

        private void _ApplyShotStart()
        {
            if (Court == null || !GateballGeometry.IsValidBallId(StrokeBallId))
            {
                return;
            }

            Court._ApplyBallPositions(InitialBallPositions);
            _lateJoinShotId = -1;
            _activeLocalShotId = ShotId;
            LocalSimulationStep = 0;
            LocalElapsedSimulationTime = 0f;
            LocalFixedDeltaTime = Time.fixedDeltaTime;
            _settledTime = 0f;
            _observedMotion = false;

            if (Telemetry != null)
            {
                Telemetry._BeginShot(ShotId, false, StrokeBallId);
            }

            Debug.Log("[Gateball v0.2] RemoteShotStart shot=" + ShotId.ToString()
                + " ball=" + StrokeBallId.ToString()
                + " impulse=" + StrokeImpulse.ToString()
                + " frame=" + Time.frameCount.ToString()
                + " fixedDelta=" + Time.fixedDeltaTime.ToString());

            if (Telemetry != null)
            {
                Telemetry._RecordInitialSample(Court.Balls);
            }
            _pendingStrokeApply = true;
        }

        private void _FinishShot(bool forced)
        {
            if (!_IsLocalOwner() || Court == null || Phase != PhaseSimulating)
            {
                return;
            }

            _pendingStrokeApply = false;

            if (Telemetry != null)
            {
                Telemetry._RecordFinalSample(Court.Balls);
            }

            Court._CaptureBallPositions(FinalBallPositions);
            CopyPositions(FinalBallPositions, AuthoritativeBallPositions);
            ShotEndSimulationStep = LocalSimulationStep;
            ShotEndElapsedSimulationTime = LocalElapsedSimulationTime;
            ShotEndWasForced = forced;
            ShotEndEventCount = Telemetry == null ? 0 : Telemetry.EventCount;
            if (Telemetry != null)
            {
                CopyEvents(Telemetry.EventSequence, ShotEndEventSequence, Telemetry.EventCount);
                Telemetry._RecordFinalComparison(
                    FinalBallPositions,
                    FinalBallPositions,
                    StrokeBallId,
                    ShotEndSimulationStep,
                    ShotEndElapsedSimulationTime,
                    ShotEndEventSequence,
                    ShotEndEventCount);
                _CopyTelemetrySummary();
            }

            Phase = PhaseSettled;
            _activeLocalShotId = -1;
            _lastAppliedShotId = ShotId;
            _lastObservedPhase = PhaseSettled;
            LastShotEndWasForced = forced;
            Debug.Log("[Gateball v0.2] ShotEnd shot=" + ShotId.ToString()
                + " step=" + ShotEndSimulationStep.ToString()
                + " elapsed=" + ShotEndElapsedSimulationTime.ToString()
                + " events=" + ShotEndEventCount.ToString()
                + " finalMax=" + LastMaxFinalPositionError.ToString()
                + " finalMean=" + LastMeanFinalPositionError.ToString()
                + " correction=" + LastFinalCorrectionDistance.ToString()
                + " observed=" + _observedMotion.ToString()
                + " moving=" + Court._GetMovingBallCount().ToString()
                + " forced=" + forced.ToString());
            _RequestSerializationIfOwner();
        }

        private void _ApplyShotEnd()
        {
            Vector3[] localPositions = new Vector3[GateballGeometry.BallCount];
            if (Court != null)
            {
                Court._CaptureBallPositions(localPositions);
            }

            if (Telemetry != null && Court != null && _activeLocalShotId == ShotId)
            {
                Telemetry._RecordFinalSample(Court.Balls);
            }

            if (Telemetry != null && _activeLocalShotId == ShotId)
            {
                Telemetry._RecordFinalComparison(
                    localPositions,
                    FinalBallPositions,
                    StrokeBallId,
                    ShotEndSimulationStep,
                    ShotEndElapsedSimulationTime,
                    ShotEndEventSequence,
                    ShotEndEventCount);
                _CopyTelemetrySummary();
            }

            _ApplyAuthoritativePositions();
            _activeLocalShotId = -1;
            _lateJoinShotId = -1;
            _pendingShotStart = false;
            _pendingStrokeApply = false;
            LastShotEndWasForced = ShotEndWasForced;
            Debug.Log("[Gateball v0.2] RemoteShotEnd shot=" + ShotId.ToString()
                + " trajectoryMax=" + LastMaxTrajectoryPositionError.ToString()
                + " trajectoryMean=" + LastMeanTrajectoryPositionError.ToString()
                + " finalMax=" + LastMaxFinalPositionError.ToString()
                + " finalMean=" + LastMeanFinalPositionError.ToString()
                + " finalStroke=" + LastFinalPositionError.ToString()
                + " correction=" + LastFinalCorrectionDistance.ToString()
                + " localEvents=" + (Telemetry == null ? 0 : Telemetry.EventCount).ToString()
                + " settleTimeError=" + LastSettleTimeError.ToString()
                + " settleStepError=" + LastSettleStepError.ToString()
                + " divergence=" + LastEventDivergenceCount.ToString()
                + " missingRemote=" + (Telemetry == null ? 0 : Telemetry.MissingRemoteEventCount).ToString()
                + " extraRemote=" + (Telemetry == null ? 0 : Telemetry.ExtraRemoteEventCount).ToString()
                + " differentType=" + (Telemetry == null ? 0 : Telemetry.DifferentEventTypeCount).ToString()
                + " differentBall=" + (Telemetry == null ? 0 : Telemetry.DifferentBallCount).ToString()
                + " differentTarget=" + (Telemetry == null ? 0 : Telemetry.DifferentTargetCount).ToString()
                + " differentGate=" + (Telemetry == null ? 0 : Telemetry.DifferentGateCount).ToString()
                + " orderingOnly=" + (Telemetry == null ? 0 : Telemetry.OrderingOnlyEventDifferenceCount).ToString()
                + " duplicate=" + (Telemetry == null ? 0 : Telemetry.DuplicateEventDifferenceCount).ToString()
                + " gameplayCritical=" + (Telemetry == null ? 0 : Telemetry.GameplayCriticalEventDivergenceCount).ToString()
                + " diagnostic=" + (Telemetry == null ? 0 : Telemetry.DiagnosticEventDivergenceCount).ToString()
                + " forced=" + ShotEndWasForced.ToString());
        }

        private void _ApplyAuthoritativePositions()
        {
            if (Court != null)
            {
                Court._ApplyBallPositions(AuthoritativeBallPositions);
            }
        }

        private void _TryApplyPendingStroke()
        {
            if (!_pendingStrokeApply || Court == null || Phase != PhaseSimulating)
            {
                return;
            }

            GateballBall strokeBall = Court._GetBall(StrokeBallId);
            if (strokeBall == null || !strokeBall._IsReady())
            {
                return;
            }

            _pendingStrokeApply = false;
            strokeBall._ApplyStroke(StrokeDirection, StrokeImpulse);
            Debug.Log("[Gateball v0.2] StrokeApplied shot=" + ShotId.ToString()
                + " role=" + (_IsLocalOwner() ? "Owner" : "Remote")
                + " ball=" + StrokeBallId.ToString()
                + " impulse=" + StrokeImpulse.ToString()
                + " simulationStep=" + LocalSimulationStep.ToString()
                + " fixedDelta=" + Time.fixedDeltaTime.ToString()
                + " velocity=" + strokeBall.Body.velocity.ToString()
                + " kinematic=" + strokeBall.Body.isKinematic.ToString());
        }

        private void _TryApplyPendingShotStart()
        {
            if (!_pendingShotStart || Court == null || Phase != PhaseSimulating)
            {
                return;
            }

            GateballBall pendingStrokeBall = Court._GetBall(StrokeBallId);
            if (pendingStrokeBall == null || !pendingStrokeBall._IsReady())
            {
                return;
            }

            _pendingShotStart = false;
            if (_activeLocalShotId == ShotId && _IsLocalOwner())
            {
                _ApplyLocalShotStart();
            }
            else
            {
                _ApplyShotStart();
            }
        }

        private void _ApplyLocalShotStart()
        {
            Court._ApplyBallPositions(InitialBallPositions);
            LocalFixedDeltaTime = Time.fixedDeltaTime;
            if (Telemetry != null)
            {
                Telemetry._RecordInitialSample(Court.Balls);
            }

            _pendingStrokeApply = true;
        }

        private void _CopyTelemetrySummary()
        {
            if (Telemetry == null)
            {
                return;
            }

            LastMaxPositionError = Telemetry.MaxPositionError;
            LastMeanPositionError = Telemetry.MeanPositionError;
            LastMaxTrajectoryPositionError = Telemetry.MaxTrajectoryPositionError;
            LastMeanTrajectoryPositionError = Telemetry.MeanTrajectoryPositionError;
            LastMaxFinalPositionError = Telemetry.MaxFinalPositionError;
            LastMeanFinalPositionError = Telemetry.MeanFinalPositionError;
            LastFinalPositionError = Telemetry.FinalPositionError;
            LastSettleTimeError = Telemetry.SettleTimeError;
            LastSettleStepError = Telemetry.SettleStepError;
            LastFinalCorrectionDistance = Telemetry.FinalCorrectionDistance;
            LastEventDivergenceCount = Telemetry.EventDivergenceCount;
        }

        private void _RequestSerializationIfOwner()
        {
            if (_IsLocalOwner() && Networking.LocalPlayer != null)
            {
                RequestSerialization();
            }
        }

        private void _EnsureArrays()
        {
            if (AuthoritativeBallPositions == null || AuthoritativeBallPositions.Length != GateballGeometry.BallCount)
            {
                AuthoritativeBallPositions = new Vector3[GateballGeometry.BallCount];
            }

            if (InitialBallPositions == null || InitialBallPositions.Length != GateballGeometry.BallCount)
            {
                InitialBallPositions = new Vector3[GateballGeometry.BallCount];
            }

            if (FinalBallPositions == null || FinalBallPositions.Length != GateballGeometry.BallCount)
            {
                FinalBallPositions = new Vector3[GateballGeometry.BallCount];
            }

            if (ShotEndEventSequence == null || ShotEndEventSequence.Length != GateballTelemetry.MaxEventCount)
            {
                ShotEndEventSequence = new int[GateballTelemetry.MaxEventCount];
            }
        }

        private void _ClearEventSequence()
        {
            for (int i = 0; i < ShotEndEventSequence.Length; i++)
            {
                ShotEndEventSequence[i] = 0;
            }
        }

        private static void CopyEvents(int[] source, int[] destination, int count)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int safeCount = Mathf.Clamp(count, 0, Mathf.Min(source.Length, destination.Length));
            for (int i = 0; i < safeCount; i++)
            {
                destination[i] = source[i];
            }
        }
    }
}
