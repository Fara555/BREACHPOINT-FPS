using System;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    // Owned by EnemyActiveRagdoll. Only moving fatalities use this reaction.
    [Serializable]
    internal sealed class EnemyMovingDeathImpact
    {
        private const float RayBackstep = .5f;
        private const float AnatomyRayLength = 1.8f;
        private const float MaximumPointDistance = .6f;
        [SerializeField, Min(0f), Tooltip("Local horizontal velocity kick, capped by maximum impulse. Does not replace travel inertia.")] private float _linearVelocityChange = 1.4f;
        [SerializeField, Min(0f)] private float _maximumImpulse = 6f;
        [SerializeField, Min(0f), Tooltip("Minimum directional angular jerk in rad/s; off-centre hits may reach the angular cap.")] private float _angularSpeed = 2.2f;
        [SerializeField, Min(0f)] private float _maximumAngularChange = 3f;
        [SerializeField, Min(0f)] private float _muscleReleaseDuration = .14f;
        [SerializeField, Min(.01f)] private float _muscleRecoveryDuration = .12f;
        [SerializeField, Range(0f, 1f)] private float _hitMuscleStrength = .1f;
        private Collider[] _anatomy;
        private Transform _owner;
        [SerializeField, Range(0f, 1f), Tooltip("Small neck recoil accompanying a torso hit.")] private float _torsoHeadReaction = .6f;
        private Rigidbody _reactionParent, _head, _chest, _secondaryBody;
        private bool _hasReaction;
        public Rigidbody HitBody { get; private set; }
        public Vector3 AppliedImpulse { get; private set; }
        public Vector3 AppliedAngularChange { get; private set; }
        public Vector3 HitPoint { get; private set; }
        public float MaximumImpulse => _maximumImpulse;

        public void Initialize(Transform owner, Animator animator, Transform skeleton)
        {
            if (_anatomy != null) return;
            _owner = owner;
            _head = animator.GetBoneTransform(HumanBodyBones.Head)?.GetComponent<Rigidbody>();
            _chest = animator.GetBoneTransform(HumanBodyBones.Chest)?.GetComponent<Rigidbody>();
            Collider[] candidates = skeleton.GetComponentsInChildren<Collider>(true);
            CharacterJoint[] joints = skeleton.GetComponentsInChildren<CharacterJoint>(true);
            // The extra skeleton-root hitbox has no anatomical joint. Keep it out
            // of bone refinement so it cannot mask the head/shoulder behind it.
            var valid = new Collider[candidates.Length];
            int count = 0;
            foreach (var candidate in candidates)
            {
                var body = candidate.attachedRigidbody;
                if (body == null) continue;
                bool anatomical = false;
                foreach (var joint in joints)
                    if (joint.GetComponent<Rigidbody>() == body || joint.connectedBody == body) { anatomical = true; break; }
                if (anatomical) valid[count++] = candidate;
            }
            _anatomy = new Collider[count];
            Array.Copy(valid, _anatomy, count);
        }

        public void Resolve(Collider sourceCollider, Vector3 point, Vector3 direction)
        {
            Reset();
            if (sourceCollider == null || !sourceCollider.transform.IsChildOf(_owner) || direction.sqrMagnitude < .0001f) return;
            Collider hit = ResolveAnatomy(sourceCollider, point, direction.normalized, out Vector3 refinedPoint);
            if (hit == null || hit.attachedRigidbody.isKinematic) return;
            HitBody = hit.attachedRigidbody; HitPoint = refinedPoint;
            var joint = HitBody.GetComponent<CharacterJoint>();
            _reactionParent = joint != null ? joint.connectedBody : null;
        }

        public void Apply(Collider sourceCollider, Vector3 point, Vector3 direction)
        {
            Resolve(sourceCollider, point, direction);
            if (HitBody == null) return;
            Vector3 refinedPoint = HitPoint;
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction.normalized, Vector3.up);
            if (planarDirection.sqrMagnitude < .0001f) return;
            planarDirection.Normalize();
            AppliedImpulse = planarDirection * Mathf.Min(HitBody.mass * _linearVelocityChange, _maximumImpulse);
            HitBody.AddForce(AppliedImpulse, ForceMode.Impulse);
            Vector3 angularImpulse = Vector3.Cross(refinedPoint - HitBody.worldCenterOfMass, AppliedImpulse);
            Quaternion inertiaFrame = HitBody.rotation * HitBody.inertiaTensorRotation;
            Vector3 localImpulse = Quaternion.Inverse(inertiaFrame) * angularImpulse;
            Vector3 inertia = HitBody.inertiaTensor;
            Vector3 angularChange = inertiaFrame * new Vector3(localImpulse.x / Mathf.Max(inertia.x, .0001f),
                localImpulse.y / Mathf.Max(inertia.y, .0001f), localImpulse.z / Mathf.Max(inertia.z, .0001f));
            // A centre-line hit still produces a short bodily recoil. Bound the
            // lever-arm contribution so a tiny head inertia cannot create a spin.
            angularChange = Vector3.ClampMagnitude(angularChange, _maximumAngularChange * .5f);
            angularChange += Vector3.Cross(Vector3.up, planarDirection) * _angularSpeed;
            AppliedAngularChange = Vector3.ClampMagnitude(angularChange, _maximumAngularChange);
            HitBody.AddTorque(AppliedAngularChange, ForceMode.VelocityChange);
            if (HitBody == _chest && _head != null && _angularSpeed > 0f && _torsoHeadReaction > 0f)
            {
                _secondaryBody = _head;
                _head.AddTorque(Vector3.Cross(Vector3.up, planarDirection) * (Mathf.Min(_angularSpeed, _maximumAngularChange) * _torsoHeadReaction), ForceMode.VelocityChange);
            }
            _hasReaction = AppliedImpulse.sqrMagnitude + AppliedAngularChange.sqrMagnitude > .0001f;
        }

        private Collider ResolveAnatomy(Collider source, Vector3 point, Vector3 direction, out Vector3 refinedPoint)
        {
            foreach (var candidate in _anatomy)
                if (candidate == source) { refinedPoint = candidate.ClosestPoint(point); return candidate; }
            var ray = new Ray(point - direction * RayBackstep, direction);
            Collider closest = null;
            float distance = AnatomyRayLength;
            refinedPoint = point;
            foreach (var candidate in _anatomy)
                if (candidate.enabled && candidate.Raycast(ray, out RaycastHit hit, distance))
                { closest = candidate; distance = hit.distance; refinedPoint = hit.point; }
            if (closest != null) return closest;
            // Capsule grazing hits can miss the simplified bone geometry. Map
            // their real surface point conservatively rather than invent a head hit.
            float squaredDistance = MaximumPointDistance * MaximumPointDistance;
            foreach (var candidate in _anatomy)
            {
                if (!candidate.enabled) continue;
                Vector3 candidatePoint = candidate.ClosestPoint(point);
                float squared = (candidatePoint - point).sqrMagnitude;
                if (squared >= squaredDistance) continue;
                squaredDistance = squared; closest = candidate; refinedPoint = candidatePoint;
            }
            return closest;
        }

        public float MuscleWeight(Rigidbody body, float elapsed)
        {
            if (!_hasReaction || body != HitBody && body != _reactionParent && body != _secondaryBody) return 1f;
            float recovery = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - _muscleReleaseDuration) / _muscleRecoveryDuration));
            return Mathf.Lerp(_hitMuscleStrength, 1f, recovery);
        }

        public void Reset()
        {
            HitBody = _reactionParent = _secondaryBody = null; _hasReaction = false;
            AppliedImpulse = AppliedAngularChange = HitPoint = Vector3.zero;
        }
    }
}
