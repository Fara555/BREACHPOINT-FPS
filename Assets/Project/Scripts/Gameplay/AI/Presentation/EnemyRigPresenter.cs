using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Breachpoint.Gameplay.AI
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyRigPresenter : MonoBehaviour
    {
        [SerializeField] private Rig _aimRig;
        [SerializeField] private Rig _leftHandRig;
        private EnemyBrain _brain;
        private EnemyActor _actor;
        private RigLayer _aimLayer;
        private RigLayer _handLayer;
        private bool _aimActive;
        private bool _handActive;
        private bool _ready;
        private RigBuilder _builder;
        private EnemyAnimationBridge _bridge;
        private TwoBoneIKConstraint _handIk;
        private Transform _chest;
        private Vector3 _originalHint;
        private float _handWeight = 1f;
        private float _aimWeight = 1f;
        private AnimationScriptPlayable _hintPlayable;
        private Vector3 _hintOffset;
        private float _coarseWeight;
        public float HandWeight => _handWeight;
        public float AimRigWeight => _aimActive ? _aimWeight : 0f;
        public float CoarseWeight => _coarseWeight;
        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>(); _actor = GetComponent<EnemyActor>();
            _bridge = GetComponent<EnemyAnimationBridge>();
            RigBuilder builder = _aimRig != null ? _aimRig.GetComponentInParent<RigBuilder>() : null;
            if (builder != null)
                foreach (RigLayer layer in builder.layers)
                {
                    if (layer.rig == _aimRig) _aimLayer = layer;
                    if (layer.rig == _leftHandRig) _handLayer = layer;
                }
            if (_aimLayer == null || _handLayer == null)
            { Debug.LogError($"{name}: EnemyRigPresenter requires the existing authored AimRig and LeftHandRig layers.", this); enabled = false; return; }
            _aimActive = _aimLayer.active; _handActive = _handLayer.active; _ready = true;
            _builder = builder;
            _handIk = _leftHandRig.GetComponentInChildren<TwoBoneIKConstraint>();
            _chest = _bridge.Animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_handIk != null && _handIk.data.hint != null) _originalHint = _handIk.data.hint.localPosition;
        }
        private void OnEnable()
        {
            if (!_ready) return;
            _brain.ResetCompleted += ResetLayers; ResetLayers();
        }
        private void Update()
        {
            if (!_ready || _brain.Combat == null || _actor.Health == null) return;
            bool dead = _actor.Health.IsDead;
            EnemyAnimationConfig config = _bridge.Config;
            _handWeight = dead ? 0f : Mathf.MoveTowards(_handWeight, _brain.Combat.IsReloading ? 0f : 1f, Time.deltaTime / Mathf.Max(0.01f, config.IkBlend));
            // A passive turn must retain its authored torso weight shift instead of counter-aiming.
            bool passiveTurn = _brain.States.Group == EnemyStateGroup.Passive && _actor.Navigation.IsTurning;
            float aimRigTarget = passiveTurn ? 0f : Mathf.Lerp(1f, config.SprintAimRigWeight, _bridge.SprintPoseWeight);
            _aimWeight = dead ? 0f : Mathf.MoveTowards(_aimWeight, aimRigTarget, Time.deltaTime / Mathf.Max(0.01f, config.AimBlend));
            _aimLayer.active = _aimActive && _aimWeight > 0f;
            _handLayer.active = _handActive && _handWeight > 0f;
            // Blend the existing rig outputs, outside Animator's default property stream.
            // This keeps constraint weights and the calibrated hand grip unchanged.
            if (_builder.graph.IsValid() && _builder.graph.GetOutputCount() == 3)
            {
                _builder.graph.GetOutput(1).SetWeight(_aimWeight);
                _builder.graph.GetOutput(2).SetWeight(_handWeight);
                if (!_hintPlayable.IsValid() && _chest != null && _handIk != null && _handIk.data.hint != null)
                {
                    var job = new ElbowHintJob
                    {
                        Chest = _bridge.Animator.BindStreamTransform(_chest),
                        Hint = _bridge.Animator.BindStreamTransform(_handIk.data.hint),
                        Muzzle = _bridge.Animator.BindStreamTransform(_actor.Muzzle),
                        Target = _bridge.Animator.BindSceneTransform(_bridge.AimTarget)
                    };
                    _hintPlayable = AnimationScriptPlayable.Create(_builder.graph, job);
                    // One authored hand constraint: supply its hint after AimRig in the same stream.
                    var constraint = _builder.graph.GetOutput(2).GetSourcePlayable();
                    constraint.AddInput(_hintPlayable, 0, 1f);
                }
            }
            if (!dead && _hintPlayable.IsValid())
            {
                Vector3 offset = _bridge.IsCrouching ? config.CrouchElbowFromChest : config.ElbowFromChest;
                _hintOffset = Vector3.Lerp(_hintOffset, offset, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, config.HintBlend)));
                var job = _hintPlayable.GetJobData<ElbowHintJob>(); job.Offset = transform.TransformDirection(_hintOffset);
                _coarseWeight = Mathf.MoveTowards(_coarseWeight, config.CoarseAimWeight * _bridge.AimPresentationWeight, Time.deltaTime / config.CoarseAimBlend);
                job.Raising = _bridge.IsRaising;
                job.AimWeight = _coarseWeight;
                job.YawLimit = config.CoarseAimYawLimit;
                job.PitchLimit = config.CoarseAimPitchLimit;
                job.PitchWeight = config.CoarseAimPitchWeight * Mathf.Clamp01(_coarseWeight / Mathf.Max(.01f, config.CoarseAimWeight)) ;
                _hintPlayable.SetJobData(job);
            }
        }
        private void ResetLayers()
        {
            if (!_ready) return;
            _handWeight = _aimWeight = 1f; _aimLayer.active = _aimActive; _handLayer.active = _handActive;
            _hintOffset = _bridge.Config.ElbowFromChest;
            _coarseWeight = 0f;
            if (_handIk != null && _handIk.data.hint != null) _handIk.data.hint.localPosition = _originalHint;
            if (_builder.graph.IsValid() && _builder.graph.GetOutputCount() == 3)
            { _builder.graph.GetOutput(1).SetWeight(1f); _builder.graph.GetOutput(2).SetWeight(1f); }
        }
        private void OnDisable()
        { if (_brain != null) _brain.ResetCompleted -= ResetLayers; ResetLayers(); }

        private struct ElbowHintJob : IAnimationJob
        {
            public TransformStreamHandle Chest;
            public TransformStreamHandle Hint;
            public TransformStreamHandle Muzzle;
            public TransformSceneHandle Target;
            public Vector3 Offset;
            public float AimWeight;
            public float YawLimit;
            public float PitchLimit;
            public float PitchWeight;
            public bool Raising;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if (!Chest.IsValid(stream)) return;
                if (AimWeight > 0f && Muzzle.IsValid(stream) && Target.IsValid(stream))
                {
                    Vector3 forward = Muzzle.GetRotation(stream) * Vector3.forward;
                    Vector3 direction = Target.GetPosition(stream) - Muzzle.GetPosition(stream);
                    direction.Normalize();
                    float pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg - Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
                    // Early Raise owns the upward gesture. Correct overshoot, not its low carry pose.
                    if (Raising) pitch = Mathf.Max(0f, pitch);
                    Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up), horizontalTarget = Vector3.ProjectOnPlane(direction, Vector3.up);
                    if (horizontalForward.sqrMagnitude > .0001f && horizontalTarget.sqrMagnitude > .0001f)
                    {
                        float yaw = Vector3.SignedAngle(horizontalForward, horizontalTarget, Vector3.up);
                        // Fade outside the forward engagement cone. Clamping +/-180 directly
                        // flips the correction sign while a stationary turn crosses the target.
                        float coneWeight = 1f - Mathf.InverseLerp(60f, 100f, Mathf.Abs(yaw));
                        Vector3 right = Vector3.Cross(Vector3.up, horizontalForward).normalized;
                        Quaternion pitchRotation = Quaternion.AngleAxis(Mathf.Clamp(pitch, -PitchLimit, PitchLimit) * PitchWeight * coneWeight, right);
                        Chest.SetRotation(stream, pitchRotation * Chest.GetRotation(stream));
                        // Pitch changes the chest-relative barrel position. Measure yaw again
                        // from the updated stream before applying the existing bounded yaw gain.
                        horizontalForward = Vector3.ProjectOnPlane(Muzzle.GetRotation(stream) * Vector3.forward, Vector3.up);
                        horizontalTarget = Vector3.ProjectOnPlane(Target.GetPosition(stream) - Muzzle.GetPosition(stream), Vector3.up);
                        yaw = Vector3.SignedAngle(horizontalForward, horizontalTarget, Vector3.up);
                        coneWeight = 1f - Mathf.InverseLerp(60f, 100f, Mathf.Abs(yaw));
                        Quaternion yawRotation = Quaternion.AngleAxis(Mathf.Clamp(yaw, -YawLimit, YawLimit) * AimWeight * coneWeight, Vector3.up);
                        Chest.SetRotation(stream, yawRotation * Chest.GetRotation(stream));
                    }
                }
                if (Hint.IsValid(stream)) Hint.SetPosition(stream, Chest.GetPosition(stream) + Offset);
            }
        }
    }
}
