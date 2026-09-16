using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    public static class GateballGeometry
    {
        public const int BallCount = 10;
        public const int GateCount = 3;

        public static bool IsValidBallId(int ballId)
        {
            return ballId >= 1 && ballId <= BallCount;
        }

        public static int BallIdToIndex(int ballId)
        {
            return IsValidBallId(ballId) ? ballId - 1 : -1;
        }

        public static bool IsRedBall(int ballId)
        {
            return IsValidBallId(ballId) && (ballId & 1) == 1;
        }

        public static float SignedPlaneDistance(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            return Vector3.Dot(point - planePoint, planeNormal.normalized);
        }

        public static bool TryGetGateCrossing(
            Vector3 previousPosition,
            Vector3 currentPosition,
            Vector3 gateCenter,
            Vector3 gateForward,
            Vector3 gateRight,
            float openingWidth,
            float openingHeight,
            float ballRadius,
            bool forwardPass,
            out Vector3 crossingPoint)
        {
            crossingPoint = Vector3.zero;

            Vector3 forward = gateForward.normalized;
            Vector3 right = gateRight.normalized;
            float previousDistance = SignedPlaneDistance(previousPosition, gateCenter, forward);
            float currentDistance = SignedPlaneDistance(currentPosition, gateCenter, forward);

            bool crossed = forwardPass
                ? previousDistance < 0f && currentDistance >= 0f
                : previousDistance > 0f && currentDistance <= 0f;

            if (!crossed)
            {
                return false;
            }

            float denominator = previousDistance - currentDistance;
            if (Mathf.Abs(denominator) < 0.000001f)
            {
                return false;
            }

            float interpolation = Mathf.Clamp01(previousDistance / denominator);
            crossingPoint = Vector3.Lerp(previousPosition, currentPosition, interpolation);

            float halfWidth = openingWidth * 0.5f - ballRadius;
            if (halfWidth <= 0f || openingHeight <= ballRadius)
            {
                return false;
            }

            float horizontalOffset = Vector3.Dot(crossingPoint - gateCenter, right);
            float verticalOffset = crossingPoint.y - gateCenter.y;
            return Mathf.Abs(horizontalOffset) <= halfWidth
                && verticalOffset >= -ballRadius
                && verticalOffset <= openingHeight + ballRadius;
        }

        public static bool IsOutOfCourt(Vector3 localPosition, float courtWidth, float courtLength, float margin)
        {
            return Mathf.Abs(localPosition.x) > courtWidth * 0.5f + margin
                || Mathf.Abs(localPosition.z) > courtLength * 0.5f + margin
                || localPosition.y < -margin;
        }

        public static bool IsBallTouchingBall(
            Vector3 firstPosition,
            Vector3 secondPosition,
            float firstRadius,
            float secondRadius)
        {
            float contactRadius = firstRadius + secondRadius;
            return (firstPosition - secondPosition).sqrMagnitude <= contactRadius * contactRadius;
        }

        public static bool IsBallTouchingVerticalPole(
            Vector3 ballPosition,
            Vector3 poleCenter,
            float ballRadius,
            float poleRadius,
            float poleHeight)
        {
            Vector3 offset = ballPosition - poleCenter;
            float contactRadius = ballRadius + poleRadius;
            return Mathf.Abs(offset.y) <= poleHeight * 0.5f + ballRadius
                && offset.x * offset.x + offset.z * offset.z <= contactRadius * contactRadius;
        }

        public static bool IsStopped(
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            float ballRadius,
            float linearSpeedThreshold,
            float angularTipSpeedThreshold)
        {
            float angularTipSpeed = angularVelocity.magnitude * ballRadius;
            return linearVelocity.magnitude <= linearSpeedThreshold
                && angularTipSpeed <= angularTipSpeedThreshold;
        }

        public static Vector3 NormalizeStrokeDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.000001f)
            {
                return Vector3.forward;
            }

            return direction.normalized;
        }

        public static float CalculateMalletImpulse(
            float malletSpeed,
            float strikeScale,
            float minimumImpulse,
            float maximumImpulse)
        {
            return Mathf.Clamp(malletSpeed * strikeScale, minimumImpulse, maximumImpulse);
        }
    }
}
