using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Breachpoint.Gameplay.AI;

namespace Breachpoint.Editor.Enemies
{
    [InitializeOnLoad]
    public static partial class AdamPresentationIntegration
    {
        internal const string VisualPath = "Assets/Project/Art/Enemies/Adam/Prefabs/PF_Enemy_Adam.prefab";
        internal const string ControllerPath = "Assets/Project/Art/Enemies/Adam/Config/AC_Enemy_Adam.controller";
        internal const string RiflemanPath = "Assets/Project/Enemies/Prefabs/Rifleman.prefab";
        internal const string EvidencePath = "Docs/AI/AdamPresentation";
        private const string RequestPath = "Tools/AdamPresentation/request.txt";
        private static double _nextPoll;

        static AdamPresentationIntegration() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
            string command = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            Directory.CreateDirectory(EvidencePath);
            try
            {
                if (command == "audit") Audit();
                else if (command == "refresh") AssetDatabase.Refresh();
                else if (command == "setup") Setup();
                else if (command == "ragdoll") SetupRagdoll();
                else if (command == "math") ValidateMath();
                else if (command == "stage2") ValidateStage(2);
                else if (command == "stage3") ValidateStage(3);
                else if (command == "stage4") ValidateStage(4);
                else if (command == "death") ValidateStage(40);
                else if (command == "regression") ValidateStage(50);
                else if (command == "assets") ValidateWiring();
                else throw new InvalidOperationException("Unknown presentation command: " + command);
                File.WriteAllText(EvidencePath + "/status.txt", command + ": PASS\n" + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception exception)
            {
                File.WriteAllText(EvidencePath + "/status.txt", command + ": FAIL\n" + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Breachpoint/Enemies/Adam presentation/Wire existing Rifleman")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before wiring assets.");
            GameObject root = PrefabUtility.LoadPrefabContents(RiflemanPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null || AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) != ControllerPath)
                    throw new InvalidOperationException("Rifleman must contain the existing authored Adam controller.");
                Transform aim = FindUnique(animator.transform, "AimTarget");
                Transform muzzle = FindUnique(animator.transform, "Muzzle");
                Transform grip = FindUnique(animator.transform, "LeftHandGrip");
                if (muzzle.parent != grip.parent) throw new InvalidOperationException("Weapon muzzle/grip hierarchy changed; inspect before wiring.");
                var bridge = new SerializedObject(root.GetComponent<EnemyAnimationBridge>());
                bridge.FindProperty("_animator").objectReferenceValue = animator;
                bridge.FindProperty("_aimTarget").objectReferenceValue = aim;
                bridge.ApplyModifiedPropertiesWithoutUndo();
                var actor = new SerializedObject(root.GetComponent<EnemyActor>());
                actor.FindProperty("<Muzzle>k__BackingField").objectReferenceValue = muzzle;
                actor.ApplyModifiedPropertiesWithoutUndo();
                var vfx = new SerializedObject(root.GetComponent<EnemyVfxPresenter>());
                var flash = vfx.FindProperty("_muzzleFlash").objectReferenceValue as ParticleSystem;
                if (flash != null)
                {
                    flash.transform.SetParent(muzzle, false);
                    flash.transform.localPosition = Vector3.zero;
                    flash.transform.localRotation = Quaternion.identity;
                }
                PrefabUtility.SaveAsPrefabAsset(root, RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform FindUnique(Transform root, string name)
        {
            Transform found = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != name) continue;
                if (found != null) throw new InvalidOperationException("Ambiguous Adam transform: " + name);
                found = child;
            }
            return found != null ? found : throw new InvalidOperationException("Missing Adam transform: " + name);
        }

