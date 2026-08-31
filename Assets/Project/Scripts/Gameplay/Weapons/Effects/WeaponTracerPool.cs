using UnityEngine;
using UnityEngine.Rendering;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponTracerPool : MonoBehaviour
    {
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");
        private static readonly int NoiseOffset = Shader.PropertyToID("_NoiseOffset");

        [Header("Air Trail")]
        [SerializeField]
        private Material _airTrailMaterial;

        [SerializeField, Min(0.001f)]
        private float _width = 0.1f;

        [SerializeField, Min(0.001f)]
        private float _textureScale = 0.15f;

        [Header("Timing")]
        [SerializeField, Min(1f)]
        private float _travelSpeed = 900f;

        [SerializeField, Min(0f)]
        private float _holdDuration = 0.055f;

        [SerializeField, Min(0.01f)]
        private float _fadeDuration = 0.28f;

        [SerializeField, Min(0f)]
        private float _muzzleOffset = 0.18f;

        [Header("Pool")]
        [SerializeField, Min(1)]
        private int _poolSize = 16;

        private TracerInstance[] _instances;
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
                TracerInstance instance = _instances[i];

                if (instance.IsActive)
                {
                    UpdateTrail(instance);
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

            TracerInstance instance = _instances[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _instances.Length;

            instance.Origin = origin;
            instance.Direction = offset / distance;
            instance.Distance = distance;
            instance.VisibleStart = Mathf.Min(_muzzleOffset, distance);
            instance.HeadDistance = instance.VisibleStart;
            instance.FadeStartTime = float.PositiveInfinity;
            instance.IsActive = true;

            instance.PropertyBlock.SetFloat(Opacity, 1f);
            instance.PropertyBlock.SetFloat(NoiseOffset, Random.value * 8f);
            instance.Renderer.SetPropertyBlock(instance.PropertyBlock);
            instance.Renderer.textureScale = new Vector2(_textureScale, 1f);
            instance.Renderer.enabled = true;

            SetPositions(instance);
        }

        private void BuildPool()
        {
            if (_airTrailMaterial == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponTracerPool)} requires an AirTrailMaterial reference.",
                    this);
                return;
            }

            _instances = new TracerInstance[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                GameObject child = new($"AirTrail_{i:00}");
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);

                LineRenderer renderer = child.AddComponent<LineRenderer>();
                ConfigureRenderer(renderer);
                _instances[i] = new TracerInstance(renderer);
            }
        }

        private void ConfigureRenderer(LineRenderer renderer)
        {
            renderer.enabled = false;
            renderer.positionCount = 2;
            renderer.useWorldSpace = true;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Tile;
            renderer.numCapVertices = 0;
            renderer.numCornerVertices = 0;
            renderer.widthMultiplier = _width;
            renderer.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 1f));
            renderer.sharedMaterial = _airTrailMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void UpdateTrail(TracerInstance instance)
        {
            if (instance.HeadDistance < instance.Distance)
            {
                instance.HeadDistance = Mathf.Min(
                    instance.HeadDistance + _travelSpeed * Time.deltaTime,
                    instance.Distance);
                SetPositions(instance);

                if (instance.HeadDistance >= instance.Distance)
                {
                    instance.FadeStartTime = Time.time + _holdDuration;
                }

                return;
            }

            if (Time.time < instance.FadeStartTime)
            {
                return;
            }

            float fade = Mathf.Clamp01(
                (Time.time - instance.FadeStartTime) / _fadeDuration);
            float opacity = 1f - fade * fade * (3f - 2f * fade);

            instance.PropertyBlock.SetFloat(Opacity, opacity);
            instance.Renderer.SetPropertyBlock(instance.PropertyBlock);

            if (fade < 1f)
            {
                return;
            }

            instance.IsActive = false;
            instance.Renderer.enabled = false;
        }

        private static void SetPositions(TracerInstance instance)
        {
            instance.Renderer.SetPosition(
                0,
                instance.Origin + instance.Direction * instance.VisibleStart);
            instance.Renderer.SetPosition(
                1,
                instance.Origin + instance.Direction * instance.HeadDistance);
        }

        private sealed class TracerInstance
        {
            public TracerInstance(LineRenderer renderer)
            {
                Renderer = renderer;
                PropertyBlock = new MaterialPropertyBlock();
            }

            public LineRenderer Renderer { get; }
            public MaterialPropertyBlock PropertyBlock { get; }
            public Vector3 Origin { get; set; }
            public Vector3 Direction { get; set; }
            public float Distance { get; set; }
            public float VisibleStart { get; set; }
            public float HeadDistance { get; set; }
            public float FadeStartTime { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
