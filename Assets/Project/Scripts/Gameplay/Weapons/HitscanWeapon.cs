using System;
using Breachpoint.Gameplay.Combat;
using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class HitscanWeapon : MonoBehaviour
    {
        private const float HitDistancePadding = 0.05f;

        [Header("References")]
        [SerializeField]
        private Camera _aimCamera;

        [SerializeField]
        private Transform _muzzle;

        [SerializeField]
        private GameObject _damageSource;

        [SerializeField]
        private PlayerWeaponController _weaponController;

        [SerializeField]
        private PlayerWeaponView _weaponView;

        private WeaponConfig Config => _weaponController != null
            ? _weaponController.CurrentConfig
            : null;

        private Transform Muzzle =>
            _weaponView != null &&
            _weaponView.CurrentView != null &&
            _weaponView.CurrentView.Muzzle != null
                ? _weaponView.CurrentView.Muzzle
                : _muzzle;

        public event Action<WeaponShotResult> ShotResolved;

        private void Awake()
        {
            if (_weaponController == null)
            {
                _weaponController = GetComponent<PlayerWeaponController>();
            }

            if (_weaponView == null)
            {
                _weaponView = GetComponent<PlayerWeaponView>();
            }

            ValidateReferences();
        }

        public void Fire(bool isAiming)
        {
            if (!CanFire())
            {
                return;
            }

            Vector3 cameraDirection = ApplySpread(
                _aimCamera.transform.forward,
                isAiming);

            Vector3 targetPoint = GetCameraTargetPoint(
                cameraDirection);

            WeaponConfig config = Config;
            Transform muzzle = Muzzle;
            Vector3 muzzleOrigin = muzzle.position;
            Vector3 muzzleDirection = targetPoint - muzzleOrigin;
            float targetDistance = Mathf.Min(
                muzzleDirection.magnitude,
                config.Range);

            if (targetDistance <= Mathf.Epsilon)
            {
                return;
            }

            muzzleDirection /= muzzleDirection.magnitude;
            float raycastDistance = Mathf.Min(
                targetDistance + HitDistancePadding,
                config.Range);

            if (Physics.Raycast(
                    muzzleOrigin,
                    muzzleDirection,
                    out RaycastHit hit,
                    raycastDistance,
                    config.HitMask,
                    QueryTriggerInteraction.Ignore))
            {
                ApplyDamage(hit, muzzleDirection);

                ShotResolved?.Invoke(
                    new WeaponShotResult(
                        muzzleOrigin,
                        hit.point,
                        hit.normal,
                        hit.collider));

                return;
            }

            ShotResolved?.Invoke(
                new WeaponShotResult(
                    muzzleOrigin,
                    muzzleOrigin + muzzleDirection * targetDistance,
                    Vector3.zero,
                    null));
        }

        private Vector3 GetCameraTargetPoint(Vector3 direction)
        {
            Vector3 origin = _aimCamera.transform.position;

            if (Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    Config.Range,
                    Config.HitMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return origin + direction * Config.Range;
        }

        private Vector3 ApplySpread(
            Vector3 forward,
            bool isAiming)
        {
            float spreadAngle = isAiming
                ? Config.AimSpreadAngle
                : Config.HipSpreadAngle;

            if (spreadAngle <= 0f)
            {
                return forward;
            }

            Vector2 spread = UnityEngine.Random.insideUnitCircle *
                             Mathf.Tan(spreadAngle * Mathf.Deg2Rad);

            Transform cameraTransform = _aimCamera.transform;

            return (forward +
                    cameraTransform.right * spread.x +
                    cameraTransform.up * spread.y).normalized;
        }

        private void ApplyDamage(
            RaycastHit hit,
            Vector3 direction)
        {
            IDamageable damageable =
                hit.collider.GetComponentInParent<IDamageable>();

            if (damageable == null)
            {
                return;
            }

            GameObject source = _damageSource != null
                ? _damageSource
                : gameObject;

            damageable.TakeDamage(
                new DamageInfo(
                    Config.Damage,
                    hit.point,
                    direction,
                    source));
        }

        private bool CanFire()
        {
            return
                Config != null &&
                _aimCamera != null &&
                Muzzle != null;
        }

        private void ValidateReferences()
        {
            if (_aimCamera == null)
            {
                Debug.LogError(
                    $"{nameof(HitscanWeapon)} requires an AimCamera reference.",
                    this);
            }

            if (_muzzle == null)
            {
                Debug.LogError(
                    $"{nameof(HitscanWeapon)} requires a Muzzle reference.",
                    this);
            }

            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(HitscanWeapon)} requires a PlayerWeaponController reference.",
                    this);
            }
        }
    }
}
