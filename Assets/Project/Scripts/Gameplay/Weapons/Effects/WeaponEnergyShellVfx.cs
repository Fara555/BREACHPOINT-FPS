using UnityEngine;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponEnergyShellVfx : MonoBehaviour
    {
        private static readonly int VisibilityId = Shader.PropertyToID(
            "_Visibility");

        [SerializeField]
        private Renderer _shellRenderer;

        [SerializeField, Range(0.5f, 1f)]
        private float _appearScaleMultiplier = 0.84f;

        [SerializeField, Range(1f, 1.25f)]
        private float _disappearScaleMultiplier = 1.08f;

        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _baseScale;
        private float _visibility;
        private float _fadeDuration;
        private float _fadeElapsed;
        private bool _isPlaying;
        private bool _isFadingOut;
        private bool _isInitialized;

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
                transform.localScale = _baseScale * Mathf.Lerp(
                    1f,
                    _disappearScaleMultiplier,
                    smoothTime);

                if (normalizedTime >= 1f)
                {
                    StopEffect();
                }

                return;
            }

            SetVisibility(smoothTime);
            transform.localScale = _baseScale * Mathf.Lerp(
                _appearScaleMultiplier,
                1f,
                smoothTime);
        }

        public void PlayEffect(float fadeInDuration)
        {
            EnsureInitialized();

            if (_shellRenderer == null)
            {
                return;
            }

            _isPlaying = true;
            _isFadingOut = false;
            _fadeDuration = Mathf.Max(0f, fadeInDuration);
            _fadeElapsed = 0f;
            transform.localScale = _baseScale * _appearScaleMultiplier;
            _shellRenderer.enabled = true;
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
            EnsureInitialized();
            _isPlaying = false;
            _isFadingOut = false;
            _visibility = 0f;
            transform.localScale = _baseScale;

            if (_shellRenderer == null)
            {
                return;
            }

            SetVisibility(0f);
            _shellRenderer.enabled = false;
        }

        private void SetVisibility(float visibility)
        {
            _visibility = Mathf.Clamp01(visibility);

            if (_shellRenderer == null || _propertyBlock == null)
            {
                return;
            }

            _shellRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(VisibilityId, _visibility);
            _shellRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            _baseScale = transform.localScale;
            _propertyBlock = new MaterialPropertyBlock();
            _isInitialized = true;
        }

        private void OnValidate()
        {
            _appearScaleMultiplier = Mathf.Clamp(
                _appearScaleMultiplier,
                0.5f,
                1f);
            _disappearScaleMultiplier = Mathf.Clamp(
                _disappearScaleMultiplier,
                1f,
                1.25f);
        }
    }
}
