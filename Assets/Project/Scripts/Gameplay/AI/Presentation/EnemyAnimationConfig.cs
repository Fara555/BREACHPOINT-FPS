using System;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [Serializable]
    public sealed class EnemyAnimationConfig
    {
        [field: SerializeField, Min(0f)] public float FloatDamping { get; private set; } = 0.12f;
        [field: SerializeField, Min(0.01f)] public float StationarySpeed { get; private set; } = 0.08f;
        [field: SerializeField, Range(0f, 1f)] public float WalkThreshold { get; private set; } = 0.33f;
        [field: SerializeField, Range(0f, 1f)] public float RunThreshold { get; private set; } = 0.66f;
        [field: SerializeField, Range(0f, 1f)] public float SprintThreshold { get; private set; } = 1f;
        [field: SerializeField, Min(1f)] public float SprintSpeedRatio { get; private set; } = 1.5f;
        [field: SerializeField, Range(1f, 180f)] public float Turn90Angle { get; private set; } = 55f;
        [field: SerializeField, Range(1f, 180f)] public float Turn180Angle { get; private set; } = 135f;
        [field: SerializeField, Min(0f)] public float TurnCooldown { get; private set; } = 1.2f;
        [field: SerializeField, Range(0f, 1f)] public float RunEnterRatio { get; private set; } = 0.7f;
        [field: SerializeField, Min(0f)] public float ModeBlendDuration { get; private set; } = 0.15f;
        [field: SerializeField, Min(0f)] public float AimSmoothing { get; private set; } = 12f;
        [field: SerializeField, Min(0.1f)] public float RestAimDistance { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float FireHoldDuration { get; private set; } = 0.18f;
    }
}
