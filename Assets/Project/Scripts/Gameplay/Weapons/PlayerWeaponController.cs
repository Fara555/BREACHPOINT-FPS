using System;
using System.Collections.Generic;
using Breachpoint.Gameplay.Player.Input;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class PlayerWeaponController :
        MonoBehaviour,
        IWeaponAimState,
        IWeaponActionState
    {
        [Header("References")]
        [SerializeField]
        private HitscanWeapon _weapon;

        [SerializeField]
        private WeaponHighReady _highReady;

        [SerializeField]
        private PlayerWeaponView _weaponView;

        private IPlayerInput _input;
        private WeaponConfig _config;
        private WeaponAmmo _ammo;
        private WeaponConfig _defaultConfig;
        private readonly Dictionary<WeaponView, WeaponAmmo> _ammunition = new();

        private float _nextShotTime;
        private float _reloadCompletionTime;
        private bool _reloadRequested;
        private bool _fireRequested;
        private bool _wasFireHeld;
        private bool _isMotionReadyForAction = true;
        private bool _isReloadPresentationReady = true;

        public event Action<int> AmmunitionChanged;
        public event Action<float> ReloadStarted;
        public event Action ReloadCompleted;
        public event Action<WeaponShotResult> ShotFired;
        public event Action<bool> AimChanged;

        public int Magazine => _ammo?.Magazine ?? 0;
        public int MagazineSize => _ammo?.MagazineSize ?? 0;
        public bool IsReloading { get; private set; }
        public bool IsReloadInProgress =>
            IsReloading || _reloadRequested;
        public bool IsAiming { get; private set; }
        public WeaponConfig CurrentConfig => _config;
        public bool IsActionRequested =>
            IsReloading ||
            _reloadRequested ||
            _fireRequested ||
            (IsAutomaticFire &&
             _input != null &&
             _input.IsFireHeld);

        private bool IsAutomaticFire =>
            _config != null &&
            _config.FireMode == WeaponFireMode.Automatic;

        [Inject]
        public void Construct(
            IPlayerInput input,
            WeaponConfig config)
        {
            _input = input;
            _defaultConfig = config;
        }

        private void Awake()
        {
            if (_weaponView == null)
            {
                _weaponView = GetComponent<PlayerWeaponView>();
            }

            ValidateReferences();
        }

        private void OnEnable()
        {
            if (_weapon != null)
            {
                _weapon.ShotResolved += HandleShotResolved;
            }

            if (_weaponView != null)
            {
                _weaponView.ViewEquipped += HandleViewEquipped;
                EquipView(_weaponView.CurrentView);
            }
        }

        private void Start()
        {
            EquipView(_weaponView != null ? _weaponView.CurrentView : null);
            ValidateDependencies();
            NotifyAmmunitionChanged();
        }

        private void Update()
        {
            if (!CanProcessInput())
            {
                return;
            }

            if (_input.WasReloadPressed && _ammo.CanReload)
            {
                QueueReload();
            }

            bool isFireHeld = _input.IsFireHeld;

            if (isFireHeld &&
                !_wasFireHeld &&
                !IsReloadInProgress)
            {
                _fireRequested = true;
            }

            _wasFireHeld = isFireHeld;

            UpdateAimState();

            if (IsReloading)
            {
                UpdateReload();
                return;
            }

            if (_reloadRequested)
            {
                if (!_isMotionReadyForAction ||
                    !_isReloadPresentationReady ||
                    (_highReady != null && !_highReady.IsAtRest))
                {
                    return;
                }

                TryStartReload();
                _reloadRequested = false;
                return;
            }

            if (_fireRequested ||
                (IsAutomaticFire && isFireHeld))
            {
                if (!_isMotionReadyForAction)
                {
                    return;
                }

                TryFire();
                _fireRequested = false;
            }
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ShotResolved -= HandleShotResolved;
            }

            if (_weaponView != null)
            {
                _weaponView.ViewEquipped -= HandleViewEquipped;
            }

            IsReloading = false;
            _reloadRequested = false;
            _fireRequested = false;
            _wasFireHeld = false;
            _isMotionReadyForAction = true;
            _isReloadPresentationReady = true;
            SetAiming(false);
        }

        private void TryFire()
        {
            if (_highReady != null && _highReady.IsBlockingFire)
            {
                return;
            }

            if (Time.time < _nextShotTime)
            {
                return;
            }

            if (!_ammo.TryConsumeRound())
            {
                QueueReload();
                return;
            }

            _nextShotTime = Time.time + _config.SecondsPerShot;

            _weapon.Fire(IsAiming);
            NotifyAmmunitionChanged();
        }

        private void TryStartReload()
        {
            if (!_ammo.CanReload)
            {
                return;
            }

            IsReloading = true;
            SetAiming(false);
            _reloadCompletionTime = Time.time + _config.ReloadDuration;

            ReloadStarted?.Invoke(_config.ReloadDuration);
        }

        private void QueueReload()
        {
            if (!_ammo.CanReload)
            {
                return;
            }

            _fireRequested = false;
            _reloadRequested = true;
            _isReloadPresentationReady = false;
        }

        public void ReportMotionReadyForAction(bool isReady)
        {
            _isMotionReadyForAction = isReady;
        }

        public void ReportReloadPresentationReady(bool isReady)
        {
            _isReloadPresentationReady = isReady;
        }

        private void UpdateReload()
        {
            if (Time.time < _reloadCompletionTime)
            {
                return;
            }

            _ammo.Reload();
            IsReloading = false;

            NotifyAmmunitionChanged();
            ReloadCompleted?.Invoke();
        }

        private void HandleShotResolved(WeaponShotResult result)
        {
            ShotFired?.Invoke(result);
        }

        private void HandleViewEquipped(WeaponView view)
        {
            EquipView(view);
        }

        private void EquipView(WeaponView view)
        {
            WeaponConfig nextConfig = view != null && view.Config != null
                ? view.Config
                : _defaultConfig;

            if (nextConfig == null)
            {
                _config = null;
                _ammo = null;
                return;
            }

            if (view != null && _ammunition.TryGetValue(view, out WeaponAmmo ammo))
            {
                _ammo = ammo;
            }
            else
            {
                _ammo = new WeaponAmmo(nextConfig);

                if (view != null)
                {
                    _ammunition.Add(view, _ammo);
                }
            }

            _config = nextConfig;
            IsReloading = false;
            _reloadRequested = false;
            _fireRequested = false;
            _nextShotTime = 0f;
            SetAiming(false);
            NotifyAmmunitionChanged();
        }

        private void UpdateAimState()
        {
            bool isHighReady =
                _highReady != null &&
                _highReady.IsInHighReady;

            bool shouldAim =
                _input.IsAimHeld &&
                !IsReloading &&
                !_reloadRequested &&
                !isHighReady;

            SetAiming(shouldAim);
        }

        private void SetAiming(bool isAiming)
        {
            if (IsAiming == isAiming)
            {
                return;
            }

            IsAiming = isAiming;
            AimChanged?.Invoke(IsAiming);
        }

        private void NotifyAmmunitionChanged()
        {
            if (_ammo == null)
            {
                return;
            }

            AmmunitionChanged?.Invoke(_ammo.Magazine);
        }

        private bool CanProcessInput()
        {
            return
                _input != null &&
                _config != null &&
                _ammo != null &&
                _weapon != null;
        }

        private void ValidateReferences()
        {
            if (_weapon == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponController)} requires a HitscanWeapon reference.",
                    this);
            }

            if (_highReady == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponController)} requires a WeaponHighReady reference.",
                    this);
            }

            if (_weaponView == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponController)} requires a PlayerWeaponView reference.",
                    this);
            }
        }

        private void ValidateDependencies()
        {
            if (_input == null || _config == null || _ammo == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponController)} dependencies were not injected.",
                    this);
            }
        }
    }
}
