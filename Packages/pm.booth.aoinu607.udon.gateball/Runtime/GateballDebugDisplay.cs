using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballDebugDisplay : UdonSharpBehaviour
    {
        public GateballNetworkState NetworkState;
        public GateballGameplayState Gameplay;
        public GateballTelemetry Telemetry;
        public Text Text;
        public bool Visible = true;

        private void Start()
        {
            if (Text == null)
            {
                Text = GetComponent<Text>();
            }

            if (Gameplay == null && NetworkState != null)
            {
                Gameplay = NetworkState.GetComponent<GateballGameplayState>();
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
            string gameplayText = Gameplay == null
                ? "gameplay unavailable"
                : "mode " + Gameplay._GetModeName()
                    + " / phase " + Gameplay._GetGameplayPhaseName() + "\n"
                    + "current ball " + Gameplay.CurrentBallId.ToString()
                    + " / player " + Gameplay._GetCurrentControllerPlayerId().ToString() + "\n"
                    + "score red " + Gameplay.RedScore.ToString()
                    + " / white " + Gameplay.WhiteScore.ToString() + "\n"
                    + "ball progress " + Gameplay._GetCurrentProgress().ToString()
                    + " / out " + Gameplay.LastOutBallId.ToString() + "\n"
                    + "touch " + Gameplay.DidTouch.ToString()
                    + " -> " + Gameplay.TouchedBallId.ToString()
                    + " / spark " + Gameplay.SparkTargetBallId.ToString();
            Text.text = "Gateball v0.3\n"
                + "client " + client + " / " + role + "\n"
                + "phase " + NetworkState._GetPhaseName() + " / shot " + NetworkState.ShotId.ToString() + "\n"
                + "stroke ball " + NetworkState.StrokeBallId.ToString() + " impulse " + NetworkState.StrokeImpulse.ToString() + "\n"
                + gameplayText + "\n"
                + "step " + NetworkState.LocalSimulationStep.ToString() + " fixed " + NetworkState.LocalFixedDeltaTime.ToString() + "\n"
                + "correction " + NetworkState.LastFinalCorrectionDistance.ToString() + "\n"
                + "trajectory metrics offline validation only\n"
                + "final max " + NetworkState.LastMaxFinalPositionError.ToString()
                + " mean " + NetworkState.LastMeanFinalPositionError.ToString()
                + " stroke " + NetworkState.LastFinalPositionError.ToString() + "\n"
                + "event divergence " + NetworkState.LastEventDivergenceCount.ToString();
        }
    }
}
