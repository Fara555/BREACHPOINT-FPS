using Breachpoint.Audio;
using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Audio
{
    public sealed class PlayerMovementAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private Transform _audioOrigin;

        private IAudioService _audioService;
        private PlayerMovementAudioConfig _config;

        private AudioHandle _slideLoopHandle;

        private PlayerMovementState _previousMovementState;

        private float _footstepDistance;

        private bool _wasSliding;
        private bool _initialized;

        [Inject]
        public void Construct(
            IAudioService audioService,
            PlayerMovementAudioConfig config)
        {
            _audioService =
                audioService;

            _config =
                config;
        }

        private void OnEnable()
        {
            if (_movement == null)
            {
                return;
            }

            _movement.Jumped +=
                HandleJumped;

            _movement.Landed +=
                HandleLanded;
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.Jumped -=
                    HandleJumped;

                _movement.Landed -=
                    HandleLanded;
            }

            StopSlideLoop();

            _initialized = false;
        }

        private void Start()
        {
            ValidateReferences();
            Initialize();
        }

        private void Update()
        {
            if (!CanUpdate())
            {
                return;
            }

            UpdateSlideAudio();
            UpdateFootsteps();

            _previousMovementState =
                _movement.CurrentState;

            _wasSliding =
                _movement.IsSliding;
        }

        private void UpdateFootsteps()
        {
            if (!CanPlayFootsteps())
            {
                ResetFootstepDistance();

                return;
            }

            PlayerMovementState currentState =
                _movement.CurrentState;

            if (currentState !=
                _previousMovementState)
            {
                ResetFootstepDistance();
            }

            float horizontalSpeed =
                GetHorizontalSpeed(
                    _movement.Velocity);

            if (horizontalSpeed <
                _config.MinimumFootstepSpeed)
            {
                ResetFootstepDistance();

                return;
            }

            float stepDistance =
                GetCurrentStepDistance(
                    currentState);

            if (stepDistance <=
                Mathf.Epsilon)
            {
                return;
            }

            _footstepDistance +=
                horizontalSpeed *
                Time.deltaTime;

            if (_footstepDistance <
                stepDistance)
            {
                return;
            }

            _footstepDistance -=
                stepDistance;

            PlayFootstep(
                currentState);
        }

        private bool CanPlayFootsteps()
        {
            if (!_movement.IsGrounded ||
                _movement.IsSliding)
            {
                return false;
            }

            return
                _movement.CurrentState ==
                PlayerMovementState.Walking ||
                _movement.CurrentState ==
                PlayerMovementState.Sprinting ||
                _movement.CurrentState ==
                PlayerMovementState.Crouching;
        }

        private float GetCurrentStepDistance(
            PlayerMovementState state)
        {
            return state switch
            {
                PlayerMovementState.Sprinting =>
                    _config.SprintStepDistance,

                PlayerMovementState.Crouching =>
                    _config.CrouchStepDistance,

                PlayerMovementState.Walking =>
                    _config.WalkStepDistance,

                _ =>
                    0f
            };
        }

        private void PlayFootstep(
            PlayerMovementState state)
        {
            AudioCue cue =
                state switch
                {
                    PlayerMovementState.Sprinting =>
                        _config.SprintFootstepCue,

                    PlayerMovementState.Crouching =>
                        _config.CrouchFootstepCue,

                    PlayerMovementState.Walking =>
                        _config.WalkFootstepCue,

                    _ =>
                        null
                };

            Play(
                cue);
        }

        private void HandleJumped()
        {
            ResetFootstepDistance();

            Play(
                _config.JumpCue);
        }

        private void HandleLanded(
            float downwardSpeed)
        {
            ResetFootstepDistance();

            if (downwardSpeed <
                _config.MinimumLandingSpeed)
            {
                return;
            }

            AudioCue cue;

            if (downwardSpeed >=
                _config.HeavyLandingSpeed)
            {
                cue =
                    _config.HeavyLandingCue;
            }
            else if (downwardSpeed >=
                     _config.MediumLandingSpeed)
            {
                cue =
                    _config.MediumLandingCue;
            }
            else
            {
                cue =
                    _config.LightLandingCue;
            }

            Play(
                cue);
        }

        private void UpdateSlideAudio()
        {
            bool isSliding =
                _movement.IsSliding;

            if (isSliding &&
                !_wasSliding)
            {
                HandleSlideStarted();

                return;
            }

            if (!isSliding &&
                _wasSliding)
            {
                HandleSlideEnded();
            }
        }

        private void HandleSlideStarted()
        {
            ResetFootstepDistance();

            Play(
                _config.SlideEnterCue);

            StartSlideLoop();
        }

        private void HandleSlideEnded()
        {
            StopSlideLoop();

            Play(
                _config.SlideExitCue);
        }

        private void StartSlideLoop()
        {
            if (_slideLoopHandle.IsPlaying ||
                _config.SlideLoopCue == null)
            {
                return;
            }

            _slideLoopHandle =
                _audioService.PlayLoop(
                    _config.SlideLoopCue,
                    GetAudioOrigin());
        }

        private void StopSlideLoop()
        {
            if (!_slideLoopHandle.IsPlaying)
            {
                return;
            }

            _slideLoopHandle.Stop();

            _slideLoopHandle =
                default;
        }

        private void Play(
            AudioCue cue)
        {
            if (cue == null ||
                _audioService == null)
            {
                return;
            }

            _audioService.Play(
                cue,
                GetAudioOrigin());
        }

        private Transform GetAudioOrigin()
        {
            return
                _audioOrigin != null
                    ? _audioOrigin
                    : transform;
        }

        private void ResetFootstepDistance()
        {
            _footstepDistance = 0f;
        }

        private static float GetHorizontalSpeed(
            Vector3 velocity)
        {
            velocity.y = 0f;

            return
                velocity.magnitude;
        }

        private void Initialize()
        {
            if (_movement == null)
            {
                return;
            }

            _previousMovementState =
                _movement.CurrentState;

            _wasSliding =
                _movement.IsSliding;

            _footstepDistance = 0f;

            _initialized = true;
        }

        private bool CanUpdate()
        {
            return
                _initialized &&
                _movement != null &&
                _audioService != null &&
                _config != null;
        }

        private void ValidateReferences()
        {
            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovementAudio)} requires PlayerMovement.",
                    this);
            }

            if (_audioService == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovementAudio)} requires IAudioService.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovementAudio)} requires PlayerMovementAudioConfig.",
                    this);
            }
        }
    }
}