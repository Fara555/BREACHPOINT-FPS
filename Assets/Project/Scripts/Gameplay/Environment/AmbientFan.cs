using UnityEngine;

namespace Breachpoint.Gameplay.Environment
{
    [DisallowMultipleComponent]
    public sealed class AmbientFan : MonoBehaviour
    {
        [SerializeField] private Transform _rotor;
        [SerializeField, Min(0f)] private float _degreesPerSecond = 160f;

        private void Awake()
        {
            if (_rotor != null) return;
            Debug.LogError("AmbientFan requires its local rotor reference.", this);
            enabled = false;
        }

        private void Update()
        {
            if (_rotor == null || _degreesPerSecond <= 0f) return;
            _rotor.localRotation *= Quaternion.AngleAxis(_degreesPerSecond * Time.deltaTime, Vector3.right);
        }

        private void OnValidate() => _degreesPerSecond = Mathf.Max(0f, _degreesPerSecond);
    }
}
