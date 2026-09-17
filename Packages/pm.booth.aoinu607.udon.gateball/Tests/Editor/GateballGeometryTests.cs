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
        public void PhysicalDimensionsMatchLocalGateballSpecification()
        {
            Assert.AreEqual(0.075f, GateballGeometry.BallDiameter, 0.000001f);
            Assert.AreEqual(0.230f, GateballGeometry.BallMass, 0.000001f);
            Assert.AreEqual(0.22f, GateballGeometry.GateOpeningWidth, 0.000001f);
            Assert.AreEqual(0.19f, GateballGeometry.GateOpeningHeight, 0.000001f);
            Assert.AreEqual(0.02f, GateballGeometry.GatePostDiameter, 0.000001f);
            Assert.AreEqual(0.20f, GateballGeometry.GatePostHeight, 0.000001f);
            Assert.AreEqual(0.02f, GateballGeometry.GoalPoleDiameter, 0.000001f);
            Assert.AreEqual(0.20f, GateballGeometry.GoalPoleHeight, 0.000001f);
            Assert.AreEqual(15f, GateballGeometry.CourtWidth, 0.000001f);
            Assert.AreEqual(20f, GateballGeometry.CourtLength, 0.000001f);
        }

        [Test]
        public void GateGeometryDetectsForwardAndReverseCrossings()
        {
            Vector3 crossingPoint;
            bool forward = GateballGeometry.TryGetGateCrossing(
                new Vector3(0f, 0.10f, -1f),
                new Vector3(0f, 0.10f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                GateballGeometry.GateOpeningWidth,
                GateballGeometry.GateOpeningHeight,
                GateballGeometry.BallRadius,
                true,
                out crossingPoint);

            bool reverse = GateballGeometry.TryGetGateCrossing(
                new Vector3(0f, 0.10f, 1f),
                new Vector3(0f, 0.10f, -1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                GateballGeometry.GateOpeningWidth,
                GateballGeometry.GateOpeningHeight,
                GateballGeometry.BallRadius,
                false,
                out crossingPoint);

            Assert.IsTrue(forward);
            Assert.IsTrue(reverse);
            Assert.AreEqual(0f, crossingPoint.z, 0.001f);
        }

        [Test]
        public void GateGeometryAccountsForBallRadiusAtOpeningEdges()
        {
            Vector3 crossingPoint;
            bool horizontalEdge = GateballGeometry.TryGetGateCrossing(
                new Vector3(GateballGeometry.GateOpeningWidth * 0.5f - GateballGeometry.BallRadius, 0.10f, -1f),
                new Vector3(GateballGeometry.GateOpeningWidth * 0.5f - GateballGeometry.BallRadius, 0.10f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                GateballGeometry.GateOpeningWidth,
                GateballGeometry.GateOpeningHeight,
                GateballGeometry.BallRadius,
                true,
                out crossingPoint);

            bool outsideHorizontalEdge = GateballGeometry.TryGetGateCrossing(
                new Vector3(GateballGeometry.GateOpeningWidth * 0.5f - GateballGeometry.BallRadius + 0.001f, 0.10f, -1f),
                new Vector3(GateballGeometry.GateOpeningWidth * 0.5f - GateballGeometry.BallRadius + 0.001f, 0.10f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                GateballGeometry.GateOpeningWidth,
                GateballGeometry.GateOpeningHeight,
                GateballGeometry.BallRadius,
                true,
                out crossingPoint);

            Assert.IsTrue(horizontalEdge);
            Assert.IsFalse(outsideHorizontalEdge);
        }

        [Test]
        public void GateGeometryRejectsAboveBarAndBelowFloorCases()
        {
            Vector3 crossingPoint;
            bool aboveBar = GateballGeometry.TryGetGateCrossing(
                new Vector3(0f, GateballGeometry.GateOpeningHeight - GateballGeometry.BallRadius + 0.001f, -1f),
                new Vector3(0f, GateballGeometry.GateOpeningHeight - GateballGeometry.BallRadius + 0.001f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                GateballGeometry.GateOpeningWidth,
                GateballGeometry.GateOpeningHeight,
                GateballGeometry.BallRadius,
                true,
                out crossingPoint);
            bool belowFloor = GateballGeometry.TryGetGateCrossing(
                new Vector3(0f, GateballGeometry.BallRadius - 0.001f, -1f),
                new Vector3(0f, GateballGeometry.BallRadius - 0.001f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                GateballGeometry.GateOpeningWidth,
                GateballGeometry.GateOpeningHeight,
                GateballGeometry.BallRadius,
                true,
                out crossingPoint);

            Assert.IsFalse(aboveBar);
            Assert.IsFalse(belowFloor);
        }

        [Test]
        public void CourtBoundsAndStopThresholdsAreStable()
        {
            Assert.IsFalse(GateballGeometry.IsOutOfCourt(Vector3.zero, 15f, 20f, 0.35f));
            Assert.IsTrue(GateballGeometry.IsOutOfCourt(new Vector3(7.9f, 0f, 0f), 15f, 20f, 0.35f));
            Assert.IsTrue(GateballGeometry.IsStopped(
                new Vector3(0.02f, 0f, 0f),
                new Vector3(0f, 0.1f, 0f),
                GateballGeometry.BallRadius,
                0.03f,
                0.02f));
            Assert.IsFalse(GateballGeometry.IsStopped(
                new Vector3(0.2f, 0f, 0f),
                Vector3.zero,
                GateballGeometry.BallRadius,
                0.03f,
                0.02f));
        }

        [Test]
        public void BallTouchUsesCombinedRadii()
        {
            Assert.IsTrue(GateballGeometry.IsBallTouchingBall(
                Vector3.zero,
                new Vector3(0.075f, 0f, 0f),
                GateballGeometry.BallRadius,
                GateballGeometry.BallRadius));
            Assert.IsFalse(GateballGeometry.IsBallTouchingBall(
                Vector3.zero,
                new Vector3(0.08f, 0f, 0f),
                GateballGeometry.BallRadius,
                GateballGeometry.BallRadius));
        }

        [Test]
        public void GoalPoleContactUsesHorizontalAndVerticalBounds()
        {
            Assert.IsTrue(GateballGeometry.IsBallTouchingVerticalPole(
                new Vector3(0.0475f, 0.10f, 0f),
                new Vector3(0f, 0.10f, 0f),
                GateballGeometry.BallRadius,
                GateballGeometry.GoalPoleRadius,
                GateballGeometry.GoalPoleHeight));
            Assert.IsFalse(GateballGeometry.IsBallTouchingVerticalPole(
                new Vector3(0.0485f, 0.10f, 0f),
                new Vector3(0f, 0.10f, 0f),
                GateballGeometry.BallRadius,
                GateballGeometry.GoalPoleRadius,
                GateballGeometry.GoalPoleHeight));
        }

        [Test]
        public void MalletSpeedMapsToBoundedImpulse()
        {
            Assert.AreEqual(0.5f, GateballGeometry.CalculateMalletImpulse(1f, 0.5f, 0.5f, 5f), 0.001f);
            Assert.AreEqual(5f, GateballGeometry.CalculateMalletImpulse(20f, 0.5f, 0.5f, 5f), 0.001f);
        }
    }
}
