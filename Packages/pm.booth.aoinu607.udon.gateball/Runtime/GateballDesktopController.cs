using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballDesktopController : UdonSharpBehaviour
    {
        public GateballStrokeRouter StrokeRouter;
        public int SelectedBallId = 1;
        public float AimYawDegrees;
        public float Power = GateballGeometry.NormalStrokeImpulse;
        public float AimSensitivity = 3f;
        public float PowerSensitivity = 0.08f;
        public float MinimumPower = GateballGeometry.WeakStrokeImpulse;
        public float MaximumPower = GateballGeometry.StrongStrokeImpulse;

        public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
        {
            if (InputManager.IsUsingHandController())
            {
                return;
            }

            AimYawDegrees += value * AimSensitivity;
        }

        public override void InputMoveVertical(float value, UdonInputEventArgs args)
        {
            if (InputManager.IsUsingHandController())
            {
                return;
            }

            Power = Mathf.Clamp(Power + value * PowerSensitivity, MinimumPower, MaximumPower);
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value || InputManager.IsUsingHandController())
            {
                return;
            }

            _Stroke(Power);
        }

        public override void Interact()
        {
            if (!InputManager.IsUsingHandController())
            {
                _Stroke(Power);
            }
        }

        public void _StrokeWeak()
        {
            _Stroke(GateballGeometry.WeakStrokeImpulse);
        }

        public void _StrokeNormal()
        {
            _Stroke(GateballGeometry.NormalStrokeImpulse);
        }

        public void _StrokeStrong()
        {
            _Stroke(GateballGeometry.StrongStrokeImpulse);
        }

        public void _NextBall()
        {
            SelectedBallId++;
            if (SelectedBallId > GateballGeometry.BallCount)
            {
                SelectedBallId = 1;
            }
        }

        public void _PreviousBall()
        {
            SelectedBallId--;
            if (SelectedBallId < 1)
            {
                SelectedBallId = GateballGeometry.BallCount;
            }
        }

        public void _ResetAimAndPower()
        {
            AimYawDegrees = 0f;
            Power = GateballGeometry.NormalStrokeImpulse;
        }

        private void _Stroke(float impulse)
        {
            if (StrokeRouter == null)
            {
                return;
            }

            Vector3 direction = Quaternion.AngleAxis(AimYawDegrees, Vector3.up) * Vector3.forward;
            StrokeRouter._ApplyStrokeById(SelectedBallId, direction, impulse);
        }
    }
}
