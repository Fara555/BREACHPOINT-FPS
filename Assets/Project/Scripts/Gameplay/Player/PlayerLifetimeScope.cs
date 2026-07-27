using Breachpoint.Gameplay.Player.Input;
using Breachpoint.Gameplay.Player.Look;
using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Breachpoint.Gameplay.Player.Composition
{
    public sealed class PlayerLifetimeScope : LifetimeScope
    {
        [Header("Player Components")]
        [SerializeField] private PlayerInputReader _inputReader;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerGroundDetector _groundDetector;
        [SerializeField] private PlayerLook _look;

        protected override void Configure(IContainerBuilder builder)
        {
            ValidateReferences();

            builder.RegisterComponent(_inputReader)
                .As<IPlayerInput>();

            builder.RegisterComponent(_movement);
            builder.RegisterComponent(_groundDetector);
            builder.RegisterComponent(_look);
        }

        private void ValidateReferences()
        {
            if (_inputReader == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires a PlayerInputReader reference.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires a PlayerMovement reference.",
                    this);
            }

            if (_groundDetector == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires a PlayerGroundDetector reference.",
                    this);
            }

            if (_look == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLifetimeScope)} requires a PlayerLook reference.",
                    this);
            }
        }
    }
}