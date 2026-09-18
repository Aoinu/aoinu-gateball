using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    public static class GateballGameplayRules
    {
        public const int ModePractice = 0;
        public const int ModeMatch = 1;

        public const int PhaseSetup = 0;
        public const int PhaseWaitingForStroke = 1;
        public const int PhaseSimulating = 2;
        public const int PhaseResolvingShot = 3;
        public const int PhaseSparkPlacement = 4;
        public const int PhaseWaitingForSparkStroke = 5;
        public const int PhaseGameOver = 6;

        public const int SparkPlacementForward = 0;
        public const int SparkPlacementRight = 1;
        public const int SparkPlacementBack = 2;
        public const int SparkPlacementLeft = 3;

        public static int TeamForBall(int ballId)
        {
            return GateballGeometry.IsRedBall(ballId) ? 0 : 1;
        }

        public static void AssignBallsToPlayers(int[] playerIds, int playerCount, int[] assignments)
        {
            if (assignments == null)
            {
                return;
            }

            int safePlayerCount = Mathf.Clamp(playerCount, 0, playerIds == null ? 0 : playerIds.Length);
            for (int i = 0; i < assignments.Length; i++)
            {
                assignments[i] = safePlayerCount == 0 ? -1 : playerIds[i % safePlayerCount];
            }
        }

        public static int FindNextPlayableBall(
            int currentBallId,
            int[] goalStates,
            int[] outStates,
            int ballCount)
        {
            if (ballCount <= 0)
            {
                return -1;
            }

            int startIndex = Mathf.Clamp(currentBallId - 1, -1, ballCount - 1);
            for (int offset = 1; offset <= ballCount; offset++)
            {
                int index = (startIndex + offset) % ballCount;
                bool isGoal = goalStates != null && index < goalStates.Length && goalStates[index] != 0;
                if (!isGoal)
                {
                    return index + 1;
                }
            }

            return -1;
        }

        public static int FindNextPlayableBall(
            int currentBallId,
            bool[] goalStates,
            bool[] outStates,
            int ballCount)
        {
            if (ballCount <= 0)
            {
                return -1;
            }

            int startIndex = Mathf.Clamp(currentBallId - 1, -1, ballCount - 1);
            for (int offset = 1; offset <= ballCount; offset++)
            {
                int index = (startIndex + offset) % ballCount;
                bool isGoal = goalStates != null && index < goalStates.Length && goalStates[index];
                if (!isGoal)
                {
                    return index + 1;
                }
            }

            return -1;
        }

        public static int ApplyGatePass(int currentProgress, int gateIndex, bool forwardPass)
        {
            if (!forwardPass || gateIndex != currentProgress)
            {
                return Mathf.Clamp(currentProgress, 0, 4);
            }

            return Mathf.Clamp(currentProgress + 1, 0, 4);
        }

        public static int CalculateScoreDelta(int previousProgress, int nextProgress)
        {
            int previous = Mathf.Clamp(previousProgress, 0, 4);
            int next = Mathf.Clamp(nextProgress, 0, 4);
            int delta = 0;

            if (previous < 1 && next >= 1)
            {
                delta++;
            }

            if (previous < 2 && next >= 2)
            {
                delta++;
            }

            if (previous < 3 && next >= 3)
            {
                delta++;
            }

            if (previous < 4 && next >= 4)
            {
                delta += 2;
            }

            return delta;
        }

        public static bool IsTouchEstablished(int strikerBallId, int touchedBallId, int touchCount)
        {
            return GateballGeometry.IsValidBallId(strikerBallId)
                && GateballGeometry.IsValidBallId(touchedBallId)
                && strikerBallId != touchedBallId
                && touchCount > 0;
        }

        public static bool ShouldEnterSpark(bool didTouch, bool touchedBallIsPlayable, bool strikerIsGoal)
        {
            return didTouch && touchedBallIsPlayable && !strikerIsGoal;
        }

        public static bool ShouldGrantExtraStroke(bool passedGate, bool didTouch, bool goal)
        {
            return passedGate && !didTouch && !goal;
        }

        public static bool ShouldReturnOutBall(bool outState, bool goalState)
        {
            return outState && !goalState;
        }

        public static bool IsGameOver(bool[] goalStates, int ballCount)
        {
            if (goalStates == null || ballCount <= 0)
            {
                return false;
            }

            int count = Mathf.Min(ballCount, goalStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (!goalStates[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static void ReassignDisconnectedControllers(
            int[] assignments,
            int disconnectedPlayerId,
            int[] activePlayerIds,
            int activePlayerCount)
        {
            if (assignments == null || activePlayerIds == null || activePlayerCount <= 0)
            {
                return;
            }

            int safeCount = Mathf.Min(activePlayerCount, activePlayerIds.Length);
            int replacementIndex = 0;
            for (int i = 0; i < assignments.Length; i++)
            {
                if (assignments[i] != disconnectedPlayerId)
                {
                    continue;
                }

                assignments[i] = activePlayerIds[replacementIndex % safeCount];
                replacementIndex++;
            }
        }

        public static void CopyIntArray(int[] source, int[] destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int count = Mathf.Min(source.Length, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }

        public static void CopyBoolArray(bool[] source, bool[] destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int count = Mathf.Min(source.Length, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }
    }
}
