using UdonSharp;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    public class GateballGate : UdonSharpBehaviour
    {
        public int GateIndex;
        public float OpeningWidth = GateballGeometry.GateOpeningWidth;
        public float OpeningHeight = GateballGeometry.GateOpeningHeight;
        public float BallRadius = GateballGeometry.BallRadius;
    }
}
