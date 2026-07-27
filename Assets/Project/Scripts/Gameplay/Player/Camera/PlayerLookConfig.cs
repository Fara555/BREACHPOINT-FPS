using UnityEngine;

namespace Breachpoint.Gameplay.Player.Look
{
    [CreateAssetMenu(
        fileName = "PlayerLookConfig",
        menuName = "Breachpoint/Player/Look Config")]
    public sealed class PlayerLookConfig : ScriptableObject
    {
        [Header("Sensitivity")]
        [SerializeField, Min(0f)] private float _mouseSensitivity = 0.08f;

        [Header("Vertical Rotation")]
        [SerializeField, Range(-89f, 0f)] private float _minimumPitch = -85f;
        [SerializeField, Range(0f, 89f)] private float _maximumPitch = 85f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursorOnStart = true;
        [SerializeField] private bool _hideCursorOnStart = true;

        public float MouseSensitivity => _mouseSensitivity;
        public float MinimumPitch => _minimumPitch;
        public float MaximumPitch => _maximumPitch;
        public bool LockCursorOnStart => _lockCursorOnStart;
        public bool HideCursorOnStart => _hideCursorOnStart;

        private void OnValidate()
        {
            if (_minimumPitch > _maximumPitch)
            {
                _minimumPitch = -85f;
                _maximumPitch = 85f;
            }
        }
    }
}