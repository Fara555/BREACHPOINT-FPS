using UnityEngine;

namespace Breachpoint.Audio
{
    public interface IAudioService
    {
        void Play(AudioCue cue);

        void Play(AudioCue cue, Vector3 position);

        void Play(AudioCue cue, Transform followTarget);

        AudioHandle PlayLoop(AudioCue cue, Vector3 position);

        AudioHandle PlayLoop(AudioCue cue, Transform followTarget);

        void StopAll();
    }
}