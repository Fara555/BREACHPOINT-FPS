using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyRagdollPresenter : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private RigBuilder _rigBuilder;
        [SerializeField] private Transform _skeletonRoot;
        [SerializeField, Min(0f)] private float _takeoverDelay = 1f;
        [SerializeField, Range(0f, 1f)] private float _momentumScale = 1f;
        private Rigidbody[] _bodies;
        private Transform[] _transforms;
        private Pose[] _restPose;
        private Pose[] _takeoverPose;
        private Vector3[] _restScale;
        private Collider[] _rootColliders;
        private bool[] _rootColliderEnabled;
        private bool _initialized;
        private bool _pending;
        private float _takeoverAt;
        private Vector3 _deathVelocity;
        public bool IsRagdoll { get; private set; }

        private void Awake() => Initialize();
        private void OnEnable() => ResetPresentation();

        private void Initialize()
        {
            if (_initialized || _skeletonRoot == null) return;
            _bodies = _skeletonRoot.GetComponentsInChildren<Rigidbody>(true);
            _transforms = _skeletonRoot.GetComponentsInChildren<Transform>(true);
            _restPose = new Pose[_transforms.Length];
            _takeoverPose = new Pose[_transforms.Length];
            _restScale = new Vector3[_transforms.Length];
            for (int i = 0; i < _transforms.Length; i++)
            {
                _restPose[i] = new Pose(_transforms[i].localPosition, _transforms[i].localRotation);
                _restScale[i] = _transforms[i].localScale;
            }
            _rootColliders = GetComponents<Collider>();
            _rootColliderEnabled = new bool[_rootColliders.Length];
            for (int i = 0; i < _rootColliders.Length; i++) _rootColliderEnabled[i] = _rootColliders[i].enabled;
            _initialized = true;
            FreezeBodies();
        }

        public void BeginDeath(Vector3 velocity, bool hasDeathAnimation)
        {
            Initialize();
            if (!_initialized || _pending || IsRagdoll) return;
            _deathVelocity = velocity * _momentumScale;
            _takeoverAt = Time.time + (hasDeathAnimation ? _takeoverDelay : 0f);
            _pending = true;
            for (int i = 0; i < _rootColliders.Length; i++) _rootColliders[i].enabled = false;
        }

        private void LateUpdate()
        {
            if (!_pending || Time.time < _takeoverAt) return;
            _pending = false;
            // Clearing a rig graph can restore bound transforms. Preserve the evaluated pose
            // across graph disposal, then release physics in this same LateUpdate.
            for (int i = 0; i < _transforms.Length; i++)
                _takeoverPose[i] = new Pose(_transforms[i].localPosition, _transforms[i].localRotation);
            if (_animator != null) _animator.enabled = false;
            if (_rigBuilder != null) _rigBuilder.enabled = false;
            RestorePose(_takeoverPose, false);
            Physics.SyncTransforms();
            for (int i = 0; i < _bodies.Length; i++)
            {
                _bodies[i].isKinematic = false;
                _bodies[i].linearVelocity = _deathVelocity;
                _bodies[i].angularVelocity = Vector3.zero;
                _bodies[i].WakeUp();
            }
            IsRagdoll = true;
        }

        public void ResetPresentation()
        {
            Initialize();
            if (!_initialized) return;
            _pending = false;
            IsRagdoll = false;
            _deathVelocity = Vector3.zero;
            FreezeBodies();
            if (_rigBuilder != null) _rigBuilder.enabled = false;
            RestorePose(_restPose, true);
            for (int i = 0; i < _rootColliders.Length; i++) _rootColliders[i].enabled = _rootColliderEnabled[i];
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.applyRootMotion = false;
                _animator.Rebind();
                _animator.Update(0f);
            }
            if (_rigBuilder != null) _rigBuilder.enabled = true;
        }

        private void FreezeBodies()
        {
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (!_bodies[i].isKinematic)
                {
                    _bodies[i].linearVelocity = Vector3.zero;
                    _bodies[i].angularVelocity = Vector3.zero;
                }
                _bodies[i].isKinematic = true;
            }
        }

        private void RestorePose(Pose[] pose, bool restoreScale)
        {
            for (int i = 0; i < _transforms.Length; i++)
            {
                _transforms[i].SetLocalPositionAndRotation(pose[i].position, pose[i].rotation);
                if (restoreScale) _transforms[i].localScale = _restScale[i];
            }
        }

        private void OnDisable()
        {
            _pending = false;
            if (_initialized) FreezeBodies();
        }
    }
}
