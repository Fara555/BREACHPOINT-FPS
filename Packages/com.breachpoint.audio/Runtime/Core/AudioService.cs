using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Breachpoint.Audio
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        [Header("Pool")]
        [Min(1)]
        [Tooltip("Number of AudioSources created at startup. The pool grows as needed.")]
        [FormerlySerializedAs("_maxPoolSize")]
        [SerializeField] private int _prewarmSourceCount = 48;

        private readonly List<AudioSourceSlot> _activeSlots = new();
        private readonly Stack<AudioSourceSlot> _availableSlots = new();
        private readonly Dictionary<int, AudioSourceSlot> _loopPlaybacks = new();

        private int _createdSourceCount;
        private int _nextPlaybackId = 1;
        private bool _initialized;

        private void Awake()
        {
            InitializePool();
        }

        private void Update()
        {
            UpdateActiveSources();
        }

        private void OnDestroy()
        {
            StopAll();
        }

        public void Play(AudioCue cue)
        {
            PlayInternal(
                cue,
                transform.position,
                null,
                false);
        }

        public void Play(AudioCue cue, Vector3 position)
        {
            PlayInternal(
                cue,
                position,
                null,
                false);
        }

        public void Play(AudioCue cue, Transform followTarget)
        {
            if (followTarget == null)
            {
                return;
            }

            PlayInternal(
                cue,
                followTarget.position,
                followTarget,
                false);
        }

        public AudioHandle PlayLoop(AudioCue cue, Vector3 position)
        {
            return PlayInternal(
                cue,
                position,
                null,
                true);
        }

        public AudioHandle PlayLoop(AudioCue cue, Transform followTarget)
        {
            if (followTarget == null)
            {
                return default;
            }

            return PlayInternal(
                cue,
                followTarget.position,
                followTarget,
                true);
        }

        public void StopAll()
        {
            for (int i = _activeSlots.Count - 1; i >= 0; i--)
            {
                ReleaseSlot(_activeSlots[i], i);
            }
        }

        internal bool IsPlaybackActive(int playbackId)
        {
            if (playbackId <= 0)
            {
                return false;
            }

            return _loopPlaybacks.ContainsKey(playbackId);
        }

        internal void StopPlayback(int playbackId)
        {
            if (playbackId <= 0)
            {
                return;
            }

            if (!_loopPlaybacks.TryGetValue(playbackId, out AudioSourceSlot slot))
            {
                return;
            }

            ReleaseSlot(slot);
        }

        private AudioHandle PlayInternal(
            AudioCue cue,
            Vector3 position,
            Transform followTarget,
            bool loop)
        {
            if (cue == null)
            {
                return default;
            }

            AudioClip clip = cue.GetRandomClip();

            if (clip == null)
            {
                return default;
            }

            AudioSourceSlot slot = AcquireSlot();

            ConfigureSource(
                slot,
                cue,
                clip,
                position,
                followTarget,
                loop);

            _activeSlots.Add(slot);

            if (!loop)
            {
                slot.Source.Play();
                return default;
            }

            int playbackId = GetNextPlaybackId();

            slot.PlaybackId = playbackId;

            _loopPlaybacks.Add(playbackId, slot);

            slot.Source.Play();

            return new AudioHandle(this, playbackId);
        }

        private void InitializePool()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            for (int i = 0; i < _prewarmSourceCount; i++)
            {
                _availableSlots.Push(CreateSlot());
            }
        }

        private AudioSourceSlot AcquireSlot()
        {
            if (!_initialized)
            {
                InitializePool();
            }

            if (_availableSlots.Count > 0)
            {
                return _availableSlots.Pop();
            }

            return CreateSlot();
        }

        private AudioSourceSlot CreateSlot()
        {
            GameObject sourceObject = new(
                $"AudioSource_{_createdSourceCount:00}");

            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();

            source.playOnAwake = false;

            _createdSourceCount++;
            return new AudioSourceSlot(source);
        }

        private void ConfigureSource(
            AudioSourceSlot slot,
            AudioCue cue,
            AudioClip clip,
            Vector3 position,
            Transform followTarget,
            bool loop)
        {
            AudioSource source = slot.Source;

            slot.FollowTarget = followTarget;
            slot.PlaybackId = 0;

            source.transform.position = position;

            source.clip = clip;
            source.outputAudioMixerGroup = cue.OutputMixerGroup;

            source.volume = cue.GetRandomVolume();
            source.pitch = cue.GetRandomPitch();

            source.spatialBlend = cue.SpatialBlend;
            source.minDistance = cue.MinDistance;
            source.maxDistance = cue.MaxDistance;
            source.rolloffMode = cue.RolloffMode;

            source.priority = cue.Priority;

            source.loop = loop;
        }

        private void UpdateActiveSources()
        {
            for (int i = _activeSlots.Count - 1; i >= 0; i--)
            {
                AudioSourceSlot slot = _activeSlots[i];

                Transform followTarget = slot.FollowTarget;

                // A destroyed Unity object compares equal to null but retains its managed reference.
                bool lostLoopTarget = slot.PlaybackId > 0 &&
                                      followTarget is not null &&
                                      followTarget == null;
                if (lostLoopTarget || !slot.Source.isPlaying)
                {
                    ReleaseSlot(slot, i);
                    continue;
                }

                if (followTarget != null)
                {
                    slot.Source.transform.position = followTarget.position;
                }
            }
        }

        private void ReleaseSlot(AudioSourceSlot slot)
        {
            int index = _activeSlots.IndexOf(slot);

            if (index < 0)
            {
                return;
            }

            ReleaseSlot(slot, index);
        }

        private void ReleaseSlot(AudioSourceSlot slot, int activeIndex)
        {
            AudioSource source = slot.Source;

            source.Stop();

            if (slot.PlaybackId > 0)
            {
                _loopPlaybacks.Remove(slot.PlaybackId);
            }

            slot.PlaybackId = 0;
            slot.FollowTarget = null;

            source.clip = null;
            source.outputAudioMixerGroup = null;

            source.volume = 1f;
            source.pitch = 1f;

            source.spatialBlend = 0f;
            source.loop = false;

            _activeSlots.RemoveAt(activeIndex);
            _availableSlots.Push(slot);
        }

        private int GetNextPlaybackId()
        {
            if (_nextPlaybackId == int.MaxValue)
            {
                _nextPlaybackId = 1;
            }

            while (_loopPlaybacks.ContainsKey(_nextPlaybackId))
            {
                _nextPlaybackId++;
            }

            return _nextPlaybackId++;
        }

        private sealed class AudioSourceSlot
        {
            public AudioSourceSlot(AudioSource source)
            {
                Source = source;
            }

            public AudioSource Source { get; }

            public Transform FollowTarget { get; set; }

            public int PlaybackId { get; set; }
        }
    }
}