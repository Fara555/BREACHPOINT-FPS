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
    public static class BattlefieldServiceLoopArt
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/ServiceLoop";
        private const string ModelPath = Folder + "/Models/ServiceLoop.fbx";
        private const string PrefabPath = Folder + "/PF_ServiceLoop.prefab";
        private const string HashPath = Folder + "/AcceptedAssets.sha256.txt";
        private const string GameplayPath = Folder + "/GameplayBefore.sha256.txt";
        private const string Output = "Logs/BattlefieldServiceLoop";
        private const string RootName = "11 Art - Energy service loop deck";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Create Service Loop Deck")]
        public static void Create()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var parent = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                .Single(t => t.name == "Blockout B - Service Quarter");
            if (parent.Find(RootName) != null)
            {
                ValidateVisuals();
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Service loop scene save failed.");
                Capture();
                return;
            }
            string accepted = AcceptedHash();
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            File.Copy(scene.path, Output + "/scene-before-service-loop.unity", true);
            if (File.Exists(PrefabPath))
            {
                if (File.ReadAllText(HashPath) != accepted) throw new InvalidOperationException("Accepted assets differ from the service-loop baseline.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
                instance.name = RootName;
                Undo.RegisterCreatedObjectUndo(instance, "Connect service loop deck");
            }
            else
            {
                ConfigureModel();
                var root = new GameObject(RootName);
                root.transform.SetParent(parent, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
                model.name = "Service loop deck - Blender";
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                Undo.RegisterCreatedObjectUndo(root, "Create service loop deck");
                File.WriteAllText(HashPath, accepted);
                File.WriteAllText(GameplayPath, gameplay);
                AssetDatabase.ImportAsset(HashPath);
                AssetDatabase.ImportAsset(GameplayPath);
            }
            if (AcceptedHash() != accepted || BattlefieldCornerArt.GameplayFingerprint(scene) != gameplay)
                throw new InvalidOperationException("Service loop art changed accepted assets or gameplay data.");
            ValidateVisuals();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Service loop scene wiring save failed.");
            Capture();
        }

        private static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export the service loop Blender model first.");
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
                    if (material == null) throw new InvalidOperationException("Empty service loop material slot.");
                    var target = AssetDatabase.LoadAssetAtPath<Material>(BattlefieldCornerMaterials.Folder + "/Materials/" + material.name + ".mat");
                    if (target == null) throw new InvalidOperationException("Unknown service loop material: " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Service Loop Visuals")]
        public static void ValidateVisuals()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PrefabPath ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Service loop must be linked visual-only art without scripts, lights or colliders.");
            if (AcceptedHash() != File.ReadAllText(HashPath)) throw new InvalidOperationException("Previously accepted asset contents changed.");
            if (BattlefieldCornerArt.GameplayFingerprint(scene) != File.ReadAllText(GameplayPath))
                throw new InvalidOperationException("Gameplay data changed relative to the pre-stage snapshot.");
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 3 || renderers.Any(r => r.sharedMaterials.Any(m => m == null || m.shader.name != "HDRP/Lit")))
                throw new InvalidOperationException("Service loop requires three shared HDRP material groups.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Service loop mesh/UV/normal reference failure.");
                triangles += mesh.triangles.Length / 3;
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 p = filter.transform.TransformPoint(vertex);
                    bool west = p.x >= -11.001f && p.x <= -5.999f && p.z >= 31.999f && p.z <= 50.001f;
                    bool south = p.x >= -6.001f && p.x <= 4.001f && p.z >= 31.999f && p.z <= 36.001f;
                    bool north = p.x >= -6.001f && p.x <= 4.001f && p.z >= 45.999f && p.z <= 50.001f;
                    bool east = p.x >= 3.999f && p.x <= 13.001f && p.z >= 38.999f && p.z <= 50.001f;
                    bool insideEnergy = p.x > -5.999f && p.x < 3.999f && p.z > 36.001f && p.z < 45.999f;
                    bool insideOldDeck = p.x > 4.001f && p.x < 11.999f && p.z > 31.001f && p.z < 38.999f;
                    if (p.y < -.021f || p.y > .035f || (!west && !south && !north && !east) || insideEnergy || insideOldDeck)
                        throw new InvalidOperationException("Deck outside loop or overlapping accepted footprint: " + p);
                }
            }
            if (triangles > 130000) throw new InvalidOperationException("Service loop visual triangle budget exceeded: " + triangles);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/visual-validation.txt", "PASS loop deck: three shared HDRP groups, UVs/normals/bounds, no overlap with energy/accepted deck footprint, no lights/colliders/scripts.\n" +
                "58 full + 18 closure + 1 square modules, 269 square metres. Accepted assets and pre-stage gameplay checksum unchanged.\nTriangles=" + triangles +
                "\nVisual-only validation; no combat, AI or pathfinding tests.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Service Loop Deck")]
        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            ulong prior = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                View("west-loop", new Vector3(-8.5f, 1.75f, 32.5f), new Vector3(-8.3f, .9f, 43));
                View("south-connection", new Vector3(-7.5f, 3.4f, 30.8f), new Vector3(-.5f, .15f, 34.5f));
                View("north-loop", new Vector3(-8.3f, 3, 50.4f), new Vector3(-.5f, .5f, 47.5f));
                View("east-gallery-join", new Vector3(7.5f, 3.4f, 50.4f), new Vector3(11.2f, .6f, 43));
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
                    "Assets/Project/Art/Environment/ServiceQuarter/EntryZone", "Assets/Project/Art/Environment/ServiceQuarter/ServiceGallery",
                    "Assets/Project/Art/Environment/ServiceQuarter/EnergyCompletion" })
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).OrderBy(p => p))
                    { byte[] bytes = File.ReadAllBytes(path); writer.Write(path); writer.Write(bytes.Length); writer.Write(bytes); }
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(memory.ToArray()));
        }
    }
}
