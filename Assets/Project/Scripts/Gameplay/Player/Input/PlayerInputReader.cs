using UnityEngine;
using UnityEngine.InputSystem;

namespace Breachpoint.Gameplay.Player.Input
{
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [Header("Movement")]
        [SerializeField]
        private InputActionReference _moveAction;

        [SerializeField]
        private InputActionReference _lookAction;

        [SerializeField]
        private InputActionReference _jumpAction;

        [SerializeField]
        private InputActionReference _sprintAction;

        [SerializeField]
        private InputActionReference _crouchAction;

        public Vector2 Move =>
            _moveAction != null
                ? _moveAction.action.ReadValue<Vector2>()
                : Vector2.zero;

        public Vector2 Look =>
            _lookAction != null
                ? _lookAction.action.ReadValue<Vector2>()
                : Vector2.zero;

        public bool IsSprintHeld =>
            _sprintAction != null &&
            _sprintAction.action.IsPressed();

        public bool IsCrouchHeld =>
            _crouchAction != null &&
            _crouchAction.action.IsPressed();

        public bool IsJumpHeld =>
            _jumpAction != null &&
            _jumpAction.action.IsPressed();

        public bool WasJumpPressed =>
            _jumpAction != null &&
            _jumpAction.action.WasPressedThisFrame();

        public bool WasJumpReleased =>
            _jumpAction != null &&
            _jumpAction.action.WasReleasedThisFrame();

        private void OnEnable()
        {
            EnableAction(
                _moveAction);

            EnableAction(
                _lookAction);

            EnableAction(
                _jumpAction);

            EnableAction(
                _sprintAction);

            EnableAction(
                _crouchAction);
        }

        private void OnDisable()
        {
            DisableAction(
                _moveAction);

            DisableAction(
                _lookAction);

            DisableAction(
                _jumpAction);

            DisableAction(
                _sprintAction);

            DisableAction(
                _crouchAction);
        }

        private static void EnableAction(
            InputActionReference actionReference)
        {
            if (actionReference == null)
            {
                return;
            }

            actionReference.action.Enable();
        }

        private static void DisableAction(
            InputActionReference actionReference)
        {
            if (actionReference == null)
            {
                return;
            }

            actionReference.action.Disable();
        }
    }
}