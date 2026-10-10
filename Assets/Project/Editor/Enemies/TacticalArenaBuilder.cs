using System;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;
using TMPro;

namespace Breachpoint.Editor.Enemies
{
    public static class TacticalArenaBuilder
    {
        private const string ScenePath = "Assets/Project/Scenes/EnemyArena/EnemyArena.unity";
        public static void Inspect()
        {
            var report = new StringBuilder(Application.dataPath + "\n" + EnemyValidationRunner.ConsoleCounts());
            report.AppendLine("Playing=" + EditorApplication.isPlaying);
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            Type entryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry");
            object entry = Activator.CreateInstance(entryType);
            var getEntry = entries.GetMethod("GetEntryInternal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            int count = (int)entries.GetMethod("GetCount").Invoke(null, null);
            entries.GetMethod("StartGettingEntries").Invoke(null, null);
            for (int i = 0; i < count; i++)
            {
                getEntry.Invoke(null, new[] { (object)i, entry });
                report.AppendLine("CONSOLE: " + entryType.GetField("message").GetValue(entry));
            }
            entries.GetMethod("EndGettingEntries").Invoke(null, null);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                report.AppendLine(scene.path + " dirty=" + scene.isDirty);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    report.AppendLine(root.name + " position=" + root.transform.position + " active=" + root.activeSelf);
                    foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>().Take(3))
                        report.AppendLine("Mesh " + renderer.name + " enabled=" + renderer.enabled + " bounds=" + renderer.bounds + " material=" + renderer.sharedMaterial?.name);
                }
            }
            Directory.CreateDirectory(EnemyTools.Evidence);
            File.WriteAllText(EnemyTools.Evidence + "/arena-level-inspect.txt", report.ToString());
        }
        private const string RootName = "Tactical Arena - ProBuilder";
        private const string MaterialFolder = "Assets/Project/Materials/EnemyArena";
        private static readonly List<EnemyCoverPoint> Covers = new List<EnemyCoverPoint>();

        [MenuItem("Breachpoint/Level Design/Build Enemy Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before arena authoring.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Open and save EnemyArena before authoring.");
            if (scene.GetRootGameObjects().Any(root => root.name == RootName)) throw new InvalidOperationException("Arena already exists. Edit its ProBuilder meshes directly; rebuilding would overwrite authoring.");
            var scope = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameLifetimeScope>()).Single();
            GameObject player = scene.GetRootGameObjects().Single(root => root.name == "Player");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Enemy/Rifleman.prefab");
            if (prefab == null) throw new InvalidOperationException("Rifleman prefab is missing.");
            Directory.CreateDirectory(EnemyTools.Evidence);
            File.Copy(ScenePath, EnemyTools.Evidence + "/arena-before-level.unity", true);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build tactical Enemy Arena");
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create arena");
            var legacy = new GameObject("Previous test layout (disabled)");
            Undo.RegisterCreatedObjectUndo(legacy, "Preserve test layout");
            foreach (GameObject old in scene.GetRootGameObjects())
            {
                if (old.name == "Arena floor" || old.name == "Sight blocker" || old.name == "Cover" ||
                    old.name == "PatrolRoute" || old.name == "Authored tactical cover" || old.GetComponent<EnemyActor>() != null)
                    Undo.SetTransformParent(old.transform, legacy.transform, "Preserve test object");
            }
            legacy.SetActive(false);
            Transform geometry = Group(root.transform, "01 Architecture");
            Transform detail = Group(root.transform, "02 Markings and trim");
            Transform coverRoot = Group(root.transform, "03 Authored AI cover");
            Transform encounters = Group(root.transform, "04 Enemy squads");
            Covers.Clear();
            Material floor = Material("Floor - graphite", new Color(.22f, .26f, .29f));
            Material concrete = Material("Concrete - warm grey", new Color(.48f, .49f, .46f));
            Material blue = Material("Service blocks - blue", new Color(.13f, .29f, .36f));
            Material dark = Material("Trim - dark metal", new Color(.075f, .10f, .12f));
            Material orange = Material("Safety - amber", new Color(.95f, .39f, .075f));
            Material line = Material("Markings - ivory", new Color(.73f, .77f, .72f));
            Box(geometry, "Courtyard floor 56 x 64", new Vector3(0, -.25f, 0), new Vector3(56, .5f, 64), floor);
            Box(geometry, "West perimeter", new Vector3(-28.5f, 3, 0), new Vector3(1, 6, 65), concrete);
            Box(geometry, "East perimeter", new Vector3(28.5f, 3, 0), new Vector3(1, 6, 65), concrete);
            Box(geometry, "South perimeter", new Vector3(0, 3, -32.5f), new Vector3(56, 6, 1), concrete);
            Box(geometry, "North perimeter", new Vector3(0, 3, 32.5f), new Vector3(56, 6, 1), concrete);
            foreach (float x in new[] { -28f, 28f })
                for (int z = -28; z <= 28; z += 8)
                    Box(detail, "Perimeter pier", new Vector3(x, 3, z), new Vector3(.5f, 6.4f, .6f), dark, false);
            foreach (float x in new[] { -28f, 28f })
                Box(detail, "Wall coping", new Vector3(x, 6.1f, 0), new Vector3(1.2f, .2f, 64), dark, false);
            foreach (float z in new[] { -32f, 32f })
                Box(detail, "Wall coping", new Vector3(0, 6.1f, z), new Vector3(56, .2f, 1.2f), dark, false);

            // Staggered solid islands create interconnected loops and break spawn-to-player sightlines.
            Block(geometry, detail, coverRoot, "A - Pump station", new Vector3(-7, 0, -5), new Vector3(8, 4, 10), blue, orange, dark, true);
            Block(geometry, detail, coverRoot, "B - Power station", new Vector3(7, 0, 10), new Vector3(8, 4, 10), blue, orange, dark, true);
            Vector3[] highCenters = {
                new Vector3(-18,0,-13), new Vector3(15,0,-15),
                new Vector3(-18,0,5), new Vector3(18,0,0),
                new Vector3(-14,0,22), new Vector3(18,0,22)
            };
            for (int i = 0; i < highCenters.Length; i++)
                Block(geometry, detail, coverRoot, "High shield " + (i + 1), highCenters[i], new Vector3(4, 2.7f, 1), concrete, orange, dark, false);
            Vector3[] lowCenters = {
                new Vector3(-7,0,-23), new Vector3(6,0,-20),
                new Vector3(-20,0,-4), new Vector3(7,0,-7),
                new Vector3(-5,0,9), new Vector3(21,0,11),
                new Vector3(-4,0,23), new Vector3(8,0,26)
            };
            for (int i = 0; i < lowCenters.Length; i++)
            {
                BoxCollider protection = Box(geometry, "Low barrier " + (i + 1), lowCenters[i] + Vector3.up * .55f, new Vector3(4, 1.1f, 1.2f), concrete);
                Box(detail, "Low barrier cap", lowCenters[i] + Vector3.up * 1.13f, new Vector3(4.15f, .06f, 1.3f), orange, false);
                foreach (float sign in new[] { -1f, 1f })
                    foreach (float offset in new[] { -1.15f, 1.15f })
                        Cover(coverRoot, protection, EnemyCoverKind.Low, lowCenters[i] + new Vector3(offset, 0, sign * 1.35f), Vector3.forward * -sign, EnemyHighCoverSide.FromExposure);
            }
            // Flush visual marks have no collider and cannot introduce navigation seams.
            foreach (float x in new[] { -24f, 24f })
                Box(detail, "Flank lane boundary", new Vector3(x, .015f, 0), new Vector3(.12f, .02f, 58), orange, false);
            for (int z = -28; z <= 28; z += 4)
                foreach (float x in new[] { -22f, 22f })
                    Box(detail, "Flank lane dash", new Vector3(x, .015f, z), new Vector3(.1f, .02f, 1.5f), line, false);
            for (int z = -24; z <= 24; z += 8)
                Box(detail, "Floor expansion joint", new Vector3(0, .007f, z), new Vector3(55, .008f, .025f), dark, false);
            foreach (float x in new[] { -16f, 0f, 16f })
                Box(detail, "Floor expansion joint", new Vector3(x, .007f, 0), new Vector3(.025f, .008f, 63), dark, false);
            foreach (float z in new[] { -31.96f, 31.96f })
            {
                Box(detail, "Loading bay frame", new Vector3(0, 2.3f, z), new Vector3(8, 4.6f, .12f), dark, false);
                for (int y = 1; y <= 16; y++)
                    Box(detail, "Shutter slat", new Vector3(0, y * .25f, z + (z < 0 ? .08f : -.08f)), new Vector3(7.2f, .035f, .04f), blue, false);
                Box(detail, "Loading bay header", new Vector3(0, 4.7f, z), new Vector3(8.4f, .2f, .2f), orange, false);
            }
            Undo.RecordObject(player.transform, "Position player");
            player.transform.SetPositionAndRotation(new Vector3(0, .15f, -28), Quaternion.identity);
            Vector3[][] patrols = {
                new[] { new Vector3(-16,0,-18), new Vector3(-20,0,-8), new Vector3(-15,0,0), new Vector3(-16,0,-8) },
                new[] { new Vector3(17,0,-10), new Vector3(22,0,3), new Vector3(16,0,13), new Vector3(14,0,0) },
                new[] { new Vector3(-9,0,18), new Vector3(-19,0,26), new Vector3(0,0,29), new Vector3(14,0,28) }
            };
            Vector3[][] placements = {
                new[] { new Vector3(-16,0,-16), new Vector3(-20,0,-10), new Vector3(-13,0,-11), new Vector3(-18,0,-1) },
                new[] { new Vector3(14,0,-10), new Vector3(20,0,-3), new Vector3(22,0,6), new Vector3(14,0,13) },
                new[] { new Vector3(-17,0,18), new Vector3(-10,0,27), new Vector3(2,0,27), new Vector3(20,0,27) }
            };
            for (int squad = 0; squad < 3; squad++)
            {
                Transform squadRoot = Group(encounters, "Squad " + (squad + 1));
                var route = Group(squadRoot, "Patrol loop").gameObject.AddComponent<PatrolRoute>();
                var routeData = new SerializedObject(route);
                SerializedProperty routePoints = routeData.FindProperty("_points");
                routePoints.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    Transform point = Group(route.transform, "Waypoint " + (i + 1));
                    point.position = patrols[squad][i];
                    routePoints.GetArrayElementAtIndex(i).objectReferenceValue = point;
                }
                routeData.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < 4; i++)
                {
                    var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, squadRoot);
                    Undo.RegisterCreatedObjectUndo(enemy, "Place Rifleman");
                    enemy.name = "Rifleman " + (squad + 1) + "-" + (i + 1);
                    enemy.transform.SetPositionAndRotation(placements[squad][i], Quaternion.Euler(0, 180, 0));
                    EnemyActor actor = enemy.GetComponent<EnemyActor>();
                    actor.Route = route;
                    actor.ConfigureSquad("Arena squad " + (squad + 1));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                }
            }
            Undo.RecordObject(scope, "Wire tactical cover");
            var scopeData = new SerializedObject(scope);
            SerializedProperty coverReferences = scopeData.FindProperty("_coverPoints");
            coverReferences.arraySize = Covers.Count;
            for (int i = 0; i < Covers.Count; i++) coverReferences.GetArrayElementAtIndex(i).objectReferenceValue = Covers[i];
            scopeData.ApplyModifiedPropertiesWithoutUndo();
            AddOverlookAndSigns();
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem("Breachpoint/Level Design/Validate and Bake Enemy Arena")]
        public static void Finish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open EnemyArena in Edit Mode first.");
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = scene.GetRootGameObjects().Single(item => item.name == RootName);
            GameObject player = scene.GetRootGameObjects().Single(item => item.name == "Player");
            ConfigureLighting(root.transform);
            Covers.Clear();
            Covers.AddRange(root.GetComponentsInChildren<EnemyCoverPoint>());
            var surface = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<NavMeshSurface>()).Single();
            // Bake architecture only: child ragdoll colliders must never cut holes under enemy starts.
            Undo.SetTransformParent(surface.transform, root.transform.Find("01 Architecture"), "Restrict navigation to arena geometry");
            Transform architecture = root.transform.Find("01 Architecture");
            foreach (Transform item in architecture.Cast<Transform>().ToArray())
                if (item != surface.transform) Undo.SetTransformParent(item, surface.transform, "Collect architectural collider");
            Undo.RecordObject(surface, "Bake arena navigation");
            var navData = new SerializedObject(surface);
            navData.FindProperty("m_CollectObjects").enumValueIndex = (int)CollectObjects.Children;
            navData.FindProperty("m_UseGeometry").enumValueIndex = (int)NavMeshCollectGeometry.PhysicsColliders;
            navData.FindProperty("m_OverrideVoxelSize").boolValue = true;
            navData.FindProperty("m_VoxelSize").floatValue = .08f;
            navData.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            Validate(scene, player, root);
            NavMeshData baked = surface.navMeshData;
            NavMeshData persistent = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Project/Scenes/EnemyArena/ArenaNavMesh.asset");
            if (persistent == null) throw new InvalidOperationException("Existing arena navigation asset is missing.");
            EditorUtility.CopySerialized(baked, persistent);
            persistent.name = "ArenaNavMesh";
            surface.RemoveData();
            surface.navMeshData = persistent;
            surface.AddData();
            EditorUtility.SetDirty(persistent);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Arena save failed.");
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 0), Quaternion.Euler(60, 0, 0), 52);
            Capture(scene, new Vector3(38, 50, -44), new Vector3(0, 0, 0), "arena-overview");
            Capture(scene, new Vector3(0, 1.7f, -27), new Vector3(0, 1.7f, 8), "arena-player-view");
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Breachpoint/Level Design/Add Enemy Arena Overlook and Signs")]
        public static void AddOverlookAndSigns()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath) throw new InvalidOperationException("Open EnemyArena in Edit Mode.");
            Transform root = scene.GetRootGameObjects().Single(item => item.name == RootName).transform;
            Transform geometry = root.Find("01 Architecture/Navigation");
            if (geometry == null) geometry = root.Find("01 Architecture");
            Transform detail = root.Find("02 Markings and trim");
            if (geometry.Find("West overlook 1.5m") == null)
            {
                Material floor = Material("Floor - graphite", new Color(.22f, .26f, .29f));
                Material concrete = Material("Concrete - warm grey", new Color(.48f, .49f, .46f));
                Material orange = Material("Safety - amber", new Color(.95f, .39f, .075f));
                Box(geometry, "West overlook 1.5m", new Vector3(-23, .75f, 20), new Vector3(6, 1.5f, 6), floor);
                Ramp(geometry, "Overlook south ramp", new Vector3(-23, 0, 13), false, floor);
                Ramp(geometry, "Overlook north ramp", new Vector3(-23, 0, 27), true, floor);
                foreach (float sign in new[] { -1f, 1f })
                {
                    BoxCollider rail = Box(geometry, "Overlook parapet", new Vector3(-23 + sign * 3, 2.05f, 20), new Vector3(.2f, 1.1f, 6), concrete);
                    Box(detail, "Overlook safety cap", new Vector3(-23 + sign * 3, 2.62f, 20), new Vector3(.3f, .04f, 6), orange, false);
                    foreach (float z in new[] { 18.5f, 21.5f })
                        Cover(root.Find("03 Authored AI cover"), rail, EnemyCoverKind.Low, new Vector3(-23 + sign * 2.1f, 1.5f, z), Vector3.right * sign, EnemyHighCoverSide.FromExposure);
                }
                Sign(detail, "01 / PUMP", new Vector3(-7, 2.8f, -10.08f), 0, 1.0f);
                Sign(detail, "02 / POWER", new Vector3(7, 2.8f, 4.92f), 0, 1.0f);
                Sign(detail, "BREACHPOINT\nTRAINING YARD", new Vector3(0, 5.5f, 31.9f), 0, 1.0f);
                Sign(detail, "WEST / OVERLOOK", new Vector3(-27.95f, 4, 20), -90, .8f);
                var scope = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<GameLifetimeScope>()).Single();
                var data = new SerializedObject(scope);
                EnemyCoverPoint[] points = root.GetComponentsInChildren<EnemyCoverPoint>();
                SerializedProperty references = data.FindProperty("_coverPoints");
                references.arraySize = points.Length;
                for (int i = 0; i < points.Length; i++) references.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (TextMeshPro label in detail.GetComponentsInChildren<TextMeshPro>())
                if (label.fontSize < 2) { Undo.RecordObject(label, "Size arena sign"); label.fontSize *= 10; }
            Transform overlookSign = detail.Find("WEST / OVERLOOK");
            if (overlookSign != null) overlookSign.rotation = Quaternion.Euler(0, -90, 0);
            Finish();
        }

        public static void ReloadAndValidate()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath) throw new InvalidOperationException("Open EnemyArena in Edit Mode.");
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Arena save failed.");
            scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject root = scene.GetRootGameObjects().Single(item => item.name == RootName);
            foreach (Transform item in root.GetComponentsInChildren<Transform>())
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                    throw new InvalidOperationException("Missing script: " + item.name);
            foreach (ProBuilderMesh mesh in root.GetComponentsInChildren<ProBuilderMesh>())
                if (mesh.GetComponent<MeshFilter>().sharedMesh == null || mesh.GetComponent<MeshFilter>().sharedMesh.vertexCount == 0)
                    throw new InvalidOperationException("Empty ProBuilder mesh after reload: " + mesh.name);
            Covers.Clear(); Covers.AddRange(root.GetComponentsInChildren<EnemyCoverPoint>());
            Validate(scene, scene.GetRootGameObjects().Single(item => item.name == "Player"), root);
            File.AppendAllText(EnemyTools.Evidence + "/arena-level-validation.txt", "PASS: saved scene reload, ProBuilder mesh persistence, no missing arena scripts.\n");
        }

        private static void Ramp(Transform parent, string name, Vector3 position, bool reverse, Material material)
        {
            Vector3[] vertices = {
                new Vector3(-3,-.15f,-4), new Vector3(3,-.15f,-4), new Vector3(3,-.15f,4), new Vector3(-3,-.15f,4),
                new Vector3(-3,0,-4), new Vector3(3,0,-4), new Vector3(3,1.5f,4), new Vector3(-3,1.5f,4)
            };
            int[][] triangles = { new[] {0,1,2,0,2,3}, new[] {4,7,6,4,6,5}, new[] {0,4,5,0,5,1}, new[] {3,2,6,3,6,7}, new[] {0,3,7,0,7,4}, new[] {1,5,6,1,6,2} };
            ProBuilderMesh mesh = ProBuilderMesh.Create(vertices, triangles.Select(indices => new Face(indices)));
            mesh.name = name;
            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create ProBuilder ramp");
            mesh.transform.SetParent(parent, false);
            mesh.transform.SetPositionAndRotation(position, Quaternion.Euler(0, reverse ? 180 : 0, 0));
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            mesh.ToMesh(); mesh.Refresh();
            var collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
        }

        private static void Sign(Transform parent, string text, Vector3 position, float yaw, float size)
        {
            Transform sign = Group(parent, text.Replace('\n', ' '));
            sign.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var label = sign.gameObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size * 10;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.9f, .93f, .9f);
            label.rectTransform.sizeDelta = new Vector2(12, 2);
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, "Create arena group");
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void ConfigureLighting(Transform parent)
        {
            const string profilePath = "Assets/Project/Scenes/EnemyArena/ArenaLighting.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                Exposure exposure = profile.Add<Exposure>(true);
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(11f);
                VisualEnvironment environment = profile.Add<VisualEnvironment>(true);
                environment.skyType.Override(3);
                GradientSky sky = profile.Add<GradientSky>(true);
                sky.top.Override(new Color(.25f, .39f, .58f));
                sky.middle.Override(new Color(.63f, .70f, .75f));
                sky.bottom.Override(new Color(.25f, .27f, .29f));
                foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
            }
            Transform lighting = parent.Find("05 Lighting");
            if (lighting == null) lighting = Group(parent, "05 Lighting");
            Volume volume = lighting.GetComponent<Volume>();
            if (volume == null) volume = lighting.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            profile.TryGet(out GradientSky gradient);
            gradient.exposure.Override(11f);
            EditorUtility.SetDirty(gradient);
            var sun = SceneManager.GetActiveScene().GetRootGameObjects().Single(item => item.name == "Sun").GetComponent<Light>();
            Undo.RecordObject(sun, "Light arena");
            Undo.RecordObject(sun.transform, "Set afternoon light direction");
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = 24000;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
        }

        private static Material Material(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Project/Materials", "EnemyArena");
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("HDRP/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .22f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool solid = true)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            mesh.name = name;
            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create ProBuilder geometry");
            mesh.transform.SetParent(parent, false);
            mesh.transform.position = center;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            mesh.ToMesh();
            mesh.Refresh();
            foreach (Collider collider in mesh.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            if (!solid) return null;
            var box = mesh.gameObject.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        private static void Block(Transform geometry, Transform detail, Transform points, string name, Vector3 floorPosition, Vector3 size, Material body, Material accent, Material trim, bool island)
        {
            BoxCollider box = Box(geometry, name, floorPosition + Vector3.up * size.y * .5f, size, body);
            Box(detail, name + " coping", floorPosition + Vector3.up * (size.y + .05f), new Vector3(size.x + .2f, .1f, size.z + .2f), trim, false);
            foreach (float sign in new[] { -1f, 1f })
            {
                Box(detail, name + " safety band", floorPosition + new Vector3(0, size.y - .35f, sign * (size.z * .5f + .015f)), new Vector3(size.x, .15f, .025f), accent, false);
                foreach (EnemyHighCoverSide side in new[] { EnemyHighCoverSide.Left, EnemyHighCoverSide.Right })
                    Cover(points, box, EnemyCoverKind.High, floorPosition, Vector3.forward * sign, side);
                if (!island) continue;
                foreach (EnemyHighCoverSide side in new[] { EnemyHighCoverSide.Left, EnemyHighCoverSide.Right })
                    Cover(points, box, EnemyCoverKind.High, floorPosition, Vector3.right * sign, side);
                for (int i = -2; i <= 2; i++)
                    Box(detail, name + " service louver", floorPosition + new Vector3(i * .65f, 2, sign * (size.z * .5f + .025f)), new Vector3(.35f, 1.2f, .04f), trim, false);
            }
        }

        private static void Cover(Transform parent, BoxCollider protection, EnemyCoverKind kind, Vector3 position, Vector3 normal, EnemyHighCoverSide side)
        {
            Transform anchor = Group(parent, protection.name + " " + kind + " " + normal + " " + side + " " + Covers.Count);
            anchor.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
            var point = anchor.gameObject.AddComponent<EnemyCoverPoint>();
            point.Configure(kind, protection, null);
            if (kind == EnemyCoverKind.High)
            {
                var data = new SerializedObject(point);
                data.FindProperty("_wallClearance").floatValue = .75f;
                data.FindProperty("_exposureClearance").floatValue = .85f;
                data.ApplyModifiedPropertiesWithoutUndo();
                point.ConfigureHighEdge(protection, side);
            }
            Covers.Add(point);
        }

        private static void Validate(Scene scene, GameObject player, GameObject root)
        {
            var report = new StringBuilder("Tactical Arena: 56 x 64m; 12 Riflemen; 3 squads; " + Covers.Count + " cover positions.\n");
            Vector3 origin = player.transform.position;
            if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 1, NavMesh.AllAreas)) throw new InvalidOperationException("Player start is off NavMesh.");
            var path = new NavMeshPath();
            int checkedPositions = 0;
            foreach (EnemyCoverPoint point in Covers)
            {
                if (!point.IsUsable) throw new InvalidOperationException("Unusable cover: " + point.name);
                float protectedHeight = point.Kind == EnemyCoverKind.Low ? .7f : 1.7f;
                if (!point.Protection.Raycast(new Ray(point.ProtectedPosition + Vector3.up * protectedHeight, point.Normal), out _, 12))
                    throw new InvalidOperationException("Cover does not protect its authored face: " + point.name);
                if (point.Kind == EnemyCoverKind.High && point.Protection.Raycast(new Ray(point.ExposurePosition + Vector3.up * 1.4f, point.Normal), out _, 12))
                    throw new InvalidOperationException("High-cover exposure is blocked by its wall: " + point.name);
                foreach (Vector3 position in new[] { point.ProtectedPosition, point.ExposurePosition })
                {
                    CheckPath(start.position, position, point.name, path);
                    checkedPositions++;
                }
            }
            foreach (EnemyActor actor in root.GetComponentsInChildren<EnemyActor>())
                CheckPath(start.position, actor.transform.position, actor.name, path);
            foreach (PatrolRoute route in root.GetComponentsInChildren<PatrolRoute>())
                for (int i = 0; i < route.Count; i++)
                    if (route.TryGetPoint(i, out Vector3 position)) CheckPath(start.position, position, route.name, path);
            report.AppendLine("PASS: all protected and exposed cover positions (" + checkedPositions + "), 12 enemy starts and 12 patrol waypoints reachable from player start.");
            report.AppendLine("ProBuilder meshes: " + root.GetComponentsInChildren<ProBuilderMesh>().Length);
            report.AppendLine("PASS: physical cover protection and high-cover firing exposure.");
            report.AppendLine(EnemyValidationRunner.ConsoleCounts());
            File.WriteAllText(EnemyTools.Evidence + "/arena-level-validation.txt", report.ToString());
        }

        private static void CheckPath(Vector3 origin, Vector3 goal, string context, NavMeshPath path)
        {
            if (!NavMesh.SamplePosition(goal, out NavMeshHit hit, .2f, NavMesh.AllAreas) || Vector3.Distance(hit.position, goal) > .2f ||
                !NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Unreachable arena position: " + context + " at " + goal);
        }

        private static void Capture(Scene scene, Vector3 position, Vector3 target, string name)
        {
            var cameraObject = new GameObject("Temporary arena preview");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<HDAdditionalCameraData>();
            camera.enabled = false;
            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.fieldOfView = name == "arena-overview" ? 55 : 65;
            var texture = new RenderTexture(1600, 1000, 24);
            texture.Create();
            var request = new RenderPipeline.StandardRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            image.Apply();
            File.WriteAllBytes(EnemyTools.Evidence + "/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
