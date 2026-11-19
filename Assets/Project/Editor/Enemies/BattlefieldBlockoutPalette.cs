using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldBlockoutPalette
    {
        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Match Backing Blocks To Wall Finish")]
        public static void MatchBackingBlocks()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            var backing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Scenes/BattlefieldBlockout/Materials/Architecture.mat");
            var wall = AssetDatabase.LoadAssetAtPath<Material>(BattlefieldCornerMaterials.Folder + "/Materials/CoatedMetal.mat");
            if (backing == null || wall == null) throw new InvalidOperationException("Blockout backing or accepted wall material is missing.");
            var blocks = BattlefieldBlockout.FindInScene<MeshRenderer>(scene)
                .Where(renderer => renderer.sharedMaterials.Contains(backing)).Select(renderer => renderer.name).ToArray();
            if (blocks.Length == 0) throw new InvalidOperationException("No backing blocks use the local architecture material.");
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Undo.RecordObject(backing, "Match backing blocks to wall finish");
            // Keep the backing asset GUID and all scene references; copy the accepted finish only.
            string name = backing.name;
            backing.shader = wall.shader;
            backing.CopyPropertiesFromMaterial(wall);
            backing.name = name;
            // Backing cubes have coarse UVs: use the average wall tint without enlarged wear patterns.
            foreach (string slot in new[] { "_BaseColorMap", "_MaskMap", "_NormalMap", "_HeightMap", "_DetailMap" })
                backing.SetTexture(slot, null);
            Color tint = wall.GetColor("_BaseColor") * .65f;
            tint.a = 1;
            backing.SetColor("_BaseColor", tint);
            backing.SetFloat("_Metallic", wall.GetFloat("_Metallic"));
            backing.SetFloat("_Smoothness", .35f);
            backing.SetFloat("_DisplacementMode", 0);
            HDMaterial.ValidateMaterial(backing);
            EditorUtility.SetDirty(backing);
            AssetDatabase.SaveAssetIfDirty(backing);
            if (BattlefieldCornerArt.GameplayFingerprint(scene) != gameplay)
                throw new InvalidOperationException("Palette change altered gameplay data.");
            BattlefieldEntryArt.Validate();
            Directory.CreateDirectory("Logs/BattlefieldEntryArt");
            File.WriteAllText("Logs/BattlefieldEntryArt/backing-palette.txt", "PASS local Architecture material matches the average accepted wall tint, without coarse-UV wear textures; gameplay and accepted assets unchanged.\n" +
                string.Join("\n", blocks) + "\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }
    }
}
