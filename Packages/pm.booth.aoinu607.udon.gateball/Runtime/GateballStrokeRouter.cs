using UdonSharp;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballStrokeRouter : UdonSharpBehaviour
    {
        public GateballCourt Court;
        public float MaximumImpulse = GateballGeometry.StrongStrokeImpulse;

        public void _ApplyStrokeById(int ballId, Vector3 direction, float impulse)
        {
            if (Court == null)
            {
                return;
            }

            _ApplyStrokeToBall(Court._GetBall(ballId), direction, impulse);
        }

        public void _ApplyStrokeToBall(GateballBall ball, Vector3 direction, float impulse)
        {
            if (ball == null)
            {
                return;
            }

            ball._ApplyStroke(direction, Mathf.Clamp(impulse, 0f, MaximumImpulse));
        }
    }
}
