using Breachpoint.Audio;
using Breachpoint.Gameplay.Weapons.Effects;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class WeaponPresentation : MonoBehaviour
    {
        private const float ReloadPoseReadyThreshold = 0.01f;

        [Header("References")]
        [SerializeField]
        private PlayerWeaponController _weaponController;

        [SerializeField]
        private Transform _visualRoot;

        [SerializeField]
        private Transform _muzzle;

        [Header("Aiming")]
        [SerializeField]
        private Transform _aimPose;

        [SerializeField]
        private Camera _aimCamera;

        [Header("Visual Effects")]
        [SerializeField]
        private WeaponMuzzleFlash _muzzleFlashEffect;

        [SerializeField]
        private WeaponProjectileTracerPool _tracerPool;

        [SerializeField]
        private WeaponImpactSparkEffect _impactSparkEffect;

        [SerializeField]
        private BulletImpactPool _impactPool;

        [Header("Audio")]
        [SerializeField]
        private AudioCue _shotCue;

        [SerializeField]
        private AudioCue _reloadCue;

        private IAudioService _audioService;
        private WeaponConfig _config;

        private Vector3 _hipLocalPosition;
        private Quaternion _hipLocalRotation;
        private float _hipFieldOfView;
        private float _aimWeight;
        private float _currentKick;
        private float _targetKick;

        [Inject]
        public void Construct(
            IAudioService audioService,
            WeaponConfig config)
        {
            _audioService = audioService;
            _config = config;
        }

        private void Awake()
        {
            ResolveOptionalEffects();
            ValidateReferences();

            if (_visualRoot != null)
            {
                _hipLocalPosition = _visualRoot.localPosition;
                _hipLocalRotation = _visualRoot.localRotation;
            }

            if (_aimCamera != null)
            {
                _hipFieldOfView = _aimCamera.fieldOfView;
            }

        }

        private void ResolveOptionalEffects()
        {
            if (_muzzleFlashEffect == null && _muzzle != null)
            {
                _muzzleFlashEffect =
                    _muzzle.GetComponentInChildren<WeaponMuzzleFlash>(true);
            }
        }

        private void OnEnable()
        {
            if (_weaponController == null)
            {
                return;
            }

            _weaponController.ShotFired += HandleShotFired;
            _weaponController.ReloadStarted += HandleReloadStarted;
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.ShotFired -= HandleShotFired;
                _weaponController.ReloadStarted -= HandleReloadStarted;
            }

            ResetPresentation();
        }

        private void LateUpdate()
        {
            UpdateAimAndVisualRecoil();
            ReportReloadPresentationReady();
        }

        private void HandleShotFired(WeaponShotResult result)
        {
            _targetKick = 1f;

            if (_muzzleFlashEffect != null)
            {
                _muzzleFlashEffect.Play();
            }

            if (_tracerPool != null)
            {
                _tracerPool.Play(
                    result.Origin,
                    result.EndPoint);
            }

            if (_impactPool != null)
            {
                _impactPool.Play(result);
            }

            if (_impactSparkEffect != null)
            {
                _impactSparkEffect.Play(result);
            }

            if (_audioService != null && _shotCue != null)
            {
                _audioService.Play(_shotCue, _muzzle);
            }
        }

        private void HandleReloadStarted(float duration)
        {
            ResetPresentation();

            if (_audioService != null && _reloadCue != null)
            {
                _audioService.Play(_reloadCue, _visualRoot);
            }
        }

        private void ReportReloadPresentationReady()
        {
            if (_weaponController == null)
            {
                return;
            }

            bool isReady =
                _aimWeight <= ReloadPoseReadyThreshold &&
                Mathf.Abs(_currentKick) <= ReloadPoseReadyThreshold &&
                Mathf.Abs(_targetKick) <= ReloadPoseReadyThreshold;

            _weaponController.ReportReloadPresentationReady(isReady);
        }

        private void UpdateAimAndVisualRecoil()
        {
            if (_visualRoot == null ||
                _aimPose == null ||
                _config == null)
            {
                return;
            }

            float aimTarget =
                _weaponController != null &&
                _weaponController.IsAiming
                    ? 1f
                    : 0f;

            float aimWeight = 1f - Mathf.Exp(
                -_config.AimTransitionSpeed * Time.deltaTime);

            _aimWeight = Mathf.Lerp(
                _aimWeight,
                aimTarget,
                aimWeight);

            float returnWeight = 1f - Mathf.Exp(
                -_config.VisualRecoilReturnSpeed * Time.deltaTime);

            float followWeight = 1f - Mathf.Exp(
                -_config.VisualRecoilSnappiness * Time.deltaTime);

            _targetKick = Mathf.Lerp(
                _targetKick,
                0f,
                returnWeight);

            _currentKick = Mathf.Lerp(
                _currentKick,
                _targetKick,
                followWeight);

            Vector3 posePosition = Vector3.Lerp(
                _hipLocalPosition,
                _aimPose.localPosition,
                _aimWeight);

            Quaternion poseRotation = Quaternion.Slerp(
                _hipLocalRotation,
                _aimPose.localRotation,
                _aimWeight);

            float kickDistanceMultiplier = Mathf.Lerp(
                1f,
                _config.AimVisualKickDistanceMultiplier,
                _aimWeight);

            float kickPitchMultiplier = Mathf.Lerp(
                1f,
                _config.AimVisualKickPitchMultiplier,
                _aimWeight);

            _visualRoot.localPosition =
                posePosition +
                Vector3.back *
                (_config.VisualKickDistance *
                 kickDistanceMultiplier *
                 _currentKick);

            _visualRoot.localRotation =
                poseRotation *
                Quaternion.Euler(
                    -_config.VisualKickPitch *
                    kickPitchMultiplier *
                    _currentKick,
                    0f,
                    0f);

            if (_aimCamera != null)
            {
                float aimFieldOfView =
                    _hipFieldOfView *
                    _config.AimFieldOfViewMultiplier;

                _aimCamera.fieldOfView = Mathf.Lerp(
                    _hipFieldOfView,
                    aimFieldOfView,
                    _aimWeight);
            }
        }

        private void ResetPresentation()
        {
            _currentKick = 0f;
            _targetKick = 0f;
            _aimWeight = 0f;

            if (_visualRoot != null)
            {
                _visualRoot.localPosition = _hipLocalPosition;
                _visualRoot.localRotation = _hipLocalRotation;
            }

            if (_aimCamera != null)
            {
                _aimCamera.fieldOfView = _hipFieldOfView;
            }

        }

        private void ValidateReferences()
        {
            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponPresentation)} requires a PlayerWeaponController reference.",
                    this);
            }

            if (_visualRoot == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponPresentation)} requires a VisualRoot reference.",
                    this);
            }

            if (_muzzle == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponPresentation)} requires a Muzzle reference.",
                    this);
            }

            if (_aimPose == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponPresentation)} requires an AimPose reference.",
                    this);
            }

            if (_aimCamera == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponPresentation)} requires an AimCamera reference.",
                    this);
            }

            if (_visualRoot != null &&
                _aimPose != null &&
                _visualRoot.parent != _aimPose.parent)
            {
                Debug.LogError(
                    $"{nameof(WeaponPresentation)} requires VisualRoot and AimPose " +
                    "to have the same parent.",
                    this);
            }
        }
    }
}
