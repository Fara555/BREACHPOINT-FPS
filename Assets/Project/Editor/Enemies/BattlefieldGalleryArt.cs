using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldGalleryArt
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/ServiceGallery";
        private const string ModelPath = Folder + "/Models/ServiceGallery.fbx";
        private const string PrefabPath = Folder + "/PF_ServiceGallery.prefab";
        private const string HashPath = Folder + "/AcceptedAssets.sha256.txt";
        private const string Output = "Logs/BattlefieldGalleryArt";
        private const string RootName = "09 Art - Service gallery";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Create Service Gallery")]
        public static void Create()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var parent = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                .Single(t => t.name == "Blockout B - Service Quarter");
            if (parent.Find(RootName) != null)
            {
                ValidateVisuals();
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Gallery scene save failed.");
                Capture();
                return;
            }
            string protectedHash = AcceptedAssetsHash();
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            File.Copy(scene.path, Output + "/scene-before-gallery.unity", true);
            if (File.Exists(PrefabPath))
            {
                if (File.ReadAllText(HashPath) != protectedHash) throw new InvalidOperationException("Accepted assets differ from the saved gallery baseline.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
                instance.name = RootName;
                Undo.RegisterCreatedObjectUndo(instance, "Connect service gallery visuals");
            }
            else
            {
                ConfigureModel();
                var root = new GameObject(RootName);
                root.transform.SetParent(parent, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
                instance.name = "Service gallery - Blender";
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                Undo.RegisterCreatedObjectUndo(root, "Create service gallery visuals");
                File.WriteAllText(HashPath, protectedHash);
                AssetDatabase.ImportAsset(HashPath);
            }
            MatchGalleryBacking();
            if (AcceptedAssetsHash() != protectedHash || BattlefieldCornerArt.GameplayFingerprint(scene) != gameplay)
                throw new InvalidOperationException("Gallery visuals changed accepted assets or gameplay configuration.");
            ValidateVisuals();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Gallery scene wiring save failed.");
            Capture();
        }

        private static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export the Blender gallery model first.");
            importer.importAnimation = false;
            importer.importLights = false;
            importer.importCameras = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) throw new InvalidOperationException("Empty gallery material slot.");
                    var target = AssetDatabase.LoadAssetAtPath<Material>(BattlefieldCornerMaterials.Folder + "/Materials/" + material.name + ".mat");
                    if (target == null) throw new InvalidOperationException("Unknown gallery material: " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
        }

        private static void MatchGalleryBacking()
        {
            const string materials = "Assets/Project/Scenes/BattlefieldBlockout/Materials/";
            var backing = AssetDatabase.LoadAssetAtPath<Material>(materials + "Gallery.mat");
            var architecture = AssetDatabase.LoadAssetAtPath<Material>(materials + "Architecture.mat");
            if (backing == null || architecture == null) throw new InvalidOperationException("Gallery backing palette is missing.");
            Undo.RecordObject(backing, "Match gallery backing to the existing dark block palette");
            backing.shader = architecture.shader;
            backing.CopyPropertiesFromMaterial(architecture);
            HDMaterial.ValidateMaterial(backing);
            EditorUtility.SetDirty(backing);
            AssetDatabase.SaveAssetIfDirty(backing);
        }

        private static float SurfaceHeight(float z)
        {
            if (z < 32 || z > 56) return 0;
            if (z < 40) return (z - 32) * .1875f;
            return z <= 48 ? 1.5f : (56 - z) * .1875f;
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Gallery Visuals")]
        public static void ValidateVisuals()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PrefabPath ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Gallery must remain a linked visual-only prefab without lights, scripts or colliders.");
            if (AcceptedAssetsHash() != File.ReadAllText(HashPath)) throw new InvalidOperationException("Accepted corner or entry assets changed.");
            if (BattlefieldCornerArt.GameplayFingerprint(scene) != File.ReadAllText(BattlefieldCornerMaterials.Folder + "/GameplayBaseline.txt"))
                throw new InvalidOperationException("Gameplay data differs from the saved baseline.");
            var geometry = BattlefieldBlockout.LegacyMeshValidationRoot(root);
            var renderers = geometry.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 5 || renderers.Any(r => r.sharedMaterials.Any(m => m == null || m.shader.name != "HDRP/Lit")))
                throw new InvalidOperationException("Gallery must use five existing HDRP material groups.");
            int triangles = 0;
            foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Gallery mesh/UV/normal reference failed.");
                triangles += mesh.triangles.Length / 3;
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 p = filter.transform.TransformPoint(vertex);
                    bool apron = p.x >= 11.99f && p.x <= 18.01f && ((p.z >= 29.99f && p.z <= 32.01f) || (p.z >= 55.99f && p.z <= 58.01f)) && p.y >= -.021f && p.y <= .035f;
                    bool deck = p.x >= 13 && p.x <= 18.01f && p.z >= 31.99f && p.z <= 56.01f && Mathf.Abs(p.y - SurfaceHeight(p.z)) <= .035f;
                    bool skirt = p.x >= 12.93f && p.x <= 13.26f && p.z >= 31.99f && p.z <= 56.01f && p.y >= -.14f && p.y <= SurfaceHeight(p.z) + .035f;
                    bool parapet = p.x >= 12.90f && p.x <= 13.27f && p.z >= 39.99f && p.z <= 48.01f && p.y >= 1.49f && p.y <= 2.65f;
                    if (!apron && !deck && !skirt && !parapet) throw new InvalidOperationException("Stray gallery vertex: " + p + " in " + filter.name);
                }
            }
            if (triangles > 80000) throw new InvalidOperationException("Gallery visual triangle budget exceeded: " + triangles);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/visual-validation.txt", "PASS visual-only gallery: five shared HDRP groups, references/UVs/normals/slope/skirt/parapet bounds, no new lights/colliders/scripts.\n" +
                "Accepted asset hashes and gameplay checksum unchanged. Triangles=" + triangles + "\nNo combat route, AI or NavMesh path tests were run.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Service Gallery")]
        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            ulong prior = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                View("gallery-overview", new Vector3(5, 9, 44), new Vector3(15.3f, 1, 44));
                View("south-approach", new Vector3(15.3f, 1.7f, 30.5f), new Vector3(15.3f, 1.8f, 38));
                View("apron-connection", new Vector3(10, 2.3f, 30.4f), new Vector3(14.7f, .1f, 32.4f));
                View("north-approach", new Vector3(10, 2.2f, 57.2f), new Vector3(15.1f, 1.2f, 51));
                void View(string name, Vector3 position, Vector3 target) => BattlefieldBaseline.Capture(scene, name, position,
                    Quaternion.LookRotation(target - position), false, Output);
            }
            finally { EditorSceneManager.SetSceneCullingMask(scene, prior); }
        }

        private static string AcceptedAssetsHash()
        {
            using var memory = new MemoryStream();
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
                foreach (string folder in new[] { BattlefieldCornerMaterials.Folder, "Assets/Project/Art/Environment/ServiceQuarter/EntryZone" })
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).OrderBy(p => p))
                    { byte[] bytes = File.ReadAllBytes(path); writer.Write(path); writer.Write(bytes.Length); writer.Write(bytes); }
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(memory.ToArray()));
        }
    }
}
