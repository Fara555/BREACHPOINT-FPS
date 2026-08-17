using UnityEngine;
using UnityEngine.Audio;

namespace Breachpoint.Audio
{
    [CreateAssetMenu(
        fileName = "AudioCue",
        menuName = "Breachpoint/Audio/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [Header("Clips")]
        [SerializeField] private AudioClip[] _clips;

        [Header("Output")]
        [SerializeField] private AudioMixerGroup _outputMixerGroup;

        [Header("Volume")]
        [SerializeField] private Vector2 _volumeRange = Vector2.one;

        [Header("Pitch")]
        [SerializeField] private Vector2 _pitchRange = Vector2.one;

        [Header("Spatial")]
        [Range(0f, 1f)]
        [SerializeField] private float _spatialBlend = 1f;

        [Min(0.01f)]
        [SerializeField] private float _minDistance = 1f;

        [Min(0.01f)]
        [SerializeField] private float _maxDistance = 25f;

        [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;

        [Header("Priority")]
        [Range(0, 256)]
        [SerializeField] private int _priority = 128;

        public AudioMixerGroup OutputMixerGroup => _outputMixerGroup;

        public Vector2 VolumeRange => _volumeRange;

        public Vector2 PitchRange => _pitchRange;

        public float SpatialBlend => _spatialBlend;

        public float MinDistance => _minDistance;

        public float MaxDistance => _maxDistance;

        public AudioRolloffMode RolloffMode => _rolloffMode;

        public int Priority => _priority;

        public AudioClip GetRandomClip()
        {
            if (_clips == null || _clips.Length == 0)
            {
                return null;
            }

            int startIndex = Random.Range(0, _clips.Length);

            for (int i = 0; i < _clips.Length; i++)
            {
                int index = (startIndex + i) % _clips.Length;
                AudioClip clip = _clips[index];

                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        public float GetRandomVolume()
        {
            return Random.Range(_volumeRange.x, _volumeRange.y);
        }

        public float GetRandomPitch()
        {
            return Random.Range(_pitchRange.x, _pitchRange.y);
        }

        private void OnValidate()
        {
            if (_volumeRange.x > _volumeRange.y)
            {
                _volumeRange.x = _volumeRange.y;
            }

            if (_pitchRange.x > _pitchRange.y)
            {
                _pitchRange.x = _pitchRange.y;
            }

            if (_maxDistance < _minDistance)
            {
                _maxDistance = _minDistance;
            }
        }
    }
}