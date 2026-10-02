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
        public float HandWeight => _handWeight;
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
            _aimWeight = dead ? 0f : Mathf.MoveTowards(_aimWeight, 1f, Time.deltaTime / Mathf.Max(0.01f, config.AimBlend));
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
                        Hint = _bridge.Animator.BindStreamTransform(_handIk.data.hint)
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
                var job = _hintPlayable.GetJobData<ElbowHintJob>(); job.Offset = transform.TransformDirection(_hintOffset); _hintPlayable.SetJobData(job);
            }
        }
        private void ResetLayers()
        {
            if (!_ready) return;
            _handWeight = _aimWeight = 1f; _aimLayer.active = _aimActive; _handLayer.active = _handActive;
            _hintOffset = _bridge.Config.ElbowFromChest;
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
            public Vector3 Offset;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if (Chest.IsValid(stream) && Hint.IsValid(stream)) Hint.SetPosition(stream, Chest.GetPosition(stream) + Offset);
            }
        }
    }
}
