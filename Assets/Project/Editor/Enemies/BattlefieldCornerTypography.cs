using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class BattlefieldCornerArt
    {
        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Refine Sample Typography")]
        public static void RefineTypography()
        {
            BattlefieldBaseline.RequireIdleEditor();
            BattlefieldBlockout.RequireBlockout();
            const string fontPath = Folder + "/Fonts/Orbitron SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/Orbitron.ttf");
                if (source == null) throw new InvalidOperationException("Import the licensed Orbitron source font first.");
                font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                font.name = "Orbitron SDF";
                if (!font.TryAddCharacters("0123456789ION% "))
                    throw new InvalidOperationException("Orbitron could not provide all sample glyphs.");
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font, fontPath);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
                AssetDatabase.AddObjectToAsset(font.material, font);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }

            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var label in prefab.GetComponentsInChildren<TextMeshPro>(true))
                {
                    string text = label.text;
                    if (text == "07") label.fontSize = 9;
                    else if (text == "ION // GRID SYSTEMS" || text == "ION")
                    {
                        label.text = "ION";
                        label.fontSize = 3.1f;
                        label.rectTransform.sizeDelta = new Vector2(2.1f, .4f);
                        Vector3 position = label.transform.position;
                        position.y = 4.02f;
                        label.transform.position = position;
                        label.characterSpacing = 14;
                    }
                    else if (text == "LOAD 72%\nSYSTEM ONLINE" || text == "72%")
                    {
                        label.text = "72%";
                        label.fontSize = 2.3f;
                    }
                    else
                    {
                        Object.DestroyImmediate(label.gameObject);
                        continue;
                    }
                    label.font = font;
                    label.fontStyle = FontStyles.Normal;
                    label.enableAutoSizing = false;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.ForceMeshUpdate(true);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            // Let prefab instance synchronization finish before TMP render requests.
            EditorApplication.delayCall += Validate;
        }
    }
}
