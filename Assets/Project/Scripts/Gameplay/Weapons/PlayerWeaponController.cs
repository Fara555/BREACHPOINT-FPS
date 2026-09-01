using System;
using Breachpoint.Gameplay.Player.Input;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class PlayerWeaponController :
        MonoBehaviour,
        IWeaponAimState
    {
        [Header("References")]
        [SerializeField]
        private HitscanWeapon _weapon;

        private IPlayerInput _input;
        private WeaponConfig _config;
        private WeaponAmmo _ammo;

        private float _nextShotTime;
        private float _reloadCompletionTime;

        public event Action<int, int> AmmunitionChanged;
        public event Action<float> ReloadStarted;
        public event Action ReloadCompleted;
        public event Action<WeaponShotResult> ShotFired;
        public event Action<bool> AimChanged;

        public int Magazine => _ammo?.Magazine ?? 0;
        public int Reserve => _ammo?.Reserve ?? 0;
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }

        [Inject]
        public void Construct(
            IPlayerInput input,
            WeaponConfig config,
            WeaponAmmo ammo)
        {
            _input = input;
            _config = config;
            _ammo = ammo;
        }

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (_weapon != null)
            {
                _weapon.ShotResolved += HandleShotResolved;
            }
        }

        private void Start()
        {
            ValidateDependencies();
            NotifyAmmunitionChanged();
        }

        private void Update()
        {
            if (!CanProcessInput())
            {
                return;
            }

            UpdateAimState();

            if (IsReloading)
            {
                UpdateReload();
                return;
            }

            if (_input.WasReloadPressed)
            {
                TryStartReload();
                return;
            }

            if (_input.IsFireHeld)
            {
                TryFire();
            }
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ShotResolved -= HandleShotResolved;
            }

            IsReloading = false;
            SetAiming(false);
        }

        private void TryFire()
        {
            if (Time.time < _nextShotTime)
            {
                return;
            }

            if (!_ammo.TryConsumeRound())
            {
                TryStartReload();
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

        private void UpdateAimState()
        {
            bool shouldAim =
                _input.IsAimHeld &&
                !IsReloading;

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

            AmmunitionChanged?.Invoke(
                _ammo.Magazine,
                _ammo.Reserve);
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
