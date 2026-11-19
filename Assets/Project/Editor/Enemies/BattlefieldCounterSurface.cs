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
    // Offline HDRP coating authoring, bounded to the requested checkout counter.
    public static class BattlefieldCounterSurface
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom";
        private const int Width = 1024;
        private const int Height = 2048;
        private const int DetailSize = 256;

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Refine Checkout Counter Surfaces")]
        public static void Bake()
        {
            BattlefieldBaseline.RequireIdleEditor();
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            var metalDetail = Detail("CounterBrushedDetail", true);
            var paintDetail = Detail("CounterCoatingDetail", false);
            Surface("CounterTopSatin", "MarketWorktop", new Color(.34f, .36f, .375f), .64f, .35f, 0, metalDetail);
            Surface("CounterPaint", "MarketLining", new Color(.55f, .53f, .46f), .08f, .26f, 1, paintDetail);
            Surface("CounterBase", "MarketFrame", new Color(.16f, .19f, .20f), .10f, .19f, 2, paintDetail);
            Surface("CounterAccent", "MarketWarm", new Color(.52f, .29f, .06f), .08f, .19f, 3, paintDetail);
            BattlefieldMarketRoom.RefreshSurfaces();
            var report = new StringBuilder("PASS north checkout counter only: satin top, painted cabinet, protective base.\n");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/MarketRoom.fbx");
            foreach (string name in new[] { "CounterTopSatin", "CounterPaint", "CounterBase", "CounterAccent" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
                if (material.shader.name != "HDRP/Lit" || !material.IsKeywordEnabled("_MASKMAP") ||
                    !material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE") || !material.IsKeywordEnabled("_DETAIL_MAP"))
                    throw new InvalidOperationException("Counter HDRP maps are inactive: " + name);
                foreach (string property in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap", "_DetailMap" })
                {
                    var texture = material.GetTexture(property);
                    if (texture == null) throw new InvalidOperationException("Missing counter map " + property);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                    if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear)
                        throw new InvalidOperationException("Counter texture filtering/mips failed: " + texture.name);
                    if (importer.sRGBTexture != (property == "_BaseColorMap"))
                        throw new InvalidOperationException("Counter texture color space failed: " + texture.name);
                }
                if (material.GetVector("_UVDetailsMappingMask") != new Vector4(0, 1, 0, 0))
                    throw new InvalidOperationException("Counter detail must use physical UV1.");
                var filters = model.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<MeshRenderer>().sharedMaterials.Contains(material)).ToArray();
                if (filters.Length == 0 || filters.Any(f => f.sharedMesh.uv2.Length != f.sharedMesh.vertexCount))
                    throw new InvalidOperationException("Counter finish assignment/UV1 failed: " + name);
                report.AppendLine("PASS " + name + ": 1024x2048 base/mask/normal, 256px periodic detail, physical UV1, specular AA.");
            }
            report.AppendLine("PASS exact gameplay/nav fingerprint and accepted shared environment/floor files unchanged.");
            report.AppendLine(EnemyValidationRunner.ConsoleCounts());
            File.WriteAllText("Logs/BattlefieldMarketRoom/counter-surface-validation.txt", report.ToString());
        }

        private static void Surface(string name, string source, Color paint, float metal, float smoothness, int kind, Texture2D detail)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + source + ".mat")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            var scratches = Scratches(kind == 0 ? 96 : 30, 943 + kind);
            var height = new float[Width * Height];
            var colors = new Color[height.Length];
            var masks = new Color[height.Length];
            var normals = new Color[height.Length];
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                float u = x / (float)(Width - 1), v = y / (float)(Height - 1);
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                float patch = Mathf.PerlinNoise(u * 31 + kind * 53, v * 23 + 19);
                float edgeWear = Mathf.Pow(Mathf.Clamp01(1 - edge / .012f), 2) * Mathf.SmoothStep(.53f, .83f, patch);
                float wear = edgeWear * .24f + scratches[i] * (kind == 0 ? .20f : .11f);
                float broad = Mathf.PerlinNoise(u * 5 + 47, v * 7 + kind * 71) - .5f;
                float grain = Mathf.PerlinNoise(x * (kind == 0 ? .21f : .15f) + 53, y * (kind == 0 ? .025f : .15f) + kind * 17) - .5f;
                float contact = kind == 0 ? Mathf.Max(Oval(u, v, .82f, .35f, .17f, .20f), Oval(u, v, .82f, .72f, .15f, .18f)) : 0;
                float dirt = Mathf.Clamp01(1 - edge / .04f) * patch * .12f;
                if (kind == 2) dirt += Mathf.Clamp01(1 - v / .18f) * (broad + .5f) * .16f;
                colors[i] = Color.Lerp(paint * (1 + broad * .025f + grain * .025f - dirt * .10f + contact * .015f),
                    new Color(.47f, .49f, .50f), wear * (kind == 0 ? .32f : .42f));
                colors[i].a = 1;
                masks[i] = new Color(Mathf.Lerp(metal, .82f, wear), 1 - dirt * .08f, 1,
                    Mathf.Clamp01(smoothness + broad * .055f + grain * .06f + contact * .08f + wear * .09f - dirt * .08f));
                height[i] = grain * (kind == 0 ? .007f : .012f) - scratches[i] * .008f;
            }
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                float dx = height[y * Width + Mathf.Min(Width - 1, x + 1)] - height[y * Width + Mathf.Max(0, x - 1)];
                float dy = height[Mathf.Min(Height - 1, y + 1) * Width + x] - height[Mathf.Max(0, y - 1) * Width + x];
                var n = new Vector3(-dx * 7, -dy * 7, 1).normalized;
                normals[y * Width + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", Save(name + "_BaseColor", colors, Width, Height, true, false, false));
            material.SetTexture("_MaskMap", Save(name + "_Mask", masks, Width, Height, false, false, false));
            material.SetTexture("_NormalMap", Save(name + "_Normal", normals, Width, Height, false, true, false));
            material.SetTexture("_DetailMap", detail);
            material.SetTextureScale("_DetailMap", new Vector2(6, 6));
            material.SetFloat("_UVDetail", 1); material.SetVector("_UVDetailsMappingMask", new Vector4(0, 1, 0, 0));
            material.SetFloat("_LinkDetailsWithBase", 0);
            material.SetFloat("_NormalScale", kind == 0 ? .65f : .5f);
            material.SetFloat("_DetailNormalScale", kind == 0 ? .48f : .32f);
            material.SetFloat("_DetailAlbedoScale", 0); material.SetFloat("_DetailSmoothnessScale", .3f);
            material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 1);
            material.SetFloat("_SmoothnessRemapMin", 0); material.SetFloat("_SmoothnessRemapMax", 1);
            material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
            material.SetFloat("_CoatMask", kind == 1 ? .07f : 0);
            material.SetFloat("_EnableGeometricSpecularAA", 1);
            material.SetFloat("_SpecularAAScreenSpaceVariance", .1f); material.SetFloat("_SpecularAAThreshold", .2f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        private static float Oval(float u, float v, float x, float y, float rx, float ry)
        {
            float distance = Mathf.Pow((u - x) / rx, 2) + Mathf.Pow((v - y) / ry, 2);
            return Mathf.Exp(-distance * 2.5f);
        }

        private static float[] Scratches(int count, int seed)
        {
            var pixels = new float[Width * Height]; var random = new System.Random(seed);
            for (int stroke = 0; stroke < count; stroke++)
            {
                int x = random.Next(24, Width - 24), y = random.Next(24, Height - 180), length = random.Next(12, 150);
                float slope = ((float)random.NextDouble() - .5f) * .6f;
                for (int step = 0; step < length; step++)
                {
                    float column = x + step * slope; int c = Mathf.FloorToInt(column);
                    if (c < 1 || c >= Width - 1) continue;
                    float strength = Mathf.Sin(Mathf.PI * step / length) * .3f;
                    int index = (y + step) * Width + c;
                    pixels[index] = Mathf.Max(pixels[index], strength * (1 - column + c));
                    pixels[index + 1] = Mathf.Max(pixels[index + 1], strength * (column - c));
                }
            }
            return pixels;
        }

        private static Texture2D Detail(string name, bool brushed)
        {
            var random = new System.Random(brushed ? 157 : 277); var noise = new float[DetailSize * DetailSize];
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)random.NextDouble() - .5f;
            var heights = new float[noise.Length]; var pixels = new Color[noise.Length];
            int radius = brushed ? 7 : 1;
            int Index(int x, int y) => ((y + DetailSize) % DetailSize) * DetailSize + (x + DetailSize) % DetailSize;
            for (int y = 0; y < DetailSize; y++) for (int x = 0; x < DetailSize; x++)
            {
                float sum = 0, weights = 0;
                for (int offset = -radius; offset <= radius; offset++)
                {
                    float weight = radius + 1 - Mathf.Abs(offset);
                    sum += noise[Index(x, y + offset)] * weight; weights += weight;
                }
                heights[Index(x, y)] = sum / weights;
            }
            for (int y = 0; y < DetailSize; y++) for (int x = 0; x < DetailSize; x++)
            {
                float dx = heights[Index(x + 1, y)] - heights[Index(x - 1, y)];
                float dy = heights[Index(x, y + 1)] - heights[Index(x, y - 1)];
                var n = new Vector3(-dx * .65f, -dy * .65f, 1).normalized;
                pixels[Index(x, y)] = new Color(.5f, n.y * .5f + .5f, .5f + heights[Index(x, y)] * .08f, n.x * .5f + .5f);
            }
            return Save(name, pixels, DetailSize, DetailSize, false, false, true);
        }

        private static Texture2D Save(string name, Color[] pixels, int width, int height, bool srgb, bool normal, bool repeat)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8; importer.maxTextureSize = Height;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
