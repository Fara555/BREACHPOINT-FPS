using UnityEngine;

namespace Breachpoint.Gameplay.Combat
{
    public readonly struct DamageInfo
    {
        public DamageInfo(
            float amount,
            Vector3 point,
            Vector3 direction,
            GameObject source,
            Collider hitCollider = null)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            HitCollider = hitCollider;
        }

        public float Amount { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }
        public Collider HitCollider { get; }
    }
}
