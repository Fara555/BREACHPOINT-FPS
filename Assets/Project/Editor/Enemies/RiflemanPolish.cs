using System;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    internal static class RiflemanPolish
    {
        internal const string Evidence = "Docs/AI/RiflemanPolish";
        [MenuItem("Breachpoint/Enemies/AI Test / Tactical Debug/Audit source animation speeds")]
        internal static void AuditSpeed()
        {
            Directory.CreateDirectory(Evidence);
            var report = new StringBuilder("Source,Duration,Loop,AverageSpeed,AverageAngularSpeed\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { RiflemanRework.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                    report.AppendLine(FormattableString.Invariant($"\"{path}\",{clip.length},{clip.isLooping},\"{clip.averageSpeed}\",{clip.averageAngularSpeed}"));
            }
            File.WriteAllText(Evidence + "/source-speeds.csv", report.ToString());
            var turns = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { RiflemanRework.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("turn", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                turns.AppendLine(path + " | bakedOrientation=" + settings.loopBlendOrientation + " original=" + settings.keepOriginalOrientation);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.Contains("RootQ")))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    turns.AppendLine(binding.propertyName + " | " + string.Join(",", Enumerable.Range(0, 5).Select(i => curve.Evaluate(clip.length * i / 4f).ToString("F3"))));
                }
            }
            File.WriteAllText(Evidence + "/turn-root-audit.txt", turns.ToString());
            var reload = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { RiflemanRework.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("Reload crouch.fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                reload.AppendLine("Start=" + settings.startTime + " End=" + settings.stopTime);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.Contains("RootT.y") || b.propertyName.Contains("Spine Front")))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    reload.AppendLine(binding.propertyName + " | " + string.Join(",", Enumerable.Range(0, 15).Select(i => curve.Evaluate(clip.length * i / 14f).ToString("F3"))));
                }
            }
            File.WriteAllText(Evidence + "/reload-source-audit.txt", reload.ToString());
            File.WriteAllText(Evidence + "/status.txt", AdamPresentationIntegration.ConsoleCounts() + "\n" + DateTime.UtcNow.ToString("O"));
        }

    }
}
