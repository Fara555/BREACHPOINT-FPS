using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Weapons.Effects;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    [InitializeOnLoad]
    public static partial class EnemyTools
    {

        internal const string Evidence = "Logs/EnemyValidation";
        internal const string ControllerPath = EnemyValidationRunner.ControllerPath;
        internal const string AnimationFolder = "Assets/Project/Art/Enemies/Adam/Animations";
        private const string Request = "Tools/EnemyTools/request.txt";
        private static double _nextPoll;
        static EnemyTools() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Request)) return;
            string command;
            try
            {
                command = File.ReadAllText(Request).Trim();
                if (command.Length == 0) return;
                File.Delete(Request);
            }
            catch (IOException) { return; }
            try
            {
                Directory.CreateDirectory(Evidence);
                if (command.StartsWith("crouch-probe-", StringComparison.Ordinal)) { SessionState.SetString("EnemyTools.CrouchDeath.Label", command.Substring(13)); EnemyValidationRunner.ValidateStage(175); }
                else if (command.StartsWith("death-probe-", StringComparison.Ordinal)) { SessionState.SetString("EnemyTools.CrouchDeath.Label", command.Substring(12)); EnemyValidationRunner.ValidateStage(176); }
                else if (command.StartsWith("crouch-death-capture:",StringComparison.Ordinal))
                {
                    EnemyValidationRunner.RunHumanTacticalReview(int.Parse(command.Substring("crouch-death-capture:".Length)),false);
                    SessionState.SetBool("EnemyTools.CrouchDeath.Capture",true);
                }
                else if (command == "active-death-settings") ConfigureActiveDeath();
                else if (command.StartsWith("death-tracking-", StringComparison.Ordinal)) { SessionState.SetString("EnemyTools.DeathTracking.Label", command.Substring(15)); EnemyValidationRunner.ValidateStage(180); }
                else if (command == "moving-impact-tests") EnemyValidationRunner.ValidateStage(181);
                else if (command == "editor-tools-test") ValidateEditorTools();
                else if (command == "human-review-control-test") EnemyValidationRunner.ValidateHumanReviewControls();
                else if (command == "active-death-tests") EnemyValidationRunner.ValidateStage(179);
                else if (command == "crouch-death-tests") EnemyValidationRunner.ValidateStage(177);
                else if (command == "death-classification-tests") EnemyValidationRunner.ValidateStage(178);
                else if (command == "animation-inventory") InventoryEnemyAnimations();
                else if (command == "animation-organize") OrganizeEnemyAnimations();
                else if (command == "animation-deduplicate") ConsolidateDirectionalDeathSources();
                else if (command == "tactical-review-open") EnemyTacticalDebugWindow.Open();
                else if (command == "tactical-review-stop") EnemyValidationRunner.StopHumanTacticalReview();
                else if (command == "strafe-review-tests") EnemyValidationRunner.ValidateStage(174);
                else if (command.StartsWith("tactical-review-repeat:", StringComparison.Ordinal)) EnemyValidationRunner.RunHumanTacticalReview(int.Parse(command.Substring(23)), true);
                else if (command.StartsWith("tactical-review:", StringComparison.Ordinal)) EnemyValidationRunner.RunHumanTacticalReview(int.Parse(command.Substring(16)), false);
                else if (command == "strafe-audit") EnemyValidationRunner.AuditCombatStrafe();
                else if (command.StartsWith("strafe-probe-", StringComparison.Ordinal)) { SessionState.SetString("EnemyTools.Strafe.Label", command.Substring(13)); SessionState.SetBool("EnemyTools.Strafe.Acceptance", command.Substring(13) != "before"); EnemyValidationRunner.ValidateStage(171); }
                else if (command == "final-short-review") EnemyValidationRunner.ValidateStage(170);
                else if (command == "final-review") EnemyValidationRunner.ValidateStage(169);
                else if (command == "final-source") EnemyValidationRunner.ValidateStage(167);
                else if (command.StartsWith("final-probe-", StringComparison.Ordinal)) { SessionState.SetString("EnemyTools.Final.Label", command.Substring(12)); EnemyValidationRunner.ValidateStage(168); }
                else if (command == "sync-source-review") EnemyValidationRunner.RunSourceTurnReview();
                else if (command == "sync-test") EnemyValidationRunner.ValidateStage(165);
                else if (command == "sync-source") EnemyValidationRunner.ValidateStage(164);
                else if (command == "sync-curves") EnemyValidationRunner.AuditSynchronizationCurves();
                else if (command == "sync-probe") EnemyValidationRunner.ValidateStage(163);
                else if (command == "steady-start-test") EnemyValidationRunner.ValidateStage(162);
                else if (command == "steady-mapping") RiflemanAnimatorBuilder.RefreshSteadyMapping();
                else if (command == "movement-polish-test") EnemyValidationRunner.ValidateStage(161);
                else if (command == "movement-pose-audit") EnemyValidationRunner.ValidateStage(160);
                else if (command == "audit") Audit();
                else if (command == "pose-test") EnemyValidationRunner.CompareSourcePoses();
                else if (command == "math") EnemyValidationRunner.ValidateMath();
                else if (command == "regression") EnemyValidationRunner.ValidateStage(50);
                else if (command == "assets") EnemyValidationRunner.ValidateWiring();
                else if (command == "vfx-audit") AuditWeaponEffects();
                else if (command == "vfx-test") EnemyValidationRunner.ValidateStage(60);
                else if (command == "freeze") EnemyValidationRunner.FreezePoseComparison();
                else if (command == "freeze-control-test") EnemyValidationRunner.ValidateFrozenPoseControls();
                else if (command == "review-test") EnemyValidationRunner.RunReview(-1, false);
                else if (command == "review-control-test") EnemyValidationRunner.ValidateReviewControls();
                else if (command == "presentation-snapshot")
                {
                    var snapshot = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
                    foreach (var bridge in Object.FindObjectsByType<EnemyAnimationBridge>())
                    {
                        var brain = bridge.GetComponent<EnemyBrain>();
                        var actor = bridge.GetComponent<EnemyActor>();
                        snapshot.AppendLine($"{bridge.name}: state={EnemyAnimationStateNames.Get(bridge.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash)} body={bridge.BodyAimErrorDegrees} muzzle={bridge.MuzzleAimErrorDegrees} target={bridge.AimTarget.position} expected={brain.Memory.Target?.AimPosition} visible={brain.Memory.Visible} alive={brain.Memory.Target?.IsAlive} velocity={actor.Navigation.Velocity} move={bridge.MoveSpeedSmoothed}");
                    }
                    File.WriteAllText(Evidence + "/presentation-snapshot.txt", snapshot.ToString());
                }
                else if (command == "review-capture")
                {
                    if (!EditorApplication.isPlaying || !SessionState.GetBool("EnemyTools.Validation", false)) throw new InvalidOperationException("Run an owned review first.");
                    ScreenCapture.CaptureScreenshot(Evidence + "/review-" + Time.frameCount + ".png");
                }
                else if (command == "review-stop")
                {
                    if (!EditorApplication.isPlaying || !SessionState.GetBool("EnemyTools.Validation", false) || SessionState.GetInt("EnemyTools.Validation.Stage", 0) < 130)
                        throw new InvalidOperationException("No owned presentation review is running.");
                    EditorApplication.isPlaying = false;
                }
                else if (command.StartsWith("review-record:", StringComparison.Ordinal)) { SessionState.SetBool("EnemyTools.Review.Record", true); EnemyValidationRunner.RunReview(int.Parse(command.Substring(14)), false); }
                else if (command.StartsWith("review-case:", StringComparison.Ordinal)) EnemyValidationRunner.RunReview(int.Parse(command.Substring(12)), false);
                else if (command.StartsWith("review:", StringComparison.Ordinal)) EnemyValidationRunner.RunReview(int.Parse(command.Substring(7)), true);
                else if (command == "arena-audit") AuditArena();
                else if (command == "arena-setup") WireArenaCover();
                else if (command == "refresh") AssetDatabase.Refresh();
                else if (command == "animator") RiflemanAnimatorBuilder.Rebuild();
                else if (command == "performance-test") EnemyValidationRunner.ValidateStage(110);
                else if (command == "edge-test") EnemyValidationRunner.ValidateStage(120);
                else if (command == "tactical-test") EnemyValidationRunner.RunTacticalScenario(-1);
                else if (command.StartsWith("tactical-from:", StringComparison.Ordinal)) EnemyValidationRunner.RunTacticalRange(int.Parse(command.Substring(14)));
                else if (command.StartsWith("tactical-case:", StringComparison.Ordinal)) EnemyValidationRunner.RunTacticalScenario(int.Parse(command.Substring(14)));
                else if (command == "policy-test") EnemyValidationRunner.ValidateTacticalPolicy();
                else if (command == "cover-test") EnemyValidationRunner.ValidateStage(80);
                else if (command == "squad-test") EnemyValidationRunner.ValidateStage(90);
                else if (command == "animation-test") EnemyValidationRunner.RunAnimationMatrix();
                else if (command.StartsWith("animation-case:", StringComparison.Ordinal)) EnemyValidationRunner.RunAnimationScenario(int.Parse(command.Substring(15)));
                else if (command.StartsWith("animation-from:", StringComparison.Ordinal)) EnemyValidationRunner.RunAnimationRange(int.Parse(command.Substring(15)));
                else throw new InvalidOperationException("Unknown enemy-tool command: " + command);
                File.WriteAllText(Evidence + "/status.txt", command + (command == "refresh" || EditorApplication.isPlayingOrWillChangePlaymode ? ": DISPATCHED\n" : ": PASS\n") + DateTime.UtcNow.ToString("O") + "\n" + EnemyValidationRunner.ConsoleCounts());
            }
            catch (Exception exception)
            {
                File.WriteAllText(Evidence + "/status.txt", command + ": FAIL\n" + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Inspect Animator and clips")]
        public static void Audit()
        {
            Directory.CreateDirectory(Evidence);
            var report = new StringBuilder();
            var references = new Dictionary<AnimationClip, List<string>>();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            report.AppendLine("Unity " + Application.unityVersion + " | " + DateTime.UtcNow.ToString("O"));
            report.AppendLine(EnemyValidationRunner.ConsoleCounts());
            foreach (var parameter in controller.parameters) report.AppendLine("Parameter " + parameter.name + " | " + parameter.type);
            foreach (var layer in controller.layers)
            {
                report.AppendLine("Layer " + layer.name + " | weight=" + layer.defaultWeight + " | mask=" + AssetDatabase.GetAssetPath(layer.avatarMask));
                DescribeMachine(layer.stateMachine, layer.name, report, references);
            }
            File.WriteAllText(Evidence + "/animator-graph.txt", report.ToString());
            var inventory = new StringBuilder("Asset,Clip,Loop,Seconds,AverageAngularSpeed,StatesOrTrees,Status,PlayMode\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                    references.TryGetValue(clip, out List<string> owners);
                    inventory.AppendLine(string.Join(",", Csv(path), Csv(clip.name), clip.isLooping,
                        clip.length.ToString(System.Globalization.CultureInfo.InvariantCulture), clip.averageAngularSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Csv(owners == null ? "" : string.Join("; ", owners)), owners == null ? "UNREFERENCED" : "REFERENCED - topology requires scenario verification", "NOT RUN"));
                }
            }
            File.WriteAllText(Evidence + "/animation-inventory.csv", inventory.ToString());
        }

        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        private static void DescribeMachine(AnimatorStateMachine machine, string path, StringBuilder text, Dictionary<AnimationClip, List<string>> refs)
        {
            text.AppendLine("Machine " + path + " | default=" + machine.defaultState?.name);
            DescribeTransitions(machine.anyStateTransitions, path + "/AnyState", text);
            DescribeTransitions(machine.entryTransitions, path + "/Entry", text);
            foreach (var child in machine.states)
            {
                AnimatorState state = child.state;
                string statePath = path + "/" + state.name;
                text.AppendLine("State " + statePath + " | tag=" + state.tag + " | speed=" + state.speed + " | writeDefaults=" + state.writeDefaultValues);
                DescribeTransitions(state.transitions, statePath, text);
                DescribeMotion(state.motion, statePath, text, refs);
            }
            foreach (var child in machine.stateMachines)
            {
                DescribeTransitions(machine.GetStateMachineTransitions(child.stateMachine), path + "/" + child.stateMachine.name + "/Exit", text);
                DescribeMachine(child.stateMachine, path + "/" + child.stateMachine.name, text, refs);
            }
        }
        private static void DescribeTransitions(AnimatorTransitionBase[] transitions, string source, StringBuilder text)
        {
            for (int i = 0; i < transitions.Length; i++)
            {
                AnimatorTransitionBase transition = transitions[i];
                text.Append(source + " -> " + (transition.destinationState != null ? transition.destinationState.name : transition.destinationStateMachine != null ? transition.destinationStateMachine.name : "Exit") + " | priority=" + i);
                if (transition is AnimatorStateTransition timed)
                    text.Append(" | exit=" + timed.hasExitTime + "/" + timed.exitTime + " | duration=" + timed.duration + " | fixed=" + timed.hasFixedDuration + " | offset=" + timed.offset + " | interruption=" + timed.interruptionSource + " | ordered=" + timed.orderedInterruption + " | self=" + timed.canTransitionToSelf);
                foreach (var condition in transition.conditions) text.Append(" | " + condition.parameter + " " + condition.mode + " " + condition.threshold);
                text.AppendLine();
            }
        }
        private static void DescribeMotion(Motion motion, string owner, StringBuilder text, Dictionary<AnimationClip, List<string>> refs)
        {
            if (motion is AnimationClip clip)
            {
                if (!refs.TryGetValue(clip, out List<string> owners)) { owners = new List<string>(); refs.Add(clip, owners); }
                owners.Add(owner);
                text.AppendLine("  Clip " + AssetDatabase.GetAssetPath(clip) + " | loop=" + clip.isLooping);
            }
            else if (motion is BlendTree tree)
            {
                text.AppendLine("  Tree " + tree.name + " | " + tree.blendType + " | " + tree.blendParameter + "/" + tree.blendParameterY);
                foreach (var child in tree.children)
                {
                    text.AppendLine("    " + child.motion?.name + " | threshold=" + child.threshold + " | position=" + child.position + " | timeScale=" + child.timeScale);
                    DescribeMotion(child.motion, owner + "/" + tree.name, text, refs);
                }
            }
        }

        private const string PlayerPath = "Assets/Project/Prefabs/Player.prefab";
        private const string PrefabFolder = "Assets/Project/Enemies/Prefabs";
        private const string ReportFolder = Evidence;

        [MenuItem("Breachpoint/Enemies/Validation/Inspect weapon effects")]
        public static void AuditWeaponEffects()
        {
            var report = new StringBuilder();
            report.AppendLine("Enemy weapon effects | " + DateTime.UtcNow.ToString("O"));
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            foreach (WeaponMuzzleFlash flash in player.GetComponentsInChildren<WeaponMuzzleFlash>(true))
            {
                report.AppendLine("Player muzzle: " + AnimationUtility.CalculateTransformPath(flash.transform, player.transform));
                report.AppendLine("Scale: " + flash.transform.localScale + " | " + EditorJsonUtility.ToJson(flash));
                foreach (Transform child in flash.GetComponentsInChildren<Transform>(true))
                    report.AppendLine("  " + child.name + " | " + string.Join(", ", child.GetComponents<Component>().Select(c => c.GetType().Name)));
            }
            foreach (WeaponProjectileTracerPool tracer in player.GetComponentsInChildren<WeaponProjectileTracerPool>(true))
                report.AppendLine("Player tracer: " + EditorJsonUtility.ToJson(tracer));
            foreach (string path in PrefabPaths())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.AppendLine(path);
                foreach (Component effect in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (effect is ParticleSystem || effect is LineRenderer || effect is WeaponMuzzleFlash || effect is WeaponProjectileTracerPool)
                        report.AppendLine("  " + AnimationUtility.CalculateTransformPath(effect.transform, prefab.transform) + " | " + effect.GetType().Name);
                }
                report.AppendLine(EditorJsonUtility.ToJson(prefab.GetComponent<EnemyVfxPresenter>()));
            }
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/asset-audit.txt", report.ToString());
        }

        internal static void ValidateWeaponEffects(GameObject root)
        {
            if (root.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
                throw new InvalidOperationException("Legacy hit/muzzle particles remain: " + root.name);
            if (root.GetComponentsInChildren<LineRenderer>(true).Length != 0)
                throw new InvalidOperationException("Legacy static tracer remains: " + root.name);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException("Missing script: " + child.name);
            if (root.GetComponent<EnemyHitscanWeapon>() == null) return;
            var presenter = new SerializedObject(root.GetComponent<EnemyVfxPresenter>());
            var flash = presenter.FindProperty("_muzzleFlashEffect").objectReferenceValue as WeaponMuzzleFlash;
            var tracer = presenter.FindProperty("_tracerPool").objectReferenceValue as WeaponProjectileTracerPool;
            if (flash == null || tracer == null || flash.transform.parent != root.GetComponent<EnemyActor>().Muzzle)
                throw new InvalidOperationException("Enemy effects are not wired to the actual muzzle.");
            if (new SerializedObject(flash).FindProperty("_sparksAsset").objectReferenceValue == null ||
                new SerializedObject(flash).FindProperty("_flareMaterial").objectReferenceValue == null ||
                new SerializedObject(tracer).FindProperty("_tracerMaterial").objectReferenceValue == null)
                throw new InvalidOperationException("An existing VFX asset/material was not assigned.");
        }

        private static string[] PrefabPaths() => AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyVfxPresenter>() != null).ToArray();

    }
}
