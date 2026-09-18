using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GateballGameplayState : UdonSharpBehaviour
    {
        [Header("References")]
        public GateballCourt Court;
        public GateballNetworkState NetworkState;

        [Header("Synchronized gameplay state")]
        [UdonSynced] public int Mode = GateballGameplayRules.ModePractice;
        [UdonSynced] public int GameplayPhase = GateballGameplayRules.PhaseSetup;
        [UdonSynced] public int CurrentBallId = 1;
        [UdonSynced] public int CurrentControllerPlayerId = -1;
        [UdonSynced] public int CurrentAuthorityPlayerId = -1;
        [UdonSynced] public int RedScore;
        [UdonSynced] public int WhiteScore;
        [UdonSynced] public int[] BallControllerPlayerIds = new int[GateballGeometry.BallCount];
        [UdonSynced] public int[] BallGateProgress = new int[GateballGeometry.BallCount];
        [UdonSynced] public int[] BallScores = new int[GateballGeometry.BallCount];
        [UdonSynced] public bool[] BallOutStates = new bool[GateballGeometry.BallCount];
        [UdonSynced] public bool[] BallGoalStates = new bool[GateballGeometry.BallCount];
        [UdonSynced] public int LastOutBallId = -1;
        [UdonSynced] public bool DidTouch;
        [UdonSynced] public int TouchedBallId = -1;
        [UdonSynced] public int SparkTargetBallId = -1;
        [UdonSynced] public int SparkPlacementIndex = -1;
        [UdonSynced] public bool SparkBallLocked;
        [UdonSynced] public bool SparkStroke;
        [UdonSynced] public int GameplayRevision;
        public int NetworkShotId;

        [System.NonSerialized] public string LocalStatus = string.Empty;

        private bool _initialized;
        private bool _recoveryPending;
        private int _departedPlayerId = -1;

        private void Start()
        {
            _EnsureReferences();
            _EnsureArrays();
            _ApplyStateToCourt();
            _initialized = true;
        }

        public override void OnDeserialization()
        {
            _EnsureReferences();
            _EnsureArrays();
            _ApplyStateToCourt();
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player == null || !player.isLocal)
            {
                return;
            }

            if (_recoveryPending)
            {
                SendCustomEventDelayedSeconds(nameof(_RecoverAfterDisconnect), 0.05f);
                return;
            }

            _RequestSerializationIfOwner();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid())
            {
                return;
            }

            bool affectsAssignment = player.playerId == CurrentControllerPlayerId;
            bool affectsShot = NetworkState != null
                && NetworkState.Phase == GateballNetworkState.PhaseSimulating
                && NetworkState.ShotAuthorityPlayerId == player.playerId;
            if (!affectsAssignment && !affectsShot)
            {
                return;
            }

            _departedPlayerId = player.playerId;
            _recoveryPending = true;
            SendCustomEventDelayedSeconds(nameof(_RecoverAfterDisconnect), 0.20f);
        }

        public void _StartPractice()
        {
            if (!_TakeOwnership())
            {
                return;
            }

            _BeginSession(GateballGameplayRules.ModePractice, null, 0);
            LocalStatus = "Practice started";
        }

        public void _StartMatch()
        {
            if (!_TakeOwnership())
            {
                return;
            }

            VRCPlayerApi[] players = new VRCPlayerApi[80];
            int playerCount = _CollectPlayers(players);
            if (playerCount < 2)
            {
                LocalStatus = "Match requires at least 2 players";
                return;
            }

            int[] playerIds = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerIds[i] = players[i].playerId;
            }

            _BeginSession(GateballGameplayRules.ModeMatch, playerIds, playerCount);
            LocalStatus = "Match started";
        }

        public void _EndMatch()
        {
            if (!_TakeOwnership())
            {
                return;
            }

            GameplayPhase = GateballGameplayRules.PhaseGameOver;
            GameplayRevision++;
            _RequestSerializationIfOwner();
            LocalStatus = "Match ended";
        }

        public bool _CanRequestStroke(int ballId)
        {
            if (!_initialized && NetworkState == null)
            {
                return false;
            }

            if (!GateballGeometry.IsValidBallId(ballId)
                || GameplayPhase == GateballGameplayRules.PhaseSetup
                || GameplayPhase == GateballGameplayRules.PhaseSimulating
                || GameplayPhase == GateballGameplayRules.PhaseResolvingShot
                || GameplayPhase == GateballGameplayRules.PhaseSparkPlacement
                || GameplayPhase == GateballGameplayRules.PhaseGameOver)
            {
                return false;
            }

            int ballIndex = GateballGeometry.BallIdToIndex(ballId);
            if (BallGoalStates != null && BallGoalStates[ballIndex])
            {
                return false;
            }

            if (Mode == GateballGameplayRules.ModePractice)
            {
                return true;
            }

            if (ballId != CurrentBallId)
            {
                return false;
            }

            if (Networking.LocalPlayer == null)
            {
                return true;
            }

            return CurrentControllerPlayerId == Networking.LocalPlayer.playerId;
        }

        public void _OnShotStarted()
        {
            if (!_IsLocalOwner())
            {
                return;
            }

            int shotId = NetworkShotId;
            bool startingSparkStroke = GameplayPhase == GateballGameplayRules.PhaseWaitingForSparkStroke;
            if (Mode == GateballGameplayRules.ModePractice
                && NetworkState != null
                && GateballGeometry.IsValidBallId(NetworkState.StrokeBallId))
            {
                CurrentBallId = NetworkState.StrokeBallId;
            }

            GameplayPhase = GateballGameplayRules.PhaseSimulating;
            SparkStroke = startingSparkStroke;
            DidTouch = false;
            TouchedBallId = -1;
            GameplayRevision = Mathf.Max(GameplayRevision, shotId);
            _RequestSerializationIfOwner();
        }

        public float _GetSparkTransferImpulse(float strikerImpulse)
        {
            return Mathf.Max(0f, strikerImpulse * 0.75f);
        }

        public bool _IsSparkStroke()
        {
            return SparkStroke && GateballGeometry.IsValidBallId(SparkTargetBallId);
        }

        public void _ResolveAuthoritativeShot(int shotId)
        {
            if (!_IsLocalOwner()
                || Court == null
                || GameplayPhase != GateballGameplayRules.PhaseSimulating)
            {
                return;
            }

            _EnsureArrays();
            GameplayPhase = GateballGameplayRules.PhaseResolvingShot;

            int resolvedBallId = CurrentBallId;
            if (NetworkState != null && GateballGeometry.IsValidBallId(NetworkState.StrokeBallId))
            {
                resolvedBallId = NetworkState.StrokeBallId;
            }

            int ballIndex = GateballGeometry.BallIdToIndex(resolvedBallId);
            if (ballIndex < 0)
            {
                GameplayPhase = GateballRulesPhaseAfterInvalidShot();
                _RequestSerializationIfOwner();
                return;
            }

            int previousProgress = BallGateProgress[ballIndex];
            int detectedProgress = Mathf.Clamp(Court._GetGateProgress(CurrentBallId), 0, 3);
            int nextProgress = Mathf.Max(previousProgress, detectedProgress);
            bool didTouch = GateballGameplayRules.IsTouchEstablished(
                resolvedBallId,
                Court._GetTouchTargetId(resolvedBallId),
                Court._GetTouchCount(resolvedBallId));
            int touchedBallId = didTouch ? Court._GetTouchTargetId(resolvedBallId) : -1;
            bool outState = Court._IsOut(resolvedBallId);
            bool goal = nextProgress >= 3 && Court._HasGoalPoleHit(resolvedBallId) && !outState;
            if (goal)
            {
                nextProgress = 4;
            }

            BallGateProgress[ballIndex] = nextProgress;
            DidTouch = didTouch;
            TouchedBallId = touchedBallId;
            BallOutStates[ballIndex] = outState;
            if (outState)
            {
                LastOutBallId = resolvedBallId;
            }

            if (goal)
            {
                BallGoalStates[ballIndex] = true;
            }

            if (Mode == GateballGameplayRules.ModeMatch)
            {
                int scoreDelta = GateballGameplayRules.CalculateScoreDelta(previousProgress, nextProgress);
                BallScores[ballIndex] += scoreDelta;
                if (scoreDelta > 0)
                {
                    if (GateballGameplayRules.TeamForBall(resolvedBallId) == 0)
                    {
                        RedScore += scoreDelta;
                    }
                    else
                    {
                        WhiteScore += scoreDelta;
                    }
                }
            }

            _ApplyStateToCourt();
            bool sparkStroke = SparkStroke;
            SparkStroke = false;

            if (Mode == GateballGameplayRules.ModePractice)
            {
                CurrentBallId = resolvedBallId;
                GameplayPhase = GateballGameplayRules.PhaseWaitingForStroke;
                SparkTargetBallId = -1;
                SparkPlacementIndex = -1;
                SparkBallLocked = false;
            }
            else if (GateballGameplayRules.IsGameOver(BallGoalStates, GateballGeometry.BallCount))
            {
                GameplayPhase = GateballGameplayRules.PhaseGameOver;
            }
            else if (sparkStroke)
            {
                GameplayPhase = GateballGameplayRules.PhaseWaitingForStroke;
                SparkTargetBallId = -1;
                SparkPlacementIndex = -1;
                SparkBallLocked = false;
            }
            else
            {
                int touchedIndex = GateballGeometry.BallIdToIndex(touchedBallId);
                bool targetPlayable = touchedIndex >= 0
                    && !BallGoalStates[touchedIndex]
                    && !BallOutStates[touchedIndex];
                if (GateballGameplayRules.ShouldEnterSpark(didTouch, targetPlayable, goal))
                {
                    SparkTargetBallId = touchedBallId;
                    SparkPlacementIndex = -1;
                    SparkBallLocked = false;
                    GameplayPhase = GateballGameplayRules.PhaseSparkPlacement;
                }
                else
                {
                    bool passedGate = nextProgress > previousProgress && nextProgress < 4;
                    if (GateballGameplayRules.ShouldGrantExtraStroke(passedGate, didTouch, goal))
                    {
                        GameplayPhase = GateballGameplayRules.PhaseWaitingForStroke;
                    }
                    else
                    {
                        _AdvanceTurn();
                    }
                }
            }

            CurrentAuthorityPlayerId = CurrentControllerPlayerId;
            GameplayRevision = Mathf.Max(GameplayRevision + 1, shotId + 1);
            _RequestSerializationIfOwner();
        }

        public void _ResolveAuthoritativeShotFromNetwork()
        {
            _ResolveAuthoritativeShot(NetworkShotId);
        }

        public void _SelectSparkTarget(int ballId)
        {
            if (!_IsLocalOwner() || GameplayPhase != GateballGameplayRules.PhaseSparkPlacement)
            {
                return;
            }

            if (ballId == TouchedBallId && GateballGeometry.IsValidBallId(ballId))
            {
                SparkTargetBallId = ballId;
                _RequestSerializationIfOwner();
            }
        }

        public void _PlaceSparkForward()
        {
            _PlaceSpark(GateballGameplayRules.SparkPlacementForward);
        }

        public void _PlaceSparkRight()
        {
            _PlaceSpark(GateballGameplayRules.SparkPlacementRight);
        }

        public void _PlaceSparkBack()
        {
            _PlaceSpark(GateballGameplayRules.SparkPlacementBack);
        }

        public void _PlaceSparkLeft()
        {
            _PlaceSpark(GateballGameplayRules.SparkPlacementLeft);
        }

        public void _PlaceSparkDefault()
        {
            _PlaceSpark(GateballGameplayRules.SparkPlacementForward);
        }

        public string _GetGameplayPhaseName()
        {
            switch (GameplayPhase)
            {
                case GateballGameplayRules.PhaseSetup: return "Setup";
                case GateballGameplayRules.PhaseWaitingForStroke: return "WaitingForStroke";
                case GateballGameplayRules.PhaseSimulating: return "Simulating";
                case GateballGameplayRules.PhaseResolvingShot: return "ResolvingShot";
                case GateballGameplayRules.PhaseSparkPlacement: return "SparkPlacement";
                case GateballGameplayRules.PhaseWaitingForSparkStroke: return "WaitingForSparkStroke";
                case GateballGameplayRules.PhaseGameOver: return "GameOver";
                default: return "Unknown";
            }
        }

        public string _GetModeName()
        {
            return Mode == GateballGameplayRules.ModeMatch ? "Match" : "Practice";
        }

        public int _GetCurrentControllerPlayerId()
        {
            return CurrentControllerPlayerId;
        }

        public int _GetCurrentProgress()
        {
            int index = GateballGeometry.BallIdToIndex(CurrentBallId);
            return index < 0 || BallGateProgress == null || index >= BallGateProgress.Length
                ? 0
                : BallGateProgress[index];
        }

        public void _RecoverAfterDisconnect()
        {
            if (!_recoveryPending || !_IsLocalOwner())
            {
                return;
            }

            _recoveryPending = false;
            VRCPlayerApi[] players = new VRCPlayerApi[80];
            int playerCount = _CollectPlayers(players);
            int[] playerIds = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerIds[i] = players[i].playerId;
            }

            if (NetworkState != null
                && NetworkState.Phase == GateballNetworkState.PhaseSimulating
                && NetworkState.ShotAuthorityPlayerId == _departedPlayerId)
            {
                NetworkState._RollbackToAuthoritativeState();
                GameplayPhase = GateballGameplayRules.PhaseWaitingForStroke;
                SparkStroke = false;
            }

            GateballGameplayRules.ReassignDisconnectedControllers(
                BallControllerPlayerIds,
                _departedPlayerId,
                playerIds,
                playerCount);

            int currentIndex = GateballGeometry.BallIdToIndex(CurrentBallId);
            if (currentIndex >= 0 && currentIndex < BallControllerPlayerIds.Length)
            {
                CurrentControllerPlayerId = BallControllerPlayerIds[currentIndex];
            }

            CurrentAuthorityPlayerId = CurrentControllerPlayerId;
            GameplayRevision++;
            _RequestSerializationIfOwner();
            LocalStatus = "Player left; controller reassigned";
        }

        private void _BeginSession(int mode, int[] playerIds, int playerCount)
        {
            _EnsureArrays();
            Mode = mode;
            GameplayPhase = GateballGameplayRules.PhaseWaitingForStroke;
            CurrentBallId = 1;
            CurrentControllerPlayerId = -1;
            CurrentAuthorityPlayerId = -1;
            RedScore = 0;
            WhiteScore = 0;
            LastOutBallId = -1;
            DidTouch = false;
            TouchedBallId = -1;
            SparkTargetBallId = -1;
            SparkPlacementIndex = -1;
            SparkBallLocked = false;
            SparkStroke = false;
            GameplayRevision++;

            for (int i = 0; i < GateballGeometry.BallCount; i++)
            {
                BallControllerPlayerIds[i] = -1;
                BallGateProgress[i] = 0;
                BallScores[i] = 0;
                BallOutStates[i] = false;
                BallGoalStates[i] = false;
            }

            if (mode == GateballGameplayRules.ModeMatch)
            {
                GateballGameplayRules.AssignBallsToPlayers(playerIds, playerCount, BallControllerPlayerIds);
                CurrentControllerPlayerId = BallControllerPlayerIds[0];
                CurrentAuthorityPlayerId = CurrentControllerPlayerId;
            }

            if (NetworkState != null)
            {
                NetworkState._ResetForGameplay();
            }
            else if (Court != null)
            {
                Court._ResetAll();
            }

            _ApplyStateToCourt();
            _RequestSerializationIfOwner();
        }

        private void _AdvanceTurn()
        {
            int nextBallId = GateballGameplayRules.FindNextPlayableBall(
                CurrentBallId,
                BallGoalStates,
                BallOutStates,
                GateballGeometry.BallCount);
            if (nextBallId < 0)
            {
                GameplayPhase = GateballGameplayRules.PhaseGameOver;
                return;
            }

            CurrentBallId = nextBallId;
            int index = GateballGeometry.BallIdToIndex(nextBallId);
            CurrentControllerPlayerId = index < 0 ? -1 : BallControllerPlayerIds[index];
            CurrentAuthorityPlayerId = CurrentControllerPlayerId;
            _ReturnOutBallIfNeeded(nextBallId);
            GameplayPhase = GateballGameplayRules.PhaseWaitingForStroke;
        }

        private void _ReturnOutBallIfNeeded(int ballId)
        {
            int index = GateballGeometry.BallIdToIndex(ballId);
            if (index < 0 || !BallOutStates[index] || Court == null)
            {
                return;
            }

            Vector3 position = new Vector3(
                ((ballId - 1) % 5 - 2) * 0.25f,
                GateballGeometry.BallRadius,
                -Court.CourtLength * 0.5f + 0.5f + ((ballId - 1) / 5) * 0.15f);
            Court._SetBallPosition(ballId, position);
            BallOutStates[index] = false;
            _ApplyStateToCourt();
        }

        private void _PlaceSpark(int placementIndex)
        {
            if (!_IsLocalOwner()
                || GameplayPhase != GateballGameplayRules.PhaseSparkPlacement
                || Court == null)
            {
                return;
            }

            int targetBallId = SparkTargetBallId;
            if (!GateballGeometry.IsValidBallId(targetBallId))
            {
                targetBallId = TouchedBallId;
            }

            GateballBall target = Court._GetBall(targetBallId);
            GateballBall striker = Court._GetBall(CurrentBallId);
            if (target == null || striker == null || target.Body == null || striker.Body == null)
            {
                return;
            }

            Vector3[] directions =
            {
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left
            };
            int start = Mathf.Clamp(placementIndex, 0, directions.Length - 1);
            Vector3 targetPosition = target.Body.position;
            float spacing = GateballGeometry.BallRadius * 2f + 0.01f;
            Vector3 selectedPosition = Vector3.zero;
            int selectedIndex = -1;
            for (int i = 0; i < directions.Length; i++)
            {
                int candidateIndex = (start + i) % directions.Length;
                Vector3 candidate = targetPosition - directions[candidateIndex] * spacing;
                if (!_IsSparkPositionSafe(candidate, targetBallId))
                {
                    continue;
                }

                selectedPosition = candidate;
                selectedIndex = candidateIndex;
                break;
            }

            if (selectedIndex < 0)
            {
                return;
            }

            Court._SetBallPosition(CurrentBallId, selectedPosition);
            _ApplyStateToCourt();
            SparkTargetBallId = targetBallId;
            SparkPlacementIndex = selectedIndex;
            SparkBallLocked = true;
            GameplayPhase = GateballGameplayRules.PhaseWaitingForSparkStroke;
            GameplayRevision++;
            _RequestSerializationIfOwner();
        }

        private bool _IsSparkPositionSafe(Vector3 candidate, int targetBallId)
        {
            Vector3 local = Court.transform.InverseTransformPoint(candidate);
            if (GateballGeometry.IsOutOfCourt(local, Court.CourtWidth, Court.CourtLength, 0f))
            {
                return false;
            }

            for (int i = 0; i < Court.Balls.Length; i++)
            {
                GateballBall ball = Court.Balls[i];
                if (ball == null || ball.BallId == CurrentBallId || ball.BallId == targetBallId || ball.Body == null)
                {
                    continue;
                }

                if (GateballGeometry.IsBallTouchingBall(
                    candidate,
                    ball.Body.position,
                    GateballGeometry.BallRadius,
                    ball.Radius + 0.002f))
                {
                    return false;
                }
            }

            return true;
        }

        private void _EnsureReferences()
        {
            if (NetworkState == null)
            {
                NetworkState = GetComponent<GateballNetworkState>();
            }

            if (Court == null && NetworkState != null)
            {
                Court = NetworkState.Court;
            }
        }

        private void _EnsureArrays()
        {
            if (BallControllerPlayerIds == null || BallControllerPlayerIds.Length != GateballGeometry.BallCount)
            {
                BallControllerPlayerIds = new int[GateballGeometry.BallCount];
            }

            if (BallGateProgress == null || BallGateProgress.Length != GateballGeometry.BallCount)
            {
                BallGateProgress = new int[GateballGeometry.BallCount];
            }

            if (BallScores == null || BallScores.Length != GateballGeometry.BallCount)
            {
                BallScores = new int[GateballGeometry.BallCount];
            }

            if (BallOutStates == null || BallOutStates.Length != GateballGeometry.BallCount)
            {
                BallOutStates = new bool[GateballGeometry.BallCount];
            }

            if (BallGoalStates == null || BallGoalStates.Length != GateballGeometry.BallCount)
            {
                BallGoalStates = new bool[GateballGeometry.BallCount];
            }
        }

        private void _ApplyStateToCourt()
        {
            if (Court != null)
            {
                Court._ApplyAuthoritativeGameplayState(BallGateProgress, BallOutStates, BallGoalStates);
            }
        }

        private int _CollectPlayers(VRCPlayerApi[] players)
        {
            if (players == null)
            {
                return 0;
            }

            int count = Mathf.Min(VRCPlayerApi.GetPlayerCount(), players.Length);
            if (count > 0)
            {
                VRCPlayerApi.GetPlayers(players);
            }

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (players[j] != null && (players[i] == null || players[j].playerId < players[i].playerId))
                    {
                        VRCPlayerApi swap = players[i];
                        players[i] = players[j];
                        players[j] = swap;
                    }
                }
            }

            int validCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (players[i] != null && players[i].IsValid())
                {
                    players[validCount] = players[i];
                    validCount++;
                }
            }

            return validCount;
        }

        private bool _TakeOwnership()
        {
            if (Networking.LocalPlayer == null)
            {
                return true;
            }

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            return Networking.IsOwner(gameObject);
        }

        private bool _IsLocalOwner()
        {
            if (Networking.LocalPlayer == null)
            {
                return true;
            }

            return Networking.IsOwner(gameObject);
        }

        private void _RequestSerializationIfOwner()
        {
            if (_IsLocalOwner() && Networking.LocalPlayer != null)
            {
                RequestSerialization();
            }
        }

        private int GateballRulesPhaseAfterInvalidShot()
        {
            return Mode == GateballGameplayRules.ModeMatch
                ? GateballGameplayRules.PhaseWaitingForStroke
                : GateballGameplayRules.PhaseGameOver;
        }
    }
}
