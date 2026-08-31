using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class BulletImpactPool : MonoBehaviour
    {
        private const float SpawnDuration = 0.05f;

        [SerializeField]
        private SurfaceImpactSetup[] _surfaceEffects;

        [SerializeField, Min(0f)]
        private float _surfaceOffset = 0.003f;

        [SerializeField, Min(0f)]
        private float _decalFadeDuration = 2f;

        private ImpactBucket[] _buckets;

        private void Awake()
        {
            BuildPools();
        }

        private void Update()
        {
            if (_buckets == null)
            {
                return;
            }

            for (int bucketIndex = 0;
                 bucketIndex < _buckets.Length;
                 bucketIndex++)
            {
                ImpactEntry[] entries = _buckets[bucketIndex].Entries;

                for (int entryIndex = 0;
                     entryIndex < entries.Length;
                     entryIndex++)
                {
                    ImpactEntry entry = entries[entryIndex];

                    if (entry.IsSpawning &&
                        Time.time >= entry.SpawnStopTime)
                    {
                        StopVisualEffects(entry);
                    }

                    if (entry.IsActive &&
                        Time.time >= entry.DisableTime)
                    {
                        Deactivate(entry);
                    }
                    else if (entry.IsActive)
                    {
                        UpdateDecalFade(entry);
                    }
                }
            }
        }

        public void Play(WeaponShotResult result)
        {
            if (!result.HasHit || _buckets == null)
            {
                return;
            }

            SurfaceType surfaceType = GetSurfaceType(
                result.HitCollider);

            ImpactBucket bucket = FindBucket(surfaceType) ??
                                  FindBucket(SurfaceType.Default);

            if (bucket == null || bucket.Entries.Length == 0)
            {
                return;
            }

            ImpactEntry entry = bucket.Acquire();

            PrepareEntry(
                entry,
                bucket.Lifetime,
                result);
        }

        private void BuildPools()
        {
            if (_surfaceEffects == null)
            {
                return;
            }

            int validSetupCount = 0;

            for (int i = 0; i < _surfaceEffects.Length; i++)
            {
                if (_surfaceEffects[i] != null &&
                    _surfaceEffects[i].Prefab != null)
                {
                    validSetupCount++;
                }
            }

            _buckets = new ImpactBucket[validSetupCount];
            int bucketIndex = 0;

            for (int setupIndex = 0;
                 setupIndex < _surfaceEffects.Length;
                 setupIndex++)
            {
                SurfaceImpactSetup setup = _surfaceEffects[setupIndex];

                if (setup == null || setup.Prefab == null)
                {
                    continue;
                }

                ImpactEntry[] entries = new ImpactEntry[setup.PoolSize];

                for (int entryIndex = 0;
                     entryIndex < entries.Length;
                     entryIndex++)
                {
                    GameObject instance = Instantiate(
                        setup.Prefab,
                        transform);

                    instance.name =
                        $"{setup.SurfaceType}_Impact_{entryIndex:00}";

                    instance.SetActive(false);

                    entries[entryIndex] = new ImpactEntry(
                        instance,
                        CreateVisualEffects(instance, setup.VisualEffectAssets),
                        instance.GetComponentsInChildren<DecalProjector>(true));
                }

                _buckets[bucketIndex] = new ImpactBucket(
                    setup.SurfaceType,
                    setup.Lifetime,
                    entries);

                bucketIndex++;
            }
        }

        private void PrepareEntry(
            ImpactEntry entry,
            float lifetime,
            WeaponShotResult result)
        {
            entry.Instance.SetActive(false);
            entry.Instance.transform.SetParent(transform, false);

            Transform targetTransform =
                result.HitCollider.attachedRigidbody != null
                    ? result.HitCollider.transform
                    : null;

            entry.Instance.transform.SetParent(
                targetTransform,
                true);

            entry.Instance.transform.SetPositionAndRotation(
                result.EndPoint + result.Normal * _surfaceOffset,
                Quaternion.LookRotation(result.Normal) *
                Quaternion.AngleAxis(
                    UnityEngine.Random.Range(0f, 360f),
                    Vector3.forward));

            entry.Instance.SetActive(true);
            SetDecalFade(entry, 1f);

            for (int i = 0; i < entry.VisualEffects.Length; i++)
            {
                VisualEffect effect = entry.VisualEffects[i];
                effect.Reinit();
                effect.Play();
            }

            entry.SpawnStopTime = Time.time + SpawnDuration;
            entry.IsSpawning = true;
            entry.DisableTime = Time.time + lifetime;
            entry.IsActive = true;
        }

        private void UpdateDecalFade(ImpactEntry entry)
        {
            if (_decalFadeDuration <= 0f)
            {
                return;
            }

            float remaining = entry.DisableTime - Time.time;
            if (remaining >= _decalFadeDuration)
            {
                return;
            }

            float normalized = Mathf.Clamp01(
                remaining / _decalFadeDuration);
            float smoothFade = normalized * normalized *
                               (3f - 2f * normalized);
            SetDecalFade(entry, smoothFade);
        }

        private static void SetDecalFade(
            ImpactEntry entry,
            float fadeFactor)
        {
            for (int i = 0; i < entry.Decals.Length; i++)
            {
                entry.Decals[i].fadeFactor = fadeFactor;
            }
        }

        private static VisualEffect[] CreateVisualEffects(
            GameObject instance,
            VisualEffectAsset[] assets)
        {
            if (assets == null || assets.Length == 0)
            {
                return Array.Empty<VisualEffect>();
            }

            VisualEffect[] effects = new VisualEffect[assets.Length];

            for (int i = 0; i < assets.Length; i++)
            {
                GameObject effectObject = new($"VFX_{assets[i].name}");
                effectObject.layer = instance.layer;
                effectObject.transform.SetParent(instance.transform, false);
                effectObject.transform.localRotation =
                    Quaternion.Euler(90f, 0f, 0f);

                VisualEffect effect = effectObject.AddComponent<VisualEffect>();
                effect.visualEffectAsset = assets[i];
                effect.initialEventName = string.Empty;
                effect.Reinit();
                effect.Stop();
                effects[i] = effect;
            }

            return effects;
        }

        private static void StopVisualEffects(ImpactEntry entry)
        {
            for (int i = 0; i < entry.VisualEffects.Length; i++)
            {
                entry.VisualEffects[i].Stop();
            }

            entry.IsSpawning = false;
        }

        private void Deactivate(ImpactEntry entry)
        {
            StopVisualEffects(entry);
            entry.IsActive = false;
            entry.Instance.SetActive(false);
            entry.Instance.transform.SetParent(transform, false);
        }

        private ImpactBucket FindBucket(SurfaceType surfaceType)
        {
            for (int i = 0; i < _buckets.Length; i++)
            {
                if (_buckets[i].SurfaceType == surfaceType)
                {
                    return _buckets[i];
                }
            }

            return null;
        }

        private static SurfaceType GetSurfaceType(Collider collider)
        {
            SurfaceIdentifier identifier =
                collider.GetComponentInParent<SurfaceIdentifier>();

            return identifier != null
                ? identifier.SurfaceType
                : SurfaceType.Default;
        }

        [Serializable]
        private sealed class SurfaceImpactSetup
        {
            [SerializeField]
            private SurfaceType _surfaceType = SurfaceType.Default;

            [SerializeField]
            private GameObject _prefab;

            [SerializeField]
            private VisualEffectAsset[] _visualEffectAssets;

            [SerializeField, Min(1)]
            private int _poolSize = 24;

            [SerializeField, Min(0.1f)]
            private float _lifetime = 15f;

            public SurfaceType SurfaceType => _surfaceType;
            public GameObject Prefab => _prefab;
            public VisualEffectAsset[] VisualEffectAssets => _visualEffectAssets;
            public int PoolSize => _poolSize;
            public float Lifetime => _lifetime;
        }

        private sealed class ImpactBucket
        {
            private int _nextIndex;

            public ImpactBucket(
                SurfaceType surfaceType,
                float lifetime,
                ImpactEntry[] entries)
            {
                SurfaceType = surfaceType;
                Lifetime = lifetime;
                Entries = entries;
            }

            public SurfaceType SurfaceType { get; }
            public float Lifetime { get; }
            public ImpactEntry[] Entries { get; }

            public ImpactEntry Acquire()
            {
                ImpactEntry entry = Entries[_nextIndex];

                _nextIndex = (_nextIndex + 1) % Entries.Length;

                return entry;
            }
        }

        private sealed class ImpactEntry
        {
            public ImpactEntry(
                GameObject instance,
                VisualEffect[] visualEffects,
                DecalProjector[] decals)
            {
                Instance = instance;
                VisualEffects = visualEffects;
                Decals = decals;
            }

            public GameObject Instance { get; }
            public VisualEffect[] VisualEffects { get; }
            public DecalProjector[] Decals { get; }
            public float DisableTime { get; set; }
            public float SpawnStopTime { get; set; }
            public bool IsActive { get; set; }
            public bool IsSpawning { get; set; }
        }
    }
}
