using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    // Offline PBR authoring: sparse paint abrasion and scuffs, with no changes to shared finishes.
    public static class MarketFacadeWeathering
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketFacade";
        private const float TileMeters = 8;

        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Wait for an idle Editor.");
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            BakeFinish("FacadeEnamel", new Color(.52f, .54f, .51f), .03f, .26f, 2048, 617);
            BakeFinish("FacadeGraphite", new Color(.26f, .32f, .33f), .18f, .29f, 1024, 887);
            BakeFinish("FacadeTrim", new Color(.18f, .23f, .25f), .30f, .32f, 1024, 1093);
            BakeFinish("FacadeWarm", new Color(.56f, .31f, .14f), .20f, .28f, 1024, 1249);
            Detail();
            foreach (string name in new[] { "FacadeEnamel", "FacadeGraphite", "FacadeTrim", "FacadeWarm" })
                Apply(AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat"), name);
            MarketFacadeAuthoring.Validate(); MarketFacadeAuthoring.Capture();
        }

        internal static void Apply(Material material, string name)
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + "_WornBase.png");
            if (albedo == null) return;
            material.SetTexture("_BaseColorMap", albedo);
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + "_WornMask.png"));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + "_WornNormal.png"));
            material.SetTextureScale("_BaseColorMap", Vector2.one / TileMeters);
            material.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/Facade_MicroDetail.png"));
            material.SetTextureScale("_DetailMap", new Vector2(2, 2));
            material.SetFloat("_UVDetail", 1); material.SetVector("_UVDetailsMappingMask", new Vector4(0, 1, 0, 0));
            material.SetFloat("_LinkDetailsWithBase", 0); material.SetFloat("_DetailAlbedoScale", 0);
            material.SetFloat("_DetailNormalScale", .15f); material.SetFloat("_DetailSmoothnessScale", .16f);
            material.SetFloat("_NormalScale", .65f);
            HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void BakeFinish(string name, Color paint, float metallic, float smoothness, int size, int seed)
        {
            var random = new System.Random(seed);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var scratches = new float[size * size]; var chips = new float[scratches.Length];
            // Positions are spread over an eight-meter tile: no one-meter repeated scratch pattern.
            for (int i = 0; i < 62; i++)
            {
                float u = Range(0, 1), v = Range(0, 1), length = Range(.035f, .19f) / TileMeters;
                float angle = Range(-Mathf.PI, Mathf.PI);
                float radius = Range(.0015f, .004f) / TileMeters;
                Stamp(scratches, size, u, v, u + Mathf.Cos(angle) * length, v + Mathf.Sin(angle) * length, radius, Range(.3f, .8f));
            }
            for (int i = 0; i < 38; i++)
            {
                float u = Range(0, 1), v = Range(0, 1);
                Stamp(chips, size, u, v, u + Range(-.010f, .010f) / TileMeters, v + Range(-.010f, .010f) / TileMeters, Range(.003f, .009f) / TileMeters, Range(.4f, .85f));
            }
            var heights = new float[scratches.Length]; var colors = new Color[heights.Length]; var masks = new Color[heights.Length]; var normals = new Color[heights.Length];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int i = y * size + x; float u = x / (float)size, v = y / (float)size;
                float broad = Noise(u, v, 17, seed * .13f) - .5f;
                float rubbed = Mathf.Pow(Noise(u, v, 89, seed * .23f), 7);
                float damage = Mathf.Clamp01(scratches[i] * .65f + chips[i]);
                Color undercoat = new Color(.22f, .245f, .25f);
                colors[i] = Color.Lerp(paint * (1 + broad * .025f - rubbed * .09f), undercoat, damage * .65f);
                colors[i].a = 1;
                masks[i] = new Color(Mathf.Lerp(metallic, .58f, damage), 1 - chips[i] * .035f, 1,
                    Mathf.Clamp01(smoothness + broad * .07f + rubbed * .15f - damage * .08f));
                heights[i] = -scratches[i] * .00012f - chips[i] * .00018f + rubbed * .000015f;
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                var n = new Vector3(-dx * size / (2 * TileMeters), -dy * size / (2 * TileMeters), 1).normalized;
                normals[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            Save(name + "_WornBase", colors, size, true, false);
            Save(name + "_WornMask", masks, size, false, false);
            Save(name + "_WornNormal", normals, size, false, true);
        }

        private static float Noise(float u, float v, float frequency, float seed)
        {
            float a = Mathf.Lerp(Mathf.PerlinNoise(seed + u * frequency, seed + v * frequency), Mathf.PerlinNoise(seed + (u - 1) * frequency, seed + v * frequency), u);
            float b = Mathf.Lerp(Mathf.PerlinNoise(seed + u * frequency, seed + (v - 1) * frequency), Mathf.PerlinNoise(seed + (u - 1) * frequency, seed + (v - 1) * frequency), u);
            return Mathf.Lerp(a, b, v);
        }

        private static void Stamp(float[] target, int size, float u0, float v0, float u1, float v1, float radius, float strength)
        {
            var a = new Vector2(u0 * size, v0 * size); var b = new Vector2(u1 * size, v1 * size);
            float r = Mathf.Max(.65f, radius * size); Vector2 direction = b - a; float lengthSquared = direction.sqrMagnitude;
            for (int y = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1); y <= Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1); y++)
                for (int x = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1); x <= Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1); x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float t = lengthSquared < .00001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, direction) / lengthSquared);
                    float coverage = Mathf.Clamp01(r + .5f - Vector2.Distance(p, a + t * direction));
                    int index = ((y % size + size) % size) * size + (x % size + size) % size;
                    target[index] = Mathf.Max(target[index], coverage * strength);
                }
        }

        private static void Detail()
        {
            const int size = 256; var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                uint hash = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
                float grain = (hash % 1024) / 1023f - .5f;
                pixels[y * size + x] = new Color(.5f, .5f + grain * .045f, .5f + grain * .07f, .5f + grain * .045f);
            }
            Save("Facade_MicroDetail", pixels, size, false, false);
        }

        private static void Save(string name, Color[] pixels, int size, bool srgb, bool normal)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }
    }
}
