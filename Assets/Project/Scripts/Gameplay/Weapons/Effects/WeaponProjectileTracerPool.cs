using UnityEngine;
using UnityEngine.Rendering;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponProjectileTracerPool : MonoBehaviour
    {
        [Header("Projectile Tracer")]
        [SerializeField]
        private Material _tracerMaterial;

        [SerializeField, Min(0.001f)]
        private float _length = 0.85f;

        [SerializeField, Min(0.001f)]
        private float _width = 0.035f;

        [SerializeField, Min(1f)]
        private float _travelSpeed = 240f;

        [SerializeField, Min(0f)]
        private float _spawnOffset = 0.12f;

        [Header("Pool")]
        [SerializeField, Min(1)]
        private int _poolSize = 16;

        private ProjectileTracerInstance[] _instances;
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
                ProjectileTracerInstance instance = _instances[i];

                if (instance.IsActive)
                {
                    UpdateTracer(instance);
                }
            }
        }

        public void Play(Vector3 origin, Vector3 target)
        {
            if (_instances == null || _instances.Length == 0)
            {
                return;
            }

            Vector3 offset = target - origin;
            float distance = offset.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                return;
            }

            ProjectileTracerInstance instance = _instances[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _instances.Length;

            instance.Origin = origin;
            instance.Direction = offset / distance;
            instance.Distance = distance;
            instance.LeadingDistance = Mathf.Min(_spawnOffset, distance);
            instance.IsActive = true;
            instance.Renderer.enabled = true;

            SetPositions(instance);
        }

        private void BuildPool()
        {
            if (_tracerMaterial == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponProjectileTracerPool)} requires a TracerMaterial reference.",
                    this);
                return;
            }

            _instances = new ProjectileTracerInstance[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                GameObject child = new($"ProjectileTracer_{i:00}");
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);

                LineRenderer renderer = child.AddComponent<LineRenderer>();
                ConfigureRenderer(renderer);
                _instances[i] = new ProjectileTracerInstance(renderer);
            }
        }

        private void ConfigureRenderer(LineRenderer renderer)
        {
            renderer.enabled = false;
            renderer.positionCount = 2;
            renderer.useWorldSpace = true;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 0;
            renderer.numCornerVertices = 0;
            renderer.widthMultiplier = _width;
            renderer.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 1f));
            renderer.sharedMaterial = _tracerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void UpdateTracer(ProjectileTracerInstance instance)
        {
            instance.LeadingDistance += _travelSpeed * Time.deltaTime;

            float trailingDistance = instance.LeadingDistance - _length;

            if (trailingDistance >= instance.Distance)
            {
                instance.IsActive = false;
                instance.Renderer.enabled = false;
                return;
            }

            SetPositions(instance);
        }

        private void SetPositions(ProjectileTracerInstance instance)
        {
            float leadingDistance = Mathf.Min(
                instance.LeadingDistance,
                instance.Distance);
            float trailingDistance = Mathf.Max(
                0f,
                instance.LeadingDistance - _length);

            trailingDistance = Mathf.Min(trailingDistance, leadingDistance);

            instance.Renderer.SetPosition(
                0,
                instance.Origin + instance.Direction * trailingDistance);
            instance.Renderer.SetPosition(
                1,
                instance.Origin + instance.Direction * leadingDistance);
        }

        private sealed class ProjectileTracerInstance
        {
            public ProjectileTracerInstance(LineRenderer renderer)
            {
                Renderer = renderer;
            }

            public LineRenderer Renderer { get; }
            public Vector3 Origin { get; set; }
            public Vector3 Direction { get; set; }
            public float Distance { get; set; }
            public float LeadingDistance { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
