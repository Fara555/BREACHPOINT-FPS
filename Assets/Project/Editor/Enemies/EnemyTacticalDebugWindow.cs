using System.Collections.Generic;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public sealed class EnemyTacticalDebugWindow : EditorWindow
    {
        private int _scenario;
        private Vector2 _scroll;
        private AnimatorController _controller;
        private readonly Dictionary<int, string> _states = new Dictionary<int, string>();
        private int _tacticalScenario;
        private int _reviewScenario;
        private bool _global;
        private string _validationStatus = "No validation selected";
        private double _nextSnapshot;
        private double _lastSnapshot;
        private EnemyBrain[] _brains = new EnemyBrain[0];
        private int _lastChecks, _lastDecisions, _lastRepaths;
        private float _checksRate, _decisionsRate, _repathsRate;
        [MenuItem("Breachpoint/Enemies/AI Test / Tactical Debug/Open debug and scenarios")]
        public static void Open() => GetWindow<EnemyTacticalDebugWindow>("Enemy AI / Tactical");
        private void OnInspectorUpdate()
        {
            int stage = SessionState.GetInt("AdamPresentation.Validation.Stage", 70);
            string path = "Docs/AI/AdamPresentation/stage-" + stage + "-play.txt";
            if (System.IO.File.Exists(path))
            {
                string[] lines = System.IO.File.ReadAllLines(path);
                string result = "RUNNING";
                string checkpoint = "";
                foreach (string line in lines)
                {
                    if (line.StartsWith("RESULT:")) result = line;
                    if (line.StartsWith("SCENARIO ") || line.StartsWith("TACTICAL SCENARIO ") || line.StartsWith("MEASUREMENT ")) checkpoint = line;
                    if (line.StartsWith("FAIL ")) checkpoint = line;
                }
                _validationStatus = "Stage " + stage + " | " + result + "\n" + checkpoint;
            }
            Repaint();
        }
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(_validationStatus, _validationStatus.Contains("FAIL") ? MessageType.Error : MessageType.Info);
            EditorGUILayout.LabelField("Selected enemy", EditorStyles.boldLabel);
            EnemyBrain brain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<EnemyBrain>() : null;
            if (brain == null) EditorGUILayout.HelpBox("Select a Rifleman gameplay root or child in Play Mode.", MessageType.Info);
            else if (brain.Config != null)
            {
                EnemyActor actor = brain.GetComponent<EnemyActor>();
                EnemyAnimationBridge bridge = brain.GetComponent<EnemyAnimationBridge>();
                Show("Entity ID", actor.GetEntityId().ToString());
                Show("Enemy / state", brain.name + " / " + brain.States.Current);
                Show("Previous state", brain.States.Previous.ToString());
                Show("Transition reason", brain.States.LastReason);
                Show("Alert", brain.Memory.Alert.ToString("F2"));
                ShowTactics(brain);
                Show("Target / direct LOS", (brain.Memory.Target != null ? brain.Memory.Target.name : "none") + " / " + brain.Memory.Visible);
                Show("Health / ammo / reload", actor.Health.CurrentHealth + " / " + brain.Combat.Ammo + " / " + brain.Combat.IsReloading);
                Show("World velocity", actor.Navigation.Velocity.ToString("F3"));
                Show("Local velocity", brain.transform.InverseTransformDirection(actor.Navigation.Velocity).ToString("F3"));
                Show("Desired velocity", actor.Navigation.DesiredVelocity.ToString("F3"));
                Show("Destination / last path result", actor.Navigation.Destination + " / " + actor.Navigation.Result);
                Show("Desired facing", actor.Navigation.DesiredFacing.ToString("F3"));
                Show("Current facing", brain.transform.forward.ToString("F3"));
                Show("Angular error", Vector3.SignedAngle(brain.transform.forward, actor.Navigation.DesiredFacing, Vector3.up).ToString("F1"));
                Show("Current turn", bridge != null ? bridge.CurrentTurn : "presentation missing");
                if (bridge != null && bridge.Animator != null)
                {
                    Animator animator = bridge.Animator;
                    ReadController(animator.runtimeAnimatorController as AnimatorController);
                    for (int layer = 0; layer < animator.layerCount; layer++)
                    {
                        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                        Show("Layer " + layer + " state", StateName(state.fullPathHash));
                        Show("Next / transition", StateName(next.fullPathHash) + " / " + animator.IsInTransition(layer));
                        Show("State time / layer weight", state.normalizedTime.ToString("F2") + " / " + animator.GetLayerWeight(layer).ToString("F2"));
                        foreach (var clip in animator.GetCurrentAnimatorClipInfo(layer)) Show("Clip / tree weight", clip.clip.name + " / " + clip.weight.ToString("F2"));
                    }
                    foreach (var parameter in animator.parameters)
                    {
                        string value = parameter.type == AnimatorControllerParameterType.Float ? animator.GetFloat(parameter.nameHash).ToString("F3") :
                            parameter.type == AnimatorControllerParameterType.Bool ? animator.GetBool(parameter.nameHash).ToString() :
                            parameter.type == AnimatorControllerParameterType.Int ? animator.GetInteger(parameter.nameHash).ToString() : "event trigger";
                        Show(parameter.name, value);
                    }
                    bool logging = EditorGUILayout.Toggle("Animation transition logs", bridge.LogsAnimationTransitions);
                    if (logging != bridge.LogsAnimationTransitions) bridge.SetAnimationLogging(logging);
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Continuous Game View presentation review", EditorStyles.boldLabel);
            _reviewScenario = EditorGUILayout.Popup("Review loop", _reviewScenario, AdamPresentationIntegration.ReviewNames);
            bool reviewing = EditorApplication.isPlaying && SessionState.GetBool("AdamPresentation.Validation", false) && SessionState.GetInt("AdamPresentation.Validation.Stage", 0) >= 130;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                if (GUILayout.Button("Run visible review")) AdamPresentationIntegration.RunReview(_reviewScenario, true);
            using (new EditorGUI.DisabledScope(!reviewing))
            {
                bool paused = SessionState.GetBool("RiflemanPolish.Paused", false);
                if (GUILayout.Button(paused ? "Resume review" : "Pause review")) SessionState.SetBool("RiflemanPolish.Paused", !paused);
                SessionState.SetBool("RiflemanPolish.Repeat", EditorGUILayout.Toggle("Repeat scenario", SessionState.GetBool("RiflemanPolish.Repeat", true)));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("0.5x review")) SessionState.SetFloat("RiflemanPolish.TimeScale", .5f);
                if (GUILayout.Button("1.0x review")) SessionState.SetFloat("RiflemanPolish.TimeScale", 1f);
                if (GUILayout.Button("Next scenario")) { _reviewScenario = (_reviewScenario + 1) % AdamPresentationIntegration.ReviewNames.Length; SessionState.SetInt("RiflemanPolish.Scenario", _reviewScenario); }
                if (GUILayout.Button("Stop / restore scene")) EditorApplication.isPlaying = false;
                EditorGUILayout.EndHorizontal();
            }
            _global = EditorGUILayout.Toggle("Global live statistics", _global);
            if (_global && EditorApplication.isPlaying) ShowGlobal();
            EditorGUILayout.LabelField("Deterministic tactical scenarios", EditorStyles.boldLabel);
            _tacticalScenario = EditorGUILayout.Popup("Tactical scenario", _tacticalScenario, AdamPresentationIntegration.TacticalScenarioNames);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Run selected tactical scenario")) AdamPresentationIntegration.RunTacticalScenario(_tacticalScenario);
                if (GUILayout.Button("Run all 20 tactical scenarios")) AdamPresentationIntegration.RunTacticalScenario(-1);
                if (GUILayout.Button("Measure 1 / 3 / 10 / 30 agents")) AdamPresentationIntegration.ValidateStage(110);
                if (GUILayout.Button("Validate lifecycle and edge cases")) AdamPresentationIntegration.ValidateStage(120);
            }
            EditorGUILayout.LabelField("Deterministic animation scenarios", EditorStyles.boldLabel);
            _scenario = EditorGUILayout.Popup("Scenario", _scenario, AdamPresentationIntegration.AnimationScenarioNames);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Run selected scenario")) AdamPresentationIntegration.RunAnimationScenario(_scenario);
                if (GUILayout.Button("Run complete animation matrix")) AdamPresentationIntegration.RunAnimationMatrix();
            }
            EditorGUILayout.HelpBox("Review shows continuous motion in Game View and records a sequence. Slow/pause controls apply only to this review session; exit restores time and scene setup. Automated PASS verifies mechanics, not visual acceptance.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        private static void ShowTactics(EnemyBrain brain)
        {
            var tactics = brain.Tactics;
            if (tactics != null)
            {
                Show("Squad / role", brain.GetComponent<EnemyActor>().SquadId + " / " + tactics.Member?.Role);
                Show("Intent / score / decision reason", tactics.Current.Intent + " / " + tactics.Current.Score.ToString("F1") + " / " + tactics.LastDecisionReason);
                Show("Knowledge / sight time", tactics.Knowledge + " / " + brain.Memory.LastSeenTime.ToString("F2"));
                Show("Line of fire", tactics.FireLane.ToString());
                Show("Shooter / mover / flank sector", tactics.Member?.Shooter + " / " + tactics.Member?.Mover + " / " + tactics.Member?.FlankSide);
                Show("Cover / phase / score", (tactics.CurrentCover != null ? tactics.CurrentCover.name : "none") + " / " + tactics.CoverPhase + " / " + tactics.CoverRating.Score.ToString("F1"));
                Show("Cover rejection", tactics.CoverRating.Rejection.ToString());
                var reservation = tactics.Reservation(tactics.CurrentCover);
                Show("Reservation / expires", reservation != null ? reservation.Owner.Actor.name + " / " + reservation.Phase + " / " + reservation.ExpiresAt.ToString("F1") : "none");
                Show("Last decision / count", tactics.LastDecisionAt.ToString("F2") + " / " + tactics.DecisionCount);
                tactics.MeasureDecisionCost = EditorGUILayout.Toggle("Measure decision cost", tactics.MeasureDecisionCost);
                Show("Measured mean cost / bytes", tactics.AverageDecisionMilliseconds.ToString("F3") + " ms / " + tactics.DecisionAllocatedBytes);
                for (int i = 0; i < tactics.Planner.CoverEvaluationCount; i++)
                {
                    var evaluation = tactics.Planner.EvaluatedCovers[i];
                    Show("Cover candidate " + (evaluation.Point != null ? evaluation.Point.name : "missing"), evaluation.Rating.Score.ToString("F1") + " / " + evaluation.Rating.Rejection + " / path " + evaluation.Rating.PathCost.ToString("F1"));
                }
                for (int i = 0; i < tactics.Planner.Count; i++)
                {
                    var candidate = tactics.Planner.Candidates[i];
                    Show("Candidate " + candidate.Intent, candidate.Score.ToString("F1") + " / " + candidate.Valid + " / " + candidate.Reason + " / " + candidate.Destination);
                }
            }
            var view = brain.GetComponent<EnemyDebugView>();
            if (view != null)
            {
                var serialized = new SerializedObject(view);
                EditorGUILayout.PropertyField(serialized.FindProperty("_settings"), true);
                serialized.ApplyModifiedProperties();
            }
        }
        private void ShowGlobal()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextSnapshot)
            {
                _nextSnapshot = now + 1;
                _brains = Object.FindObjectsByType<EnemyBrain>();
                int checks = 0, decisions = 0, repaths = 0;
                foreach (var brain in _brains)
                {
                    checks += brain.PerceptionCheckCount; decisions += brain.Tactics != null ? brain.Tactics.DecisionCount : 0;
                    repaths += brain.GetComponent<EnemyActor>().Navigation.RepathCount;
                }
                if (_lastSnapshot > 0)
                {
                    float seconds = (float)(now - _lastSnapshot);
                    _checksRate = Mathf.Max(0, checks - _lastChecks) / seconds;
                    _decisionsRate = Mathf.Max(0, decisions - _lastDecisions) / seconds;
                    _repathsRate = Mathf.Max(0, repaths - _lastRepaths) / seconds;
                }
                _lastSnapshot = now; _lastChecks = checks; _lastDecisions = decisions; _lastRepaths = repaths;
            }
            var states = new int[8]; var intents = new int[11]; var squads = new HashSet<EnemySquad>(); var covers = new HashSet<EnemyCoverPoint>();
            int invalid = 0, partial = 0, shooters = 0, movers = 0, measured = 0;
            double milliseconds = 0;
            foreach (var brain in _brains)
            {
                if (brain == null || brain.States == null) continue;
                states[(int)brain.States.Current]++;
                var nav = brain.GetComponent<EnemyActor>().Navigation;
                if (nav.Result == NavigationResult.Partial) partial++;
                if (nav.Result == NavigationResult.Invalid || nav.Result == NavigationResult.Unavailable || nav.Result == NavigationResult.Stuck) invalid++;
                var tactics = brain.Tactics; if (tactics == null) continue;
                if (tactics.Current.Valid) intents[(int)tactics.Current.Intent]++;
                if (tactics.Member != null) { if (tactics.Member.Shooter) shooters++; if (tactics.Member.Mover) movers++; }
                if (tactics.Squad != null) squads.Add(tactics.Squad);
                if (tactics.CurrentCover != null) covers.Add(tactics.CurrentCover);
                measured += tactics.MeasuredDecisions; milliseconds += tactics.DecisionMilliseconds;
            }
            Show("Enemies / squads", _brains.Length + " / " + squads.Count);
            Show("Shooters / movers / covers", shooters + " / " + movers + " / " + covers.Count);
            Show("Perception / decisions / repaths per sec", _checksRate.ToString("F1") + " / " + _decisionsRate.ToString("F1") + " / " + _repathsRate.ToString("F1"));
            Show("Invalid / partial paths", invalid + " / " + partial);
            Show("Mean measured tactical decision", measured > 0 ? (milliseconds / measured).ToString("F3") + " ms" : "measurement disabled");
            for (int i = 0; i < states.Length; i++) if (states[i] > 0) Show(((EnemyStateId)i).ToString(), states[i].ToString());
            for (int i = 0; i < intents.Length; i++) if (intents[i] > 0) Show(((EnemyTacticalIntent)i).ToString(), intents[i].ToString());
        }
        private static void Show(string label, string value) => EditorGUILayout.LabelField(label, value ?? "none");
        private string StateName(int hash) => _states.TryGetValue(hash, out string name) ? name : hash == 0 ? "none" : hash.ToString();
        private void ReadController(AnimatorController controller)
        {
            if (_controller == controller) return;
            _controller = controller; _states.Clear();
            if (controller == null) return;
            foreach (var layer in controller.layers) ReadMachine(layer.stateMachine, layer.name);
        }
        private void ReadMachine(AnimatorStateMachine machine, string path)
        {
            foreach (var state in machine.states) _states[Animator.StringToHash(path + "." + state.state.name)] = path + "/" + state.state.name;
            foreach (var child in machine.stateMachines) ReadMachine(child.stateMachine, path + "." + child.stateMachine.name);
        }
    }
}
