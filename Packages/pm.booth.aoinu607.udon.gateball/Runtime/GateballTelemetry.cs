using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballTelemetry : UdonSharpBehaviour
    {
        public const int MaxEventCount = 128;
        public const int MaxSampleCount = 4096;

        public const int EventBallCollision = 1;
        public const int EventGateCrossing = 2;
        public const int EventGatePostCollision = 3;
        public const int EventOut = 4;
        public const int EventSettled = 5;

        public const int EventClassificationMissingRemote = 0;
        public const int EventClassificationExtraRemote = 1;
        public const int EventClassificationDifferentType = 2;
        public const int EventClassificationDifferentBall = 3;
        public const int EventClassificationDifferentTarget = 4;
        public const int EventClassificationDifferentGate = 5;
        public const int EventClassificationOrderingOnly = 6;
        public const int EventClassificationDuplicate = 7;
        public const int EventClassificationGameplayCritical = 8;
        public const int EventClassificationDiagnostic = 9;
        public const int EventClassificationCount = 10;

        [Header("Recording")]
        public bool RecordSamples = true;
        public bool LogAllSamples;

        [System.NonSerialized] public string ClientIdentity = "Local";
        [System.NonSerialized] public bool IsOwner;
        [System.NonSerialized] public int ShotId = -1;
        [System.NonSerialized] public int TrackedBallId = -1;
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
        [System.NonSerialized] public float MaxTrajectoryPositionError;
        [System.NonSerialized] public float MeanTrajectoryPositionError;
        [System.NonSerialized] public float MaxFinalPositionError;
        [System.NonSerialized] public float MeanFinalPositionError;
        [System.NonSerialized] public float[] MaxTrajectoryPositionErrors = new float[GateballGeometry.BallCount];
        [System.NonSerialized] public float[] MeanTrajectoryPositionErrors = new float[GateballGeometry.BallCount];
        [System.NonSerialized] public float[] FinalPositionErrors = new float[GateballGeometry.BallCount];
        [System.NonSerialized] public float FinalPositionError;
        [System.NonSerialized] public float SettleTimeError;
        [System.NonSerialized] public int SettleStepError;
        [System.NonSerialized] public float FinalCorrectionDistance;
        [System.NonSerialized] public int EventDivergenceCount;
        [System.NonSerialized] public int MissingRemoteEventCount;
        [System.NonSerialized] public int ExtraRemoteEventCount;
        [System.NonSerialized] public int DifferentEventTypeCount;
        [System.NonSerialized] public int DifferentBallCount;
        [System.NonSerialized] public int DifferentTargetCount;
        [System.NonSerialized] public int DifferentGateCount;
        [System.NonSerialized] public int OrderingOnlyEventDifferenceCount;
        [System.NonSerialized] public int DuplicateEventDifferenceCount;
        [System.NonSerialized] public int GameplayCriticalEventDivergenceCount;
        [System.NonSerialized] public int DiagnosticEventDivergenceCount;

        private float _wallClockStart;
        private int _sampleWriteIndex;

        public void _BeginShot(int shotId, bool isOwner, int trackedBallId)
        {
            ShotId = shotId;
            IsOwner = isOwner;
            TrackedBallId = trackedBallId;
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
            MaxTrajectoryPositionError = 0f;
            MeanTrajectoryPositionError = 0f;
            MaxFinalPositionError = 0f;
            MeanFinalPositionError = 0f;
            FinalPositionError = 0f;
            SettleTimeError = 0f;
            SettleStepError = 0;
            FinalCorrectionDistance = 0f;
            EventDivergenceCount = 0;
            MissingRemoteEventCount = 0;
            ExtraRemoteEventCount = 0;
            DifferentEventTypeCount = 0;
            DifferentBallCount = 0;
            DifferentTargetCount = 0;
            DifferentGateCount = 0;
            OrderingOnlyEventDifferenceCount = 0;
            DuplicateEventDifferenceCount = 0;
            GameplayCriticalEventDivergenceCount = 0;
            DiagnosticEventDivergenceCount = 0;
            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                MaxTrajectoryPositionErrors[i] = 0f;
                MeanTrajectoryPositionErrors[i] = 0f;
                FinalPositionErrors[i] = 0f;
            }

            if (Networking.LocalPlayer != null)
            {
                ClientIdentity = Networking.LocalPlayer.displayName;
            }
        }

        public void _RecordInitialSample(GateballBall[] balls)
        {
            if (ShotId < 0 || !RecordSamples || balls == null)
            {
                return;
            }

            _WriteSample(balls, 0);
            if (SampleCount < MaxSampleCount)
            {
                SampleCount++;
            }

            _sampleWriteIndex = 1;
            _LogTrackedBallSample(0, balls, false);
            if (LogAllSamples)
            {
                _LogAllSamples(0, balls);
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
            _WriteSample(balls, sampleIndex);
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

            if (SimulationStep == 1 || SimulationStep == 10 || SimulationStep == 100)
            {
                _LogTrackedBallSample(SimulationStep, balls, false);
            }

            if (LogAllSamples)
            {
                _LogAllSamples(SimulationStep, balls);
            }
        }

        public void _RecordFinalSample(GateballBall[] balls)
        {
            if (ShotId < 0 || balls == null)
            {
                return;
            }

            _CaptureLastPositions(balls);
            _LogTrackedBallSample(SimulationStep, balls, true);
            if (LogAllSamples)
            {
                _LogAllSamples(SimulationStep, balls);
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
            MaxFinalPositionError = CalculateMaxPositionError(localPositions, ownerPositions);
            MeanFinalPositionError = CalculateMeanPositionError(localPositions, ownerPositions);
            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                if (localPositions == null || ownerPositions == null
                    || i >= localPositions.Length || i >= ownerPositions.Length)
                {
                    FinalPositionErrors[i] = 0f;
                }
                else
                {
                    FinalPositionErrors[i] = Vector3.Distance(localPositions[i], ownerPositions[i]);
                }
            }
            FinalPositionError = CalculateFinalPositionError(localPositions, ownerPositions, strokeBallId);
            FinalCorrectionDistance = MaxFinalPositionError;
            SettleTimeError = Mathf.Abs(ElapsedSimulationTime - ownerElapsedSimulationTime);
            SettleStepError = Mathf.Abs(SimulationStep - ownerSimulationStep);
            EventDivergenceCount = CalculateEventDivergenceCount(EventSequence, EventCount, ownerEvents, ownerEventCount);
            int[] classification = CalculateEventDivergenceClassification(
                EventSequence,
                EventCount,
                ownerEvents,
                ownerEventCount);
            MissingRemoteEventCount = classification[EventClassificationMissingRemote];
            ExtraRemoteEventCount = classification[EventClassificationExtraRemote];
            DifferentEventTypeCount = classification[EventClassificationDifferentType];
            DifferentBallCount = classification[EventClassificationDifferentBall];
            DifferentTargetCount = classification[EventClassificationDifferentTarget];
            DifferentGateCount = classification[EventClassificationDifferentGate];
            OrderingOnlyEventDifferenceCount = classification[EventClassificationOrderingOnly];
            DuplicateEventDifferenceCount = classification[EventClassificationDuplicate];
            GameplayCriticalEventDivergenceCount = classification[EventClassificationGameplayCritical];
            DiagnosticEventDivergenceCount = classification[EventClassificationDiagnostic];
            if (!IsOwner && EventDivergenceCount > 0)
            {
                _LogEventDifferenceDetails(ownerEvents, ownerEventCount);
            }
        }

        public void _RecordTrajectoryComparison(
            int[] ownerSampleSteps,
            Vector3[] ownerSamplePositions,
            int ownerSampleCount,
            Vector3[] ownerFinalPositions)
        {
            MaxTrajectoryPositionError = CalculateTrajectoryMaxPositionError(
                SampleSteps,
                SamplePositions,
                SampleCount,
                ownerSampleSteps,
                ownerSamplePositions,
                ownerSampleCount,
                -1);
            MeanTrajectoryPositionError = CalculateTrajectoryMeanPositionError(
                SampleSteps,
                SamplePositions,
                SampleCount,
                ownerSampleSteps,
                ownerSamplePositions,
                ownerSampleCount,
                -1);
            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                MaxTrajectoryPositionErrors[i] = CalculateTrajectoryMaxPositionError(
                    SampleSteps,
                    SamplePositions,
                    SampleCount,
                    ownerSampleSteps,
                    ownerSamplePositions,
                    ownerSampleCount,
                    i + 1);
                MeanTrajectoryPositionErrors[i] = CalculateTrajectoryMeanPositionError(
                    SampleSteps,
                    SamplePositions,
                    SampleCount,
                    ownerSampleSteps,
                    ownerSamplePositions,
                    ownerSampleCount,
                    i + 1);
            }
            MaxPositionError = MaxTrajectoryPositionError;
            MeanPositionError = MeanTrajectoryPositionError;
            _RecordFinalComparison(
                LastPositions,
                ownerFinalPositions,
                TrackedBallId,
                SimulationStep,
                ElapsedSimulationTime,
                EventSequence,
                EventCount);
        }

        public static int EncodeEvent(int eventType, int ballId, int targetId, int gateIndex)
        {
            return eventType * 1000000 + ballId * 10000 + (targetId + 1) * 100 + gateIndex + 1;
        }

        public static int DecodeEventType(int encodedEvent)
        {
            return encodedEvent / 1000000;
        }

        public static int DecodeEventBallId(int encodedEvent)
        {
            return (encodedEvent % 1000000) / 10000;
        }

        public static int DecodeEventTargetId(int encodedEvent)
        {
            return ((encodedEvent % 10000) / 100) - 1;
        }

        public static int DecodeEventGateIndex(int encodedEvent)
        {
            return (encodedEvent % 100) - 1;
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

        public static float CalculateTrajectoryMaxPositionError(
            int[] localSampleSteps,
            Vector3[] localSamplePositions,
            int localSampleCount,
            int[] ownerSampleSteps,
            Vector3[] ownerSamplePositions,
            int ownerSampleCount,
            int ballId)
        {
            float maxError = 0f;
            int ballStart = ballId < 1 ? 0 : GateballGeometry.BallIdToIndex(ballId);
            if (ballId > GateballGeometry.BallCount || ballStart < 0)
            {
                return 0f;
            }
            int ballEnd = ballId < 1 ? GateballGeometry.BallCount : ballStart + 1;
            int localCount = _GetSampleCount(localSampleSteps, localSamplePositions, localSampleCount);
            int ownerCount = _GetSampleCount(ownerSampleSteps, ownerSamplePositions, ownerSampleCount);
            for (int localIndex = 0; localIndex < localCount; localIndex++)
            {
                int ownerIndex = _FindSampleStep(ownerSampleSteps, ownerCount, localSampleSteps[localIndex]);
                if (ownerIndex < 0)
                {
                    continue;
                }

                for (int ballIndex = ballStart; ballIndex < ballEnd; ballIndex++)
                {
                    int localPositionIndex = localIndex * GateballGeometry.BallCount + ballIndex;
                    int ownerPositionIndex = ownerIndex * GateballGeometry.BallCount + ballIndex;
                    float error = Vector3.Distance(localSamplePositions[localPositionIndex], ownerSamplePositions[ownerPositionIndex]);
                    maxError = Mathf.Max(maxError, error);
                }
            }

            return maxError;
        }

        public static float CalculateTrajectoryMeanPositionError(
            int[] localSampleSteps,
            Vector3[] localSamplePositions,
            int localSampleCount,
            int[] ownerSampleSteps,
            Vector3[] ownerSamplePositions,
            int ownerSampleCount,
            int ballId)
        {
            float totalError = 0f;
            int comparedCount = 0;
            int ballStart = ballId < 1 ? 0 : GateballGeometry.BallIdToIndex(ballId);
            if (ballId > GateballGeometry.BallCount || ballStart < 0)
            {
                return 0f;
            }
            int ballEnd = ballId < 1 ? GateballGeometry.BallCount : ballStart + 1;
            int localCount = _GetSampleCount(localSampleSteps, localSamplePositions, localSampleCount);
            int ownerCount = _GetSampleCount(ownerSampleSteps, ownerSamplePositions, ownerSampleCount);
            for (int localIndex = 0; localIndex < localCount; localIndex++)
            {
                int ownerIndex = _FindSampleStep(ownerSampleSteps, ownerCount, localSampleSteps[localIndex]);
                if (ownerIndex < 0)
                {
                    continue;
                }

                for (int ballIndex = ballStart; ballIndex < ballEnd; ballIndex++)
                {
                    int localPositionIndex = localIndex * GateballGeometry.BallCount + ballIndex;
                    int ownerPositionIndex = ownerIndex * GateballGeometry.BallCount + ballIndex;
                    totalError += Vector3.Distance(localSamplePositions[localPositionIndex], ownerSamplePositions[ownerPositionIndex]);
                    comparedCount++;
                }
            }

            return comparedCount == 0 ? 0f : totalError / comparedCount;
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

        public static int[] CalculateEventDivergenceClassification(
            int[] remoteEvents,
            int remoteCount,
            int[] ownerEvents,
            int ownerCount)
        {
            int[] result = new int[EventClassificationCount];
            int safeRemoteCount = _GetEventCount(remoteEvents, remoteCount);
            int safeOwnerCount = _GetEventCount(ownerEvents, ownerCount);
            bool[] remoteMatched = new bool[MaxEventCount];
            bool[] ownerMatched = new bool[MaxEventCount];

            for (int ownerIndex = 0; ownerIndex < safeOwnerCount; ownerIndex++)
            {
                int remoteIndex = -1;
                for (int candidate = 0; candidate < safeRemoteCount; candidate++)
                {
                    if (!remoteMatched[candidate] && remoteEvents[candidate] == ownerEvents[ownerIndex])
                    {
                        remoteIndex = candidate;
                        break;
                    }
                }

                if (remoteIndex < 0)
                {
                    result[EventClassificationMissingRemote]++;
                    if (_ContainsEvent(remoteEvents, safeRemoteCount, ownerEvents[ownerIndex]))
                    {
                        result[EventClassificationDuplicate]++;
                        result[EventClassificationDiagnostic]++;
                    }
                    else if (_IsGameplayCriticalEventType(DecodeEventType(ownerEvents[ownerIndex])))
                    {
                        result[EventClassificationGameplayCritical]++;
                    }
                    else
                    {
                        result[EventClassificationDiagnostic]++;
                    }
                    continue;
                }

                remoteMatched[remoteIndex] = true;
                ownerMatched[ownerIndex] = true;
                if (remoteIndex != ownerIndex)
                {
                    result[EventClassificationOrderingOnly]++;
                    result[EventClassificationDiagnostic]++;
                }
            }

            for (int remoteIndex = 0; remoteIndex < safeRemoteCount; remoteIndex++)
            {
                if (remoteMatched[remoteIndex])
                {
                    continue;
                }

                result[EventClassificationExtraRemote]++;
                if (_ContainsEvent(ownerEvents, safeOwnerCount, remoteEvents[remoteIndex]))
                {
                    result[EventClassificationDuplicate]++;
                    result[EventClassificationDiagnostic]++;
                }
                else if (_IsGameplayCriticalEventType(DecodeEventType(remoteEvents[remoteIndex])))
                {
                    result[EventClassificationGameplayCritical]++;
                }
                else
                {
                    result[EventClassificationDiagnostic]++;
                }
            }

            int sharedCount = Mathf.Min(safeRemoteCount, safeOwnerCount);
            for (int index = 0; index < sharedCount; index++)
            {
                if (remoteMatched[index] || ownerMatched[index]
                    || remoteEvents[index] == ownerEvents[index])
                {
                    continue;
                }

                int remoteType = DecodeEventType(remoteEvents[index]);
                int ownerType = DecodeEventType(ownerEvents[index]);
                if (remoteType != ownerType)
                {
                    result[EventClassificationDifferentType]++;
                }
                if (DecodeEventBallId(remoteEvents[index]) != DecodeEventBallId(ownerEvents[index]))
                {
                    result[EventClassificationDifferentBall]++;
                }
                if (DecodeEventTargetId(remoteEvents[index]) != DecodeEventTargetId(ownerEvents[index]))
                {
                    result[EventClassificationDifferentTarget]++;
                }
                if (DecodeEventGateIndex(remoteEvents[index]) != DecodeEventGateIndex(ownerEvents[index]))
                {
                    result[EventClassificationDifferentGate]++;
                }

                if (_IsGameplayCriticalEventType(remoteType) || _IsGameplayCriticalEventType(ownerType))
                {
                    result[EventClassificationGameplayCritical]++;
                }
                else
                {
                    result[EventClassificationDiagnostic]++;
                }
            }

            return result;
        }

        private static int _GetPositionCount(Vector3[] first, Vector3[] second)
        {
            if (first == null || second == null)
            {
                return 0;
            }

            return Mathf.Min(GateballGeometry.BallCount, Mathf.Min(first.Length, second.Length));
        }

        private static int _GetSampleCount(int[] steps, Vector3[] positions, int count)
        {
            if (steps == null || positions == null)
            {
                return 0;
            }

            int positionCount = positions.Length / GateballGeometry.BallCount;
            return Mathf.Clamp(count, 0, Mathf.Min(MaxSampleCount, Mathf.Min(steps.Length, positionCount)));
        }

        private static int _FindSampleStep(int[] steps, int count, int targetStep)
        {
            for (int i = 0; i < count; i++)
            {
                if (steps[i] == targetStep)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int _GetEventCount(int[] events, int count)
        {
            if (events == null)
            {
                return 0;
            }

            return Mathf.Clamp(count, 0, Mathf.Min(events.Length, MaxEventCount));
        }

        private static bool _ContainsEvent(int[] events, int count, int encodedEvent)
        {
            if (events == null)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (events[i] == encodedEvent)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool _IsGameplayCriticalEventType(int eventType)
        {
            return eventType == EventBallCollision
                || eventType == EventGateCrossing
                || eventType == EventGatePostCollision
                || eventType == EventOut;
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

        private void _WriteSample(GateballBall[] balls, int sampleIndex)
        {
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
        }

        private void _CaptureLastPositions(GateballBall[] balls)
        {
            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                GateballBall ball = _FindBall(balls, i + 1);
                if (ball == null || ball.Body == null)
                {
                    LastPositions[i] = Vector3.zero;
                    LastVelocities[i] = Vector3.zero;
                    continue;
                }

                LastPositions[i] = ball.Body.position;
                LastVelocities[i] = ball.Body.velocity;
            }
        }

        private void _LogTrackedBallSample(int step, GateballBall[] balls, bool isFinal)
        {
            GateballBall ball = _FindBall(balls, TrackedBallId);
            if (ball == null || ball.Body == null)
            {
                return;
            }

            Debug.Log("[Gateball v0.2] Sample shot=" + ShotId.ToString()
                + " role=" + (IsOwner ? "Owner" : "Remote")
                + " step=" + step.ToString()
                + " fixedDelta=" + FixedDeltaTime.ToString()
                + " ball=" + TrackedBallId.ToString()
                + " pos=" + ball.Body.position.ToString()
                + " vel=" + ball.Body.velocity.ToString()
                + " final=" + isFinal.ToString());
        }

        private void _LogAllSamples(int step, GateballBall[] balls)
        {
            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                GateballBall ball = _FindBall(balls, i + 1);
                if (ball == null || ball.Body == null)
                {
                    continue;
                }

                Vector3 position = ball.Body.position;
                Vector3 velocity = ball.Body.velocity;
                Debug.Log("[Gateball v0.2] SampleAll shot=" + ShotId.ToString()
                    + " role=" + (IsOwner ? "Owner" : "Remote")
                    + " step=" + step.ToString()
                    + " fixedDelta=" + FixedDeltaTime.ToString()
                    + " ball=" + ball.BallId.ToString()
                    + " posX=" + position.x.ToString()
                    + " posY=" + position.y.ToString()
                    + " posZ=" + position.z.ToString()
                    + " velX=" + velocity.x.ToString()
                    + " velY=" + velocity.y.ToString()
                    + " velZ=" + velocity.z.ToString());
            }
        }

        private void _ClearEventSequence()
        {
            for (int i = 0; i < MaxEventCount; i++)
            {
                EventSequence[i] = 0;
            }
        }

        private void _LogEventDifferenceDetails(int[] ownerEvents, int ownerEventCount)
        {
            int safeRemoteCount = _GetEventCount(EventSequence, EventCount);
            int safeOwnerCount = _GetEventCount(ownerEvents, ownerEventCount);
            int sharedCount = Mathf.Min(safeRemoteCount, safeOwnerCount);
            for (int index = 0; index < sharedCount; index++)
            {
                if (EventSequence[index] == ownerEvents[index])
                {
                    continue;
                }

                Debug.Log("[Gateball v0.2] EventDiff shot=" + ShotId.ToString()
                    + " kind=sequenceMismatch index=" + index.ToString()
                    + " remote=" + _DescribeEvent(EventSequence[index])
                    + " owner=" + _DescribeEvent(ownerEvents[index]));
            }

            for (int index = sharedCount; index < safeRemoteCount; index++)
            {
                Debug.Log("[Gateball v0.2] EventDiff shot=" + ShotId.ToString()
                    + " kind=extraRemote index=" + index.ToString()
                    + " remote=" + _DescribeEvent(EventSequence[index]));
            }

            for (int index = sharedCount; index < safeOwnerCount; index++)
            {
                Debug.Log("[Gateball v0.2] EventDiff shot=" + ShotId.ToString()
                    + " kind=missingRemote index=" + index.ToString()
                    + " owner=" + _DescribeEvent(ownerEvents[index]));
            }
        }

        private string _DescribeEvent(int encodedEvent)
        {
            return "type=" + DecodeEventType(encodedEvent).ToString()
                + ",ball=" + DecodeEventBallId(encodedEvent).ToString()
                + ",target=" + DecodeEventTargetId(encodedEvent).ToString()
                + ",gate=" + DecodeEventGateIndex(encodedEvent).ToString();
        }
    }
}
