using System;
using Breachpoint.Gameplay.Combat;
using UnityEngine;
using VContainer;

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

        private WeaponConfig _config;

        public event Action<WeaponShotResult> ShotResolved;

        [Inject]
        public void Construct(WeaponConfig config)
        {
            _config = config;
        }

        private void Awake()
        {
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

            Vector3 muzzleOrigin = _muzzle.position;
            Vector3 muzzleDirection = targetPoint - muzzleOrigin;
            float targetDistance = Mathf.Min(
                muzzleDirection.magnitude,
                _config.Range);

            if (targetDistance <= Mathf.Epsilon)
            {
                return;
            }

            muzzleDirection /= muzzleDirection.magnitude;
            float raycastDistance = Mathf.Min(
                targetDistance + HitDistancePadding,
                _config.Range);

            if (Physics.Raycast(
                    muzzleOrigin,
                    muzzleDirection,
                    out RaycastHit hit,
                    raycastDistance,
                    _config.HitMask,
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
                    _config.Range,
                    _config.HitMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return origin + direction * _config.Range;
        }

        private Vector3 ApplySpread(
            Vector3 forward,
            bool isAiming)
        {
            float spreadAngle = isAiming
                ? _config.AimSpreadAngle
                : _config.HipSpreadAngle;

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
                    _config.Damage,
                    hit.point,
                    direction,
                    source));
        }

        private bool CanFire()
        {
            return
                _config != null &&
                _aimCamera != null &&
                _muzzle != null;
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
        }
    }
}
