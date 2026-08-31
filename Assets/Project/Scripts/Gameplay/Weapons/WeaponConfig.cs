using UnityEngine;
using UnityEngine.Serialization;

namespace Breachpoint.Gameplay.Weapons
{
    [CreateAssetMenu(
        fileName = "WeaponConfig",
        menuName = "Breachpoint/Weapons/Weapon Config")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Header("Damage")]
        [SerializeField, Min(0f)]
        private float _damage = 30f;

        [SerializeField, Min(0.01f)]
        private float _range = 150f;

        [SerializeField]
        private LayerMask _hitMask = ~0;

        [Header("Firing")]
        [SerializeField, Min(1f)]
        private float _roundsPerMinute = 600f;

        [FormerlySerializedAs("_spreadAngle")]
        [SerializeField, Range(0f, 10f)]
        private float _hipSpreadAngle = 0.45f;

        [SerializeField, Range(0f, 10f)]
        private float _aimSpreadAngle = 0.08f;

        [Header("Aiming")]
        [SerializeField, Min(0.01f)]
        private float _aimTransitionSpeed = 12f;

        [SerializeField, Range(0.5f, 1f)]
        private float _aimFieldOfViewMultiplier = 0.85f;

        [Header("Ammunition")]
        [SerializeField, Min(1)]
        private int _magazineSize = 30;

        [SerializeField, Min(0)]
        private int _startingReserveAmmunition = 120;

        [SerializeField, Min(0.01f)]
        private float _reloadDuration = 2.1f;

        [Header("Camera Recoil")]
        [SerializeField, Min(0f)]
        private float _cameraRecoilPitch = 1.1f;

        [SerializeField, Min(0f)]
        private float _cameraRecoilYaw = 0.3f;

        [SerializeField, Min(0.01f)]
        private float _cameraRecoilSnappiness = 18f;

        [SerializeField, Min(0.01f)]
        private float _cameraRecoilReturnSpeed = 10f;

        [SerializeField, Min(0f)]
        private float _maximumCameraRecoil = 8f;

        [Header("Visual Recoil")]
        [SerializeField, Min(0f)]
        private float _visualKickDistance = 0.04f;

        [SerializeField, Min(0f)]
        private float _visualKickPitch = 2.5f;

        [SerializeField, Min(0.01f)]
        private float _visualRecoilSnappiness = 24f;

        [SerializeField, Min(0.01f)]
        private float _visualRecoilReturnSpeed = 14f;

        public float Damage => _damage;
        public float Range => _range;
        public LayerMask HitMask => _hitMask;
        public float SecondsPerShot => 60f / _roundsPerMinute;
        public float HipSpreadAngle => _hipSpreadAngle;
        public float AimSpreadAngle => _aimSpreadAngle;
        public float AimTransitionSpeed => _aimTransitionSpeed;
        public float AimFieldOfViewMultiplier => _aimFieldOfViewMultiplier;
        public int MagazineSize => _magazineSize;
        public int StartingReserveAmmunition => _startingReserveAmmunition;
        public float ReloadDuration => _reloadDuration;
        public float CameraRecoilPitch => _cameraRecoilPitch;
        public float CameraRecoilYaw => _cameraRecoilYaw;
        public float CameraRecoilSnappiness => _cameraRecoilSnappiness;
        public float CameraRecoilReturnSpeed => _cameraRecoilReturnSpeed;
        public float MaximumCameraRecoil => _maximumCameraRecoil;
        public float VisualKickDistance => _visualKickDistance;
        public float VisualKickPitch => _visualKickPitch;
        public float VisualRecoilSnappiness => _visualRecoilSnappiness;
        public float VisualRecoilReturnSpeed => _visualRecoilReturnSpeed;

        private void OnValidate()
        {
            _aimSpreadAngle = Mathf.Min(
                _aimSpreadAngle,
                _hipSpreadAngle);
        }
    }
}
