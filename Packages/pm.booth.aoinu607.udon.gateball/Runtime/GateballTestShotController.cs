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
        public int SelectedPreset;
        public bool DebugMode;

        private const int PresetCount = 7;

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
            switch (SelectedPreset)
            {
                case 0:
                    _LoadStraightWeak();
                    break;
                case 1:
                    _LoadStraightStrong();
                    break;
                case 2:
                    _LoadFrontCollision();
                    break;
                case 3:
                    _LoadGateCenter();
                    break;
                case 4:
                    _LoadOutBoundary();
                    break;
                case 5:
                    _LoadTouch();
                    break;
                case 6:
                    _LoadGoalPole();
                    break;
            }
        }

        public void _LoadStraightWeak()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, 1.5f);
        }

        public void _LoadStraightStrong()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, 5.5f);
        }

        public void _LoadFrontCollision()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-0.55f, GateballGeometry.BallRadius, -5.5f));
            _ResetAndPlaceBall(2, new Vector3(0.55f, GateballGeometry.BallRadius, -5.5f));
            _Stroke(1, Vector3.right, 2.2f);
        }

        public void _LoadGateCenter()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, -7.5f));
            _Stroke(1, Vector3.forward, 3.2f);
        }

        public void _LoadOutBoundary()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(6.5f, GateballGeometry.BallRadius, 0f));
            _Stroke(1, Vector3.right, 3.5f);
        }

        public void _LoadTouch()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(-1f, GateballGeometry.BallRadius, -5.5f));
            _ResetAndPlaceBall(2, new Vector3(0f, GateballGeometry.BallRadius, -5.5f));
            _Stroke(1, Vector3.right, 1.4f);
        }

        public void _LoadGoalPole()
        {
            _PreparePreset();
            _ResetAndPlaceBall(1, new Vector3(0f, GateballGeometry.BallRadius, 7.0f));
            _Stroke(1, Vector3.forward, 1.8f);
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
