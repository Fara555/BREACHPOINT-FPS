using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement
{
    public readonly struct GroundInfo
    {
        public static GroundInfo None => new(
            false,
            Vector3.up,
            Vector3.zero,
            null,
            float.PositiveInfinity,
            90f);

        public bool IsGrounded { get; }
        public Vector3 Normal { get; }
        public Vector3 Point { get; }
        public Collider Collider { get; }
        public float Distance { get; }
        public float SurfaceAngle { get; }

        public GroundInfo(
            bool isGrounded,
            Vector3 normal,
            Vector3 point,
            Collider collider,
            float distance,
            float surfaceAngle)
        {
            IsGrounded = isGrounded;
            Normal = normal;
            Point = point;
            Collider = collider;
            Distance = distance;
            SurfaceAngle = surfaceAngle;
        }
    }
}