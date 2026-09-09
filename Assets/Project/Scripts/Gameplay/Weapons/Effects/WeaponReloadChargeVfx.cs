using System;
using UnityEngine;
using UnityEngine.VFX;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponReloadChargeVfx :
        MonoBehaviour,
        IWeaponReloadWindowSource
    {
        public event Action<float> ReloadWindowStarted;
        public event Action ReloadWindowEnded;

        [Header("References")]
        [SerializeField]
        private PlayerWeaponController _weaponController;

        [SerializeField]
        private PlayerWeaponView _weaponView;

        [SerializeField]
        private VisualEffect[] _reloadEffects;

        [SerializeField]
        private GameObject[] _reloadObjects;

        [SerializeField]
        private Light[] _chargeLights;

        [Header("Reload window")]
        [Tooltip("Fraction of the complete reload occupied by the charge phase.")]
        [SerializeField, Range(0f, 1f)]
        private float _reloadWindowNormalizedDuration = 0.423f;

        [Header("Fist light")]
        [SerializeField, Min(0f)]
        private float _peakLightIntensity = 9000000f;

        [SerializeField, Min(0f)]
        private float _lightFadeInDuration = 0.18f;

        [SerializeField, Min(0f)]
        private float _lightFadeOutDuration = 0.18f;

        [SerializeField]
        private AnimationCurve _lightFadeCurve = AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f);

        private float _currentLightIntensity;
        private float _lightFadeFrom;
        private float _lightFadeTo;
        private float _lightFadeDuration;
        private float _lightFadeElapsed;
        private float _currentReloadDuration;
        private bool _isReloading;
        private bool _effectsActive;
        private bool _isReloadWindowActive;
        private bool _isLightFading;
        private bool _stopEffectsAfterFade;

        private void Awake()
        {
            ResolveReferences();
            StopEffectsImmediate();
        }

        private void OnEnable()
        {
            if (_weaponController == null)
            {
                return;
            }

            _weaponController.ReloadStarted += HandleReloadStarted;
            _weaponController.ReloadCompleted += HandleReloadCompleted;
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.ReloadStarted -= HandleReloadStarted;
                _weaponController.ReloadCompleted -= HandleReloadCompleted;
            }

            _isReloading = false;
            EndReloadWindow();
            StopEffectsImmediate();
        }

        private void Update()
        {
            UpdateLightFade();
        }

        public void BeginFistCharge()
        {
            if (!_isReloading)
            {
                return;
            }

            _stopEffectsAfterFade = false;
            SetLightIntensity(0f);
            StartEffects();
            StartLightFade(_peakLightIntensity, _lightFadeInDuration);

            if (!_isReloadWindowActive)
            {
                _isReloadWindowActive = true;
                ReloadWindowStarted?.Invoke(
                    _currentReloadDuration *
                    _reloadWindowNormalizedDuration);
            }
        }

        public void EndFistCharge()
        {
            if (!_effectsActive)
            {
                return;
            }

            _stopEffectsAfterFade = true;
            StartLightFade(0f, _lightFadeOutDuration);
            EndReloadWindow();
        }

        private void HandleReloadStarted(float duration)
        {
            if (_weaponView != null &&
                _weaponView.CurrentView != null &&
                !_weaponView.CurrentView.UseReloadEffects)
            {
                _isReloading = false;
                EndReloadWindow();
                StopEffectsImmediate();
                return;
            }

            _isReloading = true;
            _currentReloadDuration = Mathf.Max(0f, duration);
            EndReloadWindow();
            StopEffectsImmediate();
        }

        private void HandleReloadCompleted()
        {
            _isReloading = false;
            EndReloadWindow();
            StopEffectsImmediate();
        }

        private void EndReloadWindow()
        {
            if (!_isReloadWindowActive)
            {
                return;
            }

            _isReloadWindowActive = false;
            ReloadWindowEnded?.Invoke();
        }

        private void StartEffects()
        {
            if (_effectsActive)
            {
                return;
            }

            _effectsActive = true;
            SetObjectsActive(true);
            SetEffectsEnabled(true);
        }

        private void StopEffectsImmediate()
        {
            _effectsActive = false;
            _isLightFading = false;
            _stopEffectsAfterFade = false;
            SetLightIntensity(0f);
            SetEffectsEnabled(false);
            SetObjectsActive(false);
        }

        private void StartLightFade(float targetIntensity, float duration)
        {
            _lightFadeFrom = _currentLightIntensity;
            _lightFadeTo = Mathf.Max(0f, targetIntensity);
            _lightFadeDuration = Mathf.Max(0f, duration);
            _lightFadeElapsed = 0f;

            if (_lightFadeDuration <= 0f)
            {
                SetLightIntensity(_lightFadeTo);
                CompleteLightFade();
                return;
            }

            _isLightFading = true;
        }

        private void UpdateLightFade()
        {
            if (!_isLightFading)
            {
                return;
            }

            _lightFadeElapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(
                _lightFadeElapsed / _lightFadeDuration);
            float curveValue = _lightFadeCurve == null
                ? normalizedTime
                : _lightFadeCurve.Evaluate(normalizedTime);

            SetLightIntensity(Mathf.LerpUnclamped(
                _lightFadeFrom,
                _lightFadeTo,
                curveValue));

            if (normalizedTime >= 1f)
            {
                CompleteLightFade();
            }
        }

        private void CompleteLightFade()
        {
            _isLightFading = false;

            if (_stopEffectsAfterFade)
            {
                StopEffectsImmediate();
            }
        }

        private void SetLightIntensity(float intensity)
        {
            _currentLightIntensity = Mathf.Max(0f, intensity);

            if (_chargeLights == null)
            {
                return;
            }

            for (int i = 0; i < _chargeLights.Length; i++)
            {
                Light chargeLight = _chargeLights[i];

                if (chargeLight != null)
                {
                    chargeLight.intensity = _currentLightIntensity;
                }
            }
        }

        private void SetObjectsActive(bool isActive)
        {
            if (_reloadObjects == null)
            {
                return;
            }

            for (int i = 0; i < _reloadObjects.Length; i++)
            {
                GameObject reloadObject = _reloadObjects[i];

                if (reloadObject != null && reloadObject != gameObject)
                {
                    reloadObject.SetActive(isActive);
                }
            }
        }

        private void SetEffectsEnabled(bool isEnabled)
        {
            if (_reloadEffects == null)
            {
                return;
            }

            for (int i = 0; i < _reloadEffects.Length; i++)
            {
                SetEffectEnabled(_reloadEffects[i], isEnabled);
            }
        }

        private static void SetEffectEnabled(
            VisualEffect effect,
            bool isEnabled)
        {
            if (effect == null)
            {
                return;
            }

            if (isEnabled)
            {
                effect.enabled = true;
                effect.Reinit();
                effect.Play();
                return;
            }

            effect.Stop();
            effect.enabled = false;
        }

        private void ResolveReferences()
        {
            if (_weaponController == null)
            {
                _weaponController = GetComponent<PlayerWeaponController>();
            }

            if (_weaponView == null)
            {
                _weaponView = GetComponent<PlayerWeaponView>();
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            _peakLightIntensity = Mathf.Max(0f, _peakLightIntensity);
            _reloadWindowNormalizedDuration = Mathf.Clamp01(
                _reloadWindowNormalizedDuration);
            _lightFadeInDuration = Mathf.Max(0f, _lightFadeInDuration);
            _lightFadeOutDuration = Mathf.Max(0f, _lightFadeOutDuration);
        }
    }
}
