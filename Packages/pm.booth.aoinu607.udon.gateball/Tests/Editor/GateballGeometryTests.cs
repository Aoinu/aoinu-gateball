using NUnit.Framework;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballGeometryTests
    {
        [Test]
        public void BallIdsMapToExpectedTeamAndIndex()
        {
            Assert.IsTrue(GateballGeometry.IsValidBallId(1));
            Assert.IsTrue(GateballGeometry.IsRedBall(1));
            Assert.AreEqual(0, GateballGeometry.BallIdToIndex(1));
            Assert.IsFalse(GateballGeometry.IsRedBall(2));
            Assert.AreEqual(9, GateballGeometry.BallIdToIndex(10));
            Assert.IsFalse(GateballGeometry.IsValidBallId(0));
            Assert.AreEqual(-1, GateballGeometry.BallIdToIndex(11));
        }

        [Test]
        public void GateGeometryDetectsForwardAndReverseCrossings()
        {
            Vector3 crossingPoint;
            bool forward = GateballGeometry.TryGetGateCrossing(
                new Vector3(0f, 0.16f, -1f),
                new Vector3(0f, 0.16f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                1.2f,
                0.45f,
                0.12f,
                true,
                out crossingPoint);

            bool reverse = GateballGeometry.TryGetGateCrossing(
                new Vector3(0f, 0.16f, 1f),
                new Vector3(0f, 0.16f, -1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                1.2f,
                0.45f,
                0.12f,
                false,
                out crossingPoint);

            Assert.IsTrue(forward);
            Assert.IsTrue(reverse);
            Assert.AreEqual(0f, crossingPoint.z, 0.001f);
        }

        [Test]
        public void GateGeometryRejectsCrossingOutsideOpening()
        {
            Vector3 crossingPoint;
            bool result = GateballGeometry.TryGetGateCrossing(
                new Vector3(0.7f, 0.16f, -1f),
                new Vector3(0.7f, 0.16f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                1.2f,
                0.45f,
                0.12f,
                true,
                out crossingPoint);

            Assert.IsFalse(result);
        }

        [Test]
        public void CourtBoundsAndStopThresholdsAreStable()
        {
            Assert.IsFalse(GateballGeometry.IsOutOfCourt(Vector3.zero, 15f, 20f, 0.35f));
            Assert.IsTrue(GateballGeometry.IsOutOfCourt(new Vector3(7.9f, 0f, 0f), 15f, 20f, 0.35f));
            Assert.IsTrue(GateballGeometry.IsStopped(
                new Vector3(0.02f, 0f, 0f),
                new Vector3(0f, 0.1f, 0f),
                0.12f,
                0.03f,
                0.02f));
            Assert.IsFalse(GateballGeometry.IsStopped(
                new Vector3(0.2f, 0f, 0f),
                Vector3.zero,
                0.12f,
                0.03f,
                0.02f));
        }

        [Test]
        public void BallTouchUsesCombinedRadii()
        {
            Assert.IsTrue(GateballGeometry.IsBallTouchingBall(
                Vector3.zero,
                new Vector3(0.3f, 0f, 0f),
                0.16f,
                0.16f));
            Assert.IsFalse(GateballGeometry.IsBallTouchingBall(
                Vector3.zero,
                new Vector3(0.5f, 0f, 0f),
                0.16f,
                0.16f));
        }

        [Test]
        public void GoalPoleContactUsesHorizontalAndVerticalBounds()
        {
            Assert.IsTrue(GateballGeometry.IsBallTouchingVerticalPole(
                new Vector3(0.2f, 0.16f, 0f),
                new Vector3(0f, 0.65f, 0f),
                0.16f,
                0.08f,
                1.3f));
            Assert.IsFalse(GateballGeometry.IsBallTouchingVerticalPole(
                new Vector3(0.3f, 0.16f, 0f),
                new Vector3(0f, 0.65f, 0f),
                0.16f,
                0.08f,
                1.3f));
        }

        [Test]
        public void MalletSpeedMapsToBoundedImpulse()
        {
            Assert.AreEqual(0.5f, GateballGeometry.CalculateMalletImpulse(1f, 0.5f, 0.5f, 5f), 0.001f);
            Assert.AreEqual(5f, GateballGeometry.CalculateMalletImpulse(20f, 0.5f, 0.5f, 5f), 0.001f);
        }
    }
}
