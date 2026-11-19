using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    internal static class BattlefieldCornerMaterials
    {
        internal const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/CornerSample";
        private const int Resolution = 512;

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }

        internal static Material Surface(string name, Color color, float metallic, float smoothness, int seed, float weathering = 0)
        {
            var albedo = new Color[Resolution * Resolution];
            var mask = new Color[albedo.Length];
            var normals = new Color[albedo.Length];
            for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    int index = y * Resolution + x;
                    float u = x / (float)Resolution;
                    float v = y / (float)Resolution;
                    float broad = TileNoise(u, v, 5, seed);
                    float grain = Random01(x, y, seed);
                    float scratch = metallic > .1f && Random01(x, y / 32, seed + 11) > .987f ? .06f : 0;
                    float value = Mathf.Clamp01(.84f + broad * .07f + (grain - .5f) * .035f + scratch);
                    float dirt = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .7f, TileNoise(u, v, 3, seed + 101))) * weathering;
                    float runoff = Mathf.Max(0, TileNoise(u, .5f, 22, seed + 71) - .45f) * (1 - v) * weathering;
                    float chips = metallic > .1f ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.61f, .74f, TileNoise(u, v, 27, seed + 7))) * weathering : 0;
                    value *= 1 - dirt * .24f - runoff * .4f;
                    Color baseColor = new Color(value, value, value, 1);
                    Color exposedMetal = Color.Lerp(new Color(.57f, .60f, .59f, 1), new Color(.46f, .27f, .13f, 1), dirt * .3f);
                    albedo[index] = Color.Lerp(baseColor, exposedMetal, chips * .55f);
                    mask[index] = new Color(Mathf.Lerp(metallic, .05f, chips), .95f + broad * .05f - dirt * .12f, 1,
                        Mathf.Clamp01(smoothness + (broad - .5f) * .08f - scratch - dirt * .16f - chips * .12f));
                    float dx = TileNoise(u + 1f / Resolution, v, 48, seed) - TileNoise(u - 1f / Resolution, v, 48, seed);
                    float dy = TileNoise(u, v + 1f / Resolution, 48, seed) - TileNoise(u, v - 1f / Resolution, 48, seed);
                    Vector3 normal = new Vector3(-dx * .25f, -dy * .25f, 1).normalized;
                    normals[index] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
                }
            Texture2D baseMap = SaveTexture(name + "_BaseColor", albedo, TextureImporterType.Default, true);
            Texture2D maskMap = SaveTexture(name + "_Mask", mask, TextureImporterType.Default, false);
            Texture2D normalMap = SaveTexture(name + "_Normal", normals, TextureImporterType.NormalMap, false);
            string materialPath = Folder + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool creating = material == null;
            if (creating) material = NewMaterial(name, color, metallic, smoothness);
            else Undo.RecordObject(material, "Refine energy corner surface maps");
            material.SetTexture("_BaseColorMap", baseMap);
            material.SetTexture("_MaskMap", maskMap);
            material.SetTexture("_NormalMap", normalMap);
            material.SetFloat("_NormalScale", .35f);
            HDMaterial.ValidateMaterial(material);
            if (creating) AssetDatabase.CreateAsset(material, materialPath);
            else { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
            return material;
        }

        internal static Material Plain(string name, Color color, float metallic = 0, float smoothness = .4f, float emission = 0)
        {
            Material material = NewMaterial(name, color, metallic, smoothness);
            if (emission > 0)
            {
                material.SetFloat("_UseEmissiveIntensity", 0);
                material.SetColor("_EmissiveColor", color.linear * emission);
                material.SetFloat("_EmissiveExposureWeight", .1f);
            }
            HDMaterial.ValidateMaterial(material);
            AssetDatabase.CreateAsset(material, Folder + "/Materials/" + name + ".mat");
            return material;
        }

        private static Material NewMaterial(string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("HDRP/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Texture2D SaveTexture(string name, Color[] pixels, TextureImporterType type, bool srgb)
        {
            var temporary = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            try
            {
                temporary.SetPixels(pixels);
                temporary.Apply();
                string path = Folder + "/Textures/" + name + ".png";
                File.WriteAllBytes(path, temporary.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = Resolution;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            finally { Object.DestroyImmediate(temporary); }
        }

        private static float TileNoise(float u, float v, float scale, int seed)
        {
            u = Mathf.Repeat(u, 1);
            v = Mathf.Repeat(v, 1);
            float offset = seed * .173f;
            float a = Mathf.PerlinNoise(u * scale + offset, v * scale + offset);
            float b = Mathf.PerlinNoise((u - 1) * scale + offset, v * scale + offset);
            float c = Mathf.PerlinNoise(u * scale + offset, (v - 1) * scale + offset);
            float d = Mathf.PerlinNoise((u - 1) * scale + offset, (v - 1) * scale + offset);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Random01(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + seed * 144269);
                value = (value ^ value >> 13) * 1274126177;
                return (value ^ value >> 16) / (float)uint.MaxValue;
            }
        }
    }
}
