using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Camera
{
    public sealed class PlayerHeadBob : MonoBehaviour
    {
        private const float FullCycle =
            Mathf.PI * 2f;

        private const float VerticalWavePower =
            1.6f;

        [Header("References")]
        [SerializeField]
        private Transform _headBobRoot;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private PlayerHeadBobConfig _config;

        private Vector3 _baseLocalPosition;

        private float _phase;
        private float _effectWeight;

        private void Awake()
        {
            ValidateReferences();
            Initialize();
        }

        private void LateUpdate()
        {
            if (!CanUpdate())
            {
                return;
            }

            float horizontalSpeed =
                GetHorizontalSpeed(
                    _movement.Velocity);

            bool shouldBob =
                _movement.IsGrounded &&
                horizontalSpeed >=
                _config.MinimumMovementSpeed;

            UpdateEffectWeight(
                shouldBob);

            if (shouldBob)
            {
                HeadBobProfile profile =
                    GetCurrentProfile();

                AdvancePhase(
                    horizontalSpeed,
                    profile.CyclesPerMeter);

                ApplyBob(
                    profile);

                return;
            }

            ReturnToCenter();
        }

        private void UpdateEffectWeight(
            bool shouldBob)
        {
            float targetWeight =
                shouldBob
                    ? 1f
                    : 0f;

            float blendSpeed =
                shouldBob
                    ? _config.BlendInSpeed
                    : _config.BlendOutSpeed;

            _effectWeight =
                Mathf.MoveTowards(
                    _effectWeight,
                    targetWeight,
                    blendSpeed *
                    Time.deltaTime);
        }

        private void AdvancePhase(
            float horizontalSpeed,
            float cyclesPerMeter)
        {
            float traveledDistance =
                horizontalSpeed *
                Time.deltaTime;

            _phase +=
                traveledDistance *
                cyclesPerMeter *
                FullCycle;

            _phase %= FullCycle;
        }

        private void ApplyBob(
            HeadBobProfile profile)
        {
            float horizontalWave =
                Mathf.Sin(
                    _phase);

            float footstepPulse =
                Mathf.Pow(
                    Mathf.Abs(
                        Mathf.Sin(_phase)),
                    VerticalWavePower);

            float centeredVerticalWave =
                0.35f -
                footstepPulse;

            float horizontalOffset =
                horizontalWave *
                profile.HorizontalAmplitude *
                _effectWeight;

            float verticalOffset =
                centeredVerticalWave *
                profile.VerticalAmplitude *
                _effectWeight;

            Vector3 targetPosition =
                _baseLocalPosition +
                new Vector3(
                    horizontalOffset,
                    verticalOffset,
                    0f);

            _headBobRoot.localPosition =
                targetPosition;

            _headBobRoot.localRotation =
                Quaternion.identity;
        }

        private void ReturnToCenter()
        {
            float interpolation =
                1f -
                Mathf.Exp(
                    -_config.ReturnSpeed *
                    Time.deltaTime);

            _headBobRoot.localPosition =
                Vector3.Lerp(
                    _headBobRoot.localPosition,
                    _baseLocalPosition,
                    interpolation);

            _headBobRoot.localRotation =
                Quaternion.identity;

            if (_effectWeight <= Mathf.Epsilon)
            {
                _phase = 0f;
            }
        }

        private HeadBobProfile GetCurrentProfile()
        {
            return _movement.CurrentState switch
            {
                PlayerMovementState.Sprinting =>
                    new HeadBobProfile(
                        _config.SprintCyclesPerMeter,
                        _config.SprintHorizontalAmplitude,
                        _config.SprintVerticalAmplitude),

                PlayerMovementState.Crouching =>
                    new HeadBobProfile(
                        _config.CrouchCyclesPerMeter,
                        _config.CrouchHorizontalAmplitude,
                        _config.CrouchVerticalAmplitude),

                _ =>
                    new HeadBobProfile(
                        _config.WalkCyclesPerMeter,
                        _config.WalkHorizontalAmplitude,
                        _config.WalkVerticalAmplitude)
            };
        }

        private static float GetHorizontalSpeed(
            Vector3 velocity)
        {
            velocity.y = 0f;

            return velocity.magnitude;
        }

        private bool CanUpdate()
        {
            return
                _headBobRoot != null &&
                _movement != null &&
                _config != null;
        }

        private void Initialize()
        {
            if (_headBobRoot == null)
            {
                return;
            }

            _baseLocalPosition =
                _headBobRoot.localPosition;

            _headBobRoot.localRotation =
                Quaternion.identity;

            _phase = 0f;
            _effectWeight = 0f;
        }

        private void ValidateReferences()
        {
            if (_headBobRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHeadBob)} requires a HeadBobRoot reference.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHeadBob)} requires a PlayerMovement reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHeadBob)} requires a PlayerHeadBobConfig reference.",
                    this);
            }
        }

        private readonly struct HeadBobProfile
        {
            public float CyclesPerMeter { get; }

            public float HorizontalAmplitude { get; }

            public float VerticalAmplitude { get; }

            public HeadBobProfile(
                float cyclesPerMeter,
                float horizontalAmplitude,
                float verticalAmplitude)
            {
                CyclesPerMeter =
                    cyclesPerMeter;

                HorizontalAmplitude =
                    horizontalAmplitude;

                VerticalAmplitude =
                    verticalAmplitude;
            }
        }
    }
}