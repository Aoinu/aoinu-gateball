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
        public void TrajectoryMetricsMatchSamplesBySimulationStep()
        {
            int[] localSteps = { 0, 1, 2 };
            int[] ownerSteps = { 0, 1, 2 };
            Vector3[] localSamples = new Vector3[localSteps.Length * GateballGeometry.BallCount];
            Vector3[] ownerSamples = new Vector3[ownerSteps.Length * GateballGeometry.BallCount];
            localSamples[GateballGeometry.BallIdToIndex(1) + GateballGeometry.BallCount] = new Vector3(1f, 0f, 0f);
            localSamples[GateballGeometry.BallIdToIndex(1) + GateballGeometry.BallCount * 2] = new Vector3(2f, 0f, 0f);
            ownerSamples[GateballGeometry.BallIdToIndex(1) + GateballGeometry.BallCount] = new Vector3(0.5f, 0f, 0f);
            ownerSamples[GateballGeometry.BallIdToIndex(1) + GateballGeometry.BallCount * 2] = new Vector3(1f, 0f, 0f);

            Assert.AreEqual(
                1f,
                GateballTelemetry.CalculateTrajectoryMaxPositionError(
                    localSteps,
                    localSamples,
                    localSteps.Length,
                    ownerSteps,
                    ownerSamples,
                    ownerSteps.Length,
                    1),
                0.0001f);
            Assert.AreEqual(
                0.5f,
                GateballTelemetry.CalculateTrajectoryMeanPositionError(
                    localSteps,
                    localSamples,
                    localSteps.Length,
                    ownerSteps,
                    ownerSamples,
                    ownerSteps.Length,
                    1),
                0.0001f);
            Vector3[] localFinal = new Vector3[GateballGeometry.BallCount];
            Vector3[] ownerFinal = new Vector3[GateballGeometry.BallCount];
            localFinal[0] = new Vector3(2f, 0f, 0f);
            ownerFinal[0] = new Vector3(1f, 0f, 0f);
            Assert.AreEqual(1f, GateballTelemetry.CalculateMaxPositionError(localFinal, ownerFinal), 0.0001f);
            Assert.AreEqual(0.1f, GateballTelemetry.CalculateMeanPositionError(localFinal, ownerFinal), 0.0001f);
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
        public void EventComparisonClassifiesCriticalAndDiagnosticDifferences()
        {
            int collision = GateballTelemetry.EncodeEvent(GateballTelemetry.EventBallCollision, 1, 2, -1);
            int settled = GateballTelemetry.EncodeEvent(GateballTelemetry.EventSettled, 1, -1, -1);
            int[] owner = { collision, settled };
            int[] remote = { settled, collision, collision };

            int[] classification = GateballTelemetry.CalculateEventDivergenceClassification(
                remote,
                remote.Length,
                owner,
                owner.Length);

            Assert.AreEqual(0, classification[GateballTelemetry.EventClassificationMissingRemote]);
            Assert.AreEqual(1, classification[GateballTelemetry.EventClassificationExtraRemote]);
            Assert.AreEqual(2, classification[GateballTelemetry.EventClassificationOrderingOnly]);
            Assert.AreEqual(1, classification[GateballTelemetry.EventClassificationDuplicate]);
            Assert.AreEqual(0, classification[GateballTelemetry.EventClassificationGameplayCritical]);
            Assert.GreaterOrEqual(classification[GateballTelemetry.EventClassificationDiagnostic], 2);
            Assert.AreEqual(GateballTelemetry.EventBallCollision, GateballTelemetry.DecodeEventType(collision));
            Assert.AreEqual(1, GateballTelemetry.DecodeEventBallId(collision));
            Assert.AreEqual(2, GateballTelemetry.DecodeEventTargetId(collision));
            Assert.AreEqual(-1, GateballTelemetry.DecodeEventGateIndex(collision));
        }

        [Test]
        public void LateJoinAlwaysReconstructsFromAuthoritativeState()
        {
            Assert.IsTrue(GateballNetworkState.ShouldLateJoinUseAuthoritativeState(GateballNetworkState.PhaseWaiting));
            Assert.IsTrue(GateballNetworkState.ShouldLateJoinUseAuthoritativeState(GateballNetworkState.PhaseSimulating));
            Assert.IsTrue(GateballNetworkState.ShouldLateJoinUseAuthoritativeState(GateballNetworkState.PhaseSettled));
        }

        [Test]
        public void SimulatingLateJoinAppliesSameShotSettledSnapshot()
        {
            Assert.IsTrue(GateballNetworkState.ShouldApplyShotEnd(
                GateballNetworkState.PhaseSettled,
                7,
                7,
                7,
                -1));
            Assert.IsTrue(GateballNetworkState.ShouldApplyShotEnd(
                GateballNetworkState.PhaseSettled,
                8,
                7,
                -1,
                -1));
            Assert.IsFalse(GateballNetworkState.ShouldApplyShotEnd(
                GateballNetworkState.PhaseSettled,
                6,
                7,
                -1,
                -1));
            Assert.IsFalse(GateballNetworkState.ShouldApplyShotEnd(
                GateballNetworkState.PhaseSimulating,
                7,
                7,
                7,
                -1));
        }
    }
}
