using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldWallSurface
    {
        private const int Size = 512;
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Bake Revised Wall Surfaces")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before wall surface authoring.");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            Surface("WallDark", new Color(.055f, .060f, .065f), .30f, 473);
            Surface("WallCeramic", new Color(.28f, .26f, .22f), .15f, 811);
        }

        internal static void BakeAccentFinishes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before accent surface authoring.");
            // Match the accepted material Color values when replacing them with sRGB albedo maps.
            Surface("WallWarm", new Color(.52f, .29f, .06f).linear, .15f, 1297);
            Surface("InteriorNeutral", new Color(.55f, .53f, .44f).linear, .20f, 1657);
            foreach (string name in new[] { "WallWarm", "InteriorNeutral" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
                material.SetFloat("_CoatMask", .1f);
                HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static void Surface(string name, Color tint, float metal, int seed)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
            if (material == null) throw new InvalidOperationException("Update the wall model/materials before baking: " + name);
            var strokes = new float[Size * Size];
            var random = new System.Random(seed);
            for (int n = 0; n < 90; n++)
            {
                int x = random.Next(8, Size - 40), y = random.Next(8, Size - 8), length = random.Next(4, 31);
                for (int step = 0; step < length; step++) strokes[y * Size + x + step] = .2f * Mathf.Sin(Mathf.PI * step / length);
            }
            var color = new Color[Size * Size]; var mask = new Color[color.Length]; var normal = new Color[color.Length];
            Color paint = tint.gamma;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                float u = x / (float)(Size - 1), v = y / (float)(Size - 1);
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                float grain = Noise(x, y, seed) - .5f;
                float edgeFade = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge / .04f));
                float dirt = edgeFade * (.35f + Noise(x / 5, y / 9, seed) * .65f);
                float wear = Mathf.Max(strokes[i], (1 - Mathf.Clamp01(edge / .007f)) * Mathf.Max(0, Noise(x / 2, y / 3, seed) - .6f));
                color[i] = Color.Lerp(paint * (1 + grain * .006f - dirt * .055f), new Color(.58f, .60f, .61f), wear * .25f);
                color[i].a = 1;
                mask[i] = new Color(Mathf.Lerp(metal, .85f, wear), 1 - dirt * .08f, 1, .34f + grain * .006f - dirt * .06f + wear * .22f);
                float dx = Noise((x + 1) % Size, y, seed) - Noise((x + Size - 1) % Size, y, seed);
                float dy = Noise(x, (y + 1) % Size, seed) - Noise(x, (y + Size - 1) % Size, seed);
                Vector3 n = new Vector3(-dx * .008f, -dy * .008f, 1).normalized;
                normal[i] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            Undo.RecordObject(material, "Refine revised wall finish");
            material.SetTexture("_BaseColorMap", Save(name + "_BaseColor", color, true, false));
            material.SetTexture("_MaskMap", Save(name + "_Mask", mask, false, false));
            material.SetTexture("_NormalMap", Save(name + "_Normal", normal, false, true));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_NormalScale", .6f);
            material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0); material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
            HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        private static Texture2D Save(string name, Color[] pixels, bool srgb, bool normal)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8; importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Noise(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed);
                n = (n ^ (n >> 16)) * 0x7feb352du;
                n = (n ^ (n >> 15)) * 0x846ca68bu;
                return (n ^ (n >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
