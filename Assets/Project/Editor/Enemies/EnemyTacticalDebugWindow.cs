using System.Collections.Generic;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public sealed class EnemyTacticalDebugWindow : EditorWindow
    {
        [SerializeField] private int _humanCase;
        [SerializeField] private bool _repeat;
        [SerializeField] private bool _advanced;
        [SerializeField] private int _scenario;
        private Vector2 _scroll;
        private AnimatorController _controller;
        private readonly Dictionary<int, string> _states = new Dictionary<int, string>();
        [SerializeField] private int _tacticalScenario;
        [SerializeField] private bool _global;
        private readonly EnemyValidationReport _report = new EnemyValidationReport();
        private string _validationStatus = "No validation selected";
        private readonly int[] _stateCounts = new int[System.Enum.GetValues(typeof(EnemyStateId)).Length];
        private readonly int[] _intentCounts = new int[System.Enum.GetValues(typeof(EnemyTacticalIntent)).Length];
        private readonly HashSet<EnemySquad> _squads = new HashSet<EnemySquad>();
        private readonly HashSet<EnemyCoverPoint> _covers = new HashSet<EnemyCoverPoint>();
        private int _enemyCount, _invalidPaths, _partialPaths, _shooters, _movers, _measured;
        private double _decisionMilliseconds;
        private double _nextSnapshot;
        private double _lastSnapshot;
        private EnemyBrain[] _brains = new EnemyBrain[0];
        private int _lastChecks, _lastDecisions, _lastRepaths;
        private float _checksRate, _decisionsRate, _repathsRate;
        [MenuItem("Breachpoint/Enemies/AI Test / Tactical Debug/Open debug and scenarios")]
        public static void Open() => GetWindow<EnemyTacticalDebugWindow>("Enemy AI / Tactical");
        private void OnEnable()
        {
            minSize = new Vector2(440f, 420f);
            _humanCase = Mathf.Clamp(_humanCase, 0, EnemyValidationRunner.HumanTacticalNames.Length - 1);
            _scenario = Mathf.Clamp(_scenario, 0, EnemyValidationRunner.AnimationScenarioNames.Length - 1);
            _tacticalScenario = Mathf.Clamp(_tacticalScenario, 0, EnemyValidationRunner.TacticalScenarioNames.Length - 1);
            _nextSnapshot = _lastSnapshot = 0;
            _checksRate = _decisionsRate = _repathsRate = 0f;
        }
        private void OnDisable()
        {
            _brains = System.Array.Empty<EnemyBrain>(); _squads.Clear(); _covers.Clear();
            _controller = null; _states.Clear();
        }
        private void OnInspectorUpdate()
        {
            _validationStatus = _report.Refresh(EnemyTools.Evidence + "/validation.txt", SessionState.GetInt("EnemyTools.Validation.Stage", 70), SessionState.GetBool("EnemyTools.Validation", false));
            Repaint();
        }
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHumanReview();
            _advanced = EditorGUILayout.Foldout(_advanced, "Advanced diagnostics and automated checks", true);
            if (_advanced)
            {
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
                    Show("Movement phase / permitted speed", actor.Navigation.MovementPhase + " / " + actor.Navigation.AllowedSpeed.ToString("F3"));
                    Show("Desired tier / requested speed / NavMesh limit", actor.Navigation.DesiredMovementTier + " / " + actor.Navigation.RequestedWorldSpeed.ToString("F2") + " / " + actor.Navigation.DesiredSpeed.ToString("F2"));
                    if (bridge != null)
                    {
                        Show("MoveSpeed raw / smoothed", bridge.MoveSpeedRaw.ToString("F3") + " / " + bridge.MoveSpeedSmoothed.ToString("F3"));
                        Show("Direction raw / smoothed", bridge.MoveDirectionRaw + " / " + bridge.MoveDirectionSmoothed);
                        Show("Foot motion detected", bridge.FootMotionDetected.ToString());
                        Show("Muzzle horizontal / vertical / total", bridge.HorizontalMuzzleErrorDegrees.ToString("F1") + " / " + bridge.VerticalMuzzleErrorDegrees.ToString("F1") + " / " + bridge.MuzzleAimErrorDegrees.ToString("F1"));
                        Show("Body / muzzle aim error", bridge.BodyAimErrorDegrees.ToString("F1") + " / " + bridge.MuzzleAimErrorDegrees.ToString("F1"));
                    }
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
                        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                        if (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
                        ReadController(controller as AnimatorController);
                        for (int layer = 0; layer < animator.layerCount; layer++)
                        {
                            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                            Show("Layer " + layer + " state", StateName(state.fullPathHash));
                            Show("Next / transition", StateName(next.fullPathHash) + " / " + animator.IsInTransition(layer));
                            Show("Effective state playback speed", (state.speed * state.speedMultiplier * animator.speed).ToString("F2"));
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
                if (GUILayout.Button("Open live animation review")) RiflemanLiveReviewWindow.Open();
                _global = EditorGUILayout.Toggle("Global live statistics", _global);
                if (_global && EditorApplication.isPlaying) ShowGlobal();
                EditorGUILayout.LabelField("Deterministic tactical scenarios", EditorStyles.boldLabel);
                _tacticalScenario = EditorGUILayout.Popup("Tactical scenario", _tacticalScenario, EnemyValidationRunner.TacticalScenarioNames);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                {
                    if (GUILayout.Button("Run selected tactical scenario")) EnemyValidationRunner.RunTacticalScenario(_tacticalScenario);
                    if (GUILayout.Button("Run all 20 tactical scenarios")) EnemyValidationRunner.RunTacticalScenario(-1);
                    if (GUILayout.Button("Measure 1 / 3 / 10 / 30 agents")) EnemyValidationRunner.ValidateStage(110);
                    if (GUILayout.Button("Validate lifecycle and edge cases")) EnemyValidationRunner.ValidateStage(120);
                    if (GUILayout.Button("Validate moving Hit to ragdoll")) EnemyValidationRunner.ValidateStage(181);
                    if (GUILayout.Button("Validate active death physics")) EnemyValidationRunner.ValidateStage(179);
                    if (GUILayout.Button("Validate physical death animation tracking")) { SessionState.SetString("EnemyTools.DeathTracking.Label", "after"); EnemyValidationRunner.ValidateStage(180); }
                }
                EditorGUILayout.LabelField("Deterministic animation scenarios", EditorStyles.boldLabel);
                _scenario = EditorGUILayout.Popup("Scenario", _scenario, EnemyValidationRunner.AnimationScenarioNames);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                {
                    if (GUILayout.Button("Run selected scenario")) EnemyValidationRunner.RunAnimationScenario(_scenario);
                    if (GUILayout.Button("Run complete animation matrix")) EnemyValidationRunner.RunAnimationMatrix();
                }
                EditorGUILayout.HelpBox("Review shows continuous motion in Game View. Slow/pause controls apply only to this review session; exit restores time and scene setup. Automated PASS verifies mechanics, not visual acceptance.", MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }
        private void DrawHumanReview()
        {
            EditorGUILayout.LabelField("Tactical visual review", EditorStyles.boldLabel);
            bool running = EnemyValidationRunner.TacticalReviewRunning;
            if (running) { _humanCase = EnemyValidationRunner.HumanTacticalIndex; _repeat = EnemyValidationRunner.HumanTacticalRepeating; }
            _humanCase = Mathf.Clamp(_humanCase, 0, EnemyValidationRunner.HumanTacticalNames.Length - 1);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode && !running))
            {
                int group = EnemyTacticalReviewCatalog.GroupFor(_humanCase);
                int selectedGroup = EditorGUILayout.Popup("Review group", group, EnemyTacticalReviewCatalog.GroupNames);
                int scenario = selectedGroup == group ? System.Array.IndexOf(EnemyTacticalReviewCatalog.Cases[group], _humanCase) : 0;
                int selected = EditorGUILayout.Popup("Scenario", scenario, EnemyTacticalReviewCatalog.Names[selectedGroup]);
                int globalIndex = EnemyTacticalReviewCatalog.Cases[selectedGroup][selected];
                if (globalIndex != _humanCase)
                {
                    _humanCase = globalIndex;
                    if (running) EnemyValidationRunner.RunHumanTacticalReview(_humanCase, _repeat);
                }
            }
            EditorGUILayout.HelpBox(EnemyValidationRunner.HumanTacticalPurpose(_humanCase), MessageType.None);
            EditorGUILayout.HelpBox(EnemyValidationRunner.HumanTacticalWatch(_humanCase), MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode && !running))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Run")) { _repeat = false; EnemyValidationRunner.RunHumanTacticalReview(_humanCase,false); }
                if (GUILayout.Button("Repeat")) { _repeat = true; EnemyValidationRunner.RunHumanTacticalReview(_humanCase,true); }
                using (new EditorGUI.DisabledScope(!running))
                    if (GUILayout.Button("Stop")) EnemyValidationRunner.StopHumanTacticalReview();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous")) SelectHumanCase(-1,running);
                if (GUILayout.Button("Next")) SelectHumanCase(1,running);
                EditorGUILayout.EndHorizontal();
            }
            if (running)
            {
                Show("Review", EnemyValidationRunner.TacticalReviewPhase + (_repeat ? " / repeating" : ""));
                bool paused = SessionState.GetBool("EnemyTools.Review.Paused", false);
                if (GUILayout.Button(paused ? "Resume" : "Pause")) SessionState.SetBool("EnemyTools.Review.Paused", !paused);
                float scale = SessionState.GetFloat("EnemyTools.Review.TimeScale",1f);
                float selectedScale = EditorGUILayout.Slider("Playback speed",scale,.25f,1f);
                if (!Mathf.Approximately(scale,selectedScale)) SessionState.SetFloat("EnemyTools.Review.TimeScale",selectedScale);
            }
            EnemyBrain selectedBrain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<EnemyBrain>() : null;
            EnemyBrain brain = running ? EnemyValidationRunner.TacticalReviewBrain : selectedBrain;
            if (running && selectedBrain != null && brain != null && selectedBrain.transform.parent == brain.transform.parent) brain = selectedBrain;
            if (brain != null && brain.Config != null && brain.States != null)
            {
                var actor = brain.GetComponent<EnemyActor>(); var bridge = brain.GetComponent<EnemyAnimationBridge>();
                EditorGUILayout.LabelField("Live enemy", EditorStyles.boldLabel);
                Show("AI state", brain.States.Current.ToString());
                Show("Intent", running && (_humanCase < 12 || _humanCase >= 32) ? "Controlled presentation" : brain.Tactics?.Current.Intent.ToString());
                Show("Movement tier / speed", actor.Navigation.DesiredMovementTier + " / " + actor.Navigation.Velocity.magnitude.ToString("F2") + " m/s");
                Show("Local direction", bridge != null ? bridge.MoveDirectionSmoothed.ToString("F2") : "none");
                var ragdoll = brain.GetComponent<EnemyRagdollPresenter>();
                var active = brain.GetComponent<EnemyActiveRagdoll>();
                if (ragdoll != null && ragdoll.IsRagdoll && active != null)
                {
                    Show("Death physics", active.IsActive ? "Active muscle support" : "Passive ragdoll");
                    Show("Feet supported / target speed", active.HasSupport + " / " + active.TargetPlaybackSpeed.ToString("F2"));
                    Show("Leg / torso strength", active.LegStrength.ToString("F2") + " / " + active.UpperBodyStrength.ToString("F2"));
                    Show("Muscle torque", active.LastMotorTorque.ToString("F2") + " N*m");
                    if (!active.IsStationaryDeath && !active.IsMovingHitDeath) Show("Fallback fatal hit / local impulse / angular kick", (active.HitBody != null ? active.HitBody.name : "no geometry") + " / " + active.AppliedHitImpulse.magnitude.ToString("F2") + " N*s / " + active.AppliedMovingHitAngularChange.magnitude.ToString("F2") + " rad/s");
                    Show("Death target", active.IsStationaryDeath ? (active.DeathTargetClip != null ? active.DeathTargetClip.name : "Directional reaction") : active.IsMovingHitDeath ? "Moving Hit → passive ragdoll" : "Locomotion fallback");
                    if (active.IsMovingHitDeath) Show("Hit clip / weight", (active.HitTargetClip != null ? active.HitTargetClip.name : "Blending in") + " / " + active.HitTargetWeight.ToString("F2"));
                }
                Show("Cover", brain.Tactics?.CurrentCover != null ? brain.Tactics.CurrentCover.name + " / " + brain.Tactics.CoverPhase : "none");
                Show("Combat", brain.Combat.IsReloading ? "Reloading" : (bridge != null && bridge.Animator.GetBool("IsFiring") ? "Firing" : "Ready") + " / ammo " + brain.Combat.Ammo);
                if (bridge != null && bridge.Animator != null)
                {
                    Show("Locomotion", EnemyAnimationStateNames.Get(bridge.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash));
                    if (bridge.Animator.IsInTransition(0)) Show("Transition to", EnemyAnimationStateNames.Get(bridge.Animator.GetNextAnimatorStateInfo(0).fullPathHash));
                }
            }
            else EditorGUILayout.HelpBox("Run a scenario to inspect it in Game View. Repeat loops until Stop; Next/Previous replaces the running scenario.", MessageType.None);
            EditorGUILayout.HelpBox("Movement, crouch and death reviews use controlled routes. Tactical AI cases run autonomous enemies. Next/Previous cycles within the selected group; Stop restores your scenes.", MessageType.None);
            EditorGUILayout.Space();
        }
        private void SelectHumanCase(int offset, bool running)
        {
            _humanCase = EnemyTacticalReviewCatalog.Step(_humanCase, offset);
            if (running) EnemyValidationRunner.RunHumanTacticalReview(_humanCase,_repeat);
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
            if (now >= _nextSnapshot) RefreshGlobal(now);
            Show("Enemies / squads", _enemyCount + " / " + _squads.Count);
            Show("Shooters / movers / covers", _shooters + " / " + _movers + " / " + _covers.Count);
            Show("Perception / decisions / repaths per sec", _checksRate.ToString("F1") + " / " + _decisionsRate.ToString("F1") + " / " + _repathsRate.ToString("F1"));
            Show("Invalid / partial paths", _invalidPaths + " / " + _partialPaths);
            Show("Mean measured tactical decision", _measured > 0 ? (_decisionMilliseconds / _measured).ToString("F3") + " ms" : "measurement disabled");
            for (int i = 0; i < _stateCounts.Length; i++) if (_stateCounts[i] > 0) Show(((EnemyStateId)i).ToString(), _stateCounts[i].ToString());
            for (int i = 0; i < _intentCounts.Length; i++) if (_intentCounts[i] > 0) Show(((EnemyTacticalIntent)i).ToString(), _intentCounts[i].ToString());
        }
        private void RefreshGlobal(double now)
        {
            _nextSnapshot = now + 1;
            _brains = Object.FindObjectsByType<EnemyBrain>();
            System.Array.Clear(_stateCounts, 0, _stateCounts.Length);
            System.Array.Clear(_intentCounts, 0, _intentCounts.Length);
            _squads.Clear(); _covers.Clear();
            _enemyCount = _invalidPaths = _partialPaths = _shooters = _movers = _measured = 0;
            _decisionMilliseconds = 0;
            int checks = 0, decisions = 0, repaths = 0;
            foreach (var brain in _brains)
            {
                if (brain == null || brain.States == null) continue;
                var actor = brain.GetComponent<EnemyActor>();
                if (actor == null || actor.Navigation == null) continue;
                _enemyCount++; _stateCounts[(int)brain.States.Current]++;
                checks += brain.PerceptionCheckCount;
                var nav = actor.Navigation; repaths += nav.RepathCount;
                if (nav.Result == NavigationResult.Partial) _partialPaths++;
                if (nav.Result == NavigationResult.Invalid || nav.Result == NavigationResult.Unavailable || nav.Result == NavigationResult.Stuck) _invalidPaths++;
                var tactics = brain.Tactics; if (tactics == null) continue;
                decisions += tactics.DecisionCount;
                if (tactics.Current.Valid) _intentCounts[(int)tactics.Current.Intent]++;
                if (tactics.Member != null) { if (tactics.Member.Shooter) _shooters++; if (tactics.Member.Mover) _movers++; }
                if (tactics.Squad != null) _squads.Add(tactics.Squad);
                if (tactics.CurrentCover != null) _covers.Add(tactics.CurrentCover);
                _measured += tactics.MeasuredDecisions; _decisionMilliseconds += tactics.DecisionMilliseconds;
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
