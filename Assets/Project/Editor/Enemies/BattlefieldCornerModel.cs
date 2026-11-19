using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class BattlefieldCornerArt
    {
        [Serializable] private class ModelSnapshot { public ModelPart[] parts; }
        [Serializable] private class ModelPart { public string material; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
        private const string ModelPath = Folder + "/Models/EnergyCorner.fbx";

        public static void ExportModelSnapshot()
        {
            BattlefieldBaseline.RequireIdleEditor();
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Single(t => t.name == RootName);
            if (root.Find("Detailed energy module - Blender") != null)
                throw new InvalidOperationException("The original model snapshot is already stored in SourceArt. Do not replace it with the detailed export.");
            var snapshot = new ModelSnapshot { parts = root.GetComponentsInChildren<MeshFilter>()
                .Where(f => f.GetComponent<TMP_Text>() == null).Select(f => new ModelPart
                {
                    material = f.GetComponent<Renderer>().sharedMaterial.name,
                    vertices = f.sharedMesh.vertices.Select(v => root.InverseTransformPoint(f.transform.TransformPoint(v))).ToArray(),
                    uv = f.sharedMesh.uv,
                    triangles = f.sharedMesh.triangles
                }).ToArray() };
            Directory.CreateDirectory("SourceArt/Environment/EnergyCorner");
            File.WriteAllText("SourceArt/Environment/EnergyCorner/BlockoutMesh.json", JsonUtility.ToJson(snapshot));
        }

        public static void InstallDetailedModel()
        {
            // Editing an asset does not require discarding or saving unrelated scene edits.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation/import before model installation.");
            BattlefieldBlockout.RequireBlockout();
            EnsureDeckMaterial();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export EnergyCorner.fbx from Blender first.");
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) throw new InvalidOperationException("Model has an empty material slot.");
                    var target = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + material.name + ".mat");
                    if (target == null) throw new InvalidOperationException("Unknown model material: " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var galleryLight = prefab.transform.Find("Gallery task spill");
                if (galleryLight != null) Object.DestroyImmediate(galleryLight.gameObject);
                var emptyDetailLayer = prefab.transform.Find("Cable and repair layer");
                if (emptyDetailLayer != null && emptyDetailLayer.childCount == 0 && emptyDetailLayer.GetComponents<Component>().Length == 1)
                    Object.DestroyImmediate(emptyDetailLayer.gameObject);
                var previousModel = prefab.transform.Find("Detailed energy module - Blender");
                if (previousModel != null) Object.DestroyImmediate(previousModel.gameObject);
                foreach (var mesh in prefab.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null).ToArray())
                    Object.DestroyImmediate(mesh.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, prefab.scene);
                instance.name = "Detailed energy module - Blender";
                instance.transform.SetParent(prefab.transform, false);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorApplication.delayCall += Validate;
        }

        private static void ValidateDetailedModel(Transform root)
        {
            var model = root.Find("Detailed energy module - Blender");
            if (model == null) return;
            var filters = model.GetComponentsInChildren<MeshFilter>();
            if (filters.Length != 8) throw new InvalidOperationException("Detailed corner requires eight metal/deck material mesh groups.");
            if (model.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m == null || m.name == "Concrete")))
                throw new InvalidOperationException("The interior sample must not use concrete or empty material slots.");
            int triangles = 0;
            var bounds = new Bounds();
            bool initialized = false;
            var moduleEnvelope = new Bounds(new Vector3(-2.9f, 2.29f, 2.9f), new Vector3(6.6f, 4.62f, 6.6f));
            var deckEnvelope = new Bounds(new Vector3(4, .01f, -1), new Vector3(8.002f, .062f, 8.002f));
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                triangles += mesh.triangles.Length / 3;
                if (mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Detailed model is missing UVs or normals.");
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (!moduleEnvelope.Contains(point) && !deckEnvelope.Contains(point))
                        throw new InvalidOperationException("Stray art vertex outside the equipment/deck: " + filter.name + " at " + point);
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (triangles > 120000 || bounds.min.x < -6.3f || bounds.min.x > -5.8f || bounds.max.x < 7.9f || bounds.max.x > 8.1f || bounds.max.y > 4.7f)
                throw new InvalidOperationException("Detailed model scale/orientation/triangle budget failed: " + bounds + " triangles=" + triangles);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/model-validation.txt", "PASS imported Blender model, eight material groups, no concrete, UVs and normals, all vertices within equipment/deck envelopes.\nTriangles=" + triangles + " bounds=" + bounds + "\n");
        }

        private static void EnsureDeckMaterial()
        {
            string path = Folder + "/Materials/DeckMetal.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Preserve the authored PBR maps when refreshing model geometry.
                return;
            }
            var source = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/BrushedSteel.mat");
            if (source == null) throw new InvalidOperationException("Deck metal requires the local brushed steel material.");
            var material = new Material(source) { name = "DeckMetal" };
            material.SetTexture("_BaseColorMap", null);
            material.SetTexture("_MaskMap", null);
            material.SetFloat("_Metallic", .75f);
            material.SetFloat("_Smoothness", .38f);
            material.SetColor("_BaseColor", new Color(.25f, .29f, .31f));
            material.SetFloat("_SmoothnessRemapMin", .12f);
            material.SetFloat("_SmoothnessRemapMax", .62f);
            material.SetFloat("_NormalScale", .16f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssetIfDirty(material);
        }
    }
}
