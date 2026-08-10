using System;
using Breachpoint.Gameplay.Player.Input;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float StanceThreshold = 0.01f;
        private const float MovementThreshold = 0.01f;
        private const float DownhillThreshold = 0.01f;
        private const int ClearanceHitCapacity = 8;

        [Header("References")]
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private CapsuleCollider _capsuleCollider;

        [SerializeField]
        private Transform _orientation;

        [SerializeField]
        private Transform _cameraRoot;

        [SerializeField]
        private PlayerGroundDetector _groundDetector;

        [SerializeField]
        private PlayerCurbHandler _curbHandler;

        [SerializeField]
        private PlayerMovementConfig _config;

        private readonly RaycastHit[] _clearanceHits =
            new RaycastHit[ClearanceHitCapacity];

        private IPlayerInput _input;

        private Vector2 _moveInput;

        private bool _isSprintInputHeld;
        private bool _isJumpHeld;
        private bool _wasJumpReleased;
        private bool _wasCrouchPressed;
        private bool _wantsToCrouch;
        private bool _isStandingBlocked;
        private bool _wasGrounded;
        private bool _hasGroundState;
        private bool _isSliding;
        private bool _resumeSlideAfterLanding;
        private bool _jumpedFromSlide;

        private Vector3 _slideDirection;
        private Vector3 _slideJumpHorizontalVelocity;

        private float _standingBottomLocalY;
        private float _coyoteTimeRemaining;
        private float _jumpBufferTimeRemaining;
        private float _curbCameraOffset;
        private float _curbCameraOffsetVelocity;
        private float _maximumDownwardSpeed;

        private float _slideSpeed;
        private float _slideTimeRemaining;
        private float _slideCooldownRemaining;
        private float _slideMomentumRetentionRemaining;
        private float _slideGroundGraceRemaining;

        private float _previousFixedPositionY;

        public event Action Jumped;
        public event Action<float> Landed;

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }

        public bool IsSprintHeld =>
            _isSprintInputHeld;

        public bool IsSliding =>
            _isSliding;

        public PlayerMovementState CurrentState { get; private set; }

        public Vector3 Velocity =>
            _rigidbody != null
                ? _rigidbody.linearVelocity
                : Vector3.zero;

        [Inject]
        public void Construct(
            IPlayerInput input)
        {
            _input = input;
        }

        private void Awake()
        {
            ValidateReferences();
            ConfigureRigidbody();
            InitializeStance();

            CurrentState =
                PlayerMovementState.Idle;
        }

        private void Update()
        {
            ReadInput();
            UpdateJumpBuffer();
            UpdateCameraHeight();
        }

        private void FixedUpdate()
        {
            if (!CanSimulate())
            {
                return;
            }

            float downwardTravelSpeed =
                CalculateDownwardTravelSpeed();

            GroundInfo groundInfo =
                _groundDetector.DetectGround();

            IsGrounded =
                groundInfo.IsGrounded;

            InitializeGroundState();
            TrackDownwardSpeed();
            UpdateCoyoteTime();
            UpdateSlideCooldown();

            Vector3 currentVelocity =
                _rigidbody.linearVelocity;

            Vector3 desiredDirection =
                GetDesiredDirection();

            TryResumeSlideAfterLanding(
                currentVelocity,
                groundInfo);

            TryStartSlide(
                currentVelocity,
                desiredDirection);

            UpdateSlideGroundState(
                groundInfo);

            UpdateStance();

            bool didJump =
                TryConsumeJump();

            if (didJump)
            {
                Jumped?.Invoke();
            }
            else
            {
                TryNotifyLanding();

                currentVelocity =
                    ApplyJumpCut(
                        currentVelocity);
            }

            if (!didJump &&
                !_isSliding &&
                groundInfo.IsGrounded &&
                _curbHandler.TryResolveCurb(
                    desiredDirection,
                    groundInfo,
                    out float curbHeight))
            {
                _curbCameraOffset +=
                    curbHeight;
            }

            Vector3 finalVelocity;

            if (didJump)
            {
                finalVelocity =
                    CalculateJumpVelocity(
                        currentVelocity);
            }
            else if (_isSliding &&
                     groundInfo.IsGrounded)
            {
                finalVelocity =
                    CalculateSlideVelocity(
                        groundInfo,
                        desiredDirection,
                        downwardTravelSpeed);
            }
            else if (_isSliding)
            {
                finalVelocity =
                    CalculateAirborneSlideVelocity(
                        currentVelocity);
            }
            else if (groundInfo.IsGrounded)
            {
                finalVelocity =
                    CalculateGroundVelocity(
                        currentVelocity,
                        groundInfo,
                        desiredDirection);
            }
            else if (groundInfo.IsOnSteepSlope)
            {
                finalVelocity =
                    CalculateSteepSlopeVelocity(
                        currentVelocity,
                        groundInfo);
            }
            else
            {
                finalVelocity =
                    CalculateAirVelocity(
                        currentVelocity,
                        desiredDirection);
            }

            _rigidbody.linearVelocity =
                finalVelocity;

            UpdateMovementState(
                finalVelocity);

            _wasGrounded =
                IsGrounded;

            _previousFixedPositionY =
                _rigidbody.position.y;

            _wasJumpReleased = false;
            _wasCrouchPressed = false;
            _jumpedFromSlide = false;
        }

        private void ReadInput()
        {
            if (_input == null)
            {
                return;
            }

            _moveInput =
                Vector2.ClampMagnitude(
                    _input.Move,
                    1f);

            _isSprintInputHeld =
                _input.IsSprintHeld;

            _wantsToCrouch =
                _input.IsCrouchHeld;

            _wasCrouchPressed |=
                _input.WasCrouchPressed;

            _isJumpHeld =
                _input.IsJumpHeld;

            _wasJumpReleased |=
                _input.WasJumpReleased;

            if (!_wantsToCrouch)
            {
                _resumeSlideAfterLanding =
                    false;
            }

            if (_input.WasJumpPressed)
            {
                _jumpBufferTimeRemaining =
                    _config.JumpBufferTime;
            }
        }

        private float CalculateDownwardTravelSpeed()
        {
            float downwardDistance =
                _previousFixedPositionY -
                _rigidbody.position.y;

            return Mathf.Max(
                0f,
                downwardDistance /
                Time.fixedDeltaTime);
        }

        private void UpdateJumpBuffer()
        {
            if (_jumpBufferTimeRemaining <= 0f)
            {
                return;
            }

            _jumpBufferTimeRemaining =
                Mathf.Max(
                    0f,
                    _jumpBufferTimeRemaining -
                    Time.deltaTime);
        }

        private void UpdateCoyoteTime()
        {
            if (IsGrounded)
            {
                _coyoteTimeRemaining =
                    _config.CoyoteTime;

                return;
            }

            _coyoteTimeRemaining =
                Mathf.Max(
                    0f,
                    _coyoteTimeRemaining -
                    Time.fixedDeltaTime);
        }

        private bool TryConsumeJump()
        {
            bool hasBufferedJump =
                _jumpBufferTimeRemaining > 0f;

            bool canUseGroundJump =
                IsGrounded ||
                _coyoteTimeRemaining > 0f;

            if (!hasBufferedJump ||
                !canUseGroundJump)
            {
                return false;
            }

            if (!_isSliding &&
                (IsCrouching ||
                 _wantsToCrouch))
            {
                return false;
            }

            if (_isSliding)
            {
                PrepareSlideJump();
            }

            _jumpBufferTimeRemaining = 0f;
            _coyoteTimeRemaining = 0f;
            _maximumDownwardSpeed = 0f;
            _wasJumpReleased = false;

            IsGrounded = false;

            return true;
        }

        private void PrepareSlideJump()
        {
            _jumpedFromSlide = true;

            Vector3 currentHorizontalVelocity =
                GetHorizontalVelocity(
                    _rigidbody.linearVelocity);

            if (currentHorizontalVelocity.sqrMagnitude >
                MovementThreshold)
            {
                _slideJumpHorizontalVelocity =
                    currentHorizontalVelocity;
            }
            else
            {
                Vector3 horizontalSlideDirection =
                    GetHorizontalVelocity(
                        _slideDirection);

                if (horizontalSlideDirection.sqrMagnitude >
                    MovementThreshold)
                {
                    horizontalSlideDirection.Normalize();
                }

                _slideJumpHorizontalVelocity =
                    horizontalSlideDirection *
                    _slideSpeed;
            }

            _resumeSlideAfterLanding =
                _wantsToCrouch;

            StopSlide(
                false);
        }

        private Vector3 ApplyJumpCut(
            Vector3 currentVelocity)
        {
            if (IsGrounded ||
                !_wasJumpReleased ||
                currentVelocity.y <= 0f)
            {
                return currentVelocity;
            }

            currentVelocity.y *=
                _config.JumpCutVelocityMultiplier;

            return currentVelocity;
        }

        private Vector3 CalculateJumpVelocity(
            Vector3 currentVelocity)
        {
            Vector3 horizontalVelocity =
                _jumpedFromSlide
                    ? _slideJumpHorizontalVelocity
                    : GetHorizontalVelocity(
                        currentVelocity);

            return
                horizontalVelocity +
                Vector3.up *
                CalculateJumpSpeed();
        }

        private Vector3 CalculateAirVelocity(
            Vector3 currentVelocity,
            Vector3 desiredDirection)
        {
            Vector3 currentHorizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            Vector3 desiredVelocity =
                desiredDirection *
                GetTargetSpeed();

            Vector3 controlledVelocity =
                Vector3.Lerp(
                    currentHorizontalVelocity,
                    desiredVelocity,
                    _config.AirControl);

            Vector3 horizontalVelocity =
                Vector3.MoveTowards(
                    currentHorizontalVelocity,
                    controlledVelocity,
                    _config.AirAcceleration *
                    Time.fixedDeltaTime);

            float gravityMultiplier =
                GetGravityMultiplier(
                    currentVelocity.y);

            float verticalVelocity =
                Mathf.Max(
                    currentVelocity.y -
                    _config.Gravity *
                    gravityMultiplier *
                    Time.fixedDeltaTime,
                    -_config.MaxFallSpeed);

            return
                horizontalVelocity +
                Vector3.up *
                verticalVelocity;
        }

        private Vector3 CalculateAirborneSlideVelocity(
            Vector3 currentVelocity)
        {
            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            float verticalVelocity =
                Mathf.Max(
                    currentVelocity.y -
                    _config.Gravity *
                    _config.FallGravityMultiplier *
                    Time.fixedDeltaTime,
                    -_config.MaxFallSpeed);

            return
                horizontalVelocity +
                Vector3.up *
                verticalVelocity;
        }

        private float GetGravityMultiplier(
            float verticalVelocity)
        {
            if (verticalVelocity < 0f)
            {
                return
                    _config.FallGravityMultiplier;
            }

            if (_isJumpHeld &&
                Mathf.Abs(verticalVelocity) <=
                _config.ApexVelocityThreshold)
            {
                return
                    _config.ApexGravityMultiplier;
            }

            return 1f;
        }

        private void InitializeGroundState()
        {
            if (_hasGroundState)
            {
                return;
            }

            _wasGrounded =
                IsGrounded;

            _hasGroundState = true;
            _maximumDownwardSpeed = 0f;
        }

        private void TrackDownwardSpeed()
        {
            if (IsGrounded)
            {
                return;
            }

            float downwardSpeed =
                Mathf.Max(
                    0f,
                    -_rigidbody.linearVelocity.y);

            _maximumDownwardSpeed =
                Mathf.Max(
                    _maximumDownwardSpeed,
                    downwardSpeed);
        }

        private void TryNotifyLanding()
        {
            if (!_hasGroundState ||
                !IsGrounded ||
                _wasGrounded)
            {
                return;
            }

            Landed?.Invoke(
                _maximumDownwardSpeed);

            _maximumDownwardSpeed = 0f;
        }

        private void TryStartSlide(
            Vector3 currentVelocity,
            Vector3 desiredDirection)
        {
            if (!_wasCrouchPressed ||
                _isSliding ||
                !IsGrounded ||
                _slideCooldownRemaining > 0f ||
                !_isSprintInputHeld)
            {
                return;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            if (horizontalSpeed <
                _config.MinimumSlideStartSpeed)
            {
                return;
            }

            Vector3 startDirection =
                horizontalSpeed >
                MovementThreshold
                    ? horizontalVelocity.normalized
                    : desiredDirection;

            if (startDirection.sqrMagnitude <=
                MovementThreshold)
            {
                return;
            }

            StartSlide(
                startDirection,
                Mathf.Max(
                    horizontalSpeed,
                    _config.InitialSlideSpeed));
        }

        private void TryResumeSlideAfterLanding(
            Vector3 currentVelocity,
            GroundInfo groundInfo)
        {
            if (!_resumeSlideAfterLanding ||
                _isSliding ||
                !_wantsToCrouch ||
                !groundInfo.IsGrounded ||
                _wasGrounded)
            {
                return;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            if (horizontalSpeed <
                _config.MinimumSlideResumeSpeed)
            {
                _resumeSlideAfterLanding =
                    false;

                return;
            }

            StartSlide(
                horizontalVelocity.normalized,
                horizontalSpeed);

            _resumeSlideAfterLanding = false;
            _slideCooldownRemaining = 0f;
        }

        private void StartSlide(
            Vector3 direction,
            float speed)
        {
            _isSliding = true;

            _slideDirection =
                direction.normalized;

            _slideSpeed =
                Mathf.Clamp(
                    speed,
                    0f,
                    _config.MaximumSlideSpeed);

            _slideTimeRemaining =
                _config.MaximumSlideDuration;

            _slideGroundGraceRemaining =
                _config.SlideGroundGraceTime;
        }

        private void UpdateSlideGroundState(
            GroundInfo groundInfo)
        {
            if (!_isSliding)
            {
                return;
            }

            if (groundInfo.IsGrounded)
            {
                _slideGroundGraceRemaining =
                    _config.SlideGroundGraceTime;

                return;
            }

            _slideGroundGraceRemaining =
                Mathf.Max(
                    0f,
                    _slideGroundGraceRemaining -
                    Time.fixedDeltaTime);

            if (_slideGroundGraceRemaining <= 0f)
            {
                StopSlide();
            }
        }

        private void UpdateSlideCooldown()
        {
            if (_slideCooldownRemaining <= 0f)
            {
                return;
            }

            _slideCooldownRemaining =
                Mathf.Max(
                    0f,
                    _slideCooldownRemaining -
                    Time.fixedDeltaTime);
        }

        private Vector3 CalculateSlideVelocity(
            GroundInfo groundInfo,
            Vector3 desiredDirection,
            float downwardTravelSpeed)
        {
            Vector3 surfaceDirection =
                Vector3.ProjectOnPlane(
                    _slideDirection,
                    groundInfo.Normal);

            if (surfaceDirection.sqrMagnitude <=
                MovementThreshold)
            {
                surfaceDirection =
                    GetHorizontalVelocity(
                        _slideDirection);
            }

            if (surfaceDirection.sqrMagnitude >
                MovementThreshold)
            {
                surfaceDirection.Normalize();
            }

            Vector3 desiredSurfaceDirection =
                Vector3.ProjectOnPlane(
                    desiredDirection,
                    groundInfo.Normal);

            if (desiredSurfaceDirection.sqrMagnitude >
                MovementThreshold)
            {
                desiredSurfaceDirection.Normalize();

                float maximumRadians =
                    _config.SlideSteeringSpeed *
                    Mathf.Deg2Rad *
                    Time.fixedDeltaTime;

                surfaceDirection =
                    Vector3.RotateTowards(
                            surfaceDirection,
                            desiredSurfaceDirection,
                            maximumRadians,
                            0f)
                        .normalized;
            }

            _slideDirection =
                surfaceDirection;

            float downhillFactor =
                CalculateDownhillFactor(
                    groundInfo,
                    surfaceDirection);

            float stairFactor =
                Mathf.InverseLerp(
                    _config.MinimumStairDescentSpeed,
                    _config.MaximumStairDescentSpeed,
                    downwardTravelSpeed);

            float momentumGainFactor =
                Mathf.Max(
                    downhillFactor,
                    stairFactor);

            bool isGainingMomentum =
                momentumGainFactor >
                DownhillThreshold;

            if (isGainingMomentum)
            {
                float slopeAcceleration =
                    downhillFactor *
                    _config.SlideDownhillAcceleration;

                float stairAcceleration =
                    stairFactor *
                    _config.SlideStairAcceleration;

                float acceleration =
                    Mathf.Max(
                        slopeAcceleration,
                        stairAcceleration);

                _slideSpeed +=
                    acceleration *
                    Time.fixedDeltaTime;

                _slideMomentumRetentionRemaining =
                    _config.SlideMomentumRetentionTime;

                _slideTimeRemaining =
                    _config.MaximumSlideDuration;
            }
            else
            {
                _slideMomentumRetentionRemaining =
                    Mathf.Max(
                        0f,
                        _slideMomentumRetentionRemaining -
                        Time.fixedDeltaTime);

                _slideTimeRemaining =
                    Mathf.Max(
                        0f,
                        _slideTimeRemaining -
                        Time.fixedDeltaTime);
            }

            float decelerationMultiplier =
                _slideMomentumRetentionRemaining > 0f
                    ? _config.RetainedMomentumDecelerationMultiplier
                    : 1f;

            float deceleration =
                _config.SlideDeceleration *
                decelerationMultiplier;

            _slideSpeed =
                Mathf.MoveTowards(
                    _slideSpeed,
                    0f,
                    deceleration *
                    Time.fixedDeltaTime);

            _slideSpeed =
                Mathf.Clamp(
                    _slideSpeed,
                    0f,
                    _config.MaximumSlideSpeed);

            float resultSpeed =
                _slideSpeed;

            Vector3 groundAdhesionVelocity =
                -groundInfo.Normal *
                _config.GroundedVerticalSpeed;

            Vector3 resultVelocity =
                surfaceDirection *
                resultSpeed +
                groundAdhesionVelocity;

            if (_slideSpeed <=
                    _config.SlideEndSpeed ||
                _slideTimeRemaining <= 0f)
            {
                StopSlide();
            }

            return resultVelocity;
        }

        private static float CalculateDownhillFactor(
            GroundInfo groundInfo,
            Vector3 slideDirection)
        {
            if (groundInfo.SurfaceAngle <=
                Mathf.Epsilon)
            {
                return 0f;
            }

            Vector3 downhillDirection =
                Vector3.ProjectOnPlane(
                    Vector3.down,
                    groundInfo.Normal);

            if (downhillDirection.sqrMagnitude <=
                MovementThreshold)
            {
                return 0f;
            }

            downhillDirection.Normalize();

            float downhillAlignment =
                Mathf.Max(
                    0f,
                    Vector3.Dot(
                        slideDirection,
                        downhillDirection));

            float slopeSteepness =
                Mathf.Sin(
                    groundInfo.SurfaceAngle *
                    Mathf.Deg2Rad);

            return
                downhillAlignment *
                slopeSteepness;
        }

        private void StopSlide(
            bool startCooldown = true)
        {
            if (!_isSliding)
            {
                return;
            }

            _isSliding = false;
            _slideDirection = Vector3.zero;
            _slideSpeed = 0f;
            _slideTimeRemaining = 0f;
            _slideGroundGraceRemaining = 0f;

            if (startCooldown)
            {
                _slideCooldownRemaining =
                    _config.SlideCooldown;
            }
        }

        private Vector3 CalculateGroundVelocity(
            Vector3 currentVelocity,
            GroundInfo groundInfo,
            Vector3 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude >
                MovementThreshold)
            {
                desiredDirection =
                    Vector3.ProjectOnPlane(
                            desiredDirection,
                            groundInfo.Normal)
                        .normalized;
            }

            Vector3 currentSurfaceVelocity =
                Vector3.ProjectOnPlane(
                    currentVelocity,
                    groundInfo.Normal);

            Vector3 targetVelocity =
                desiredDirection *
                GetTargetSpeed();

            float acceleration =
                desiredDirection.sqrMagnitude >
                MovementThreshold
                    ? _config.GroundAcceleration
                    : _config.GroundDeceleration;

            Vector3 surfaceVelocity =
                Vector3.MoveTowards(
                    currentSurfaceVelocity,
                    targetVelocity,
                    acceleration *
                    Time.fixedDeltaTime);

            Vector3 groundAdhesionVelocity =
                -groundInfo.Normal *
                _config.GroundedVerticalSpeed;

            return
                surfaceVelocity +
                groundAdhesionVelocity;
        }

        private Vector3 CalculateSteepSlopeVelocity(
            Vector3 currentVelocity,
            GroundInfo groundInfo)
        {
            Vector3 slideDirection =
                Vector3.ProjectOnPlane(
                        Vector3.down,
                        groundInfo.Normal)
                    .normalized;

            Vector3 currentSurfaceVelocity =
                Vector3.ProjectOnPlane(
                    currentVelocity,
                    groundInfo.Normal);

            Vector3 targetSlideVelocity =
                slideDirection *
                _config.MaximumSteepSlopeSlideSpeed;

            Vector3 slideVelocity =
                Vector3.MoveTowards(
                    currentSurfaceVelocity,
                    targetSlideVelocity,
                    _config.SteepSlopeSlideAcceleration *
                    Time.fixedDeltaTime);

            Vector3 slopeAdhesionVelocity =
                -groundInfo.Normal *
                _config.GroundedVerticalSpeed;

            return
                slideVelocity +
                slopeAdhesionVelocity;
        }

        private void UpdateStance()
        {
            _isStandingBlocked =
                !_wantsToCrouch &&
                !_isSliding &&
                !CanStandUp();

            bool shouldCrouch =
                _wantsToCrouch ||
                _isSliding ||
                _isStandingBlocked;

            float targetHeight =
                shouldCrouch
                    ? _config.CrouchingHeight
                    : _config.StandingHeight;

            float newHeight =
                Mathf.MoveTowards(
                    _capsuleCollider.height,
                    targetHeight,
                    _config.StanceTransitionSpeed *
                    Time.fixedDeltaTime);

            ApplyColliderHeight(
                newHeight);

            IsCrouching =
                shouldCrouch ||
                Mathf.Abs(
                    _capsuleCollider.height -
                    _config.StandingHeight) >
                StanceThreshold;
        }

        private void ApplyColliderHeight(
            float height)
        {
            float minimumHeight =
                _capsuleCollider.radius * 2f;

            float validHeight =
                Mathf.Max(
                    height,
                    minimumHeight);

            _capsuleCollider.height =
                validHeight;

            Vector3 center =
                _capsuleCollider.center;

            center.y =
                _standingBottomLocalY +
                validHeight * 0.5f;

            _capsuleCollider.center =
                center;
        }

        private void UpdateCameraHeight()
        {
            if (_cameraRoot == null ||
                _config == null)
            {
                return;
            }

            _curbCameraOffset =
                Mathf.SmoothDamp(
                    _curbCameraOffset,
                    0f,
                    ref _curbCameraOffsetVelocity,
                    _config.CurbCameraSmoothTime);

            bool useCrouchingHeight =
                _wantsToCrouch ||
                _isSliding ||
                IsCrouching ||
                _isStandingBlocked;

            float stanceCameraY =
                useCrouchingHeight
                    ? _config.CrouchingCameraLocalY
                    : _config.StandingCameraLocalY;

            float targetCameraY =
                stanceCameraY -
                _curbCameraOffset;

            Vector3 localPosition =
                _cameraRoot.localPosition;

            localPosition.y =
                Mathf.MoveTowards(
                    localPosition.y,
                    targetCameraY,
                    _config.CameraTransitionSpeed *
                    Time.deltaTime);

            _cameraRoot.localPosition =
                localPosition;
        }

        private bool CanStandUp()
        {
            if (_capsuleCollider == null ||
                _config == null)
            {
                return false;
            }

            float currentHeight =
                _capsuleCollider.height;

            float standingHeight =
                _config.StandingHeight;

            if (currentHeight >=
                standingHeight -
                StanceThreshold)
            {
                return true;
            }

            float radius =
                GetWorldCapsuleRadius();

            radius =
                Mathf.Max(
                    0.01f,
                    radius -
                    _config.ClearancePadding);

            Vector3 currentTopSphereCenter =
                GetTopSphereCenter(
                    currentHeight,
                    _capsuleCollider.center.y,
                    radius);

            float standingCenterY =
                _standingBottomLocalY +
                standingHeight * 0.5f;

            Vector3 standingTopSphereCenter =
                GetTopSphereCenter(
                    standingHeight,
                    standingCenterY,
                    radius);

            Vector3 castOffset =
                standingTopSphereCenter -
                currentTopSphereCenter;

            float castDistance =
                castOffset.magnitude;

            if (castDistance <=
                Mathf.Epsilon)
            {
                return true;
            }

            Vector3 castDirection =
                castOffset /
                castDistance;

            int hitCount =
                Physics.SphereCastNonAlloc(
                    currentTopSphereCenter,
                    radius,
                    castDirection,
                    _clearanceHits,
                    castDistance,
                    _config.ClearanceMask,
                    QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider =
                    _clearanceHits[i].collider;

                if (hitCollider == null ||
                    hitCollider ==
                    _capsuleCollider ||
                    hitCollider.transform
                        .IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private Vector3 GetTopSphereCenter(
            float localHeight,
            float localCenterY,
            float worldRadius)
        {
            Vector3 localCenter =
                _capsuleCollider.center;

            localCenter.y =
                localCenterY;

            Vector3 worldCenter =
                transform.TransformPoint(
                    localCenter);

            float verticalScale =
                Mathf.Abs(
                    transform.lossyScale.y);

            float worldHeight =
                localHeight *
                verticalScale;

            float topOffset =
                Mathf.Max(
                    0f,
                    worldHeight * 0.5f -
                    worldRadius);

            return
                worldCenter +
                transform.up *
                topOffset;
        }

        private float GetWorldCapsuleRadius()
        {
            Vector3 scale =
                transform.lossyScale;

            float horizontalScale =
                Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.z));

            return
                _capsuleCollider.radius *
                horizontalScale;
        }

        private float GetTargetSpeed()
        {
            if (IsCrouching ||
                _wantsToCrouch ||
                _isStandingBlocked)
            {
                return
                    _config.CrouchSpeed;
            }

            return
                _isSprintInputHeld
                    ? _config.SprintSpeed
                    : _config.WalkSpeed;
        }

        private Vector3 GetDesiredDirection()
        {
            if (_orientation == null)
            {
                return Vector3.zero;
            }

            Vector3 forward =
                _orientation.forward;

            Vector3 right =
                _orientation.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 direction =
                forward * _moveInput.y +
                right * _moveInput.x;

            return
                Vector3.ClampMagnitude(
                    direction,
                    1f);
        }

        private static Vector3 GetHorizontalVelocity(
            Vector3 velocity)
        {
            return new Vector3(
                velocity.x,
                0f,
                velocity.z);
        }

        private float CalculateJumpSpeed()
        {
            return
                Mathf.Sqrt(
                    2f *
                    _config.Gravity *
                    _config.JumpHeight);
        }

        private void UpdateMovementState(
            Vector3 velocity)
        {
            if (!IsGrounded)
            {
                CurrentState =
                    PlayerMovementState.Airborne;

                return;
            }

            if (_isSliding)
            {
                CurrentState =
                    PlayerMovementState.Sliding;

                return;
            }

            if (IsCrouching ||
                _wantsToCrouch ||
                _isStandingBlocked)
            {
                CurrentState =
                    PlayerMovementState.Crouching;

                return;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    velocity);

            if (horizontalVelocity.sqrMagnitude <=
                MovementThreshold)
            {
                CurrentState =
                    PlayerMovementState.Idle;

                return;
            }

            CurrentState =
                _isSprintInputHeld
                    ? PlayerMovementState.Sprinting
                    : PlayerMovementState.Walking;
        }

        private void InitializeStance()
        {
            if (_capsuleCollider == null ||
                _cameraRoot == null ||
                _config == null)
            {
                return;
            }

            _standingBottomLocalY =
                _config.StandingCenterY -
                _config.StandingHeight *
                0.5f;

            ApplyColliderHeight(
                _config.StandingHeight);

            Vector3 cameraPosition =
                _cameraRoot.localPosition;

            cameraPosition.y =
                _config.StandingCameraLocalY;

            _cameraRoot.localPosition =
                cameraPosition;

            IsCrouching = false;

            _isSprintInputHeld = false;
            _isJumpHeld = false;
            _wasJumpReleased = false;
            _wasCrouchPressed = false;
            _wantsToCrouch = false;
            _isStandingBlocked = false;
            _isSliding = false;
            _resumeSlideAfterLanding = false;
            _jumpedFromSlide = false;

            _slideDirection = Vector3.zero;
            _slideJumpHorizontalVelocity = Vector3.zero;

            _curbCameraOffset = 0f;
            _curbCameraOffsetVelocity = 0f;
            _maximumDownwardSpeed = 0f;

            _slideSpeed = 0f;
            _slideTimeRemaining = 0f;
            _slideCooldownRemaining = 0f;
            _slideMomentumRetentionRemaining = 0f;
            _slideGroundGraceRemaining = 0f;

            _previousFixedPositionY =
                _rigidbody != null
                    ? _rigidbody.position.y
                    : transform.position.y;

            _hasGroundState = false;
        }

        private bool CanSimulate()
        {
            return
                _rigidbody != null &&
                _capsuleCollider != null &&
                _orientation != null &&
                _cameraRoot != null &&
                _groundDetector != null &&
                _curbHandler != null &&
                _config != null;
        }

        private void ConfigureRigidbody()
        {
            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.useGravity = false;

            _rigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;

            _rigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            _rigidbody.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;
        }

        private void ValidateReferences()
        {
            if (_rigidbody == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a Rigidbody reference.",
                    this);
            }

            if (_capsuleCollider == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a CapsuleCollider reference.",
                    this);
            }

            if (_orientation == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires an Orientation reference.",
                    this);
            }

            if (_cameraRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a CameraRoot reference.",
                    this);
            }

            if (_groundDetector == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a PlayerGroundDetector reference.",
                    this);
            }

            if (_curbHandler == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a PlayerCurbHandler reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a PlayerMovementConfig reference.",
                    this);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_capsuleCollider == null ||
                _config == null)
            {
                return;
            }

            float radius =
                GetWorldCapsuleRadius();

            radius =
                Mathf.Max(
                    0.01f,
                    radius -
                    _config.ClearancePadding);

            Vector3 currentTop =
                GetTopSphereCenter(
                    _capsuleCollider.height,
                    _capsuleCollider.center.y,
                    radius);

            float standingBottom =
                _config.StandingCenterY -
                _config.StandingHeight *
                0.5f;

            float standingCenterY =
                standingBottom +
                _config.StandingHeight *
                0.5f;

            Vector3 standingTop =
                GetTopSphereCenter(
                    _config.StandingHeight,
                    standingCenterY,
                    radius);

            Gizmos.DrawWireSphere(
                currentTop,
                radius);

            Gizmos.DrawWireSphere(
                standingTop,
                radius);

            Gizmos.DrawLine(
                currentTop,
                standingTop);
        }
#endif
    }
}