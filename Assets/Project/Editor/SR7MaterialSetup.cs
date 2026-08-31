using System;
using UnityEditor;
using UnityEngine;

namespace Breachpoint.Editor
{
    internal static class SR7MaterialSetup
    {
        private const string ModelPath = "Assets/Project/Art/Weapons/SR7/SR7_MaterialPass2.fbx";
        private const string MaterialsFolder = "Assets/Project/Art/Weapons/SR7/Materials";
        private const string TexturesFolder = "Assets/Project/Art/Weapons/SR7/Textures";

        private const string GunmetalMaterialPath = MaterialsFolder + "/M_SR7_Gunmetal.mat";
        private const string PolymerMaterialPath = MaterialsFolder + "/M_SR7_Polymer.mat";
        private const string AccentMaterialPath = MaterialsFolder + "/M_SR7_Accent.mat";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticSetup()
        {
            EditorApplication.delayCall += CreateAndAssignMaterialsIfNeeded;
        }

        [MenuItem("Tools/BREACHPOINT/SR7/Create and Assign HDRP Materials")]
        private static void CreateAndAssignMaterials()
        {
            SetupMaterials(forceUpdate: true);
        }

        private static void CreateAndAssignMaterialsIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(GunmetalMaterialPath) != null &&
                AssetDatabase.LoadAssetAtPath<Material>(PolymerMaterialPath) != null &&
                AssetDatabase.LoadAssetAtPath<Material>(AccentMaterialPath) != null)
            {
                return;
            }

            SetupMaterials(forceUpdate: false);
        }

        private static void SetupMaterials(bool forceUpdate)
        {
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
            {
                Debug.LogError("SR7 material setup failed: HDRP/Lit shader was not found.");
                return;
            }

            EnsureFolder("Assets/Project/Art/Weapons/SR7", "Materials");

            Material gunmetal = GetOrCreateMaterial(GunmetalMaterialPath, hdrpLit);
            Material polymer = GetOrCreateMaterial(PolymerMaterialPath, hdrpLit);
            Material accent = GetOrCreateMaterial(AccentMaterialPath, hdrpLit);

            ConfigureGunmetal(gunmetal);
            ConfigurePolymer(polymer);
            ConfigureAccent(accent);

            EditorUtility.SetDirty(gunmetal);
            EditorUtility.SetDirty(polymer);
            EditorUtility.SetDirty(accent);
            AssetDatabase.SaveAssets();

            AssignMaterialsToModel(gunmetal, polymer, accent);

            if (forceUpdate)
            {
                Debug.Log("SR7 HDRP materials were created and assigned successfully.");
            }
        }

        private static Material GetOrCreateMaterial(string path, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.shader = shader;
                return material;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureGunmetal(Material material)
        {
            Texture2D baseColor = LoadTexture("T_RS7_Gunmetal_BaseColor.png");
            Texture2D maskMap = LoadTexture("T_SR7_Gunmetal_MaskMap.png");

            material.name = "M_SR7_Gunmetal";
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", baseColor);
            material.SetTexture("_MaskMap", maskMap);
            material.SetFloat("_Metallic", 0.84f);
            material.SetFloat("_Smoothness", 0.69f);
        }

        private static void ConfigurePolymer(Material material)
        {
            Texture2D baseColor = LoadTexture("T_RS7_Polymer_BaseColor.png");
            Texture2D maskMap = LoadTexture("T_SR7_Polymer_MaskMap.png");

            material.name = "M_SR7_Polymer";
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", baseColor);
            material.SetTexture("_MaskMap", maskMap);
            material.SetFloat("_Metallic", 0.01f);
            material.SetFloat("_Smoothness", 0.42f);
        }

        private static void ConfigureAccent(Material material)
        {
            Color orange = new Color(1f, 0.075f, 0.004f, 1f);
            Color emissiveOrange = orange * 3f;

            material.name = "M_SR7_Accent";
            material.SetColor("_BaseColor", orange);
            material.SetFloat("_Metallic", 0.32f);
            material.SetFloat("_Smoothness", 0.76f);
            material.SetColor("_EmissiveColor", emissiveOrange);
            material.SetFloat("_EmissiveIntensity", 3f);
            material.EnableKeyword("_EMISSIVE_COLOR_MAP");
        }

        private static Texture2D LoadTexture(string fileName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{fileName}");
            if (texture == null)
            {
                Debug.LogWarning($"SR7 texture was not found: {fileName}");
            }

            return texture;
        }

        private static void AssignMaterialsToModel(Material gunmetal, Material polymer, Material accent)
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"SR7 material setup failed: model was not found at {ModelPath}.");
                return;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(
                new AssetImporter.SourceAssetIdentifier(typeof(Material), "Blockout Gunmetal"),
                gunmetal);
            importer.AddRemap(
                new AssetImporter.SourceAssetIdentifier(typeof(Material), "Blockout Polymer"),
                polymer);
            importer.AddRemap(
                new AssetImporter.SourceAssetIdentifier(typeof(Material), "Blockout Accent"),
                accent);
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
