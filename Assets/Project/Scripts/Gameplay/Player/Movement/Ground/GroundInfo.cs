using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement
{
    public readonly struct GroundInfo
    {
        public static GroundInfo None => new(
            false,
            false,
            false,
            Vector3.up,
            Vector3.zero,
            null,
            float.PositiveInfinity,
            90f);

        public bool HasSurface { get; }
        public bool IsWithinGroundDistance { get; }
        public bool IsWalkable { get; }

        public bool IsGrounded =>
            HasSurface &&
            IsWithinGroundDistance &&
            IsWalkable;

        public bool IsOnSteepSlope =>
            HasSurface &&
            IsWithinGroundDistance &&
            !IsWalkable;

        public Vector3 Normal { get; }
        public Vector3 Point { get; }
        public Collider Collider { get; }
        public float Distance { get; }
        public float SurfaceAngle { get; }

        public GroundInfo(
            bool hasSurface,
            bool isWithinGroundDistance,
            bool isWalkable,
            Vector3 normal,
            Vector3 point,
            Collider collider,
            float distance,
            float surfaceAngle)
        {
            HasSurface = hasSurface;
            IsWithinGroundDistance = isWithinGroundDistance;
            IsWalkable = isWalkable;
            Normal = normal;
            Point = point;
            Collider = collider;
            Distance = distance;
            SurfaceAngle = surfaceAngle;
        }
    }
}