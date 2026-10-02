using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    [InitializeOnLoad]
    public static partial class RiflemanRework
    {
        internal const string Evidence = "Docs/AI/RiflemanRework";
        internal const string ControllerPath = AdamPresentationIntegration.ControllerPath;
        internal const string AnimationFolder = "Assets/Project/Art/Enemies/Adam/Animations";
        private const string Request = "Tools/RiflemanRework/request.txt";
        private static double _nextPoll;
        static RiflemanRework() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            Directory.CreateDirectory(Evidence);
            try
            {
                if (command == "audit") Audit();
                else if (command == "arena-audit") AuditArena();
                else if (command == "arena-setup") WireArenaCover();
                else if (command == "refresh") AssetDatabase.Refresh();
                else if (command == "animator") RiflemanAnimatorBuilder.Rebuild();
                else if (command == "stance") WireStance();
                else if (command == "tactics-setup") WireTactics();
                else if (command == "performance-test") AdamPresentationIntegration.ValidateStage(110);
                else if (command == "edge-test") AdamPresentationIntegration.ValidateStage(120);
                else if (command == "tactical-test") AdamPresentationIntegration.RunTacticalScenario(-1);
                else if (command.StartsWith("tactical-from:", StringComparison.Ordinal)) AdamPresentationIntegration.RunTacticalRange(int.Parse(command.Substring(14)));
                else if (command.StartsWith("tactical-case:", StringComparison.Ordinal)) AdamPresentationIntegration.RunTacticalScenario(int.Parse(command.Substring(14)));
                else if (command == "policy-test") AdamPresentationIntegration.ValidateTacticalPolicy();
                else if (command == "cover-test") AdamPresentationIntegration.ValidateStage(80);
                else if (command == "squad-test") AdamPresentationIntegration.ValidateStage(90);
                else if (command == "animation-test") AdamPresentationIntegration.RunAnimationMatrix();
                else if (command.StartsWith("animation-case:", StringComparison.Ordinal)) AdamPresentationIntegration.RunAnimationScenario(int.Parse(command.Substring(15)));
                else throw new InvalidOperationException("Unknown rework stage: " + command);
                File.WriteAllText(Evidence + "/status.txt", command + (EditorApplication.isPlayingOrWillChangePlaymode ? ": DISPATCHED\n" : ": PASS\n") + DateTime.UtcNow.ToString("O") + "\n" + AdamPresentationIntegration.ConsoleCounts());
            }
            catch (Exception exception)
            {
                File.WriteAllText(Evidence + "/status.txt", command + ": FAIL\n" + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Breachpoint/Enemies/AI Test / Tactical Debug/Forensic Animator audit")]
        public static void Audit()
        {
            Directory.CreateDirectory(Evidence);
            var report = new StringBuilder();
            var references = new Dictionary<AnimationClip, List<string>>();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            report.AppendLine("Unity " + Application.unityVersion + " | " + DateTime.UtcNow.ToString("O"));
            report.AppendLine(AdamPresentationIntegration.ConsoleCounts());
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

        private static void WireStance()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AdamPresentationIntegration.RiflemanPath);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                if (root.GetComponent<Breachpoint.Gameplay.AI.EnemyStance>() == null) root.AddComponent<Breachpoint.Gameplay.AI.EnemyStance>();
                var rigPresenter = root.GetComponent<Breachpoint.Gameplay.AI.EnemyRigPresenter>();
                if (rigPresenter == null) rigPresenter = root.AddComponent<Breachpoint.Gameplay.AI.EnemyRigPresenter>();
                var rigData = new SerializedObject(rigPresenter);
                var rigs = root.GetComponentsInChildren<UnityEngine.Animations.Rigging.Rig>(true);
                rigData.FindProperty("_aimRig").objectReferenceValue = rigs.Single(rig => rig.name == "AimRig");
                rigData.FindProperty("_leftHandRig").objectReferenceValue = rigs.Single(rig => rig.name == "LeftHandRig");
                rigData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AdamPresentationIntegration.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void WireTactics()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before configuration changes.");
            var tactics = CreateConfig<Breachpoint.Gameplay.AI.EnemyTacticalConfig>("RiflemanTactics");
            var squad = CreateConfig<Breachpoint.Gameplay.AI.EnemySquadConfig>("Squad");
            var cover = CreateConfig<Breachpoint.Gameplay.AI.EnemyCoverConfig>("Cover");
            var data = new SerializedObject(tactics);
            data.FindProperty("<CommittedCoverBonus>k__BackingField").floatValue = 85f;
            data.FindProperty("<CoverPreferenceBonus>k__BackingField").floatValue = 50f;
            data.FindProperty("<Squad>k__BackingField").objectReferenceValue = squad;
            data.FindProperty("<Cover>k__BackingField").objectReferenceValue = cover;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(tactics); AssetDatabase.SaveAssetIfDirty(tactics);
            var rifleman = AssetDatabase.LoadAssetAtPath<Breachpoint.Gameplay.AI.EnemyArchetypeConfig>("Assets/Project/Enemies/Configs/Rifleman.asset");
            data = new SerializedObject(rifleman); data.FindProperty("<Tactics>k__BackingField").objectReferenceValue = tactics;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(rifleman);
            var coverData = new SerializedObject(cover);
            coverData.FindProperty("<StandingMuzzleHeight>k__BackingField").floatValue = 1.4f;
            coverData.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(cover); AssetDatabase.SaveAssetIfDirty(cover);
            File.WriteAllText(Evidence + "/config-integration.txt", "CommittedCoverBonus=" + tactics.CommittedCoverBonus + ", CoverPreferenceBonus=" + tactics.CoverPreferenceBonus + ", AdvanceScore=" + tactics.AdvanceScore + ", StandingMuzzleHeight=" + cover.StandingMuzzleHeight);
            WireStance();
        }
        private static T CreateConfig<T>(string name) where T : ScriptableObject
        {
            string path = "Assets/Project/Enemies/Configs/" + name + ".asset";
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
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
                    text.Append(" | exit=" + timed.hasExitTime + "/" + timed.exitTime + " | duration=" + timed.duration + " | fixed=" + timed.hasFixedDuration + " | interruption=" + timed.interruptionSource + " | ordered=" + timed.orderedInterruption + " | self=" + timed.canTransitionToSelf);
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
    }
}
