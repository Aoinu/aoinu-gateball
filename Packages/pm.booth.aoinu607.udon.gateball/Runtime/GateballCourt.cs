using UdonSharp;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballCourt : UdonSharpBehaviour
    {
        [Header("Court")]
        public float CourtWidth = GateballGeometry.CourtWidth;
        public float CourtLength = GateballGeometry.CourtLength;
        public float OutMargin = GateballGeometry.DefaultOutMargin;

        [Header("Scene References")]
        public GateballBall[] Balls;
        public GateballGate[] Gates;
        public GateballGoalPole GoalPole;
        public GateballTelemetry Telemetry;

        [Header("Debug")]
        public bool DebugMode;

        [System.NonSerialized] public int LastStrokeBallId = -1;
        [System.NonSerialized] public int LastStoppedBallId = -1;
        [System.NonSerialized] public Vector3 LastStrokeDirection;
        [System.NonSerialized] public float LastStrokeImpulse;

        private Vector3[] _previousPositions = new Vector3[GateballGeometry.BallCount];
        private int[] _gateProgress = new int[GateballGeometry.BallCount];
        private bool[] _outStates = new bool[GateballGeometry.BallCount];
        private bool[] _goalPoleHits = new bool[GateballGeometry.BallCount];
        private int[] _lastTouchTargetIds = new int[GateballGeometry.BallCount];
        private int[] _touchCounts = new int[GateballGeometry.BallCount];
        private int[] _gatePostCollisionCounts = new int[GateballGeometry.BallCount];
        private int[] _boundaryCollisionCounts = new int[GateballGeometry.BallCount];
        private int[] _goalPoleCollisionCounts = new int[GateballGeometry.BallCount];
        private int[] _invalidGatePassCounts = new int[GateballGeometry.BallCount];
        private int[] _reverseGatePassCounts = new int[GateballGeometry.BallCount];
        private bool _initialized;

        private void Start()
        {
            _EnsureStateArrays();
            _ClearState();

            if (Balls == null)
            {
                Balls = GetComponentsInChildren<GateballBall>(true);
            }

            if (Gates == null)
            {
                Gates = GetComponentsInChildren<GateballGate>(true);
            }

            if (GoalPole == null)
            {
                GoalPole = GetComponentInChildren<GateballGoalPole>(true);
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball == null || !GateballGeometry.IsValidBallId(ball.BallId))
                {
                    continue;
                }

                ball.Court = this;
                int index = GateballGeometry.BallIdToIndex(ball.BallId);
                _previousPositions[index] = ball.transform.position;
            }

            _initialized = true;
        }

        private void FixedUpdate()
        {
            if (!_initialized || Balls == null)
            {
                return;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball == null || !GateballGeometry.IsValidBallId(ball.BallId))
                {
                    continue;
                }

                int ballIndex = GateballGeometry.BallIdToIndex(ball.BallId);
                Vector3 currentPosition = transform.InverseTransformPoint(ball.transform.position);
                if (!_outStates[ballIndex]
                    && GateballGeometry.IsOutOfCourt(currentPosition, CourtWidth, CourtLength, OutMargin))
                {
                    _outStates[ballIndex] = true;
                    ball._StopForOut();
                }

                _EvaluateGateCrossings(ball, ballIndex, _previousPositions[ballIndex], ball.transform.position);
                _EvaluateGoalPoleContact(ball, ballIndex);
                _previousPositions[ballIndex] = ball.transform.position;
            }

            if (Telemetry != null)
            {
                Telemetry._RecordFixedStep(Balls);
            }
        }

        public void _RegisterBall(GateballBall ball)
        {
            if (ball == null || !GateballGeometry.IsValidBallId(ball.BallId))
            {
                return;
            }

            ball.Court = this;
            int index = GateballGeometry.BallIdToIndex(ball.BallId);
            _previousPositions[index] = ball.transform.position;
        }

        public void _NotifyStroke(GateballBall ball, Vector3 direction, float impulse)
        {
            if (ball == null)
            {
                return;
            }

            LastStrokeBallId = ball.BallId;
            LastStrokeDirection = direction;
            LastStrokeImpulse = impulse;
        }

        public void _NotifyBallStopped(GateballBall ball)
        {
            if (ball != null)
            {
                LastStoppedBallId = ball.BallId;
                if (Telemetry != null)
                {
                    Telemetry._RecordEvent(GateballTelemetry.EventSettled, ball.BallId, -1, -1);
                }
            }
        }

        public void _RegisterTouch(GateballBall striker, GateballBall target)
        {
            if (striker == null || target == null || striker == target)
            {
                return;
            }

            int index = GateballGeometry.BallIdToIndex(striker.BallId);
            if (index < 0)
            {
                return;
            }

            _touchCounts[index]++;
            _lastTouchTargetIds[index] = target.BallId;
            if (Telemetry != null)
            {
                Telemetry._RecordEvent(GateballTelemetry.EventBallCollision, striker.BallId, target.BallId, -1);
            }
        }

        public void _RegisterGatePostCollision(GateballBall ball, int gateIndex)
        {
            int index = _GetBallIndex(ball);
            if (index < 0 || gateIndex < 0 || gateIndex >= GateballGeometry.GateCount)
            {
                return;
            }

            _gatePostCollisionCounts[index]++;
            if (Telemetry != null)
            {
                Telemetry._RecordEvent(GateballTelemetry.EventGatePostCollision, ball.BallId, -1, gateIndex);
            }
        }

        public void _RegisterBoundaryCollision(GateballBall ball)
        {
            int index = _GetBallIndex(ball);
            if (index >= 0)
            {
                _boundaryCollisionCounts[index]++;
                _outStates[index] = true;
                if (Telemetry != null)
                {
                    Telemetry._RecordEvent(GateballTelemetry.EventOut, ball.BallId, -1, -1);
                }

                ball._StopForOut();
            }
        }

        public void _RegisterGoalPoleContact(GateballBall ball)
        {
            int index = _GetBallIndex(ball);
            if (index < 0)
            {
                return;
            }

            if (!_goalPoleHits[index])
            {
                _goalPoleHits[index] = true;
                _goalPoleCollisionCounts[index]++;
            }
        }

        public void _ResetAll()
        {
            if (Balls == null)
            {
                return;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                if (Balls[i] != null)
                {
                    Balls[i]._ResetBall();
                }
            }

            _ClearState();
            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball != null && GateballGeometry.IsValidBallId(ball.BallId))
                {
                    _previousPositions[GateballGeometry.BallIdToIndex(ball.BallId)] = ball.transform.position;
                }
            }
        }

        public void _SetBallPosition(int ballId, Vector3 worldPosition)
        {
            GateballBall ball = _GetBall(ballId);
            if (ball == null)
            {
                return;
            }

            ball._SetPosition(worldPosition);
            _previousPositions[GateballGeometry.BallIdToIndex(ballId)] = worldPosition;
        }

        public void _CaptureBallPositions(Vector3[] destination)
        {
            if (destination == null || Balls == null)
            {
                return;
            }

            int count = Mathf.Min(GateballGeometry.BallCount, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = Vector3.zero;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball == null || !GateballGeometry.IsValidBallId(ball.BallId))
                {
                    continue;
                }

                int index = GateballGeometry.BallIdToIndex(ball.BallId);
                if (index >= 0 && index < count)
                {
                    destination[index] = ball.Body == null ? ball.transform.position : ball.Body.position;
                }
            }
        }

        public void _ApplyBallPositions(Vector3[] positions)
        {
            if (positions == null || Balls == null)
            {
                return;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball == null || !GateballGeometry.IsValidBallId(ball.BallId))
                {
                    continue;
                }

                int index = GateballGeometry.BallIdToIndex(ball.BallId);
                if (index >= 0 && index < positions.Length)
                {
                    ball._SetPosition(positions[index]);
                }
            }
        }

        public bool _HasAnyBallMotion()
        {
            if (Balls == null)
            {
                return false;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball != null && ball.Body != null
                    && !GateballGeometry.IsStopped(
                        ball.Body.velocity,
                        ball.Body.angularVelocity,
                        ball.Radius,
                        ball.StopLinearSpeed,
                        ball.StopAngularTipSpeed))
                {
                    return true;
                }
            }

            return false;
        }

        public bool _AreAllBallsStopped()
        {
            if (Balls == null)
            {
                return true;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball != null && ball.Body != null
                    && !GateballGeometry.IsStopped(
                        ball.Body.velocity,
                        ball.Body.angularVelocity,
                        ball.Radius,
                        ball.StopLinearSpeed,
                        ball.StopAngularTipSpeed))
                {
                    return false;
                }
            }

            return true;
        }

        public int _GetMovingBallCount()
        {
            if (Balls == null)
            {
                return 0;
            }

            int movingCount = 0;
            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball != null && ball.Body != null
                    && !GateballGeometry.IsStopped(
                        ball.Body.velocity,
                        ball.Body.angularVelocity,
                        ball.Radius,
                        ball.StopLinearSpeed,
                        ball.StopAngularTipSpeed))
                {
                    movingCount++;
                }
            }

            return movingCount;
        }

        public float _GetMaxLinearSpeed()
        {
            if (Balls == null)
            {
                return 0f;
            }

            float maxSpeed = 0f;
            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball != null && ball.Body != null)
                {
                    maxSpeed = Mathf.Max(maxSpeed, ball.Body.velocity.magnitude);
                }
            }

            return maxSpeed;
        }

        public float _GetMaxAngularTipSpeed()
        {
            if (Balls == null)
            {
                return 0f;
            }

            float maxSpeed = 0f;
            for (int i = 0; i < Balls.Length; i++)
            {
                GateballBall ball = Balls[i];
                if (ball != null && ball.Body != null)
                {
                    maxSpeed = Mathf.Max(maxSpeed, ball.Body.angularVelocity.magnitude * ball.Radius);
                }
            }

            return maxSpeed;
        }

        public GateballBall _GetBall(int ballId)
        {
            if (!GateballGeometry.IsValidBallId(ballId) || Balls == null)
            {
                return null;
            }

            for (int i = 0; i < Balls.Length; i++)
            {
                if (Balls[i] != null && Balls[i].BallId == ballId)
                {
                    return Balls[i];
                }
            }

            return null;
        }

        public int _GetGateProgress(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 ? _gateProgress[index] : -1;
        }

        public bool _IsOut(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 && _outStates[index];
        }

        public bool _HasGoalPoleHit(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 && _goalPoleHits[index];
        }

        public int _GetTouchTargetId(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 ? _lastTouchTargetIds[index] : -1;
        }

        public int _GetTouchCount(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 ? _touchCounts[index] : 0;
        }

        public int _GetGatePostCollisionCount(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 ? _gatePostCollisionCounts[index] : 0;
        }

        public int _GetBoundaryCollisionCount(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 ? _boundaryCollisionCounts[index] : 0;
        }

        public int _GetGoalPoleCollisionCount(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            return index >= 0 ? _goalPoleCollisionCounts[index] : 0;
        }

        public void _ResetBallState(GateballBall ball)
        {
            int index = _GetBallIndex(ball);
            if (index < 0)
            {
                return;
            }

            _gateProgress[index] = 0;
            _outStates[index] = false;
            _goalPoleHits[index] = false;
            _lastTouchTargetIds[index] = -1;
            _touchCounts[index] = 0;
            _gatePostCollisionCounts[index] = 0;
            _boundaryCollisionCounts[index] = 0;
            _goalPoleCollisionCounts[index] = 0;
            _invalidGatePassCounts[index] = 0;
            _reverseGatePassCounts[index] = 0;
            _previousPositions[index] = ball.transform.position;
        }

        private void _EvaluateGateCrossings(
            GateballBall ball,
            int ballIndex,
            Vector3 previousPosition,
            Vector3 currentPosition)
        {
            if (Gates == null)
            {
                return;
            }

            for (int i = 0; i < Gates.Length; i++)
            {
                GateballGate gate = Gates[i];
                if (gate == null || gate.GateIndex < 0 || gate.GateIndex >= GateballGeometry.GateCount)
                {
                    continue;
                }

                Vector3 crossingPoint;
                if (GateballGeometry.TryGetGateCrossing(
                    previousPosition,
                    currentPosition,
                    gate.transform.position,
                    gate.transform.forward,
                    gate.transform.right,
                    gate.OpeningWidth,
                    gate.OpeningHeight,
                    gate.BallRadius,
                    true,
                    out crossingPoint))
                {
                    if (gate.GateIndex == _gateProgress[ballIndex])
                    {
                        _gateProgress[ballIndex] = gate.GateIndex + 1;
                    }
                    else
                    {
                        _invalidGatePassCounts[ballIndex]++;
                    }

                    if (Telemetry != null)
                    {
                        Telemetry._RecordEvent(GateballTelemetry.EventGateCrossing, ball.BallId, 1, gate.GateIndex);
                    }
                }

                if (GateballGeometry.TryGetGateCrossing(
                    previousPosition,
                    currentPosition,
                    gate.transform.position,
                    gate.transform.forward,
                    gate.transform.right,
                    gate.OpeningWidth,
                    gate.OpeningHeight,
                    gate.BallRadius,
                    false,
                    out crossingPoint))
                {
                    _reverseGatePassCounts[ballIndex]++;
                    if (Telemetry != null)
                    {
                        Telemetry._RecordEvent(GateballTelemetry.EventGateCrossing, ball.BallId, 0, gate.GateIndex);
                    }
                }
            }
        }

        private void _EvaluateGoalPoleContact(GateballBall ball, int ballIndex)
        {
            if (_goalPoleHits[ballIndex] || GoalPole == null)
            {
                return;
            }

            float ballRadius = ball.Radius;
            if (GateballGeometry.IsBallTouchingVerticalPole(
                ball.transform.position,
                GoalPole.transform.position,
                ballRadius,
                GoalPole.Radius,
                GoalPole.Height))
            {
                _goalPoleHits[ballIndex] = true;
                _goalPoleCollisionCounts[ballIndex]++;
            }
        }

        private int _GetBallIndex(GateballBall ball)
        {
            return ball == null ? -1 : GateballGeometry.BallIdToIndex(ball.BallId);
        }

        private void _EnsureStateArrays()
        {
            if (_previousPositions == null || _previousPositions.Length != GateballGeometry.BallCount)
            {
                _previousPositions = new Vector3[GateballGeometry.BallCount];
            }

            if (_gateProgress == null || _gateProgress.Length != GateballGeometry.BallCount)
            {
                _gateProgress = new int[GateballGeometry.BallCount];
            }

            if (_outStates == null || _outStates.Length != GateballGeometry.BallCount)
            {
                _outStates = new bool[GateballGeometry.BallCount];
            }

            if (_goalPoleHits == null || _goalPoleHits.Length != GateballGeometry.BallCount)
            {
                _goalPoleHits = new bool[GateballGeometry.BallCount];
            }

            if (_lastTouchTargetIds == null || _lastTouchTargetIds.Length != GateballGeometry.BallCount)
            {
                _lastTouchTargetIds = new int[GateballGeometry.BallCount];
            }

            if (_touchCounts == null || _touchCounts.Length != GateballGeometry.BallCount)
            {
                _touchCounts = new int[GateballGeometry.BallCount];
            }

            if (_gatePostCollisionCounts == null || _gatePostCollisionCounts.Length != GateballGeometry.BallCount)
            {
                _gatePostCollisionCounts = new int[GateballGeometry.BallCount];
            }

            if (_boundaryCollisionCounts == null || _boundaryCollisionCounts.Length != GateballGeometry.BallCount)
            {
                _boundaryCollisionCounts = new int[GateballGeometry.BallCount];
            }

            if (_goalPoleCollisionCounts == null || _goalPoleCollisionCounts.Length != GateballGeometry.BallCount)
            {
                _goalPoleCollisionCounts = new int[GateballGeometry.BallCount];
            }

            if (_invalidGatePassCounts == null || _invalidGatePassCounts.Length != GateballGeometry.BallCount)
            {
                _invalidGatePassCounts = new int[GateballGeometry.BallCount];
            }

            if (_reverseGatePassCounts == null || _reverseGatePassCounts.Length != GateballGeometry.BallCount)
            {
                _reverseGatePassCounts = new int[GateballGeometry.BallCount];
            }
        }

        private void _ClearState()
        {
            LastStrokeBallId = -1;
            LastStoppedBallId = -1;
            LastStrokeDirection = Vector3.zero;
            LastStrokeImpulse = 0f;

            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                _gateProgress[i] = 0;
                _outStates[i] = false;
                _goalPoleHits[i] = false;
                _lastTouchTargetIds[i] = -1;
                _touchCounts[i] = 0;
                _gatePostCollisionCounts[i] = 0;
                _boundaryCollisionCounts[i] = 0;
                _goalPoleCollisionCounts[i] = 0;
                _invalidGatePassCounts[i] = 0;
                _reverseGatePassCounts[i] = 0;
            }
        }
    }
}
