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
        [field: SerializeField] public Vector4 SteadyTurnDurations { get; private set; } = new Vector4(1.466667f, 1.7f, 1.866667f, 1.966667f);
        [field: SerializeField] public Vector4 CombatTurnDurations { get; private set; } = new Vector4(1f, 1f, 1.733333f, 1.8f);
        [field: SerializeField] public Vector4 CrouchTurnDurations { get; private set; } = new Vector4(1.266667f, 1.233333f, 1.433333f, 1.233333f);
        [field: SerializeField, Min(0f)] public float AimSmoothing { get; private set; } = 12f;
        [field: SerializeField, Min(0.1f)] public float RestAimDistance { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float FireHoldDuration { get; private set; } = 0.18f;
    }
}
