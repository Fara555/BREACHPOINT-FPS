using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Player.Input;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldMarketRoom
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom";
        private const string Art = "Assets/Project/Art/Environment/ServiceQuarter";
        private const string Output = "Logs/BattlefieldMarketRoom";
        private const string RootName = "14 Art - Walk-through market room";
        private const string CollisionName = "Market room collision shell";
        private const string NavPath = Folder + "/MarketRoomNavMesh.asset";

        // The authorized spatial revision has a versioned before/after fingerprint. Historical art baselines stay immutable.
        internal static bool MatchesGameplay(Scene scene, string historicalPath)
        {
            string historical = File.ReadAllText(historicalPath);
            if (MarketServiceDoorAuthoring.MatchesGameplay(scene, historical)) return true;
            return File.Exists(Folder + "/GameplayAfter.sha256.txt") && File.Exists(Folder + "/GameplayBefore.sha256.txt") &&
                scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Any(t => t.name == RootName) &&
                historical == File.ReadAllText(Folder + "/GameplayBefore.sha256.txt") && MarketServiceDoorAuthoring.MatchesGameplay(scene, File.ReadAllText(Folder + "/GameplayAfter.sha256.txt"));
        }

        internal static string ExpectedNavigationPath(string original) => File.Exists(Folder + "/GameplayAfter.sha256.txt") ? NavPath : original;

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Create Walk Through Market Room")]
        public static void Create()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            if (FindOrNull(RootName) != null) { Validate(); Capture(); return; }
            BattlefieldQuietFloor.Validate();
            Directory.CreateDirectory(Output);
            File.Copy(scene.path, Output + "/scene-before-market.unity", true);
            WriteBaseline("GameplayBefore.sha256.txt", BattlefieldCornerArt.GameplayFingerprint(scene));
            WriteBaseline("ProtectedAssetsBefore.sha256.txt", ProtectedAssets());
            WriteBaseline("ActorsBefore.sha256.txt", Actors());
            WriteBaseline("OtherCollisionBefore.sha256.txt", OtherCollision());
            var surface = BattlefieldBlockout.FindInScene<NavMeshSurface>(scene).Single();
            File.WriteAllText(Output + "/navigation-before.txt", AssetDatabase.GetAssetPath(surface.navMeshData));
            foreach (string name in new[] { "InteriorWithoutWorkshop", "MarketRoom" }) Configure(name);
            var root = new GameObject(RootName);
            root.transform.SetParent(Find("Blockout B - Service Quarter"), false);
            Undo.RegisterCreatedObjectUndo(root, "Create through market room");
            foreach (string name in new[] { "InteriorWithoutWorkshop", "MarketRoom" })
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/" + name + ".fbx"), root.transform);
                instance.name = name;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
            foreach (var renderer in Find("12 Art - Interior walls ceiling and equipment").GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<TMP_Text>() == null))
            {
                Undo.RecordObject(renderer, "Archive interior skin containing the closed workshop"); renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var old = Find("West workshop");
            Undo.RecordObject(old.GetComponent<Collider>(), "Open the former solid workshop volume");
            old.GetComponent<Collider>().enabled = false;
            Undo.RecordObject(old.GetComponent<MeshRenderer>(), "Hide archived solid workshop backing");
            old.GetComponent<MeshRenderer>().enabled = false;
            foreach (var sign in BattlefieldBlockout.FindInScene<TextMeshPro>(scene).Where(t => t.text == "WORKSHOP"))
            { Undo.RecordObject(sign.gameObject, "Replace outdated workshop review label"); sign.gameObject.SetActive(false); }
            var collision = new GameObject(CollisionName).transform;
            collision.SetParent(surface.transform, false); Undo.RegisterCreatedObjectUndo(collision.gameObject, "Create market physical shell");
            Box(collision, "Market west wall", new Vector3(-17.7f, 2.5f, 40.5f), new Vector3(.6f, 5, 13));
            BoxCollider southReturn = null, northReturn = null;
            foreach (float z in new[] { 34.14f, 46.86f })
            {
                Box(collision, "Market end west return " + z, new Vector3(-16.9f, 2.5f, z), new Vector3(2.2f, 5, .28f));
                var wall = Box(collision, "Market end east return " + z, new Vector3(-12, 2.5f, z), new Vector3(2, 5, .28f));
                if (z < 40) southReturn = wall; else northReturn = wall;
                Box(collision, "Market end lintel " + z, new Vector3(-14.4f, 4.1f, z), new Vector3(2.8f, 1.8f, .28f));
            }
            foreach (float z in new[] { 36.5f, 44.5f }) Box(collision, "Market east wall " + z, new Vector3(-11.14f, 2.5f, z), new Vector3(.28f, 5, 5));
            Box(collision, "Market east lintel", new Vector3(-11.14f, 4.1f, 40.5f), new Vector3(.28f, 1.8f, 3));
            Box(collision, "Market window breast wall", new Vector3(-11.14f, .575f, 40.5f), new Vector3(.28f, 1.15f, 3));
            Box(collision, "Market window serving sill", new Vector3(-11.10f, 1.20f, 40.5f), new Vector3(.93f, .14f, 3.42f));
            Box(collision, "Market ceiling", new Vector3(-14.5f, 4.98f, 40.5f), new Vector3(7, .12f, 13));
            foreach (float z in new[] { 36.8f, 44.2f }) Box(collision, "Market counter " + z, new Vector3(-12, .55f, z), new Vector3(1.2f, 1.1f, 2.2f));
            foreach (float z in new[] { 36.6f, 40.5f, 44.4f }) Box(collision, "Market storage " + z, new Vector3(-17.15f, 1.18f, z), new Vector3(.9f, 2.36f, 2.3f));
            var coverReport = new StringBuilder();
            foreach (var point in BattlefieldBlockout.FindInScene<EnemyCoverPoint>(scene).Where(p => p.Protection == old.GetComponent<Collider>()))
            {
                Vector3 protectedBefore = point.ProtectedPosition, exposureBefore = point.ExposurePosition;
                bool right = point.ExposesRight;
                var wall = point.Normal.z > 0 ? southReturn : northReturn;
                Undo.RecordObject(point, "Retain corner cover against the new room wall"); Undo.RecordObject(point.transform, "Retain cover anchor position");
                point.ConfigureHighEdge(wall, right ? EnemyHighCoverSide.Right : EnemyHighCoverSide.Left);
                var data = new SerializedObject(point);
                data.FindProperty("_exposureNormalOffset").floatValue = Vector3.Dot(exposureBefore - wall.bounds.center, point.Normal);
                data.ApplyModifiedPropertiesWithoutUndo();
                if (Vector3.Distance(protectedBefore, point.ProtectedPosition) > .03f || Vector3.Distance(exposureBefore, point.ExposurePosition) > .03f)
                    throw new InvalidOperationException("Market conversion displaced an existing cover anchor: " + point.name);
                coverReport.AppendLine(point.name + " => " + wall.name + " | protected/exposure positions retained");
            }
            File.WriteAllText(Output + "/retained-cover.txt", coverReport.ToString());
            foreach (float z in new[] { 37.6f, 43.4f })
            {
                var lamp = new GameObject("Market interior fixture " + z); lamp.transform.SetParent(root.transform, false);
                lamp.transform.SetPositionAndRotation(new Vector3(-14.6f, 4.55f, z), Quaternion.Euler(90, 0, 0));
                var light = lamp.AddComponent<Light>();
                if (lamp.GetComponent<HDAdditionalLightData>() == null) lamp.AddComponent<HDAdditionalLightData>();
                light.type = LightType.Spot; light.spotAngle = 110; light.innerSpotAngle = 90; light.range = 8;
                light.lightUnit = UnityEngine.Rendering.LightUnit.Candela; light.intensity = 4000; light.color = new Color(1, .94f, .83f); light.shadows = LightShadows.None;
            }
            var label = new GameObject("Market attached header").AddComponent<TextMeshPro>();
            label.transform.SetParent(root.transform, false); label.transform.SetPositionAndRotation(new Vector3(-10.84f, 3.76f, 40.5f), Quaternion.Euler(0, -90, 0));
            label.text = "MARKET"; label.font = Font(); label.fontSize = 4.4f; label.characterSpacing = 8;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(.86f, .8f, .62f); label.rectTransform.sizeDelta = new Vector2(2.8f, .45f);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Folder + "/PF_MarketRoom.prefab", InteractionMode.AutomatedAction);
            Physics.SyncTransforms();
            Undo.RecordObject(surface, "Rebuild navigation for accessible market");
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Market navigation bake produced no data.");
            surface.navMeshData.name = "MarketRoomNavMesh";
            if (File.Exists(NavPath)) throw new InvalidOperationException("Inspect the existing market navigation asset before replacing it.");
            AssetDatabase.CreateAsset(surface.navMeshData, NavPath); EditorUtility.SetDirty(surface);
            CompleteRevision();
        }

        internal static void FitWestLining()
        {
            RequireIdle();
            var wall = Find("Market west wall").GetComponent<BoxCollider>();
            Undo.RecordObject(wall, "Fit market lining collision");
            Undo.RecordObject(wall.transform, "Fit market lining position");
            wall.transform.position = new Vector3(-17.7f, 2.5f, 40.5f);
            wall.size = new Vector3(.6f, 5, 13);
            Physics.SyncTransforms();
            var surface = BattlefieldBlockout.FindInScene<NavMeshSurface>(BattlefieldBlockout.RequireBlockout()).Single();
            var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            surface.BuildNavMesh();
            var generated = surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(generated, stored);
            stored.name = "MarketRoomNavMesh";
            surface.navMeshData = stored;
            surface.AddData();
            EditorUtility.SetDirty(stored);
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssetIfDirty(stored);
            Object.DestroyImmediate(generated);
            CompleteRevision();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Market Window And Detail Pass One")]
        public static void RefineWindow()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            bool alreadyWindow = FindOrNull("Market window breast wall") != null;
            if (!alreadyWindow)
            {
                Validate();
                Directory.CreateDirectory(Output + "/BeforeWindow");
                File.Copy(scene.path, Output + "/BeforeWindow/scene.unity", true);
                if (!File.Exists(Folder + "/GameplayThroughRoom.sha256.txt"))
                    WriteBaseline("GameplayThroughRoom.sha256.txt", BattlefieldCornerArt.GameplayFingerprint(scene));
            }
            Configure("MarketRoom");
            if (!alreadyWindow)
            {
                var shell = Find(CollisionName);
                Box(shell, "Market window breast wall", new Vector3(-11.14f, .575f, 40.5f), new Vector3(.28f, 1.15f, 3));
                Box(shell, "Market window serving sill", new Vector3(-11.10f, 1.20f, 40.5f), new Vector3(.93f, .14f, 3.42f));
                Physics.SyncTransforms();
                var surface = BattlefieldBlockout.FindInScene<NavMeshSurface>(scene).Single();
                var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
                surface.BuildNavMesh();
                var generated = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(generated, stored);
                stored.name = "MarketRoomNavMesh";
                surface.navMeshData = stored;
                surface.AddData();
                EditorUtility.SetDirty(stored);
                EditorUtility.SetDirty(surface);
                AssetDatabase.SaveAssetIfDirty(stored);
                Object.DestroyImmediate(generated);
            }
            CompleteRevision();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Complete Market Layout Revision")]
        public static void CompleteRevision()
        {
            RequireIdle(); ValidateCore();
            var scene = BattlefieldBlockout.RequireBlockout();
            WriteBaseline("GameplayAfter.sha256.txt", BattlefieldCornerArt.GameplayFingerprint(scene));
            BattlefieldBlockout.Validate(); BattlefieldQuietFloor.Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Market scene could not be saved.");
            Capture();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Market Room")]
        public static void Validate()
        {
            RequireIdle(); ValidateCore();
            if (!MarketServiceDoorAuthoring.MatchesGameplay(BattlefieldBlockout.RequireBlockout(), File.ReadAllText(Folder + "/GameplayAfter.sha256.txt")))
                throw new InvalidOperationException("Market layout differs from its validated layout revision.");
            BattlefieldBlockout.Validate(); BattlefieldQuietFloor.Validate();
        }

        private static void ValidateCore()
        {
            var scene = BattlefieldBlockout.RequireBlockout(); var root = Find(RootName);
            if (ProtectedAssets() != File.ReadAllText(Folder + "/ProtectedAssetsBefore.sha256.txt")) throw new InvalidOperationException("Accepted environment or floor assets changed.");
            if (Actors() != File.ReadAllText(Folder + "/ActorsBefore.sha256.txt") || OtherCollision() != File.ReadAllText(Folder + "/OtherCollisionBefore.sha256.txt"))
                throw new InvalidOperationException("Unrelated actors, routes or collision changed.");
            if (Find("West workshop").GetComponent<Collider>().enabled || Find("West workshop").GetComponent<MeshRenderer>().enabled)
                throw new InvalidOperationException("The archived solid workshop still blocks the new room.");
            if (Find("12 Art - Interior walls ceiling and equipment").GetComponentsInChildren<MeshRenderer>(true).Any(r => r.GetComponent<TMP_Text>() == null && r.enabled))
                throw new InvalidOperationException("The original closed workshop skin still renders.");
            if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == null))
                throw new InvalidOperationException("Market visual model references or collision ownership failed.");
            var surface = BattlefieldBlockout.FindInScene<NavMeshSurface>(scene).Single();
            if (AssetDatabase.GetAssetPath(surface.navMeshData) != NavPath || surface.useGeometry != NavMeshCollectGeometry.PhysicsColliders)
                throw new InvalidOperationException("Market must use its own persisted PhysicsColliders navigation bake.");
            var report = new StringBuilder("PASS accepted art/floor files, actors, patrols and other colliders unchanged.\n");
            Physics.SyncTransforms();
            var capsule = BattlefieldBlockout.FindInScene<PlayerInputReader>(scene).Single().GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = BattlefieldBlockout.FindInScene<PlayerInputReader>(scene).Single().GetComponentInChildren<CapsuleCollider>();
            if (capsule == null) throw new InvalidOperationException("Inspect the player capsule before verifying market passage.");
            float radius = capsule.radius * Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
            float height = Mathf.Max(capsule.height * capsule.transform.lossyScale.y, radius * 2);
            var through = new[] { new Vector3(-14.4f, 0, 31.8f), new Vector3(-14.4f, 0, 35.5f), new Vector3(-14.4f, 0, 40.5f), new Vector3(-14.4f, 0, 45.5f), new Vector3(-14.4f, 0, 49) };
            var side = new[] { new Vector3(-8.5f, 0, 40.5f), new Vector3(-14.4f, 0, 40.5f) };
            bool servingWindow = FindOrNull("Market window breast wall") != null;
            foreach (var route in servingWindow ? new[] { through } : new[] { through, side })
                for (int segment = 1; segment < route.Length; segment++)
                {
                    if (!BattlefieldBlockout.Path(route[segment - 1], route[segment], out var length)) throw new InvalidOperationException("Market passage has no complete NavMesh path.");
                    int samples = Mathf.CeilToInt(Vector3.Distance(route[segment - 1], route[segment]) / .2f);
                    for (int i = 0; i <= samples; i++)
                    {
                        Vector3 p = Vector3.Lerp(route[segment - 1], route[segment], i / (float)samples);
                        if (Physics.OverlapCapsule(p + Vector3.up * (radius + .04f), p + Vector3.up * (height - radius + .04f), radius, ~0, QueryTriggerInteraction.Ignore)
                            .Any(c => c.transform.IsChildOf(surface.transform))) throw new InvalidOperationException("Player capsule is blocked in the market at " + p);
                    }
                    report.AppendLine("PASS complete path and player capsule clearance: " + route[segment - 1] + " -> " + route[segment] + "; length=" + length.ToString("F2"));
                }
            if (servingWindow)
            {
                var breast = Find("Market window breast wall").GetComponent<BoxCollider>();
                var sill = Find("Market window serving sill").GetComponent<BoxCollider>();
                if (!breast.enabled || !sill.enabled || breast.bounds.max.y < 1.14f || sill.bounds.max.y < 1.26f)
                    throw new InvalidOperationException("Serving window has no physical breast wall/sill.");
                if (!Physics.RaycastAll(new Vector3(-9.5f, .65f, 40.5f), Vector3.left, 3, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider == breast))
                    throw new InvalidOperationException("The former side entrance is not physically closed below the window.");
                if (Physics.RaycastAll(new Vector3(-9.5f, 1.8f, 40.5f), Vector3.left, 3, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider.transform.IsChildOf(surface.transform)))
                    throw new InvalidOperationException("Serving window aperture is obstructed at standing eye height.");
                if (!NavMesh.SamplePosition(side[0], out var from, .6f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(side[1], out var to, .6f, NavMesh.AllAreas) ||
                    !NavMesh.Raycast(from.position, to.position, out _, NavMesh.AllAreas))
                    throw new InvalidOperationException("Navigation still treats the serving window as a doorway.");
                report.AppendLine("PASS solid lower serving wall and sill, open eye-height aperture, no direct NavMesh passage through window.");
            }
            int triangles = root.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null).Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 500000 || root.GetComponentsInChildren<Light>().Length != 2) throw new InvalidOperationException("Market visual/light budget failed.");
            report.AppendLine("Market plus retained interior triangles=" + triangles + "; two unshadowed local fixtures; " +
                (servingWindow ? "two end doorways and an open serving window." : "three open doorways, 3.2m clear height."));
            report.AppendLine(EnemyValidationRunner.ConsoleCounts()); Directory.CreateDirectory(Output); File.WriteAllText(Output + "/market-validation.txt", report.ToString());
        }

        private static BoxCollider Box(Transform parent, string name, Vector3 p, Vector3 size)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.position = p; obj.isStatic = true;
            Undo.RegisterCreatedObjectUndo(obj, "Create market shell section");
            var collider = obj.AddComponent<BoxCollider>(); collider.size = size;
            var modifier = obj.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1; modifier.applyToChildren = false;
            return collider;
        }

        private static void Configure(string name)
        {
            string path = Folder + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Build the market Blender models first.");
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    var target = new[] { "/MarketRoom/Materials/", "/InteriorFinish/Materials/", "/QuietFloor/Materials/", "/CornerSample/Materials/" }
                        .Select(p => AssetDatabase.LoadAssetAtPath<Material>(Art + p + material.name + ".mat")).FirstOrDefault(m => m != null);
                    if (target == null) throw new InvalidOperationException("Unknown market material " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
        }

        internal static void RefreshSurfaces()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            string before = BattlefieldCornerArt.GameplayFingerprint(scene);
            if (!MarketServiceDoorAuthoring.MatchesGameplay(scene, File.ReadAllText(Folder + "/GameplayAfter.sha256.txt")))
                throw new InvalidOperationException("Inspect unsaved gameplay changes before the visual surface pass.");
            Configure("MarketRoom");
            if (ProtectedAssets() != File.ReadAllText(Folder + "/ProtectedAssetsBefore.sha256.txt") ||
                before != BattlefieldCornerArt.GameplayFingerprint(scene))
                throw new InvalidOperationException("Market surface pass changed protected assets or gameplay geometry.");
            SealedSupplyPackAuthoring.ApplyLegacyRemoval();
            MarketWallLiningAuthoring.ApplyLegacyReplacement();
            MarketServiceDoorAuthoring.ApplyRackRemoval();
            var renderers = Find(RootName).GetComponentsInChildren<MeshRenderer>();
            foreach (string name in new[] { "MarketFrame", "MarketWorktop", "MarketWarm" })
                if (!renderers.Any(r => r.sharedMaterials.Any(m => m != null && m.name == name)))
                    throw new InvalidOperationException("Missing market finish assignment: " + name);
            if (renderers.Any(r => r.sharedMaterials.Any(m => m == null)))
                throw new InvalidOperationException("Missing market surface material reference.");
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Market surface scene could not be saved.");
            Capture();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Market Interior Fittings Pass Three")]
        public static void RefreshFittings()
        {
            RequireIdle();
            const string statusPath = Folder + "/Materials/MarketStatus.mat";
            var status = AssetDatabase.LoadAssetAtPath<Material>(statusPath);
            if (status == null)
            {
                status = new Material(AssetDatabase.LoadAssetAtPath<Material>(Art + "/CornerSample/Materials/CyanIndicator.mat")) { name = "MarketStatus" };
                AssetDatabase.CreateAsset(status, statusPath);
            }
            status.SetColor("_BaseColor", new Color(.06f, .25f, .28f));
            status.SetColor("_EmissiveColor", new Color(.02f, .30f, .35f));
            status.SetFloat("_UseEmissiveIntensity", 0);
            HDMaterial.ValidateMaterial(status);
            EditorUtility.SetDirty(status);
            AssetDatabase.SaveAssetIfDirty(status);
            RefreshSurfaces();
            var root = Find(RootName);
            int triangles = root.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null).Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 500000 || root.GetComponentsInChildren<Light>().Length != 2 || root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Market fittings exceeded the existing visual/light/collision budget.");
            File.WriteAllText(Output + "/fittings-validation.txt",
                "PASS fittings remapped, valid material references, accepted art/floor and exact gameplay/nav fingerprint unchanged.\n" +
                "PASS no added visual-root colliders; two existing lights retained; triangles=" + triangles + "\n" +
                "Blender fitting bounds and central-aisle audit: SourceArt/Environment/MarketRoom/MarketRoomReport.json\n" +
                EnemyValidationRunner.ConsoleCounts());
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Market Envelope Pass Four")]
        public static void RefreshEnvelope()
        {
            RequireIdle();
            const string path = Folder + "/Materials/MarketLining.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Art + "/InteriorFinish/Materials/InteriorNeutral.mat")) { name = "MarketLining" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_SmoothnessRemapMin", .12f);
            material.SetFloat("_SmoothnessRemapMax", .42f);
            material.SetFloat("_NormalScale", .28f);
            material.SetFloat("_CoatMask", .035f);
            material.SetFloat("_EnableGeometricSpecularAA", 1);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            const string ceilingPath = Folder + "/Materials/MarketCeiling.mat";
            var ceiling = AssetDatabase.LoadAssetAtPath<Material>(ceilingPath);
            if (ceiling == null)
            {
                ceiling = new Material(AssetDatabase.LoadAssetAtPath<Material>(Art + "/InteriorFinish/Materials/InteriorCeiling.mat")) { name = "MarketCeiling" };
                AssetDatabase.CreateAsset(ceiling, ceilingPath);
            }
            ceiling.SetFloat("_Metallic", .08f);
            ceiling.SetFloat("_MetallicRemapMax", .10f);
            ceiling.SetFloat("_Smoothness", .18f);
            ceiling.SetFloat("_SmoothnessRemapMin", .08f);
            ceiling.SetFloat("_SmoothnessRemapMax", .30f);
            ceiling.SetFloat("_CoatMask", 0);
            ceiling.SetFloat("_NormalScale", .15f);
            ceiling.SetFloat("_EnableGeometricSpecularAA", 1);
            HDMaterial.ValidateMaterial(ceiling);
            EditorUtility.SetDirty(ceiling); AssetDatabase.SaveAssetIfDirty(ceiling);
            RefreshSurfaces();
            var root = Find(RootName);
            int triangles = root.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null).Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 500000 || root.GetComponentsInChildren<Light>().Length != 2 || root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Market envelope exceeded the existing visual/light/collision budget.");
            File.WriteAllText(Output + "/envelope-validation.txt",
                "PASS matte interior lining remapped, valid material references, protected art/floor and exact gameplay/nav fingerprint unchanged.\n" +
                "PASS no added visual-root colliders or lights; triangles=" + triangles + "\n" +
                "Blender envelope bounds and end-doorway audit: SourceArt/Environment/MarketRoom/MarketRoomReport.json\n" +
                EnemyValidationRunner.ConsoleCounts());
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Market Technical Detail Pass Five")]
        public static void RefreshDetails()
        {
            RefreshEnvelope();
            File.WriteAllText(Output + "/detail-validation.txt",
                "PASS technical fittings reuse accepted materials, valid model references, protected art/floor and exact gameplay/nav fingerprint unchanged.\n" +
                "PASS existing light/collider/triangle budgets retained; fitting bounds and clear aisle/end doorways audited in Blender.\n" +
                EnemyValidationRunner.ConsoleCounts());
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Rebuild Checkout Devices")]
        public static void RefreshDesk()
        {
            RequireIdle();
            string texturePath = Folder + "/Textures/MarketDeviceDisplay.png";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.sRGBTexture = true; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            string path = Folder + "/Materials/MarketDeviceDisplay.mat";
            var display = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (display == null)
            {
                display = new Material(AssetDatabase.LoadAssetAtPath<Material>(Art + "/CornerSample/Materials/DisplayGlass.mat")) { name = "MarketDeviceDisplay" };
                AssetDatabase.CreateAsset(display, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            display.SetTexture("_BaseColorMap", texture);
            display.SetColor("_BaseColor", Color.white);
            display.SetFloat("_Metallic", .05f); display.SetFloat("_Smoothness", .56f);
            display.SetFloat("_CoatMask", .12f);
            display.SetTexture("_EmissiveColorMap", texture);
            display.SetColor("_EmissiveColor", new Color(.9f, .9f, .9f));
            display.SetFloat("_UseEmissiveIntensity", 0);
            HDMaterial.ValidateMaterial(display);
            EditorUtility.SetDirty(display); AssetDatabase.SaveAssetIfDirty(display);
            string shellPath = Folder + "/Materials/MarketDeviceShell.mat";
            var shell = AssetDatabase.LoadAssetAtPath<Material>(shellPath);
            if (shell == null)
            {
                shell = new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/MarketLining.mat")) { name = "MarketDeviceShell" };
                AssetDatabase.CreateAsset(shell, shellPath);
            }
            shell.SetTexture("_BaseColorMap", null);
            shell.SetColor("_BaseColor", new Color(.16f, .18f, .19f));
            shell.SetFloat("_SmoothnessRemapMin", .15f); shell.SetFloat("_SmoothnessRemapMax", .45f);
            shell.SetFloat("_MetallicRemapMax", .35f); shell.SetFloat("_CoatMask", .05f);
            shell.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/Market_Detail.png"));
            shell.SetTextureScale("_DetailMap", new Vector2(6, 6));
            shell.SetFloat("_UVDetail", 1); shell.SetVector("_UVDetailsMappingMask", new Vector4(0, 1, 0, 0));
            shell.SetFloat("_LinkDetailsWithBase", 0); shell.SetFloat("_DetailAlbedoScale", 0);
            shell.SetFloat("_DetailNormalScale", .15f); shell.SetFloat("_DetailSmoothnessScale", .12f);
            HDMaterial.ValidateMaterial(shell);
            EditorUtility.SetDirty(shell); AssetDatabase.SaveAssetIfDirty(shell);
            RefreshSurfaces();
            var root = Find(RootName);
            int triangles = root.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null).Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 500000 || root.GetComponentsInChildren<Light>().Length != 2 || root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Checkout device budget/ownership failed.");
            if (!display.IsKeywordEnabled("_EMISSIVE_COLOR_MAP"))
                throw new InvalidOperationException("Checkout display texture is not active.");
            File.WriteAllText(Output + "/desk-validation.txt",
                "PASS two existing checkout devices rebuilt, display atlas assigned, valid material references.\n" +
                "PASS protected art/floor and exact gameplay/navigation fingerprint unchanged; no added visual-root colliders/lights.\n" +
                "Triangles including retained interior=" + triangles + "\n" + EnemyValidationRunner.ConsoleCounts());
        }

        private static TMP_FontAsset Font()
        {
            string path = Folder + "/Market Orbitron SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;
            font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Art + "/CornerSample/Fonts/Orbitron.ttf"), 90, 9, GlyphRenderMode.SDFAA, 512, 512);
            font.name = "Market Orbitron SDF";
            if (!font.TryAddCharacters("MARKET ")) throw new InvalidOperationException("Market font glyphs failed.");
            font.atlasPopulationMode = AtlasPopulationMode.Static; AssetDatabase.CreateAsset(font, path);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font); EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }

        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout(); Directory.CreateDirectory(Output);
            View("market-exterior", new Vector3(-6.7f, 3.2f, 32.5f), new Vector3(-14, 2.3f, 40));
            View("market-side-entry", new Vector3(-8.3f, 1.7f, 40.5f), new Vector3(-16, 1.8f, 40.5f));
            View("market-through-south", new Vector3(-14.4f, 1.7f, 31.9f), new Vector3(-14.4f, 1.7f, 48));
            View("market-interior", new Vector3(-14.5f, 1.7f, 36), new Vector3(-14.2f, 1.9f, 45.5f));
            View("market-through-north", new Vector3(-14.4f, 1.7f, 49), new Vector3(-14.4f, 1.7f, 32));
            View("market-window", new Vector3(-7.1f, 2.3f, 37.2f), new Vector3(-11.1f, 2.05f, 40.5f));
            View("market-sill-close", new Vector3(-9.6f, 1.95f, 39.65f), new Vector3(-11.0f, 1.26f, 40.4f));
            View("market-counter-close", new Vector3(-14.0f, 1.85f, 42.8f), new Vector3(-12.1f, .95f, 44.2f));
            View("market-storage", new Vector3(-14.65f, 1.85f, 38.8f), new Vector3(-17.0f, 1.55f, 40.9f));
            View("market-packing", new Vector3(-14.0f, 1.95f, 38.2f), new Vector3(-12.05f, 1.15f, 36.6f));
            View("market-ceiling", new Vector3(-14.5f, 2.1f, 36.0f), new Vector3(-14.45f, 4.05f, 42.3f));
            View("market-shutter-control", new Vector3(-12.9f, 1.95f, 41.7f), new Vector3(-11.42f, 1.82f, 42.45f));
            View("market-cassette-detail", new Vector3(-15.65f, 1.48f, 39.1f), new Vector3(-16.87f, 1.28f, 40.5f));
            View("market-desk-player", new Vector3(-13.15f, 1.96f, 44.30f), new Vector3(-12.0f, 1.42f, 44.25f));
            View("market-desk-front", new Vector3(-13.15f, 1.68f, 44.65f), new Vector3(-12.0f, 1.53f, 44.65f));
            View("market-counter-front", new Vector3(-14.1f, 1.55f, 44.2f), new Vector3(-12.0f, .85f, 44.2f));
            View("market-counter-surface", new Vector3(-12.95f, 1.60f, 44.35f), new Vector3(-12.15f, 1.13f, 44.15f));
            void View(string name, Vector3 p, Vector3 target) => BattlefieldBaseline.Capture(scene, name, p, Quaternion.LookRotation(target - p), false, Output, temporalAntialiasing: true);
        }

        private static string Actors()
        {
            var scene = BattlefieldBlockout.RequireBlockout(); var text = new StringBuilder();
            foreach (var actor in BattlefieldBlockout.FindInScene<EnemyActor>(scene).OrderBy(a => a.name))
            {
                text.Append(actor.name).Append(actor.SquadId).Append(actor.EncounterId).Append(actor.transform.position.ToString("F4")).Append(actor.transform.rotation.ToString("F4"));
                for (int i = 0; i < actor.Route.Count; i++) { actor.Route.TryGetPoint(i, out var p); text.Append(p.ToString("F4")); }
            }
            text.Append(BattlefieldBlockout.FindInScene<PlayerInputReader>(scene).Single().transform.position.ToString("F4")); return Hash(text.ToString());
        }

        private static string OtherCollision()
        {
            var navigation = BattlefieldBlockout.FindInScene<NavMeshSurface>(BattlefieldBlockout.RequireBlockout()).Single();
            var text = new StringBuilder();
            foreach (var c in navigation.GetComponentsInChildren<Collider>(true).Where(c => c.name != "West workshop" && !c.transform.GetComponentsInParent<Transform>().Any(t => t.name == CollisionName)).OrderBy(c => c.name))
            {
                text.Append(c.name).Append(c.enabled).Append(c.isTrigger).Append(c.gameObject.layer).Append(c.transform.position.ToString("F4")).Append(c.transform.rotation.ToString("F4")).Append(c.transform.lossyScale.ToString("F4"));
                if (c is BoxCollider box) text.Append(box.center.ToString("F4")).Append(box.size.ToString("F4"));
                if (c is MeshCollider mesh) text.Append(AssetDatabase.GetAssetPath(mesh.sharedMesh)).Append(mesh.sharedMesh.vertexCount);
            }
            return Hash(text.ToString());
        }

        private static string ProtectedAssets()
        {
            using var hash = SHA256.Create(); var text = new StringBuilder();
            foreach (string folder in Directory.GetDirectories(Art).Where(p => !p.EndsWith("MarketRoom", StringComparison.Ordinal)).OrderBy(p => p))
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).OrderBy(p => p))
                    text.AppendLine(path + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
            return text.ToString();
        }

        private static string Hash(string text) { using var hash = SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text))); }
        private static void WriteBaseline(string name, string text) { File.WriteAllText(Folder + "/" + name, text); AssetDatabase.ImportAsset(Folder + "/" + name); }
        private static Transform Find(string name) => FindOrNull(name) ?? throw new InvalidOperationException("Market authoring object missing: " + name);
        private static Transform FindOrNull(string name) => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == name);
        private static void RequireIdle() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for an idle Editor before market authoring."); }
    }
}
