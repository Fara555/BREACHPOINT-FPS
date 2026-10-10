using System;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Breachpoint.Editor.Enemies
{
    public static class RiflemanHighCoverAuthoring
    {
        [MenuItem("Breachpoint/Enemies/Animator Authoring/Connect High Cover animations only")]
        public static void Connect()
        {
            PrepareSources(); InspectSources();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyTools.ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var standing = machine.states.First(s => s.state.name == "StandingLocomotion").state;
            var parameters = new[] { ("HighCoverPhase", AnimatorControllerParameterType.Int), ("HighCoverMirror", AnimatorControllerParameterType.Bool), ("HighCoverSide", AnimatorControllerParameterType.Float) };
            foreach (var parameter in parameters)
                if (!controller.parameters.Any(p => p.name == parameter.Item1)) controller.AddParameter(parameter.Item1, parameter.Item2);
            var clips = BuildClips();
            string[] names = { "TakingHighCover", "HighCoverIdle", "LeavingHighCover" };
            for (int i = 0; i < names.Length; i++)
            {
                var state = machine.states.FirstOrDefault(s => s.state.name == names[i]).state;
                if (state != null) continue; // Preserve subsequent Inspector tuning on repeated maintenance.
                state = machine.AddState(names[i], new Vector3(950, i * 100 + 200));
                state.motion = clips[i]; state.tag = "HighCover"; state.writeDefaultValues = false;
                state.mirrorParameter = "HighCoverMirror"; state.mirrorParameterActive = true;
                var enter = machine.AddAnyStateTransition(state);
                enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = .16f;
                enter.canTransitionToSelf = false; enter.interruptionSource = TransitionInterruptionSource.None;
                enter.AddCondition(AnimatorConditionMode.Equals, i + 1, "HighCoverPhase");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHit");
                var exit = state.AddTransition(standing);
                exit.hasExitTime = false; exit.hasFixedDuration = true; exit.duration = .2f;
                exit.AddCondition(AnimatorConditionMode.Equals, 0, "HighCoverPhase");
                exit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            }
            // Humanoid mirroring swaps weapon hands. Mirror only the cover body/feet,
            // retaining the authored right-handed arms and the calibrated rifle attachment.
            for (int i = 0; i < names.Length; i++)
            {
                var state = machine.states.First(s => s.state.name == names[i]).state;
                var right = BuildRightClip(clips[i], names[i]);
                var tree = state.motion as BlendTree;
                if (tree == null)
                {
                    tree = new BlendTree { name = names[i] + " Side", hideFlags = HideFlags.HideInHierarchy };
                    AssetDatabase.AddObjectToAsset(tree, controller);
                }
                tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "HighCoverSide";
                tree.useAutomaticThresholds = false;
                tree.children = new[] { new ChildMotion { motion = clips[i], threshold = 0f, timeScale = 1f }, new ChildMotion { motion = right, threshold = 1f, timeScale = 1f } };
                state.motion = tree; state.mirrorParameterActive = false; state.mirror = false;
                EditorUtility.SetDirty(tree); EditorUtility.SetDirty(state);
            }
            foreach (var enter in machine.anyStateTransitions.Where(t => t.destinationState != null && names.Contains(t.destinationState.name)))
                if (!enter.conditions.Any(c => c.parameter == "IsHit")) enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHit");
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
            var prefab = PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            try
            {
                var data = new SerializedObject(prefab.GetComponent<Breachpoint.Gameplay.AI.EnemyAnimationBridge>());
                data.FindProperty("_config.<HighCoverTakeDuration>k__BackingField").floatValue = clips[0].length;
                data.FindProperty("_config.<HighCoverLeaveDuration>k__BackingField").floatValue = clips[2].length;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab,EnemyValidationRunner.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EnemyTools.Audit();
        }

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Calibrate High Cover contact and footsteps")]
        public static void CalibrateContact() => EnemyValidationRunner.ValidateStage(184);

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Connect calibrated High Cover contact")]
        public static void ConnectContact()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before contact wiring.");
            var profile=AssetDatabase.LoadAssetAtPath<Breachpoint.Gameplay.AI.EnemyHighCoverAnimationProfile>(EnemyValidationRunner.HighProfilePath);
            if(profile==null) throw new InvalidOperationException("Calibrate High Cover motion/contact before wiring.");
            var prefab=PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            try
            {
                var contact=prefab.GetComponent<Breachpoint.Gameplay.AI.EnemyHighCoverPresentation>();
                if(contact==null) contact=prefab.AddComponent<Breachpoint.Gameplay.AI.EnemyHighCoverPresentation>();
                var data=new SerializedObject(contact); data.FindProperty("_profile").objectReferenceValue=profile; data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab,EnemyValidationRunner.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static AnimationClip[] BuildClips()
        {
            string folder = EnemyAnimationAssetPaths.Derived + "/HighCover";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(EnemyAnimationAssetPaths.Derived,"HighCover");
            var taking = SourceClip(SourceNames[0]); var idle = SourceClip(SourceNames[1]); var leaving = SourceClip(SourceNames[2]);
            Quaternion Rotation(AnimationClip clip,float time)
            {
                var curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("RootQ",StringComparison.Ordinal))
                    .OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length-1])).Select(b => AnimationUtility.GetEditorCurve(clip,b)).ToArray();
                return new Quaternion(curves[0].Evaluate(time),curves[1].Evaluate(time),curves[2].Evaluate(time),curves[3].Evaluate(time)).normalized;
            }
            // Keep the authored half-turn and weight shift. Align the three source
            // headings to one cover frame and remove travel already owned by NavMesh.
            float takingYaw = Rotation(taking,0).eulerAngles.y;
            float coverYaw = Mathf.DeltaAngle(takingYaw,Rotation(taking,taking.length).eulerAngles.y);
            var corrections = new[] { -takingYaw, coverYaw - Rotation(idle,0).eulerAngles.y, coverYaw - Rotation(leaving,0).eulerAngles.y };
            var result = new AnimationClip[3];
            for(int i=0;i<3;i++)
            {
                string path = folder + "/" + SourceNames[i].Replace(" ","") + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(clip != null) { result[i]=clip; continue; }
                var source = SourceClip(SourceNames[i]); clip = Object.Instantiate(source); clip.name = SourceNames[i] + " In Place";
                foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName=="RootT.x" || b.propertyName=="RootT.z"))
                    AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,clip.length,0));
                var bindings = AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("RootQ",StringComparison.Ordinal))
                    .OrderBy(b=>"xyzw".IndexOf(b.propertyName[b.propertyName.Length-1])).ToArray();
                var rotations = new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                Quaternion previous = Quaternion.identity;
                for(int sample=0;sample<=120;sample++)
                {
                    float time=clip.length*sample/120f;
                    Quaternion pose=Quaternion.Euler(0,corrections[i],0)*Rotation(source,time);
                    if(sample>0 && Quaternion.Dot(previous,pose)<0) pose=new Quaternion(-pose.x,-pose.y,-pose.z,-pose.w);
                    for(int component=0;component<4;component++) rotations[component].AddKey(time,pose[component]);
                    previous=pose;
                }
                for(int component=0;component<4;component++) AnimationUtility.SetEditorCurve(clip,bindings[component],rotations[component]);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime=i==1; settings.loopBlend=i==1;
                settings.keepOriginalOrientation=true; settings.loopBlendOrientation=true;
                settings.keepOriginalPositionXZ=true; settings.loopBlendPositionXZ=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                AssetDatabase.CreateAsset(clip,path); result[i]=clip;
            }
            return result;
        }
        internal static AnimationClip BuildRightClip(AnimationClip source, string name, bool refresh = false)
        {
            string path = EnemyAnimationAssetPaths.Derived + "/HighCover/" + name + "Right.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null && !refresh) return existing;
            var clip = Object.Instantiate(source); clip.name = name + "Right";
            var bindings = AnimationUtility.GetCurveBindings(source);
            foreach (var binding in bindings)
            {
                string property = binding.propertyName;
                string from = property; float sign = 1f;
                bool foot = property.StartsWith("LeftFoot", StringComparison.Ordinal) || property.StartsWith("RightFoot", StringComparison.Ordinal);
                bool leg = property.Contains(" Leg ") || property.Contains(" Foot ") || property.Contains(" Toes ");
                if (foot || leg) from = property.StartsWith("Left", StringComparison.Ordinal) ? "Right" + property.Substring(4) : "Left" + property.Substring(5);
                if (foot && (property.EndsWith("T.x", StringComparison.Ordinal) || property.EndsWith("Q.y", StringComparison.Ordinal) || property.EndsWith("Q.z", StringComparison.Ordinal))) sign = -1f;
                if (property == "RootT.x" || property == "RootQ.y" || property == "RootQ.z" || property.EndsWith("Left-Right", StringComparison.Ordinal)) sign = -1f;
                var sourceBinding = bindings.FirstOrDefault(b => b.propertyName == from);
                var curve = AnimationUtility.GetEditorCurve(source, sourceBinding);
                if (curve == null) continue;
                var keys = curve.keys;
                for (int k = 0; k < keys.Length; k++) { keys[k].value *= sign; keys[k].inTangent *= sign; keys[k].outTangent *= sign; }
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode });
            }
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
            return clip;
        }
        internal const string SourceFolder = EnemyAnimationAssetPaths.Source + "/Combat/HighCover";
        internal static readonly string[] SourceNames = { "Taking High Cover", "High Cover Idle", "Leaving High Cover" };
        internal static AnimationClip SourceClip(string name) => AssetDatabase.LoadAllAssetsAtPath(SourceFolder + "/" + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        private static void PrepareSources()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before importing source clips.");
            var referencePath = AssetDatabase.GUIDToAssetPath("1382b20499fb1894ea326a8e05059320");
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(referencePath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Missing or invalid authored Adam animation avatar.");
            foreach (string name in SourceNames)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(SourceFolder + "/" + name + ".fbx");
                if (importer.animationType != ModelImporterAnimationType.Human || importer.sourceAvatar != avatar)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                    importer.SaveAndReimport();
                }
            }
        }
        [MenuItem("Breachpoint/Enemies/Animator Authoring/Inspect High Cover sources")]
        public static void InspectSources()
        {
            Directory.CreateDirectory(EnemyTools.Evidence);
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            foreach (string name in SourceNames)
            {
                var clip = SourceClip(name);
                report.AppendLine(name + " | human=" + clip.humanMotion + " | duration=" + clip.length + " | loop=" + clip.isLooping + " | average=" + clip.averageSpeed);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("Root",StringComparison.Ordinal)))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip,binding);
                    report.AppendLine(binding.propertyName + " = " + string.Join(",", Enumerable.Range(0,5).Select(i=>curve.Evaluate(clip.length*i/4f).ToString("F3",System.Globalization.CultureInfo.InvariantCulture))));
                }
            }
            File.WriteAllText(EnemyTools.Evidence+"/high-cover-sources.txt",report.ToString());
            EnemyTools.Audit();
        }
    }
}
