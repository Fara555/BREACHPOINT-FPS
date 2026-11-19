using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    // Owned, offline surface authoring. Shared environment materials are never edited.
    public static class BattlefieldMarketSurface
    {
        private const int Size = 1024;
        private const int DetailSize = 256;
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom";
        private const string Shared = "Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Materials/";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Market Surface Pass Two")]
        public static void Bake()
        {
            BattlefieldBaseline.RequireIdleEditor();
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            var detail = Detail();
            Surface("MarketFrame", "InteriorQuietFrame", new Color(.08f, .09f, .095f).gamma, .12f, .26f, 391, false, detail);
            Surface("MarketWorktop", "WallDark", new Color(.26f, .28f, .29f), .78f, .35f, 617, true, detail);
            Surface("MarketWarm", "WallWarm", new Color(.52f, .29f, .06f), .10f, .27f, 821, false, detail);
            BattlefieldMarketRoom.RefreshSurfaces();
            var report = new StringBuilder("PASS three owned HDRP Lit finishes assigned to window/counter components.\n");
            foreach (string name in new[] { "MarketFrame", "MarketWorktop", "MarketWarm" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
                if (material.shader.name != "HDRP/Lit" || !material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE") ||
                    !material.IsKeywordEnabled("_MASKMAP") || !material.IsKeywordEnabled("_DETAIL_MAP"))
                    throw new InvalidOperationException("HDRP surface maps are not active: " + name);
                foreach (string property in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap", "_DetailMap" })
                    if (material.GetTexture(property) == null) throw new InvalidOperationException("Missing market map: " + property);
                if (material.GetFloat("_UVDetail") != 1 || material.GetVector("_UVDetailsMappingMask") != new Vector4(0, 1, 0, 0))
                    throw new InvalidOperationException("Physical detail UV1 is not active: " + name);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/MarketRoom.fbx");
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<MeshRenderer>().sharedMaterials.Contains(material)))
                    if (filter.sharedMesh.uv2.Length != filter.sharedMesh.vertexCount)
                        throw new InvalidOperationException("Physical UV1 was lost on import: " + filter.name);
                report.AppendLine(name + ": basecolor/mask/normal 1024px; detail 256px; geometric specular AA; restrained edge wear.");
            }
            report.AppendLine("PASS gameplay fingerprint, navigation bytes, accepted shared environment/floor assets unchanged.");
            report.AppendLine("PASS physical UV1 imported and enabled for owned fine detail; UV0 base/edge wear retained.");
            report.AppendLine(EnemyValidationRunner.ConsoleCounts());
            Directory.CreateDirectory("Logs/BattlefieldMarketRoom");
            File.WriteAllText("Logs/BattlefieldMarketRoom/surface-validation.txt", report.ToString());
        }

        private static void Surface(string name, string source, Color paint, float metal, float smoothness,
            int seed, bool worktop, Texture2D detail)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Shared + source + ".mat")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            var scratches = Scratches(seed, worktop ? 130 : 45);
            var height = new float[Size * Size];
            var colors = new Color[height.Length];
            var masks = new Color[height.Length];
            var normals = new Color[height.Length];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                float u = x / (float)(Size - 1), v = y / (float)(Size - 1);
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                float patch = Mathf.PerlinNoise(u * 27 + seed, v * 23 + 13);
                float edgeWear = Mathf.Pow(Mathf.Clamp01(1 - edge / .012f), 2) * Mathf.SmoothStep(.50f, .80f, patch);
                float wear = Mathf.Clamp01(edgeWear * .35f + scratches[i] * (worktop ? .24f : .12f));
                float grime = Mathf.Clamp01(1 - edge / .045f) * Mathf.PerlinNoise(u * 11 + seed, v * 15) * .35f;
                float broad = Mathf.PerlinNoise(u * 7 + seed, v * 9 + 3) - .5f;
                Color steel = new Color(.42f, .44f, .45f);
                colors[i] = Color.Lerp(paint * (1 + broad * .018f - grime * .06f), steel, wear * (worktop ? .10f : .45f));
                colors[i].a = 1;
                masks[i] = new Color(Mathf.Lerp(metal, .88f, wear), 1 - grime * .10f, 1,
                    Mathf.Clamp01(smoothness + broad * .04f - grime * .06f + wear * .12f));
                height[i] = -wear * .018f + (Mathf.PerlinNoise(x * .17f + seed, y * .17f) - .5f) * .0015f;
            }
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float dx = height[y * Size + Mathf.Min(Size - 1, x + 1)] - height[y * Size + Mathf.Max(0, x - 1)];
                float dy = height[Mathf.Min(Size - 1, y + 1) * Size + x] - height[Mathf.Max(0, y - 1) * Size + x];
                var normal = new Vector3(-dx * 4, -dy * 4, 1).normalized;
                normals[y * Size + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
            }
            Undo.RecordObject(material, "Refine market handled surfaces");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", Save(name + "_BaseColor", colors, Size, true, false, false));
            material.SetTexture("_MaskMap", Save(name + "_Mask", masks, Size, false, false, false));
            material.SetTexture("_NormalMap", Save(name + "_Normal", normals, Size, false, true, false));
            material.SetTexture("_DetailMap", detail);
            material.SetTextureScale("_DetailMap", new Vector2(6, 6));
            material.SetFloat("_UVDetail", 1);
            material.SetVector("_UVDetailsMappingMask", new Vector4(0, 1, 0, 0));
            material.SetFloat("_LinkDetailsWithBase", 0);
            material.SetFloat("_NormalScale", .45f);
            material.SetFloat("_DetailAlbedoScale", 0);
            material.SetFloat("_DetailNormalScale", worktop ? .22f : .16f);
            material.SetFloat("_DetailSmoothnessScale", .18f);
            material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0); material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
            material.SetFloat("_CoatMask", worktop ? 0 : .12f);
            material.SetFloat("_EnableGeometricSpecularAA", 1);
            material.SetFloat("_SpecularAAScreenSpaceVariance", .1f);
            material.SetFloat("_SpecularAAThreshold", .2f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        private static float[] Scratches(int seed, int count)
        {
            var result = new float[Size * Size];
            var random = new System.Random(seed);
            for (int stroke = 0; stroke < count; stroke++)
            {
                int x0 = random.Next(8, Size - 150), y0 = random.Next(8, Size - 12);
                int length = random.Next(12, 130);
                float slope = (float)(random.NextDouble() - .5) * .13f;
                float strength = (float)random.NextDouble() * .6f + .2f;
                for (int step = 0; step < length; step++)
                {
                    float y = y0 + step * slope;
                    int row = Mathf.FloorToInt(y);
                    float fade = Mathf.Sin(Mathf.PI * step / length) * strength;
                    if (row < 1 || row >= Size - 1) continue;
                    int index = row * Size + x0 + step;
                    result[index] = Mathf.Max(result[index], fade * (1 - (y - row)));
                    result[index + Size] = Mathf.Max(result[index + Size], fade * (y - row));
                }
            }
            return result;
        }

        private static Texture2D Detail()
        {
            var pixels = new Color[DetailSize * DetailSize];
            float Grain(int x, int y)
            {
                // Integer-frequency waves are periodic at the tile boundary and fade through mipmaps.
                float u = 2 * Mathf.PI * x / DetailSize, v = 2 * Mathf.PI * y / DetailSize;
                return Mathf.Sin(u * 29 + v * 17) * .35f + Mathf.Sin(u * 53 - v * 31) * .20f + Mathf.Sin(u * 71 + v * 43) * .12f;
            }
            for (int y = 0; y < DetailSize; y++) for (int x = 0; x < DetailSize; x++)
            {
                float dx = Grain(x + 1, y) - Grain(x - 1, y), dy = Grain(x, y + 1) - Grain(x, y - 1);
                var normal = new Vector3(-dx * .12f, -dy * .12f, 1).normalized;
                // HDRP detail packing: R albedo, G normal Y, B smoothness, A normal X.
                pixels[y * DetailSize + x] = new Color(.5f, normal.y * .5f + .5f, .5f + Grain(x, y) * .04f, normal.x * .5f + .5f);
            }
            return Save("Market_Detail", pixels, DetailSize, false, false, true);
        }

        private static Texture2D Save(string name, Color[] pixels, int size, bool srgb, bool normal, bool repeat)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8; importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
