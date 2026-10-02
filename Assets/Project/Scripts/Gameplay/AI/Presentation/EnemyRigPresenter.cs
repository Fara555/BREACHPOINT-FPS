using UnityEngine;
using UnityEngine.Animations.Rigging;

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
        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>(); _actor = GetComponent<EnemyActor>();
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
            // Rig layer activity bypasses Animator stream defaults that overwrite rig weights.
            // The controller blends action poses; targets and constraint calibration stay intact.
            _aimLayer.active = _aimActive && !dead;
            _handLayer.active = _handActive && !dead && !_brain.Combat.IsReloading;
        }
        private void ResetLayers()
        { if (_ready) { _aimLayer.active = _aimActive; _handLayer.active = _handActive; } }
        private void OnDisable()
        { if (_brain != null) _brain.ResetCompleted -= ResetLayers; ResetLayers(); }
    }
}
