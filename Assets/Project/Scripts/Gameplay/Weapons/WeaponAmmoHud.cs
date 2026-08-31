using TMPro;
using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class WeaponAmmoHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerWeaponController _weaponController;

        [SerializeField]
        private TMP_Text _ammunitionText;

        [SerializeField]
        private TMP_Text _reloadText;

        private void Awake()
        {
            ValidateReferences();

            if (_reloadText != null)
            {
                _reloadText.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_weaponController == null)
            {
                return;
            }

            _weaponController.AmmunitionChanged += HandleAmmunitionChanged;
            _weaponController.ReloadStarted += HandleReloadStarted;
            _weaponController.ReloadCompleted += HandleReloadCompleted;
        }

        private void Start()
        {
            RefreshAmmunition();

            if (_weaponController != null && _weaponController.IsReloading)
            {
                SetReloadVisible(true);
            }
        }

        private void OnDisable()
        {
            if (_weaponController == null)
            {
                return;
            }

            _weaponController.AmmunitionChanged -= HandleAmmunitionChanged;
            _weaponController.ReloadStarted -= HandleReloadStarted;
            _weaponController.ReloadCompleted -= HandleReloadCompleted;
        }

        private void HandleAmmunitionChanged(
            int magazine,
            int reserve)
        {
            if (_ammunitionText != null)
            {
                _ammunitionText.SetText(
                    "{0:00} / {1:000}",
                    magazine,
                    reserve);
            }
        }

        private void HandleReloadStarted(float duration)
        {
            SetReloadVisible(true);
        }

        private void HandleReloadCompleted()
        {
            SetReloadVisible(false);
        }

        private void RefreshAmmunition()
        {
            if (_weaponController == null)
            {
                return;
            }

            HandleAmmunitionChanged(
                _weaponController.Magazine,
                _weaponController.Reserve);
        }

        private void SetReloadVisible(bool isVisible)
        {
            if (_reloadText != null)
            {
                _reloadText.gameObject.SetActive(isVisible);
            }
        }

        private void ValidateReferences()
        {
            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponAmmoHud)} requires a PlayerWeaponController reference.",
                    this);
            }

            if (_ammunitionText == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponAmmoHud)} requires an AmmunitionText reference.",
                    this);
            }
        }
    }
}
