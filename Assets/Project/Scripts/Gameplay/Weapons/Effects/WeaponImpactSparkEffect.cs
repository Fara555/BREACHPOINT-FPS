using UnityEngine;
using UnityEngine.VFX;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponImpactSparkEffect : MonoBehaviour
    {
        private const float SpawnDuration = 0.04f;

        [SerializeField]
        private VisualEffectAsset _visualEffectAsset;

        [SerializeField, Min(1)]
        private int _poolSize = 12;

        [SerializeField, Min(0.05f)]
        private float _lifetime = 0.7f;

        [SerializeField, Min(0f)]
        private float _surfaceOffset = 0.012f;

        private SparkInstance[] _instances;
        private GameObject _poolRoot;
        private int _nextIndex;

        private void Awake()
        {
            BuildPool();
        }

        private void Update()
        {
            if (_instances == null)
            {
                return;
            }

            for (int i = 0; i < _instances.Length; i++)
            {
                SparkInstance instance = _instances[i];

                if (instance.IsSpawning &&
                    Time.time >= instance.SpawnStopTime)
                {
                    instance.Effect.Stop();
                    instance.IsSpawning = false;
                }

                if (instance.IsActive &&
                    Time.time >= instance.DisableTime)
                {
                    instance.IsActive = false;
                    instance.Root.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (_poolRoot != null)
            {
                Destroy(_poolRoot);
            }
        }

        public void Play(WeaponShotResult result)
        {
            if (!result.HasHit ||
                _instances == null ||
                _instances.Length == 0)
            {
                return;
            }

            SparkInstance instance = _instances[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _instances.Length;

            instance.Root.SetActive(false);
            instance.Root.transform.SetPositionAndRotation(
                result.EndPoint + result.Normal * _surfaceOffset,
                Quaternion.FromToRotation(Vector3.up, result.Normal) *
                Quaternion.AngleAxis(
                    Random.Range(0f, 360f),
                    Vector3.up));
            instance.Root.SetActive(true);

            instance.Effect.Reinit();
            instance.Effect.Play();
            instance.SpawnStopTime = Time.time + SpawnDuration;
            instance.DisableTime = Time.time + _lifetime;
            instance.IsSpawning = true;
            instance.IsActive = true;
        }

        private void BuildPool()
        {
            if (_visualEffectAsset == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponImpactSparkEffect)} requires a VisualEffectAsset reference.",
                    this);
                return;
            }

            _instances = new SparkInstance[_poolSize];
            _poolRoot = new GameObject($"{name}_ImpactSparkPool");

            for (int i = 0; i < _instances.Length; i++)
            {
                GameObject root = new($"ImpactSpark_{i:00}");
                root.layer = 0;
                root.transform.SetParent(_poolRoot.transform, false);

                VisualEffect effect = root.AddComponent<VisualEffect>();
                effect.visualEffectAsset = _visualEffectAsset;
                effect.initialEventName = string.Empty;
                effect.Stop();

                root.SetActive(false);
                _instances[i] = new SparkInstance(root, effect);
            }
        }

        private sealed class SparkInstance
        {
            public SparkInstance(GameObject root, VisualEffect effect)
            {
                Root = root;
                Effect = effect;
            }

            public GameObject Root { get; }
            public VisualEffect Effect { get; }
            public float SpawnStopTime { get; set; }
            public float DisableTime { get; set; }
            public bool IsSpawning { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
