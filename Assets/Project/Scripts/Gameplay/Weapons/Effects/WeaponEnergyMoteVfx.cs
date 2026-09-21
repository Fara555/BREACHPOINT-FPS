using UnityEngine;
using UnityEngine.Rendering;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponEnergyMoteVfx : MonoBehaviour
    {
        private static readonly int VisibilityId = Shader.PropertyToID(
            "_Visibility");

        [Header("Rendering")]
        [SerializeField]
        private Material _material;

        [SerializeField]
        private Vector3 _localOffset = new Vector3(0f, 0.12f, 0.02f);

        [SerializeField]
        private Vector3 _volumeSize = new Vector3(0.22f, 0.16f, 0.12f);

        [SerializeField, Min(1)]
        private int _maxParticles = 28;

        [SerializeField, Min(0f)]
        private float _emissionRate = 26f;

        [Header("Particle motion")]
        [SerializeField]
        private Vector2 _lifetimeRange = new Vector2(0.28f, 0.56f);

        [SerializeField]
        private Vector2 _sizeRange = new Vector2(0.014f, 0.036f);

        [SerializeField]
        private Vector2 _speedRange = new Vector2(0.012f, 0.038f);

        [SerializeField, Min(0f)]
        private float _noiseStrength = 0.032f;

        private ParticleSystem _particles;
        private ParticleSystemRenderer _particleRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private float _visibility;
        private float _fadeDuration;
        private float _fadeElapsed;
        private bool _isPlaying;
        private bool _isFadingOut;

        private void Awake()
        {
            EnsureInitialized();
            StopEffect();
        }

        private void OnDisable()
        {
            StopEffect();
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _fadeElapsed += Time.deltaTime;
            float normalizedTime = _fadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            float smoothTime = Mathf.SmoothStep(0f, 1f, normalizedTime);

            if (_isFadingOut)
            {
                SetVisibility(1f - smoothTime);

                if (normalizedTime >= 1f)
                {
                    StopEffect();
                }

                return;
            }

            SetVisibility(smoothTime);
        }

        public void PlayEffect(float fadeInDuration)
        {
            EnsureInitialized();

            if (_particles == null || _particleRenderer == null)
            {
                return;
            }

            _isPlaying = true;
            _isFadingOut = false;
            _fadeDuration = Mathf.Max(0f, fadeInDuration);
            _fadeElapsed = 0f;
            _particleRenderer.enabled = true;
            _particles.Clear(true);
            _particles.Play(true);
            SetVisibility(_fadeDuration <= 0f ? 1f : 0f);
        }

        public void BeginFadeOut(float duration)
        {
            EnsureInitialized();

            if (!_isPlaying)
            {
                return;
            }

            _isFadingOut = true;
            _fadeDuration = Mathf.Max(0f, duration);
            _fadeElapsed = 0f;

            if (_fadeDuration <= 0f)
            {
                StopEffect();
            }
        }

        public void StopEffect()
        {
            _isPlaying = false;
            _isFadingOut = false;
            _visibility = 0f;

            if (_particles != null)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SetVisibility(0f);

            if (_particleRenderer != null)
            {
                _particleRenderer.enabled = false;
            }
        }

        private void EnsureInitialized()
        {
            if (_particles != null)
            {
                return;
            }

            GameObject particleObject = new GameObject("IonizedMotes_Particles");
            particleObject.layer = transform.parent != null
                ? transform.parent.gameObject.layer
                : gameObject.layer;
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = _localOffset;

            _particles = particleObject.AddComponent<ParticleSystem>();
            _particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
            ConfigureParticleSystem();
        }

        private void ConfigureParticleSystem()
        {
            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                _lifetimeRange.x,
                _lifetimeRange.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                _speedRange.x,
                _speedRange.y);
            main.startSize = new ParticleSystem.MinMaxCurve(
                _sizeRange.x,
                _sizeRange.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                -Mathf.PI,
                Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.12f, 0.72f, 1f, 0.38f),
                new Color(0.36f, 0.92f, 1f, 0.88f));
            main.maxParticles = Mathf.Max(1, _maxParticles);

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = true;
            emission.rateOverTime = _emissionRate;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = _volumeSize;
            shape.randomDirectionAmount = 1f;

            ParticleSystem.NoiseModule noise = _particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.strength = _noiseStrength;
            noise.frequency = 1.15f;
            noise.scrollSpeed = 0.32f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule color =
                _particles.colorOverLifetime;
            color.enabled = true;
            Gradient colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.18f, 0.68f, 1f), 0f),
                    new GradientColorKey(new Color(0.42f, 0.94f, 1f), 0.58f),
                    new GradientColorKey(new Color(0.08f, 0.5f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.92f, 0.22f),
                    new GradientAlphaKey(0.66f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = colorGradient;

            ParticleSystem.SizeOverLifetimeModule size =
                _particles.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.18f, 1f),
                new Keyframe(0.72f, 0.72f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            _particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            _particleRenderer.alignment = ParticleSystemRenderSpace.View;
            _particleRenderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            _particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _particleRenderer.receiveShadows = false;
            _particleRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            _particleRenderer.material = _material;
        }

        private void SetVisibility(float visibility)
        {
            _visibility = Mathf.Clamp01(visibility);

            if (_particles != null)
            {
                ParticleSystem.EmissionModule emission = _particles.emission;
                emission.rateOverTime = _emissionRate * _visibility;
            }

            if (_particleRenderer == null || _propertyBlock == null)
            {
                return;
            }

            _particleRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(VisibilityId, _visibility);
            _particleRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnValidate()
        {
            _volumeSize.x = Mathf.Max(0f, _volumeSize.x);
            _volumeSize.y = Mathf.Max(0f, _volumeSize.y);
            _volumeSize.z = Mathf.Max(0f, _volumeSize.z);
            _maxParticles = Mathf.Max(1, _maxParticles);
            _emissionRate = Mathf.Max(0f, _emissionRate);
            _lifetimeRange.x = Mathf.Max(0.01f, _lifetimeRange.x);
            _lifetimeRange.y = Mathf.Max(
                _lifetimeRange.x,
                _lifetimeRange.y);
            _sizeRange.x = Mathf.Max(0.0001f, _sizeRange.x);
            _sizeRange.y = Mathf.Max(_sizeRange.x, _sizeRange.y);
            _speedRange.x = Mathf.Max(0f, _speedRange.x);
            _speedRange.y = Mathf.Max(_speedRange.x, _speedRange.y);
            _noiseStrength = Mathf.Max(0f, _noiseStrength);
        }
    }
}
