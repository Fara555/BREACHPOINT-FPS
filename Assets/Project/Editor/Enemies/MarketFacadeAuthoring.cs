using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class MarketFacadeAuthoring
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketFacade";
        private const string SourceFolder = "SourceArt/Environment/MarketFacade";
        private const string Output = "Logs/MarketFacade";
        private const string RootName = "20 Art - Market exterior facade";
        private const string Shared = "Assets/Project/Art/Environment/ServiceQuarter/MarketWallLining/Materials/";

        public static void Create()
        {
            RequireIdle(); var scene = BattlefieldBlockout.RequireBlockout(); Directory.CreateDirectory(Output);
            if (!File.Exists(SourceFolder + "/GameplayBefore.sha256.txt"))
            {
                File.WriteAllText(SourceFolder + "/GameplayBefore.sha256.txt", BattlefieldCornerArt.GameplayFingerprint(scene));
                File.WriteAllText(SourceFolder + "/ProtectedArtBefore.sha256.txt", ProtectedArt());
                File.Copy(scene.path, Output + "/scene-before.unity", false);
                View("before-exterior", new Vector3(-6.7f, 3.2f, 32.5f), new Vector3(-14, 2.3f, 40));
                View("before-window", new Vector3(-7.1f, 2.3f, 37.2f), new Vector3(-11.1f, 2.05f, 40.5f));
            }
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            Finish("FacadeEnamel", "LiningCeramic", new Color(1, .98f, .94f));
            Finish("FacadeGraphite", "LiningGraphite", new Color(.95f, .96f, .94f));
            Finish("FacadeTrim", "LiningTrim", Color.white);
            Finish("FacadeWarm", "LiningWarm", Color.white);
            string modelPath = Folder + "/Models/MarketFacade.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            foreach (var material in AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                var finish = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + material.name + ".mat");
                if (finish == null) throw new InvalidOperationException("Missing facade finish: " + material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), finish);
            }
            importer.SaveAndReimport();
            var prefabRoot = new GameObject("PF_MarketFacade");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), prefabRoot.transform);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, Folder + "/PF_MarketFacade.prefab");
            }
            finally { Object.DestroyImmediate(prefabRoot); }
            if (FindRoot() == null)
            {
                var root = new GameObject(RootName);
                root.transform.SetParent(scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "Blockout B - Service Quarter"), false);
                Undo.RegisterCreatedObjectUndo(root, "Refine market exterior facade");
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PF_MarketFacade.prefab"), root.transform);
            }
            Validate(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save market facade.");
            Capture();
        }

        private static void Finish(string name, string source, Color tint)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var template = AssetDatabase.LoadAssetAtPath<Material>(Shared + source + ".mat");
            if (template == null) throw new InvalidOperationException("Accepted wall finish library is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(template) { name = name }; AssetDatabase.CreateAsset(material, path); }
            else EditorUtility.CopySerialized(template, material);
            material.name = name; material.SetColor("_BaseColor", tint);
            material.SetFloat("_SmoothnessRemapMax", .90f); material.SetFloat("_CoatMask", .025f);
            MarketFacadeWeathering.Apply(material, name);
            HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        public static void Validate()
        {
            RequireIdle(); var scene = BattlefieldBlockout.RequireBlockout(); var root = FindRoot();
            if (root == null || root.transform.childCount != 1 || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.transform.GetChild(0).gameObject) != Folder + "/PF_MarketFacade.prefab") throw new InvalidOperationException("Facade prefab link missing.");
            if (BattlefieldCornerArt.GameplayFingerprint(scene) != File.ReadAllText(SourceFolder + "/GameplayBefore.sha256.txt")) throw new InvalidOperationException("Facade changed gameplay, collision or navigation bytes.");
            if (ProtectedArt() != File.ReadAllText(SourceFolder + "/ProtectedArtBefore.sha256.txt")) throw new InvalidOperationException("Accepted interior, furniture, props or canonical room art changed.");
            if (root.GetComponentsInChildren<Collider>().Length != 0 || root.GetComponentsInChildren<Light>().Length != 0 || root.GetComponentsInChildren<MonoBehaviour>().Length != 0) throw new InvalidOperationException("Facade must remain passive art.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                triangles += filter.sharedMesh.triangles.Length / 3;
                if (filter.sharedMesh.uv2.Length != filter.sharedMesh.vertexCount) throw new InvalidOperationException("Physical surface UVs missing.");
                foreach (Vector3 local in filter.sharedMesh.vertices)
                {
                    Vector3 p = filter.transform.TransformPoint(local);
                    if (p.x < -18.03f || p.x > -10.76f || p.y < .015f || p.y > 5.002f || p.z < 33.80f || p.z > 47.20f) throw new InvalidOperationException("Facade escaped its exterior envelope.");
                    if (p.x > -11.20f && p.z > 39.08f && p.z < 41.92f && p.y > 1.29f && p.y < 3.09f) throw new InvalidOperationException("Window blocked by facade.");
                    if ((p.z < 34.20f || p.z > 46.80f) && p.x > -15.79f && p.x < -13.01f && p.y < 3.20f) throw new InvalidOperationException("Doorway blocked by facade.");
                }
                foreach (var material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                {
                    if (material == null || !AssetDatabase.GetAssetPath(material).StartsWith(Folder + "/Materials/", StringComparison.Ordinal) || material.shader.name != "HDRP/Lit" || !material.IsKeywordEnabled("_MASKMAP") || !material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE")) throw new InvalidOperationException("Facade HDRP finish invalid.");
                    foreach (string property in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap" })
                    {
                        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture(property))) as TextureImporter;
                        if (importer == null || !importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.sRGBTexture != (property == "_BaseColorMap")) throw new InvalidOperationException("Shared PBR map import invalid.");
                    }
                    if (AssetDatabase.GetAssetPath(material.GetTexture("_BaseColorMap")).StartsWith(Folder + "/Textures/", StringComparison.Ordinal))
                    {
                        if (!material.IsKeywordEnabled("_DETAIL_MAP") || material.GetVector("_UVDetailsMappingMask") != new Vector4(0, 1, 0, 0) || material.GetTextureScale("_BaseColorMap") != Vector2.one / 8)
                            throw new InvalidOperationException("Facade weathering scale or physical detail UVs invalid.");
                        foreach (string property in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap", "_DetailMap" })
                            if (!AssetDatabase.GetAssetPath(material.GetTexture(property)).StartsWith(Folder + "/Textures/", StringComparison.Ordinal)) throw new InvalidOperationException("Facade weathering map is not owned.");
                        var detailImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture("_DetailMap")));
                        if (detailImporter.sRGBTexture || !detailImporter.mipmapEnabled || detailImporter.filterMode != FilterMode.Trilinear) throw new InvalidOperationException("Facade detail data import invalid.");
                    }
                }
            }
            if (triangles > 22000) throw new InvalidOperationException("Facade mesh budget exceeded.");
            MarketServiceDoorAuthoring.Validate();
            File.WriteAllText(Output + "/validation.txt", "PASS facade prefab, four owned HDRP materials/maps, physical UVs, weathering scale/detail ownership when applied, door/window vertex envelopes, passive components.\nPASS exact current gameplay/collision/nav fingerprint and protected canonical room/interior/furniture/props bytes. Recessed door and previous filters preserved.\nTriangles=" + triangles + "\n" + EnemyValidationRunner.ConsoleCounts());
        }

        public static void Capture()
        {
            View("exterior", new Vector3(-6.7f, 3.2f, 32.5f), new Vector3(-14, 2.3f, 40));
            View("window", new Vector3(-7.1f, 2.3f, 37.2f), new Vector3(-11.1f, 2.05f, 40.5f));
            View("south-portal", new Vector3(-13.5f, 2.05f, 30.1f), new Vector3(-14.4f, 2.4f, 34.1f));
            View("north-portal", new Vector3(-13.5f, 2.05f, 50.8f), new Vector3(-14.4f, 2.4f, 46.9f));
            View("corner-detail", new Vector3(-9.4f, 2.4f, 32.6f), new Vector3(-11.15f, 2.7f, 34.2f));
            View("window-skin-detail", new Vector3(-9.4f, 1.8f, 38.4f), new Vector3(-10.95f, 1.6f, 39.0f));
            View("paint-close", new Vector3(-9.88f, 2.5f, 36.65f), new Vector3(-10.88f, 2.35f, 36.2f));
        }
        private static void View(string name, Vector3 p, Vector3 at) => BattlefieldBaseline.Capture(BattlefieldBlockout.RequireBlockout(), name, p, Quaternion.LookRotation(at - p), false, Output, temporalAntialiasing: true);
        private static string ProtectedArt()
        {
            using var hash = SHA256.Create(); var text = new StringBuilder();
            string frozen = SourceFolder + "/ProtectedArtBefore.sha256.txt";
            if (File.Exists(frozen))
            {
                // Protect exactly the original manifest, while permitting newly authorized standalone assets.
                foreach (string line in File.ReadAllLines(frozen).Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    string path = line.Substring(0, line.LastIndexOf(':'));
                    text.AppendLine(path + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
                }
                return text.ToString();
            }
            foreach (string directory in new[] { "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom", "Assets/Project/Art/Environment/ServiceQuarter/MarketWallLining", "Assets/Project/Art/Props" })
                foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
                    text.AppendLine(path + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
            return text.ToString();
        }
        private static GameObject FindRoot() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == RootName)?.gameObject;
        private static void RequireIdle() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Wait for idle Unity Editor."); }
    }
}
