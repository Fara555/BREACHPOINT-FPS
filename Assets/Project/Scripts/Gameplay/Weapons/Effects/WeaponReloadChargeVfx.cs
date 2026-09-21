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

        [Tooltip("Effects used for the fist aura before energy transfer begins.")]
        [SerializeField]
        private VisualEffect[] _handGlowEffects;

        [Tooltip("Effects used while energy flows from the hand into the magazine.")]
        [SerializeField]
        private VisualEffect[] _energyTransferEffects;

        [SerializeField]
        private WeaponEnergyArcVfx[] _energyArcEffects;

        [SerializeField]
        private WeaponEnergyShellVfx[] _handEnergyShellEffects;

        [Tooltip("Secondary particles that build volume around the charging hand.")]
        [SerializeField]
        private WeaponEnergyMoteVfx[] _ambientEnergyEffects;

        [SerializeField]
        private GameObject[] _reloadObjects;

        [SerializeField]
        private GameObject[] _handGlowObjects;

        [SerializeField]
        private GameObject[] _energyTransferObjects;

        [Tooltip("Objects that contain the optional hand light shared by reload styles.")]
        [SerializeField]
        private GameObject[] _chargeLightObjects;

        [SerializeField]
        private Light[] _chargeLights;

        [Header("Reload window")]
        [Tooltip("Fraction of the complete reload occupied by the charge phase.")]
        [SerializeField, Range(0f, 1f)]
        private float _reloadWindowNormalizedDuration = 0.423f;

        [Header("Fist light")]
        [SerializeField, Min(0f)]
        private float _peakLightIntensity = 9000000f;

        [Tooltip("Light color used by the dedicated SP6 hand aura.")]
        [SerializeField]
        private Color _handGlowLightColor = new Color(
            0.025f,
            0.34f,
            1f,
            1f);

        [Tooltip("Peak light intensity used by the dedicated SP6 hand aura.")]
        [SerializeField, Min(0f)]
        private float _handGlowPeakLightIntensity = 550000f;

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
        private Color[] _defaultLightColors;
        private bool _isReloading;
        private bool _effectsActive;
        private bool _isReloadWindowActive;
        private bool _isLightFading;
        private bool _stopEffectsAfterFade;

        private void Awake()
        {
            ResolveReferences();
            CacheDefaultLightColors();
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
            RestoreDefaultLightColors();
            SetObjectsActive(_chargeLightObjects, true);
            SetLightIntensity(0f);
            StartEffects(_reloadEffects, _reloadObjects);
            StartLightFade(_peakLightIntensity, _lightFadeInDuration);

            StartReloadWindow();
        }

        public void BeginFistGlow()
        {
            if (!_isReloading)
            {
                return;
            }

            _stopEffectsAfterFade = false;
            SetLightColor(_handGlowLightColor);
            SetObjectsActive(_chargeLightObjects, true);
            SetLightIntensity(0f);
            StartEffects(_handGlowEffects, _handGlowObjects);
            PlayHandEnergyShells(_lightFadeInDuration);
            PlayAmbientEnergyEffects(_lightFadeInDuration);
            StartLightFade(
                _handGlowPeakLightIntensity,
                _lightFadeInDuration);
        }

        public void BeginEnergyTransfer()
        {
            if (!_isReloading)
            {
                return;
            }

            StartEffects(
                _energyTransferEffects,
                _energyTransferObjects);
            PlayEnergyArcs();
            StartReloadWindow();
        }

        public void CompleteEnergyTransfer()
        {
            if (!_effectsActive)
            {
                return;
            }

            EndReloadWindow();
            FadeOutEnergyArcs(_lightFadeOutDuration);
            FadeOutHandEnergyShells(_lightFadeOutDuration);
            FadeOutAmbientEnergyEffects(_lightFadeOutDuration);
            _stopEffectsAfterFade = true;
            StartLightFade(0f, _lightFadeOutDuration);
        }

        public void EndFistChargeImmediate()
        {
            EndReloadWindow();
            StopEffectsImmediate();
        }

        private void StartReloadWindow()
        {
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
            FadeOutHandEnergyShells(_lightFadeOutDuration);
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

        private void StartEffects(
            VisualEffect[] effects,
            GameObject[] effectObjects)
        {
            if (!_effectsActive)
            {
                _effectsActive = true;
            }

            SetObjectsActive(effectObjects, true);
            SetEffectsEnabled(effects, true);
        }

        private void StopEffectsImmediate()
        {
            _effectsActive = false;
            _isLightFading = false;
            _stopEffectsAfterFade = false;
            SetLightIntensity(0f);
            RestoreDefaultLightColors();
            SetEffectsEnabled(_reloadEffects, false);
            SetEffectsEnabled(_handGlowEffects, false);
            SetEffectsEnabled(_energyTransferEffects, false);
            StopEnergyArcs();
            StopHandEnergyShells();
            StopAmbientEnergyEffects();
            SetObjectsActive(_reloadObjects, false);
            SetObjectsActive(_handGlowObjects, false);
            SetObjectsActive(_energyTransferObjects, false);
            SetObjectsActive(_chargeLightObjects, false);
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

        private void SetObjectsActive(
            GameObject[] effectObjects,
            bool isActive)
        {
            if (effectObjects == null)
            {
                return;
            }

            for (int i = 0; i < effectObjects.Length; i++)
            {
                GameObject effectObject = effectObjects[i];

                if (effectObject != null && effectObject != gameObject)
                {
                    effectObject.SetActive(isActive);
                }
            }
        }

        private static void SetEffectsEnabled(
            VisualEffect[] effects,
            bool isEnabled)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                SetEffectEnabled(effects[i], isEnabled);
            }
        }

        private void PlayEnergyArcs()
        {
            if (_energyArcEffects == null)
            {
                return;
            }

            for (int i = 0; i < _energyArcEffects.Length; i++)
            {
                WeaponEnergyArcVfx energyArc = _energyArcEffects[i];

                if (energyArc != null)
                {
                    energyArc.PlayEffect();
                }
            }
        }

        private void FadeOutEnergyArcs(float duration)
        {
            if (_energyArcEffects == null)
            {
                return;
            }

            for (int i = 0; i < _energyArcEffects.Length; i++)
            {
                WeaponEnergyArcVfx energyArc = _energyArcEffects[i];

                if (energyArc != null)
                {
                    energyArc.BeginFadeOut(duration);
                }
            }
        }

        private void StopEnergyArcs()
        {
            if (_energyArcEffects == null)
            {
                return;
            }

            for (int i = 0; i < _energyArcEffects.Length; i++)
            {
                WeaponEnergyArcVfx energyArc = _energyArcEffects[i];

                if (energyArc != null)
                {
                    energyArc.StopEffect();
                }
            }
        }

        private void PlayHandEnergyShells(float duration)
        {
            if (_handEnergyShellEffects == null)
            {
                return;
            }

            for (int i = 0; i < _handEnergyShellEffects.Length; i++)
            {
                WeaponEnergyShellVfx energyShell =
                    _handEnergyShellEffects[i];

                if (energyShell != null)
                {
                    energyShell.PlayEffect(duration);
                }
            }
        }

        private void FadeOutHandEnergyShells(float duration)
        {
            if (_handEnergyShellEffects == null)
            {
                return;
            }

            for (int i = 0; i < _handEnergyShellEffects.Length; i++)
            {
                WeaponEnergyShellVfx energyShell =
                    _handEnergyShellEffects[i];

                if (energyShell != null)
                {
                    energyShell.BeginFadeOut(duration);
                }
            }
        }

        private void StopHandEnergyShells()
        {
            if (_handEnergyShellEffects == null)
            {
                return;
            }

            for (int i = 0; i < _handEnergyShellEffects.Length; i++)
            {
                WeaponEnergyShellVfx energyShell =
                    _handEnergyShellEffects[i];

                if (energyShell != null)
                {
                    energyShell.StopEffect();
                }
            }
        }

        private void PlayAmbientEnergyEffects(float duration)
        {
            if (_ambientEnergyEffects == null)
            {
                return;
            }

            for (int i = 0; i < _ambientEnergyEffects.Length; i++)
            {
                WeaponEnergyMoteVfx ambientEffect =
                    _ambientEnergyEffects[i];

                if (ambientEffect != null)
                {
                    ambientEffect.PlayEffect(duration);
                }
            }
        }

        private void FadeOutAmbientEnergyEffects(float duration)
        {
            if (_ambientEnergyEffects == null)
            {
                return;
            }

            for (int i = 0; i < _ambientEnergyEffects.Length; i++)
            {
                WeaponEnergyMoteVfx ambientEffect =
                    _ambientEnergyEffects[i];

                if (ambientEffect != null)
                {
                    ambientEffect.BeginFadeOut(duration);
                }
            }
        }

        private void StopAmbientEnergyEffects()
        {
            if (_ambientEnergyEffects == null)
            {
                return;
            }

            for (int i = 0; i < _ambientEnergyEffects.Length; i++)
            {
                WeaponEnergyMoteVfx ambientEffect =
                    _ambientEnergyEffects[i];

                if (ambientEffect != null)
                {
                    ambientEffect.StopEffect();
                }
            }
        }

        private void CacheDefaultLightColors()
        {
            if (_chargeLights == null)
            {
                _defaultLightColors = null;
                return;
            }

            _defaultLightColors = new Color[_chargeLights.Length];

            for (int i = 0; i < _chargeLights.Length; i++)
            {
                Light chargeLight = _chargeLights[i];

                if (chargeLight != null)
                {
                    _defaultLightColors[i] = chargeLight.color;
                }
            }
        }

        private void RestoreDefaultLightColors()
        {
            if (_chargeLights == null || _defaultLightColors == null)
            {
                return;
            }

            int lightCount = Mathf.Min(
                _chargeLights.Length,
                _defaultLightColors.Length);

            for (int i = 0; i < lightCount; i++)
            {
                Light chargeLight = _chargeLights[i];

                if (chargeLight != null)
                {
                    chargeLight.color = _defaultLightColors[i];
                }
            }
        }

        private void SetLightColor(Color color)
        {
            if (_chargeLights == null)
            {
                return;
            }

            for (int i = 0; i < _chargeLights.Length; i++)
            {
                Light chargeLight = _chargeLights[i];

                if (chargeLight != null)
                {
                    chargeLight.color = color;
                }
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
            _handGlowPeakLightIntensity = Mathf.Max(
                0f,
                _handGlowPeakLightIntensity);
            _reloadWindowNormalizedDuration = Mathf.Clamp01(
                _reloadWindowNormalizedDuration);
            _lightFadeInDuration = Mathf.Max(0f, _lightFadeInDuration);
            _lightFadeOutDuration = Mathf.Max(0f, _lightFadeOutDuration);
        }
    }
}
