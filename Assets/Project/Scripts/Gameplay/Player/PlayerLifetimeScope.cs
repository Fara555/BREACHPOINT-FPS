using Breachpoint.Gameplay.Player.Audio;
using Breachpoint.Gameplay.Player.Camera;
using Breachpoint.Gameplay.Player.Input;
using Breachpoint.Gameplay.Player.Look;
using Breachpoint.Gameplay.Player.Movement;
using Breachpoint.Gameplay.Player.Movement.Jump;
using Breachpoint.Gameplay.Player.Movement.Slide;
using Breachpoint.Gameplay.Player.Movement.Stance;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Breachpoint.Gameplay.Player.Composition
{
    public sealed class PlayerLifetimeScope : LifetimeScope
    {
        [Header("Config")]
        [SerializeField]
        private PlayerMovementConfig _movementConfig;

        [SerializeField]
        private PlayerMovementAudioConfig _movementAudioConfig;

        [Header("Player Components")]
        [SerializeField]
        private PlayerInputReader _inputReader;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private PlayerGroundDetector _groundDetector;

        [SerializeField]
        private PlayerCurbHandler _curbHandler;

        [SerializeField]
        private PlayerLook _look;

        [SerializeField]
        private PlayerCameraHeight _cameraHeight;

        [SerializeField]
        private PlayerMovementAudio _movementAudio;

        protected override void Configure(
            IContainerBuilder builder)
        {
            ValidateReferences();

            builder.RegisterInstance(
                _movementConfig);

            builder.RegisterInstance(
                _movementAudioConfig);

            builder.Register<PlayerJumpController>(
                Lifetime.Scoped);

            builder.Register<PlayerSlideController>(
                Lifetime.Scoped);

            builder.Register<PlayerStanceController>(
                Lifetime.Scoped);

            builder.RegisterComponent(
                    _inputReader)
                .As<IPlayerInput>();

            builder.RegisterComponent(
                _movement);

            builder.RegisterComponent(
                _groundDetector);

            builder.RegisterComponent(
                _curbHandler);

            builder.RegisterComponent(
                _look);

            builder.RegisterComponent(
                _cameraHeight);

            builder.RegisterComponent(
                _movementAudio);
        }

        private void ValidateReferences()
        {
            if (_movementConfig == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerMovementConfig.",
                    this);
            }

            if (_movementAudioConfig == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerMovementAudioConfig.",
                    this);
            }

            if (_inputReader == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerInputReader.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerMovement.",
                    this);
            }

            if (_groundDetector == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerGroundDetector.",
                    this);
            }

            if (_curbHandler == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerCurbHandler.",
                    this);
            }

            if (_look == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerLook.",
                    this);
            }

            if (_cameraHeight == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerCameraHeight.",
                    this);
            }

            if (_movementAudio == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires PlayerMovementAudio.",
                    this);
            }
        }
    }
}