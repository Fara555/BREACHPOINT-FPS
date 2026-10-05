using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;
using Breachpoint.Gameplay.AI;

namespace Breachpoint.Editor.Enemies
{
    public static class RiflemanAnimatorBuilder
    {
        private const string MaskPath = "Assets/Project/Art/Enemies/Adam/Config/AM_Adam_UpperBody.mask";
        private static AnimatorController _controller;
        private static EnemyAnimationConfig Tuning => AssetDatabase.LoadAssetAtPath<GameObject>(EnemyValidationRunner.RiflemanPath).GetComponent<EnemyAnimationBridge>().Config;
        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Rebuild Rifleman Animator")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding Animator.");
            if (!EditorUtility.DisplayDialog("Replace manually tuned Animator?", "This recreates the entire Rifleman graph and replaces state tuning, transitions and masks. Use targeted maintenance to preserve Inspector edits.", "Replace entire graph", "Cancel")) return;
            LoadSourceClips();
            RebuildGraph();
        }

        private static void LoadSourceClips()
        {
            Clips.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { EnemyTools.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                if (clip != null) Clips.Add(System.IO.Path.GetFileNameWithoutExtension(path), clip);
            }
        }

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Refresh turn profiles only")]
        public static void RefreshTurnProfiles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            LoadSourceClips(); WriteTurnProfiles();
        }

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Refresh derived turn clips only")]
        public static void RefreshTurnClips()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            LoadSourceClips();
            foreach (string prefix in new[] { "Steady rifle turn ", "turn ", "crouching turn " })
                foreach (string suffix in new[] { "90 left", "90 right", "180 left", "180 right" }) TurnClip(prefix + suffix);
        }

        private static void RebuildGraph()
        {
            // Validate required assets before touching the serialized graph.
            foreach (string name in new[] { "Rifle Idle", "Rifle Walk", "idle aiming", "Fire", "Reload", "death from left", "Standing to crouch" }) Clip(name);
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyTools.ControllerPath);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(EnemyTools.ControllerPath))
                if (asset != _controller) Object.DestroyImmediate(asset, true);
            _controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (string name in new[] { "MoveSpeed", "MoveX", "MoveY", "HitDirection", "ReloadSpeed", "SteadyStride", "CombatStride", "CrouchStride" }) _controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in new[] { "IsCombat", "IsCrouching", "IsFiring", "IsReloading", "IsHit", "IsDead" }) _controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "Turn90Left", "Turn90Right", "Turn180Left", "Turn180Right", "Hit" }) _controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine body = Machine("Base Layer");
            AnimatorStateMachine actions = Machine("Actions");
            AnimatorStateMachine recoil = Machine("Recoil");
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null) { mask = new AvatarMask { name = "Adam Upper Body" }; AssetDatabase.CreateAsset(mask, MaskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i != (int)AvatarMaskBodyPart.Root && i != (int)AvatarMaskBodyPart.LeftLeg && i != (int)AvatarMaskBodyPart.RightLeg && i != (int)AvatarMaskBodyPart.LeftFootIK && i != (int)AvatarMaskBodyPart.RightFootIK);
            _controller.layers = new[]
            {
                new AnimatorControllerLayer { name = "Base Layer", stateMachine = body, defaultWeight = 1f },
                new AnimatorControllerLayer { name = "Actions", stateMachine = actions, avatarMask = mask, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override },
                new AnimatorControllerLayer { name = "Recoil", stateMachine = recoil, avatarMask = RecoilMask(), defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Additive }
            };
            BuildBody(body); BuildActions(actions); BuildRecoil(recoil);
            EditorUtility.SetDirty(_controller); EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(_controller); AssetDatabase.SaveAssetIfDirty(mask);
            AssetDatabase.ImportAsset(EnemyTools.ControllerPath);
            WriteTurnProfiles();
            EnemyTools.Audit();
        }

        private static AnimationClip Clip(string name) => Clips.TryGetValue(name, out AnimationClip clip) ? clip : throw new InvalidOperationException("Missing authored Adam clip: " + name);
        private static AnimationClip OneShot(string source)
        {
            string path = EnemyAnimationAssetPaths.DerivedClip(source.Replace(" ", "") + ".anim");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Object.Instantiate(Clip(source)); clip.name = source + " One Shot";
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false; settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, path);
            }
            if (source == "Rifle Raise") MatchReadinessPose(clip, Clip("Rifle Raise"), Clip("Rifle Idle"), Clip("idle aiming"));
            return clip;
        }

        private static void MatchReadinessPose(AnimationClip destination, AnimationClip authored, AnimationClip startPose, AnimationClip endPose)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(authored))
            {
                if (binding.type != typeof(Animator) || binding.propertyName.StartsWith("RootQ", StringComparison.Ordinal)) continue;
                var curve = AnimationUtility.GetEditorCurve(authored, binding);
                var first = AnimationUtility.GetEditorCurve(startPose, binding);
                var last = AnimationUtility.GetEditorCurve(endPose, binding);
                if (first == null || last == null) continue;
                float startCorrection = first.Evaluate(0) - curve.Evaluate(0);
                float endCorrection = last.Evaluate(0) - curve.Evaluate(authored.length * .9f);
                var matched = new AnimationCurve();
                for (int frame = 0; frame <= 60; frame++)
                {
                    float time = authored.length * frame / 60f;
                    float phase = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / (authored.length * .9f)));
                    float value = time >= authored.length * .9f ? last.Evaluate(0) : curve.Evaluate(time) + Mathf.Lerp(startCorrection, endCorrection, phase);
                    matched.AddKey(time, value);
                }
                AnimationUtility.SetEditorCurve(destination, binding, matched);
            }
            var rotationBindings = AnimationUtility.GetCurveBindings(authored).Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal)).OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1])).ToArray();
            if (rotationBindings.Length == 4)
            {
                Quaternion Rotation(AnimationClip clip, float time)
                {
                    var values = rotationBindings.Select(b => AnimationUtility.GetEditorCurve(clip, b).Evaluate(time)).ToArray();
                    return new Quaternion(values[0], values[1], values[2], values[3]).normalized;
                }
                Quaternion initial = Rotation(startPose, 0) * Quaternion.Inverse(Rotation(authored, 0));
                Quaternion final = Rotation(endPose, 0) * Quaternion.Inverse(Rotation(authored, authored.length * .9f));
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                Quaternion previous = Rotation(startPose, 0);
                for (int frame = 0; frame <= 60; frame++)
                {
                    float time = authored.length * frame / 60f;
                    float phase = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / (authored.length * .9f)));
                    Quaternion rotation = time >= authored.length * .9f ? Rotation(endPose, 0) : (Quaternion.Slerp(initial, final, phase) * Rotation(authored, time)).normalized;
                    if (Quaternion.Dot(previous, rotation) < 0) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    for (int i = 0; i < 4; i++) curves[i].AddKey(time, rotation[i]);
                    previous = rotation;
                }
                for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(destination, rotationBindings[i], curves[i]);
            }
            EditorUtility.SetDirty(destination); AssetDatabase.SaveAssetIfDirty(destination);
        }

        private static AvatarMask RecoilMask()
        {
            const string path = "Assets/Project/Art/Enemies/Adam/Config/AM_Adam_Recoil.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null) { mask = new AvatarMask { name = "Adam recoil arms" }; AssetDatabase.CreateAsset(mask, path); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.RightArm);
            EditorUtility.SetDirty(mask); AssetDatabase.SaveAssetIfDirty(mask); return mask;
        }

        private static AnimationClip RecoilClip(string source)
        {
            string path = EnemyAnimationAssetPaths.DerivedClip(source.Replace(" ", "") + "Recoil.anim");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) clip = Object.Instantiate(Clip(source));
            else EditorUtility.CopySerialized(Clip(source), clip);
            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAdditiveReferencePose(clip, Clip(source), 0f);
            if (created) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
            return clip;
        }

        private static void BuildRecoil(AnimatorStateMachine machine)
        {
            var empty = State(machine, "None", null, "None"); machine.defaultState = empty;
            var fire = State(machine, "Fire", RecoilClip("Fire"), "Fire");
            var crouchTree = Tree("CrouchRecoil", "MoveSpeed"); crouchTree.AddChild(RecoilClip("Fire Crouch"), 0); crouchTree.AddChild(RecoilClip("Fire Crouch Moving"), .33f);
            var crouchFire = State(machine, "CrouchFire", crouchTree, "Fire");
            foreach (var state in new[] { fire, crouchFire })
            {
                var enter = machine.AddAnyStateTransition(state); enter.duration = Tuning.ActionBlendIn; enter.hasFixedDuration = true; enter.hasExitTime = false; enter.canTransitionToSelf = false;
                Alive(enter); Bool(enter, "IsHit", false); Bool(enter, "IsReloading", false); Bool(enter, "IsFiring", true); Bool(enter, "IsCrouching", state == crouchFire);
                Bool(To(state, empty, Tuning.ActionBlendOut), "IsFiring", false);
                Bool(To(state, empty, Tuning.ActionBlendOut), "IsHit", true);
                Bool(To(state, empty, Tuning.ActionBlendOut), "IsReloading", true);
                Bool(To(state, empty, .02f), "IsDead", true);
            }
        }
        private static AnimationClip TurnClip(string source)
        {
            string path = EnemyAnimationAssetPaths.DerivedClip(source.Replace(" ", "") + "InPlace.anim");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Object.Instantiate(Clip(source)); AssetDatabase.CreateAsset(clip, path);
            }
            else EditorUtility.CopySerialized(Clip(source), clip);
            // Preserve the initial body heading, tilt and weight shift. Remove only
            // the changing authored yaw, which navigation reapplies at the same phase.
            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var rotationBindings = bindings.Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal)).OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1])).ToArray();
            var original = rotationBindings.Select(b => AnimationUtility.GetEditorCurve(Clip(source), b)).ToArray();
            var rotations = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var xBinding = bindings.First(b => b.propertyName == "RootT.x");
            var zBinding = bindings.First(b => b.propertyName == "RootT.z");
            var originalX = AnimationUtility.GetEditorCurve(Clip(source), xBinding); var originalZ = AnimationUtility.GetEditorCurve(Clip(source), zBinding);
            var xCurve = new AnimationCurve(); var zCurve = new AnimationCurve();
            Quaternion At(float time) => new Quaternion(original[0].Evaluate(time), original[1].Evaluate(time), original[2].Evaluate(time), original[3].Evaluate(time)).normalized;
            float initialYaw = At(0).eulerAngles.y; Quaternion previous = At(0);
            for (int frame = 0; frame <= 60; frame++)
            {
                float time = clip.length * frame / 60f;
                Quaternion authored = At(time);
                Quaternion removeYaw = Quaternion.Euler(0, -Mathf.DeltaAngle(initialYaw, authored.eulerAngles.y), 0);
                Quaternion pose = (removeYaw * authored).normalized;
                if (Quaternion.Dot(previous, pose) < 0) pose = new Quaternion(-pose.x, -pose.y, -pose.z, -pose.w);
                for (int i = 0; i < 4; i++) rotations[i].AddKey(time, pose[i]);
                Vector3 position = removeYaw * new Vector3(originalX.Evaluate(time), 0, originalZ.Evaluate(time));
                xCurve.AddKey(time, position.x); zCurve.AddKey(time, position.z); previous = pose;
            }
            for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(clip, rotationBindings[i], rotations[i]);
            AnimationUtility.SetEditorCurve(clip, xBinding, xCurve); AnimationUtility.SetEditorCurve(clip, zBinding, zCurve);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionXZ = true;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }
        private static string TurnSourceSuffix(string group, string suffix) => group != "Steady" ? suffix : suffix.Contains("left") ? suffix.Replace("left", "right") : suffix.Replace("right", "left");

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Refresh Steady clip and profile mapping")]
        public static void RefreshSteadyMapping()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            LoadSourceClips();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyTools.ControllerPath);
            bool changed = false;
            foreach (string suffix in new[] { "90 left", "90 right", "180 left", "180 right" })
            {
                string stateName = "SteadyTurn" + suffix.Replace(" ", "").Replace("left", "Left").Replace("right", "Right");
                var state = controller.layers[0].stateMachine.states.First(s => s.state.name == stateName).state;
                string source = "Steady rifle turn " + TurnSourceSuffix("Steady", suffix);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyAnimationAssetPaths.DerivedClip(source.Replace(" ", "") + "InPlace.anim"));
                if (clip == null) throw new InvalidOperationException("Missing existing derived clip: " + source);
                if (state.motion == clip) continue;
                Undo.RecordObject(state, "Correct reversed Steady source handedness"); state.motion = clip; EditorUtility.SetDirty(state); changed = true;
            }
            // A second maintenance run preserves profiles the user may subsequently tune.
            if (!changed) return;
            AssetDatabase.SaveAssetIfDirty(controller); WriteTurnProfiles(true);
        }

        private static void WriteTurnProfiles(bool steadyOnly = false)
        {
            var prefab = PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            try
            {
                var serialized = new SerializedObject(prefab.GetComponent<EnemyAnimationBridge>());
                var profiles = serialized.FindProperty("_config._turnProfiles"); if (!steadyOnly) profiles.arraySize = 12;
                int index = 0;
                foreach (var group in new[] { ("Steady", "Steady rifle turn "), ("Combat", "turn "), ("Crouch", "crouching turn ") })
                foreach (string suffix in new[] { "90 left", "90 right", "180 left", "180 right" })
                {
                    if (steadyOnly && group.Item1 != "Steady") continue;
                    var clip = Clip(group.Item2 + TurnSourceSuffix(group.Item1, suffix));
                    var bindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal)).OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1])).ToArray();
                    if (bindings.Length != 4) throw new InvalidOperationException("Turn has no complete authored yaw: " + clip.name);
                    var components = bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
                    var yaw = new float[61]; float previous = 0;
                    for (int frame = 0; frame < yaw.Length; frame++)
                    {
                        float time = clip.length * frame / (yaw.Length - 1);
                        var rotation = new Quaternion(components[0].Evaluate(time), components[1].Evaluate(time), components[2].Evaluate(time), components[3].Evaluate(time)).normalized;
                        float angle = rotation.eulerAngles.y;
                        yaw[frame] = frame == 0 ? 0 : yaw[frame - 1] + Mathf.DeltaAngle(previous, angle);
                        previous = angle;
                    }
                    float total = yaw[yaw.Length - 1];
                    if (Mathf.Abs(total) < 30) throw new InvalidOperationException("Insufficient authored turn yaw: " + clip.name);
                    var curve = new AnimationCurve();
                    for (int frame = 0; frame < yaw.Length; frame++) curve.AddKey(frame / (float)(yaw.Length - 1), yaw[frame] / total);
                    string state = group.Item1 + "Turn" + suffix.Replace(" ", "").Replace("left", "Left").Replace("right", "Right");
                    int profileIndex = index++;
                    if (steadyOnly)
                    {
                        profileIndex = -1;
                        for (int i = 0; i < profiles.arraySize; i++) if (profiles.GetArrayElementAtIndex(i).FindPropertyRelative("_state").stringValue == state) { profileIndex = i; break; }
                        if (profileIndex < 0) throw new InvalidOperationException("Missing existing turn profile: " + state);
                    }
                    var property = profiles.GetArrayElementAtIndex(profileIndex);
                    property.FindPropertyRelative("_state").stringValue = state;
                    property.FindPropertyRelative("_duration").floatValue = clip.length;
                    property.FindPropertyRelative("_progress").animationCurveValue = curve;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(prefab, EnemyValidationRunner.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static AnimationClip CrouchReloadClip()
        {
            string path = EnemyAnimationAssetPaths.DerivedClip("CrouchReloadUpper.anim");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) { clip = Object.Instantiate(Clip("Reload")); clip.name = "CrouchReloadUpper"; }
            // Retain authored arm/hand timing while the crouched torso stays crouched.
            // Body muscles on an override layer otherwise straighten the lower stance.
            var binding = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName == "RootT.y");
            var crouchHeight = AnimationUtility.GetEditorCurve(Clip("idle crouching"), binding);
            if (crouchHeight == null) throw new InvalidOperationException("Authored crouch body height is missing.");
            float height = crouchHeight.Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, height));
            foreach (var body in AnimationUtility.GetCurveBindings(clip))
            {
                if (!body.propertyName.StartsWith("Spine", StringComparison.Ordinal) && !body.propertyName.StartsWith("Chest", StringComparison.Ordinal) && !body.propertyName.StartsWith("UpperChest", StringComparison.Ordinal)) continue;
                var reference = AnimationUtility.GetEditorCurve(Clip("idle crouching"), body);
                if (reference != null) AnimationUtility.SetEditorCurve(clip, body, AnimationCurve.Constant(0f, clip.length, reference.Evaluate(0f)));
            }
            if (created) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
            return clip;
        }
        private static AnimatorStateMachine Machine(string name)
        {
            var machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, _controller); return machine;
        }
        private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, string tag, float speed = 1f)
        {
            AnimatorState state = machine.AddState(name); state.motion = motion; state.tag = tag; state.speed = speed; state.writeDefaultValues = false; return state;
        }
        private static AnimatorStateTransition To(AnimatorState source, AnimatorState target, float duration = 0.1f, float exit = -1f)
        {
            AnimatorStateTransition transition = source.AddTransition(target);
            transition.duration = duration; transition.hasFixedDuration = true; transition.hasExitTime = exit >= 0f; transition.exitTime = Mathf.Max(0f, exit);
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination; transition.orderedInterruption = true; transition.canTransitionToSelf = false;
            return transition;
        }
        private static void Bool(AnimatorStateTransition transition, string parameter, bool value) => transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        private static void Alive(AnimatorStateTransition transition) => Bool(transition, "IsDead", false);
        private static void Speed(AnimatorStateTransition transition, bool moving) => transition.AddCondition(moving ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, moving ? 0.025f : 0.015f, "MoveSpeed");
        private static BlendTree Tree(string name, string parameter)
        {
            var tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = parameter, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, _controller); return tree;
        }
        private static BlendTree Directional(string name, string prefix, AnimationClip idle = null)
        {
            BlendTree tree = Tree(name, "MoveX"); tree.blendType = BlendTreeType.FreeformDirectional2D; tree.blendParameterY = "MoveY";
            if (idle != null) tree.AddChild(idle, Vector2.zero);
            string[] names = { "forward", "forward right", "right", "backward right", "backward", "backward left", "left", "forward left" };
            for (int i = 0; i < names.Length; i++)
            {
                float angle = i * Mathf.PI / 4f;
                tree.AddChild(Clip(prefix + names[i]), new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
            }
            return tree;
        }
        private static BlendTree SpeedTree(string name, params string[] clips)
        {
            BlendTree tree = Tree(name, "MoveSpeed");
            for (int i = 0; i < clips.Length; i++) tree.AddChild(Clip(clips[i]), i == 3 ? 1f : i * 0.33f);
            return tree;
        }

        private static void BuildBody(AnimatorStateMachine machine)
        {
            AnimatorState idle = State(machine, "SteadyIdle", Clip("Rifle Idle"), "Locomotion"); machine.defaultState = idle;
            AnimatorState start = State(machine, "SteadyStartWalk", Clip("Rifle start walking"), "Locomotion", Tuning.SteadyStartSpeed);
            AnimatorState walk = State(machine, "SteadyWalk", Clip("Rifle Walk"), "Locomotion");
            AnimatorState stop = State(machine, "SteadyStopWalk", Clip("Rifle stop walking"), "Locomotion", Tuning.SteadyStopSpeed);
            walk.speedParameter = "SteadyStride"; walk.speedParameterActive = true;
            var standingTree = Tree("StandingLocomotion", "MoveSpeed");
            standingTree.AddChild(Clip("idle aiming"), 0f);
            standingTree.AddChild(Directional("Walk", "walk "), 0.33f);
            standingTree.AddChild(Directional("Run", "run "), 0.66f);
            standingTree.AddChild(Directional("Sprint", "sprint "), 1f);
            AnimatorState standing = State(machine, "StandingLocomotion", standingTree, "Locomotion");
            standing.speedParameter = "CombatStride"; standing.speedParameterActive = true;
            AnimatorState crouch = State(machine, "CrouchLocomotion", Directional("Crouch", "walk crouching ", Clip("idle crouching")), "Locomotion");
            crouch.speedParameter = "CrouchStride"; crouch.speedParameterActive = true;
            AnimatorState down = State(machine, "StandingToCrouch", Clip("Standing to crouch"), "Stance", Tuning.StancePlaybackSpeed);
            AnimatorState up = State(machine, "CrouchToStanding", Clip("Crouch to standing"), "Stance", Tuning.StancePlaybackSpeed);
            AnimatorState raise = State(machine, "RaiseWeapon", OneShot("Rifle Raise"), "Readiness");
            AnimatorState lower = State(machine, "LowerWeapon", OneShot("Rifle Lower"), "Readiness");
            Speed(To(idle, start, Tuning.LocomotionBlend), true);
            // Use the authored opening/settling phase at natural speed, not a 4x whole clip.
            To(start, walk, Tuning.LocomotionBlend, 0.25f); Speed(To(start, stop, Tuning.LocomotionBlend), false);
            Speed(To(walk, stop, Tuning.LocomotionBlend), false); To(stop, idle, Tuning.LocomotionBlend, 0.35f); Speed(To(stop, start, Tuning.LocomotionBlend), true);
            foreach (AnimatorState state in new[] { idle, start, walk, stop })
            {
                AnimatorStateTransition stance = To(state, down, Tuning.StanceBlend); Bool(stance, "IsCrouching", true); Alive(stance);
                AnimatorStateTransition mode = To(state, raise, Tuning.ReadinessBlend); Bool(mode, "IsCombat", true); Bool(mode, "IsCrouching", false); Alive(mode);
            }
            To(raise, standing, Tuning.ReadinessBlend, 0.9f);
            To(lower, idle, Tuning.ReadinessBlend, 0.9f);
            Bool(To(raise, lower, Tuning.ReadinessBlend), "IsCombat", false);
            Bool(To(lower, raise, Tuning.ReadinessBlend), "IsCombat", true);
            foreach (var mode in new[] { raise, lower }) Bool(To(mode, down, Tuning.StanceBlend), "IsCrouching", true);
            AnimatorStateTransition toSteady = To(standing, lower, Tuning.ReadinessBlend); Bool(toSteady, "IsCombat", false); Bool(toSteady, "IsCrouching", false); Alive(toSteady);
            AnimatorStateTransition toDown = To(standing, down, Tuning.StanceBlend); Bool(toDown, "IsCrouching", true); Alive(toDown);
            AnimatorStateTransition toUp = To(crouch, up, Tuning.StanceBlend); Bool(toUp, "IsCrouching", false); Alive(toUp);
            To(down, crouch, Tuning.StanceBlend, 0.9f); Bool(To(down, up, Tuning.StanceBlend), "IsCrouching", false);
            To(up, standing, Tuning.StanceBlend, 0.9f); Bool(To(up, down, Tuning.StanceBlend), "IsCrouching", true);
            AddTurns(machine, idle, "Steady", "Steady rifle turn ");
            AddTurns(machine, standing, "Combat", "turn ");
            AddTurns(machine, crouch, "Crouch", "crouching turn ");
            BlendTree deathTree = Tree("DeathDirections", "HitDirection");
            string[] deaths = { "death from the front", "death from left", "death from right", "death from the back" };
            for (int i = 0; i < deaths.Length; i++) deathTree.AddChild(Clip(deaths[i]), i);
            AnimatorState crouchDeath = State(machine, "CrouchDeath", Clip("death crouching headshot front"), "Death");
            AnimatorState death = State(machine, "Death", deathTree, "Death");
            foreach (AnimatorState state in new[] { crouchDeath, death })
            {
                AnimatorStateTransition transition = machine.AddAnyStateTransition(state); transition.hasExitTime = false; transition.duration = 0.04f; transition.hasFixedDuration = true; transition.canTransitionToSelf = false;
                Bool(transition, "IsDead", true); Bool(transition, "IsCrouching", state == crouchDeath);
            }
        }
        private static void AddTurns(AnimatorStateMachine machine, AnimatorState entry, string group, string prefix)
        {
            foreach (string suffix in new[] { "90 left", "90 right", "180 left", "180 right" })
            {
                string trigger = "Turn" + suffix.Replace(" ", ""); trigger = trigger.Replace("left", "Left").Replace("right", "Right");
                AnimatorState turn = State(machine, group + trigger, TurnClip(prefix + TurnSourceSuffix(group, suffix)), "Turn");
                AnimatorStateTransition enter = To(entry, turn, Tuning.TurnBlendIn); enter.AddCondition(AnimatorConditionMode.If, 0f, trigger); Speed(enter, false); Bool(enter, "IsHit", false); Bool(enter, "IsReloading", false); Alive(enter);
                To(turn, entry, Tuning.TurnBlendOut, 0.92f);
                Speed(To(turn, entry, Tuning.TurnBlendOut), true);
                AnimatorStateTransition mode = To(turn, entry); Bool(mode, "IsHit", true);
                if (group == "Steady") Bool(To(turn, machine.states.First(s => s.state.name == "StandingLocomotion").state), "IsCombat", true);
                else if (group == "Combat") Bool(To(turn, machine.states.First(s => s.state.name == "SteadyIdle").state), "IsCombat", false);
                Bool(To(turn, entry), "IsCrouching", group != "Crouch");
            }
        }
        private static BlendTree NormalizeDuration(BlendTree tree)
        {
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++) if (children[i].motion is AnimationClip clip) children[i].timeScale = clip.length;
            tree.children = children; return tree;
        }
        private static void BuildActions(AnimatorStateMachine machine)
        {
            AnimatorState empty = State(machine, "None", null, "None"); machine.defaultState = empty;
            AnimatorState reload = State(machine, "Reload", NormalizeDuration(SpeedTree("ReloadMotion", "Reload", "Reload Walking", "Reload Sprinting")), "Reload");
            var crouchReloadTree = Tree("CrouchReloadMotion", "MoveSpeed"); crouchReloadTree.AddChild(CrouchReloadClip(), 0f); NormalizeDuration(crouchReloadTree);
            AnimatorState crouchReload = State(machine, "CrouchReload", crouchReloadTree, "Reload");
            reload.speedParameter = crouchReload.speedParameter = "ReloadSpeed"; reload.speedParameterActive = crouchReload.speedParameterActive = true;
            var hitMotion = Tree("HitMotion", "MoveSpeed");
            foreach (var group in new[] { ("Idle Hit", "Idle hit left", "Idle hit right"), ("Walk Hit", "Walk hit left", "Walk hit right"), ("Sprint hit", "Sprint hit left", "Sprint hit right") })
            {
                var directional = Tree(group.Item1, "HitDirection"); directional.AddChild(Clip(group.Item1), 0f); directional.AddChild(Clip(group.Item2), 1f); directional.AddChild(Clip(group.Item3), 2f);
                hitMotion.AddChild(directional, hitMotion.children.Length * 0.33f);
            }
            AnimatorState hit = State(machine, "Hit", hitMotion, "Hit");
            var crouchHitTree = Tree("CrouchHitMotion", "HitDirection");
            for (int i = 0; i < 3; i++) crouchHitTree.AddChild(Clip("Crouch hit " + (i + 1)), i);
            AnimatorState crouchHit = State(machine, "CrouchHit", crouchHitTree, "Hit");
            // Priority: hit > reload > fire. Gameplay clock controls reload completion.
            foreach (AnimatorState state in new[] { hit, crouchHit, reload, crouchReload })
            {
                AnimatorStateTransition enter = machine.AddAnyStateTransition(state); enter.duration = state == hit || state == crouchHit ? Tuning.HitBlendIn : Tuning.ActionBlendIn; enter.hasFixedDuration = true; enter.hasExitTime = false; enter.canTransitionToSelf = false;
                Bool(enter, "IsDead", false); Bool(enter, "IsCrouching", state == crouchHit || state == crouchReload);
                if (state == hit || state == crouchHit) enter.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                else Bool(enter, "IsHit", false);
                if (state != hit && state != crouchHit) Bool(enter, "IsReloading", state == reload || state == crouchReload);
                AnimatorStateTransition leave = To(state, empty, Tuning.ActionBlendOut);
                if (state == hit || state == crouchHit) Bool(leave, "IsHit", false);
                if (state == reload || state == crouchReload) Bool(leave, "IsReloading", false);
                Bool(To(state, empty, 0.02f), "IsDead", true);
            }
        }
    }
}