        [MenuItem("Breachpoint/Enemies/Adam presentation/Wire existing ragdoll")]
        public static void SetupRagdoll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before wiring assets.");
            GameObject root = PrefabUtility.LoadPrefabContents(RiflemanPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                Transform skeleton = FindUnique(animator.transform, "Adam_Reference");
                if (skeleton.GetComponentsInChildren<Rigidbody>(true).Length != 12)
                    throw new InvalidOperationException("Authored ragdoll body count changed; inspect before wiring.");
                EnemyRagdollPresenter presenter = root.GetComponent<EnemyRagdollPresenter>();
                if (presenter == null) presenter = root.AddComponent<EnemyRagdollPresenter>();
                var data = new SerializedObject(presenter);
                data.FindProperty("_animator").objectReferenceValue = animator;
                data.FindProperty("_rigBuilder").objectReferenceValue = animator.GetComponent<RigBuilder>();
                data.FindProperty("_skeletonRoot").objectReferenceValue = skeleton;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Breachpoint/Enemies/Adam presentation/Validate asset wiring (read only)")]
        public static void ValidateWiring()
        {
            var report = new StringBuilder();
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath);
            Animator animator = root.GetComponentInChildren<Animator>(true);
            var bridge = new SerializedObject(root.GetComponent<EnemyAnimationBridge>());
            var ragdoll = new SerializedObject(root.GetComponent<EnemyRagdollPresenter>());
            Check(root.GetComponent<EnemyLifetimeScope>().Archetype.IsValid, "Existing gameplay archetype remains valid", report);
            Check(root.GetComponent<EnemyActor>().Muzzle == FindUnique(animator.transform, "Muzzle"), "EnemyHitscanWeapon uses existing SCIFIAR muzzle", report);
            Check(bridge.FindProperty("_animator").objectReferenceValue == animator && bridge.FindProperty("_aimTarget").objectReferenceValue == FindUnique(animator.transform, "AimTarget"), "Bridge references existing Animator and AimTarget", report);
            Check(ragdoll.FindProperty("_animator").objectReferenceValue == animator && ragdoll.FindProperty("_rigBuilder").objectReferenceValue == animator.GetComponent<RigBuilder>(), "Ragdoll references existing Animator and RigBuilder", report);
            Transform skeleton = ragdoll.FindProperty("_skeletonRoot").objectReferenceValue as Transform;
            Check(skeleton != null && skeleton.name == "Adam_Reference" && skeleton.GetComponentsInChildren<Rigidbody>(true).Length == 12, "Ragdoll references existing primary skeleton and all 12 bodies", report);
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) Check(component != null, "Serialized component exists", report);
            foreach (var parameter in AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath).parameters) report.AppendLine("Authored parameter: " + parameter.name + " / " + parameter.type);
            report.AppendLine(ConsoleCounts());
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(EvidencePath + "/asset-validation.txt", report.ToString());
        }

        [MenuItem("Breachpoint/Enemies/Adam presentation/Validate calculations")]
        public static void ValidateMath()
        {
            var report = new StringBuilder();
            var config = new EnemyAnimationConfig();
            Check(Mathf.Approximately(EnemyAnimationMath.MoveSpeed(0f, 2f, 4.5f, config), 0f), "Stationary => idle", report);
            Check(Mathf.Approximately(EnemyAnimationMath.MoveSpeed(2f, 2f, 4.5f, config), 0.33f), "Walk speed => authored 0.33", report);
            Check(Mathf.Approximately(EnemyAnimationMath.MoveSpeed(4.5f, 2f, 4.5f, config), 0.66f), "Run speed => authored 0.66", report);
            Check(Mathf.Approximately(EnemyAnimationMath.MoveSpeed(6.75f, 2f, 4.5f, config), 1f), "Higher supplied speed => authored sprint 1", report);
            Check(!float.IsNaN(EnemyAnimationMath.MoveSpeed(2f, 2f, 2f, config)), "Equal configured speeds remain finite", report);
            Quaternion rotated = Quaternion.Euler(0f, 90f, 0f);
            Check(Vector2.Distance(EnemyAnimationMath.LocalMovement(rotated, Vector3.right * 2f, 2f), Vector2.up) < 0.001f, "Rotated world movement => enemy-local forward", report);
            Check(Vector2.Distance(EnemyAnimationMath.LocalMovement(rotated, Vector3.back * 2f, 2f), Vector2.right) < 0.001f, "Rotated world movement => enemy-local right", report);
            foreach (var pair in new[] { (Vector3.back, 0f), (Vector3.right, 1f), (Vector3.left, 2f), (Vector3.forward, 3f) })
                Check(EnemyAnimationMath.HitDirection(rotated, rotated * pair.Item1, true) == pair.Item2, "Death direction " + pair.Item2 + " independent of world rotation", report);
            Check(EnemyAnimationMath.HitDirection(Quaternion.identity, Vector3.forward, false) == 0f, "Rear nonlethal hit uses available generic reaction", report);
            Check(EnemyAnimationMath.HitDirection(Quaternion.identity, Vector3.zero, true) == 0f, "Missing direction has deterministic generic fallback", report);
            report.AppendLine(ConsoleCounts());
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(EvidencePath + "/calculation-tests.txt", report.ToString());
        }

        private static void Check(bool condition, string description, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            report.AppendLine("PASS: " + description);
        }

        [MenuItem("Breachpoint/Enemies/Adam presentation/Audit (read only)")]
        public static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("Unity " + Application.unityVersion + " | " + Application.dataPath);
            report.AppendLine("Play Mode: " + EditorApplication.isPlaying + " | Target: " + EditorUserBuildSettings.activeBuildTarget);
            foreach (var scene in EditorSceneManager.GetSceneManagerSetup()) report.AppendLine("Open scene: " + scene.path);
            report.AppendLine(ConsoleCounts());
            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (visual == null) throw new InvalidOperationException("Adam presentation prefab missing.");
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                report.Append(AnimationUtility.CalculateTransformPath(child, visual.transform) + " | ");
                foreach (Component component in child.GetComponents<Component>()) report.Append((component == null ? "MISSING" : component.GetType().Name) + ", ");
                report.AppendLine();
            }
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            report.AppendLine("Avatar human/valid: " + animator.avatar.isHuman + "/" + animator.avatar.isValid + " | Root motion: " + animator.applyRootMotion);
            RigBuilder rigBuilder = animator.GetComponent<RigBuilder>();
            foreach (var layer in rigBuilder.layers) report.AppendLine("Rig layer: " + layer.rig.name + " | active: " + layer.active);
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true)) report.AppendLine("Ragdoll: " + body.name + " | kinematic: " + body.isKinematic + " | mass: " + body.mass);
            foreach (MultiAimConstraint aim in visual.GetComponentsInChildren<MultiAimConstraint>(true))
                report.AppendLine("Constraint: " + aim.name + " | " + JsonUtility.ToJson(aim));
            foreach (TwoBoneIKConstraint ik in visual.GetComponentsInChildren<TwoBoneIKConstraint>(true))
                report.AppendLine("Constraint: " + ik.name + " | " + JsonUtility.ToJson(ik));
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            foreach (var parameter in controller.parameters) report.AppendLine("Parameter: " + parameter.name + " | " + parameter.type);
            foreach (var layer in controller.layers) DescribeMachine(layer.stateMachine, layer.name, report);
            GameObject rifleman = AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath);
            foreach (Component component in rifleman.GetComponents<Component>()) report.AppendLine("Gameplay root: " + component.GetType().Name);
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(EvidencePath + "/editor-audit.txt", report.ToString());
        }

        private static void DescribeMachine(AnimatorStateMachine machine, string path, StringBuilder report)
        {
            foreach (var child in machine.states)
            {
                AnimatorState state = child.state;
                report.AppendLine("State: " + path + "/" + state.name);
                DescribeMotion(state.motion, report);
                foreach (var transition in state.transitions) DescribeTransition(transition, report);
            }
            foreach (var transition in machine.anyStateTransitions) DescribeTransition(transition, report);
            foreach (var transition in machine.entryTransitions) DescribeTransition(transition, report);
            foreach (var child in machine.stateMachines) DescribeMachine(child.stateMachine, path + "/" + child.stateMachine.name, report);
        }

        private static void DescribeTransition(AnimatorTransitionBase transition, StringBuilder report)
        {
            report.Append("  -> " + (transition.destinationState != null ? transition.destinationState.name : transition.destinationStateMachine != null ? transition.destinationStateMachine.name : "Exit"));
            foreach (var condition in transition.conditions) report.Append(" | " + condition.parameter + " " + condition.mode + " " + condition.threshold);
            report.AppendLine();
        }

        private static void DescribeMotion(Motion motion, StringBuilder report)
        {
            if (motion is AnimationClip clip) report.AppendLine("  Clip: " + clip.name + " | seconds: " + clip.length + " | loop: " + clip.isLooping);
            if (!(motion is BlendTree tree)) return;
            report.AppendLine("  Tree: " + tree.name + " | " + tree.blendType + " | " + tree.blendParameter + "/" + tree.blendParameterY);
            foreach (var child in tree.children)
            {
                report.AppendLine("    Child: " + (child.motion != null ? child.motion.name : "MISSING") + " | threshold: " + child.threshold + " | position: " + child.position);
                DescribeMotion(child.motion, report);
            }
        }

        // Unity exposes no public Console reader; reflection is confined to this Editor audit.
        internal static string ConsoleCounts()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo method = entries?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return "Console counts unavailable.";
            object[] counts = { 0, 0, 0 };
            method.Invoke(null, counts);
            return "Console: errors=" + counts[0] + ", warnings=" + counts[1] + ", logs=" + counts[2];
        }
    }
}
