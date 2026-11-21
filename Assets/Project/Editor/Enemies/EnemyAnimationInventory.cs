using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyTools
    {
        private const string InventoryFolder = "Docs/AI";

        public static void InventoryEnemyAnimations()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before asset inventory.");
            var references = new Dictionary<AnimationClip,List<string>>();
            var controllerText = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController",new[] { EnemyAnimationAssetPaths.AdamRoot,"Assets/Project/Enemies" }))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var layer in controller.layers) DescribeMachine(layer.stateMachine,AssetDatabase.GetAssetPath(controller)+":"+layer.name,controllerText,references);
            }
            var clips = AssetDatabase.FindAssets("t:AnimationClip",new[] { EnemyAnimationAssetPaths.AdamRoot,"Assets/Project/Enemies" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).Distinct().OrderBy(AssetDatabase.GetAssetPath).ThenBy(c=>c.name).ToArray();
            var toolFiles = Directory.GetFiles("Assets/Project/Editor/Enemies","*.cs").Where(p=>!p.EndsWith("EnemyAnimationInventory.cs",StringComparison.Ordinal)).ToDictionary(p=>p,p=>File.ReadAllText(p));
            var assetFiles = AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/Project/",StringComparison.Ordinal) &&
                new[] { ".prefab",".unity",".asset",".overrideController",".playable" }.Contains(Path.GetExtension(p))).ToArray();
            var directDependencies = assetFiles.ToDictionary(p=>p,p=>AssetDatabase.GetDependencies(p,false));
            var report = new StringBuilder("# Enemy animation inventory\n\nGenerated from the loaded Unity assets, controller topology, direct serialized dependencies and explicit authoring/review inputs. Embedded preview clips are excluded.\n\n");
            var csv = new StringBuilder("Path,Clip,Classification,Needed,References,ContentFingerprint\n");
            var unused = new List<AnimationClip>();
            var signatures = new Dictionary<string,List<AnimationClip>>();
            var statuses = new Dictionary<AnimationClip,string>();
            foreach (var clip in clips)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                references.TryGetValue(clip,out var owners); owners = owners == null ? new List<string>() : new List<string>(owners);
                var direct = directDependencies.Where(p=>p.Value.Contains(path)).Select(p=>p.Key).ToArray();
                // A model's mesh/avatar references do not imply that every embedded clip is played.
                if (Path.GetExtension(path)==".anim") owners.AddRange(direct);
                var tools = toolFiles.Where(p=>p.Value.IndexOf("\""+clip.name+"\"",StringComparison.OrdinalIgnoreCase)>=0).Select(p=>Path.GetFileName(p.Key)).ToList();
                string derivedName = clip.name.Replace(" ","") + (clip.name.IndexOf("turn",StringComparison.OrdinalIgnoreCase)>=0 ? "InPlace.anim" :
                    clip.name.StartsWith("Fire",StringComparison.OrdinalIgnoreCase) ? "Recoil.anim" : ".anim");
                string[] candidates = { EnemyAnimationAssetPaths.DerivedClip(derivedName),EnemyAnimationAssetPaths.Config+"/"+derivedName };
                bool derivedSource = Path.GetExtension(path)==".fbx" && candidates.Any(p=>AssetDatabase.LoadAssetAtPath<AnimationClip>(p)!=null);
                if (clip.name=="Reload" && Path.GetExtension(path)==".fbx") derivedSource = true;
                bool used = owners.Count>0;
                string status = used ? Path.GetExtension(path)==".anim" && path.Contains("/Adam/") ? "USED; DERIVED / RUNTIME REPRESENTATION" : "USED" :
                    derivedSource || tools.Count>0 ? "SOURCE" : "UNUSED";
                if (derivedSource) owners.Add("Authoring input for production derived clip/turn profile");
                if (tools.Count>0) owners.Add("Explicit tool input: "+string.Join("; ",tools));
                bool needed = status!="UNUSED";
                if (!needed) unused.Add(clip);
                statuses[clip]=status;
                string signature = ClipFingerprint(clip);
                if (!signatures.TryGetValue(signature,out var group)) signatures[signature]=group=new List<AnimationClip>();
                group.Add(clip);
                csv.AppendLine(string.Join(",",Csv(path),Csv(clip.name),Csv(status),needed,Csv(string.Join("; ",owners)),signature));
            }
            report.AppendLine($"Total: {clips.Length} clips; used: {statuses.Count(p=>p.Value.StartsWith("USED"))}; source/tool inputs: {statuses.Count(p=>p.Value=="SOURCE")}; unused: {unused.Count}.\n");
            report.AppendLine("## Enemy animations currently NOT USED\n");
            foreach (var clip in unused) report.AppendLine("- `"+clip.name+"` — `"+AssetDatabase.GetAssetPath(clip)+"`");
            if (unused.Count==0) report.AppendLine("None.");
            report.AppendLine("\n## Duplicate content candidates\n\nSignatures include every float/object curve, event, clip settings, wraps and duration; filenames are excluded. A match is not permission to remove a required source or a clip with a live consumer.\n");
            foreach (var group in signatures.Values.Where(g=>g.Count>1))
            {
                report.AppendLine("- Identical represented content:");
                foreach (var clip in group) report.AppendLine("  - `"+AssetDatabase.GetAssetPath(clip)+"` / `"+clip.name+"` — "+statuses[clip]);
            }
            if (!signatures.Values.Any(g=>g.Count>1)) report.AppendLine("No matching clip-content signatures.");
            report.AppendLine("\n## Config contents\n");
            foreach (var path in Directory.GetFiles(EnemyAnimationAssetPaths.Config).Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal))) report.AppendLine("- `"+Path.GetFileName(path)+"` — "+AssetDatabase.GetMainAssetTypeAtPath(path.Replace('\\','/'))?.Name);
            report.AppendLine("\nFull per-clip paths, classifications, references and state/tree owners: `EnemyAnimationInventory.csv`. Source clips used to derive Raise, Lower, recoil, reload and turn profiles are retained intentionally. Legacy generic enemy clips with live controller consumers are retained for original gameplay validation.");
            Directory.CreateDirectory(InventoryFolder);
            File.WriteAllText(InventoryFolder+"/EnemyAnimationInventory.md",report.ToString());
            File.WriteAllText(InventoryFolder+"/EnemyAnimationInventory.csv",csv.ToString());
        }

        private static string ClipFingerprint(AnimationClip clip)
        {
            var text = new StringBuilder();
            void Number(float value) => text.Append(value.ToString("R",CultureInfo.InvariantCulture)).Append('|');
            Number(clip.length); text.Append(clip.wrapMode).Append('|').Append(EditorJsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip)));
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).OrderBy(b=>b.path).ThenBy(b=>b.type.FullName).ThenBy(b=>b.propertyName))
            {
                text.Append(binding.path).Append('|').Append(binding.type.FullName).Append('|').Append(binding.propertyName).Append('|');
                var curve = AnimationUtility.GetEditorCurve(clip,binding); text.Append(curve.preWrapMode).Append('|').Append(curve.postWrapMode).Append('|');
                foreach(var key in curve.keys) { Number(key.time); Number(key.value); Number(key.inTangent); Number(key.outTangent); Number(key.inWeight); Number(key.outWeight); text.Append(key.weightedMode).Append('|'); }
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip).OrderBy(b=>b.path).ThenBy(b=>b.propertyName))
            {
                text.Append(binding.path).Append('|').Append(binding.propertyName);
                foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding)) { Number(key.time); text.Append(GlobalObjectId.GetGlobalObjectIdSlow(key.value)).Append('|'); }
            }
            foreach (var e in AnimationUtility.GetAnimationEvents(clip)) { Number(e.time); Number(e.floatParameter); text.Append(e.functionName).Append('|').Append(e.stringParameter).Append('|').Append(e.intParameter).Append('|').Append(GlobalObjectId.GetGlobalObjectIdSlow(e.objectReferenceParameter)); }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
        }

    }
}
