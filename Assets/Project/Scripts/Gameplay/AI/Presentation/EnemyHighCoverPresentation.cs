using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [DefaultExecutionOrder(-110), DisallowMultipleComponent, RequireComponent(typeof(EnemyAnimationBridge))]
    public sealed class EnemyHighCoverPresentation : MonoBehaviour
    {
        private const float WallSkinGap = .015f;
        private const float HeadEdgeInset = .10f;
        private static readonly int _takingStateHash = Animator.StringToHash("TakingHighCover");
        private static readonly int _idleStateHash = Animator.StringToHash("HighCoverIdle");
        private static readonly int _leavingStateHash = Animator.StringToHash("LeavingHighCover");
        private static int Phase(AnimatorStateInfo pose) => pose.shortNameHash == _takingStateHash ? 0 :
            pose.shortNameHash == _idleStateHash ? 1 : pose.shortNameHash == _leavingStateHash ? 2 : -1;
        [SerializeField] private EnemyHighCoverAnimationProfile _profile;
        private EnemyBrain _brain;
        private EnemyActor _actor;
        private EnemyAnimationBridge _bridge;
        private EnemyNavigation _navigation;
        private CapsuleCollider _capsule;
        private EnemyStance _stance;
        private Vector3 _baseVisualPosition, _baseEyesPosition, _baseCapsuleCenter;
        private readonly EnemyPhysics _physics = new EnemyPhysics();
        private EnemyCoverPoint _point;
        private Vector3 _startVisualWorld, _lastRenderWorld, _contactOffset, _entryResidual;
        private bool _taking, _action, _offsetApplied, _groundAfterExit;
        public bool IsConfigured => _profile != null && _profile.IsValid;
        public bool CanDriveMotion => IsConfigured && isActiveAndEnabled && _bridge != null && _bridge.enabled && _bridge.Animator.enabled && _navigation != null && _navigation.Ready;
        public Vector3 VisualOffset { get; private set; }
        public Vector3 AppliedOffset { get; private set; }
        public float GroundHeight { get; private set; }
        public float NormalizedMotion { get; private set; }
        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>(); _actor = GetComponent<EnemyActor>();
            _bridge = GetComponent<EnemyAnimationBridge>(); _navigation = GetComponent<EnemyNavigation>();
            _capsule = GetComponent<CapsuleCollider>(); _stance = GetComponent<EnemyStance>();
            _baseVisualPosition = _bridge.Animator.transform.localPosition;
            _baseEyesPosition = _actor.Eyes.localPosition;
            if (_capsule != null) _baseCapsuleCenter = _capsule.center;
        }
        private void OnEnable() => _brain.ResetCompleted += ResetPresentation;
        private Vector3 Frame(EnemyCoverPoint point, Vector3 local) => point.transform.right * local.x + point.Normal * local.z;
        private Vector3 ContactOffset(EnemyCoverPoint point)
        {
            Vector2 reference = _profile.Reference(point.ExposesRight);
            float sign = point.ExposesRight ? 1f : -1f;
            return point.Normal * (point.WallClearance - reference.x - WallSkinGap) + point.transform.right * (sign * (point.CornerInset - HeadEdgeInset) - reference.y);
        }
        public Vector3 EntryPosition(EnemyCoverPoint point) => point.ProtectedPosition + ContactOffset(point) - Frame(point, _profile.Motion(true, point.ExposesRight, 1f));
        public void Begin(EnemyCoverPoint point, bool taking)
        {
            if (!CanDriveMotion) return;
            _point = point; _taking = taking; _action = true;
            _contactOffset = ContactOffset(point);
            _startVisualWorld = _lastRenderWorld = _bridge.Animator.transform.position;
            _entryResidual = taking ? Vector3.ProjectOnPlane(point.ProtectedPosition + _contactOffset - _startVisualWorld - Frame(point, _profile.Motion(true, point.ExposesRight, 1f)), Vector3.up) : Vector3.zero;
            NormalizedMotion = 0f; _navigation.BeginPresentationMotion();
        }
        private void LateUpdate()
        {
            if (!IsConfigured || _actor.Health == null || _actor.Health.IsDead || !_bridge.Animator.enabled) return;
            var animator = _bridge.Animator;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            float blend = transition ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0f;
            int currentPhase = Phase(state), nextPhase = transition ? Phase(next) : -1;
            if (currentPhase == 2 || nextPhase == 2) _groundAfterExit = true;
            float weight = currentPhase >= 0 ? nextPhase >= 0 || !transition ? 1f : 1f - blend : nextPhase >= 0 ? blend : 0f;
            bool ownsCover = _brain.enabled && _brain.Tactics != null && _brain.Tactics.CurrentCover == _point && _brain.Tactics.Current.Valid && CanDriveMotion && _brain.States.Group != EnemyStateGroup.Disabled;
            if (_action && !ownsCover) { _action = false; _navigation.EndPresentationMotion(); }
            if (_action && _point != null)
            {
                int wanted = _taking ? 0 : 2;
                bool active = currentPhase == wanted || nextPhase == wanted;
                if (active)
                {
                    float time = currentPhase == wanted ? state.normalizedTime : next.normalizedTime;
                    NormalizedMotion = Mathf.Clamp01(time);
                    float progress = _profile.Progress(_taking, _point.ExposesRight, NormalizedMotion);
                    Vector3 renderTarget = _startVisualWorld + Frame(_point, _profile.Motion(_taking, _point.ExposesRight, NormalizedMotion)) + _entryResidual * progress;
                    Vector3 intendedOffset = _contactOffset * (_taking ? progress : 1f - progress);
                    // Facing rotates the previous local visual offset before LateUpdate.
                    // Measure the rendered path itself, excluding that transient rotation.
                    Vector3 bodyVelocity = Time.deltaTime > 0f ? Vector3.ProjectOnPlane(renderTarget - _lastRenderWorld, Vector3.up) / Time.deltaTime : Vector3.zero;
                    _lastRenderWorld = renderTarget;
                    _navigation.ApplyPresentationMotion(renderTarget - intendedOffset, bodyVelocity, Time.deltaTime);
                    Vector3 baseWorld = animator.transform.parent.TransformPoint(_baseVisualPosition);
                    VisualOffset = Vector3.ProjectOnPlane(renderTarget - baseWorld, Vector3.up);
                }
                else if (_brain.Tactics.CurrentCover == _point && _brain.Tactics.CoverPhase == EnemyCoverActionPhase.Protected)
                    VisualOffset = _contactOffset;
                else if (weight <= 0f)
                { _action = false; _point = null; _navigation.EndPresentationMotion(); VisualOffset = Vector3.zero; }
            }
            if (weight <= 0f && !_groundAfterExit) { if (_offsetApplied) RestoreOffsets(); return; }
            bool right = _point != null ? _point.ExposesRight : animator.GetFloat("HighCoverSide") > .5f;
            float sole = currentPhase >= 0 ? _profile.SoleHeight(currentPhase, right, state.normalizedTime) : _profile.StandingSoleHeight;
            if (transition)
            {
                float nextSole = nextPhase >= 0 ? _profile.SoleHeight(nextPhase, right, next.normalizedTime) : _profile.StandingSoleHeight;
                if (_groundAfterExit || currentPhase >= 0 && nextPhase >= 0) sole = Mathf.Lerp(sole, nextSole, blend);
                else if (currentPhase < 0) sole = nextSole;
            }
            if (_physics.TryFirstHit(transform.position + VisualOffset + Vector3.up * .6f, Vector3.down, 2f, _brain.Config.Perception.ObstructionMask, transform, out var hit)) GroundHeight = hit.point.y;
            Vector3 offset = VisualOffset * (_action ? 1f : weight);
            // The baked NavMesh root can sit above the physical floor. Keep the
            // rest correction across the handoff instead of raising the whole body.
            offset.y = (GroundHeight - transform.position.y - sole) * (_groundAfterExit ? 1f : weight);
            ApplyOffsets(offset);
        }
        private void ApplyOffsets(Vector3 worldOffset)
        {
            AppliedOffset = worldOffset;
            var visual = _bridge.Animator.transform;
            visual.localPosition = _baseVisualPosition + visual.parent.InverseTransformVector(worldOffset);
            if (!_actor.Eyes.IsChildOf(visual)) _actor.Eyes.localPosition = (_stance != null ? _stance.EyesPositionForCurrentStance : _baseEyesPosition) + _actor.Eyes.parent.InverseTransformVector(worldOffset);
            if (_capsule != null && (_point != null || _groundAfterExit))
            {
                Vector3 colliderOffset = worldOffset;
                if (_point != null)
                {
                    float distance = _point.WallClearance + Vector3.Dot(_point.ProtectedPosition - transform.position, _point.Normal);
                    float radius = _capsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
                    float toward = Vector3.Dot(colliderOffset, _point.Normal);
                    colliderOffset += _point.Normal * (Mathf.Min(toward, Mathf.Max(0f, distance - radius - WallSkinGap)) - toward);
                }
                colliderOffset.y = GroundHeight - transform.position.y;
                _capsule.center = (_stance != null ? _stance.BodyCenterForCurrentStance : _baseCapsuleCenter) + transform.InverseTransformVector(colliderOffset);
            }
            _offsetApplied = true;
        }
        private void RestoreOffsets()
        {
            _bridge.Animator.transform.localPosition = _baseVisualPosition;
            if (_stance != null) _stance.SetCrouching(_stance.IsCrouching);
            else
            {
                _actor.Eyes.localPosition = _baseEyesPosition;
                if (_capsule != null) _capsule.center = _baseCapsuleCenter;
            }
            _offsetApplied = false; AppliedOffset = Vector3.zero;
        }
        private void ResetPresentation()
        {
            _action = false;
            _point = null;
            _groundAfterExit = false;
            VisualOffset = Vector3.zero;
            NormalizedMotion = 0f;
            _navigation.EndPresentationMotion();
            RestoreOffsets();
        }
        private void OnDisable()
        {
            _brain.ResetCompleted -= ResetPresentation;
            if (_actor.Health == null || !_actor.Health.IsDead) ResetPresentation();
        }
    }
}
