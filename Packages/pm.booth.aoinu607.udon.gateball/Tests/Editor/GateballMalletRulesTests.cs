using NUnit.Framework;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballMalletRulesTests
    {
        [Test]
        public void TimeWindowVelocityIsStableAcrossSampleIntervals()
        {
            Vector3[] fastPositions =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0.02f),
                new Vector3(0f, 0f, 0.04f),
                new Vector3(0f, 0f, 0.06f),
                new Vector3(0f, 0f, 0.08f),
                new Vector3(0f, 0f, 0.10f)
            };
            float[] fastTimes = { 0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.10f };
            Vector3[] slowPositions =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0.05f),
                new Vector3(0f, 0f, 0.10f)
            };
            float[] slowTimes = { 0f, 0.05f, 0.10f };

            Vector3 fastVelocity = GateballMalletRules.EstimateWindowVelocity(
                fastPositions, fastTimes, 0, fastPositions.Length, 0.10f, 0.04f);
            Vector3 slowVelocity = GateballMalletRules.EstimateWindowVelocity(
                slowPositions, slowTimes, 0, slowPositions.Length, 0.10f, 0.04f);

            Assert.AreEqual(1f, fastVelocity.z, 0.0001f);
            Assert.AreEqual(fastVelocity.z, slowVelocity.z, 0.0001f);
        }

        [Test]
        public void TimeWindowVelocityUsesSamplesAroundWindowAndRejectsInsufficientHistory()
        {
            Vector3[] positions =
            {
                new Vector3(0f, 0f, -10f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0.02f),
                new Vector3(0f, 0f, 0.10f)
            };
            float[] times = { -5f, 0f, 0.01f, 0.05f };

            Vector3 velocity = GateballMalletRules.EstimateWindowVelocity(
                positions, times, 0, positions.Length, 0.05f, 0.02f);
            Vector3 insufficient = GateballMalletRules.EstimateWindowVelocity(
                positions, times, 0, 1, 0.05f, 0.02f);

            Assert.AreEqual(2f, velocity.z, 0.0001f);
            Assert.AreEqual(Vector3.zero, insufficient);
        }

        [Test]
        public void ShotSpeedHasDeadzoneNoMinimumAndMaximumCap()
        {
            Assert.IsFalse(TryBuildShotVelocities(0.09f, out Vector3 belowDeadzone, out _));
            Assert.IsTrue(TryBuildShotVelocities(0.10f, out Vector3 atDeadzone, out _));
            Assert.IsTrue(TryBuildShotVelocities(0.101f, out Vector3 justAboveDeadzone, out _));
            Assert.IsTrue(TryBuildShotVelocities(0.5f, out Vector3 normalSpeed, out _));
            Assert.IsTrue(TryBuildShotVelocities(100f, out Vector3 cappedSpeed, out _));

            Assert.AreEqual(0.10f, atDeadzone.magnitude, 0.0001f);
            Assert.AreEqual(0.101f, justAboveDeadzone.magnitude, 0.0001f);
            Assert.Greater(normalSpeed.magnitude, justAboveDeadzone.magnitude);
            Assert.AreEqual(6f, cappedSpeed.magnitude, 0.0001f);
            Assert.AreEqual(0f, normalSpeed.y, 0.0001f);
        }

        [Test]
        public void AimAssistShrinksSmallErrorAndHonorsMaximumDeviation()
        {
            Vector3 swing = Quaternion.AngleAxis(20f, Vector3.up) * Vector3.forward;
            Assert.IsTrue(GateballMalletRules.TryBuildShotVelocities(
                Vector3.forward,
                swing * 1f,
                Vector3.up,
                0.1f,
                1f,
                6f,
                0.75f,
                5f,
                90f,
                GateballGeometry.BallRadius,
                1f,
                out Vector3 linear,
                out _));

            Assert.AreEqual(5f, Vector3.Angle(Vector3.forward, linear), 0.001f);
        }

        [Test]
        public void InvalidSwingIsRejectedAndRollingSpinMatchesLinearVelocity()
        {
            Vector3 backwards = -Vector3.forward;
            Assert.IsFalse(GateballMalletRules.TryBuildShotVelocities(
                Vector3.forward,
                backwards,
                Vector3.up,
                0.1f,
                1f,
                6f,
                0.75f,
                12f,
                90f,
                GateballGeometry.BallRadius,
                1f,
                out _,
                out _));

            Assert.IsTrue(TryBuildShotVelocities(1f, out Vector3 linear, out Vector3 angular));
            Assert.AreEqual(Vector3.Cross(Vector3.up, linear) / GateballGeometry.BallRadius, angular);
            Assert.IsFalse(GateballMalletRules.TryBuildShotVelocities(
                Vector3.forward,
                new Vector3(float.NaN, 0f, 1f),
                Vector3.up,
                0.1f,
                1f,
                6f,
                0.75f,
                12f,
                90f,
                GateballGeometry.BallRadius,
                1f,
                out _,
                out _));
        }

        [Test]
        public void OneHandPoseAndTwoHandNeutralTransitionPreservePose()
        {
            Vector3 localGripPosition = new Vector3(0f, 0.4f, 0f);
            Quaternion localGripRotation = Quaternion.Euler(10f, 20f, 30f);
            Quaternion handRotation = Quaternion.Euler(-15f, 60f, 5f);
            Vector3 expectedMalletPosition = new Vector3(2f, 1f, -3f);
            Quaternion expectedMalletRotation = Quaternion.Euler(0f, 40f, 0f);
            Vector3 gripPosition = expectedMalletPosition + expectedMalletRotation * localGripPosition;
            Quaternion gripRotation = expectedMalletRotation * localGripRotation;

            GateballMalletRules.SolveSingleHandPose(
                gripPosition,
                gripRotation,
                localGripPosition,
                localGripRotation,
                out Vector3 malletPosition,
                out Quaternion malletRotation);
            Quaternion twoHandNeutral = GateballMalletRules.SolveTwoHandRotation(
                expectedMalletRotation,
                Vector3.down,
                Vector3.down,
                0f);

            Assert.Less(Vector3.Distance(expectedMalletPosition, malletPosition), 0.00001f);
            Assert.Less(Quaternion.Angle(expectedMalletRotation, malletRotation), 0.001f);
            Assert.Less(Quaternion.Angle(expectedMalletRotation, twoHandNeutral), 0.001f);
            Assert.Greater(Quaternion.Angle(expectedMalletRotation, handRotation), 1f);
        }

        [Test]
        public void ShaftTracksHandsAndTwistAverageWrapsAt180Degrees()
        {
            Vector3 currentShaft = Vector3.right;
            Quaternion rotation = GateballMalletRules.SolveTwoHandRotation(
                Quaternion.identity,
                Vector3.down,
                currentShaft,
                0f);
            float averageTwist = GateballMalletRules.CalculateAverageTwistDegrees(
                Quaternion.identity,
                Quaternion.AngleAxis(179f, Vector3.down),
                Quaternion.identity,
                Quaternion.AngleAxis(-179f, Vector3.down),
                Vector3.down);

            Assert.Less(Vector3.Angle(rotation * Vector3.down, currentShaft), 0.001f);
            Assert.AreEqual(180f, Mathf.Abs(averageTwist), 0.1f);
        }

        [Test]
        public void AimLockKeepsHeadRollWhileShaftTracksGripPositions()
        {
            const float lockedRoll = 32f;
            Quaternion neutralPrimary = Quaternion.identity;
            Quaternion neutralSecondary = Quaternion.identity;
            Quaternion firstPrimaryRotation = Quaternion.AngleAxis(15f, Vector3.down);
            Quaternion firstSecondaryRotation = Quaternion.AngleAxis(-5f, Vector3.down);
            Quaternion changedPrimaryRotation = Quaternion.AngleAxis(80f, Vector3.down);
            Quaternion changedSecondaryRotation = Quaternion.AngleAxis(55f, Vector3.down);

            float firstRoll = GateballMalletRules.ResolveTwoHandRollDegrees(
                true,
                lockedRoll,
                neutralPrimary,
                firstPrimaryRotation,
                neutralSecondary,
                firstSecondaryRotation,
                Vector3.down);
            float changedRoll = GateballMalletRules.ResolveTwoHandRollDegrees(
                true,
                lockedRoll,
                neutralPrimary,
                changedPrimaryRotation,
                neutralSecondary,
                changedSecondaryRotation,
                Vector3.right);
            Quaternion malletRotation = GateballMalletRules.SolveTwoHandRotation(
                Quaternion.identity,
                Vector3.down,
                Vector3.right,
                changedRoll);

            Assert.AreEqual(lockedRoll, firstRoll, 0.0001f);
            Assert.AreEqual(lockedRoll, changedRoll, 0.0001f);
            Assert.Less(Vector3.Angle(malletRotation * Vector3.down, Vector3.right), 0.001f);
            Assert.AreEqual(lockedRoll, Vector3.SignedAngle(
                Quaternion.FromToRotation(Vector3.down, Vector3.right) * Vector3.forward,
                malletRotation * Vector3.forward,
                Vector3.right), 0.001f);

            float aimingRoll = GateballMalletRules.ResolveTwoHandRollDegrees(
                false,
                lockedRoll,
                neutralPrimary,
                firstPrimaryRotation,
                neutralSecondary,
                firstSecondaryRotation,
                Vector3.down);
            Assert.AreEqual(5f, aimingRoll, 0.001f);
        }

        private static bool TryBuildShotVelocities(float speed, out Vector3 linear, out Vector3 angular)
        {
            return GateballMalletRules.TryBuildShotVelocities(
                Vector3.forward,
                Vector3.forward * speed,
                Vector3.up,
                0.1f,
                1f,
                6f,
                0.75f,
                12f,
                90f,
                GateballGeometry.BallRadius,
                1f,
                out linear,
                out angular);
        }
    }
}
