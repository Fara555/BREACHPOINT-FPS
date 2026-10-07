using System;
using System.Collections;
using System.IO;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal static readonly string[] ReviewNames = {
            "Steady start / walk / stop", "Combat walk / run / sprint", "Combat eight directions", "Steady / Combat readiness",
            "Steady 90L", "Steady 90R", "Steady 180L", "Steady 180R", "Combat 90L", "Combat 90R", "Combat 180L", "Combat 180R",
            "Crouch 90L", "Crouch 90R", "Crouch 180L", "Crouch 180R", "Crouch enter / move / exit", "Fire idle / moving / crouch",
            "Reload walking / crouch", "Hit moving / crouch", "Death / ragdoll", "Crouch IK eight directions", "Standing Fire", "Crouch Fire",
            "Focus: Steady Idle", "Focus: Idle -> StartWalk", "Focus: StartWalk -> Walk", "Focus: sustained Steady Walk", "Focus: Walk -> StopWalk", "Focus: StopWalk -> Idle", "Focus: repeated start / stop",
            "Focus: Steady 90 left", "Focus: Steady 90 right", "Focus: Steady 180 left", "Focus: Steady 180 right",
            "Focus: Idle -> Walk forward", "Focus: Idle -> Walk left", "Focus: Idle -> Walk right", "Focus: Idle -> Walk diagonal", "Focus: Idle -> Run forward", "Focus: Idle -> Run left", "Focus: Idle -> Run right",
            "Focus: Walk -> Run", "Focus: Run -> Walk", "Focus: Run -> Sprint", "Focus: Sprint -> Run", "Focus: Run left -> right", "Focus: forward -> lateral", "Focus: lateral -> diagonal",
            "Focus: Crouch Idle -> forward", "Focus: Crouch Idle -> left", "Focus: Crouch Idle -> right", "Focus: Crouch move -> stop",
            "Focus: centered aim", "Focus: left aim", "Focus: right aim", "Focus: Fire left / right", "Focus: Crouch aim left / right",
            "Focus: short reposition pace", "Focus: long reposition pace", "Focus: MoveToCover pace", "Focus: flank pace", "Focus: fallback pace", "Focus: aim at chest height", "Focus: aim above", "Focus: aim below", "Focus: Fire center / above / below", "Focus: Crouch Fire center / above / below", "Final: Raise center", "Final: Raise above", "Final: Raise below / moving target", "Final: Sprint posture", "Final: sustained Sprint", "Final: Run -> Sprint -> Run" };
        private static Camera _reviewCamera;
        internal static EnemyActor ReviewActor { get; private set; }
        private static int _reviewActiveScenario = -1;
        private static float _nextReviewCapture;
        private static int _lastReviewCaptureFrame = -1;

        [MenuItem("Breachpoint/Enemies/Validation/Validate live review controls")]
        internal static void ValidateReviewControls()
        {
            SessionState.SetFloat("EnemyTools.Review.OriginalScale", Time.timeScale);
            SessionState.SetBool("EnemyTools.Review.OriginalBackground", Application.runInBackground);
            Time.timeScale = .75f;
            Application.runInBackground = true;
            SessionState.SetFloat("EnemyTools.Review.ControlScale", Time.timeScale);
            SessionState.SetBool("EnemyTools.Review.ControlBackground", Application.runInBackground);
            SessionState.SetBool("EnemyTools.Review.ControlRepeat", SessionState.GetBool("EnemyTools.Review.Repeat", true));
            SessionState.SetString("EnemyTools.Review.ControlError", "");
            try
            {
                RunReview(3, true);
                SessionState.SetBool("EnemyTools.Review.ControlTest", true);
            }
            catch
            {
                RestoreReviewControlEnvironment();
                throw;
            }
        }

        private static void StartReviewControlChecks()
        {
            SessionState.SetBool("EnemyTools.Review.ControlTest", false);
            bool originalRepeat = SessionState.GetBool("EnemyTools.Review.ControlRepeat", true);
            SessionState.SetBool("EnemyTools.Review.Repeat", true);
            int phase = 0;
            double next = EditorApplication.timeSinceStartup;
            float frozen = 0f;
            EditorApplication.update += CheckControls;
            void CheckControls()
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .4;
                try
                {
                    if (phase == 0)
                    {
                        if (!EditorApplication.isPlaying || _reviewActiveScenario < 0) return;
                        SessionState.SetBool("EnemyTools.Review.Paused", true);
                    }
                    else if (phase == 1) { Require(Time.timeScale == 0f, "Pause freezes owned review"); frozen = Time.time; }
                    else if (phase == 2)
                    {
                        Require(Mathf.Abs(Time.time - frozen) < .001f, "Paused simulation does not advance");
                        SessionState.SetBool("EnemyTools.Review.Paused", false); SessionState.SetFloat("EnemyTools.Review.TimeScale", .5f);
                    }
                    else if (phase == 3) { Require(Time.timeScale == .5f, "Half speed applied"); SessionState.SetInt("EnemyTools.Review.Scenario", 21); }
                    else if (phase == 4) { Require(_reviewActiveScenario == 21, "Next changes live scenario"); SessionState.SetFloat("EnemyTools.Review.TimeScale", 1f); }
                    else if (phase == 5) { Require(Time.timeScale == 1f, "Normal speed restored"); SessionState.SetBool("EnemyTools.Review.Paused", true); }
                    else if (phase == 6)
                    {
                        Require(Time.timeScale == 0f, "Stop fixture is paused");
                        EditorApplication.update -= CheckControls;
                        SessionState.SetBool("EnemyTools.Review.ControlStopping", true);
                        EditorApplication.isPlaying = false;
                    }
                    phase++;
                }
                catch (Exception error)
                {
                    EditorApplication.update -= CheckControls;
                    SessionState.SetBool("EnemyTools.Review.Repeat", originalRepeat);
                    SessionState.SetString("EnemyTools.Review.ControlError", error.ToString());
                    SessionState.SetBool("EnemyTools.Review.ControlStopping", true);
                    EditorApplication.isPlaying = false;
                }
            }
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        }

        private static void CompleteReviewControlChecks()
        {
            SessionState.SetBool("EnemyTools.Review.ControlStopping", false);
            SessionState.SetBool("EnemyTools.Review.Repeat", SessionState.GetBool("EnemyTools.Review.ControlRepeat", true));
            string error = SessionState.GetString("EnemyTools.Review.ControlError", "");
            string console = ConsoleCounts();
            bool restored = Time.timeScale == SessionState.GetFloat("EnemyTools.Review.ControlScale", 1f) && Application.runInBackground == SessionState.GetBool("EnemyTools.Review.ControlBackground", false) && _reviewActiveScenario < 0 && _reviewCamera == null && SessionState.GetBool("EnemyTools.Review.Disposed", false);
            bool passed = restored && error.Length == 0 && console.Contains("errors=0") && console.Contains("warnings=0");
            File.WriteAllText(EnemyTools.Evidence + "/review-controls.txt", (passed ? "RESULT: PASS" : "RESULT: FAIL") + "\n" + DateTime.UtcNow.ToString("O") + "\nPause, frozen simulation, 0.5x, next, 1x, stop while paused, scene/resource restoration\nRestored scale=" + Time.timeScale + ", background=" + Application.runInBackground + " (expected .75/true)\n" + error + "\n" + console);
            RestoreReviewControlEnvironment();
            AppendResult(passed ? "PASS: Live review pause/speed/next/stop controls and restoration" : "FAIL: Live review controls | " + error);
            AppendResult(passed ? "RESULT: PASS" : "RESULT: FAIL");
            AppendResult(console);
        }

        private static void RestoreReviewControlEnvironment()
        {
            Time.timeScale = SessionState.GetFloat("EnemyTools.Review.OriginalScale", 1f);
            Application.runInBackground = SessionState.GetBool("EnemyTools.Review.OriginalBackground", false);
        }

        internal static void RunReview(int index, bool interactive)
        {
            if (index < -1 || index >= ReviewNames.Length || interactive && index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            _nextReviewCapture = 0f; _lastReviewCaptureFrame = -1;
            SessionState.SetFloat("EnemyTools.Review.PriorScale", Time.timeScale);
            SessionState.SetBool("EnemyTools.Review.Disposed", false);
            SessionState.SetInt("EnemyTools.Review.Scenario", index);
            SessionState.SetBool("EnemyTools.Review.Paused", false);
            SessionState.SetFloat("EnemyTools.Review.TimeScale", 1f);
            ValidateStage(interactive ? 131 : 130);
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Show();
        }

        private static IEnumerator LiveReviewTests(GameLifetimeScope scope, EnemyWorld world, bool interactive)
        {
            float priorScale = SessionState.GetFloat("EnemyTools.Review.PriorScale", 1f);
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var player = new GameObject("Presentation review target"); player.layer = 6;
            var collider = player.AddComponent<CapsuleCollider>(); collider.height = 2f; collider.center = Vector3.up; collider.radius = .4f;
            player.AddComponent<Health>().Configure(1000000f, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            _reviewCamera = new GameObject("Presentation review Game View camera").AddComponent<Camera>();
            _reviewCamera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            _reviewCamera.depth = 100; _reviewCamera.fieldOfView = 35f; _reviewCamera.nearClipPlane = .03f;
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); var actor = enemy.GetComponent<EnemyActor>();
            ReviewActor = actor;
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); brain.enabled = false;
            int selected = SessionState.GetInt("EnemyTools.Review.Scenario", -1);
            try
            {
                do
                {
                    for (int index = 0; index < ReviewNames.Length; index++)
                    {
                        selected = interactive ? SessionState.GetInt("EnemyTools.Review.Scenario", 0) : selected;
                        if (selected >= 0 && index != selected) continue;
                        int passes = interactive ? 1 : 2;
                        for (int pass = 0; pass < passes; pass++)
                        {
                            if (!interactive) SessionState.SetFloat("EnemyTools.Review.TimeScale", pass == 0 ? 1f : .5f);
                            enemy.SetActive(false); enemy.transform.SetPositionAndRotation(AnimationStart, Quaternion.identity); enemy.SetActive(true);
                            yield return null; brain.ResetForSpawn(); brain.enabled = false;
                            player.transform.position = AnimationStart + Vector3.forward * 10;
                            brain.Memory.Target = target; brain.Memory.Visible = true; brain.Memory.HasContact = true;
                            _reviewActiveScenario = index;
                            AppendResult("REVIEW " + index + " | " + ReviewNames[index] + " | " + SessionState.GetFloat("EnemyTools.Review.TimeScale", 1f) + "x");
                            IEnumerator run = ReviewCase(index, enemy, brain, actor, bridge, target);
                            try
                            {
                                while (run.MoveNext())
                                {
                                    FollowReviewCamera(enemy);
                                    if (interactive && SessionState.GetInt("EnemyTools.Review.Scenario", index) != index) break;
                                    yield return run.Current;
                                }
                            }
                            finally { (run as IDisposable)?.Dispose(); }
                            AppendResult("Completed live review loop; visual acceptance requires inspection.");
                        }
                    }
                } while (interactive && SessionState.GetBool("EnemyTools.Review.Repeat", true));
            }
            finally
            {
                Time.timeScale = priorScale;
                _reviewActiveScenario = -1;
                ReviewActor = null;
                DestroyReviewObject(enemy); DestroyReviewObject(player);
                if (_reviewCamera != null) DestroyReviewObject(_reviewCamera.gameObject);
                _reviewCamera = null;
                SessionState.SetBool("EnemyTools.Review.Disposed", true);
                SessionState.SetBool("EnemyTools.Review.Record", false);
            }
        }

        private static void DestroyReviewObject(Object value)
        {
            if (value == null) return;
            if (EditorApplication.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private static void FollowReviewCamera(GameObject enemy)
        {
            _reviewCamera.transform.position = enemy.transform.position + new Vector3(2.6f, 1.65f, 3.5f);
            _reviewCamera.transform.LookAt(enemy.transform.position + Vector3.up * .95f);
            if (SessionState.GetBool("EnemyTools.Review.Record", false) && Time.unscaledTime >= _nextReviewCapture && Time.frameCount != _lastReviewCaptureFrame)
            {
                _lastReviewCaptureFrame = Time.frameCount; _nextReviewCapture = Time.unscaledTime + 1f / 30f;
                string group = SessionState.GetString("EnemyTools.Review.RecordGroup", "");
                string folder = EnemyTools.Evidence + (group.Length == 0 ? "/ReviewFrames/" : "/" + group + "Frames/") + _reviewActiveScenario + "-" + SessionState.GetFloat("EnemyTools.Review.TimeScale", 1f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(folder + "/" + Time.frameCount.ToString("D7") + ".png");
                var bridge = enemy.GetComponent<EnemyAnimationBridge>(); var nav = enemy.GetComponent<EnemyActor>().Navigation;
                File.AppendAllText(folder + "/frames.csv", FormattableString.Invariant($"{Time.frameCount},{Time.time:F3},{EnemyAnimationStateNames.Get(bridge.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash)},{nav.Velocity.magnitude:F3},{bridge.MoveSpeedSmoothed:F3},{bridge.BodyAimErrorDegrees:F2},{bridge.MuzzleAimErrorDegrees:F2},{nav.MovementPhase},{bridge.HorizontalMuzzleErrorDegrees:F2},{bridge.VerticalMuzzleErrorDegrees:F2},{bridge.FootMotionDetected}\n"));
            }
        }
        private static IEnumerable WaitReview(float seconds)
        { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private static IEnumerator ReviewCase(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            if (index >= 24)
            {
                IEnumerator focused = index >= 68 ? FinalFocusedReview(index - 68, enemy, brain, actor, bridge, target) : index >= 63 ? VerticalAimReview(index - 63, enemy, brain, actor, bridge, target) : FocusedMovementReview(index - 24, enemy, brain, actor, bridge, target);
                while (focused.MoveNext()) yield return focused.Current;
                yield break;
            }
            if (index >= 1 && index != 3 && index < 4 || index >= 8)
                brain.States.Change(EnemyStateId.Combat, "continuous presentation review");
            foreach (var wait in WaitReview(1.4f)) yield return wait;
            if (index >= 4 && index <= 15)
            {
                if (index >= 12) { bridge.SetCrouching(true); foreach (var wait in WaitReview(1.4f)) yield return wait; }
                int turn = (index - 4) % 4; float angle = turn == 0 ? -90 : turn == 1 ? 90 : turn == 2 ? -175 : 175;
                Vector3 turnTarget = enemy.transform.position + Quaternion.Euler(0, angle, 0) * enemy.transform.forward * 10;
                if (index >= 8) { target.transform.position = turnTarget; Physics.SyncTransforms(); }
                actor.Navigation.Face(turnTarget, Time.deltaTime);
                foreach (var wait in WaitReview(3f)) yield return wait;
                yield break;
            }
            if (index == 3)
            {
                brain.States.Change(EnemyStateId.Combat, "readiness"); foreach (var wait in WaitReview(2.5f)) yield return wait;
                brain.Memory.Visible = false; brain.Memory.HasContact = false; brain.States.Change(EnemyStateId.Idle, "relax");
                foreach (var wait in WaitReview(2.5f)) yield return wait; yield break;
            }
            if (index == 20)
            {
                actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, Vector3.back, target.gameObject));
                foreach (var wait in WaitReview(3f)) yield return wait; yield break;
            }
            if (index == 16 || index == 21 || index == 18)
            { bridge.SetCrouching(index != 18); foreach (var wait in WaitReview(1.3f)) yield return wait; }
            if (index == 18)
            {
                brain.Combat.Attack(target, Time.time - 1); brain.Combat.Attack(target, Time.time); brain.Combat.RequestReload(Time.time);
                float until = Time.time + brain.Config.Combat.ReloadDuration + .5f;
                while (Time.time < until)
                { brain.Combat.Tick(Time.time); actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 10, bridge.IsCrouching ? EnemyMovePace.Crouch : EnemyMovePace.Walk, Time.time); if (Time.time > until - 2f) bridge.SetCrouching(true); yield return null; }
            }
            else if (index == 19)
            {
                for (int phase = 0; phase < 2; phase++)
                {
                    bridge.SetCrouching(phase == 1);
                    foreach (var wait in WaitReview(1.3f)) yield return wait;
                    actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 15, phase == 0 ? EnemyMovePace.Run : EnemyMovePace.Crouch, Time.time);
                    foreach (var wait in WaitReview(.5f)) yield return wait;
                    actor.Health.TakeDamage(new DamageInfo(1, actor.Eyes.position, Vector3.back, target.gameObject));
                    foreach (var wait in WaitReview(1.5f)) yield return wait;
                    actor.Navigation.RequestStop();
                }
            }
            else if (index == 17 || index == 22 || index == 23)
            {
                for (int phase = index == 23 ? 2 : 0; phase < (index == 22 ? 1 : 3); phase++)
                {
                    bridge.SetCrouching(phase == 2); foreach (var wait in WaitReview(1.3f)) yield return wait;
                    float until = Time.time + 2f;
                    while (Time.time < until)
                    {
                        if (phase == 1) actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 10, EnemyMovePace.Run, Time.time);
                        brain.Combat.Attack(target, Time.time); brain.Combat.Tick(Time.time); yield return null;
                    }
                    actor.Navigation.RequestStop();
                }
            }
            else
            {
                int directions = index == 2 || index == 21 ? 8 : index == 1 ? 5 : 1;
                for (int direction = 0; direction < directions; direction++)
                {
                    var pace = index == 1 ? (EnemyMovePace)(direction <= 2 ? direction : 4 - direction) : index == 16 || index == 21 ? EnemyMovePace.Crouch : EnemyMovePace.Walk;
                    Vector3 heading = Quaternion.Euler(0, directions == 8 ? direction * 45 : 0, 0) * Vector3.forward;
                    Vector3 goal = index == 1 ? AnimationStart + Vector3.forward * 24 : enemy.transform.position + heading * 5;
                    float until = Time.time + (directions == 8 ? .9f : index == 1 ? 1.2f : 2.5f);
                    while (Time.time < until)
                    {
                        // Keep the speed-ramp target ahead, so passing a static dummy cannot
                        // introduce unrelated backward aiming during the deceleration review.
                        if (index == 1) { target.transform.position = enemy.transform.position + Vector3.forward * 10; Physics.SyncTransforms(); }
                        actor.Navigation.MoveTo(goal, pace, Time.time);
                        if (index != 0) actor.Navigation.Face(enemy.transform.position + Vector3.forward * 10, Time.deltaTime);
                        yield return null;
                    }
                }
            }
            actor.Navigation.RequestStop();
            if (index == 16) bridge.SetCrouching(false);
            foreach (var wait in WaitReview(2f)) yield return wait;
        }
    }

    public sealed class RiflemanLiveReviewWindow : EditorWindow
    {
        [SerializeField] private int _scenario;
        [SerializeField] private bool _advanced;
        private Vector2 _scroll;
        private void OnEnable() => minSize = new Vector2(440f, 420f);
        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying && SessionState.GetInt("EnemyTools.Validation.Stage", 0) == 131) Repaint();
        }
        private static readonly int[] TargetedScenarios = { 0, 30, 27, 68, 69, 70, 71, 72, 73, 10, 11 };
        private static readonly string[] TargetedNames = TargetedScenarios.Select(i => EnemyValidationRunner.ReviewNames[i]).ToArray();
        [MenuItem("Breachpoint/Enemies/Animation Review/Live review")]
        public static void Open() => GetWindow<RiflemanLiveReviewWindow>("Rifleman live review");

        private void OnGUI()
        {
            _scenario = Mathf.Clamp(_scenario, 0, EnemyValidationRunner.ReviewNames.Length - 1);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox("Continuous Game View playback. Inspect pose transitions, elbows, foot motion and body facing at both speeds. Mechanical PASS does not certify visual quality.", MessageType.Info);
            bool owned = EditorApplication.isPlaying && SessionState.GetBool("EnemyTools.Validation", false) && SessionState.GetInt("EnemyTools.Validation.Stage", 0) == 131;
            var actor = EnemyValidationRunner.ReviewActor;
            if (actor != null)
            {
                var nav = actor.Navigation; var bridge = actor.GetComponent<EnemyAnimationBridge>(); var state = bridge.Animator.GetCurrentAnimatorStateInfo(0);
                EditorGUILayout.LabelField("Movement phase", nav.MovementPhase.ToString());
                EditorGUILayout.LabelField("Desired / allowed / actual speed", nav.RequestedWorldSpeed.ToString("F2") + " / " + nav.AllowedSpeed.ToString("F2") + " / " + nav.Velocity.magnitude.ToString("F2"));
                EditorGUILayout.LabelField("Animator / normalized time", EnemyAnimationStateNames.Get(state.fullPathHash) + " / " + state.normalizedTime.ToString("F3"));
                var clips = bridge.Animator.GetCurrentAnimatorClipInfo(0);
                if (clips.Length > 0) EditorGUILayout.LabelField("Current clip", clips.OrderByDescending(c => c.weight).First().clip.name);
                var rig = actor.GetComponent<EnemyRigPresenter>();
                EditorGUILayout.LabelField("Aim policy / coarse / AimRig", bridge.AimPresentationWeight.ToString("F2") + " / " + rig.CoarseWeight.ToString("F2") + " / " + rig.AimRigWeight.ToString("F2"));
                EditorGUILayout.LabelField("Foot motion / MoveSpeed", bridge.FootMotionDetected + " / " + bridge.MoveSpeedSmoothed.ToString("F3"));
                EditorGUILayout.LabelField("Muzzle horizontal / vertical / total", bridge.HorizontalMuzzleErrorDegrees.ToString("F1") + " / " + bridge.VerticalMuzzleErrorDegrees.ToString("F1") + " / " + bridge.MuzzleAimErrorDegrees.ToString("F1"));
            }
            if (owned) _scenario = SessionState.GetInt("EnemyTools.Review.Scenario", _scenario);
            EditorGUI.BeginChangeCheck();
            if (_advanced) _scenario = EditorGUILayout.Popup("Scenario", _scenario, EnemyValidationRunner.ReviewNames);
            else { int selected = System.Array.IndexOf(TargetedScenarios, _scenario); _scenario = TargetedScenarios[EditorGUILayout.Popup("Scenario", Mathf.Max(0, selected), TargetedNames)]; }
            if (EditorGUI.EndChangeCheck() && owned) SessionState.SetInt("EnemyTools.Review.Scenario", _scenario);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
            {
                if (GUILayout.Button("Run selected loop")) EnemyValidationRunner.RunReview(_scenario, true);
                if (GUILayout.Button("Record synchronization trace")) EnemyValidationRunner.RunSynchronizationProbe();
            }
            using (new EditorGUI.DisabledScope(!File.Exists(EnemyTools.Evidence + "/synchronization-trace.csv")))
                if (GUILayout.Button("Show latest synchronization trace")) EditorUtility.RevealInFinder(EnemyTools.Evidence + "/synchronization-trace.csv");
            _advanced = EditorGUILayout.Foldout(_advanced, "Advanced diagnostics / previous production cases");
            if (_advanced)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                {
                    if (GUILayout.Button("Record source Steady / Sprint / Left-Right comparison")) EnemyValidationRunner.ValidateStage(167);
                    if (GUILayout.Button("Record five rig modes / Raise / Steady")) { SessionState.SetString("EnemyTools.Final.Label", "review"); EnemyValidationRunner.ValidateStage(168); }
                }
            }
            using (new EditorGUI.DisabledScope(!owned))
            {
                bool paused = SessionState.GetBool("EnemyTools.Review.Paused", false);
                if (GUILayout.Button(paused ? "Resume" : "Pause")) SessionState.SetBool("EnemyTools.Review.Paused", !paused);
                SessionState.SetBool("EnemyTools.Review.Repeat", EditorGUILayout.Toggle("Repeat", SessionState.GetBool("EnemyTools.Review.Repeat", true)));
                SessionState.SetBool("EnemyTools.Review.Record", EditorGUILayout.Toggle("Capture review frames", SessionState.GetBool("EnemyTools.Review.Record", false)));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("0.5x")) SessionState.SetFloat("EnemyTools.Review.TimeScale", .5f);
                if (GUILayout.Button("1x")) SessionState.SetFloat("EnemyTools.Review.TimeScale", 1f);
                if (GUILayout.Button("Next"))
                {
                    int currentIndex = SessionState.GetInt("EnemyTools.Review.Scenario", _scenario);
                    _scenario = _advanced ? (currentIndex + 1) % EnemyValidationRunner.ReviewNames.Length : TargetedScenarios[(Mathf.Max(0, System.Array.IndexOf(TargetedScenarios, currentIndex)) + 1) % TargetedScenarios.Length];
                    SessionState.SetInt("EnemyTools.Review.Scenario", _scenario);
                }
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Stop / restore scene")) EditorApplication.isPlaying = false;
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
