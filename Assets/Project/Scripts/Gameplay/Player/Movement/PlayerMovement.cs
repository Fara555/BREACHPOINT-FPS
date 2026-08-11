using System;
using Breachpoint.Gameplay.Player.Input;
using Breachpoint.Gameplay.Player.Movement.Jump;
using Breachpoint.Gameplay.Player.Movement.Slide;
using Breachpoint.Gameplay.Player.Movement.Stance;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float MovementThreshold = 0.01f;

        [Header("References")]
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private CapsuleCollider _capsuleCollider;

        [SerializeField]
        private Transform _orientation;

        [SerializeField]
        private PlayerGroundDetector _groundDetector;

        [SerializeField]
        private PlayerCurbHandler _curbHandler;

        private IPlayerInput _input;
        private PlayerMovementConfig _config;
        private PlayerJumpController _jumpController;
        private PlayerSlideController _slideController;
        private PlayerStanceController _stanceController;

        private Vector2 _moveInput;

        private bool _isSprintInputHeld;
        private bool _wantsToCrouch;
        private bool _wasGrounded;
        private bool _hasGroundState;

        public event Action Jumped;
        public event Action<float> Landed;
        public event Action<float> CurbResolved;

        public bool IsGrounded { get; private set; }

        public bool IsCrouching =>
            _stanceController != null &&
            _stanceController.IsCrouching;

        public bool IsSliding =>
            _slideController != null &&
            _slideController.IsSliding;

        public bool IsSprintHeld =>
            _isSprintInputHeld;

        public bool ShouldUseCrouchingCameraHeight =>
            _wantsToCrouch ||
            IsSliding ||
            IsCrouching ||
            (_stanceController != null &&
             _stanceController.IsStandingBlocked);

        public PlayerMovementState CurrentState { get; private set; }

        public Vector3 Velocity =>
            _rigidbody != null
                ? _rigidbody.linearVelocity
                : Vector3.zero;

        [Inject]
        public void Construct(
            IPlayerInput input,
            PlayerMovementConfig config,
            PlayerJumpController jumpController,
            PlayerSlideController slideController,
            PlayerStanceController stanceController)
        {
            _input = input;
            _config = config;
            _jumpController = jumpController;
            _slideController = slideController;
            _stanceController = stanceController;
        }

        private void Awake()
        {
            ValidateReferences();
            ConfigureRigidbody();
        }

        private void Start()
        {
            ValidateDependencies();
            Initialize();
        }

        private void Update()
        {
            if (!CanProcessInput())
            {
                return;
            }

            ReadInput();

            _jumpController.Update(
                Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (!CanSimulate())
            {
                return;
            }

            float fixedDeltaTime =
                Time.fixedDeltaTime;

            float downwardTravelSpeed =
                _slideController
                    .CalculateDownwardTravelSpeed(
                        _rigidbody.position.y,
                        fixedDeltaTime);

            GroundInfo groundInfo =
                _groundDetector.DetectGround();

            IsGrounded =
                groundInfo.IsGrounded;

            InitializeGroundState();

            Vector3 currentVelocity =
                _rigidbody.linearVelocity;

            _jumpController.TrackDownwardSpeed(
                IsGrounded,
                currentVelocity);

            _jumpController.UpdateCoyoteTime(
                IsGrounded,
                fixedDeltaTime);

            _slideController.UpdateCooldown(
                fixedDeltaTime);

            Vector3 desiredDirection =
                GetDesiredDirection();

            _slideController.TryResumeAfterLanding(
                currentVelocity,
                IsGrounded,
                _wasGrounded);

            _slideController.TryStart(
                currentVelocity,
                desiredDirection,
                IsGrounded,
                _isSprintInputHeld);

            _slideController.UpdateGroundState(
                groundInfo,
                fixedDeltaTime);

            _stanceController.Update(
                _capsuleCollider,
                transform,
                _wantsToCrouch,
                IsSliding,
                fixedDeltaTime);

            bool canJump =
                IsSliding ||
                (!IsCrouching &&
                 !_wantsToCrouch);

            bool didJump =
                _jumpController.TryConsumeJump(
                    IsGrounded,
                    canJump);

            Vector3 slideJumpVelocity =
                Vector3.zero;

            bool jumpedFromSlide =
                didJump &&
                IsSliding;

            if (jumpedFromSlide)
            {
                slideJumpVelocity =
                    _slideController.PrepareJump(
                        currentVelocity);
            }

            if (didJump)
            {
                IsGrounded = false;

                Jumped?.Invoke();
            }
            else
            {
                NotifyLanding();

                currentVelocity =
                    _jumpController.ApplyJumpCut(
                        currentVelocity,
                        IsGrounded);
            }

            TryResolveCurb(
                didJump,
                groundInfo,
                desiredDirection);

            Vector3 finalVelocity =
                ResolveVelocity(
                    currentVelocity,
                    groundInfo,
                    desiredDirection,
                    downwardTravelSpeed,
                    didJump,
                    jumpedFromSlide,
                    slideJumpVelocity,
                    fixedDeltaTime);

            _rigidbody.linearVelocity =
                finalVelocity;

            UpdateMovementState(
                finalVelocity);

            _wasGrounded =
                IsGrounded;

            _jumpController.EndFixedStep();

            _slideController.EndFixedStep(
                _rigidbody.position.y);
        }

        private void ReadInput()
        {
            _moveInput =
                Vector2.ClampMagnitude(
                    _input.Move,
                    1f);

            _isSprintInputHeld =
                _input.IsSprintHeld;

            _wantsToCrouch =
                _input.IsCrouchHeld;

            _jumpController.ReadInput(
                _input);

            _slideController.ReadInput(
                _input);
        }

        private Vector3 ResolveVelocity(
            Vector3 currentVelocity,
            GroundInfo groundInfo,
            Vector3 desiredDirection,
            float downwardTravelSpeed,
            bool didJump,
            bool jumpedFromSlide,
            Vector3 slideJumpVelocity,
            float fixedDeltaTime)
        {
            if (didJump)
            {
                return jumpedFromSlide
                    ? _jumpController.CalculateJumpVelocity(
                        currentVelocity,
                        slideJumpVelocity)
                    : _jumpController.CalculateJumpVelocity(
                        currentVelocity);
            }

            if (IsSliding &&
                groundInfo.IsGrounded)
            {
                return
                    _slideController
                        .CalculateGroundVelocity(
                            groundInfo,
                            desiredDirection,
                            downwardTravelSpeed,
                            fixedDeltaTime);
            }

            if (IsSliding)
            {
                return
                    _slideController
                        .CalculateAirborneVelocity(
                            currentVelocity,
                            fixedDeltaTime);
            }

            if (groundInfo.IsGrounded)
            {
                return
                    CalculateGroundVelocity(
                        currentVelocity,
                        groundInfo,
                        desiredDirection);
            }

            if (groundInfo.IsOnSteepSlope)
            {
                return
                    CalculateSteepSlopeVelocity(
                        currentVelocity,
                        groundInfo);
            }

            return
                CalculateAirVelocity(
                    currentVelocity,
                    desiredDirection);
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
                _jumpController
                    .CalculateGravityMultiplier(
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

        private void TryResolveCurb(
            bool didJump,
            GroundInfo groundInfo,
            Vector3 desiredDirection)
        {
            if (didJump ||
                IsSliding ||
                !groundInfo.IsGrounded)
            {
                return;
            }

            if (!_curbHandler.TryResolveCurb(
                    desiredDirection,
                    groundInfo,
                    out float curbHeight))
            {
                return;
            }

            CurbResolved?.Invoke(
                curbHeight);
        }

        private void NotifyLanding()
        {
            if (!_jumpController.TryGetLandingSpeed(
                    IsGrounded,
                    _wasGrounded,
                    out float downwardSpeed))
            {
                return;
            }

            Landed?.Invoke(
                downwardSpeed);
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
        }

        private float GetTargetSpeed()
        {
            if (IsCrouching ||
                _wantsToCrouch ||
                (_stanceController != null &&
                 _stanceController.IsStandingBlocked))
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

        private void UpdateMovementState(
            Vector3 velocity)
        {
            if (!IsGrounded)
            {
                CurrentState =
                    PlayerMovementState.Airborne;

                return;
            }

            if (IsSliding)
            {
                CurrentState =
                    PlayerMovementState.Sliding;

                return;
            }

            if (IsCrouching ||
                _wantsToCrouch ||
                (_stanceController != null &&
                 _stanceController.IsStandingBlocked))
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

        private void Initialize()
        {
            _stanceController.Initialize(
                _capsuleCollider);

            _jumpController.Reset();

            _slideController.Initialize(
                _rigidbody.position.y);

            _moveInput = Vector2.zero;

            _isSprintInputHeld = false;
            _wantsToCrouch = false;
            _wasGrounded = false;
            _hasGroundState = false;

            IsGrounded = false;

            CurrentState =
                PlayerMovementState.Idle;
        }

        private static Vector3 GetHorizontalVelocity(
            Vector3 velocity)
        {
            velocity.y = 0f;

            return velocity;
        }

        private bool CanProcessInput()
        {
            return
                _input != null &&
                _jumpController != null &&
                _slideController != null;
        }

        private bool CanSimulate()
        {
            return
                _rigidbody != null &&
                _capsuleCollider != null &&
                _orientation != null &&
                _groundDetector != null &&
                _curbHandler != null &&
                _config != null &&
                _jumpController != null &&
                _slideController != null &&
                _stanceController != null;
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
        }

        private void ValidateDependencies()
        {
            if (_input == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires IPlayerInput.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires PlayerMovementConfig.",
                    this);
            }

            if (_jumpController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires PlayerJumpController.",
                    this);
            }

            if (_slideController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires PlayerSlideController.",
                    this);
            }

            if (_stanceController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires PlayerStanceController.",
                    this);
            }
        }
    }
}