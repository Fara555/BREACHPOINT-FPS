using UnityEngine;

namespace Breachpoint.Gameplay.Player.Camera
{
    [CreateAssetMenu(
        fileName = "PlayerHeadBobConfig",
        menuName = "Breachpoint/Player/Head Bob Config")]
    public sealed class PlayerHeadBobConfig : ScriptableObject
    {
        [Header("Walk")]
        [SerializeField, Min(0f)]
        private float _walkCyclesPerMeter = 0.28f;

        [SerializeField, Min(0f)]
        private float _walkHorizontalAmplitude = 0.01f;

        [SerializeField, Min(0f)]
        private float _walkVerticalAmplitude = 0.014f;

        [Header("Sprint")]
        [SerializeField, Min(0f)]
        private float _sprintCyclesPerMeter = 0.22f;

        [SerializeField, Min(0f)]
        private float _sprintHorizontalAmplitude = 0.014f;

        [SerializeField, Min(0f)]
        private float _sprintVerticalAmplitude = 0.02f;

        [Header("Crouch")]
        [SerializeField, Min(0f)]
        private float _crouchCyclesPerMeter = 0.32f;

        [SerializeField, Min(0f)]
        private float _crouchHorizontalAmplitude = 0.006f;

        [SerializeField, Min(0f)]
        private float _crouchVerticalAmplitude = 0.008f;

        [Header("Blending")]
        [SerializeField, Min(0.01f)]
        private float _blendInSpeed = 6f;

        [SerializeField, Min(0.01f)]
        private float _blendOutSpeed = 8f;

        [SerializeField, Min(0.01f)]
        private float _returnSpeed = 14f;

        [SerializeField, Min(0f)]
        private float _minimumMovementSpeed = 0.3f;

        public float WalkCyclesPerMeter =>
            _walkCyclesPerMeter;

        public float WalkHorizontalAmplitude =>
            _walkHorizontalAmplitude;

        public float WalkVerticalAmplitude =>
            _walkVerticalAmplitude;

        public float SprintCyclesPerMeter =>
            _sprintCyclesPerMeter;

        public float SprintHorizontalAmplitude =>
            _sprintHorizontalAmplitude;

        public float SprintVerticalAmplitude =>
            _sprintVerticalAmplitude;

        public float CrouchCyclesPerMeter =>
            _crouchCyclesPerMeter;

        public float CrouchHorizontalAmplitude =>
            _crouchHorizontalAmplitude;

        public float CrouchVerticalAmplitude =>
            _crouchVerticalAmplitude;

        public float BlendInSpeed =>
            _blendInSpeed;

        public float BlendOutSpeed =>
            _blendOutSpeed;

        public float ReturnSpeed =>
            _returnSpeed;

        public float MinimumMovementSpeed =>
            _minimumMovementSpeed;
    }
}