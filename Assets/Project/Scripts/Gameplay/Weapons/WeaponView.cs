using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class WeaponView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _visualRoot;

        [SerializeField]
        private Animator _weaponAnimator;

        [SerializeField]
        private WeaponViewAnimationSet _animations;

        public Animator WeaponAnimator => _weaponAnimator;
        public WeaponViewAnimationSet Animations => _animations;

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
        }
    }
}
