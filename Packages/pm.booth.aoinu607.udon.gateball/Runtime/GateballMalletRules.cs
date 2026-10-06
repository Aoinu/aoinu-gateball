using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    public static class GateballMalletRules
    {
        public static Vector3 EstimateWindowVelocity(
            Vector3[] positions,
            float[] times,
            int nextIndex,
            int count,
            float now,
            float windowSeconds)
        {
            if (positions == null || times == null || positions.Length == 0 || positions.Length != times.Length
                || count < 2 || count > positions.Length || !(windowSeconds > 0f))
            {
                return Vector3.zero;
            }

            int oldestIndex = (nextIndex - count) % positions.Length;
            if (oldestIndex < 0)
            {
                oldestIndex += positions.Length;
            }

            int newestIndex = (nextIndex - 1) % positions.Length;
            if (newestIndex < 0)
            {
                newestIndex += positions.Length;
            }

            float oldestTime = times[oldestIndex];
            float newestTime = times[newestIndex];
            if (!(newestTime > oldestTime) || now < newestTime)
            {
                return Vector3.zero;
            }

            float targetTime = now - windowSeconds;
            float startTime = oldestTime;
            Vector3 startPosition = positions[oldestIndex];
            if (targetTime > oldestTime)
            {
                int previousIndex = oldestIndex;
                float previousTime = oldestTime;
                Vector3 previousPosition = positions[previousIndex];
                bool foundWindowStart = false;

                for (int i = 1; i < count; i++)
                {
                    int currentIndex = (oldestIndex + i) % positions.Length;
                    float currentTime = times[currentIndex];
                    Vector3 currentPosition = positions[currentIndex];
                    if (currentTime >= targetTime)
                    {
                        float sampleDuration = currentTime - previousTime;
                        if (!(sampleDuration > 0f))
                        {
                            return Vector3.zero;
                        }

                        float interpolation = Mathf.Clamp01((targetTime - previousTime) / sampleDuration);
                        startPosition = Vector3.Lerp(previousPosition, currentPosition, interpolation);
                        startTime = targetTime;
                        foundWindowStart = true;
                        break;
                    }

                    previousIndex = currentIndex;
                    previousTime = currentTime;
                    previousPosition = currentPosition;
                }

                if (!foundWindowStart)
                {
                    return Vector3.zero;
                }
            }

            float elapsed = now - startTime;
            if (!(elapsed > 0.001f))
            {
                return Vector3.zero;
            }

            Vector3 velocity = (positions[newestIndex] - startPosition) / elapsed;
            return _IsFiniteVector(velocity) ? velocity : Vector3.zero;
        }

        public static bool TryBuildShotVelocities(
            Vector3 lockedAimDirection,
            Vector3 swingVelocity,
            Vector3 groundNormal,
            float deadzoneSpeed,
            float ballSpeedScale,
            float maximumBallSpeed,
            float aimAssistStrength,
            float maximumAimDeviationDegrees,
            float invalidSwingAngleDegrees,
            float ballRadius,
            float rollingFactor,
            out Vector3 initialLinearVelocity,
            out Vector3 initialAngularVelocity)
        {
            initialLinearVelocity = Vector3.zero;
            initialAngularVelocity = Vector3.zero;
            if (!_IsFiniteVector(lockedAimDirection)
                || !_IsFiniteVector(swingVelocity)
                || !_IsFiniteVector(groundNormal)
                || !(groundNormal.sqrMagnitude > 0.000001f)
                || !(ballRadius > 0.0001f)
                || !(deadzoneSpeed >= 0f)
                || !(ballSpeedScale > 0f)
                || !(maximumBallSpeed > 0f)
                || !(maximumAimDeviationDegrees >= 0f)
                || !(invalidSwingAngleDegrees > 0f))
            {
                return false;
            }

            Vector3 up = groundNormal.normalized;
            Vector3 lockedDirection = Vector3.ProjectOnPlane(lockedAimDirection, up);
            Vector3 horizontalSwing = Vector3.ProjectOnPlane(swingVelocity, up);
            if (!(lockedDirection.sqrMagnitude > 0.000001f)
                || !(horizontalSwing.sqrMagnitude >= deadzoneSpeed * deadzoneSpeed))
            {
                return false;
            }

            float speed = horizontalSwing.magnitude;
            if (!(speed > 0f) || speed < deadzoneSpeed || speed > 10000f)
            {
                return false;
            }

            lockedDirection = lockedDirection.normalized;
            Vector3 swingDirection = horizontalSwing.normalized;
            float errorAngle = Vector3.SignedAngle(lockedDirection, swingDirection, up);
            if (!(Mathf.Abs(errorAngle) <= 180f) || Mathf.Abs(errorAngle) >= invalidSwingAngleDegrees)
            {
                return false;
            }

            float correctedError = errorAngle * (1f - Mathf.Clamp01(aimAssistStrength));
            correctedError = Mathf.Clamp(
                correctedError,
                -maximumAimDeviationDegrees,
                maximumAimDeviationDegrees);
            Vector3 finalDirection = Quaternion.AngleAxis(correctedError, up) * lockedDirection;
            float ballSpeed = Mathf.Min(speed * ballSpeedScale, maximumBallSpeed);
            if (!(ballSpeed > 0f) || ballSpeed > 10000f || !_IsFiniteVector(finalDirection))
            {
                return false;
            }

            initialLinearVelocity = Vector3.ProjectOnPlane(finalDirection.normalized * ballSpeed, up);
            initialAngularVelocity = GateballGeometry.CalculateRollingAngularVelocity(
                initialLinearVelocity,
                up,
                ballRadius,
                rollingFactor);
            if (!_IsFiniteVector(initialLinearVelocity) || !_IsFiniteVector(initialAngularVelocity))
            {
                initialLinearVelocity = Vector3.zero;
                initialAngularVelocity = Vector3.zero;
                return false;
            }

            return true;
        }

        public static void SolveSingleHandPose(
            Vector3 gripPosition,
            Quaternion gripRotation,
            Vector3 gripLocalPosition,
            Quaternion gripLocalRotation,
            out Vector3 malletPosition,
            out Quaternion malletRotation)
        {
            malletRotation = gripRotation * Quaternion.Inverse(gripLocalRotation);
            malletPosition = gripPosition - malletRotation * gripLocalPosition;
        }

        public static float CalculateAverageTwistDegrees(
            Quaternion neutralPrimaryRotation,
            Quaternion currentPrimaryRotation,
            Quaternion neutralSecondaryRotation,
            Quaternion currentSecondaryRotation,
            Vector3 shaftDirection)
        {
            if (!(shaftDirection.sqrMagnitude > 0.000001f))
            {
                return 0f;
            }

            Vector3 axis = shaftDirection.normalized;
            float primaryTwist = _CalculateTwistDegrees(neutralPrimaryRotation, currentPrimaryRotation, axis);
            float secondaryTwist = _CalculateTwistDegrees(neutralSecondaryRotation, currentSecondaryRotation, axis);
            float primaryRadians = primaryTwist * Mathf.Deg2Rad;
            float secondaryRadians = secondaryTwist * Mathf.Deg2Rad;
            float sine = Mathf.Sin(primaryRadians) + Mathf.Sin(secondaryRadians);
            float cosine = Mathf.Cos(primaryRadians) + Mathf.Cos(secondaryRadians);
            if (!(sine * sine + cosine * cosine > 0.000001f))
            {
                return Mathf.DeltaAngle(primaryTwist, secondaryTwist) * 0.5f + primaryTwist;
            }

            return Mathf.Atan2(sine, cosine) * Mathf.Rad2Deg;
        }

        public static float ResolveTwoHandRollDegrees(
            bool isAimLocked,
            float lockedRollDegrees,
            Quaternion neutralPrimaryRotation,
            Quaternion currentPrimaryRotation,
            Quaternion neutralSecondaryRotation,
            Quaternion currentSecondaryRotation,
            Vector3 shaftDirection)
        {
            return isAimLocked
                ? lockedRollDegrees
                : CalculateAverageTwistDegrees(
                    neutralPrimaryRotation,
                    currentPrimaryRotation,
                    neutralSecondaryRotation,
                    currentSecondaryRotation,
                    shaftDirection);
        }

        public static Quaternion SolveTwoHandRotation(
            Quaternion neutralMalletRotation,
            Vector3 neutralShaftDirection,
            Vector3 currentShaftDirection,
            float rollDegrees)
        {
            if (!(neutralShaftDirection.sqrMagnitude > 0.000001f)
                || !(currentShaftDirection.sqrMagnitude > 0.000001f))
            {
                return neutralMalletRotation;
            }

            Vector3 currentAxis = currentShaftDirection.normalized;
            Quaternion shaftRotation = Quaternion.FromToRotation(
                neutralShaftDirection.normalized,
                currentAxis);
            Quaternion alignedRotation = shaftRotation * neutralMalletRotation;
            return Quaternion.AngleAxis(rollDegrees, currentAxis) * alignedRotation;
        }

        private static float _CalculateTwistDegrees(
            Quaternion neutralRotation,
            Quaternion currentRotation,
            Vector3 axis)
        {
            Quaternion delta = currentRotation * Quaternion.Inverse(neutralRotation);
            Vector3 vectorPart = new Vector3(delta.x, delta.y, delta.z);
            float axialPart = Vector3.Dot(vectorPart, axis);
            if (!(Mathf.Abs(axialPart) > 0.000001f) && !(Mathf.Abs(delta.w) > 0.000001f))
            {
                return 0f;
            }

            float angle = 2f * Mathf.Atan2(axialPart, delta.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, angle);
        }

        private static bool _IsFiniteVector(Vector3 value)
        {
            return Mathf.Abs(value.x) < 100000f
                && Mathf.Abs(value.y) < 100000f
                && Mathf.Abs(value.z) < 100000f;
        }
    }
}
