using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballGameplayPlayModeTests
    {
        private GameObject[] _createdObjects = new GameObject[64];
        private int _createdCount;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _createdCount; i++)
            {
                if (_createdObjects[i] != null)
                {
                    Object.Destroy(_createdObjects[i]);
                }
            }

            yield return null;
            _createdCount = 0;
        }

        [UnityTest]
        public IEnumerator GatePassResolvesScoreAndAdvancesTurn()
        {
            GateballCourt court = CreateCourt();
            GateballBall first = CreateBall(court, 1, new Vector3(0f, 0.10f, -1f));
            GateballBall second = CreateBall(court, 2, new Vector3(2f, GateballGeometry.BallRadius, 0f));
            GateballGate gate = CreateGate(0, Vector3.zero);
            court.Balls = new[] { first, second };
            court.Gates = new[] { gate };
            GateballGameplayState gameplay = CreateGameplay(court);
            gameplay.Mode = GateballGameplayRules.ModeMatch;
            gameplay.GameplayPhase = GateballGameplayRules.PhaseSimulating;
            gameplay.CurrentBallId = 1;
            gameplay.BallControllerPlayerIds[0] = 1;
            gameplay.BallControllerPlayerIds[1] = 2;
            yield return null;

            first.Body.position = new Vector3(0f, 0.10f, 1f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            gameplay._ResolveAuthoritativeShot(1);

            Assert.AreEqual(1, gameplay.BallGateProgress[0]);
            Assert.AreEqual(1, gameplay.RedScore);
            Assert.AreEqual(1, gameplay.CurrentBallId);
            Assert.AreEqual(GateballGameplayRules.PhaseWaitingForStroke, gameplay.GameplayPhase);
        }

        [UnityTest]
        public IEnumerator TouchEntersSparkPlacementAndLocksAdjacentBall()
        {
            GateballCourt court = CreateCourt();
            GateballBall striker = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            GateballBall target = CreateBall(court, 2, new Vector3(0.5f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { striker, target };
            court.Gates = new GateballGate[0];
            GateballGameplayState gameplay = CreateGameplay(court);
            gameplay.Mode = GateballGameplayRules.ModeMatch;
            gameplay.GameplayPhase = GateballGameplayRules.PhaseSimulating;
            gameplay.CurrentBallId = 1;
            yield return null;

            court._RegisterTouch(striker, target);
            gameplay._ResolveAuthoritativeShot(1);

            Assert.IsTrue(gameplay.DidTouch);
            Assert.AreEqual(2, gameplay.SparkTargetBallId);
            Assert.AreEqual(GateballGameplayRules.PhaseSparkPlacement, gameplay.GameplayPhase);

            gameplay._PlaceSparkForward();

            Assert.AreEqual(GateballGameplayRules.PhaseWaitingForSparkStroke, gameplay.GameplayPhase);
            Assert.IsTrue(gameplay.SparkBallLocked);
            Assert.Greater(Vector3.Distance(striker.Body.position, target.Body.position), GateballGeometry.BallDiameter);
        }

        [UnityTest]
        public IEnumerator SparkStrokeUsesShotStartAndTransfersImpulseToTarget()
        {
            GateballCourt court = CreateCourt();
            GateballBall striker = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            GateballBall target = CreateBall(court, 2, new Vector3(0.2f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { striker, target };
            court.Gates = new GateballGate[0];

            GameObject networkObject = CreateObject("NetworkState");
            GateballNetworkState networkState = networkObject.AddComponent<GateballNetworkState>();
            GateballGameplayState gameplay = networkObject.AddComponent<GateballGameplayState>();
            networkState.Court = court;
            networkState.Gameplay = gameplay;
            gameplay.Court = court;
            gameplay.NetworkState = networkState;
            gameplay.Mode = GateballGameplayRules.ModeMatch;
            gameplay.GameplayPhase = GateballGameplayRules.PhaseWaitingForSparkStroke;
            gameplay.CurrentBallId = 1;
            gameplay.SparkTargetBallId = 2;
            gameplay.SparkBallLocked = true;
            yield return null;

            Vector3 strikerPlacement = striker.Body.position;
            networkState._RequestStroke(1, Vector3.forward, GateballGeometry.NormalStrokeImpulse);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(2, networkState.StrokeTargetBallId);
            Assert.AreEqual(GateballGameplayRules.PhaseSimulating, gameplay.GameplayPhase);
            Assert.IsTrue(gameplay.SparkStroke);
            Assert.AreEqual(Vector3.zero, striker.Body.velocity);
            Assert.That(Vector3.Distance(striker.Body.position, strikerPlacement), Is.LessThan(0.0001f));
            Assert.IsTrue(striker.Body.isKinematic);
            Assert.That(networkState.StrokeTargetImpulse, Is.EqualTo(GateballGeometry.NormalStrokeImpulse * 0.75f).Within(0.0001f));
            Assert.Greater(target.Body.velocity.magnitude, 0.1f);
        }

        [UnityTest]
        public IEnumerator SparkStrokeDoesNotResolveStrikerRules()
        {
            GateballCourt court = CreateCourt();
            GateballBall striker = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            GateballBall target = CreateBall(court, 2, new Vector3(0.2f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { striker, target };
            court.Gates = new GateballGate[0];

            GameObject networkObject = CreateObject("NetworkState");
            GateballNetworkState networkState = networkObject.AddComponent<GateballNetworkState>();
            GateballGameplayState gameplay = networkObject.AddComponent<GateballGameplayState>();
            networkState.Court = court;
            networkState.Gameplay = gameplay;
            gameplay.Court = court;
            gameplay.NetworkState = networkState;
            gameplay.Mode = GateballGameplayRules.ModeMatch;
            gameplay.GameplayPhase = GateballGameplayRules.PhaseWaitingForSparkStroke;
            gameplay.CurrentBallId = 1;
            gameplay.SparkTargetBallId = 2;
            gameplay.SparkBallLocked = true;
            gameplay.BallGateProgress[0] = 3;
            gameplay.OnDeserialization();
            yield return null;

            networkState._RequestStroke(1, Vector3.forward, GateballGeometry.NormalStrokeImpulse);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            court._RegisterTouch(striker, target);
            striker.Body.isKinematic = false;
            court._RegisterBoundaryCollision(striker);
            striker.Body.isKinematic = true;
            court._RegisterGoalPoleContact(striker);
            gameplay._ResolveAuthoritativeShot(1);

            Assert.AreEqual(3, gameplay.BallGateProgress[0]);
            Assert.IsFalse(gameplay.BallOutStates[0]);
            Assert.IsFalse(gameplay.BallGoalStates[0]);
            Assert.IsFalse(gameplay.DidTouch);
            Assert.AreEqual(-1, gameplay.TouchedBallId);
            Assert.AreEqual(1, gameplay.CurrentBallId);
            Assert.AreEqual(GateballGameplayRules.PhaseWaitingForStroke, gameplay.GameplayPhase);
        }

        [UnityTest]
        public IEnumerator OutStateIsRecordedAndTurnContinues()
        {
            GateballCourt court = CreateCourt();
            GateballBall first = CreateBall(court, 1, Vector3.zero + Vector3.up * GateballGeometry.BallRadius);
            GateballBall second = CreateBall(court, 2, new Vector3(1f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { first, second };
            court.Gates = new GateballGate[0];
            GateballGameplayState gameplay = CreateGameplay(court);
            gameplay.Mode = GateballGameplayRules.ModeMatch;
            gameplay.GameplayPhase = GateballGameplayRules.PhaseSimulating;
            gameplay.CurrentBallId = 1;
            yield return null;

            court._RegisterBoundaryCollision(first);
            gameplay._ResolveAuthoritativeShot(1);

            Assert.IsTrue(gameplay.BallOutStates[0]);
            Assert.AreEqual(1, gameplay.LastOutBallId);
            Assert.AreEqual(2, gameplay.CurrentBallId);
            Assert.AreEqual(GateballGameplayRules.PhaseWaitingForStroke, gameplay.GameplayPhase);
        }

        [UnityTest]
        public IEnumerator LateJoinGameplaySnapshotAppliesToCourt()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { ball };
            court.Gates = new GateballGate[0];
            GateballGameplayState gameplay = CreateGameplay(court);
            yield return null;

            gameplay.BallGateProgress[0] = 2;
            gameplay.BallOutStates[0] = true;
            gameplay.BallGoalStates[0] = true;
            gameplay.OnDeserialization();

            Assert.AreEqual(2, court._GetGateProgress(1));
            Assert.IsTrue(court._IsOut(1));
            Assert.IsTrue(court._HasGoalPoleHit(1));
        }

        private GateballGameplayState CreateGameplay(GateballCourt court)
        {
            GameObject gameplayObject = CreateObject("GameplayState");
            GateballGameplayState gameplay = gameplayObject.AddComponent<GateballGameplayState>();
            gameplay.Court = court;
            return gameplay;
        }

        private GateballCourt CreateCourt()
        {
            GameObject courtObject = CreateObject("Court");
            GateballCourt court = courtObject.AddComponent<GateballCourt>();
            court.CourtWidth = GateballGeometry.CourtWidth;
            court.CourtLength = GateballGeometry.CourtLength;
            court.Gates = new GateballGate[0];
            return court;
        }

        private GateballBall CreateBall(GateballCourt court, int ballId, Vector3 position)
        {
            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Register(ballObject);
            ballObject.name = "Ball" + ballId.ToString();
            ballObject.transform.position = position;
            ballObject.transform.localScale = Vector3.one * GateballGeometry.BallDiameter;
            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.mass = GateballGeometry.BallMass;
            body.useGravity = false;
            body.drag = 0f;
            GateballBall ball = ballObject.AddComponent<GateballBall>();
            ball.BallId = ballId;
            ball.Court = court;
            ball.Body = body;
            ball.Radius = GateballGeometry.BallRadius;
            return ball;
        }

        private GateballGate CreateGate(int gateIndex, Vector3 position)
        {
            GameObject gateObject = CreateObject("Gate");
            gateObject.transform.position = position;
            GateballGate gate = gateObject.AddComponent<GateballGate>();
            gate.GateIndex = gateIndex;
            gate.OpeningWidth = GateballGeometry.GateOpeningWidth;
            gate.OpeningHeight = GateballGeometry.GateOpeningHeight;
            gate.BallRadius = GateballGeometry.BallRadius;
            return gate;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            Register(gameObject);
            return gameObject;
        }

        private void Register(GameObject gameObject)
        {
            _createdObjects[_createdCount] = gameObject;
            _createdCount++;
        }
    }
}
