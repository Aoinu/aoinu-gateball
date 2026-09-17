using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballTestShotController : UdonSharpBehaviour
    {
        public GateballCourt Court;
        public GateballStrokeRouter StrokeRouter;
        public GateballNetworkState NetworkState;
        public int SelectedPreset;
        public bool DebugMode;
        public bool AutoRunOnStart;
        public int AutoRunPlayerId = 1;
        public float AutoRunDelaySeconds = 2f;

        private const int PresetCount = 12;

        private void Start()
        {
            if (AutoRunOnStart)
            {
                SendCustomEventDelayedSeconds("_AutoRun", AutoRunDelaySeconds);
            }
        }

        public void _AutoRun()
        {
            if (!AutoRunOnStart || Networking.LocalPlayer == null)
            {
                return;
            }

            bool isTargetPlayer = AutoRunPlayerId > 0
                && Networking.LocalPlayer.playerId == AutoRunPlayerId;
            bool isTargetMaster = AutoRunPlayerId <= 0 && Networking.IsMaster;
            if (!isTargetPlayer && !isTargetMaster)
            {
                return;
            }

            _RunSelectedPreset();
        }

        public override void Interact()
        {
            SelectedPreset++;
            if (SelectedPreset >= PresetCount)
            {
                SelectedPreset = 0;
            }

            _RunSelectedPreset();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (value && !InputManager.IsUsingHandController())
            {
                _RunSelectedPreset();
            }
        }

        public void _RunSelectedPreset()
        {
            if (NetworkState != null && NetworkState.Phase == GateballNetworkState.PhaseSimulating)
            {
                return;
            }

            switch (SelectedPreset)
            {
                case 0:
                    _LoadStraightWeak();
                    break;
                case 1:
                    _LoadStraightNormal();
                    break;
                case 2:
                    _LoadStraightStrong();
                    break;
                case 3:
                    _LoadFrontCollision();
                    break;
                case 4:
                    _LoadAngleCollision();
                    break;
                case 5:
                    _LoadDoubleCollision();
                    break;
                case 6:
                    _LoadMultiBallCollision();
                    break;
                case 7:
                    _LoadGateCenter();
                    break;
                case 8:
                    _LoadGateEdge();
                    break;
                case 9:
                    _LoadGatePost();
                    break;
                case 10:
                    _LoadLongRoll();
                    break;
                case 11:
                    _LoadVeryLowSpeedTouch();
                    break;
            }
        }

        public void _LoadStraightWeak()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, GateballGeometry.WeakStrokeImpulse);
        }

        public void _LoadStraightNormal()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, GateballGeometry.NormalStrokeImpulse);
        }

        public void _LoadStraightStrong()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, GateballGeometry.StrongStrokeImpulse);
        }

        public void _LoadFrontCollision()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-0.55f, GateballGeometry.BallRadius, -5.5f));
            _ResetAndPlaceBall(2, new Vector3(0.55f, GateballGeometry.BallRadius, -5.5f));
            _Stroke(1, Vector3.right, GateballGeometry.NormalStrokeImpulse);
        }

        public void _LoadAngleCollision()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-0.75f, GateballGeometry.BallRadius, -6.2f));
            _ResetAndPlaceBall(2, new Vector3(0f, GateballGeometry.BallRadius, -5.2f));
            _Stroke(1, new Vector3(0.6f, 0f, 0.8f), GateballGeometry.NormalStrokeImpulse);
        }

        public void _LoadDoubleCollision()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-1.0f, GateballGeometry.BallRadius, -6.0f));
            _ResetAndPlaceBall(2, new Vector3(-0.25f, GateballGeometry.BallRadius, -6.0f));
            _ResetAndPlaceBall(3, new Vector3(0.50f, GateballGeometry.BallRadius, -6.0f));
            _Stroke(1, Vector3.right, GateballGeometry.StrongStrokeImpulse);
        }

        public void _LoadMultiBallCollision()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-1.2f, GateballGeometry.BallRadius, -6.0f));
            _ResetAndPlaceBall(2, new Vector3(-0.4f, GateballGeometry.BallRadius, -6.0f));
            _ResetAndPlaceBall(3, new Vector3(0.4f, GateballGeometry.BallRadius, -6.0f));
            _ResetAndPlaceBall(4, new Vector3(1.2f, GateballGeometry.BallRadius, -6.0f));
            _Stroke(1, Vector3.right, GateballGeometry.StrongStrokeImpulse);
        }

        public void _LoadGateCenter()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, GateballGeometry.NormalStrokeImpulse);
        }

        public void _LoadGateEdge()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0.071f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, GateballGeometry.NormalStrokeImpulse);
        }

        public void _LoadGatePost()
        {
            _PreparePreset();
            float postCenterOffset = GateballGeometry.GateOpeningWidth * 0.5f + GateballGeometry.GatePostDiameter * 0.5f;
            _ResetAndPlaceBall(1, new Vector3(postCenterOffset, GateballGeometry.BallRadius, -5.5f));
            _Stroke(1, Vector3.forward, GateballGeometry.NormalStrokeImpulse);
        }

        public void _LoadLongRoll()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -8.0f));
            _Stroke(1, Vector3.forward, GateballGeometry.StrongStrokeImpulse);
        }

        public void _LoadVeryLowSpeedTouch()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-0.20f, GateballGeometry.BallRadius, -5.5f));
            _ResetAndPlaceBall(2, new Vector3(-0.125f, GateballGeometry.BallRadius, -5.5f));
            _Stroke(1, Vector3.right, GateballGeometry.WeakStrokeImpulse);
        }

        public void _ResetAll()
        {
            if (Court != null)
            {
                Court._ResetAll();
            }
        }

        private void _ResetAndPlaceBall(int ballId, Vector3 position)
        {
            if (Court == null)
            {
                return;
            }

            Court._SetBallPosition(ballId, position);
        }

        private void _PreparePreset()
        {
            if (Court != null)
            {
                Court._ResetAll();
                if (NetworkState != null)
                {
                    for (int i = 0; i < GateballGeometry.BallCount; i++)
                    {
                        int column = i % 5;
                        int row = i / 5;
                        _ResetAndPlaceBall(
                            i + 1,
                            new Vector3(-5.0f + column * 2.5f, GateballGeometry.BallRadius, -8.5f + row * 0.6f));
                    }
                }
            }
        }

        private void _Stroke(int ballId, Vector3 direction, float impulse)
        {
            if (StrokeRouter != null)
            {
                StrokeRouter._ApplyStrokeById(ballId, direction, impulse);
            }
        }
    }
}
