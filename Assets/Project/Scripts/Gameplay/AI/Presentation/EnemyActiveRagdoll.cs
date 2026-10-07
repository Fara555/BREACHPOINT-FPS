using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyActiveRagdoll : MonoBehaviour
    {
        private const float SupportLossGrace = .08f;
        private const float SupportLossFade = .16f;
        private const float FootProbeRadius = .06f;
        private const float FootProbeOffset = .12f;
        private const float MaximumTargetAngularSpeed = 6f;
        private const float MaximumMuscleAcceleration = 80f;
        private const float MovingTargetPlaybackScale = .65f;
        private const float MovingHitLegReleaseDelay = .08f;
        private const float BalanceDamping = 2f;
        private const float MaximumCoarseHitDistance = .75f;
        private const float MaximumTargetLinearSpeed = 6f;
        private const float StationaryAngularSpring = 400f;
        private const float StationaryAngularDamping = 40f;
        private const float MaximumStationaryAngularAcceleration = 200f;
        private const float TrajectoryGroundProbeDistance = 1.5f;
        private const float StationaryLegReleaseDelay = .1f;
        [SerializeField, Min(.1f), Tooltip("Moving death muscle lifetime. Stationary deaths use clip-phase release below.")] private float _activeDuration = .85f;
        [SerializeField, Min(.05f)] private float _upperBodyDuration = .3f;
        [SerializeField, Min(.05f)] private float _legDuration = .7f;
        [SerializeField, Min(0f)] private float _muscleSpring = 100f;
        [SerializeField, Min(0f)] private float _muscleDamping = 12f;
        [SerializeField, Min(0f)] private float _maximumMuscleTorque = 60f;
        [SerializeField, Min(0f)] private float _balanceTorque = 20f;
        [SerializeField, Range(10f, 90f)] private float _balanceLossAngle = 55f;
        [SerializeField, Min(.01f)] private float _supportProbeDistance = .3f;
        [SerializeField] private LayerMask _supportMask = ~0;
        [SerializeField, Min(0f), Tooltip("Bounded local hit impulse in N*s, separate from travel velocity.")] private float _hitImpulse = 1.5f;
        [Header("Stationary animation tracking")]
        [SerializeField, Min(.1f), Tooltip("Failsafe lifetime. Normal release follows the directional death clip phase.")] private float _stationaryDuration = 3.5f;
        [SerializeField, Min(0f)] private float _positionSpring = 240f;
        [SerializeField, Min(0f)] private float _positionDamping = 30f;
        [SerializeField, Min(0f), Tooltip("Bounded pose-following acceleration, including gravity support, in m/s².")] private float _maximumPoseAcceleration = 45f;
        [SerializeField, Range(0f, 1f)] private float _stationaryReleaseStart = .4f;
        [SerializeField, Range(0f, 1f)] private float _stationaryReleaseEnd = .8f;
        [SerializeField, Min(.1f), Tooltip("Sustained pelvis separation releases animation guidance at obstacles or unsupported drops.")] private float _maximumPoseSeparation = .65f;
        [Header("Moving hit animation tracking")]
        [SerializeField] private bool _useMovingHitAnimation = true;
        [SerializeField, Min(.1f), Tooltip("Hard lifetime of the moving Hit guide; bodies then settle passively.")] private float _movingHitDuration = .6f;
        [SerializeField, Min(0f), Tooltip("Seconds after the fatal hit when upper-body animation guidance starts relaxing.")] private float _movingHitReleaseStart = .25f;
        [SerializeField, Min(.05f), Tooltip("Seconds after the fatal hit when upper-body guidance ends. Legs release slightly later.")] private float _movingHitReleaseEnd = .45f;
        [Header("Fallback moving fatal hit")]
        [SerializeField] private EnemyMovingDeathImpact _movingDeathImpact = new EnemyMovingDeathImpact();
        private struct PoseBody
        {
            public Rigidbody Body;
            public Transform Target;
            public Vector3 PreviousPosition;
            public Quaternion PreviousRotation;
            public bool Leg;
        }
        private PoseBody[] _poseBodies;
        private Transform _targetPelvis;
        private float _separatedAt;
        private bool _startedSupported;
        private struct Muscle
        {
            public Rigidbody Body, Parent;
            public Transform Target, ParentTarget;
            public Quaternion PreviousTarget;
            public bool Leg;
        }
        private Muscle[] _muscles;
        private Collider[] _colliders;
        private Rigidbody _pelvis;
        private Transform _leftFoot, _rightFoot;
        private Vector3 _pelvisUp;
        private EnemyDeathPose _pose;
        private readonly RaycastHit[] _supportHits = new RaycastHit[16];
        private float _elapsed, _initialSpeed, _supportLostAt, _balanceLostAt;
        private bool _stationary, _lostBalance;
        public bool IsActive { get; private set; }
        public bool HasSupport { get; private set; }
        public bool HasLostBalance => _lostBalance;
        public float LegStrength { get; private set; }
        public float UpperBodyStrength { get; private set; }
        public float LastMotorTorque { get; private set; }
        public float TargetPlaybackSpeed { get; private set; }
        public float ActiveDuration => _stationary ? _stationaryDuration : IsMovingHitDeath ? _movingHitDuration : _activeDuration;
        public Animator PoseAnimator => _pose?.Animator;
        public AnimationClip DeathTargetClip => _pose?.DeathClip;
        public float DeathTargetWeight => _pose != null ? _pose.DeathClipWeight : 0f;
        public bool IsStationaryDeath => _stationary;
        public bool IsMovingHitDeath => !_stationary && _pose != null && _pose.IsMovingHit;
        public AnimationClip HitTargetClip => _pose?.HitClip;
        public float HitTargetWeight => _pose != null ? _pose.HitClipWeight : 0f;
        public Rigidbody HitBody { get; private set; }
        public Vector3 AppliedHitImpulse { get; private set; }
        public Vector3 AppliedMovingHitAngularChange => _movingDeathImpact.AppliedAngularChange;
        public Vector3 MovingHitPoint => _movingDeathImpact.HitPoint;
        public float MaximumMovingHitImpulse => _movingDeathImpact.MaximumImpulse;

        public bool Initialize(Animator animator, Transform skeleton)
        {
            if (_pose != null) return true;
            if (animator == null || !animator.isHuman || animator.runtimeAnimatorController == null) return false;
            _pelvis = animator.GetBoneTransform(HumanBodyBones.Hips).GetComponent<Rigidbody>();
            if (_pelvis == null) return false;
            _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            CharacterJoint[] joints = skeleton.GetComponentsInChildren<CharacterJoint>(true);
            if (joints.Length == 0 || _leftFoot == null || _rightFoot == null) return false;
            _pose = new EnemyDeathPose(animator);
            _colliders = skeleton.GetComponentsInChildren<Collider>(true);
            _muscles = new Muscle[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                var body = joints[i].GetComponent<Rigidbody>();
                var parent = joints[i].connectedBody;
                _muscles[i] = new Muscle {
                    Body = body, Parent = parent,
                    Target = _pose.TargetFor(body.transform),
                    ParentTarget = parent != null ? _pose.TargetFor(parent.transform) : null,
                    Leg = body.transform == leftLeg || body.transform.IsChildOf(leftLeg) ||
                          body.transform == rightLeg || body.transform.IsChildOf(rightLeg)
                };
            }
            _movingDeathImpact.Initialize(transform, animator, skeleton);
            _targetPelvis = _pose.TargetFor(_pelvis.transform);
            var bodies = skeleton.GetComponentsInChildren<Rigidbody>(true);
            _poseBodies = new PoseBody[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
                _poseBodies[i] = new PoseBody {
                    Body = bodies[i], Target = _pose.TargetFor(bodies[i].transform),
                    Leg = bodies[i].transform == leftLeg || bodies[i].transform.IsChildOf(leftLeg) ||
                          bodies[i].transform == rightLeg || bodies[i].transform.IsChildOf(rightLeg)
                };
            return true;
        }

        public void Begin(Animator animator, Vector3 velocity, bool stationary, float hitDirection)
        {
            _movingDeathImpact.Reset();
            _pose.Begin(animator, stationary, hitDirection, _useMovingHitAnimation);
            _pelvisUp = Quaternion.Inverse(_pelvis.rotation) * Vector3.up;
            _elapsed = 0f; _supportLostAt = _balanceLostAt = -1f; _lostBalance = false;
            _initialSpeed = velocity.magnitude; _stationary = stationary;
            IsActive = true; HasSupport = false;
            _startedSupported = FootSupported(_leftFoot) || FootSupported(_rightFoot);
            _separatedAt = -1f;
            for (int i = 0; i < _poseBodies.Length; i++)
            {
                _poseBodies[i].PreviousPosition = _poseBodies[i].Target.TransformPoint(_poseBodies[i].Body.centerOfMass);
                _poseBodies[i].PreviousRotation = _poseBodies[i].Target.rotation;
            }
            LegStrength = UpperBodyStrength = 1f; LastMotorTorque = 0f;
            for (int i = 0; i < _muscles.Length; i++) _muscles[i].PreviousTarget = RelativeTarget(_muscles[i]);
        }

        public void ApplyHit(Collider hitCollider, Vector3 point, Vector3 direction)
        {
            if (IsActive && !_stationary)
            {
                if (IsMovingHitDeath) _movingDeathImpact.Resolve(hitCollider, point, direction);
                else _movingDeathImpact.Apply(hitCollider, point, direction);
                HitBody = _movingDeathImpact.HitBody;
                AppliedHitImpulse = _movingDeathImpact.AppliedImpulse;
                return;
            }
            HitBody = null; AppliedHitImpulse = Vector3.zero;
            if (!IsActive || hitCollider == null || direction.sqrMagnitude < .0001f) return;
            // A bone hit identifies the body exactly. The alive navigation capsule
            // can be the first hitscan collider; map that coarse hit to nearby anatomy.
            bool belongsToOwner = hitCollider.transform == transform;
            Collider physicalHit = null;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] == hitCollider)
                {
                    belongsToOwner = true;
                    if (IsAnatomicalBody(hitCollider.attachedRigidbody)) physicalHit = hitCollider;
                    break;
                }
            if (!belongsToOwner) return;
            if (physicalHit == null)
            {
                float closest = MaximumCoarseHitDistance * MaximumCoarseHitDistance;
                for (int i = 0; i < _colliders.Length; i++)
                {
                    Collider candidate = _colliders[i];
                    if (!candidate.enabled || !IsAnatomicalBody(candidate.attachedRigidbody)) continue;
                    float distance = (candidate.ClosestPoint(point) - point).sqrMagnitude;
                    if (distance >= closest) continue;
                    closest = distance; physicalHit = candidate;
                }
            }
            if (physicalHit == null) return;
            HitBody = physicalHit.attachedRigidbody;
            if (HitBody.isKinematic) { HitBody = null; return; }
            AppliedHitImpulse = Vector3.ProjectOnPlane(direction.normalized, Vector3.up) * _hitImpulse;
            HitBody.AddForceAtPosition(AppliedHitImpulse, physicalHit.ClosestPoint(point), ForceMode.Impulse);
        }

        private bool IsAnatomicalBody(Rigidbody body) => body != null &&
            (body == _pelvis || body.transform.IsChildOf(_pelvis.transform));

        private void FixedUpdate()
        {
            if (!IsActive) return;
            float dt = Time.fixedDeltaTime;
            _elapsed += dt;
            HasSupport = FootSupported(_leftFoot) || FootSupported(_rightFoot);
            if (_stationary) { UpdateStationary(dt); return; }
            if (IsMovingHitDeath) { UpdateMovingHit(dt); return; }
            float tilt = Vector3.Angle(_pelvis.rotation * _pelvisUp, Vector3.up);
            if (!HasSupport || tilt > _balanceLossAngle)
            {
                if (_supportLostAt < 0f) _supportLostAt = _elapsed;
                // One lifted foot is normal; sustained loss of both feet is terminal.
                if (!_lostBalance && (_elapsed - _supportLostAt > SupportLossGrace || tilt > _balanceLossAngle))
                {
                    _lostBalance = true;
                    _balanceLostAt = _elapsed;
                }
            }
            else _supportLostAt = -1f;
            float loss = _lostBalance ? 1f - Mathf.Clamp01((_elapsed - _balanceLostAt) / SupportLossFade) : 1f;
            LegStrength = Fade(_elapsed, _legDuration) * loss;
            UpperBodyStrength = Fade(_elapsed, _upperBodyDuration) * loss;
            if (_elapsed >= _activeDuration || LegStrength <= .001f && UpperBodyStrength <= .001f) { Stop(); return; }
            float speedRatio = _initialSpeed > .0001f ? Mathf.Clamp01(Vector3.ProjectOnPlane(_pelvis.linearVelocity, Vector3.up).magnitude / _initialSpeed) : 0f;
            // The clock follows physical speed and freezes after loss of support.
            // Moving deaths keep locomotion; stationary targets use directional death.
            TargetPlaybackSpeed = _stationary ? 1f : HasSupport && !_lostBalance ? MovingTargetPlaybackScale * speedRatio : 0f;
            _pose.Evaluate(dt, TargetPlaybackSpeed);
            LastMotorTorque = 0f;
            for (int i = 0; i < _muscles.Length; i++) Drive(ref _muscles[i], dt);
            if (HasSupport && !_lostBalance && _elapsed < _upperBodyDuration)
            {
                Vector3 uprightError = Vector3.Cross(_pelvis.rotation * _pelvisUp, Vector3.up);
                // Bounded orientation assistance only: no positional pin or upward force.
                _pelvis.AddTorque(Vector3.ClampMagnitude(uprightError * _balanceTorque - _pelvis.angularVelocity * BalanceDamping, _balanceTorque) * LegStrength, ForceMode.Force);
            }
        }

        private void UpdateMovingHit(float dt)
        {
            // Follow the physical horizontal travel, rather than pulling the corpse
            // toward a fixed navigation root or adding another launch impulse.
            bool unsupported = !TrajectoryHasGround() || Mathf.Abs(_pelvis.position.y - _targetPelvis.position.y) > _maximumPoseSeparation;
            if (unsupported)
            {
                if (_separatedAt < 0f) _separatedAt = _elapsed;
                if (!_lostBalance && _elapsed - _separatedAt >= SupportLossGrace)
                { _lostBalance = true; _balanceLostAt = _elapsed; }
            }
            else _separatedAt = -1f;
            float loss = _lostBalance ? 1f - Mathf.Clamp01((_elapsed - _balanceLostAt) / SupportLossFade) : 1f;
            TargetPlaybackSpeed = 1f;
            _pose.FollowHorizontalPosition(_pelvis.position, _targetPelvis.position);
            _pose.Evaluate(dt, TargetPlaybackSpeed);
            UpperBodyStrength = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_movingHitReleaseStart, _movingHitReleaseEnd, _elapsed))) * loss;
            LegStrength = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_movingHitReleaseStart + MovingHitLegReleaseDelay, _movingHitReleaseEnd + MovingHitLegReleaseDelay, _elapsed))) * loss;
            if (_elapsed >= _movingHitDuration || LegStrength <= .001f && UpperBodyStrength <= .001f) { Stop(); return; }
            LastMotorTorque = 0f;
            if (unsupported || _lostBalance)
                for (int i = 0; i < _muscles.Length; i++) Drive(ref _muscles[i], dt);
            else for (int i = 0; i < _poseBodies.Length; i++) DrivePose(ref _poseBodies[i], dt);
        }

        private void UpdateStationary(float dt)
        {
            // An authored fall intentionally tilts the pelvis and lifts feet. Those
            // are not balance failures. Detect inability to follow the trajectory,
            // rather than cancelling the animation at its first falling frame.
            float separation = Vector3.Distance(_pelvis.position, _targetPelvis.position);
            bool unsupported = !_startedSupported || !TrajectoryHasGround() || separation > _maximumPoseSeparation;
            if (unsupported)
            {
                if (_separatedAt < 0f) _separatedAt = _elapsed;
                if (!_lostBalance && _elapsed - _separatedAt >= SupportLossGrace)
                { _lostBalance = true; _balanceLostAt = _elapsed; }
            }
            else _separatedAt = -1f;
            float loss = _lostBalance ? 1f - Mathf.Clamp01((_elapsed - _balanceLostAt) / SupportLossFade) : 1f;
            TargetPlaybackSpeed = 1f;
            _pose.Evaluate(dt, TargetPlaybackSpeed);
            float phase = _pose.Animator.GetCurrentAnimatorStateInfo(0).IsTag("Death")
                ? _pose.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
            UpperBodyStrength = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_stationaryReleaseStart, _stationaryReleaseEnd, phase))) * loss;
            LegStrength = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_stationaryReleaseStart + StationaryLegReleaseDelay, _stationaryReleaseEnd, phase))) * loss;
            if (_elapsed >= _stationaryDuration || LegStrength <= .001f && UpperBodyStrength <= .001f) { Stop(); return; }
            LastMotorTorque = 0f;
            if (unsupported || _lostBalance)
            {
                // An obstacle/drop cancels world-position assistance immediately;
                // relative muscles fade out without pulling anatomy back to space.
                for (int i = 0; i < _muscles.Length; i++) Drive(ref _muscles[i], dt);
            }
            else for (int i = 0; i < _poseBodies.Length; i++) DrivePose(ref _poseBodies[i], dt);
        }

        private void DrivePose(ref PoseBody muscle, float dt)
        {
            var body = muscle.Body;
            if (body.isKinematic) return;
            float strength = muscle.Leg ? LegStrength : UpperBodyStrength;
            Vector3 position = muscle.Target.TransformPoint(body.centerOfMass);
            Vector3 targetVelocity = Vector3.ClampMagnitude((position - muscle.PreviousPosition) / dt, MaximumTargetLinearSpeed);
            muscle.PreviousPosition = position;
            // Implicit PD gains remain stable at the project's fixed step. Forces
            // drive dynamic bodies; contacts and joint constraints still resolve.
            float gain = 1f / (1f + _positionDamping * dt + _positionSpring * dt * dt);
            Vector3 acceleration = (position - body.worldCenterOfMass) * (_positionSpring * gain) +
                (targetVelocity - body.linearVelocity) * ((_positionDamping + _positionSpring * dt) * gain) - Physics.gravity;
            body.AddForce(Vector3.ClampMagnitude(acceleration, _maximumPoseAcceleration) * strength, ForceMode.Acceleration);
            Quaternion rotation = muscle.Target.rotation;
            Vector3 targetAngular = Vector3.ClampMagnitude(RotationVector(rotation * Quaternion.Inverse(muscle.PreviousRotation)) / dt, MaximumTargetAngularSpeed);
            muscle.PreviousRotation = rotation;
            Vector3 angularError = RotationVector(rotation * Quaternion.Inverse(body.rotation));
            float angularGain = 1f / (1f + StationaryAngularDamping * dt + StationaryAngularSpring * dt * dt);
            Vector3 angularAcceleration = Vector3.ClampMagnitude(angularError * (StationaryAngularSpring * angularGain) +
                (targetAngular - body.angularVelocity) * ((StationaryAngularDamping + StationaryAngularSpring * dt) * angularGain), MaximumStationaryAngularAcceleration);
            Quaternion inertiaFrame = body.rotation * body.inertiaTensorRotation;
            Vector3 torque = inertiaFrame * Vector3.Scale(Quaternion.Inverse(inertiaFrame) * angularAcceleration, body.inertiaTensor);
            torque = Vector3.ClampMagnitude(torque, _maximumMuscleTorque) * strength;
            body.AddTorque(torque, ForceMode.Force);
            LastMotorTorque += torque.magnitude;
        }

        private void Drive(ref Muscle muscle, float dt)
        {
            if (muscle.Body == null || muscle.Parent == null || muscle.Body.isKinematic || muscle.Parent.isKinematic) return;
            float strength = muscle.Leg ? LegStrength : UpperBodyStrength;
            if (!_stationary && !IsMovingHitDeath) strength *= _movingDeathImpact.MuscleWeight(muscle.Body, _elapsed);
            Quaternion target = RelativeTarget(muscle);
            Quaternion current = Quaternion.Inverse(muscle.Parent.rotation) * muscle.Body.rotation;
            Vector3 error = muscle.Parent.rotation * RotationVector(target * Quaternion.Inverse(current));
            Vector3 targetVelocity = muscle.Parent.rotation * Vector3.ClampMagnitude(RotationVector(target * Quaternion.Inverse(muscle.PreviousTarget)) / dt, MaximumTargetAngularSpeed);
            muscle.PreviousTarget = target;
            Vector3 velocity = muscle.Body.angularVelocity - muscle.Parent.angularVelocity;
            Vector3 acceleration = Vector3.ClampMagnitude(error * _muscleSpring + (targetVelocity - velocity) * _muscleDamping, MaximumMuscleAcceleration);
            Quaternion inertiaFrame = muscle.Body.rotation * muscle.Body.inertiaTensorRotation;
            Vector3 torque = inertiaFrame * Vector3.Scale(Quaternion.Inverse(inertiaFrame) * acceleration, muscle.Body.inertiaTensor);
            torque = Vector3.ClampMagnitude(torque, _maximumMuscleTorque) * strength;
            // Equal and opposite joint torques preserve physical reaction at the parent.
            muscle.Body.AddTorque(torque, ForceMode.Force);
            muscle.Parent.AddTorque(-torque, ForceMode.Force);
            LastMotorTorque += torque.magnitude;
        }

        private bool TrajectoryHasGround()
        {
            // Test the environment under the planned fall, rather than demanding
            // that animation feet stay planted while the character tips over.
            int count = Physics.SphereCastNonAlloc(_targetPelvis.position + Vector3.up * FootProbeOffset,
                FootProbeRadius, Vector3.down, _supportHits, TrajectoryGroundProbeDistance, _supportMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!_supportHits[i].collider.transform.IsChildOf(transform) && _supportHits[i].normal.y >= .5f) return true;
            return false;
        }

        private bool FootSupported(Transform foot)
        {
            int count = Physics.SphereCastNonAlloc(foot.position + Vector3.up * FootProbeOffset, FootProbeRadius, Vector3.down,
                _supportHits, _supportProbeDistance, _supportMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _supportHits[i].collider;
                if (collider.transform.IsChildOf(transform) || _supportHits[i].normal.y < .5f) continue;
                return true;
            }
            return false;
        }

        private static Quaternion RelativeTarget(Muscle muscle) => muscle.ParentTarget != null
            ? Quaternion.Inverse(muscle.ParentTarget.rotation) * muscle.Target.rotation : muscle.Target.rotation;
        private static float Fade(float time, float duration) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
        private static Vector3 RotationVector(Quaternion rotation)
        {
            if (rotation.w < 0f) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            rotation.ToAngleAxis(out float degrees, out Vector3 axis);
            return degrees < .001f || float.IsNaN(axis.x) ? Vector3.zero : axis * (degrees * Mathf.Deg2Rad);
        }

        public void Stop()
        {
            IsActive = HasSupport = false;
            LegStrength = UpperBodyStrength = LastMotorTorque = TargetPlaybackSpeed = 0f;
            _pose?.Stop();
        }

        public void ResetPresentation()
        {
            _movingDeathImpact.Reset();
            Stop(); _elapsed = _initialSpeed = 0f; _stationary = _lostBalance = false;
            _pose?.ResetSnapshot();
            HitBody = null; AppliedHitImpulse = Vector3.zero;
        }
        private void OnDisable() => ResetPresentation();
    }
}
