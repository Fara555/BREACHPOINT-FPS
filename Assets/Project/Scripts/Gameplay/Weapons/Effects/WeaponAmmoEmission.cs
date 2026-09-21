using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponAmmoEmission : MonoBehaviour
    {
        private static readonly int EmissiveColorId =
            Shader.PropertyToID("_EmissiveColor");

        [Header("References")]
        [SerializeField]
        private PlayerWeaponController _weaponController;

        [FormerlySerializedAs("_reloadChargeVfx")]
        [Tooltip("Component that reports the active visual phase of reloading.")]
        [SerializeField]
        private MonoBehaviour _reloadWindowSourceComponent;

        [Tooltip("Material whose emission represents the loaded ammunition.")]
        [SerializeField]
        private Material _accentMaterial;

        [Tooltip("Renderer name prefix used to discover ordered magazine charge segments.")]
        [SerializeField]
        private string _reloadSegmentNamePrefix = "Magazine_Charge_";

        [Tooltip("Reverses the discovered numeric segment order when a weapon is authored bottom-to-top.")]
        [SerializeField]
        private bool _reverseReloadSegmentOrder;

        [Header("Transitions")]
        [Tooltip("Time used to smoothly reach the next ammunition level after a shot.")]
        [SerializeField, Min(0f)]
        private float _shotFadeDuration = 0.08f;

        [SerializeField]
        private AnimationCurve _reloadFillCurve = AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f);

        private readonly List<EmissionTarget> _weaponTargets = new();
        private readonly List<EmissionSegment> _reloadSegments = new();
        private MaterialPropertyBlock _propertyBlock;
        private IWeaponReloadWindowSource _reloadWindowSource;

        private Vector4 _fullEmissionColor;
        private float _currentLevel;
        private float _transitionFrom;
        private float _transitionTo;
        private float _transitionDuration;
        private float _transitionElapsed;
        private bool _isTransitioning;
        private bool _isReloadFilling;

        private sealed class EmissionTarget
        {
            public EmissionTarget(Renderer renderer, int materialIndex)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
            }

            public Renderer Renderer { get; }
            public int MaterialIndex { get; }
        }

        private sealed class EmissionSegment
        {
            public EmissionSegment(EmissionTarget target, int order)
            {
                Target = target;
                Order = order;
            }

            public EmissionTarget Target { get; }
            public int Order { get; }
        }

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            ResolveReferences();
            CacheEmissionTargets();
            ValidateConfiguration();
        }

        private void OnEnable()
        {
            Subscribe();
            SynchronizeImmediate();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _isTransitioning = false;
            _isReloadFilling = false;
        }

        private void Update()
        {
            if (!_isTransitioning)
            {
                return;
            }

            _transitionElapsed += Time.deltaTime;
            float normalizedTime = _transitionDuration > 0f
                ? Mathf.Clamp01(_transitionElapsed / _transitionDuration)
                : 1f;

            float interpolation = _isReloadFilling
                ? EvaluateReloadCurve(normalizedTime)
                : Mathf.SmoothStep(0f, 1f, normalizedTime);

            ApplyLevel(Mathf.LerpUnclamped(
                _transitionFrom,
                _transitionTo,
                interpolation));

            if (normalizedTime >= 1f)
            {
                _isTransitioning = false;
            }
        }

        private void HandleAmmunitionChanged(int magazine)
        {
            if (_isReloadFilling)
            {
                return;
            }

            StartTransition(
                CalculateLevel(magazine),
                _shotFadeDuration,
                false);
        }

        private void HandleReloadWindowStarted(float duration)
        {
            if (_weaponController == null)
            {
                return;
            }

            StartTransition(
                1f,
                duration,
                true);
        }

        private void HandleReloadWindowEnded()
        {
            if (!_isReloadFilling)
            {
                return;
            }

            ApplyLevel(_transitionTo);
            _isTransitioning = false;
            _isReloadFilling = false;
        }

        private void HandleReloadCompleted()
        {
            _isReloadFilling = false;

            if (_weaponController != null)
            {
                ApplyLevel(CalculateLevel(_weaponController.Magazine));
            }
        }

        private void StartTransition(
            float targetLevel,
            float duration,
            bool isReloadFilling)
        {
            _transitionFrom = _currentLevel;
            _transitionTo = Mathf.Clamp01(targetLevel);
            _transitionDuration = Mathf.Max(0f, duration);
            _transitionElapsed = 0f;
            _isReloadFilling = isReloadFilling;

            if (_transitionDuration <= 0f ||
                Mathf.Approximately(_transitionFrom, _transitionTo))
            {
                ApplyLevel(_transitionTo);
                _isTransitioning = false;
                return;
            }

            _isTransitioning = true;
        }

        private float CalculateLevel(int magazine)
        {
            if (_weaponController == null ||
                _weaponController.MagazineSize <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                (float)magazine / _weaponController.MagazineSize);
        }

        private float EvaluateReloadCurve(float normalizedTime)
        {
            return _reloadFillCurve == null
                ? normalizedTime
                : _reloadFillCurve.Evaluate(normalizedTime);
        }

        private void ApplyLevel(float level)
        {
            _currentLevel = Mathf.Clamp01(level);

            for (int i = 0; i < _weaponTargets.Count; i++)
            {
                ApplyEmission(_weaponTargets[i], _currentLevel);
            }

            int segmentCount = _reloadSegments.Count;

            if (segmentCount == 0)
            {
                return;
            }

            float scaledLevel = _currentLevel * segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                // Each segment owns an equal part of the fill interval. This
                // preserves a smooth transition while keeping the top-to-bottom
                // charging sequence clearly readable.
                float segmentLevel = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(scaledLevel - i));

                ApplyEmission(_reloadSegments[i].Target, segmentLevel);
            }
        }

        private void ApplyEmission(EmissionTarget target, float level)
        {
            if (target.Renderer == null)
            {
                return;
            }

            float clampedLevel = Mathf.Clamp01(level);
            Vector4 emissionColor = new(
                _fullEmissionColor.x * clampedLevel,
                _fullEmissionColor.y * clampedLevel,
                _fullEmissionColor.z * clampedLevel,
                _fullEmissionColor.w);

            _propertyBlock.Clear();
            target.Renderer.GetPropertyBlock(
                _propertyBlock,
                target.MaterialIndex);
            _propertyBlock.SetVector(EmissiveColorId, emissionColor);
            target.Renderer.SetPropertyBlock(
                _propertyBlock,
                target.MaterialIndex);
        }

        private void SynchronizeImmediate()
        {
            if (_weaponController == null ||
                (_weaponTargets.Count == 0 && _reloadSegments.Count == 0))
            {
                return;
            }

            _isTransitioning = false;
            _isReloadFilling = false;
            ApplyLevel(CalculateLevel(_weaponController.Magazine));
        }

        private void CacheEmissionTargets()
        {
            _weaponTargets.Clear();
            _reloadSegments.Clear();

            if (_accentMaterial == null)
            {
                return;
            }

            _fullEmissionColor = _accentMaterial.HasProperty(EmissiveColorId)
                ? _accentMaterial.GetVector(EmissiveColorId)
                : Vector4.zero;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    if (materials[materialIndex] == _accentMaterial)
                    {
                        EmissionTarget target = new(
                            renderer,
                            materialIndex);

                        if (TryGetReloadSegmentOrder(
                            renderer.name,
                            out int segmentOrder))
                        {
                            _reloadSegments.Add(new EmissionSegment(
                                target,
                                segmentOrder));
                        }
                        else
                        {
                            _weaponTargets.Add(target);
                        }
                    }
                }
            }

            _reloadSegments.Sort(CompareSegments);

            if (_reverseReloadSegmentOrder)
            {
                _reloadSegments.Reverse();
            }
        }

        private bool TryGetReloadSegmentOrder(
            string rendererName,
            out int order)
        {
            order = 0;

            if (string.IsNullOrWhiteSpace(_reloadSegmentNamePrefix) ||
                string.IsNullOrEmpty(rendererName) ||
                !rendererName.StartsWith(
                    _reloadSegmentNamePrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string orderText = rendererName.Substring(
                _reloadSegmentNamePrefix.Length);

            return int.TryParse(orderText, out order);
        }

        private static int CompareSegments(
            EmissionSegment left,
            EmissionSegment right)
        {
            return left.Order.CompareTo(right.Order);
        }

        private void Subscribe()
        {
            if (_weaponController != null)
            {
                _weaponController.AmmunitionChanged +=
                    HandleAmmunitionChanged;
                _weaponController.ReloadCompleted +=
                    HandleReloadCompleted;
            }

            if (_reloadWindowSource != null)
            {
                _reloadWindowSource.ReloadWindowStarted +=
                    HandleReloadWindowStarted;
                _reloadWindowSource.ReloadWindowEnded +=
                    HandleReloadWindowEnded;
            }
        }

        private void Unsubscribe()
        {
            if (_weaponController != null)
            {
                _weaponController.AmmunitionChanged -=
                    HandleAmmunitionChanged;
                _weaponController.ReloadCompleted -=
                    HandleReloadCompleted;
            }

            if (_reloadWindowSource != null)
            {
                _reloadWindowSource.ReloadWindowStarted -=
                    HandleReloadWindowStarted;
                _reloadWindowSource.ReloadWindowEnded -=
                    HandleReloadWindowEnded;
            }
        }

        private void ResolveReferences()
        {
            if (_weaponController == null)
            {
                _weaponController =
                    GetComponentInParent<PlayerWeaponController>(true);
            }

            _reloadWindowSource =
                _reloadWindowSourceComponent as IWeaponReloadWindowSource;

            if (_reloadWindowSource == null)
            {
                MonoBehaviour[] behaviours =
                    GetComponentsInParent<MonoBehaviour>(true);

                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is not IWeaponReloadWindowSource source)
                    {
                        continue;
                    }

                    _reloadWindowSourceComponent = behaviours[i];
                    _reloadWindowSource = source;
                    break;
                }
            }
        }

        private void ValidateConfiguration()
        {
            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponAmmoEmission)} requires a " +
                    $"{nameof(PlayerWeaponController)} reference.",
                    this);
            }

            if (_reloadWindowSource == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponAmmoEmission)} requires a " +
                    $"component that implements " +
                    $"{nameof(IWeaponReloadWindowSource)}.",
                    this);
            }

            if (_accentMaterial == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponAmmoEmission)} requires an Accent Material.",
                    this);
                return;
            }

            if (!_accentMaterial.HasProperty(EmissiveColorId))
            {
                Debug.LogError(
                    $"Material {_accentMaterial.name} does not contain " +
                    "an _EmissiveColor property.",
                    this);
            }

            if (_weaponTargets.Count == 0 && _reloadSegments.Count == 0)
            {
                Debug.LogError(
                    $"{nameof(WeaponAmmoEmission)} could not find " +
                    $"{_accentMaterial.name} on a child Renderer.",
                    this);
            }

            if (!string.IsNullOrWhiteSpace(_reloadSegmentNamePrefix) &&
                _reloadSegments.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(WeaponAmmoEmission)} could not find ordered " +
                    $"reload segments using prefix " +
                    $"'{_reloadSegmentNamePrefix}'.",
                    this);
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            _shotFadeDuration = Mathf.Max(0f, _shotFadeDuration);
        }
    }
}
