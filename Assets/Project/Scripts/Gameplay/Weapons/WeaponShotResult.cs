using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    public readonly struct WeaponShotResult
    {
        public WeaponShotResult(
            Vector3 origin,
            Vector3 endPoint,
            Vector3 normal,
            Collider hitCollider)
        {
            Origin = origin;
            EndPoint = endPoint;
            Normal = normal;
            HitCollider = hitCollider;
        }

        public Vector3 Origin { get; }
        public Vector3 EndPoint { get; }
        public Vector3 Normal { get; }
        public Collider HitCollider { get; }
        public bool HasHit => HitCollider != null;
    }
}
