using Breachpoint.Audio;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Breachpoint.Composition
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Audio")]
        [SerializeField]
        private AudioService _audioService;

        [Header("Authored enemy cover") ]
        [SerializeField] private Breachpoint.Gameplay.AI.EnemyCoverPoint[] _coverPoints;

        protected override void Configure(
            IContainerBuilder builder)
        {
            ValidateReferences();

            builder.Register<Breachpoint.Gameplay.AI.EnemyWorld>(Lifetime.Singleton);
            builder.Register<Breachpoint.Gameplay.AI.EnemySquadService>(Lifetime.Singleton);
            builder.Register(resolver => new Breachpoint.Gameplay.AI.EnemyCoverService(_coverPoints), Lifetime.Singleton);

            builder.RegisterComponent(
                    _audioService)
                .As<IAudioService>();
        }

        private void ValidateReferences()
        {
            if (_audioService == null)
            {
                Debug.LogError(
                    $"{nameof(GameLifetimeScope)} requires AudioService.",
                    this);
            }
        }
    }
}
