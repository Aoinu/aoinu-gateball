using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballDebugDisplay : UdonSharpBehaviour
    {
        public GateballNetworkState NetworkState;
        public GateballTelemetry Telemetry;
        public Text Text;
        public bool Visible = true;

        private void Start()
        {
            if (Text == null)
            {
                Text = GetComponent<Text>();
            }
        }

        private void Update()
        {
            if (Text == null || NetworkState == null || !Visible)
            {
                return;
            }

            string role = NetworkState._IsLocalOwner() ? "Owner" : "Remote";
            string client = Telemetry == null ? "Local" : Telemetry.ClientIdentity;
            Text.text = "Gateball v0.2\n"
                + "client " + client + " / " + role + "\n"
                + "phase " + NetworkState._GetPhaseName() + " / shot " + NetworkState.ShotId.ToString() + "\n"
                + "stroke ball " + NetworkState.StrokeBallId.ToString() + " impulse " + NetworkState.StrokeImpulse.ToString() + "\n"
                + "step " + NetworkState.LocalSimulationStep.ToString() + " fixed " + NetworkState.LocalFixedDeltaTime.ToString() + "\n"
                + "correction " + NetworkState.LastFinalCorrectionDistance.ToString() + "\n"
                + "trajectory max " + NetworkState.LastMaxTrajectoryPositionError.ToString()
                + " mean " + NetworkState.LastMeanTrajectoryPositionError.ToString() + "\n"
                + "final max " + NetworkState.LastMaxFinalPositionError.ToString()
                + " mean " + NetworkState.LastMeanFinalPositionError.ToString()
                + " stroke " + NetworkState.LastFinalPositionError.ToString() + "\n"
                + "event divergence " + NetworkState.LastEventDivergenceCount.ToString();
        }
    }
}
