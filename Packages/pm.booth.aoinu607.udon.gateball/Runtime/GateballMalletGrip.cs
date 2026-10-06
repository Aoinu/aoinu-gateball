using UdonSharp;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballMalletGrip : UdonSharpBehaviour
    {
        public GateballMallet Mallet;
        public int GripIndex;

        public override void OnPickup()
        {
            if (Mallet != null)
            {
                Mallet._OnGripPicked(GripIndex);
            }
        }

        public override void OnDrop()
        {
            if (Mallet != null)
            {
                Mallet._OnGripDropped(GripIndex);
            }
        }

        public override void OnPickupUseDown()
        {
            if (Mallet != null)
            {
                Mallet._OnGripUseDown(GripIndex);
            }
        }

        public override void OnPickupUseUp()
        {
            if (Mallet != null)
            {
                Mallet._OnGripUseUp(GripIndex);
            }
        }
    }
}
