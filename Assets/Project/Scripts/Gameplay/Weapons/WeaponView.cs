using Breachpoint.Gameplay.Weapons.Effects;
using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class WeaponView : MonoBehaviour
    {
        [Header("Weapon")]
        [SerializeField]
        private WeaponConfig _config;

        [SerializeField]
        private GameObject _visualRoot;

        [SerializeField]
        private Animator _weaponAnimator;

        [SerializeField]
        private WeaponViewAnimationSet _animations;

        [SerializeField]
        private Transform _muzzle;

        [SerializeField]
        private WeaponMuzzleFlash _muzzleFlashEffect;

        [SerializeField]
        private bool _useReloadEffects = true;

        [Header("Aiming")]
        [SerializeField]
        private bool _overrideAimPose;

        [SerializeField]
        private Vector3 _aimLocalPosition;

        [SerializeField]
        private Vector3 _aimLocalRotation;

        [Header("Procedural Motion")]
        [SerializeField]
        private WeaponMotionConfig _motionConfig;

        [Header("High Ready")]
        [SerializeField]
        private Vector3 _highReadyPositionOffset =
            new Vector3(0f, 0.1f, -0.18f);

        [SerializeField]
        private Vector3 _highReadyRotationOffset =
            new Vector3(-55f, 0f, 0f);

        [SerializeField, Min(0f)]
        private float _obstructionCheckDistance = 0.9f;

        [SerializeField, Min(0f)]
        private float _obstructionCastRadius = 0.12f;

        [SerializeField, Min(0.01f)]
        private float _obstructionBlendDistance = 0.4f;

        public WeaponConfig Config => _config;
        public Animator WeaponAnimator => _weaponAnimator;
        public WeaponViewAnimationSet Animations => _animations;
        public Transform Muzzle => _muzzle;
        public WeaponMuzzleFlash MuzzleFlashEffect => _muzzleFlashEffect;
        public bool UseReloadEffects => _useReloadEffects;
        public bool OverrideAimPose => _overrideAimPose;
        public Vector3 AimLocalPosition => _aimLocalPosition;
        public Quaternion AimLocalRotation =>
            Quaternion.Euler(_aimLocalRotation);
        public WeaponMotionConfig MotionConfig => _motionConfig;
        public Vector3 HighReadyPositionOffset =>
            _highReadyPositionOffset;
        public Vector3 HighReadyRotationOffset =>
            _highReadyRotationOffset;
        public float ObstructionCheckDistance =>
            _obstructionCheckDistance;
        public float ObstructionCastRadius =>
            _obstructionCastRadius;
        public float ObstructionBlendDistance =>
            _obstructionBlendDistance;

        public void SetVisible(bool isVisible)
        {
            GameObject visualRoot = _visualRoot != null
                ? _visualRoot
                : gameObject;

            visualRoot.SetActive(isVisible);
        }

        private void Awake()
        {
            ResolveReferences();
            ValidateReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ValidateReferences();
        }

        private void ResolveReferences()
        {
            if (_visualRoot == null)
            {
                _visualRoot = gameObject;
            }

            if (_weaponAnimator == null)
            {
                _weaponAnimator =
                    _visualRoot.GetComponentInChildren<Animator>(true);
            }
        }

        private void ValidateReferences()
        {
            if (_weaponAnimator == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponView)} requires a WeaponAnimator reference.",
                    this);
            }

            if (_animations == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponView)} requires a view animation set.",
                    this);
            }

            if (_muzzle == null)
            {
                Debug.LogWarning(
                    $"{nameof(WeaponView)} on {name} has no Muzzle reference; " +
                    "the shared weapon muzzle will be used.",
                    this);
            }
        }
    }
}
