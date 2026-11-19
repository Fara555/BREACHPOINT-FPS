using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Player.Input;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class BattlefieldBlockout
    {
        internal const string Folder = "Assets/Project/Scenes/BattlefieldBlockout";
        internal const string ScenePath = Folder + "/BattlefieldBlockout.unity";
        internal const string ReportFolder = "Logs/BattlefieldBlockout";
        internal const int EnemyCount = 6;
        internal const string MainSquad = "Blockout B squad";
        internal const string SupportSquad = "Blockout B support";
        internal static readonly Vector3 Entry = new Vector3(6, 0, 22);
        internal static readonly Vector3 Exit = new Vector3(-4, 0, 62);
        private const string RiflemanPath = "Assets/Project/Prefabs/Enemy/Rifleman.prefab";

        [MenuItem("Breachpoint/Level Design/Blockout B/Create Separate Prototype")]
        public static void Create()
        {
            BattlefieldBaseline.RequireIdleEditor();
            if (File.Exists(ScenePath) || AssetDatabase.IsValidFolder(Folder))
                throw new InvalidOperationException("The blockout already exists. Edit it and use Bake and Validate; creation never replaces authored work.");
            AssetDatabase.CreateFolder("Assets/Project/Scenes", "BattlefieldBlockout");
            AssetDatabase.CreateFolder(Folder, "Materials");
            if (!AssetDatabase.CopyAsset(BattlefieldBaseline.ArenaPath, ScenePath)) throw new InvalidOperationException("Could not copy the arena composition.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool infrastructure = root.GetComponent<GameLifetimeScope>() != null || root.GetComponent<Light>() != null ||
                    root.GetComponentInChildren<PlayerInputReader>() != null;
                if (!infrastructure) Object.DestroyImmediate(root);
            }

            var scope = FindInScene<GameLifetimeScope>(scene).Single();
            var player = FindInScene<PlayerInputReader>(scene).Single();
            player.transform.position = Entry + Vector3.up * 1.09f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            var rootObject = new GameObject("Blockout B - Service Quarter");
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            Transform navigation = Group("01 Architecture and navigation", rootObject.transform);
            Transform coverRoot = Group("02 Authored cover", rootObject.transform);
            Transform actors = Group("03 Rifleman squad", rootObject.transform);
            Transform routes = Group("04 Patrol routes", rootObject.transform);
            Transform markers = Group("05 Review markers and signs", rootObject.transform);

            Material floor = MaterialAsset("Ground", new Color(.23f, .25f, .27f));
            Material building = MaterialAsset("Architecture", new Color(.39f, .41f, .43f));
            Material boundary = MaterialAsset("Boundary", new Color(.27f, .29f, .31f));
            Material cover = MaterialAsset("Equipment", new Color(.51f, .46f, .36f));
            Material gallery = MaterialAsset("Gallery", new Color(.34f, .38f, .4f));
            Material marking = MaterialAsset("Wayfinding", new Color(.43f, .72f, .7f));

            Box(navigation, "Ground 36 x 44", new Vector3(0, -.25f, 42), new Vector3(36, .5f, 44), floor, walkableTop: true);
            Box(navigation, "West facade", new Vector3(-18.5f, 4, 42), new Vector3(1, 8, 46), boundary);
            Box(navigation, "East facade", new Vector3(18.5f, 4, 42), new Vector3(1, 8, 46), boundary);
            Box(navigation, "South facade", new Vector3(0, 4, 19.5f), new Vector3(38, 8, 1), boundary);
            Box(navigation, "North facade", new Vector3(0, 4, 64.5f), new Vector3(38, 8, 1), boundary);
            BoxCollider energy = Box(navigation, "Energy module 10 x 10", new Vector3(-1, 2.25f, 41), new Vector3(10, 4.5f, 10), building);
            BoxCollider workshop = Box(navigation, "West workshop", new Vector3(-14.5f, 2.5f, 40.5f), new Vector3(7, 5, 13), building);
            Box(navigation, "Entry service block", new Vector3(-4, 2, 28), new Vector3(10, 4, 4), building);
            Box(navigation, "Entry sight screen", new Vector3(7, 2, 29), new Vector3(6, 4, 2), building);
            Box(navigation, "Exit sight screen", new Vector3(5, 2, 57), new Vector3(12, 4, 2), building);
            Box(navigation, "Gallery 1.5m", new Vector3(15.5f, .75f, 44), new Vector3(5, 1.5f, 8), gallery, walkableTop: true);
            Ramp(navigation, "Gallery south ramp", new Vector3(15.5f, 0, 36), false, gallery);
            Ramp(navigation, "Gallery north ramp", new Vector3(15.5f, 0, 52), true, gallery);
            BoxCollider rail = Box(navigation, "Gallery inward parapet", new Vector3(13.1f, 2.05f, 44), new Vector3(.2f, 1.1f, 8), gallery);
            BoxCollider entryCover = Box(navigation, "Equipment pallets", new Vector3(1, .55f, 31.6f), new Vector3(4, 1.1f, 1.2f), cover);
            BoxCollider eastCover = Box(navigation, "Loading equipment", new Vector3(8.5f, .55f, 43.6f), new Vector3(3, 1.1f, 1.2f), cover);
            BoxCollider northCover = Box(navigation, "Service unit", new Vector3(-5.5f, .55f, 51.6f), new Vector3(3, 1.1f, 1.2f), cover);

            Physics.SyncTransforms();
            var points = new List<EnemyCoverPoint>();
            foreach (Vector3 normal in new[] { Vector3.forward, Vector3.back })
                foreach (var side in new[] { EnemyHighCoverSide.Left, EnemyHighCoverSide.Right })
                    HighPoint(coverRoot, energy, normal, side, points);
            HighPoint(coverRoot, workshop, Vector3.forward, EnemyHighCoverSide.Right, points);
            HighPoint(coverRoot, workshop, Vector3.back, EnemyHighCoverSide.Left, points);
            foreach (BoxCollider box in new[] { entryCover, eastCover, northCover })
            {
                LowPoint(coverRoot, box.name + " north", box, new Vector3(box.bounds.center.x, 0, box.bounds.max.z + .75f), Vector3.back, points);
                LowPoint(coverRoot, box.name + " south", box, new Vector3(box.bounds.center.x, 0, box.bounds.min.z - .75f), Vector3.forward, points);
            }
            LowPoint(coverRoot, "Gallery south protected", rail, new Vector3(13.95f, 1.5f, 42), Vector3.left, points);
            LowPoint(coverRoot, "Gallery north protected", rail, new Vector3(13.95f, 1.5f, 46), Vector3.left, points);
            var scopeData = new SerializedObject(scope);
            SerializedProperty assigned = scopeData.FindProperty("_coverPoints");
            assigned.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++) assigned.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            scopeData.ApplyModifiedPropertiesWithoutUndo();

            AddRifleman(actors, routes, 1, new Vector3(6, 0, 40), 180,
                new[] { new Vector3(6, 0, 40), new Vector3(9, 0, 35), new Vector3(11, 0, 40) });
            AddRifleman(actors, routes, 2, new Vector3(-8.5f, 0, 43), 180,
                new[] { new Vector3(-8.5f, 0, 43), new Vector3(-8.5f, 0, 37), new Vector3(-8.5f, 0, 48) });
            AddRifleman(actors, routes, 3, new Vector3(7, 0, 51), 180,
                new[] { new Vector3(7, 0, 51), new Vector3(5, 0, 49), new Vector3(9, 0, 52) });
            AddRifleman(actors, routes, 4, new Vector3(15.5f, 1.5f, 45), -90,
                new[] { new Vector3(15.5f, 1.5f, 45), new Vector3(15.5f, 1.5f, 42), new Vector3(15.5f, 1.5f, 47) });
            AddSupportPair(actors, routes);

            Marker(markers, "Entry", Entry);
            Marker(markers, "Exit", Exit);
            Marker(markers, "West route", new Vector3(-8.5f, 0, 40));
            Marker(markers, "East route", new Vector3(11, 0, 40));
            Marker(markers, "Gallery", new Vector3(15.5f, 1.5f, 44));
            Sign(markers, "ENERGY / B", new Vector3(-1, 3.2f, 35.97f), 4);
            Sign(markers, "SERVICE NODE / EXIT", new Vector3(-4, 4, 63.97f), 3);
            Sign(markers, "WORKSHOP", new Vector3(-14.5f, 3.5f, 33.97f), 3);
            Box(markers, "Entry direction stripe", new Vector3(11, .012f, 31), new Vector3(.22f, .02f, 3), marking, false);
            Box(markers, "Exit direction stripe", new Vector3(-4, .012f, 60), new Vector3(.22f, .02f, 3), marking, false);

            string lightingPath = Folder + "/BlockoutLighting.asset";
            if (!AssetDatabase.CopyAsset("Assets/Project/Scenes/EnemyArena/ArenaLighting.asset", lightingPath))
                throw new InvalidOperationException("Could not create scene-local lighting.");
            var volume = Group("06 Neutral HDRP lighting", rootObject.transform).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(lightingPath);
            var surface = navigation.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = .08f;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save new blockout scene.");
            BakeAndValidate();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Open Prototype")]
        public static void Open()
        {
            BattlefieldBaseline.RequireIdleEditor();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Add Support Pair (4 to 6)")]
        public static void AddSupport()
        {
            BattlefieldBaseline.RequireIdleEditor();
            Scene scene = RequireBlockout();
            EnemyActor first = FindInScene<EnemyActor>(scene).First();
            if (AddSupportPair(first.transform.parent, first.Route.transform.parent))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the support pair.");
            }
            Validate();
            Capture();
        }

        private static bool AddSupportPair(Transform actors, Transform routes)
        {
            bool changed = false;
            if (actors.Find("Rifleman B-5") == null)
            {
                AddRifleman(actors, routes, 5, new Vector3(-8.5f, 0, 50), 180,
                    new[] { new Vector3(-8.5f, 0, 50), new Vector3(-9, 0, 54), new Vector3(-11, 0, 50) }, SupportSquad);
                changed = true;
            }
            if (actors.Find("Rifleman B-6") == null)
            {
                AddRifleman(actors, routes, 6, new Vector3(11.3f, 0, 33), 180,
                    new[] { new Vector3(11.3f, 0, 33), new Vector3(11.5f, 0, 38), new Vector3(11.5f, 0, 31) }, SupportSquad);
                changed = true;
            }
            return changed;
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Bake and Validate")]
        public static void BakeAndValidate()
        {
            Scene scene = RequireBlockout();
            NavMeshSurface surface = FindInScene<NavMeshSurface>(scene).Single();
            Physics.SyncTransforms();
            NavMeshData oldData = surface.navMeshData;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Blockout NavMesh bake produced no data.");
            surface.navMeshData.name = "BlockoutNavMesh";
            string navPath = Folder + "/BlockoutNavMesh.asset";
            if (oldData != null && AssetDatabase.GetAssetPath(oldData) == navPath)
            {
                NavMeshData baked = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(baked, oldData);
                surface.navMeshData = oldData;
                Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(oldData);
                surface.AddData();
            }
            else AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            FitLowCoverAnchors(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save baked blockout.");
            Validate();
            Capture();
        }

        private static void FitLowCoverAnchors(Scene scene)
        {
            Directory.CreateDirectory(ReportFolder);
            var changes = new List<string>();
            foreach (EnemyCoverPoint point in FindInScene<EnemyCoverPoint>(scene))
            {
                if (point.Kind != EnemyCoverKind.Low) continue;
                Vector3 original = point.ProtectedPosition;
                if (!NavMesh.SamplePosition(original, out NavMeshHit hit, .8f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - original.y) > .15f)
                    throw new InvalidOperationException(point.name + ": low cover needs a manually authored navigable position.");
                Vector3 fitted = new Vector3(hit.position.x, original.y, hit.position.z);
                if (Vector3.Distance(original, fitted) <= .02f) continue;
                Undo.RecordObject(point.transform, "Fit blockout low cover to baked clearance");
                point.transform.position = fitted;
                changes.Add(point.name + ": " + original + " -> " + fitted);
            }
            File.WriteAllLines(ReportFolder + "/cover-fitting.txt", changes);
        }

        private static Material MaterialAsset(string name, Color color)
        {
            var material = new Material(Shader.Find("HDRP/Lit")) { name = "Blockout " + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .25f);
            AssetDatabase.CreateAsset(material, Folder + "/Materials/" + name + ".mat");
            return material;
        }

        private static Transform Group(string name, Transform parent)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collidable = true, bool walkableTop = false)
        {
            var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            mesh.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.position = center;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            foreach (Collider existing in mesh.GetComponents<Collider>()) Object.DestroyImmediate(existing);
            if (!collidable) return null;
            var box = mesh.gameObject.AddComponent<BoxCollider>();
            box.size = size;
            if (!walkableTop)
            {
                var modifier = mesh.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = 1;
                modifier.applyToChildren = false;
            }
            return box;
        }

        private static void Ramp(Transform parent, string name, Vector3 position, bool reverse, Material material)
        {
            var vertices = new[] { new Vector3(-2.5f, -.2f, -4), new Vector3(2.5f, -.2f, -4), new Vector3(2.5f, -.2f, 4), new Vector3(-2.5f, -.2f, 4),
                new Vector3(-2.5f, 0, -4), new Vector3(2.5f, 0, -4), new Vector3(2.5f, 1.5f, 4), new Vector3(-2.5f, 1.5f, 4) };
            Face Quad(int a, int b, int c, int d) => new Face(new[] { a, b, c, a, c, d });
            var mesh = ProBuilderMesh.Create(vertices, new[] { Quad(0, 1, 2, 3), Quad(4, 7, 6, 5), Quad(0, 4, 5, 1), Quad(1, 5, 6, 2), Quad(2, 6, 7, 3), Quad(3, 7, 4, 0) });
            mesh.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.SetPositionAndRotation(position, Quaternion.Euler(0, reverse ? 180 : 0, 0));
            mesh.ToMesh();
            mesh.Refresh();
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
        }

        private static void HighPoint(Transform parent, BoxCollider box, Vector3 normal, EnemyHighCoverSide side, List<EnemyCoverPoint> points)
        {
            var point = Group(box.name + " high " + normal + " " + side, parent).gameObject.AddComponent<EnemyCoverPoint>();
            point.transform.SetPositionAndRotation(new Vector3(box.bounds.center.x, 0, box.bounds.center.z), Quaternion.LookRotation(normal));
            var data = new SerializedObject(point);
            data.FindProperty("_wallClearance").floatValue = .75f;
            data.FindProperty("_exposureClearance").floatValue = .85f;
            data.ApplyModifiedPropertiesWithoutUndo();
            point.ConfigureHighEdge(box, side);
            points.Add(point);
        }

        private static void LowPoint(Transform parent, string name, BoxCollider box, Vector3 position, Vector3 normal, List<EnemyCoverPoint> points)
        {
            var point = Group(name, parent).gameObject.AddComponent<EnemyCoverPoint>();
            point.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
            point.Configure(EnemyCoverKind.Low, box, null);
            points.Add(point);
        }

        private static void AddRifleman(Transform parent, Transform routes, int index, Vector3 position, float yaw, Vector3[] patrol, string squad = MainSquad)
        {
            var route = Group("Rifleman " + index + " patrol", routes).gameObject.AddComponent<PatrolRoute>();
            var data = new SerializedObject(route);
            var array = data.FindProperty("_points");
            array.arraySize = patrol.Length;
            for (int i = 0; i < patrol.Length; i++)
            {
                var point = Group("Waypoint " + (i + 1), route.transform);
                point.position = patrol[i];
                array.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "Rifleman B-" + index;
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            var actor = instance.GetComponent<EnemyActor>();
            actor.Route = route;
            actor.ConfigureSquad(squad);
            actor.ConfigureEncounter("Blockout B");
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
            Undo.RegisterCreatedObjectUndo(route.gameObject, "Add blockout patrol route");
            Undo.RegisterCreatedObjectUndo(instance, "Add blockout Rifleman");
        }

        private static void Marker(Transform parent, string name, Vector3 position) => Group(name, parent).position = position;

        private static void Sign(Transform parent, string value, Vector3 position, float size)
        {
            var label = Group(value, parent).gameObject.AddComponent<TextMeshPro>();
            label.transform.position = position;
            label.text = value;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.75f, .85f, .86f);
            label.rectTransform.sizeDelta = new Vector2(10, 2);
        }

        internal static T[] FindInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>()).ToArray();

        internal static Scene RequireBlockout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Exit Play Mode and finish compilation/import before editing blockout.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || SceneManager.sceneCount != 1) throw new InvalidOperationException("Open only the separate Blockout B scene for this operation.");
            return scene;
        }
    }
}
