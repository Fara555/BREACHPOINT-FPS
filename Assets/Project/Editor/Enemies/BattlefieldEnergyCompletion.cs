using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldEnergyCompletion
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/EnergyCompletion";
        private const string ModelPath = Folder + "/Models/EnergyCompletion.fbx";
        private const string PrefabPath = Folder + "/PF_EnergyCompletion.prefab";
        private const string HashPath = Folder + "/AcceptedAssets.sha256.txt";
        private const string GameplayPath = Folder + "/GameplayBefore.sha256.txt";
        private const string Output = "Logs/BattlefieldEnergyCompletion";
        private const string RootName = "10 Art - Energy module completion";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Complete Energy Module Visuals")]
        public static void Create()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var parent = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                .Single(t => t.name == "Blockout B - Service Quarter");
            if (parent.Find(RootName) != null)
            {
                ValidateVisuals();
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Completion scene save failed.");
                Capture();
                return;
            }
            string approved = AcceptedHash();
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            File.Copy(scene.path, Output + "/scene-before-completion.unity", true);
            if (File.Exists(PrefabPath))
            {
                if (File.ReadAllText(HashPath) != approved) throw new InvalidOperationException("Accepted assets differ from this stage's baseline.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
                instance.name = RootName;
                Undo.RegisterCreatedObjectUndo(instance, "Connect energy completion visuals");
            }
            else
            {
                ConfigureModel();
                var root = new GameObject(RootName);
                root.transform.SetParent(parent, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
                model.name = "Energy completion - Blender";
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                Undo.RegisterCreatedObjectUndo(root, "Complete energy module visuals");
                File.WriteAllText(HashPath, approved);
                File.WriteAllText(GameplayPath, gameplay);
                AssetDatabase.ImportAsset(HashPath);
                AssetDatabase.ImportAsset(GameplayPath);
            }
            if (AcceptedHash() != approved || BattlefieldCornerArt.GameplayFingerprint(scene) != gameplay)
                throw new InvalidOperationException("Visual completion changed accepted assets or gameplay data.");
            ValidateVisuals();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Energy completion scene wiring save failed.");
            Capture();
        }

        private static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export the Blender completion model first.");
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) throw new InvalidOperationException("Empty energy completion material slot.");
                    var target = AssetDatabase.LoadAssetAtPath<Material>(BattlefieldCornerMaterials.Folder + "/Materials/" + material.name + ".mat");
                    if (target == null) throw new InvalidOperationException("Unknown completion material: " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Energy Completion Visuals")]
        public static void ValidateVisuals()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PrefabPath ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Completion must be a linked visual-only prefab without scripts, lights or colliders.");
            if (AcceptedHash() != File.ReadAllText(HashPath)) throw new InvalidOperationException("Previously accepted asset contents changed.");
            if (BattlefieldCornerArt.GameplayFingerprint(scene) != File.ReadAllText(GameplayPath))
                throw new InvalidOperationException("Gameplay data changed relative to the pre-stage snapshot.");
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 5 || renderers.Any(r => r.sharedMaterials.Any(m => m == null || m.shader.name != "HDRP/Lit")))
                throw new InvalidOperationException("Energy completion requires five shared HDRP material groups.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Completion mesh references, normals or UVs failed.");
                triangles += mesh.triangles.Length / 3;
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 p = filter.transform.TransformPoint(vertex);
                    bool south = p.x >= -6.3f && p.x <= -2.03f && p.z >= 35.6f && p.z <= 36.005f;
                    bool east = p.x >= 3.99f && p.x <= 4.35f && p.z >= 42.03f && p.z <= 46.3f;
                    bool north = p.x >= -6.35f && p.x <= 4.1f && p.z >= 45.99f && p.z <= 46.35f;
                    bool west = p.x >= -6.35f && p.x <= -5.99f && p.z >= 35.9f && p.z <= 46.35f;
                    bool upperReturn = p.y >= 4.46f && p.y <= 4.53f &&
                        ((p.x >= -6.3f && p.x <= -2.03f && p.z >= 35.9f && p.z <= 36.08f) ||
                        (p.x >= 3.92f && p.x <= 4.1f && p.z >= 42.03f && p.z <= 46.3f) ||
                        (p.x >= -6.35f && p.x <= 4.1f && p.z >= 45.92f && p.z <= 46.1f) ||
                        (p.x >= -6.1f && p.x <= -5.92f && p.z >= 35.9f && p.z <= 46.35f));
                    if (p.y < -.02f || p.y > 4.6f || (!south && !east && !north && !west && !upperReturn))
                        throw new InvalidOperationException("Completion geometry outside uncovered elevations: " + p);
                }
            }
            if (triangles > 80000) throw new InvalidOperationException("Energy completion visual budget exceeded: " + triangles);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/visual-validation.txt", "PASS 14 facade bays in uncovered energy elevations; five shared HDRP groups, UVs/normals/bounds, no lights/colliders/scripts.\n" +
                "Accepted corner/entry/gallery contents unchanged; gameplay checksum matches the pre-stage snapshot.\nTriangles=" + triangles +
                "\nVisual-only validation; no combat, AI or pathfinding tests.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Energy Completion")]
        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            ulong prior = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                View("south-connection", new Vector3(-8, 1.75f, 31.5f), new Vector3(-2, 2, 36));
                View("west-elevation", new Vector3(-10, 2.2f, 40), new Vector3(-6, 2.1f, 41));
                View("northwest-corner", new Vector3(-10, 2.8f, 50), new Vector3(-3.8f, 2, 44));
                View("northeast-connection", new Vector3(8, 2.5f, 50), new Vector3(1.6f, 2, 43.3f));
                void View(string name, Vector3 position, Vector3 target) => BattlefieldBaseline.Capture(scene, name, position,
                    Quaternion.LookRotation(target - position), false, Output);
            }
            finally { EditorSceneManager.SetSceneCullingMask(scene, prior); }
        }

        private static string AcceptedHash()
        {
            using var memory = new MemoryStream();
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
                foreach (string folder in new[] { BattlefieldCornerMaterials.Folder,
                    "Assets/Project/Art/Environment/ServiceQuarter/EntryZone", "Assets/Project/Art/Environment/ServiceQuarter/ServiceGallery" })
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).OrderBy(p => p))
                    { byte[] bytes = File.ReadAllBytes(path); writer.Write(path); writer.Write(bytes.Length); writer.Write(bytes); }
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(memory.ToArray()));
        }
    }
}
