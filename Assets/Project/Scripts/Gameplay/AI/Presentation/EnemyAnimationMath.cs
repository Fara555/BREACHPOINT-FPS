using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    public static class EnemyAnimationMath
    {
        private const float SteadyStartBlend = .035f;
        public static Vector2 MovementDirection(Quaternion rotation, Vector3 desired, Vector3 actual, float requestedSpeed)
        {
            desired.y = actual.y = 0f;
            if (desired.sqrMagnitude < .0001f) desired = actual;
            float actualWeight = Mathf.Clamp01(actual.magnitude / Mathf.Max(.01f, requestedSpeed));
            Vector3 direction = Vector3.Lerp(desired.normalized, actual.normalized, actualWeight);
            Vector3 local = Quaternion.Inverse(rotation) * direction;
            return new Vector2(local.x, local.z).normalized;
        }

        public static float MovementBlend(float speed, float requestedSpeed, EnemyMovementTier tier, bool requested, EnemyMovementConfig movement, EnemyAnimationConfig config)
        {
            float threshold = tier == EnemyMovementTier.Sprint ? config.SprintThreshold : tier == EnemyMovementTier.Run ? config.RunThreshold : config.WalkThreshold;
            // A tier request is a target, not the current pose. Use actual world speed
            // across continuous thresholds while navigation changes its speed limit.
            float raw = tier == EnemyMovementTier.Crouch ? threshold * Mathf.Clamp01(speed / Mathf.Max(.01f, movement.CrouchSpeed)) : ContinuousMovementBlend(speed, movement, config);
            return requested ? Mathf.Max(raw, tier == EnemyMovementTier.Steady ? SteadyStartBlend : threshold * config.MovementStartAnticipation) : raw;
        }
        private static float ContinuousMovementBlend(float speed, EnemyMovementConfig movement, EnemyAnimationConfig config)
        {
            if (speed <= config.StationarySpeed) return 0f;
            if (speed <= movement.WalkSpeed) return config.WalkThreshold * speed / Mathf.Max(.01f, movement.WalkSpeed);
            if (speed <= movement.RunSpeed) return Mathf.Lerp(config.WalkThreshold, config.RunThreshold, Mathf.InverseLerp(movement.WalkSpeed, movement.RunSpeed, speed));
            return Mathf.Lerp(config.RunThreshold, config.SprintThreshold, Mathf.InverseLerp(movement.RunSpeed, movement.SprintSpeed, speed));
        }
        public static float StrideSpeed(float speed, float naturalSpeed, EnemyAnimationConfig config)
        {
            if (speed <= config.StationarySpeed) return 1f;
            return Mathf.Clamp(speed / Mathf.Max(0.01f, naturalSpeed), config.StridePlaybackLimits.x, config.StridePlaybackLimits.y);
        }

        public static float NaturalCombatSpeed(float blend, EnemyAnimationConfig config)
        {
            if (blend <= config.WalkThreshold) return config.CombatNaturalSpeeds.x;
            if (blend <= config.RunThreshold) return Mathf.Lerp(config.CombatNaturalSpeeds.x, config.CombatNaturalSpeeds.y, Mathf.InverseLerp(config.WalkThreshold, config.RunThreshold, blend));
            return Mathf.Lerp(config.CombatNaturalSpeeds.y, config.CombatNaturalSpeeds.z, Mathf.InverseLerp(config.RunThreshold, config.SprintThreshold, blend));
        }
        public static float MoveSpeed(float speed, float walkSpeed, float runSpeed, EnemyAnimationConfig config)
        {
            if (speed <= config.StationarySpeed) return 0f;
            walkSpeed = Mathf.Max(0.01f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            if (speed <= walkSpeed) return Mathf.Lerp(0f, config.WalkThreshold, speed / walkSpeed);
            if (speed <= runSpeed && runSpeed > walkSpeed)
                return Mathf.Lerp(config.WalkThreshold, config.RunThreshold, (speed - walkSpeed) / (runSpeed - walkSpeed));
            float sprintSpeed = runSpeed * Mathf.Max(1.01f, config.SprintSpeedRatio);
            return Mathf.Lerp(config.RunThreshold, config.SprintThreshold, Mathf.Clamp01((speed - runSpeed) / (sprintSpeed - runSpeed)));
        }

        public static Vector2 LocalMovement(Quaternion rotation, Vector3 velocity, float referenceSpeed)
        {
            Vector3 local = Quaternion.Inverse(rotation) * velocity;
            return Vector2.ClampMagnitude(new Vector2(local.x, local.z) / Mathf.Max(0.01f, referenceSpeed), 1f);
        }

        // DamageInfo.Direction is projectile travel; its inverse points toward the attacker.
        public static float HitDirection(Quaternion rotation, Vector3 travelDirection, bool death)
        {
            Vector3 local = Quaternion.Inverse(rotation) * -travelDirection;
            if (local.x * local.x + local.z * local.z < 0.0001f) return 0f;
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z)) return local.x < 0f ? 1f : 2f;
            return death && local.z < 0f ? 3f : 0f;
        }
    }
}
