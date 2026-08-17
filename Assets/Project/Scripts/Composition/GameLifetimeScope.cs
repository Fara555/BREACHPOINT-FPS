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

        protected override void Configure(
            IContainerBuilder builder)
        {
            ValidateReferences();

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