using NUnit.Framework;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballDistributedPhysicsTests
    {
        [Test]
        public void ShotIdOnlyAcceptsNewerShotOnce()
        {
            Assert.IsTrue(GateballNetworkState.IsNewShot(4, 5));
            Assert.IsFalse(GateballNetworkState.IsNewShot(5, 5));
            Assert.IsFalse(GateballNetworkState.IsNewShot(5, 4));
        }

        [Test]
        public void PhaseTransitionAllowsWaitingAndSettledButNotSimulation()
        {
            Assert.IsTrue(GateballNetworkState.CanStartShot(GateballNetworkState.PhaseWaiting));
            Assert.IsFalse(GateballNetworkState.CanStartShot(GateballNetworkState.PhaseSimulating));
            Assert.IsTrue(GateballNetworkState.CanStartShot(GateballNetworkState.PhaseSettled));
        }

        [Test]
        public void TenBallSnapshotCopiesEveryPosition()
        {
            Vector3[] source = new Vector3[GateballGeometry.BallCount];
            Vector3[] destination = new Vector3[GateballGeometry.BallCount];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = new Vector3(i, GateballGeometry.BallRadius, -i * 0.5f);
            }

            GateballNetworkState.CopyPositions(source, destination);

            for (int i = 0; i < source.Length; i++)
            {
                Assert.AreEqual(source[i], destination[i]);
            }
        }

        [Test]
        public void ShotEndCorrectionMetricsUseOwnerSnapshot()
        {
            Vector3[] owner = new Vector3[GateballGeometry.BallCount];
            Vector3[] local = new Vector3[GateballGeometry.BallCount];
            local[0] = new Vector3(0.3f, 0f, 0f);
            local[1] = new Vector3(0.1f, 0f, 0f);

            Assert.AreEqual(0.3f, GateballTelemetry.CalculateMaxPositionError(local, owner), 0.0001f);
            Assert.AreEqual(0.04f, GateballTelemetry.CalculateMeanPositionError(local, owner), 0.0001f);
            Assert.AreEqual(0.3f, GateballTelemetry.CalculateFinalPositionError(local, owner, 1), 0.0001f);
        }

        [Test]
        public void EventComparisonReportsSequenceAndCountDivergence()
        {
            int[] owner =
            {
                GateballTelemetry.EncodeEvent(GateballTelemetry.EventBallCollision, 1, 2, -1),
                GateballTelemetry.EncodeEvent(GateballTelemetry.EventSettled, 1, -1, -1)
            };
            int[] remote =
            {
                GateballTelemetry.EncodeEvent(GateballTelemetry.EventBallCollision, 1, 3, -1),
                GateballTelemetry.EncodeEvent(GateballTelemetry.EventSettled, 1, -1, -1),
                GateballTelemetry.EncodeEvent(GateballTelemetry.EventOut, 1, -1, -1)
            };

            Assert.AreEqual(2, GateballTelemetry.CalculateEventDivergenceCount(remote, 3, owner, 2));
        }

        [Test]
        public void LateJoinAlwaysReconstructsFromAuthoritativeState()
        {
            Assert.IsTrue(GateballNetworkState.ShouldLateJoinUseAuthoritativeState(GateballNetworkState.PhaseWaiting));
            Assert.IsTrue(GateballNetworkState.ShouldLateJoinUseAuthoritativeState(GateballNetworkState.PhaseSimulating));
            Assert.IsTrue(GateballNetworkState.ShouldLateJoinUseAuthoritativeState(GateballNetworkState.PhaseSettled));
        }
    }
}
