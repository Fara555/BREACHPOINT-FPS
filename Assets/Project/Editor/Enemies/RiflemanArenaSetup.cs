using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyTools
    {
        private const string ArenaPath = "Assets/Project/Enemies/EnemyArena.unity";
        private static void AuditArena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before scene inspection.");
            Scene scene = SceneManager.GetSceneByPath(ArenaPath);
            bool temporary = !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Additive);
            try
            {
                var report = new StringBuilder();
                report.AppendLine("Read-only collider and cover authoring audit");
                foreach (var root in scene.GetRootGameObjects())
                {
                    report.AppendLine("ROOT " + root.name + " | " + root.transform.position);
                    foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                        report.AppendLine("COLLIDER " + HierarchyPath(collider.transform) + " | " + collider.GetType().Name + " | center=" + collider.bounds.center.ToString("F3") + " | size=" + collider.bounds.size.ToString("F3") + " | layer=" + collider.gameObject.layer + " | enabled=" + collider.enabled + " | trigger=" + collider.isTrigger);
                }
                File.WriteAllText(Evidence + "/arena-geometry.txt", report.ToString());
            }
            finally { if (temporary) EditorSceneManager.CloseScene(scene, true); }
        }
        [MenuItem("Breachpoint/Enemies/Cover Authoring/Wire EnemyArena cover")]
        public static void WireArenaCover()
        {
            Directory.CreateDirectory(Evidence);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before cover authoring.");
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save unsaved scene work before cover authoring.");
            Scene scene = SceneManager.GetSceneByPath(ArenaPath); bool temporary = !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                var scope = Array.Find(roots, root => root.GetComponent<GameLifetimeScope>() != null).GetComponent<GameLifetimeScope>();
                var low = Array.Find(roots, root => root.name == "Cover").GetComponent<Collider>();
                var high = Array.Find(roots, root => root.name == "Sight blocker").GetComponent<Collider>();
                GameObject group = Array.Find(roots, root => root.name == "Authored tactical cover");
                if (group == null) { group = new GameObject("Authored tactical cover"); SceneManager.MoveGameObjectToScene(group, scene); }
                var points = new List<EnemyCoverPoint>();
                AddArenaPoint(group.transform, "Low north", EnemyCoverKind.Low, low, new Vector3(-7, 0, 2.9f), Vector3.back, null, points);
                AddArenaPoint(group.transform, "Low south", EnemyCoverKind.Low, low, new Vector3(-7, 0, -2.9f), Vector3.forward, null, points);
                AddArenaPoint(group.transform, "High north left", EnemyCoverKind.High, high, new Vector3(0, 0, 2.3f), Vector3.back, new Vector3(-4.4f, 0, 2.3f), points);
                AddArenaPoint(group.transform, "High south left", EnemyCoverKind.High, high, new Vector3(0, 0, -0.3f), Vector3.forward, new Vector3(-4.4f, 0, -0.3f), points);
                var serialized = new SerializedObject(scope);
                var property = serialized.FindProperty("_coverPoints"); property.arraySize = points.Count;
                for (int i = 0; i < points.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("EnemyArena save failed.");
                File.WriteAllText(Evidence + "/arena-cover-setup.txt", "Added four local authored points for the two existing in-bounds barriers. GameLifetimeScope references all four. Existing geometry, actor placement, authored rigs, navigation data and other scene roots are preserved. No navigation rebake required.\n" + EnemyValidationRunner.ConsoleCounts());
            }
            finally
            {
                if (temporary) { EditorSceneManager.CloseScene(scene, true); EditorSceneManager.RestoreSceneManagerSetup(setup); }
            }
        }
        private static void AddArenaPoint(Transform parent, string name, EnemyCoverKind kind, Collider protection, Vector3 position, Vector3 normal, Vector3? exposurePosition, List<EnemyCoverPoint> points)
        {
            Transform child = parent.Find(name);
            if (child == null) { var created = new GameObject(name); created.transform.SetParent(parent); child = created.transform; }
            child.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
            Transform exposure = null;
            if (exposurePosition.HasValue)
            {
                exposure = child.Find("Exposure");
                if (exposure == null) { var created = new GameObject("Exposure"); created.transform.SetParent(child); exposure = created.transform; }
                exposure.position = exposurePosition.Value;
            }
            var point = child.GetComponent<EnemyCoverPoint>();
            if (point == null) point = child.gameObject.AddComponent<EnemyCoverPoint>();
            point.Configure(kind, protection, exposure);
            points.Add(point);
        }
        private static string HierarchyPath(Transform value)
        { string path = value.name; while (value.parent != null) { value = value.parent; path = value.name + "/" + path; } return path; }
    }
}
