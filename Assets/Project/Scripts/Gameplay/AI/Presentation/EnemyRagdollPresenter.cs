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
        [SerializeField, Range(.1f, .9f)] private float _takeoverNormalizedTime = .5f;
        [SerializeField, Min(.1f)] private float _maximumAnimationWait = 4f;
        [SerializeField, Min(0f)] private float _maximumHorizontalMomentum = 1.2f;
        [SerializeField, Tooltip("Actual planar speed (m/s) to initial ragdoll speed (m/s). Walk 1.5 -> 2, Run 3.2 -> 4.2, Sprint 4.8 -> 6.5. Used only for immediate takeover.")] private AnimationCurve _movingDeathSpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(1.5f, 2f), new Keyframe(3.2f, 4.2f), new Keyframe(4.8f, 6.5f));
        [SerializeField, Min(0f)] private float _maximumMovingDeathSpeed = 6.5f;
        [SerializeField, Min(0f)] private float _maximumDepenetrationSpeed = 1f;
        [SerializeField, Min(0f)] private float _maximumAngularSpeed = 6f;
        [SerializeField, Min(0f)] private float _angularDamping = .8f;
        private Rigidbody[] _bodies;
        private Collider[] _bodyColliders;
        private CharacterJoint[] _joints;
        private Rigidbody[] _connectedBodies;
        private Transform[] _transforms;
        private Pose[] _restPose;
        private Pose[] _takeoverPose;
        private Vector3[] _restScale;
        private Collider[] _rootColliders;
        private bool[] _rootColliderEnabled;
        private bool _initialized;
        private bool _pending;
        private float _takeoverAt;
        private float _animationDeadline;
        private bool _waitForAnimation;
        private Vector3 _deathVelocity;
        public bool IsRagdoll { get; private set; }
        public float MaximumAnimationWait => _maximumAnimationWait;
        public float TakeoverNormalizedTime { get; private set; }
        public float TakeoverTime { get; private set; }
        public Vector3 AppliedMomentum { get; private set; }
        public float MaximumMovingDeathSpeed => _maximumMovingDeathSpeed;

        private void Awake() => Initialize();
        private void OnEnable() => ResetPresentation();

        private void Initialize()
        {
            if (_initialized || _skeletonRoot == null) return;
            _bodies = _skeletonRoot.GetComponentsInChildren<Rigidbody>(true);
            _bodyColliders = _skeletonRoot.GetComponentsInChildren<Collider>(true);
            _joints = _skeletonRoot.GetComponentsInChildren<CharacterJoint>(true);
            _connectedBodies = new Rigidbody[_joints.Length];
            for (int i = 0; i < _joints.Length; i++) _connectedBodies[i] = _joints[i].connectedBody;
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
            IgnoreSelfCollisions();
            FreezeBodies();
        }

        public void BeginDeath(Vector3 velocity, bool hasDeathAnimation)
        {
            Initialize();
            if (!_initialized || _pending || IsRagdoll) return;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float actualSpeed = planarVelocity.magnitude;
            if (hasDeathAnimation)
                _deathVelocity = Vector3.ClampMagnitude(planarVelocity * _momentumScale, _maximumHorizontalMomentum);
            else
            {
                float launchSpeed = Mathf.Clamp(_movingDeathSpeed.Evaluate(actualSpeed), 0f, _maximumMovingDeathSpeed);
                _deathVelocity = actualSpeed > .0001f ? planarVelocity * (launchSpeed / actualSpeed) : Vector3.zero;
            }
            _takeoverAt = Time.time + (hasDeathAnimation ? _takeoverDelay : 0f);
            _animationDeadline = Time.time + _maximumAnimationWait;
            _waitForAnimation = hasDeathAnimation;
            _pending = true;
            for (int i = 0; i < _rootColliders.Length; i++) _rootColliders[i].enabled = false;
            if (!hasDeathAnimation) TakeOver();
        }

        private void LateUpdate()
        {
            if (!_pending || Time.time < _takeoverAt) return;
            var state = _animator != null && _animator.enabled ? _animator.GetCurrentAnimatorStateInfo(0) : default;
            if (_waitForAnimation && _animator != null && _animator.enabled && Time.time < _animationDeadline &&
                (!state.IsTag("Death") || _animator.IsInTransition(0) || state.normalizedTime < _takeoverNormalizedTime)) return;
            TakeOver();
        }

        private void TakeOver()
        {
            _pending = false;
            TakeoverNormalizedTime = _waitForAnimation && _animator != null && _animator.enabled ? _animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
            TakeoverTime = Time.time;
            AppliedMomentum = _deathVelocity;
            // Clearing a rig graph can restore bound transforms. Preserve the evaluated pose
            // across graph disposal, then release physics synchronously.
            for (int i = 0; i < _transforms.Length; i++)
                _takeoverPose[i] = new Pose(_transforms[i].localPosition, _transforms[i].localRotation);
            if (_animator != null) _animator.enabled = false;
            if (_rigBuilder != null) _rigBuilder.enabled = false;
            RestorePose(_takeoverPose, false);
            Physics.SyncTransforms();
            IgnoreSelfCollisions();
            RefreshJointFrames();
            for (int i = 0; i < _bodies.Length; i++)
            {
                _bodies[i].isKinematic = false;
                _bodies[i].maxDepenetrationVelocity = _maximumDepenetrationSpeed;
                _bodies[i].maxAngularVelocity = _maximumAngularSpeed;
                _bodies[i].angularDamping = _angularDamping;
                _bodies[i].linearVelocity = Vector3.zero;
                _bodies[i].angularVelocity = Vector3.zero;
                // A single uniform velocity change carries the entire body along its
                // travel direction without pulling joints apart or adding upward force.
                _bodies[i].AddForce(_deathVelocity, ForceMode.VelocityChange);
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
            TakeoverNormalizedTime = TakeoverTime = 0f; AppliedMomentum = Vector3.zero;
            FreezeBodies();
            IgnoreSelfCollisions();
            if (_rigBuilder != null) _rigBuilder.enabled = false;
            RestorePose(_restPose, true);
            RefreshJointFrames();
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

        private void IgnoreSelfCollisions()
        {
            // Authored death poses overlap non-connected parts, including the extra
            // skeleton-root hitbox and pelvis. Contacts inside one corpse otherwise
            // inject separation energy. Environment contacts remain enabled.
            for (int i = 0; i < _bodyColliders.Length; i++)
                for (int j = i + 1; j < _bodyColliders.Length; j++)
                    Physics.IgnoreCollision(_bodyColliders[i], _bodyColliders[j]);
        }

        private void RefreshJointFrames()
        {
            // Rebind the existing constraints in the preserved pose. Their previous
            // reference frames belong to the standing bind pose and can produce
            // large angular correction impulses on an already folded death pose.
            // Keep the same bodies, axes, limits, masses and joint components.
            for (int i = 0; i < _joints.Length; i++)
            {
                _joints[i].connectedBody = null;
                _joints[i].connectedBody = _connectedBodies[i];
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
