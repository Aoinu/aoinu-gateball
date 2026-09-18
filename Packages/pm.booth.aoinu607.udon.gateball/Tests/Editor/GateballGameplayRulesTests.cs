using NUnit.Framework;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballGameplayRulesTests
    {
        [Test]
        public void BallTeamsStayIndependentFromPlayerAssignments()
        {
            Assert.AreEqual(0, GateballGameplayRules.TeamForBall(1));
            Assert.AreEqual(1, GateballGameplayRules.TeamForBall(2));
            Assert.AreEqual(0, GateballGameplayRules.TeamForBall(9));
            Assert.AreEqual(1, GateballGameplayRules.TeamForBall(10));
        }

        [Test]
        public void TenBallsAreAssignedEvenlyAcrossPlayers()
        {
            int[] playerIds = { 101, 202, 303 };
            int[] assignments = new int[GateballGeometry.BallCount];

            GateballGameplayRules.AssignBallsToPlayers(playerIds, playerIds.Length, assignments);

            CollectionAssert.AreEqual(
                new[] { 101, 202, 303, 101, 202, 303, 101, 202, 303, 101 },
                assignments);
        }

        [Test]
        public void TurnProgressionWrapsFromTenToOneAndSkipsGoals()
        {
            bool[] goals = new bool[GateballGeometry.BallCount];
            bool[] outStates = new bool[GateballGeometry.BallCount];
            for (int current = 1; current <= GateballGeometry.BallCount; current++)
            {
                int next = GateballGameplayRules.FindNextPlayableBall(
                    current,
                    goals,
                    outStates,
                    GateballGeometry.BallCount);
                Assert.AreEqual(current == GateballGeometry.BallCount ? 1 : current + 1, next);
            }

            goals[1] = true;
            Assert.AreEqual(3, GateballGameplayRules.FindNextPlayableBall(1, goals, outStates, 10));
        }

        [Test]
        public void GateProgressOnlyAdvancesInOrder()
        {
            Assert.AreEqual(1, GateballGameplayRules.ApplyGatePass(0, 0, true));
            Assert.AreEqual(0, GateballGameplayRules.ApplyGatePass(0, 1, true));
            Assert.AreEqual(0, GateballGameplayRules.ApplyGatePass(0, 0, false));
            Assert.AreEqual(2, GateballGameplayRules.ApplyGatePass(1, 1, true));
            Assert.AreEqual(1, GateballGameplayRules.ApplyGatePass(1, 0, true));
        }

        [Test]
        public void GateAndGoalScoreUseOneAndTwoPointValues()
        {
            Assert.AreEqual(1, GateballGameplayRules.CalculateScoreDelta(0, 1));
            Assert.AreEqual(1, GateballGameplayRules.CalculateScoreDelta(1, 2));
            Assert.AreEqual(3, GateballGameplayRules.CalculateScoreDelta(0, 3));
            Assert.AreEqual(2, GateballGameplayRules.CalculateScoreDelta(3, 4));
            Assert.AreEqual(0, GateballGameplayRules.CalculateScoreDelta(4, 4));
        }

        [Test]
        public void ExtraStrokeRequiresGatePassWithoutTouchOrGoal()
        {
            Assert.IsTrue(GateballGameplayRules.ShouldGrantExtraStroke(true, false, false));
            Assert.IsFalse(GateballGameplayRules.ShouldGrantExtraStroke(false, false, false));
            Assert.IsFalse(GateballGameplayRules.ShouldGrantExtraStroke(true, true, false));
            Assert.IsFalse(GateballGameplayRules.ShouldGrantExtraStroke(true, false, true));
        }

        [Test]
        public void TouchRequiresAValidDifferentTarget()
        {
            Assert.IsTrue(GateballGameplayRules.IsTouchEstablished(1, 2, 1));
            Assert.IsFalse(GateballGameplayRules.IsTouchEstablished(1, 1, 1));
            Assert.IsFalse(GateballGameplayRules.IsTouchEstablished(1, 2, 0));
            Assert.IsTrue(GateballGameplayRules.ShouldEnterSpark(true, true, false));
            Assert.IsFalse(GateballGameplayRules.ShouldEnterSpark(true, false, false));
        }

        [Test]
        public void OutBallReturnsOnlyWhenItIsNotGoal()
        {
            Assert.IsTrue(GateballGameplayRules.ShouldReturnOutBall(true, false));
            Assert.IsFalse(GateballGameplayRules.ShouldReturnOutBall(false, false));
            Assert.IsFalse(GateballGameplayRules.ShouldReturnOutBall(true, true));
        }

        [Test]
        public void GameOverRequiresAllTenGoals()
        {
            bool[] goals = new bool[GateballGeometry.BallCount];
            Assert.IsFalse(GateballGameplayRules.IsGameOver(goals, goals.Length));
            for (int i = 0; i < goals.Length; i++)
            {
                goals[i] = true;
            }

            Assert.IsTrue(GateballGameplayRules.IsGameOver(goals, goals.Length));
        }

        [Test]
        public void DisconnectReassignsOnlyTheDepartedPlayersBalls()
        {
            int[] assignments = { 10, 20, 10, 30, 20, 10 };
            int[] activePlayers = { 20, 30 };

            GateballGameplayRules.ReassignDisconnectedControllers(assignments, 10, activePlayers, activePlayers.Length);

            CollectionAssert.AreEqual(new[] { 20, 20, 30, 30, 20, 20 }, assignments);
        }

        [Test]
        public void LateJoinSnapshotCopiesAllGameplayArrays()
        {
            int[] sourceProgress = { 1, 2, 3, 4 };
            int[] destinationProgress = new int[sourceProgress.Length];
            bool[] sourceGoals = { false, true, false, true };
            bool[] destinationGoals = new bool[sourceGoals.Length];

            GateballGameplayRules.CopyIntArray(sourceProgress, destinationProgress);
            GateballGameplayRules.CopyBoolArray(sourceGoals, destinationGoals);

            CollectionAssert.AreEqual(sourceProgress, destinationProgress);
            CollectionAssert.AreEqual(sourceGoals, destinationGoals);
        }
    }
}
