using Breachpoint.Gameplay.Player.Input;
using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class WeaponMotion : MonoBehaviour
    {
        private const float FullCycle = Mathf.PI * 2f;
        private const float MovementInputThreshold = 0.01f;

        [Header("References")]
        [SerializeField]
        private Transform _motionRoot;

        [SerializeField]
        private Transform _motionSpace;

        [SerializeField]
        private Transform _sprintRotationPivot;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private WeaponMotionConfig _config;

        private IPlayerInput _input;
        private IWeaponAimState _aimState;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private Vector3 _sprintPivotLocalPosition;
        private Vector3 _currentSwayPosition;
        private Vector3 _currentSwayRotation;
        private Vector3 _currentBobPosition;
        private Vector3 _currentBobRotation;
        private Vector3 _trackedLocalVelocity;
        private Vector3 _currentInertiaPosition;
        private Vector3 _currentInertiaRotation;
        private Vector3 _impulsePosition;
        private Vector3 _impulsePositionVelocity;
        private Vector3 _impulseRotation;
        private Vector3 _impulseRotationVelocity;
        private float _sprintPoseWeight;
        private float _sprintPoseMovementGraceTimer;
        private float _breathingPhase;
        private float _breathingWeight;
        private float _bobPhase;
        private float _bobWeight;
        private float _aimWeight;
        private bool _hasTrackedVelocity;
        private bool _isSprintPoseActive;

        [Inject]
        public void Construct(
            IPlayerInput input,
            IWeaponAimState aimState)
        {
            _input = input;
            _aimState = aimState;
        }

        private void Awake()
        {
            ValidateReferences();

            if (_motionRoot == null)
            {
                return;
            }

            _baseLocalPosition = _motionRoot.localPosition;
            _baseLocalRotation = _motionRoot.localRotation;

            if (_sprintRotationPivot != null)
            {
                _sprintPivotLocalPosition =
                    _motionRoot.InverseTransformPoint(
                        _sprintRotationPivot.position);
            }
        }

        private void OnEnable()
        {
            if (_movement == null)
            {
                return;
            }

            _movement.Jumped += HandleJumped;
            _movement.Landed += HandleLanded;
        }

        private void LateUpdate()
        {
            if (!CanUpdate())
            {
                return;
            }

            UpdateAimWeight();
            UpdateSway();
            UpdateBob(
                out Vector3 bobPosition,
                out Vector3 bobRotation);
            UpdateInertia(
                out Vector3 inertiaPosition,
                out Vector3 inertiaRotation);
            UpdateImpulseMotion();
            UpdateSprintPose(
                out Vector3 sprintPosition,
                out Vector3 sprintRotation);
            UpdateBreathing(
                out Vector3 breathingPosition,
                out Vector3 breathingRotation);

            _motionRoot.localPosition =
                _baseLocalPosition +
                _currentSwayPosition +
                bobPosition +
                inertiaPosition +
                _impulsePosition +
                sprintPosition +
                breathingPosition;

            _motionRoot.localRotation =
                _baseLocalRotation *
                Quaternion.Euler(
                    _currentSwayRotation +
                    bobRotation +
                    inertiaRotation +
                    _impulseRotation +
                    sprintRotation +
                    breathingRotation);
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.Jumped -= HandleJumped;
                _movement.Landed -= HandleLanded;
            }

            ResetMotion();
        }

        private void UpdateAimWeight()
        {
            float targetWeight =
                _aimState.IsAiming
                    ? 1f
                    : 0f;

            float interpolation = GetExponentialInterpolation(
                _config.AimBlendSpeed);

            _aimWeight = Mathf.Lerp(
                _aimWeight,
                targetWeight,
                interpolation);
        }

        private void UpdateSway()
        {
            Vector2 lookDelta = Vector2.ClampMagnitude(
                _input.Look,
                _config.MaximumLookDelta);

            float aimMultiplier = Mathf.Lerp(
                1f,
                _config.AimSwayMultiplier,
                _aimWeight);

            Vector3 targetPosition = new(
                -lookDelta.x * _config.SwayPosition.x,
                -lookDelta.y * _config.SwayPosition.y,
                0f);

            Vector3 targetRotation = new(
                lookDelta.y * _config.SwayRotation.x,
                -lookDelta.x * _config.SwayRotation.y,
                lookDelta.x * _config.SwayRotation.z);

            targetPosition *= aimMultiplier;
            targetRotation *= aimMultiplier;

            float interpolation = GetExponentialInterpolation(
                _config.SwaySnappiness);

            _currentSwayPosition = Vector3.Lerp(
                _currentSwayPosition,
                targetPosition,
                interpolation);

            _currentSwayRotation = Vector3.Lerp(
                _currentSwayRotation,
                targetRotation,
                interpolation);
        }

        private void UpdateBob(
            out Vector3 position,
            out Vector3 rotation)
        {
            Vector3 horizontalVelocity = _movement.Velocity;
            horizontalVelocity.y = 0f;

            float speed = horizontalVelocity.magnitude;
            bool shouldBob =
                _movement.IsGrounded &&
                !_movement.IsSliding &&
                speed >= _config.MinimumBobSpeed;

            float blendSpeed = shouldBob
                ? _config.BobBlendInSpeed
                : _config.BobBlendOutSpeed;

            _bobWeight = Mathf.MoveTowards(
                _bobWeight,
                shouldBob ? 1f : 0f,
                blendSpeed * Time.deltaTime);

            WeaponMotionProfile profile =
                _config.GetProfile(
                    _movement.CurrentState);

            if (shouldBob)
            {
                _bobPhase +=
                    speed *
                    profile.CyclesPerMeter *
                    FullCycle *
                    Time.deltaTime;

                _bobPhase %= FullCycle;
            }

            float horizontalWave = Mathf.Sin(_bobPhase);
            float verticalWave =
                -0.5f +
                Mathf.Cos(_bobPhase * 2f) * 0.5f;

            Vector3 wave = new(
                horizontalWave,
                verticalWave,
                Mathf.Cos(_bobPhase));

            float aimMultiplier = Mathf.Lerp(
                1f,
                _config.AimBobMultiplier,
                _aimWeight);

            float weight = _bobWeight * aimMultiplier;

            float speedWeight = Mathf.Clamp01(
                speed / profile.ReferenceSpeed);

            Vector3 targetPosition = Vector3.Scale(
                wave,
                profile.PositionAmplitude) *
                weight *
                speedWeight;

            Vector3 targetRotation = Vector3.Scale(
                wave,
                profile.RotationAmplitude) *
                weight *
                speedWeight;

            float interpolation = GetExponentialInterpolation(
                _config.BobSnappiness);

            _currentBobPosition = Vector3.Lerp(
                _currentBobPosition,
                targetPosition,
                interpolation);

            _currentBobRotation = Vector3.Lerp(
                _currentBobRotation,
                targetRotation,
                interpolation);

            position = _currentBobPosition;
            rotation = _currentBobRotation;

            if (_bobWeight <= Mathf.Epsilon)
            {
                _bobPhase = 0f;
            }
        }

        private void UpdateInertia(
            out Vector3 position,
            out Vector3 rotation)
        {
            Vector3 localVelocity =
                _motionSpace.InverseTransformDirection(
                    _movement.Velocity);

            localVelocity.y = 0f;

            if (!_hasTrackedVelocity)
            {
                _trackedLocalVelocity = localVelocity;
                _hasTrackedVelocity = true;
                position = Vector3.zero;
                rotation = Vector3.zero;
                return;
            }

            Vector3 previousTrackedVelocity =
                _trackedLocalVelocity;

            float velocityInterpolation =
                GetExponentialInterpolation(
                    _config.VelocityTrackingSpeed);

            _trackedLocalVelocity = Vector3.Lerp(
                _trackedLocalVelocity,
                localVelocity,
                velocityInterpolation);

            float deltaTime = Mathf.Max(
                Time.deltaTime,
                Mathf.Epsilon);

            Vector3 acceleration =
                (_trackedLocalVelocity -
                 previousTrackedVelocity) /
                deltaTime;

            acceleration = Vector3.ClampMagnitude(
                acceleration,
                _config.MaximumAcceleration);

            float aimMultiplier = Mathf.Lerp(
                1f,
                _config.AimInertiaMultiplier,
                _aimWeight);

            Vector3 targetPosition = new(
                -acceleration.x *
                _config.InertiaPosition.x,
                0f,
                -acceleration.z *
                _config.InertiaPosition.z);

            Vector3 targetRotation = new(
                acceleration.z *
                _config.InertiaRotation.x,
                -acceleration.x *
                _config.InertiaRotation.y,
                acceleration.x *
                _config.InertiaRotation.z);

            targetPosition *= aimMultiplier;
            targetRotation *= aimMultiplier;

            float inertiaInterpolation =
                GetExponentialInterpolation(
                    _config.InertiaSnappiness);

            _currentInertiaPosition = Vector3.Lerp(
                _currentInertiaPosition,
                targetPosition,
                inertiaInterpolation);

            _currentInertiaRotation = Vector3.Lerp(
                _currentInertiaRotation,
                targetRotation,
                inertiaInterpolation);

            position = _currentInertiaPosition;
            rotation = _currentInertiaRotation;
        }

        private void UpdateImpulseMotion()
        {
            float deltaTime = Mathf.Min(
                Time.deltaTime,
                0.05f);

            Vector3 positionAcceleration =
                -_impulsePosition *
                _config.ImpulseSpringStrength -
                _impulsePositionVelocity *
                _config.ImpulseDamping;

            _impulsePositionVelocity +=
                positionAcceleration * deltaTime;

            _impulsePosition +=
                _impulsePositionVelocity * deltaTime;

            Vector3 rotationAcceleration =
                -_impulseRotation *
                _config.ImpulseSpringStrength -
                _impulseRotationVelocity *
                _config.ImpulseDamping;

            _impulseRotationVelocity +=
                rotationAcceleration * deltaTime;

            _impulseRotation +=
                _impulseRotationVelocity * deltaTime;
        }

        private void UpdateSprintPose(
            out Vector3 position,
            out Vector3 rotation)
        {
            UpdateSprintPoseState();

            float targetWeight =
                _isSprintPoseActive
                    ? 1f
                    : 0f;

            float blendSpeed =
                _isSprintPoseActive
                    ? _config.SprintPoseEnterSpeed
                    : _config.SprintPoseExitSpeed;

            float interpolation =
                GetExponentialInterpolation(
                    blendSpeed);

            _sprintPoseWeight = Mathf.Lerp(
                _sprintPoseWeight,
                targetWeight,
                interpolation);

            rotation =
                _config.SprintRotation *
                _sprintPoseWeight;

            Quaternion sprintRotation =
                Quaternion.Euler(rotation);

            Vector3 pivotCompensation =
                _sprintPivotLocalPosition -
                sprintRotation *
                _sprintPivotLocalPosition;

            position =
                _config.SprintPosition *
                _sprintPoseWeight +
                pivotCompensation;
        }

        private void UpdateSprintPoseState()
        {
            bool hasMovementInput =
                _input.Move.sqrMagnitude >=
                MovementInputThreshold;

            bool mustExitSprintPose =
                _aimState.IsAiming ||
                !_movement.IsSprintHeld ||
                _movement.IsSliding ||
                !hasMovementInput;

            if (mustExitSprintPose)
            {
                _isSprintPoseActive = false;
                _sprintPoseMovementGraceTimer = 0f;
                return;
            }

            Vector3 horizontalVelocity =
                _movement.Velocity;

            horizontalVelocity.y = 0f;

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            if (!_isSprintPoseActive)
            {
                _isSprintPoseActive =
                    _movement.IsGrounded &&
                    horizontalSpeed >=
                    _config.MinimumSprintPoseEnterSpeed;

                return;
            }

            if (!_movement.IsGrounded ||
                horizontalSpeed >=
                _config.MinimumSprintPoseExitSpeed)
            {
                _sprintPoseMovementGraceTimer = 0f;
                return;
            }

            _sprintPoseMovementGraceTimer +=
                Time.deltaTime;

            if (_sprintPoseMovementGraceTimer <
                _config.SprintPoseMovementGraceTime)
            {
                return;
            }

            _isSprintPoseActive = false;
            _sprintPoseMovementGraceTimer = 0f;
        }

        private void UpdateBreathing(
            out Vector3 position,
            out Vector3 rotation)
        {
            Vector3 horizontalVelocity =
                _movement.Velocity;

            horizontalVelocity.y = 0f;

            bool shouldBreathe =
                _movement.IsGrounded &&
                !_movement.IsSliding &&
                !_isSprintPoseActive &&
                horizontalVelocity.sqrMagnitude <
                _config.MinimumBobSpeed *
                _config.MinimumBobSpeed;

            float blendSpeed = shouldBreathe
                ? _config.BreathingBlendInSpeed
                : _config.BreathingBlendOutSpeed;

            _breathingWeight = Mathf.MoveTowards(
                _breathingWeight,
                shouldBreathe ? 1f : 0f,
                blendSpeed * Time.deltaTime);

            if (_breathingWeight > Mathf.Epsilon)
            {
                _breathingPhase +=
                    _config.BreathingCyclesPerSecond *
                    FullCycle *
                    Time.deltaTime;

                _breathingPhase %= FullCycle;
            }

            float primaryWave =
                Mathf.Sin(_breathingPhase);

            float secondaryWave =
                Mathf.Cos(_breathingPhase);

            Vector3 wave = new(
                secondaryWave,
                primaryWave,
                -primaryWave);

            float aimMultiplier = Mathf.Lerp(
                1f,
                _config.AimBreathingMultiplier,
                _aimWeight);

            float weight =
                _breathingWeight *
                aimMultiplier;

            position = Vector3.Scale(
                wave,
                _config.BreathingPositionAmplitude) *
                weight;

            rotation = Vector3.Scale(
                wave,
                _config.BreathingRotationAmplitude) *
                weight;

            if (_breathingWeight <= Mathf.Epsilon)
            {
                _breathingPhase = 0f;
            }
        }

        private void HandleJumped()
        {
            if (_config == null)
            {
                return;
            }

            float multiplier = GetAimImpulseMultiplier();

            _impulsePositionVelocity.y -=
                _config.JumpPositionImpulse * multiplier;

            _impulseRotationVelocity.x +=
                _config.JumpPitchImpulse * multiplier;
        }

        private void HandleLanded(float downwardSpeed)
        {
            if (_config == null ||
                downwardSpeed < _config.MinimumLandingSpeed)
            {
                return;
            }

            float landingWeight = Mathf.InverseLerp(
                _config.MinimumLandingSpeed,
                _config.MaximumLandingSpeed,
                downwardSpeed);

            float positionImpulse = Mathf.Lerp(
                _config.MinimumLandingPositionImpulse,
                _config.MaximumLandingPositionImpulse,
                landingWeight);

            float pitchImpulse = Mathf.Lerp(
                _config.MinimumLandingPitchImpulse,
                _config.MaximumLandingPitchImpulse,
                landingWeight);

            float multiplier = GetAimImpulseMultiplier();

            _impulsePositionVelocity.y -=
                positionImpulse * multiplier;

            _impulseRotationVelocity.x +=
                pitchImpulse * multiplier;
        }

        private float GetAimImpulseMultiplier()
        {
            return Mathf.Lerp(
                1f,
                _config.AimImpulseMultiplier,
                _aimWeight);
        }

        private void ResetMotion()
        {
            _currentSwayPosition = Vector3.zero;
            _currentSwayRotation = Vector3.zero;
            _currentBobPosition = Vector3.zero;
            _currentBobRotation = Vector3.zero;
            _trackedLocalVelocity = Vector3.zero;
            _currentInertiaPosition = Vector3.zero;
            _currentInertiaRotation = Vector3.zero;
            _impulsePosition = Vector3.zero;
            _impulsePositionVelocity = Vector3.zero;
            _impulseRotation = Vector3.zero;
            _impulseRotationVelocity = Vector3.zero;
            _sprintPoseWeight = 0f;
            _sprintPoseMovementGraceTimer = 0f;
            _breathingPhase = 0f;
            _breathingWeight = 0f;
            _bobPhase = 0f;
            _bobWeight = 0f;
            _aimWeight = 0f;
            _hasTrackedVelocity = false;
            _isSprintPoseActive = false;

            if (_motionRoot == null)
            {
                return;
            }

            _motionRoot.localPosition = _baseLocalPosition;
            _motionRoot.localRotation = _baseLocalRotation;
        }

        private bool CanUpdate()
        {
            return
                _motionRoot != null &&
                _motionSpace != null &&
                _sprintRotationPivot != null &&
                _movement != null &&
                _config != null &&
                _input != null &&
                _aimState != null;
        }

        private float GetExponentialInterpolation(float speed)
        {
            return 1f - Mathf.Exp(
                -speed * Time.deltaTime);
        }

        private void ValidateReferences()
        {
            if (_motionRoot == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponMotion)} requires a MotionRoot reference.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponMotion)} requires a PlayerMovement reference.",
                    this);
            }

            if (_sprintRotationPivot == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponMotion)} requires a SprintRotationPivot reference.",
                    this);
            }

            if (_motionSpace == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponMotion)} requires a MotionSpace reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponMotion)} requires a WeaponMotionConfig reference.",
                    this);
            }
        }
    }
}
