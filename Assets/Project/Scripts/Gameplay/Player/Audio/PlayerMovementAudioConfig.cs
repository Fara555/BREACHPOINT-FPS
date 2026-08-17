using Breachpoint.Audio;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Audio
{
    [CreateAssetMenu(
        fileName = "PlayerMovementAudioConfig",
        menuName = "Breachpoint/Player/Movement Audio Config")]
    public sealed class PlayerMovementAudioConfig : ScriptableObject
    {
        [Header("Footsteps")]
        [SerializeField]
        private AudioCue _walkFootstepCue;

        [SerializeField]
        private AudioCue _sprintFootstepCue;

        [SerializeField]
        private AudioCue _crouchFootstepCue;

        [Header("Footstep Distance")]
        [SerializeField, Min(0.01f)]
        private float _walkStepDistance = 1.7f;

        [SerializeField, Min(0.01f)]
        private float _sprintStepDistance = 2f;

        [SerializeField, Min(0.01f)]
        private float _crouchStepDistance = 1.25f;

        [SerializeField, Min(0f)]
        private float _minimumFootstepSpeed = 0.3f;

        [Header("Jump")]
        [SerializeField]
        private AudioCue _jumpCue;

        [Header("Landing")]
        [SerializeField]
        private AudioCue _lightLandingCue;

        [SerializeField]
        private AudioCue _mediumLandingCue;

        [SerializeField]
        private AudioCue _heavyLandingCue;

        [SerializeField, Min(0f)]
        private float _minimumLandingSpeed = 2f;

        [SerializeField, Min(0f)]
        private float _mediumLandingSpeed = 7f;

        [SerializeField, Min(0f)]
        private float _heavyLandingSpeed = 13f;

        [Header("Slide")]
        [SerializeField]
        private AudioCue _slideEnterCue;

        [SerializeField]
        private AudioCue _slideLoopCue;

        [SerializeField]
        private AudioCue _slideExitCue;

        public AudioCue WalkFootstepCue =>
            _walkFootstepCue;

        public AudioCue SprintFootstepCue =>
            _sprintFootstepCue;

        public AudioCue CrouchFootstepCue =>
            _crouchFootstepCue;

        public float WalkStepDistance =>
            _walkStepDistance;

        public float SprintStepDistance =>
            _sprintStepDistance;

        public float CrouchStepDistance =>
            _crouchStepDistance;

        public float MinimumFootstepSpeed =>
            _minimumFootstepSpeed;

        public AudioCue JumpCue =>
            _jumpCue;

        public AudioCue LightLandingCue =>
            _lightLandingCue;

        public AudioCue MediumLandingCue =>
            _mediumLandingCue;

        public AudioCue HeavyLandingCue =>
            _heavyLandingCue;

        public float MinimumLandingSpeed =>
            _minimumLandingSpeed;

        public float MediumLandingSpeed =>
            _mediumLandingSpeed;

        public float HeavyLandingSpeed =>
            _heavyLandingSpeed;

        public AudioCue SlideEnterCue =>
            _slideEnterCue;

        public AudioCue SlideLoopCue =>
            _slideLoopCue;

        public AudioCue SlideExitCue =>
            _slideExitCue;

        private void OnValidate()
        {
            _mediumLandingSpeed =
                Mathf.Max(
                    _mediumLandingSpeed,
                    _minimumLandingSpeed);

            _heavyLandingSpeed =
                Mathf.Max(
                    _heavyLandingSpeed,
                    _mediumLandingSpeed);
        }
    }
}