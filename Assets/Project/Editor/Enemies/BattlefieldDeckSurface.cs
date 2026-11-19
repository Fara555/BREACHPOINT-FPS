using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldDeckSurface
    {
        private const int Size = 1024;
        private const string Folder = BattlefieldCornerMaterials.Folder;

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Bake Deck HDRP Surface")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before baking deck maps.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/DeckMetal.mat");
            if (material == null) throw new InvalidOperationException("Install the metal deck model first.");
            var heights = new float[Size * Size];
            var scratches = new float[heights.Length];
            var random = new System.Random(71931);
            for (int n = 0; n < 850; n++)
            {
                int x = random.Next(20, Size - 150), y = random.Next(40, Size - 40);
                int length = random.Next(8, 135);
                float depth = .1f + (float)random.NextDouble() * .45f;
                int direction = n % 3 - 1;
                for (int step = 0; step < length; step++)
                {
                    int py = Mathf.Clamp(y + step * direction / 8, 0, Size - 1);
                    int index = py * Size + x + step;
                    float stroke = depth * Mathf.Sin(Mathf.PI * step / length);
                    scratches[index] = Mathf.Max(scratches[index], stroke);
                    if (n % 4 == 0) scratches[index + Size] = Mathf.Max(scratches[index + Size], stroke * .45f);
                }
            }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    heights[i] = .94f + (Noise(x, y) - .5f) * .035f - scratches[i];
                }
            var color = new Color[heights.Length];
            var mask = new Color[heights.Length];
            var normal = new Color[heights.Length];
            var height = new Color[heights.Length];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = x / (float)(Size - 1), v = y / (float)(Size - 1);
                    float edgeDistance = Mathf.Abs(Mathf.Min(Mathf.Min(u, v), Mathf.Min(1 - u, 1 - v)) - .02f);
                    float wear = Mathf.Clamp01(1 - edgeDistance / .004f) * Mathf.SmoothStep(.35f, .8f, Noise(x / 3, y / 3));
                    float scratch = Mathf.Clamp01(scratches[i] * 2.5f);
                    float exposed = Mathf.Max(wear, scratch);
                    float grain = Noise(x, y) - .5f;
                    // Low-contrast paint grain; large cloudy color/gloss patches are deliberately absent.
                    Color paint = new Color(.29f, .32f, .335f) * (1 + grain * .035f + (Noise(x, y / 4) - .5f) * .018f);
                    color[i] = Color.Lerp(paint, new Color(.52f, .54f, .55f), exposed * .8f);
                    color[i].a = 1;
                    mask[i] = new Color(Mathf.Lerp(.32f, .94f, exposed), 1 - scratch * .10f, 1,
                        Mathf.Clamp01(.38f + grain * .045f + exposed * .19f));
                    float dx = heights[y * Size + (x + 1) % Size] - heights[y * Size + (x + Size - 1) % Size];
                    float dy = heights[((y + 1) % Size) * Size + x] - heights[((y + Size - 1) % Size) * Size + x];
                    // Height range is 0.6 mm over a two-metre panel, matching the POM amplitude.
                    Vector3 n = new Vector3(-dx * .0006f * Size / 4, -dy * .0006f * Size / 4, 1).normalized;
                    normal[i] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                    height[i] = new Color(heights[i], heights[i], heights[i], 1);
                }
            Undo.RecordObject(material, "Bake deck HDRP surface");
            material.SetTexture("_BaseColorMap", Save("Deck_BaseColor", color, Size, true));
            material.SetTexture("_MaskMap", Save("Deck_Mask", mask, Size, false));
            material.SetTexture("_NormalMap", Save("Deck_Normal", normal, Size, false, true));
            material.SetTexture("_HeightMap", Save("Deck_Height", height, Size, false));
            const int detailSize = 256;
            var detail = new Color[detailSize * detailSize];
            for (int y = 0; y < detailSize; y++)
                for (int x = 0; x < detailSize; x++)
                {
                    float grain = Noise(x * 7, y * 11) - .5f;
                    // HDRP detail: R albedo, G normal Y, B smoothness, A normal X.
                    detail[y * detailSize + x] = new Color(.5f + grain * .06f, .5f + grain * .16f,
                        .5f + grain * .10f, .5f + (Noise(x * 3, y) - .5f) * .10f);
                }
            material.SetTexture("_DetailMap", Save("Deck_Detail", detail, detailSize, false));
            material.SetTextureScale("_DetailMap", new Vector2(8, 8));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_MetallicRemapMin", 0);
            material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0);
            material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0);
            material.SetFloat("_AORemapMax", 1);
            material.SetFloat("_NormalScale", 1.5f);
            material.SetFloat("_DetailAlbedoScale", .15f);
            material.SetFloat("_DetailNormalScale", .5f);
            material.SetFloat("_DetailSmoothnessScale", .2f);
            material.SetFloat("_DisplacementMode", 2);
            material.SetFloat("_HeightPoMAmplitude", .06f);
            material.SetFloat("_PPDPrimitiveLength", 2);
            material.SetFloat("_PPDPrimitiveWidth", 2);
            material.SetFloat("_PPDMinSamples", 5);
            material.SetFloat("_PPDMaxSamples", 12);
            material.SetFloat("_PPDLodThreshold", 3);
            material.SetFloat("_DepthOffsetEnable", 0);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            if (!material.IsKeywordEnabled("_PIXEL_DISPLACEMENT") || !material.IsKeywordEnabled("_MASKMAP") ||
                !material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE") || !material.IsKeywordEnabled("_DETAIL_MAP"))
                throw new InvalidOperationException("Deck HDRP shader features were not enabled.");
            Directory.CreateDirectory("Logs/BattlefieldCornerArt");
            File.WriteAllText("Logs/BattlefieldCornerArt/deck-surface.txt", "PASS HDRP/Lit base, Mask (metal/AO/detail/smoothness), tangent normal, height POM 0.6 mm and RGBA detail.\nPOM 5-12 samples, fade LOD 3, depth offset disabled.\n");
        }

        private static Texture2D Save(string name, Color[] pixels, int size, bool srgb, bool normal = false)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Noise(int x, int y)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + 71931);
                value = (value ^ value >> 13) * 1274126177;
                return (value ^ value >> 16) / (float)uint.MaxValue;
            }
        }
    }
}
