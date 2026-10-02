using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyDebugView : MonoBehaviour
    {
        [SerializeField] private EnemyDebugSettings _settings = new EnemyDebugSettings();
        private EnemyBrain _brain;
        private EnemyActor _actor;
        private EnemyAnimationBridge _animation;
        private bool _animationOwned;
        private bool _bound;
        private float _nextSample;
        private bool _visible;
        private bool _lane;
        private NavigationResult _navigation;
        private EnemyCoverPoint _cover;
        private EnemySquadRole _role;
        private readonly Vector3[] _corners = new Vector3[32];
        public EnemyDebugSettings Settings => _settings;
        private void Awake() { _brain = GetComponent<EnemyBrain>(); _actor = GetComponent<EnemyActor>(); _animation = GetComponent<EnemyAnimationBridge>(); }
        private void Start() => Bind();
        private void OnEnable() { if (_brain != null && _brain.States != null) Bind(); }
        private void Bind()
        {
            if (_bound || _brain == null || _brain.States == null) return;
            _brain.States.Changed += StateChanged;
            _brain.ResetCompleted += Reset;
            _brain.Combat.Fired += Fired;
            _brain.Combat.ReloadStarted += Reload;
            if (_brain.Tactics != null) _brain.Tactics.DecisionChanged += Decision;
            _bound = true;
        }
        private void OnDisable()
        {
            if (_animationOwned && _animation != null) _animation.SetAnimationLogging(false); _animationOwned = false;
            if (!_bound) return;
            _brain.States.Changed -= StateChanged;
            _brain.ResetCompleted -= Reset;
            _brain.Combat.Fired -= Fired;
            _brain.Combat.ReloadStarted -= Reload;
            if (_brain.Tactics != null) _brain.Tactics.DecisionChanged -= Decision;
            _bound = false;
        }
        private bool Logs(EnemyDebugCategory category) => _settings.Enabled && (_settings.Logs & category) != 0;
        private void Write(EnemyDebugCategory category, string message) =>
            Debug.Log("[Enemy:" + name + "][Squad:" + _actor.SquadId + "][" + category + "] " + message, this);
        private void StateChanged(EnemyStateId state, string reason)
        {
            var category = state == EnemyStateId.Dead ? EnemyDebugCategory.Death : EnemyDebugCategory.State;
            if (Logs(category)) Write(category, _brain.States.Previous + " -> " + state + " | " + reason);
        }
        private void Reset() { if (Logs(EnemyDebugCategory.Reset)) Write(EnemyDebugCategory.Reset, "Spawn state and resources reset"); }
        private void Fired() { if (Logs(EnemyDebugCategory.Combat)) Write(EnemyDebugCategory.Combat, "Shot | ammo=" + _brain.Combat.Ammo); }
        private void Reload() { if (Logs(EnemyDebugCategory.Combat)) Write(EnemyDebugCategory.Combat, "Reload started"); }
        private void Decision(EnemyTacticalCandidate intent, TacticalReason reason)
        {
            if (Logs(EnemyDebugCategory.Decision)) Write(EnemyDebugCategory.Decision, intent.Intent + " | score=" + intent.Score.ToString("F1") + " | reason=" + reason + " | destination=" + intent.Destination);
        }
        private void Update()
        {
            bool animationLogs = Logs(EnemyDebugCategory.Animation);
            if (_animation != null && (_animationOwned || animationLogs)) _animation.SetAnimationLogging(animationLogs);
            _animationOwned = animationLogs;
            if (!_settings.Enabled || !_bound || Time.time < _nextSample) return;
            _nextSample = Time.time + 0.25f;
            var tactics = _brain.Tactics;
            bool visible = _brain.Memory.Visible; bool lane = tactics != null && tactics.FireLane;
            if ((_visible != visible || _lane != lane) && Logs(EnemyDebugCategory.Perception))
                Write(EnemyDebugCategory.Perception, "Direct sight=" + visible + " | firing lane=" + lane);
            _visible = visible; _lane = lane;
            var navigation = _actor.Navigation.Result;
            if (_navigation != navigation && Logs(EnemyDebugCategory.Navigation)) Write(EnemyDebugCategory.Navigation, navigation + " | " + _actor.Navigation.Destination);
            if (_navigation != navigation && _actor.Navigation.Failed && Logs(EnemyDebugCategory.Warning)) Write(EnemyDebugCategory.Warning, "Navigation fallback: " + navigation);
            _navigation = navigation;
            if (tactics == null) return;
            if (_cover != tactics.CurrentCover && Logs(EnemyDebugCategory.Cover)) Write(EnemyDebugCategory.Cover, (_cover != null ? _cover.name : "none") + " -> " + (tactics.CurrentCover != null ? tactics.CurrentCover.name : "none") + " | " + tactics.LastDecisionReason);
            _cover = tactics.CurrentCover;
            if (tactics.Member != null && _role != tactics.Member.Role && Logs(EnemyDebugCategory.Squad)) Write(EnemyDebugCategory.Squad, _role + " -> " + tactics.Member.Role);
            if (tactics.Member != null) _role = tactics.Member.Role;
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_settings.Enabled) return;
            if (_brain == null) _brain = GetComponent<EnemyBrain>();
            if (_actor == null) _actor = GetComponent<EnemyActor>();
            if (_brain == null || _brain.Config == null || _brain.Memory == null) return;
            Vector3 eye = _actor.Eyes.position;
            var tactics = _brain.Tactics;
            Gizmos.color = _brain.Memory.Visible ? Color.red : Color.yellow;
            if (_settings.FieldOfView)
            {
                float angle = _brain.Config.Perception.ViewAngle * 0.5f;
                float range = _brain.Config.Perception.ViewRange;
                Gizmos.DrawLine(eye, eye + Quaternion.Euler(0, -angle, 0) * transform.forward * range);
                Gizmos.DrawLine(eye, eye + Quaternion.Euler(0, angle, 0) * transform.forward * range);
                UnityEditor.Handles.DrawWireArc(eye, Vector3.up, Quaternion.Euler(0, -angle, 0) * transform.forward, angle * 2, range);
            }
            if (_settings.Hearing) Gizmos.DrawWireSphere(transform.position, _brain.Config.Perception.HearingRange);
            if (_settings.SightAndFire && _brain.Memory.HasContact)
            {
                Gizmos.DrawLine(eye, _brain.Memory.KnownAimPosition);
                Gizmos.color = tactics != null && tactics.FireLane ? Color.green : Color.magenta;
                Gizmos.DrawLine(_actor.Muzzle.position, _brain.Memory.KnownAimPosition);
            }
            if (_settings.Memory && _brain.Memory.HasContact) Gizmos.DrawWireSphere(_brain.Memory.LastKnownPosition, 0.4f);
            if (_settings.Path)
            {
                Gizmos.color = Color.cyan;
                int count = _actor.Navigation.CopyPathCorners(_corners);
                for (int i = 1; i < count; i++) Gizmos.DrawLine(_corners[i - 1], _corners[i]);
                Gizmos.DrawWireSphere(_actor.Navigation.Destination, 0.25f);
            }
            if (tactics != null && _settings.Cover)
                foreach (var point in tactics.CoverPoints)
                {
                    if (point == null) continue;
                    var reservation = tactics.Reservation(point);
                    Gizmos.color = reservation != null ? Color.red : point.Kind == EnemyCoverKind.Low ? Color.green : Color.blue;
                    Gizmos.DrawWireCube(point.ProtectedPosition + Vector3.up * 0.5f, Vector3.one);
                    Gizmos.DrawLine(point.ProtectedPosition, point.ProtectedPosition + point.Normal * 2);
                    Gizmos.DrawLine(point.ProtectedPosition, point.ExposurePosition);
                    UnityEditor.Handles.Label(point.ProtectedPosition + Vector3.up, point.name + " | " + (reservation != null ? reservation.Owner.Actor.name + "/" + reservation.Phase : "available"));
                }
            if (tactics != null && _settings.Cover)
                for (int i = 0; i < tactics.Planner.CoverEvaluationCount; i++)
                {
                    var evaluation = tactics.Planner.EvaluatedCovers[i];
                    if (evaluation.Point == null) continue;
                    Gizmos.color = evaluation.Rating.Valid ? Color.green : Color.gray;
                    Gizmos.DrawWireSphere(evaluation.Point.ProtectedPosition, 0.6f);
                    UnityEditor.Handles.Label(evaluation.Point.ProtectedPosition + Vector3.up * 1.5f, evaluation.Rating.Score.ToString("F1") + " / " + evaluation.Rating.Rejection);
                }
            if (tactics != null && _settings.Candidates)
                for (int i = 0; i < tactics.Planner.Count; i++)
                {
                    var candidate = tactics.Planner.Candidates[i];
                    Gizmos.color = candidate.Valid ? Color.green : Color.gray;
                    Gizmos.DrawLine(transform.position, candidate.Destination);
                    Gizmos.DrawWireSphere(candidate.Destination, 0.3f);
                    UnityEditor.Handles.Label(candidate.Destination + Vector3.up, candidate.Intent + "/" + candidate.Score.ToString("F1") + "/" + candidate.Reason);
                }
            if (tactics != null && tactics.Squad != null && _settings.SquadLinks)
                foreach (var member in tactics.Squad.Members)
                    if (member.Alive) { Gizmos.color = member.Shooter ? Color.red : member.Mover ? Color.cyan : Color.gray; Gizmos.DrawLine(eye, member.Context.Actor.Eyes.position); }
            if (_settings.Labels)
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2.3f, _brain.States.Previous + " -> " + _brain.States.Current + "\n" + _brain.States.LastReason + "\nHP " + _actor.Health.CurrentHealth + " | Ammo " + _brain.Combat.Ammo + (tactics != null ? "\n" + tactics.Current.Intent + " | " + tactics.Knowledge : ""));
        }
#endif
    }
}