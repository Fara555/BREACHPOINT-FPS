using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class RiflemanHighCoverExitAuthoring
    {
        private const int ExitPoseSampleCount = 120;
        internal const float ExitPlaybackDuration = .80f;
        private static readonly AnimationCurve _exitTiming = CreateExitTiming();

        private static AnimationCurve CreateExitTiming()
        {
            // Give the loaded step and torso turn more time, without stretching
            // the planted finish or making the initial response sluggish.
            var curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.30f, .20f),
                new Keyframe(.70f, .70f), new Keyframe(.85f, .85f),
                new Keyframe(.95f, .95f), new Keyframe(1, 1));
            for (int key = 0; key < curve.length; key++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
            }
            return curve;
        }

        internal static float ExitPlaybackProgress(float sourceProgress) => _exitTiming.Evaluate(Mathf.Clamp01(sourceProgress));

        internal static float ExitSourceProgress(float playbackProgress)
        {
            float lower = 0, upper = 1;
            for (int iteration = 0; iteration < 24; iteration++)
            {
                float middle = (lower + upper) * .5f;
                if (ExitPlaybackProgress(middle) < playbackProgress) lower = middle;
                else upper = middle;
            }
            return (lower + upper) * .5f;
        }

        private static AnimationCurve RetimeExitCurve(AnimationCurve source, float sourceDuration)
        {
            // Resample only the playable range: imported curves may have keys
            // beyond their trimmed clip, which must not collapse onto its end.
            var curve = new AnimationCurve { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
            for (int sample = 0; sample <= ExitPoseSampleCount; sample++)
            {
                float n = sample / (float)ExitPoseSampleCount;
                curve.AddKey(ExitPlaybackProgress(n) * ExitPlaybackDuration, source.Evaluate(n * sourceDuration));
            }
            for (int key = 0; key < curve.length; key++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
            }
            return curve;
        }

        private static void RetimeExit(AnimationClip clip, float sourceDuration)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, RetimeExitCurve(AnimationUtility.GetEditorCurve(clip, binding), sourceDuration));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = ExitPlaybackDuration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
        }

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Apply High Cover exit duration only")]
        public static void ApplyExitDuration()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before changing exit duration.");
            var clips = new[] { "", "Right" }.Select(suffix => AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyAnimationAssetPaths.Derived + "/HighCover/LeavingHighCover" + suffix + ".anim")).ToArray();
            if (clips.Any(clip => clip == null || clip.length <= 0f)) throw new InvalidOperationException("Both valid High Cover exit clips are required before changing their duration.");
            foreach (var clip in clips)
            {
                if (Mathf.Approximately(clip.length, ExitPlaybackDuration)) continue;
                float scale = ExitPlaybackDuration / clip.length;
                // Uniform time scaling preserves the baked pose, floor contact
                // and normalized trajectory profile without recalibration.
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    var keys = curve.keys;
                    for (int key = 0; key < keys.Length; key++)
                    {
                        keys[key].time *= scale;
                        keys[key].inTangent /= scale; keys[key].outTangent /= scale;
                    }
                    curve.keys = keys; AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = ExitPlaybackDuration;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            }
            SaveExitDuration();
        }

        private static void SaveExitDuration()
        {
            var prefab = PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            try
            {
                var data = new SerializedObject(prefab.GetComponent<Breachpoint.Gameplay.AI.EnemyAnimationBridge>());
                var duration = data.FindProperty("_config.<HighCoverLeaveDuration>k__BackingField");
                if (Mathf.Approximately(duration.floatValue, ExitPlaybackDuration)) return;
                duration.floatValue = ExitPlaybackDuration;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, EnemyValidationRunner.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        private const float ExitArmsBlendStart = .1f;
        private const float ExitArmsBlendEnd = .8f;
        private const float ExitTorsoBlendStart = .5f;
        private const float ExitStanceBlendStart = .2f;
        private const float ExitCombatPoseReady = .95f;

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Polish High Cover exit only")]
        public static void PolishExit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before exit authoring.");
            string folder = EnemyAnimationAssetPaths.Derived + "/HighCover/";
            var destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "LeavingHighCover.anim");
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "HighCoverIdle.anim");
            if (destination == null || idle == null) throw new InvalidOperationException("Connect the High Cover clips before polishing their exit.");
            var aim = EnemyValidationRunner.SourceClip("idle aiming");
            var source = RiflemanHighCoverAuthoring.SourceClip("Leaving High Cover");
            var taking = RiflemanHighCoverAuthoring.SourceClip("Taking High Cover");
            var clip = Object.Instantiate(source);
            try
            {
                clip.name = "LeavingHighCover";
                var bindings = AnimationUtility.GetCurveBindings(clip);
                Quaternion Rotation(AnimationClip pose, float time)
                {
                    var values = bindings.Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal))
                        .OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1]))
                        .Select(b => AnimationUtility.GetEditorCurve(pose, b).Evaluate(time)).ToArray();
                    return new Quaternion(values[0], values[1], values[2], values[3]).normalized;
                }
                float coverYaw = Mathf.DeltaAngle(Rotation(taking, 0).eulerAngles.y, Rotation(taking, taking.length).eulerAngles.y);
                Quaternion alignment = Quaternion.Euler(0, coverYaw - Rotation(source, 0).eulerAngles.y, 0);
                // Retain the authored step/half-turn. Match its late torso and rifle
                // to the existing combat pose instead of the source gun-up gesture.
                foreach (var binding in bindings)
                {
                    string property = binding.propertyName;
                    if (property == "RootT.x" || property == "RootT.z")
                    { AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, clip.length, 0)); continue; }
                    bool arms = property.Contains(" Shoulder ") || property.Contains(" Arm ") || property.Contains(" Forearm ") || property.Contains(" Hand ") || property.StartsWith("LeftHand", StringComparison.Ordinal) || property.StartsWith("RightHand", StringComparison.Ordinal);
                    bool torso = property.StartsWith("Spine", StringComparison.Ordinal) || property.StartsWith("Chest", StringComparison.Ordinal) || property.StartsWith("UpperChest", StringComparison.Ordinal) || property.StartsWith("Neck", StringComparison.Ordinal) || property.StartsWith("Head", StringComparison.Ordinal);
                    if (!arms && !torso) continue;
                    var first = AnimationUtility.GetEditorCurve(idle, binding);
                    var last = AnimationUtility.GetEditorCurve(aim, binding);
                    var authored = AnimationUtility.GetEditorCurve(source, binding);
                    if (first == null || last == null || authored == null) continue;
                    var curve = new AnimationCurve();
                    for (int sample = 0; sample <= ExitPoseSampleCount; sample++)
                    {
                        float n = sample / (float)ExitPoseSampleCount;
                        float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(arms ? ExitArmsBlendStart : ExitTorsoBlendStart, arms ? ExitArmsBlendEnd : ExitCombatPoseReady, n));
                        curve.AddKey(clip.length * n, Mathf.Lerp(arms ? first.Evaluate(0) : authored.Evaluate(clip.length * n), last.Evaluate(0), weight));
                    }
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                var rotations = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                Quaternion targetBody = Rotation(aim, 0);
                Quaternion bodyCorrection = targetBody * Quaternion.Inverse(alignment * Rotation(source, clip.length * ExitCombatPoseReady));
                Quaternion previous = Quaternion.identity;
                for (int sample = 0; sample <= ExitPoseSampleCount; sample++)
                {
                    float n = sample / (float)ExitPoseSampleCount;
                    float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ExitStanceBlendStart, ExitCombatPoseReady, n));
                    Quaternion rotation = n >= ExitCombatPoseReady ? targetBody : Quaternion.Slerp(Quaternion.identity, bodyCorrection, settle) * alignment * Rotation(source, clip.length * n);
                    if (sample > 0 && Quaternion.Dot(previous, rotation) < 0) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    for (int component = 0; component < 4; component++) rotations[component].AddKey(clip.length * n, rotation[component]);
                    previous = rotation;
                }
                foreach (var binding in bindings.Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal)))
                    AnimationUtility.SetEditorCurve(clip, binding, rotations["xyzw".IndexOf(binding.propertyName[binding.propertyName.Length - 1])]);
                MatchExitStance(clip, source, aim, false);
                var settings = AnimationUtility.GetAnimationClipSettings(destination);
                settings.startTime = 0; settings.stopTime = source.length;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.CopySerialized(clip, destination); EditorUtility.SetDirty(destination); AssetDatabase.SaveAssetIfDirty(destination);
                var right = RiflemanHighCoverAuthoring.BuildRightClip(destination, "LeavingHighCover", true);
                MatchRightExitEnding(right, aim, source, alignment);
                MatchExitStance(right, source, aim, true);
                EditorUtility.SetDirty(right); AssetDatabase.SaveAssetIfDirty(right);
                RetimeExit(destination, source.length);
                RetimeExit(right, source.length);
                RiflemanHighCoverHeightAuthoring.Stabilize();
                SaveExitDuration();
                Directory.CreateDirectory(EnemyTools.Evidence);
                File.WriteAllText(EnemyTools.Evidence + "/high-exit-authoring.txt", DateTime.UtcNow.ToString("O") + "\nUpdated LeavingHighCover and LeavingHighCoverRight in place; other clips and controller preserved. Exit duration=0.80s with synchronized turn/step timing.\n" + EnemyValidationRunner.ConsoleCounts() + "\n" + EnemyValidationRunner.ConsoleDetails());
            }
            finally { Object.DestroyImmediate(clip); }
        }
        private static void MatchRightExitEnding(AnimationClip right, AnimationClip aim, AnimationClip source, Quaternion alignment)
        {
            var bindings = AnimationUtility.GetCurveBindings(right);
            var rotationBindings = bindings.Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal))
                .OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1])).ToArray();
            var authoredRotation = rotationBindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
            var endRotation = rotationBindings.Select(b => AnimationUtility.GetEditorCurve(aim, b).Evaluate(0)).ToArray();
            var target = new Quaternion(endRotation[0], endRotation[1], endRotation[2], endRotation[3]).normalized;
            var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            float endTime = right.length * ExitCombatPoseReady;
            Quaternion authoredEnd = alignment * new Quaternion(authoredRotation[0].Evaluate(endTime), authoredRotation[1].Evaluate(endTime), authoredRotation[2].Evaluate(endTime), authoredRotation[3].Evaluate(endTime)).normalized;
            authoredEnd = new Quaternion(authoredEnd.x, -authoredEnd.y, -authoredEnd.z, authoredEnd.w);
            Quaternion correction = target * Quaternion.Inverse(authoredEnd);
            Quaternion previous = Quaternion.identity;
            for (int sample = 0; sample <= ExitPoseSampleCount; sample++)
            {
                float n = sample / (float)ExitPoseSampleCount, time = right.length * n;
                var pose = alignment * new Quaternion(authoredRotation[0].Evaluate(time), authoredRotation[1].Evaluate(time), authoredRotation[2].Evaluate(time), authoredRotation[3].Evaluate(time)).normalized;
                pose = new Quaternion(pose.x, -pose.y, -pose.z, pose.w);
                var rotation = n >= ExitCombatPoseReady ? target : Quaternion.Slerp(Quaternion.identity, correction, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ExitStanceBlendStart, ExitCombatPoseReady, n))) * pose;
                if (sample > 0 && Quaternion.Dot(previous, rotation) < 0) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                for (int component = 0; component < 4; component++) curves[component].AddKey(time, rotation[component]);
                previous = rotation;
            }
            for (int component = 0; component < 4; component++) AnimationUtility.SetEditorCurve(right, rotationBindings[component], curves[component]);
            // Both sides finish in the same right-handed combat frame. Only the
            // wall-relative opening and stepping legs are reflected.
            foreach (var binding in bindings)
            {
                string name = binding.propertyName;
                if (!name.StartsWith("Spine", StringComparison.Ordinal) && !name.StartsWith("Chest", StringComparison.Ordinal) && !name.StartsWith("UpperChest", StringComparison.Ordinal) && !name.StartsWith("Neck", StringComparison.Ordinal) && !name.StartsWith("Head", StringComparison.Ordinal)) continue;
                var authored = AnimationUtility.GetEditorCurve(right, binding);
                var end = AnimationUtility.GetEditorCurve(aim, binding);
                if (end == null) continue;
                var curve = new AnimationCurve();
                for (int sample = 0; sample <= ExitPoseSampleCount; sample++)
                {
                    float n = sample / (float)ExitPoseSampleCount, time = right.length * n;
                    curve.AddKey(time, Mathf.Lerp(authored.Evaluate(time), end.Evaluate(0), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ExitTorsoBlendStart, ExitCombatPoseReady, n))));
                }
                AnimationUtility.SetEditorCurve(right, binding, curve);
            }
            EditorUtility.SetDirty(right); AssetDatabase.SaveAssetIfDirty(right);
        }
        private static void MatchExitStance(AnimationClip destination, AnimationClip source, AnimationClip aim, bool right)
        {
            var sourceBindings = AnimationUtility.GetCurveBindings(source);
            foreach (var binding in AnimationUtility.GetCurveBindings(destination))
            {
                string property = binding.propertyName;
                bool footGoal = property.StartsWith("LeftFoot", StringComparison.Ordinal) || property.StartsWith("RightFoot", StringComparison.Ordinal);
                bool leg = property.Contains(" Leg ") || property.Contains(" Foot ") || property.Contains(" Toes ");
                if (!footGoal && !leg && property != "RootT.y") continue;
                string from = right && (footGoal || leg) ? property.StartsWith("Left", StringComparison.Ordinal) ? "Right" + property.Substring(4) : "Left" + property.Substring(5) : property;
                float sign = right && footGoal && (property.EndsWith("T.x", StringComparison.Ordinal) || property.EndsWith("Q.y", StringComparison.Ordinal) || property.EndsWith("Q.z", StringComparison.Ordinal)) ? -1f : 1f;
                var authored = AnimationUtility.GetEditorCurve(source, sourceBindings.First(b => b.propertyName == from));
                var target = AnimationUtility.GetEditorCurve(aim, binding);
                if (target == null) continue;
                var curve = new AnimationCurve();
                for (int sample = 0; sample <= ExitPoseSampleCount; sample++)
                {
                    float n = sample / (float)ExitPoseSampleCount, time = destination.length * n;
                    float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ExitStanceBlendStart, ExitCombatPoseReady, n));
                    curve.AddKey(time, Mathf.Lerp(authored.Evaluate(time) * sign, target.Evaluate(0), blend));
                }
                AnimationUtility.SetEditorCurve(destination, binding, curve);
            }
            // Root orientation and lower-body pose must agree throughout the settle.
            // Keeping the source legs under a corrected pelvis twists the support leg.
        }
    }
}
