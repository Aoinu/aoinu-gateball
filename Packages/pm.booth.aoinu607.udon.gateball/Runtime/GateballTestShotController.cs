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
        [Header("Validation matrix")]
        public bool AutoRunMatrix;
        public int MatrixRunCount = 3;
        public int MatrixFirstPreset;
        public int MatrixLastPreset = 11;
        public int MatrixPresetMask = 4095;
        public float MatrixInterShotDelaySeconds = 2f;

        private const int PresetCount = 12;
        private int _matrixRunIndex;
        private int _matrixPresetIndex;
        private int _matrixShotIdBefore;
        private bool _matrixShotActive;
        private bool _matrixShotStarted;

        private void Start()
        {
            if (AutoRunMatrix)
            {
                SendCustomEventDelayedSeconds("_StartMatrix", AutoRunDelaySeconds);
            }
            else if (AutoRunOnStart)
            {
                SendCustomEventDelayedSeconds("_AutoRun", AutoRunDelaySeconds);
            }
        }

        public void _StartMatrix()
        {
            if (!AutoRunMatrix)
            {
                return;
            }

            if (!_IsAutoRunTarget())
            {
                SendCustomEventDelayedSeconds("_StartMatrix", 1f);
                return;
            }

            _matrixRunIndex = 0;
            _matrixPresetIndex = Mathf.Clamp(MatrixFirstPreset, 0, PresetCount - 1);
            _matrixShotActive = false;
            _RunNextMatrixShot();
        }

        public void _PollMatrix()
        {
            if (!AutoRunMatrix || !_IsAutoRunTarget())
            {
                return;
            }

            if (NetworkState != null && NetworkState.Phase == GateballNetworkState.PhaseSimulating)
            {
                _matrixShotStarted = true;
                SendCustomEventDelayedSeconds("_PollMatrix", 0.5f);
                return;
            }

            if (!_matrixShotStarted
                && (NetworkState == null || NetworkState.ShotId == _matrixShotIdBefore))
            {
                SendCustomEventDelayedSeconds("_PollMatrix", 0.5f);
                return;
            }

            _matrixShotStarted = true;

            if (_matrixShotActive)
            {
                Debug.Log("[Gateball v0.2] MatrixSettled run=" + (_matrixRunIndex + 1).ToString()
                    + " preset=" + _matrixPresetIndex.ToString()
                    + " forced=" + (NetworkState != null && NetworkState.LastShotEndWasForced).ToString());
                _matrixShotActive = false;
                _matrixPresetIndex++;
                SendCustomEventDelayedSeconds("_AdvanceMatrixAfterDelay", MatrixInterShotDelaySeconds);
                return;
            }

            _RunNextMatrixShot();
        }

        public void _AdvanceMatrixAfterDelay()
        {
            if (AutoRunMatrix && _IsAutoRunTarget())
            {
                _RunNextMatrixShot();
            }
        }

        public void _AutoRun()
        {
            if (!AutoRunOnStart)
            {
                return;
            }

            if (!_IsAutoRunTarget())
            {
                SendCustomEventDelayedSeconds("_AutoRun", 1f);
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

        private void _RunNextMatrixShot()
        {
            while (_matrixRunIndex < Mathf.Max(0, MatrixRunCount)
                && _matrixPresetIndex <= Mathf.Clamp(MatrixLastPreset, 0, PresetCount - 1)
                && !_IsPresetIncluded(_matrixPresetIndex))
            {
                _matrixPresetIndex++;
            }

            if (_matrixRunIndex >= Mathf.Max(0, MatrixRunCount))
            {
                Debug.Log("[Gateball v0.2] MatrixComplete");
                return;
            }

            if (_matrixPresetIndex > Mathf.Clamp(MatrixLastPreset, 0, PresetCount - 1))
            {
                _matrixRunIndex++;
                _matrixPresetIndex = Mathf.Clamp(MatrixFirstPreset, 0, PresetCount - 1);
                _RunNextMatrixShot();
                return;
            }

            SelectedPreset = _matrixPresetIndex;
            _matrixShotActive = true;
            _matrixShotStarted = false;
            _matrixShotIdBefore = NetworkState == null ? -1 : NetworkState.ShotId;
            Debug.Log("[Gateball v0.2] MatrixShot run=" + (_matrixRunIndex + 1).ToString()
                + " preset=" + SelectedPreset.ToString());
            _RunSelectedPreset();
            SendCustomEventDelayedSeconds("_PollMatrix", 0.5f);
        }

        private bool _IsPresetIncluded(int preset)
        {
            return preset >= 0 && preset < 32 && (MatrixPresetMask & (1 << preset)) != 0;
        }

        private bool _IsAutoRunTarget()
        {
            if (Networking.LocalPlayer == null)
            {
                return false;
            }

            bool isTargetPlayer = AutoRunPlayerId > 0
                && Networking.LocalPlayer.playerId == AutoRunPlayerId;
            bool isTargetMaster = AutoRunPlayerId <= 0 && Networking.IsMaster;
            return isTargetPlayer || isTargetMaster;
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
