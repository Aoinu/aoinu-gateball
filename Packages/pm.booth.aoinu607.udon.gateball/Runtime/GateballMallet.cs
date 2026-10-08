using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GateballMallet : UdonSharpBehaviour
    {
        private const int NoMalletHolderPlayerId = -1;

        [Header("References")]
        public GateballStrokeRouter StrokeRouter;
        public Transform MalletRig;
        public Transform GripA;
        public Transform GripB;
        public Transform Head;
        public Transform StrikeFace;
        public BoxCollider HeadCollider;
        public LineRenderer AimPreview;

        [Header("Aim")]
        public int AimLayerMask;
        public float TargetSearchDistance = 0.30f;
        public float PreviewBoxDepth = 0.02f;
        public float PreviewLength = 3f;
        public float PlayerMoveUnlockDistance = 0.15f;
        public float LockedBallMoveDistance = 0.04f;
        public float LockedBallMoveSpeed = 0.05f;

        [Header("Swing")]
        public float VelocityWindowSeconds = 0.03f;
        public float DeadzoneSpeed = 0.10f;
        public float BallSpeedPerMalletSpeed = 1f;
        public float MaximumBallSpeed = 6f;
        public float AimAssistStrength = 0.75f;
        public float MaxAimDeviationDegrees = 12f;
        public float InvalidSwingAngleDegrees = 90f;
        public float RollingFactor = 1f;
        public float HitCooldown = 0.18f;
        public Color PreviewColor = new Color(0.25f, 0.85f, 1f, 0.9f);
        public Color ArmedColor = new Color(1f, 0.75f, 0.15f, 0.95f);

        private const int HistorySize = 32;
        private Vector3[] _historyPositions = new Vector3[HistorySize];
        private float[] _historyTimes = new float[HistorySize];
        private int _historyCount;
        private int _historyIndex;
        private Vector3 _measuredHeadVelocity;
        private int _primaryGripIndex = -1;
        private int _secondaryGripIndex = -1;
        [UdonSynced] private int _holderPlayerId = NoMalletHolderPlayerId;
        private Vector3 _primaryLocalGripPosition;
        private Quaternion _primaryLocalGripRotation = Quaternion.identity;
        private Vector3 _localShaftDirection = Vector3.down;
        private Vector3 _neutralShaftDirection;
        private Quaternion _neutralMalletRotation = Quaternion.identity;
        private Quaternion _neutralPrimaryGripRotation = Quaternion.identity;
        private Quaternion _neutralSecondaryGripRotation = Quaternion.identity;
        private bool _isLocallyHeld;
        private bool _isAimLocked;
        private bool _hasValidPreview;
        private GateballBall _previewBall;
        private Vector3 _previewContactPoint;
        private Vector3 _previewAimDirection;
        private GateballBall _lockedBall;
        private int _lockedTargetBallId = -1;
        private Vector3 _lockedAimDirection;
        private Vector3 _lockedContactPoint;
        private Vector3 _lockedBallPosition;
        private Vector3 _lockedStrikeFacePosition;
        private Quaternion _lockedHeadRotation = Quaternion.identity;
        private Vector3 _lockedPlayerPosition;
        private float _lockedRollDegrees;
        private Vector3 _previousStrikeFacePosition;
        private int _lastHitBallId = -1;
        private float _lastHitTime = -100f;

        private void Start()
        {
            if (MalletRig == null)
            {
                MalletRig = transform;
            }

            _CacheLocalShaftDirection();
            _ResetHistory();
            _SetPreviewVisible(false);
        }

        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi newOwner)
        {
            if (newOwner == null || !Utilities.IsValid(newOwner))
            {
                return false;
            }

            return GateballMalletRules.CanAcquireMalletAuthority(_holderPlayerId, newOwner.playerId);
        }

        public override void OnDeserialization()
        {
            if (!_isLocallyHeld)
            {
                return;
            }

            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null || !Utilities.IsValid(localPlayer)
                || _holderPlayerId != localPlayer.playerId
                || !_IsLocalAuthority())
            {
                _ClearLocalGripState();
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (player == null || !Utilities.IsValid(player)
                || player.playerId != _holderPlayerId
                || MalletRig == null
                || !Networking.IsOwner(MalletRig.gameObject))
            {
                return;
            }

            _holderPlayerId = NoMalletHolderPlayerId;
            RequestSerialization();
        }

        private void Update()
        {
            if (!_isLocallyHeld || !_IsLocalAuthority())
            {
                _hasValidPreview = false;
                _SetPreviewVisible(false);
                return;
            }

            _UpdateMalletPose();
            if (_isAimLocked)
            {
                if (!_IsAimLockValid())
                {
                    _InvalidateAimLock();
                }
                else
                {
                    _SetPreviewLine(_lockedBallPosition, _lockedAimDirection, ArmedColor);
                }
            }
            else
            {
                _UpdateAimPreview();
            }
        }

        private void FixedUpdate()
        {
            if (!_isLocallyHeld || !_IsLocalAuthority() || Head == null || StrikeFace == null)
            {
                return;
            }

            _RecordHeadPosition(Head.position, Time.fixedTime);
            _measuredHeadVelocity = GateballMalletRules.EstimateWindowVelocity(
                _historyPositions,
                _historyTimes,
                _historyIndex,
                _historyCount,
                Time.fixedTime,
                VelocityWindowSeconds);

            if (_isAimLocked)
            {
                if (!_IsAimLockValid())
                {
                    _InvalidateAimLock();
                }
                else
                {
                    _TrySweepImpact();
                }
            }
        }

        public void _OnGripPicked(int gripIndex)
        {
            if (!_IsValidGripIndex(gripIndex) || _GetGrip(gripIndex) == null
                || !_TryAcquireMalletAuthority())
            {
                return;
            }

            if (_primaryGripIndex < 0)
            {
                _primaryGripIndex = gripIndex;
                _isLocallyHeld = true;
                _CaptureSingleHandPose();
                _ResetHistory();
                return;
            }

            if (gripIndex == _primaryGripIndex || _secondaryGripIndex >= 0)
            {
                return;
            }

            if (_isAimLocked)
            {
                _InvalidateAimLock();
            }

            _secondaryGripIndex = gripIndex;
            _CaptureTwoHandNeutralPose();
        }

        public void _OnGripDropped(int gripIndex)
        {
            if (gripIndex != _primaryGripIndex && gripIndex != _secondaryGripIndex)
            {
                return;
            }

            if (_isAimLocked)
            {
                _InvalidateAimLock();
            }

            if (gripIndex == _primaryGripIndex && _secondaryGripIndex >= 0)
            {
                _primaryGripIndex = _secondaryGripIndex;
                _secondaryGripIndex = -1;
                _CaptureSingleHandPose();
            }
            else if (gripIndex == _secondaryGripIndex)
            {
                _secondaryGripIndex = -1;
                _CaptureSingleHandPose();
            }
            else
            {
                _primaryGripIndex = -1;
                _secondaryGripIndex = -1;
                _isLocallyHeld = false;
                _ResetHistory();
                _SetPreviewVisible(false);
                _ReleaseMalletAuthority();
            }
        }

        public void _OnGripUseDown(int gripIndex)
        {
            if (gripIndex != _primaryGripIndex || !_isLocallyHeld
                || !GateballMalletRules.IsValidGripMode(_primaryGripIndex, _secondaryGripIndex)
                || _isAimLocked || !_IsLocalAuthority() || !_hasValidPreview
                || _previewBall == null || _IsShotInProgress())
            {
                return;
            }

            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null || !Utilities.IsValid(localPlayer))
            {
                return;
            }

            _UpdateMalletPose();
            if (_secondaryGripIndex >= 0)
            {
                Transform primaryGrip = _GetGrip(_primaryGripIndex);
                Transform secondaryGrip = _GetGrip(_secondaryGripIndex);
                Vector3 currentShaftDirection = secondaryGrip.position - primaryGrip.position;
                _lockedRollDegrees = GateballMalletRules.CalculateAverageTwistDegrees(
                    _neutralPrimaryGripRotation,
                    primaryGrip.rotation,
                    _neutralSecondaryGripRotation,
                    secondaryGrip.rotation,
                    currentShaftDirection);
            }
            else
            {
                Transform primaryGrip = _GetGrip(_primaryGripIndex);
                GateballMalletRules.SolveSingleHandPose(
                    primaryGrip.position,
                    primaryGrip.rotation,
                    _primaryLocalGripPosition,
                    _primaryLocalGripRotation,
                    out _,
                    out Quaternion currentMalletRotation);
                Vector3 localShaftDirection = _GetLocalShaftDirection();
                Vector3 currentShaftDirection = currentMalletRotation * localShaftDirection;
                _lockedRollDegrees = GateballMalletRules.CalculatePoseRollDegrees(
                    _neutralMalletRotation,
                    _neutralShaftDirection,
                    currentMalletRotation,
                    currentShaftDirection,
                    _GetLocalFaceForward());
            }

            _isAimLocked = true;
            _lockedBall = _previewBall;
            _lockedTargetBallId = _previewBall.BallId;
            _lockedAimDirection = _previewAimDirection;
            _lockedContactPoint = _previewContactPoint;
            _lockedBallPosition = _previewBall.transform.position;
            _lockedStrikeFacePosition = StrikeFace.position;
            _lockedHeadRotation = Head == null ? MalletRig.rotation : Head.rotation;
            _lockedPlayerPosition = localPlayer.GetPosition();
            _previousStrikeFacePosition = _lockedStrikeFacePosition;
            _ResetHistory();
            _RecordHeadPosition(Head.position, Time.fixedTime);
            _SetPreviewLine(_lockedBallPosition, _lockedAimDirection, ArmedColor);
        }

        public void _OnGripUseUp(int gripIndex)
        {
            if (gripIndex == _primaryGripIndex && _isAimLocked)
            {
                _InvalidateAimLock();
            }
        }

        private void _UpdateMalletPose()
        {
            if (MalletRig == null || _primaryGripIndex < 0)
            {
                return;
            }

            Transform primaryGrip = _GetGrip(_primaryGripIndex);
            if (primaryGrip == null)
            {
                return;
            }

            Vector3 malletPosition;
            Quaternion malletRotation;
            if (_secondaryGripIndex >= 0)
            {
                Transform secondaryGrip = _GetGrip(_secondaryGripIndex);
                if (secondaryGrip == null)
                {
                    return;
                }

                Vector3 currentShaftDirection = secondaryGrip.position - primaryGrip.position;
                if (!(currentShaftDirection.sqrMagnitude > 0.0001f))
                {
                    return;
                }

                float rollDegrees = GateballMalletRules.ResolveTwoHandRollDegrees(
                    _isAimLocked,
                    _lockedRollDegrees,
                    _neutralPrimaryGripRotation,
                    primaryGrip.rotation,
                    _neutralSecondaryGripRotation,
                    secondaryGrip.rotation,
                    currentShaftDirection);
                malletRotation = GateballMalletRules.SolveTwoHandRotation(
                    _neutralMalletRotation,
                    _neutralShaftDirection,
                    currentShaftDirection,
                    rollDegrees);
                malletPosition = primaryGrip.position - malletRotation * _primaryLocalGripPosition;
            }
            else
            {
                GateballMalletRules.SolveSingleHandPose(
                    primaryGrip.position,
                    primaryGrip.rotation,
                    _primaryLocalGripPosition,
                    _primaryLocalGripRotation,
                    out malletPosition,
                    out malletRotation);
                if (_isAimLocked)
                {
                    Vector3 currentShaftDirection = malletRotation * _GetLocalShaftDirection();
                    malletRotation = GateballMalletRules.SolveTwoHandRotation(
                        _neutralMalletRotation,
                        _neutralShaftDirection,
                        currentShaftDirection,
                        _lockedRollDegrees);
                    malletPosition = primaryGrip.position - malletRotation * _primaryLocalGripPosition;
                }
            }

            MalletRig.position = malletPosition;
            MalletRig.rotation = malletRotation;
        }

        private void _CaptureSingleHandPose()
        {
            if (MalletRig == null || _primaryGripIndex < 0)
            {
                return;
            }

            Transform primaryGrip = _GetGrip(_primaryGripIndex);
            if (primaryGrip == null)
            {
                return;
            }

            Quaternion inverseMalletRotation = Quaternion.Inverse(MalletRig.rotation);
            _primaryLocalGripPosition = inverseMalletRotation * (primaryGrip.position - MalletRig.position);
            _primaryLocalGripRotation = inverseMalletRotation * primaryGrip.rotation;
            _neutralMalletRotation = MalletRig.rotation;
            _neutralShaftDirection = MalletRig.rotation * _GetLocalShaftDirection();
            _neutralPrimaryGripRotation = primaryGrip.rotation;
            if (_secondaryGripIndex >= 0)
            {
                _CaptureTwoHandNeutralPose();
            }
        }

        private void _CaptureTwoHandNeutralPose()
        {
            Transform primaryGrip = _GetGrip(_primaryGripIndex);
            Transform secondaryGrip = _GetGrip(_secondaryGripIndex);
            if (primaryGrip == null || secondaryGrip == null || MalletRig == null)
            {
                return;
            }

            _neutralShaftDirection = secondaryGrip.position - primaryGrip.position;
            if (!(_neutralShaftDirection.sqrMagnitude > 0.0001f))
            {
                _secondaryGripIndex = -1;
                return;
            }

            _neutralShaftDirection = _neutralShaftDirection.normalized;
            _neutralMalletRotation = MalletRig.rotation;
            _neutralPrimaryGripRotation = primaryGrip.rotation;
            _neutralSecondaryGripRotation = secondaryGrip.rotation;
        }

        private void _UpdateAimPreview()
        {
            _hasValidPreview = false;
            _previewBall = null;
            if (!GateballMalletRules.IsValidGripMode(_primaryGripIndex, _secondaryGripIndex)
                || StrikeFace == null || HeadCollider == null || _IsShotInProgress()
                || !_TargetSearchDistanceIsValid())
            {
                _SetPreviewVisible(false);
                return;
            }

            Vector3 direction = StrikeFace.forward;
            Vector3 origin = StrikeFace.position - direction * PreviewBoxDepth;
            RaycastHit hit;
            bool hitDetected = Physics.BoxCast(
                origin,
                _GetFaceHalfExtents(),
                direction,
                out hit,
                StrikeFace.rotation,
                TargetSearchDistance,
                AimLayerMask,
                QueryTriggerInteraction.Ignore);
            if (!hitDetected || hit.collider == null)
            {
                _SetPreviewVisible(false);
                return;
            }

            GateballBall ball = hit.collider.GetComponent<GateballBall>();
            if (ball == null || !ball._IsReady()
                || ball.Body.velocity.sqrMagnitude > LockedBallMoveSpeed * LockedBallMoveSpeed)
            {
                _SetPreviewVisible(false);
                return;
            }

            Vector3 aimDirection = Vector3.ProjectOnPlane(ball.transform.position - hit.point, Vector3.up);
            if (!(aimDirection.sqrMagnitude > 0.000001f))
            {
                aimDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            }

            if (!(aimDirection.sqrMagnitude > 0.000001f))
            {
                _SetPreviewVisible(false);
                return;
            }

            _previewBall = ball;
            _previewContactPoint = hit.point;
            _previewAimDirection = aimDirection.normalized;
            _hasValidPreview = true;
            _SetPreviewLine(ball.transform.position, _previewAimDirection, PreviewColor);
        }

        private void _TrySweepImpact()
        {
            if (!_isAimLocked || _lockedBall == null || _lockedBall.BallId != _lockedTargetBallId
                || _IsShotInProgress()
                || (_lastHitBallId == _lockedTargetBallId
                    && Time.fixedTime - _lastHitTime < HitCooldown))
            {
                return;
            }

            Vector3 currentPosition = StrikeFace.position;
            Vector3 delta = currentPosition - _previousStrikeFacePosition;
            float distance = delta.magnitude;
            if (!(distance > 0.0001f))
            {
                _previousStrikeFacePosition = currentPosition;
                return;
            }

            Vector3 faceForward = StrikeFace.forward;
            RaycastHit hit;
            bool hitDetected = Physics.BoxCast(
                _previousStrikeFacePosition - faceForward * PreviewBoxDepth,
                _GetFaceHalfExtents(),
                delta / distance,
                out hit,
                StrikeFace.rotation,
                distance + PreviewBoxDepth,
                AimLayerMask,
                QueryTriggerInteraction.Ignore);
            _previousStrikeFacePosition = currentPosition;
            if (!hitDetected || hit.collider == null)
            {
                return;
            }

            GateballBall hitBall = hit.collider.GetComponent<GateballBall>();
            if (hitBall == null || hitBall != _lockedBall || hitBall.BallId != _lockedTargetBallId)
            {
                return;
            }

            GateballNetworkState networkState = StrokeRouter == null ? null : StrokeRouter.NetworkState;
            if (networkState != null && networkState.Phase == GateballNetworkState.PhaseSimulating)
            {
                _InvalidateAimLock();
                return;
            }

            if (!GateballMalletRules.TryBuildShotVelocities(
                    _lockedAimDirection,
                    _measuredHeadVelocity,
                    Vector3.up,
                    DeadzoneSpeed,
                    BallSpeedPerMalletSpeed,
                    MaximumBallSpeed,
                    AimAssistStrength,
                    MaxAimDeviationDegrees,
                    InvalidSwingAngleDegrees,
                    hitBall.Radius,
                    RollingFactor,
                    out Vector3 initialLinearVelocity,
                    out Vector3 initialAngularVelocity))
            {
                return;
            }

            if (StrokeRouter != null
                && StrokeRouter._ApplyShotByVelocity(hitBall.BallId, initialLinearVelocity, initialAngularVelocity))
            {
                _lastHitBallId = hitBall.BallId;
                _lastHitTime = Time.fixedTime;
                _InvalidateAimLock();
            }
        }

        private bool _IsAimLockValid()
        {
            if (!_isAimLocked || !_isLocallyHeld
                || !GateballMalletRules.IsValidGripMode(_primaryGripIndex, _secondaryGripIndex)
                || !_IsLocalAuthority() || _lockedBall == null || !_lockedBall.isActiveAndEnabled
                || !_lockedBall._IsReady() || _lockedBall.BallId != _lockedTargetBallId
                || Networking.LocalPlayer == null || !Utilities.IsValid(Networking.LocalPlayer))
            {
                return false;
            }

            if (_IsShotInProgress())
            {
                return false;
            }

            Vector3 playerDelta = Networking.LocalPlayer.GetPosition() - _lockedPlayerPosition;
            playerDelta.y = 0f;
            if (playerDelta.magnitude > PlayerMoveUnlockDistance)
            {
                return false;
            }

            Vector3 ballDelta = _lockedBall.transform.position - _lockedBallPosition;
            ballDelta.y = 0f;
            float moveSpeedSquared = LockedBallMoveSpeed * LockedBallMoveSpeed;
            if (ballDelta.magnitude > LockedBallMoveDistance
                || _lockedBall.Body.velocity.sqrMagnitude > moveSpeedSquared)
            {
                return false;
            }

            return true;
        }

        private void _InvalidateAimLock()
        {
            _isAimLocked = false;
            _lockedBall = null;
            _lockedTargetBallId = -1;
            _hasValidPreview = false;
            _previewBall = null;
            _SetPreviewVisible(false);
        }

        private bool _IsShotInProgress()
        {
            return StrokeRouter != null
                && StrokeRouter.NetworkState != null
                && StrokeRouter.NetworkState.Phase == GateballNetworkState.PhaseSimulating;
        }

        private bool _IsLocalAuthority()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null || !Utilities.IsValid(localPlayer) || MalletRig == null)
            {
                return true;
            }

            return _holderPlayerId == NoMalletHolderPlayerId
                || (_holderPlayerId == localPlayer.playerId
                    && Networking.IsOwner(localPlayer, MalletRig.gameObject));
        }

        private bool _TryAcquireMalletAuthority()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null || !Utilities.IsValid(localPlayer) || MalletRig == null)
            {
                return true;
            }

            if (!GateballMalletRules.CanAcquireMalletAuthority(_holderPlayerId, localPlayer.playerId))
            {
                return false;
            }

            if (!Networking.IsOwner(localPlayer, MalletRig.gameObject))
            {
                Networking.SetOwner(localPlayer, MalletRig.gameObject);
            }

            if (!Networking.IsOwner(localPlayer, MalletRig.gameObject))
            {
                return false;
            }

            if (_holderPlayerId == NoMalletHolderPlayerId)
            {
                _holderPlayerId = localPlayer.playerId;
                RequestSerialization();
            }

            return _holderPlayerId == localPlayer.playerId;
        }

        private void _ReleaseMalletAuthority()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null || !Utilities.IsValid(localPlayer)
                || MalletRig == null
                || _holderPlayerId != localPlayer.playerId
                || !Networking.IsOwner(localPlayer, MalletRig.gameObject))
            {
                return;
            }

            _holderPlayerId = NoMalletHolderPlayerId;
            RequestSerialization();
        }

        private void _ClearLocalGripState()
        {
            _primaryGripIndex = -1;
            _secondaryGripIndex = -1;
            _isLocallyHeld = false;
            _InvalidateAimLock();
            _ResetHistory();
            _SetPreviewVisible(false);
        }

        private void _CacheLocalShaftDirection()
        {
            if (MalletRig == null || GripA == null || GripB == null)
            {
                return;
            }

            Vector3 shaftDirection = MalletRig.InverseTransformDirection(GripB.position - GripA.position);
            if (shaftDirection.sqrMagnitude > 0.0001f)
            {
                _localShaftDirection = shaftDirection.normalized;
            }
        }

        private Vector3 _GetLocalShaftDirection()
        {
            return _localShaftDirection.sqrMagnitude > 0.0001f
                ? _localShaftDirection.normalized
                : Vector3.down;
        }

        private Vector3 _GetLocalFaceForward()
        {
            if (MalletRig == null || StrikeFace == null)
            {
                return Vector3.forward;
            }

            Vector3 localForward = MalletRig.InverseTransformDirection(StrikeFace.forward);
            return localForward.sqrMagnitude > 0.0001f ? localForward.normalized : Vector3.forward;
        }

        private void _RecordHeadPosition(Vector3 position, float time)
        {
            _historyPositions[_historyIndex] = position;
            _historyTimes[_historyIndex] = time;
            _historyIndex = (_historyIndex + 1) % HistorySize;
            if (_historyCount < HistorySize)
            {
                _historyCount++;
            }
        }

        private void _ResetHistory()
        {
            _historyCount = 0;
            _historyIndex = 0;
            _measuredHeadVelocity = Vector3.zero;
            _previousStrikeFacePosition = StrikeFace == null ? Vector3.zero : StrikeFace.position;
        }

        private Vector3 _GetFaceHalfExtents()
        {
            Vector3 scale = HeadCollider.transform.lossyScale;
            return GateballMalletRules.CalculateStrikeFaceHalfExtents(
                HeadCollider.size,
                scale,
                PreviewBoxDepth);
        }

        private Transform _GetGrip(int gripIndex)
        {
            return gripIndex == 0 ? GripA : gripIndex == 1 ? GripB : null;
        }

        private bool _IsValidGripIndex(int gripIndex)
        {
            return gripIndex == 0 || gripIndex == 1;
        }

        private bool _TargetSearchDistanceIsValid()
        {
            return TargetSearchDistance > 0.001f && PreviewBoxDepth > 0.001f;
        }

        private void _SetPreviewVisible(bool visible)
        {
            if (AimPreview != null)
            {
                AimPreview.enabled = visible;
            }
        }

        private void _SetPreviewLine(Vector3 origin, Vector3 direction, Color color)
        {
            if (AimPreview == null)
            {
                return;
            }

            if (AimPreview.positionCount < 2)
            {
                AimPreview.positionCount = 2;
            }

            AimPreview.startColor = color;
            AimPreview.endColor = color;
            AimPreview.SetPosition(0, origin);
            AimPreview.SetPosition(1, origin + direction * PreviewLength);
            AimPreview.enabled = true;
        }
    }
}
