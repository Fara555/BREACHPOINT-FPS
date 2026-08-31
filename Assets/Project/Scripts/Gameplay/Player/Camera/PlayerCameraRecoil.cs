using Breachpoint.Gameplay.Weapons;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Camera
{
    public sealed class PlayerCameraRecoil : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Transform _recoilRoot;

        [SerializeField]
        private PlayerWeaponController _weaponController;

        private WeaponConfig _config;

        private Quaternion _baseLocalRotation;
        private Vector2 _currentRecoil;
        private Vector2 _targetRecoil;
        private float _lastShotTime = float.NegativeInfinity;
        private int _burstShotCount;

        [Inject]
        public void Construct(WeaponConfig config)
        {
            _config = config;
        }

        private void Awake()
        {
            ValidateReferences();

            if (_recoilRoot != null)
            {
                _baseLocalRotation = _recoilRoot.localRotation;
            }
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.ShotFired += HandleShotFired;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.ShotFired -= HandleShotFired;
            }

            ResetRecoil();
        }

        private void LateUpdate()
        {
            if (_recoilRoot == null || _config == null)
            {
                return;
            }

            float followWeight = 1f - Mathf.Exp(
                -_config.CameraRecoilSnappiness * Time.deltaTime);

            if (Time.time - _lastShotTime >=
                _config.CameraRecoilRecoveryDelay)
            {
                float returnWeight = 1f - Mathf.Exp(
                    -_config.CameraRecoilReturnSpeed * Time.deltaTime);

                _targetRecoil = Vector2.Lerp(
                    _targetRecoil,
                    Vector2.zero,
                    returnWeight);
            }

            _currentRecoil = Vector2.Lerp(
                _currentRecoil,
                _targetRecoil,
                followWeight);

            _recoilRoot.localRotation =
                _baseLocalRotation *
                Quaternion.Euler(
                    -_currentRecoil.x,
                    _currentRecoil.y,
                    0f);
        }

        private void HandleShotFired(WeaponShotResult result)
        {
            if (Time.time - _lastShotTime >=
                _config.RecoilBurstResetDelay)
            {
                _burstShotCount = 0;
            }

            _lastShotTime = Time.time;
            _burstShotCount++;

            float burstProgress = Mathf.Clamp01(
                (_burstShotCount - 1f) /
                _config.ShotsToMaximumBurstRecoil);

            float burstMultiplier = Mathf.Lerp(
                1f,
                _config.MaximumBurstRecoilMultiplier,
                burstProgress);

            float aimMultiplier =
                _weaponController.IsAiming
                    ? _config.AimRecoilMultiplier
                    : 1f;

            float pitch = Random.Range(
                _config.CameraRecoilPitch -
                _config.CameraRecoilPitchVariation,
                _config.CameraRecoilPitch +
                _config.CameraRecoilPitchVariation);

            pitch = Mathf.Max(0f, pitch) *
                    burstMultiplier *
                    aimMultiplier;

            float yaw = Random.Range(
                -_config.CameraRecoilYaw,
                _config.CameraRecoilYaw) *
                burstMultiplier *
                aimMultiplier;

            _targetRecoil.x = Mathf.Min(
                _targetRecoil.x + pitch,
                _config.MaximumCameraRecoil);

            _targetRecoil.y = Mathf.Clamp(
                _targetRecoil.y + yaw,
                -_config.MaximumHorizontalCameraRecoil,
                _config.MaximumHorizontalCameraRecoil);
        }

        private void ResetRecoil()
        {
            _currentRecoil = Vector2.zero;
            _targetRecoil = Vector2.zero;
            _lastShotTime = float.NegativeInfinity;
            _burstShotCount = 0;

            if (_recoilRoot != null)
            {
                _recoilRoot.localRotation = _baseLocalRotation;
            }
        }

        private void ValidateReferences()
        {
            if (_recoilRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraRecoil)} requires a RecoilRoot reference.",
                    this);
            }

            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraRecoil)} requires a PlayerWeaponController reference.",
                    this);
            }
        }
    }
}
