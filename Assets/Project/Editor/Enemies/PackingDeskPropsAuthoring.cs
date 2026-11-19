using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class PackingDeskPropsAuthoring
    {
        private const string Folder = "Assets/Project/Art/Props/PackingDesk";
        private const string Output = "Logs/PackingDeskProps";
        private const string RootName = "17 Props - Packing desk essentials";
        private static readonly string[] Models = { "InsulatedCup", "UtilityMarker", "DispatchSlip" };

        public static void Create()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            string fingerprint = BattlefieldCornerArt.GameplayFingerprint(scene);
            if (!File.Exists(Output + "/gameplay-before.txt"))
            {
                File.WriteAllText(Output + "/gameplay-before.txt", fingerprint);
                File.Copy(scene.path, Output + "/scene-before.unity", false);
            }
            BuildMaterials();
            foreach (string name in Models) BuildPrefab(name);
            if (FindRoot() == null)
            {
                var parent = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "Blockout B - Service Quarter");
                var root = new GameObject(RootName);
                root.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(root, "Replace packing desk placeholders");
                Place("InsulatedCup", new Vector3(-11.61f, 1.145f, 37.47f), -65, root.transform);
                Place("UtilityMarker", new Vector3(-12.47f, 1.145f, 37.54f), 63, root.transform);
                Place("DispatchSlip", new Vector3(-12.49f, 1.145f, 36.60f), -12, root.transform);
                Place("DispatchSlip", new Vector3(-12.49f, 1.145f, 36.81f), 17, root.transform);
            }
            // Keep the authoring command repeatable when refining these owned placements.
            var placed = FindRoot().transform;
            Undo.RecordObjects(placed.Cast<Transform>().ToArray(), "Refine packing desk placements");
            placed.GetChild(0).rotation = Quaternion.Euler(0, -65, 0);
            placed.GetChild(2).position = new Vector3(-12.49f, 1.145f, 36.60f);
            placed.GetChild(3).position = new Vector3(-12.49f, 1.145f, 36.81f);
            ApplyLegacyRemoval();
            Validate();
            if (fingerprint != BattlefieldCornerArt.GameplayFingerprint(scene)) throw new InvalidOperationException("Desk art changed gameplay data.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save packing desk props.");
            Capture();
        }

        private static void Place(string name, Vector3 position, float yaw, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PF_" + name + ".prefab"), parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        }

        private static void BuildPrefab(string name)
        {
            string path = Folder + "/Models/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            foreach (var material in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                var finish = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + material.name + ".mat");
                if (finish == null) throw new InvalidOperationException("Missing prop finish: " + material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), finish);
            }
            importer.SaveAndReimport();
            var root = new GameObject("PF_" + name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/PF_" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static void ApplyLegacyRemoval()
        {
            if (FindRoot() == null) return;
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Integration");
            var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldMarketLife.Folder + "/Models/DailyUse.fbx");
            var instance = BattlefieldMarketLife.FindRoot().GetComponentsInChildren<Transform>().Single(t => t.name == "Daily use props and guarded fan body");
            int removed = 0;
            foreach (var source in canonical.GetComponentsInChildren<MeshFilter>())
            {
                var target = instance.GetComponentsInChildren<MeshFilter>().Single(f => f.name == source.name);
                var original = source.sharedMesh;
                var positions = original.vertices;
                bool IsSouth(Vector3 local)
                {
                    Vector3 p = target.transform.TransformPoint(local);
                    return p.z > 36.40f && p.z < 37.80f && p.x > -12.65f && p.x < -11.30f && p.y > 1.14f && p.y < 1.30f;
                }
                var filtered = Object.Instantiate(original); int count = 0;
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    var kept = new List<int>(); int[] triangles = original.GetTriangles(sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        if (IsSouth(positions[triangles[t]]) && IsSouth(positions[triangles[t + 1]]) && IsSouth(positions[triangles[t + 2]])) { count++; continue; }
                        kept.Add(triangles[t]); kept.Add(triangles[t + 1]); kept.Add(triangles[t + 2]);
                    }
                    filtered.SetTriangles(kept, sub, false);
                }
                Undo.RecordObject(target, "Replace south desk placeholders");
                if (count == 0) { target.sharedMesh = original; Object.DestroyImmediate(filtered); }
                else
                {
                    if (count > 4000) throw new InvalidOperationException("Unexpected legacy desk removal extent.");
                    string path = Folder + "/Integration/Filtered_" + source.name + ".asset";
                    var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path); filtered.name = "Filtered_" + source.name;
                    if (stored == null) { AssetDatabase.CreateAsset(filtered, path); stored = filtered; }
                    else { EditorUtility.CopySerialized(filtered, stored); Object.DestroyImmediate(filtered); EditorUtility.SetDirty(stored); AssetDatabase.SaveAssetIfDirty(stored); }
                    target.sharedMesh = stored; removed += count;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            File.WriteAllText(Output + "/legacy-removal.txt", "Removed only south counter cup, marker and loose label triangles: " + removed + ". Canonical Life FBX unchanged; north desk/fan retained.\n");
        }

        private static void BuildMaterials()
        {
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            Finish("CupCoating", new Color(.16f, .24f, .26f), .05f, .29f, true);
            Finish("DispatchPrint", new Color(.60f, .55f, .44f), 0, .13f, true);
            Finish("DeskSteel", new Color(.55f, .60f, .62f), .90f, .46f, false);
            Finish("DeskRubber", new Color(.07f, .085f, .09f), 0, .18f, false);
            Finish("DeskPolymer", new Color(.25f, .32f, .34f), .08f, .30f, false);
            Finish("DeskAccent", new Color(.62f, .31f, .11f), .45f, .34f, false);
            Finish("DeskCoffee", new Color(.012f, .006f, .003f), 0, .70f, false);
        }

        private static void Finish(string name, Color color, float metal, float smoothness, bool print)
        {
            const int size = 512;
            var albedo = new Color[size * size]; var mask = new Color[albedo.Length]; var normal = new Color[albedo.Length];
            bool paper = name == "DispatchPrint", steel = name == "DeskSteel";
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float grain = Mathf.PerlinNoise(x * .33f + 9, y * .33f + 5) - .5f;
                float brush = Mathf.Sin(y * 1.37f) * .5f + Mathf.PerlinNoise(x * .006f, y * .8f) - .5f;
                albedo[i] = color * (1 + grain * .025f); albedo[i].a = 1;
                mask[i] = new Color(metal, 1, 1, smoothness + (steel ? brush * .07f : grain * .045f));
                var n = new Vector3(grain * (paper ? .045f : .025f), steel ? brush * .08f : grain * .025f, 1).normalized;
                normal[i] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            Texture2D baseMap = print ? ImportTexture(Folder + "/Textures/" + name + "_BaseColor.png", true, false) : Save(name + "_BaseColor", albedo, true, false);
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("HDRP/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", baseMap);
            material.SetTexture("_MaskMap", Save(name + "_Mask", mask, false, false));
            material.SetTexture("_NormalMap", Save(name + "_Normal", normal, false, true));
            material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0); material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
            material.SetFloat("_NormalScale", .6f); material.SetFloat("_EnableGeometricSpecularAA", 1);
            HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        private static Texture2D Save(string name, Color[] pixels, bool srgb, bool normal)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            return ImportTexture(path, srgb, normal);
        }

        private static Texture2D ImportTexture(string path, bool srgb, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void Validate()
        {
            RequireIdle(); var scene = BattlefieldBlockout.RequireBlockout(); var root = FindRoot();
            if (root == null || root.transform.childCount != 4) throw new InvalidOperationException("Expected cup, marker and two dispatch slips.");
            if (!MarketServiceDoorAuthoring.MatchesGameplay(scene, File.ReadAllText(Output + "/gameplay-before.txt"))) throw new InvalidOperationException("Gameplay/nav data changed outside approved door recess.");
            if (root.GetComponentsInChildren<Collider>().Length != 0 || root.GetComponentsInChildren<Light>().Length != 0 || root.GetComponentsInChildren<MonoBehaviour>().Length != 0)
                throw new InvalidOperationException("Decorative desk props must be passive.");
            foreach (Transform child in root.transform)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (!path.StartsWith(Folder + "/PF_", StringComparison.Ordinal)) throw new InvalidOperationException("Standalone prefab link lost.");
                foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>())
                {
                    var b = renderer.bounds;
                    if (b.min.x < -12.64f || b.max.x > -11.32f || b.min.z < 35.64f || b.max.z > 37.96f || b.min.y < 1.144f || b.max.y > 1.30f)
                        throw new InvalidOperationException("Desk prop outside counter envelope: " + child.name + " " + b);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader.name != "HDRP/Lit" || !AssetDatabase.GetAssetPath(material).StartsWith(Folder + "/Materials/", StringComparison.Ordinal)) throw new InvalidOperationException("Missing owned HDRP material.");
                        if (!material.IsKeywordEnabled("_MASKMAP") || !material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE")) throw new InvalidOperationException("HDRP maps inactive.");
                        foreach (string property in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap" })
                        {
                            string texture = AssetDatabase.GetAssetPath(material.GetTexture(property));
                            if (!texture.StartsWith(Folder + "/Textures/", StringComparison.Ordinal)) throw new InvalidOperationException("Prop map is not owned.");
                            var importer = (TextureImporter)AssetImporter.GetAtPath(texture);
                            if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.sRGBTexture != (property == "_BaseColorMap")) throw new InvalidOperationException("Invalid map import.");
                        }
                    }
                }
            }
            BattlefieldMarketLife.Validate(); SealedSupplyPackAuthoring.Validate();
            int triangles = root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 18000) throw new InvalidOperationException("Desk essentials mesh budget exceeded.");
            File.WriteAllText(Output + "/validation.txt", "PASS four standalone prefab instances / three reusable models, owned active HDRP maps, data color spaces and mips, counter bounds, passive props.\nPASS previous accepted art bytes and exact gameplay/nav fingerprint; fan and ration prefab links preserved.\nTriangles=" + triangles + "\n" + EnemyValidationRunner.ConsoleCounts());
        }

        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            View("packing-desk", new Vector3(-13.4f, 1.92f, 37.7f), new Vector3(-12, 1.20f, 36.95f));
            View("cup-close", new Vector3(-11.78f, 1.34f, 37.65f), new Vector3(-11.61f, 1.20f, 37.47f));
            View("marker-close", new Vector3(-12.62f, 1.27f, 37.62f), new Vector3(-12.47f, 1.151f, 37.54f));
            View("slips-close", new Vector3(-12.83f, 1.48f, 36.93f), new Vector3(-12.47f, 1.15f, 36.68f));
            void View(string name, Vector3 p, Vector3 at) => BattlefieldBaseline.Capture(scene, name, p, Quaternion.LookRotation(at - p), false, Output, temporalAntialiasing: true);
        }

        private static GameObject FindRoot() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == RootName)?.gameObject;
        private static void RequireIdle() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Wait for idle Unity Editor."); }
    }
}
