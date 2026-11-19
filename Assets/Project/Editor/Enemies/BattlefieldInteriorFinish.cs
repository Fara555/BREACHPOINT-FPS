using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TextCore.LowLevel;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldInteriorFinish
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish";
        private const string ModelPath = Folder + "/Models/InteriorFinish.fbx";
        private const string PrefabPath = Folder + "/PF_InteriorFinish.prefab";
        private const string Output = "Logs/BattlefieldInteriorFinish";
        private const string RootName = "12 Art - Interior walls ceiling and equipment";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Finish Interior Without Floor")]
        public static void Create()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var parent = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == "Blockout B - Service Quarter");
            if (parent.Find(RootName) != null) { ValidateVisuals(); Capture(); return; }
            string frozen = FrozenHash();
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            File.Copy(scene.path, Output + "/scene-before-interior.unity", true);
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("Existing interior prefab must be inspected before reconnecting lighting.");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            CreateEmissive("InteriorFixture", new Color(.65f, .8f, .95f), new Color(3, 5, 7));
            CreateEmissive("ExitAccent", new Color(.25f, .85f, .55f), new Color(1, 5, 2));
            ConfigureModel();
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
            model.name = "Interior architecture - Blender";
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(8f);
            var indirect = profile.Add<IndirectLightingController>(true);
            indirect.indirectDiffuseLightingMultiplier.Override(.20f);
            indirect.reflectionLightingMultiplier.Override(.35f);
            AssetDatabase.CreateAsset(profile, Folder + "/InteriorLighting.asset");
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            var volumeObject = new GameObject("Interior HDRP lighting"); volumeObject.transform.SetParent(root.transform, false);
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = profile;
            foreach (float x in new[] { -10f, 10f }) foreach (float z in new[] { 28f, 42f, 56f })
            {
                var obj = new GameObject("Ceiling spot " + x + " " + z); obj.transform.SetParent(root.transform, false);
                obj.transform.SetPositionAndRotation(new Vector3(x, 7.40f, z), Quaternion.Euler(90, 0, 0));
                var light = obj.AddComponent<Light>();
                if (obj.GetComponent<HDAdditionalLightData>() == null) obj.AddComponent<HDAdditionalLightData>();
                light.type = LightType.Spot; light.spotAngle = 115; light.innerSpotAngle = 95;
                light.range = 27; light.lightUnit = LightUnit.Candela; light.intensity = 6000;
                light.color = new Color(.78f, .87f, 1);
                light.shadows = (x < 0 && z != 42) || (x > 0 && z == 42) ? LightShadows.Soft : LightShadows.None;
            }
            var sign = new GameObject("Attached exit label").AddComponent<TextMeshPro>();
            sign.transform.SetParent(root.transform, false); sign.transform.position = new Vector3(-4, 4.90f, 63.50f);
            sign.text = "EXIT"; sign.font = ExitFont();
            sign.fontSize = 4.5f; sign.characterSpacing = 8; sign.alignment = TextAlignmentOptions.Center;
            sign.color = new Color(.4f, 1, .65f); sign.rectTransform.sizeDelta = new Vector2(2.8f, .45f);
            // Older blueprint signs are behind the new opaque wall skins; they are retained.
            foreach (var light in BattlefieldBlockout.FindInScene<Light>(scene).Where(l => l.type == LightType.Directional))
            { Undo.RecordObject(light, "Switch from outdoor sun to interior fixtures"); light.enabled = false; }
            foreach (string name in new[] { "Boundary", "Equipment" }) MatchBacking(name);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(root, "Finish interior without floor");
            File.WriteAllText(Folder + "/FrozenFloorAndAssets.sha256.txt", frozen);
            File.WriteAllText(Folder + "/GameplayBefore.sha256.txt", gameplay);
            AssetDatabase.ImportAsset(Folder + "/FrozenFloorAndAssets.sha256.txt"); AssetDatabase.ImportAsset(Folder + "/GameplayBefore.sha256.txt");
            ValidateVisuals();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Interior scene save failed.");
            Capture();
        }

        private static void CreateEmissive(string name, Color color, Color emission)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var material = new Material(Shader.Find("HDRP/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .4f);
            material.SetFloat("_UseEmissiveIntensity", 0); material.SetColor("_EmissiveColor", emission); material.SetFloat("_EmissiveExposureWeight", 0);
            HDMaterial.ValidateMaterial(material); AssetDatabase.CreateAsset(material, path);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Update Composed Interior Walls")]
        public static void UpdateWalls()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            if (FrozenHash() != File.ReadAllText(Folder + "/FrozenFloorAndAssets.sha256.txt"))
                throw new InvalidOperationException("Frozen floor or accepted assets differ before the wall update.");
            foreach (var entry in new[] { ("WallCeramic", new Color(.42f, .39f, .31f)), ("WallWarning", new Color(.65f, .32f, .075f)), ("WallDark", new Color(.12f, .17f, .20f)) })
            {
                string path = Folder + "/Materials/" + entry.Item1 + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                var material = new Material(Shader.Find("HDRP/Lit")) { name = entry.Item1 };
                material.SetColor("_BaseColor", entry.Item2); material.SetFloat("_Metallic", .08f); material.SetFloat("_Smoothness", .30f);
                HDMaterial.ValidateMaterial(material); AssetDatabase.CreateAsset(material, path);
            }
            ConfigureModel();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<TMP_Text>() == null))
            {
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
            }
            ValidateVisuals();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Wall revision scene save failed.");
            Capture();
        }

        private static TMP_FontAsset ExitFont()
        {
            string path = Folder + "/Exit Orbitron SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(BattlefieldCornerMaterials.Folder + "/Fonts/Orbitron.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 512, 512);
            font.name = "Exit Orbitron SDF";
            if (!font.TryAddCharacters("EXIT ")) throw new InvalidOperationException("Exit font glyph generation failed.");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        private static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export interior Blender model first.");
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) throw new InvalidOperationException("Empty interior material slot.");
                var target = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + material.name + ".mat") ??
                    AssetDatabase.LoadAssetAtPath<Material>(BattlefieldCornerMaterials.Folder + "/Materials/" + material.name + ".mat");
                if (target == null) throw new InvalidOperationException("Unknown interior material " + material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
            }
            importer.SaveAndReimport();
        }

        private static void MatchBacking(string name)
        {
            const string path = "Assets/Project/Scenes/BattlefieldBlockout/Materials/";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path + name + ".mat");
            var source = AssetDatabase.LoadAssetAtPath<Material>(path + "Architecture.mat");
            Undo.RecordObject(material, "Match remaining backing palette"); material.shader = source.shader; material.CopyPropertiesFromMaterial(source);
            HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Interior Visuals")]
        public static void ValidateVisuals()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PrefabPath) throw new InvalidOperationException("Interior prefab link is missing.");
            if (FrozenHash() != File.ReadAllText(Folder + "/FrozenFloorAndAssets.sha256.txt")) throw new InvalidOperationException("Frozen floor or accepted asset contents changed.");
            if (!BattlefieldMarketRoom.MatchesGameplay(scene, Folder + "/GameplayBefore.sha256.txt")) throw new InvalidOperationException("Gameplay data changed outside the accepted layout revision.");
            if (root.GetComponentsInChildren<Collider>(true).Length != 0) throw new InvalidOperationException("Interior visuals must not add collision.");
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null || !(c is TextMeshPro || c is Volume || c is HDAdditionalLightData)))
                throw new InvalidOperationException("Unexpected runtime component in interior visuals.");
            if (root.GetComponentsInChildren<Transform>(true).Any(t => t.GetComponents<Component>().Any(c => c == null)))
                throw new InvalidOperationException("Missing component reference in interior visuals.");
            var lights = root.GetComponentsInChildren<Light>(true);
            if (lights.Length != 6 || lights.Count(l => l.shadows != LightShadows.None) != 3 || lights.Any(l => l.type != LightType.Spot)) throw new InvalidOperationException("Interior light budget failed.");
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<TMP_Text>() == null).ToArray();
            if (renderers.Length != 12 || renderers.Any(r => r.sharedMaterial == null || r.sharedMaterial.shader.name != "HDRP/Lit")) throw new InvalidOperationException("Interior material group check failed.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.GetComponent<TMP_Text>() == null))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.normals.Length != mesh.vertexCount || mesh.uv.Length != mesh.vertexCount) throw new InvalidOperationException("Interior mesh data missing.");
                triangles += mesh.triangles.Length / 3;
                foreach (var v in mesh.vertices)
                {
                    Vector3 p = filter.transform.TransformPoint(v);
                    // FBX transforms can move the roof limit by a few floating-point ULPs.
                    if (p.x < -18.301f || p.x > 18.301f || p.z < 19.699f || p.z > 64.301f || p.y < .039f || p.y > 8.251f)
                        throw new InvalidOperationException("Interior stray geometry or floor geometry: " + p);
                }
            }
            if (triangles > 500000) throw new InvalidOperationException("Interior geometry budget exceeded: " + triangles);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/wall-material-bindings.txt", root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.GetComponent<TMP_Text>() == null)
                .SelectMany(r => r.sharedMaterials.Select(m => r.name + " => " + AssetDatabase.GetAssetPath(m) + " | " + AssetDatabase.GetAssetPath(m.GetTexture("_BaseColorMap")))));
            File.WriteAllText(Output + "/visual-validation.txt", "PASS frozen floor/accepted asset hashes and gameplay checksum unchanged.\nInterior references, UVs/normals, no generated floor/colliders, twelve mesh groups and six spotlights (three shadowed).\nTriangles=" + triangles + "\nNo combat/AI/pathfinding tests.\n" + EnemyValidationRunner.ConsoleCounts());
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Finished Interior")]
        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout(); Directory.CreateDirectory(Output);
            ulong prior = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                foreach (var text in BattlefieldBlockout.FindInScene<TextMeshPro>(scene)) text.ForceMeshUpdate();
                View("entry-interior", new Vector3(11, 1.75f, 23), new Vector3(2, 2.5f, 38));
                View("workshop", new Vector3(-8.5f, 1.75f, 32), new Vector3(-14, 2.6f, 38));
                View("main-room", new Vector3(12, 3, 34), new Vector3(-1, 2.8f, 45));
                View("gallery", new Vector3(15.5f, 3.1f, 39), new Vector3(3, 2.6f, 49));
                View("exit", new Vector3(-5, 1.75f, 59.5f), new Vector3(-4, 3, 63.7f));
                View("cover-detail", new Vector3(6.4f, 1.5f, 41), new Vector3(8.5f, .7f, 43.6f));
                View("east-wall", new Vector3(12.4f, 3.3f, 39), new Vector3(18, 4.3f, 44));
                View("north-wall", new Vector3(-8, 3.4f, 58.8f), new Vector3(-1, 4.5f, 64));
                View("south-wall", new Vector3(-4, 2.5f, 25), new Vector3(4, 4.2f, 20));
                View("west-wall-upper", new Vector3(-8.5f, 5.8f, 50), new Vector3(-18, 5.8f, 43));
                View("south-portal", new Vector3(2, 2.2f, 24), new Vector3(6, 3.2f, 20));
                View("wall-close", new Vector3(15.1f, 1.8f, 30.2f), new Vector3(18, 2.8f, 30.8f));
                View("north-services", new Vector3(12.8f, 1.8f, 60), new Vector3(15, 3.3f, 64));
                View("color-overview", new Vector3(-6, 6.8f, 31.5f), new Vector3(3, 2.2f, 45));
                void View(string name, Vector3 position, Vector3 target) => BattlefieldBaseline.Capture(scene, name, position, Quaternion.LookRotation(target-position), false, Output, temporalAntialiasing: true);
            }
            finally { EditorSceneManager.SetSceneCullingMask(scene, prior); }
        }

        private static string FrozenHash()
        {
            using var memory = new MemoryStream();
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
            {
                foreach (string folder in new[] { BattlefieldCornerMaterials.Folder, "Assets/Project/Art/Environment/ServiceQuarter/EntryZone", "Assets/Project/Art/Environment/ServiceQuarter/ServiceGallery", "Assets/Project/Art/Environment/ServiceQuarter/EnergyCompletion", "Assets/Project/Art/Environment/ServiceQuarter/ServiceLoop" })
                    foreach (string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta")).OrderBy(p=>p))
                    { byte[] bytes=File.ReadAllBytes(path); writer.Write(path); writer.Write(bytes.Length); writer.Write(bytes); }
                writer.Write(File.ReadAllBytes("Assets/Project/Scenes/BattlefieldBlockout/Materials/Ground.mat"));
            }
            using var hash=SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(memory.ToArray()));
        }
    }
}
