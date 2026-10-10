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
    public static partial class EnemyValidationRunner
    {
        internal const string VisualPath = "Assets/Project/Art/Enemies/Adam/Prefabs/PF_Enemy_Adam.prefab";
        internal const string ControllerPath = "Assets/Project/Art/Enemies/Adam/Config/AC_Enemy_Adam.controller";
        internal const string RiflemanPath = "Assets/Project/Enemies/Prefabs/Rifleman.prefab";
        internal const string EvidencePath = "Logs/EnemyValidation";
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

        [MenuItem("Breachpoint/Enemies/Validation/Validate asset wiring")]
        public static void ValidateWiring()
        {
            var report = new StringBuilder();
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath);
            Animator animator = root.GetComponentInChildren<Animator>(true);
            var bridge = new SerializedObject(root.GetComponent<EnemyAnimationBridge>());
            var ragdoll = new SerializedObject(root.GetComponent<EnemyRagdollPresenter>());
            CheckWindowScript<RiflemanLiveReviewWindow>("Assets/Project/Editor/Enemies/RiflemanLiveReviewWindow.cs", report);
            CheckWindowScript<RiflemanPoseReviewWindow>("Assets/Project/Editor/Enemies/RiflemanPoseReviewWindow.cs", report);
            Check(root.GetComponent<EnemyHighCoverPresentation>() != null && root.GetComponent<EnemyHighCoverPresentation>().IsConfigured, "Rifleman has its calibrated High Cover contact/motion profile", report);
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

        private static void CheckWindowScript<T>(string path, StringBuilder report) where T : EditorWindow
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Check(script != null && script.GetClass() == typeof(T), "Permanent review window has a serializable script binding: " + typeof(T).Name, report);
        }

        [MenuItem("Breachpoint/Enemies/Validation/Validate animation calculations")]
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
            ValidateHighCoverGeometry(report);
            report.AppendLine(ConsoleCounts());
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(EvidencePath + "/calculation-tests.txt", report.ToString());
        }

        private static void ValidateHighCoverGeometry(StringBuilder report)
        {
            var wall = new GameObject("High Cover geometry validation");
            var marker = new GameObject("High Cover corner validation");
            try
            {
                var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(4f, 3f, .6f);
                wall.transform.position = new Vector3(10f, 1.5f, -3f); wall.transform.localScale = new Vector3(1.3f, 1f, .8f);
                var point = marker.AddComponent<EnemyCoverPoint>();
                foreach(float yaw in new[] { 0f, 37f, 90f, 180f })
                {
                    wall.transform.rotation = Quaternion.Euler(0f, yaw, 0f); marker.transform.rotation = wall.transform.rotation;
                    foreach(var side in new[] { EnemyHighCoverSide.Left, EnemyHighCoverSide.Right })
                    {
                        point.ConfigureHighEdge(box,side);
                        Vector3 relative = point.ProtectedPosition - wall.transform.position;
                        float lateral = Vector3.Dot(relative, marker.transform.right);
                        float depth = Vector3.Dot(relative, marker.transform.forward);
                        float sign = side == EnemyHighCoverSide.Right ? 1f : -1f;
                        Check(Mathf.Abs(lateral - sign * (2.6f - .45f)) < .001f && Mathf.Abs(depth + .24f + .38f) < .001f,
                            "Scaled/rotated High Cover stays at " + side + " corner and near wall: " + yaw, report);
                        Check(Vector3.Dot(point.ExposurePosition-point.ProtectedPosition,marker.transform.right)*sign > 1f,
                            "Exposure remains outside the matching edge: " + side + " / " + yaw, report);
                    }
                }
                point.Configure(EnemyCoverKind.Low, box, null);
                Check(point.ProtectedPosition==marker.transform.position && point.ExposurePosition==marker.transform.position,"Low Cover continues to use its authored marker",report);
            }
            finally { UnityEngine.Object.DestroyImmediate(marker); UnityEngine.Object.DestroyImmediate(wall); }
        }

        private static void Check(bool condition, string description, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            report.AppendLine("PASS: " + description);
        }

        internal static string ConsoleDetails()
        {
            var assembly = typeof(EditorApplication).Assembly;
            var entries = assembly.GetType("UnityEditor.LogEntries");
            var entryType = assembly.GetType("UnityEditor.LogEntry");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var start = entries?.GetMethod("StartGettingEntries", flags);
            var end = entries?.GetMethod("EndGettingEntries", flags);
            var read = entries?.GetMethod("GetEntryInternal", flags);
            var message = entryType?.GetField("message") ?? entryType?.GetField("condition");
            if (start == null || end == null || read == null || message == null) return "Console detail reader unavailable.";
            var report = new StringBuilder();
            int count = (int)start.Invoke(null, null);
            try
            {
                for (int index = 0; index < Mathf.Min(count, 100); index++)
                {
                    var entry = Activator.CreateInstance(entryType);
                    if ((bool)read.Invoke(null, new[] { (object)index, entry })) report.AppendLine((string)message.GetValue(entry));
                }
            }
            finally { end.Invoke(null, null); }
            return report.ToString();
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
