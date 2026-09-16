using UdonSharp;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballMallet : UdonSharpBehaviour
    {
        public GateballStrokeRouter StrokeRouter;
        public Transform Head;
        public Collider PhysicsProxy;
        public float ProxyRadius = 0.09f;
        public int BallLayer = 13;
        public float StrikeScale = 0.45f;
        public float MinimumImpulse = 0.15f;
        public float MaximumImpulse = 7f;
        public float HitCooldown = 0.18f;

        private const int HistorySize = 6;
        private Vector3[] _historyPositions = new Vector3[HistorySize];
        private float[] _historyTimes = new float[HistorySize];
        private int _historyCount;
        private int _historyIndex;
        private Vector3 _lastPosition;
        private bool _hasPreviousPosition;
        private int _lastHitBallId = -1;
        private float _lastHitTime = -100f;

        private void Start()
        {
            if (Head == null)
            {
                Head = transform;
            }

            _ResetHistory();
        }

        public override void OnPickup()
        {
            _ResetHistory();
        }

        public override void OnDrop()
        {
            _ResetHistory();
        }

        private void Update()
        {
            if (Head == null)
            {
                return;
            }

            Vector3 currentPosition = Head.position;
            if (!_hasPreviousPosition)
            {
                _lastPosition = currentPosition;
                _RecordPosition(currentPosition);
                _hasPreviousPosition = true;
                return;
            }

            _TrySweepHit(_lastPosition, currentPosition);
            _RecordPosition(currentPosition);
            _lastPosition = currentPosition;
        }

        private void _TrySweepHit(Vector3 previousPosition, Vector3 currentPosition)
        {
            if (PhysicsProxy == null || !PhysicsProxy.enabled)
            {
                return;
            }

            Vector3 delta = currentPosition - previousPosition;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
            {
                return;
            }

            RaycastHit hit;
            int layerMask = 1 << BallLayer;
            if (!Physics.SphereCast(
                previousPosition,
                ProxyRadius,
                delta.normalized,
                out hit,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore))
            {
                return;
            }

            GateballBall ball = hit.collider.GetComponent<GateballBall>();
            if (ball == null || ball.BallId == _lastHitBallId && Time.time - _lastHitTime < HitCooldown)
            {
                return;
            }

            Vector3 malletVelocity = _CalculateSmoothedVelocity();
            float speed = malletVelocity.magnitude;
            if (speed < 0.05f)
            {
                return;
            }

            Vector3 direction = GateballGeometry.NormalizeStrokeDirection(malletVelocity);
            float impulse = GateballGeometry.CalculateMalletImpulse(
                speed,
                StrikeScale,
                MinimumImpulse,
                MaximumImpulse);

            if (StrokeRouter != null)
            {
                StrokeRouter._ApplyStrokeToBall(ball, direction, impulse);
                _lastHitBallId = ball.BallId;
                _lastHitTime = Time.time;
            }
        }

        private Vector3 _CalculateSmoothedVelocity()
        {
            if (_historyCount == 0 || Head == null)
            {
                return Vector3.zero;
            }

            int oldestIndex = (_historyIndex - _historyCount + HistorySize) % HistorySize;
            float elapsed = Time.time - _historyTimes[oldestIndex];
            if (elapsed < 0.0001f)
            {
                return Vector3.zero;
            }

            return (Head.position - _historyPositions[oldestIndex]) / elapsed;
        }

        private void _RecordPosition(Vector3 position)
        {
            _historyPositions[_historyIndex] = position;
            _historyTimes[_historyIndex] = Time.time;
            _historyIndex = (_historyIndex + 1) % HistorySize;
            if (_historyCount < HistorySize)
            {
                _historyCount++;
            }
        }

        private void _ResetHistory()
        {
            _historyCount = 0;
            _historyIndex = 0;
            _hasPreviousPosition = false;
            _lastHitBallId = -1;
            _lastHitTime = -100f;
        }
    }
}
