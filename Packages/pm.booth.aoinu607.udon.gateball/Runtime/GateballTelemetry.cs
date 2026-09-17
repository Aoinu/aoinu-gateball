using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballTelemetry : UdonSharpBehaviour
    {
        public const int MaxEventCount = 128;
        public const int MaxSampleCount = 512;

        public const int EventBallCollision = 1;
        public const int EventGateCrossing = 2;
        public const int EventGatePostCollision = 3;
        public const int EventOut = 4;
        public const int EventSettled = 5;

        [Header("Recording")]
        public bool RecordSamples = true;

        [System.NonSerialized] public string ClientIdentity = "Local";
        [System.NonSerialized] public bool IsOwner;
        [System.NonSerialized] public int ShotId = -1;
        [System.NonSerialized] public int SimulationStep;
        [System.NonSerialized] public float FixedDeltaTime;
        [System.NonSerialized] public float ElapsedSimulationTime;
        [System.NonSerialized] public float ElapsedWallClockTime;
        [System.NonSerialized] public int SampleCount;
        [System.NonSerialized] public int EventCount;
        [System.NonSerialized] public int[] EventSequence = new int[MaxEventCount];
        [System.NonSerialized] public Vector3[] LastPositions = new Vector3[GateballGeometry.BallCount];
        [System.NonSerialized] public Vector3[] LastVelocities = new Vector3[GateballGeometry.BallCount];
        [System.NonSerialized] public Vector3[] SamplePositions = new Vector3[MaxSampleCount * GateballGeometry.BallCount];
        [System.NonSerialized] public Vector3[] SampleVelocities = new Vector3[MaxSampleCount * GateballGeometry.BallCount];
        [System.NonSerialized] public int[] SampleSteps = new int[MaxSampleCount];
        [System.NonSerialized] public float[] SamplePhysicsTimes = new float[MaxSampleCount];
        [System.NonSerialized] public float[] SampleWallClockTimes = new float[MaxSampleCount];

        [System.NonSerialized] public float MaxPositionError;
        [System.NonSerialized] public float MeanPositionError;
        [System.NonSerialized] public float FinalPositionError;
        [System.NonSerialized] public float SettleTimeError;
        [System.NonSerialized] public int SettleStepError;
        [System.NonSerialized] public float FinalCorrectionDistance;
        [System.NonSerialized] public int EventDivergenceCount;

        private float _wallClockStart;
        private int _sampleWriteIndex;

        public void _BeginShot(int shotId, bool isOwner)
        {
            ShotId = shotId;
            IsOwner = isOwner;
            SimulationStep = 0;
            FixedDeltaTime = Time.fixedDeltaTime;
            ElapsedSimulationTime = 0f;
            _wallClockStart = Time.time;
            ElapsedWallClockTime = 0f;
            SampleCount = 0;
            _sampleWriteIndex = 0;
            EventCount = 0;
            _ClearEventSequence();
            MaxPositionError = 0f;
            MeanPositionError = 0f;
            FinalPositionError = 0f;
            SettleTimeError = 0f;
            SettleStepError = 0;
            FinalCorrectionDistance = 0f;
            EventDivergenceCount = 0;

            if (Networking.LocalPlayer != null)
            {
                ClientIdentity = Networking.LocalPlayer.displayName;
            }
        }

        public void _RecordFixedStep(GateballBall[] balls)
        {
            if (ShotId < 0)
            {
                return;
            }

            SimulationStep++;
            FixedDeltaTime = Time.fixedDeltaTime;
            ElapsedSimulationTime += Time.fixedDeltaTime;
            ElapsedWallClockTime = Time.time - _wallClockStart;

            if (!RecordSamples || balls == null)
            {
                return;
            }

            int sampleIndex = _sampleWriteIndex;
            SampleSteps[sampleIndex] = SimulationStep;
            SamplePhysicsTimes[sampleIndex] = ElapsedSimulationTime;
            SampleWallClockTimes[sampleIndex] = ElapsedWallClockTime;
            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                GateballBall ball = _FindBall(balls, i + 1);
                int flattenedIndex = sampleIndex * GateballGeometry.BallCount + i;
                if (ball == null || ball.Body == null)
                {
                    SamplePositions[flattenedIndex] = Vector3.zero;
                    SampleVelocities[flattenedIndex] = Vector3.zero;
                    LastPositions[i] = Vector3.zero;
                    LastVelocities[i] = Vector3.zero;
                    continue;
                }

                SamplePositions[flattenedIndex] = ball.Body.position;
                SampleVelocities[flattenedIndex] = ball.Body.velocity;
                LastPositions[i] = ball.Body.position;
                LastVelocities[i] = ball.Body.velocity;
            }

            _sampleWriteIndex++;
            if (_sampleWriteIndex >= MaxSampleCount)
            {
                _sampleWriteIndex = 0;
            }

            if (SampleCount < MaxSampleCount)
            {
                SampleCount++;
            }
        }

        public void _RecordEvent(int eventType, int ballId, int targetId, int gateIndex)
        {
            if (EventCount >= MaxEventCount)
            {
                return;
            }

            EventSequence[EventCount] = EncodeEvent(eventType, ballId, targetId, gateIndex);
            EventCount++;
        }

        public void _RecordFinalComparison(
            Vector3[] localPositions,
            Vector3[] ownerPositions,
            int strokeBallId,
            int ownerSimulationStep,
            float ownerElapsedSimulationTime,
            int[] ownerEvents,
            int ownerEventCount)
        {
            MaxPositionError = CalculateMaxPositionError(localPositions, ownerPositions);
            MeanPositionError = CalculateMeanPositionError(localPositions, ownerPositions);
            FinalPositionError = CalculateFinalPositionError(localPositions, ownerPositions, strokeBallId);
            FinalCorrectionDistance = MaxPositionError;
            SettleTimeError = Mathf.Abs(ElapsedSimulationTime - ownerElapsedSimulationTime);
            SettleStepError = Mathf.Abs(SimulationStep - ownerSimulationStep);
            EventDivergenceCount = CalculateEventDivergenceCount(EventSequence, EventCount, ownerEvents, ownerEventCount);
        }

        public static int EncodeEvent(int eventType, int ballId, int targetId, int gateIndex)
        {
            return eventType * 1000000 + ballId * 10000 + (targetId + 1) * 100 + gateIndex + 1;
        }

        public static float CalculateMaxPositionError(Vector3[] localPositions, Vector3[] ownerPositions)
        {
            float maxError = 0f;
            int count = _GetPositionCount(localPositions, ownerPositions);
            for (int i = 0; i < count; i++)
            {
                maxError = Mathf.Max(maxError, Vector3.Distance(localPositions[i], ownerPositions[i]));
            }

            return maxError;
        }

        public static float CalculateMeanPositionError(Vector3[] localPositions, Vector3[] ownerPositions)
        {
            int count = _GetPositionCount(localPositions, ownerPositions);
            if (count == 0)
            {
                return 0f;
            }

            float totalError = 0f;
            for (int i = 0; i < count; i++)
            {
                totalError += Vector3.Distance(localPositions[i], ownerPositions[i]);
            }

            return totalError / count;
        }

        public static float CalculateFinalPositionError(Vector3[] localPositions, Vector3[] ownerPositions, int strokeBallId)
        {
            int index = GateballGeometry.BallIdToIndex(strokeBallId);
            if (index < 0 || localPositions == null || ownerPositions == null
                || index >= localPositions.Length || index >= ownerPositions.Length)
            {
                return 0f;
            }

            return Vector3.Distance(localPositions[index], ownerPositions[index]);
        }

        public static int CalculateEventDivergenceCount(int[] localEvents, int localCount, int[] ownerEvents, int ownerCount)
        {
            int safeLocalCount = _GetEventCount(localEvents, localCount);
            int safeOwnerCount = _GetEventCount(ownerEvents, ownerCount);
            int sharedCount = Mathf.Min(safeLocalCount, safeOwnerCount);
            int divergence = Mathf.Abs(safeLocalCount - safeOwnerCount);
            for (int i = 0; i < sharedCount; i++)
            {
                if (localEvents[i] != ownerEvents[i])
                {
                    divergence++;
                }
            }

            return divergence;
        }

        private static int _GetPositionCount(Vector3[] first, Vector3[] second)
        {
            if (first == null || second == null)
            {
                return 0;
            }

            return Mathf.Min(GateballGeometry.BallCount, Mathf.Min(first.Length, second.Length));
        }

        private static int _GetEventCount(int[] events, int count)
        {
            if (events == null)
            {
                return 0;
            }

            return Mathf.Clamp(count, 0, Mathf.Min(events.Length, MaxEventCount));
        }

        private GateballBall _FindBall(GateballBall[] balls, int ballId)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null && balls[i].BallId == ballId)
                {
                    return balls[i];
                }
            }

            return null;
        }

        private void _ClearEventSequence()
        {
            for (int i = 0; i < MaxEventCount; i++)
            {
                EventSequence[i] = 0;
            }
        }
    }
}
