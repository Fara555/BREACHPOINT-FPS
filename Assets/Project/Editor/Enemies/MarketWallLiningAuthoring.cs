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
    public static class MarketWallLiningAuthoring
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketWallLining";
        private const string MarketModel = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Models/MarketRoom.fbx";
        private const string Output = "Logs/MarketWallLining";
        private const string RootName = "18 Art - Market interior wall lining";
        private const int TextureSize = 512;

        public static void Create()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            if (!File.Exists(Output + "/gameplay-before.txt"))
            {
                File.Copy(scene.path, Output + "/scene-before.unity", false);
                File.WriteAllText(Output + "/gameplay-before.txt", BattlefieldCornerArt.GameplayFingerprint(scene));
                View("before-interior", new Vector3(-15.2f, 1.75f, 35.7f), new Vector3(-14.5f, 2.2f, 44));
            }
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            var sharedLiningNormal = Finish("LiningCeramic", new Color(.52f, .54f, .51f), .03f, .26f, .035f);
            Finish("LiningGraphite", new Color(.26f, .32f, .33f), .18f, .29f, .055f);
            Finish("LiningTrim", new Color(.18f, .23f, .25f), .30f, .32f, .04f);
            Finish("LiningWarm", new Color(.56f, .31f, .14f), .20f, .28f, .035f, sharedLiningNormal);
            string modelPath = Folder + "/Models/MarketWallLining.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            foreach (var material in AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                var target = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + material.name + ".mat");
                if (target == null) throw new InvalidOperationException("Missing lining material: " + material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
            }
            importer.SaveAndReimport();
            var prefabRoot = new GameObject("PF_MarketWallLining");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), prefabRoot.transform);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, Folder + "/PF_MarketWallLining.prefab");
            }
            finally { Object.DestroyImmediate(prefabRoot); }
            var root = FindRoot();
            if (root == null)
            {
                root = new GameObject(RootName);
                root.transform.SetParent(scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "Blockout B - Service Quarter"), false);
                Undo.RegisterCreatedObjectUndo(root, "Refine market interior walls");
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PF_MarketWallLining.prefab"), root.transform);
            }
            ApplyLegacyReplacement();
            MarketServiceDoorAuthoring.ApplyRackRemoval();
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save wall lining scene.");
            Capture();
        }

        internal static void ApplyLegacyReplacement()
        {
            if (FindRoot() == null) return;
            var model = RoomModelInstance();
            var oldLining = model.GetComponentsInChildren<MeshRenderer>().Single(r => r.name.EndsWith("_MarketLining", StringComparison.Ordinal));
            Undo.RecordObject(oldLining, "Replace unfinished room lining"); oldLining.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldLining);
            // Remove only the six superseded backing plates from their combined material mesh.
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(MarketModel).GetComponentsInChildren<MeshFilter>().Single(f => f.name.EndsWith("_InteriorQuietFrame", StringComparison.Ordinal));
            var target = model.GetComponentsInChildren<MeshFilter>().Single(f => f.name == original.name);
            bool InOldBackpan(Vector3 p)
            {
                if (p.x > -17.401f && p.x < -17.309f && p.y > 3.489f && p.y < 4.371f &&
                    (Mathf.Abs(p.z - 37.45f) < 2.851f || Mathf.Abs(p.z - 43.55f) < 2.851f)) return true;
                if (p.y < .149f || p.y > .991f || !(Mathf.Abs(p.z - 34.34f) < .0386f || Mathf.Abs(p.z - 46.66f) < .0386f)) return false;
                return Mathf.Abs(p.x + 16.69f) < .641f || Mathf.Abs(p.x + 12.04f) < .671f;
            }
            var mesh = Object.Instantiate(original.sharedMesh); var positions = mesh.vertices; int removed = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = original.sharedMesh.GetTriangles(sub); var kept = new List<int>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    bool Inside(int index) => InOldBackpan(target.transform.TransformPoint(positions[index]));
                    if (Inside(triangles[t]) && Inside(triangles[t + 1]) && Inside(triangles[t + 2])) { removed++; continue; }
                    kept.Add(triangles[t]); kept.Add(triangles[t + 1]); kept.Add(triangles[t + 2]);
                }
                mesh.SetTriangles(kept, sub, false);
            }
            if (removed > 2000) { Object.DestroyImmediate(mesh); throw new InvalidOperationException("Backpan removal exceeds expected scope."); }
            Undo.RecordObject(target, "Replace obsolete lining backing plates");
            if (removed == 0) { target.sharedMesh = original.sharedMesh; Object.DestroyImmediate(mesh); }
            else
            {
                BattlefieldCornerMaterials.EnsureFolder(Folder + "/Integration");
                mesh.name = "Filtered_" + original.name;
                string path = Folder + "/Integration/" + mesh.name + ".asset";
                var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (stored == null) { AssetDatabase.CreateAsset(mesh, path); stored = mesh; }
                else { EditorUtility.CopySerialized(mesh, stored); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(stored); AssetDatabase.SaveAssetIfDirty(stored); }
                target.sharedMesh = stored;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            File.WriteAllText(Output + "/legacy-replacement.txt", "Old MarketLining renderer disabled (internal facing only). Backpan triangles omitted=" + removed + ". Canonical room art unchanged.\n");
        }

        private static Texture2D Finish(string name, Color color, float metal, float roughnessInverse, float relief, Texture2D sharedNormal = null)
        {
            int count = TextureSize * TextureSize;
            var colors = new Color[count]; var masks = new Color[count]; var normals = new Color[count];
            for (int y = 0; y < TextureSize; y++) for (int x = 0; x < TextureSize; x++)
            {
                int i = y * TextureSize + x;
                // Quiet albedo: visible variation lives primarily in surface response.
                float grain = Mathf.PerlinNoise(x * .34f + 31, y * .34f + 17) - .5f;
                float scuff = Mathf.Pow(Mathf.PerlinNoise(x * .07f + 5, y * .50f), 9);
                colors[i] = color * (1 + grain * .010f); colors[i].a = 1;
                masks[i] = new Color(metal, 1, 1, roughnessInverse + grain * .06f - scuff * .05f);
                var n = new Vector3(grain * relief, (Mathf.PerlinNoise(x * .39f + 73, y * .39f) - .5f) * relief, 1).normalized;
                normals[i] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("HDRP/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", Map(name + "_BaseColor", colors, true, false));
            material.SetTexture("_MaskMap", Map(name + "_Mask", masks, false, false));
            var normalMap = sharedNormal ?? Map(name + "_Normal", normals, false, true);
            material.SetTexture("_NormalMap", normalMap);
            material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0); material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
            material.SetFloat("_NormalScale", .7f); material.SetFloat("_EnableGeometricSpecularAA", 1);
            HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return normalMap;
        }

        private static Texture2D Map(string name, Color[] pixels, bool srgb, bool normal)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.maxTextureSize = TextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void Validate()
        {
            RequireIdle(); var scene = BattlefieldBlockout.RequireBlockout(); var root = FindRoot();
            if (root == null || root.transform.childCount != 1 || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.transform.GetChild(0).gameObject) != Folder + "/PF_MarketWallLining.prefab") throw new InvalidOperationException("Wall lining prefab reference is missing.");
            if (!MarketServiceDoorAuthoring.MatchesGameplay(scene, File.ReadAllText(Output + "/gameplay-before.txt"))) throw new InvalidOperationException("Wall art changed gameplay/navigation bytes outside approved door recess.");
            if (root.GetComponentsInChildren<Collider>().Length != 0 || root.GetComponentsInChildren<Light>().Length != 0 || root.GetComponentsInChildren<MonoBehaviour>().Length != 0) throw new InvalidOperationException("Wall lining must remain passive.");
            if (RoomModelInstance().GetComponentsInChildren<MeshRenderer>().Single(r => r.name.EndsWith("_MarketLining", StringComparison.Ordinal)).enabled) throw new InvalidOperationException("Obsolete lining is still visible.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                triangles += filter.sharedMesh.triangles.Length / 3;
                foreach (Vector3 local in filter.sharedMesh.vertices)
                {
                    Vector3 p = filter.transform.TransformPoint(local);
                    if (p.y < .015f || p.y > 4.831f) throw new InvalidOperationException("New lining reaches below floor/into ceiling.");
                    if (p.x > -11.60f && p.z > 39.08f && p.z < 41.92f && p.y > 1.29f && p.y < 3.09f) throw new InvalidOperationException("Window aperture obstructed by wall art.");
                    if (p.x < -17.20f && p.z > 39.66f && p.z < 41.34f && p.y < 2.69f) throw new InvalidOperationException("Recessed service door covered by wall lining.");
                    if ((p.z < 34.60f || p.z > 46.40f) && p.x > -15.79f && p.x < -13.01f && p.y < 3.20f) throw new InvalidOperationException("Door aperture obstructed by wall art.");
                }
                foreach (var material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                {
                    if (material == null || !AssetDatabase.GetAssetPath(material).StartsWith(Folder + "/Materials/", StringComparison.Ordinal) || material.shader.name != "HDRP/Lit" || !material.IsKeywordEnabled("_MASKMAP") || !material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE")) throw new InvalidOperationException("Owned HDRP lining finish missing.");
                    foreach (string property in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap" })
                    {
                        string map = AssetDatabase.GetAssetPath(material.GetTexture(property));
                        if (!map.StartsWith(Folder + "/Textures/", StringComparison.Ordinal)) throw new InvalidOperationException("Lining map is not owned.");
                        var importer = (TextureImporter)AssetImporter.GetAtPath(map);
                        if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.sRGBTexture != (property == "_BaseColorMap")) throw new InvalidOperationException("Lining texture data/filtering invalid.");
                    }
                }
            }
            if (triangles > 25000) throw new InvalidOperationException("Lining mesh budget exceeded.");
            PackingDeskPropsAuthoring.Validate();
            File.WriteAllText(Output + "/validation.txt", "PASS passive standalone wall lining, owned HDRP maps/filtering/color spaces, old lining archived, door/window vertex envelopes clear, floor/ceiling excluded.\nPASS exact gameplay/navigation fingerprint and canonical accepted room/furniture/floor asset bytes; standalone counter props and fan preserved.\nTriangles=" + triangles + "\n" + EnemyValidationRunner.ConsoleCounts());
        }

        public static void Capture()
        {
            View("interior", new Vector3(-15.2f, 1.75f, 35.7f), new Vector3(-14.5f, 2.2f, 44));
            View("reverse", new Vector3(-14.8f, 1.75f, 45.0f), new Vector3(-14.2f, 2.15f, 36));
            View("packing-wall", new Vector3(-14.6f, 1.85f, 38), new Vector3(-11.5f, 2.1f, 36.3f));
            View("window-return", new Vector3(-14.0f, 1.8f, 41.2f), new Vector3(-11.4f, 2.15f, 42.3f));
            View("storage-upper", new Vector3(-14.6f, 2.55f, 40.5f), new Vector3(-17.3f, 3.7f, 37.4f));
            View("end-corner", new Vector3(-14.6f, 1.8f, 45.4f), new Vector3(-12.0f, 2.3f, 46.6f));
        }
        private static void View(string name, Vector3 p, Vector3 at) => BattlefieldBaseline.Capture(BattlefieldBlockout.RequireBlockout(), name, p, Quaternion.LookRotation(at - p), false, Output, temporalAntialiasing: true);
        private static GameObject FindRoot() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == RootName)?.gameObject;
        private static void RequireIdle() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Wait for idle Unity Editor."); }
        private static Transform RoomModelInstance() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "14 Art - Walk-through market room").GetComponentsInChildren<Transform>().Single(t => t.name == "MarketRoom");
    }
}
