using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    [DefaultExecutionOrder(-100)]
    public sealed class WeaponHighReady : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Transform _poseRoot;

        [SerializeField]
        private Transform _castOrigin;

        [SerializeField]
        private PlayerWeaponView _weaponView;

        [SerializeField]
        private PlayerWeaponController _weaponController;

        [Header("Obstruction")]
        [SerializeField]
        private LayerMask _obstacleMask = ~0;

        [SerializeField, Range(0.01f, 1f)]
        private float _fireBlockThreshold = 0.05f;

        [Header("Transition")]
        [SerializeField, Min(0f)]
        private float _enterSpeed = 12f;

        [SerializeField, Min(0f)]
        private float _exitSpeed = 8f;

        private Vector3 _defaultLocalPosition;
        private Quaternion _defaultLocalRotation;
        private float _highReadyWeight;
        private float _targetWeight;
        private bool _hasDefaultPose;

        public float HighReadyWeight => _highReadyWeight;
        public bool IsAtRest => _highReadyWeight <= 0.01f;
        public bool IsBlockingFire =>
            _targetWeight > _fireBlockThreshold;

        private void Awake()
        {
            ValidateReferences();
            CaptureDefaultPose();
        }

        private void Update()
        {
            if (_castOrigin == null ||
                _weaponView == null)
            {
                _targetWeight = 0f;
                return;
            }

            if (_weaponController != null &&
                _weaponController.IsReloadInProgress)
            {
                _targetWeight = 0f;
                return;
            }

            _targetWeight = GetTargetWeight(
                _weaponView.CurrentView);
        }

        private void LateUpdate()
        {
            if (!_hasDefaultPose || _weaponView == null)
            {
                return;
            }

            WeaponView currentView = _weaponView.CurrentView;
            float speed = _targetWeight > _highReadyWeight
                ? _enterSpeed
                : _exitSpeed;

            _highReadyWeight = Damp(
                _highReadyWeight,
                _targetWeight,
                speed,
                Time.deltaTime);

            ApplyPose(currentView);
        }

        private void OnDisable()
        {
            _targetWeight = 0f;
            RestoreDefaultPose();
        }

        private float GetTargetWeight(WeaponView currentView)
        {
            if (currentView == null ||
                currentView.ObstructionCheckDistance <= 0f)
            {
                return 0f;
            }

            bool hasObstruction = Physics.SphereCast(
                _castOrigin.position,
                currentView.ObstructionCastRadius,
                _castOrigin.forward,
                out RaycastHit hit,
                currentView.ObstructionCheckDistance,
                _obstacleMask,
                QueryTriggerInteraction.Ignore);

            if (!hasObstruction)
            {
                return 0f;
            }

            float obstructionDepth =
                currentView.ObstructionCheckDistance - hit.distance;

            return Mathf.Clamp01(
                obstructionDepth /
                Mathf.Max(
                    currentView.ObstructionBlendDistance,
                    0.01f));
        }

        private void ApplyPose(WeaponView currentView)
        {
            Vector3 positionOffset = currentView != null
                ? currentView.HighReadyPositionOffset
                : Vector3.zero;
            Vector3 rotationOffset = currentView != null
                ? currentView.HighReadyRotationOffset
                : Vector3.zero;

            _poseRoot.SetLocalPositionAndRotation(
                _defaultLocalPosition +
                positionOffset * _highReadyWeight,
                _defaultLocalRotation * Quaternion.Euler(
                    rotationOffset * _highReadyWeight));
        }

        private void CaptureDefaultPose()
        {
            if (_poseRoot == null)
            {
                return;
            }

            _defaultLocalPosition = _poseRoot.localPosition;
            _defaultLocalRotation = _poseRoot.localRotation;
            _hasDefaultPose = true;
        }

        private void RestoreDefaultPose()
        {
            if (!_hasDefaultPose || _poseRoot == null)
            {
                return;
            }

            _poseRoot.SetLocalPositionAndRotation(
                _defaultLocalPosition,
                _defaultLocalRotation);
            _highReadyWeight = 0f;
        }

        private void ValidateReferences()
        {
            if (_poseRoot == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponHighReady)} requires a PoseRoot reference.",
                    this);
            }

            if (_castOrigin == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponHighReady)} requires a CastOrigin reference.",
                    this);
            }

            if (_weaponView == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponHighReady)} requires a PlayerWeaponView reference.",
                    this);
            }

            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponHighReady)} requires a PlayerWeaponController reference.",
                    this);
            }
        }

        private static float Damp(
            float current,
            float target,
            float speed,
            float deltaTime)
        {
            if (speed <= 0f)
            {
                return target;
            }

            float interpolation =
                1f - Mathf.Exp(-speed * deltaTime);

            return Mathf.Lerp(current, target, interpolation);
        }
    }
}
