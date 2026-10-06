using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballPhysicsPlayModeTests
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
        public IEnumerator GateballBallStrokeAndFixedStepStopUseRuntimeImplementation()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { ball };
            yield return null;

            Vector3 start = ball.Body.position;
            ball._ApplyStroke(Vector3.forward, 1.0f);
            yield return new WaitForFixedUpdate();
            Assert.Greater(ball.Body.position.z, start.z);

            ball.Body.velocity = Vector3.zero;
            ball.Body.angularVelocity = Vector3.zero;
            yield return new WaitForSeconds(ball.StopDelay + Time.fixedDeltaTime * 2f);

            Assert.AreEqual(1, court.LastStoppedBallId);
            Assert.AreEqual(Vector3.zero, ball.Body.velocity);
        }

        [UnityTest]
        public IEnumerator BallBallCollisionTransfersMotionAndRegistersTouch()
        {
            GateballCourt court = CreateCourt();
            GateballBall striker = CreateBall(court, 1, new Vector3(-0.30f, GateballGeometry.BallRadius, 0f));
            GateballBall target = CreateBall(court, 2, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { striker, target };
            yield return null;

            striker.Body.AddForce(Vector3.right * 1.5f, ForceMode.Impulse);
            yield return new WaitForSeconds(0.4f);

            Assert.Greater(target.Body.velocity.x, 0.1f);
            Assert.Greater(court._GetTouchCount(1), 0);
            Assert.AreEqual(2, court._GetTouchTargetId(1));
        }

        [UnityTest]
        public IEnumerator GatePostCollisionIsRegisteredByGateballBall()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(-0.25f, GateballGeometry.BallRadius, 0f));
            GameObject postObject = CreatePrimitive("GatePost", PrimitiveType.Cylinder);
            postObject.transform.position = new Vector3(0f, GateballGeometry.GatePostHeight * 0.5f, 0f);
            postObject.transform.localScale = new Vector3(
                GateballGeometry.GatePostDiameter,
                GateballGeometry.GatePostHeight * 0.5f,
                GateballGeometry.GatePostDiameter);
            GateballGatePost post = postObject.AddComponent<GateballGatePost>();
            post.GateIndex = 0;
            court.Balls = new[] { ball };
            yield return null;

            ball.Body.AddForce(Vector3.right * 1.0f, ForceMode.Impulse);
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(court._GetGatePostCollisionCount(1), 0);
        }

        [UnityTest]
        public IEnumerator GoalPoleContactIsRegisteredByCourt()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, -0.25f));
            GameObject poleObject = CreatePrimitive("GoalPole", PrimitiveType.Cylinder);
            poleObject.transform.position = new Vector3(0f, GateballGeometry.GoalPoleHeight * 0.5f, 0f);
            poleObject.transform.localScale = new Vector3(
                GateballGeometry.GoalPoleDiameter,
                GateballGeometry.GoalPoleHeight * 0.5f,
                GateballGeometry.GoalPoleDiameter);
            GateballGoalPole pole = poleObject.AddComponent<GateballGoalPole>();
            pole.Radius = GateballGeometry.GoalPoleRadius;
            pole.Height = GateballGeometry.GoalPoleHeight;
            court.Balls = new[] { ball };
            court.GoalPole = pole;
            yield return null;

            ball.Body.AddForce(Vector3.forward * 1.0f, ForceMode.Impulse);
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(court._HasGoalPoleHit(1));
            Assert.Greater(court._GetGoalPoleCollisionCount(1), 0);
        }

        [UnityTest]
        public IEnumerator GateSamplingRunsOnFixedStepAndAdvancesProgress()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, 0.10f, -1f));
            GameObject gateObject = new GameObject("Gate");
            Register(gateObject);
            GateballGate gate = gateObject.AddComponent<GateballGate>();
            gate.GateIndex = 0;
            gate.OpeningWidth = GateballGeometry.GateOpeningWidth;
            gate.OpeningHeight = GateballGeometry.GateOpeningHeight;
            gate.BallRadius = GateballGeometry.BallRadius;
            court.Balls = new[] { ball };
            court.Gates = new[] { gate };
            yield return null;

            ball.Body.position = new Vector3(0f, 0.10f, 1f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, court._GetGateProgress(1));
        }

        [UnityTest]
        public IEnumerator OutSamplingRunsOnFixedStep()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { ball };
            yield return null;

            ball.Body.position = new Vector3(GateballGeometry.CourtWidth * 0.5f + 0.4f, GateballGeometry.BallRadius, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(court._IsOut(1));
        }

        [UnityTest]
        public IEnumerator TestShotResetsAllBallsAndProducesTheSameStroke()
        {
            GateballCourt court = CreateCourt();
            GateballBall[] balls = new GateballBall[GateballGeometry.BallCount];
            Vector3[] initialPositions = new Vector3[balls.Length];
            for (int i = 0; i < balls.Length; i++)
            {
                initialPositions[i] = new Vector3((i - 4) * 0.15f, GateballGeometry.BallRadius, 0.5f);
                balls[i] = CreateBall(court, i + 1, initialPositions[i]);
            }

            GateballStrokeRouter router = CreateRouter(court);
            GateballTestShotController testShot = CreateTestShot(court, router);
            court.Balls = balls;
            yield return null;

            testShot._LoadStraightWeak();
            Vector3 firstInitialPosition = balls[0].Body.position;
            Vector3 firstInitialVelocity = balls[0].Body.velocity;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Vector3 firstShotFinalPosition = balls[0].Body.position;
            Vector3 firstShotFinalVelocity = balls[0].Body.velocity;

            balls[1].Body.position = new Vector3(4f, 2f, 4f);
            balls[1].Body.velocity = Vector3.one;
            court._RegisterTouch(balls[0], balls[1]);
            court._RegisterBoundaryCollision(balls[1]);
            court._RegisterGoalPoleContact(balls[1]);

            testShot._LoadStraightWeak();
            Vector3 secondInitialPosition = balls[0].Body.position;
            Vector3 secondInitialVelocity = balls[0].Body.velocity;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Vector3.Distance(firstInitialPosition, secondInitialPosition), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(firstInitialVelocity, secondInitialVelocity), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(firstShotFinalPosition, balls[0].Body.position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(firstShotFinalVelocity, balls[0].Body.velocity), Is.LessThan(0.0001f));
            for (int i = 1; i < balls.Length; i++)
            {
                Assert.AreEqual(initialPositions[i], balls[i].Body.position);
                Assert.AreEqual(Vector3.zero, balls[i].Body.velocity);
            }

            Assert.AreEqual(0, court._GetTouchCount(2));
            Assert.IsFalse(court._IsOut(2));
            Assert.IsFalse(court._HasGoalPoleHit(2));
            Assert.AreEqual(1, court.LastStrokeBallId);
            Assert.AreEqual(GateballGeometry.WeakStrokeImpulse, court.LastStrokeImpulse, 0.0001f);
        }

        [UnityTest]
        public IEnumerator MalletContactWithoutPickupAndAimLockDoesNotStroke()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { ball };
            GateballStrokeRouter router = CreateRouter(court);

            GameObject malletObject = new GameObject("Mallet");
            Register(malletObject);
            GameObject headObject = CreatePrimitive("Head", PrimitiveType.Cube);
            headObject.transform.SetParent(malletObject.transform, false);
            headObject.transform.localPosition = new Vector3(-0.4f, GateballGeometry.BallRadius, 0f);
            headObject.transform.localScale = new Vector3(0.65f, 0.18f, 0.22f);
            BoxCollider headCollider = headObject.GetComponent<BoxCollider>();
            headCollider.isTrigger = true;
            GateballMallet mallet = malletObject.AddComponent<GateballMallet>();
            mallet.StrokeRouter = router;
            mallet.Head = headObject.transform;
            mallet.HeadCollider = headCollider;
            yield return null;
            yield return new WaitForFixedUpdate();

            headObject.transform.localPosition = new Vector3(0.4f, GateballGeometry.BallRadius, 0f);
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(-1, court.LastStrokeBallId);
            Assert.AreEqual(Vector3.zero, ball.Body.velocity);
        }

        [UnityTest]
        public IEnumerator UnarmedMalletContactDoesNotStartNetworkShot()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            court.Balls = new[] { ball };
            GateballStrokeRouter router = CreateRouter(court);
            GameObject networkObject = new GameObject("NetworkState");
            Register(networkObject);
            GateballNetworkState networkState = networkObject.AddComponent<GateballNetworkState>();
            networkState.Court = court;
            router.NetworkState = networkState;

            GameObject malletObject = new GameObject("Mallet");
            Register(malletObject);
            GameObject headObject = CreatePrimitive("Head", PrimitiveType.Cube);
            headObject.transform.SetParent(malletObject.transform, false);
            headObject.transform.localPosition = new Vector3(-0.4f, GateballGeometry.BallRadius, 0f);
            headObject.transform.localScale = new Vector3(0.65f, 0.18f, 0.22f);
            BoxCollider headCollider = headObject.GetComponent<BoxCollider>();
            headCollider.isTrigger = true;
            GateballMallet mallet = malletObject.AddComponent<GateballMallet>();
            mallet.StrokeRouter = router;
            mallet.Head = headObject.transform;
            mallet.HeadCollider = headCollider;
            yield return null;
            yield return new WaitForFixedUpdate();

            headObject.transform.localPosition = new Vector3(0.4f, GateballGeometry.BallRadius, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, networkState.ShotId);
            Assert.AreEqual(GateballNetworkState.PhaseWaiting, networkState.Phase);
            Assert.AreEqual(-1, court.LastStrokeBallId);
            Assert.AreEqual(Vector3.zero, ball.Body.velocity);
        }

        [UnityTest]
        public IEnumerator GripTransitionsPreservePoseAndPromoteTheRemainingHand()
        {
            GameObject malletObject = new GameObject("MalletController");
            Register(malletObject);
            GameObject rigObject = new GameObject("MalletRig");
            Register(rigObject);
            rigObject.transform.SetPositionAndRotation(new Vector3(1f, 1f, -2f), Quaternion.identity);

            GameObject gripAObject = new GameObject("GripA");
            Register(gripAObject);
            gripAObject.transform.position = rigObject.transform.position + Vector3.up * 0.4f;
            GameObject gripBObject = new GameObject("GripB");
            Register(gripBObject);
            gripBObject.transform.position = rigObject.transform.position - Vector3.up * 0.3f;

            GateballMallet mallet = malletObject.AddComponent<GateballMallet>();
            mallet.MalletRig = rigObject.transform;
            mallet.GripA = gripAObject.transform;
            mallet.GripB = gripBObject.transform;
            yield return null;

            mallet._OnGripPicked(0);
            yield return null;
            gripAObject.transform.position += Vector3.right * 0.25f;
            gripAObject.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            yield return null;

            Vector3 beforeSecondPickupPosition = rigObject.transform.position;
            Quaternion beforeSecondPickupRotation = rigObject.transform.rotation;
            gripBObject.transform.position = gripAObject.transform.position
                + beforeSecondPickupRotation * Vector3.down * 0.7f;
            mallet._OnGripPicked(1);
            yield return null;

            Assert.Less(Vector3.Distance(beforeSecondPickupPosition, rigObject.transform.position), 0.0001f);
            Assert.Less(Quaternion.Angle(beforeSecondPickupRotation, rigObject.transform.rotation), 0.001f);

            Vector3 currentShaftDirection = new Vector3(1f, -1f, 0f).normalized;
            gripAObject.transform.position += Vector3.forward * 0.3f;
            gripBObject.transform.position = gripAObject.transform.position + currentShaftDirection * 0.7f;
            yield return null;

            Assert.Less(Vector3.Angle(rigObject.transform.rotation * Vector3.down, currentShaftDirection), 0.001f);
            Vector3 beforePrimaryReleasePosition = rigObject.transform.position;
            Quaternion beforePrimaryReleaseRotation = rigObject.transform.rotation;
            mallet._OnGripDropped(0);
            yield return null;

            Assert.Less(Vector3.Distance(beforePrimaryReleasePosition, rigObject.transform.position), 0.0001f);
            Assert.Less(Quaternion.Angle(beforePrimaryReleaseRotation, rigObject.transform.rotation), 0.001f);

            gripBObject.transform.position += Vector3.right * 0.2f;
            yield return null;
            Assert.Greater(Vector3.Distance(beforePrimaryReleasePosition, rigObject.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator ShotStartAppliesSynchronizedLinearAndAngularVelocity()
        {
            GateballCourt court = CreateCourt();
            GateballBall ball = CreateBall(court, 1, new Vector3(0f, GateballGeometry.BallRadius, 0f));
            ball.Body.angularDrag = 0f;
            court.Balls = new[] { ball };

            GameObject networkObject = new GameObject("NetworkState");
            Register(networkObject);
            GateballNetworkState networkState = networkObject.AddComponent<GateballNetworkState>();
            networkState.Court = court;
            yield return null;

            Vector3 linearVelocity = new Vector3(0.8f, 0f, 0.2f);
            Vector3 angularVelocity = new Vector3(0f, 1.5f, -2f);
            Assert.IsTrue(networkState._RequestShotWithVelocities(1, linearVelocity, angularVelocity));
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(linearVelocity, networkState.InitialLinearVelocity);
            Assert.AreEqual(angularVelocity, networkState.InitialAngularVelocity);
            Assert.AreEqual(linearVelocity, ball.Body.velocity);
            Assert.AreEqual(angularVelocity, ball.Body.angularVelocity);
        }

        [Test]
        public void ShotStartRejectsNonFiniteVelocity()
        {
            GateballNetworkState networkState = new GameObject("NetworkState")
                .AddComponent<GateballNetworkState>();

            Assert.IsFalse(networkState._RequestShotWithVelocities(
                1,
                new Vector3(float.NaN, 0f, 1f),
                Vector3.zero));
            UnityEngine.Object.DestroyImmediate(networkState.gameObject);
        }

        private GateballCourt CreateCourt()
        {
            GameObject courtObject = new GameObject("Court");
            Register(courtObject);
            GateballCourt court = courtObject.AddComponent<GateballCourt>();
            court.CourtWidth = GateballGeometry.CourtWidth;
            court.CourtLength = GateballGeometry.CourtLength;
            court.Gates = new GateballGate[0];
            return court;
        }

        private GateballBall CreateBall(GateballCourt court, int ballId, Vector3 position)
        {
            GameObject ballObject = CreatePrimitive("Ball" + ballId.ToString(), PrimitiveType.Sphere);
            ballObject.transform.position = position;
            ballObject.transform.localScale = Vector3.one * GateballGeometry.BallDiameter;
            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.mass = GateballGeometry.BallMass;
            body.useGravity = false;
            body.drag = 0f;
            body.angularDrag = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GateballBall ball = ballObject.AddComponent<GateballBall>();
            ball.BallId = ballId;
            ball.Court = court;
            ball.Body = body;
            ball.Radius = GateballGeometry.BallRadius;
            return ball;
        }

        private GateballStrokeRouter CreateRouter(GateballCourt court)
        {
            GameObject routerObject = new GameObject("StrokeRouter");
            Register(routerObject);
            GateballStrokeRouter router = routerObject.AddComponent<GateballStrokeRouter>();
            router.Court = court;
            router.MaximumImpulse = GateballGeometry.StrongStrokeImpulse;
            return router;
        }

        private GateballTestShotController CreateTestShot(GateballCourt court, GateballStrokeRouter router)
        {
            GameObject testShotObject = new GameObject("TestShotController");
            Register(testShotObject);
            GateballTestShotController testShot = testShotObject.AddComponent<GateballTestShotController>();
            testShot.Court = court;
            testShot.StrokeRouter = router;
            return testShot;
        }

        private GameObject CreatePrimitive(string name, PrimitiveType type)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            Register(gameObject);
            gameObject.name = name;
            return gameObject;
        }

        private void Register(GameObject gameObject)
        {
            _createdObjects[_createdCount] = gameObject;
            _createdCount++;
        }
    }
}
