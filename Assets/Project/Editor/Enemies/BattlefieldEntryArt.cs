using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldEntryArt
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/EntryZone";
        private const string RootName = "08 Art - Entry service zone";
        private const string PrefabPath = Folder + "/PF_EntryServiceZone.prefab";
        private const string ApprovalPath = Folder + "/AcceptedCorner.sha256.txt";
        private const string Output = "Logs/BattlefieldEntryArt";
        private static readonly string[] _modelNames = { "Deck_Service", "Deck_Solid", "Deck_Vent", "Deck_Transition", "Entry_Wall" };

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Create Entry Service Zone")]
        public static void Create()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var parent = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                .Single(t => t.name == "Blockout B - Service Quarter");
            if (parent.Find(RootName) != null) { Validate(); Capture(); return; }
            BattlefieldCornerArt.Validate();
            string approved = AcceptedCornerHash();
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            // Preserve the disk version before saving current user edits plus the new scene wiring.
            File.Copy(scene.path, Output + "/scene-before-entry.unity", true);
            if (File.Exists(PrefabPath))
            {
                if (File.ReadAllText(ApprovalPath) != approved) throw new InvalidOperationException("Accepted corner differs from the saved entry baseline.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
                instance.name = RootName;
                Undo.RegisterCreatedObjectUndo(instance, "Connect entry service zone");
            }
            else
            {
                BattlefieldCornerMaterials.EnsureFolder(Folder + "/Meshes");
                if (AssetDatabase.FindAssets("t:Mesh", new[] { Folder + "/Meshes" }).Length != 0)
                    throw new InvalidOperationException("Incomplete entry meshes exist; inspect before rebuilding.");
                foreach (string name in _modelNames) ConfigureModel(name);
                var groups = BuildGroups();
                var root = new GameObject(RootName);
                root.transform.SetParent(parent, false);
                foreach (var group in groups)
                {
                    var mesh = new Mesh { name = "SM_Entry_" + group.Key.name };
                    if (group.Value.Sum(c => c.mesh.vertexCount) > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(group.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/" + mesh.name + ".asset");
                    var part = new GameObject(group.Key.name);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                    GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic);
                }
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                Undo.RegisterCreatedObjectUndo(root, "Create entry service zone");
                File.WriteAllText(ApprovalPath, approved);
                AssetDatabase.ImportAsset(ApprovalPath);
            }
            if (BattlefieldCornerArt.GameplayFingerprint(scene) != gameplay || AcceptedCornerHash() != approved)
                throw new InvalidOperationException("Entry art changed accepted assets or gameplay data.");
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Entry scene wiring could not be saved.");
            Capture();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Rebuild Entry Geometry")]
        public static void Rebuild()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            if (!File.Exists(PrefabPath) || AcceptedCornerHash() != File.ReadAllText(ApprovalPath))
                throw new InvalidOperationException("Create the entry prefab and verify its accepted baseline first.");
            foreach (string name in _modelNames) ConfigureModel(name);
            foreach (var group in BuildGroups())
            {
                string path = Folder + "/Meshes/SM_Entry_" + group.Key.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) throw new InvalidOperationException("Missing owned entry mesh: " + path);
                var replacement = new Mesh { name = existing.name, indexFormat = IndexFormat.UInt32 };
                replacement.CombineMeshes(group.Value.ToArray(), true, true);
                replacement.RecalculateBounds();
                EditorUtility.CopySerialized(replacement, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                Object.DestroyImmediate(replacement);
            }
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Entry scene save failed.");
            Capture();
        }

        private static Dictionary<Material, List<CombineInstance>> BuildGroups()
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            for (int ix = 0; ix < 4; ix++)
                for (int iz = 0; iz < 5; iz++)
                {
                    if (iz >= 3 && ix < 3) continue;
                    string model = ix == 3 && iz == 2 ? "Deck_Vent" : (ix + iz) % 3 == 0 ? "Deck_Service" : "Deck_Solid";
                    AddModel(groups, model, new Vector3(5 + ix * 2, 0, 22 + iz * 2));
                }
            for (int ix = 0; ix < 3; ix++)
                foreach (float z in new[] { 27.5f, 30.5f }) AddModel(groups, "Deck_Transition", new Vector3(5 + ix * 2, 0, z));
            AddModel(groups, "Entry_Wall", Vector3.zero);
            return groups;
        }

        private static void ConfigureModel(string name)
        {
            string path = Folder + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export the entry model kit first: " + path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    var target = AssetDatabase.LoadAssetAtPath<Material>(BattlefieldCornerMaterials.Folder + "/Materials/" + material.name + ".mat");
                    if (target == null) throw new InvalidOperationException("Unknown entry material: " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
        }

        private static void AddModel(Dictionary<Material, List<CombineInstance>> groups, string name, Vector3 position)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/" + name + ".fbx");
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var material = filter.GetComponent<Renderer>().sharedMaterial;
                if (!groups.TryGetValue(material, out var entries)) { entries = new List<CombineInstance>(); groups.Add(material, entries); }
                entries.Add(new CombineInstance { mesh = filter.sharedMesh,
                    transform = Matrix4x4.Translate(position) * model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Entry Service Zone")]
        public static void Validate()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PrefabPath ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Entry prefab link or visual-only component contract failed.");
            if (AcceptedCornerHash() != File.ReadAllText(ApprovalPath)) throw new InvalidOperationException("Accepted corner assets changed.");
            var floor = new Bounds(new Vector3(8, .01f, 26), new Vector3(8.002f, .062f, 10.002f));
            var screen = new Bounds(new Vector3(7, 2, 29), new Vector3(6.4f, 4.02f, 2.4f));
            var blocked = new Bounds(new Vector3(7, .01f, 29), new Vector3(5.98f, .08f, 1.98f));
            var geometry = BattlefieldBlockout.LegacyMeshValidationRoot(root);
            var renderers = geometry.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length != 5 || renderers.Any(r => r.sharedMaterial == null || r.sharedMaterial.shader.name != "HDRP/Lit"))
                throw new InvalidOperationException("Entry requires five existing HDRP material groups.");
            int triangles = 0;
            foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                triangles += mesh.triangles.Length / 3;
                if (mesh.normals.Length != mesh.vertexCount || mesh.uv.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Entry mesh is missing UVs/normals.");
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    if ((!floor.Contains(point) && !screen.Contains(point)) || (floor.Contains(point) && blocked.Contains(point)))
                        throw new InvalidOperationException("Entry geometry outside its region or inside the sight-screen footprint: " + point);
                }
            }
            if (triangles > 60000) throw new InvalidOperationException("Entry triangle budget exceeded: " + triangles);
            BattlefieldCornerArt.Validate();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/validation.txt", "PASS accepted corner hash unchanged; gameplay fingerprint unchanged; 14 full + 6 closure deck cells + existing entry screen.\n" +
                "Five shared HDRP groups, no colliders/lights/scripts, UVs/normals/bounds/no blocked tiles. Triangles=" + triangles + "\n" +
                EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Entry Service Zone")]
        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            ulong prior = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                View("entry", new Vector3(7, 1.75f, 22.2f), new Vector3(8, 1.4f, 28));
                View("connection", new Vector3(12.6f, 3, 26), new Vector3(7.5f, .4f, 33));
                View("wall-detail", new Vector3(12.1f, 1.7f, 27), new Vector3(10, 1.9f, 29));
                void View(string name, Vector3 position, Vector3 target) => BattlefieldBaseline.Capture(scene, name, position,
                    Quaternion.LookRotation(target - position), false, Output);
            }
            finally { EditorSceneManager.SetSceneCullingMask(scene, prior); }
        }

        private static string AcceptedCornerHash()
        {
            using var memory = new MemoryStream();
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
                foreach (string path in Directory.GetFiles(BattlefieldCornerMaterials.Folder, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).OrderBy(p => p))
                { byte[] bytes = File.ReadAllBytes(path); writer.Write(path); writer.Write(bytes.Length); writer.Write(bytes); }
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(memory.ToArray()));
        }
    }
}
