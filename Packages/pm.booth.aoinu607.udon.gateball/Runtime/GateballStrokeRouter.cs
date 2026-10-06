using UdonSharp;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballStrokeRouter : UdonSharpBehaviour
    {
        public GateballCourt Court;
        public GateballNetworkState NetworkState;
        public float MaximumImpulse = GateballGeometry.StrongStrokeImpulse;

        public void _ApplyStrokeById(int ballId, Vector3 direction, float impulse)
        {
            if (Court == null)
            {
                return;
            }

            float safeImpulse = Mathf.Clamp(impulse, 0f, MaximumImpulse);
            if (NetworkState != null)
            {
                NetworkState._RequestStroke(ballId, direction, safeImpulse);
                return;
            }

            _ApplyStrokeToBall(Court._GetBall(ballId), direction, safeImpulse);
        }

        public void _ApplyStrokeToBall(GateballBall ball, Vector3 direction, float impulse)
        {
            if (ball == null)
            {
                return;
            }

            ball._ApplyStroke(direction, Mathf.Clamp(impulse, 0f, MaximumImpulse));
        }

        public bool _ApplyShotByVelocity(
            int ballId,
            Vector3 initialLinearVelocity,
            Vector3 initialAngularVelocity)
        {
            if (Court == null || !GateballGeometry.IsValidBallId(ballId))
            {
                return false;
            }

            GateballBall ball = Court._GetBall(ballId);
            if (ball == null || !ball._IsReady()
                || !_IsFiniteVector(initialLinearVelocity)
                || !_IsFiniteVector(initialAngularVelocity)
                || !(initialLinearVelocity.sqrMagnitude > 0.000001f))
            {
                return false;
            }

            float maximumSpeed = MaximumImpulse / Mathf.Max(0.0001f, ball.Body.mass);
            float speed = initialLinearVelocity.magnitude;
            if (!(maximumSpeed > 0f))
            {
                return false;
            }

            if (speed > maximumSpeed)
            {
                float scale = maximumSpeed / speed;
                initialLinearVelocity *= scale;
                initialAngularVelocity *= scale;
            }

            if (NetworkState != null)
            {
                return NetworkState._RequestShotWithVelocities(
                    ballId,
                    initialLinearVelocity,
                    initialAngularVelocity);
            }

            ball._ApplyInitialVelocities(initialLinearVelocity, initialAngularVelocity);
            return true;
        }

        private bool _IsFiniteVector(Vector3 value)
        {
            return Mathf.Abs(value.x) < 100000f
                && Mathf.Abs(value.y) < 100000f
                && Mathf.Abs(value.z) < 100000f;
        }
    }
}
