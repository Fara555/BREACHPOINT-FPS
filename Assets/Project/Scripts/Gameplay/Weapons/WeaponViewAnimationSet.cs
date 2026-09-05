using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    [CreateAssetMenu(
        fileName = "WeaponViewAnimationSet",
        menuName = "Breachpoint/Weapons/View Animation Set")]
    public sealed class WeaponViewAnimationSet : ScriptableObject
    {
        [Header("Reload")]
        [SerializeField]
        private AnimationClip _armsReload;

        [SerializeField]
        private AnimationClip _weaponReload;

        public AnimationClip ArmsReload => _armsReload;
        public AnimationClip WeaponReload => _weaponReload;

        public bool HasReloadAnimation =>
            _armsReload != null ||
            _weaponReload != null;
    }
}
