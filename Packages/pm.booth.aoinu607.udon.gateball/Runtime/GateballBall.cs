using UdonSharp;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballBall : UdonSharpBehaviour
    {
        [Header("Identity")]
        public int BallId = 1;

        [Header("References")]
        public GateballCourt Court;
        public Rigidbody Body;

        [Header("Physical Dimensions")]
        public float Radius = GateballGeometry.BallRadius;

        [Header("Stopping")]
        public float StopLinearSpeed = 0.03f;
        public float StopAngularTipSpeed = 0.02f;
        public float StopDelay = 0.15f;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _stillTime;
        private bool _hasMoved;
        private bool _initialized;

        public int Team
        {
            get { return GateballGeometry.IsRedBall(BallId) ? 0 : 1; }
        }

        public bool _IsReady()
        {
            return _initialized && Body != null;
        }

        private void Start()
        {
            if (Body == null)
            {
                Body = GetComponent<Rigidbody>();
            }

            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _stillTime = 0f;
            _hasMoved = false;
            _initialized = Body != null && GateballGeometry.IsValidBallId(BallId);

            if (Court != null)
            {
                Court._RegisterBall(this);
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized || Body == null)
            {
                return;
            }

            bool stopped = GateballGeometry.IsStopped(
                Body.velocity,
                Body.angularVelocity,
                Radius,
                StopLinearSpeed,
                StopAngularTipSpeed);

            if (!stopped)
            {
                _hasMoved = true;
                _stillTime = 0f;
                return;
            }

            if (!_hasMoved)
            {
                return;
            }

            _stillTime += Time.fixedDeltaTime;
            if (_stillTime >= StopDelay)
            {
                _StopAndNotify();
            }
        }

        public void _ApplyStroke(Vector3 direction, float impulse)
        {
            if (!_initialized || Body == null || impulse <= 0f)
            {
                return;
            }

            Vector3 safeDirection = GateballGeometry.NormalizeStrokeDirection(direction);
            Body.WakeUp();
            Body.velocity += safeDirection * (impulse / Mathf.Max(0.0001f, Body.mass));
            _hasMoved = true;
            _stillTime = 0f;

            if (Court != null)
            {
                Court._NotifyStroke(this, safeDirection, impulse);
            }
        }

        public void _ResetBall()
        {
            if (Body == null)
            {
                return;
            }

            Body.position = _initialPosition;
            Quaternion resetRotation = _initialRotation;
            float rotationMagnitude = resetRotation.x * resetRotation.x
                + resetRotation.y * resetRotation.y
                + resetRotation.z * resetRotation.z
                + resetRotation.w * resetRotation.w;
            if (rotationMagnitude < 0.5f)
            {
                resetRotation = Quaternion.identity;
            }

            Body.rotation = resetRotation;
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.Sleep();
            _stillTime = 0f;
            _hasMoved = false;

            if (Court != null)
            {
                Court._ResetBallState(this);
            }
        }

        public void _SetPosition(Vector3 position)
        {
            if (Body == null)
            {
                return;
            }

            Body.position = position;
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.Sleep();
            _stillTime = 0f;
            _hasMoved = false;

            if (Court != null)
            {
                Court._ResetBallState(this);
            }
        }

        public void _StopForOut()
        {
            if (!_initialized || Body == null)
            {
                return;
            }

            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.Sleep();
            _stillTime = 0f;
            _hasMoved = false;

            if (Court != null)
            {
                Court._NotifyBallStopped(this);
            }
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (Court == null || collision == null || collision.gameObject == null)
            {
                return;
            }

            GameObject other = collision.gameObject;
            GateballBall otherBall = other.GetComponent<GateballBall>();
            if (otherBall != null && otherBall != this)
            {
                Court._RegisterTouch(this, otherBall);
            }

            if (other.GetComponent<GateballGatePost>() != null)
            {
                GateballGatePost post = other.GetComponent<GateballGatePost>();
                Court._RegisterGatePostCollision(this, post.GateIndex);
            }

            if (other.GetComponent<GateballGoalPole>() != null)
            {
                Court._RegisterGoalPoleContact(this);
            }

            if (other.GetComponent<GateballBoundary>() != null)
            {
                Court._RegisterBoundaryCollision(this);
            }
        }

        private void _StopAndNotify()
        {
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.Sleep();
            _stillTime = 0f;
            _hasMoved = false;

            if (Court != null)
            {
                Court._NotifyBallStopped(this);
            }
        }
    }
}
