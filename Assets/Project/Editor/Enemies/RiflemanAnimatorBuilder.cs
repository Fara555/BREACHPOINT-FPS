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
        private static EnemyAnimationConfig Tuning => AssetDatabase.LoadAssetAtPath<GameObject>(AdamPresentationIntegration.RiflemanPath).GetComponent<EnemyAnimationBridge>().Config;
        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Breachpoint/Enemies/AI Test / Tactical Debug/Rebuild Rifleman Animator")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding Animator.");
            Clips.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { RiflemanRework.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                if (clip != null) Clips.Add(System.IO.Path.GetFileNameWithoutExtension(path), clip);
            }
            // Validate required assets before touching the serialized graph.
            foreach (string name in new[] { "Rifle Idle", "Rifle Walk", "idle aiming", "Fire", "Reload", "death from left", "Standing to crouch" }) Clip(name);
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RiflemanRework.ControllerPath);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(RiflemanRework.ControllerPath))
                if (asset != _controller) Object.DestroyImmediate(asset, true);
            _controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (string name in new[] { "MoveSpeed", "MoveX", "MoveY", "HitDirection", "ReloadSpeed", "SteadyStride", "CombatStride", "CrouchStride" }) _controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in new[] { "IsCombat", "IsCrouching", "IsFiring", "IsReloading", "IsHit", "IsDead" }) _controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "Turn90Left", "Turn90Right", "Turn180Left", "Turn180Right", "Hit" }) _controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine body = Machine("Base Layer");
            AnimatorStateMachine actions = Machine("Actions");
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null) { mask = new AvatarMask { name = "Adam Upper Body" }; AssetDatabase.CreateAsset(mask, MaskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i != (int)AvatarMaskBodyPart.Root && i != (int)AvatarMaskBodyPart.LeftLeg && i != (int)AvatarMaskBodyPart.RightLeg && i != (int)AvatarMaskBodyPart.LeftFootIK && i != (int)AvatarMaskBodyPart.RightFootIK);
            _controller.layers = new[]
            {
                new AnimatorControllerLayer { name = "Base Layer", stateMachine = body, defaultWeight = 1f },
                new AnimatorControllerLayer { name = "Actions", stateMachine = actions, avatarMask = mask, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override }
            };
            BuildBody(body); BuildActions(actions);
            EditorUtility.SetDirty(_controller); EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(_controller); AssetDatabase.SaveAssetIfDirty(mask);
            AssetDatabase.ImportAsset(RiflemanRework.ControllerPath);
            RiflemanRework.Audit();
        }

        private static AnimationClip Clip(string name) => Clips.TryGetValue(name, out AnimationClip clip) ? clip : throw new InvalidOperationException("Missing authored Adam clip: " + name);
        private static AnimationClip OneShot(string source)
        {
            string path = "Assets/Project/Art/Enemies/Adam/Config/" + source.Replace(" ", "") + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Object.Instantiate(Clip(source)); clip.name = source + " One Shot";
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false; settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, path);
            }
            return clip;
        }
        private static AnimationClip TurnClip(string source)
        {
            string path = "Assets/Project/Art/Enemies/Adam/Config/" + source.Replace(" ", "") + "InPlace.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Object.Instantiate(Clip(source)); AssetDatabase.CreateAsset(clip, path);
            }
            // Authored yaw is baked into the source pose and can oppose gameplay yaw.
            // Extract it in this representation; navigation alone rotates the actor.
            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopBlendOrientation = false;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }
        private static AnimationClip CrouchReloadClip()
        {
            const string path = "Assets/Project/Art/Enemies/Adam/Config/CrouchReloadUpper.anim";
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
                AnimatorState turn = State(machine, group + trigger, TurnClip(prefix + suffix), "Turn");
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
            AnimatorState fire = State(machine, "Fire", Clip("Fire"), "Fire");
            var crouchFireTree = Tree("CrouchFireMotion", "MoveSpeed"); crouchFireTree.AddChild(Clip("Fire Crouch"), 0f); crouchFireTree.AddChild(Clip("Fire Crouch Moving"), 0.33f);
            AnimatorState crouchFire = State(machine, "CrouchFire", crouchFireTree, "Fire");
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
            foreach (AnimatorState state in new[] { hit, crouchHit, reload, crouchReload, fire, crouchFire })
            {
                AnimatorStateTransition enter = machine.AddAnyStateTransition(state); enter.duration = state == hit || state == crouchHit ? Tuning.HitBlendIn : Tuning.ActionBlendIn; enter.hasFixedDuration = true; enter.hasExitTime = false; enter.canTransitionToSelf = false;
                Bool(enter, "IsDead", false); Bool(enter, "IsCrouching", state == crouchHit || state == crouchReload || state == crouchFire);
                if (state == hit || state == crouchHit) enter.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                else Bool(enter, "IsHit", false);
                if (state != hit && state != crouchHit) Bool(enter, "IsReloading", state == reload || state == crouchReload);
                if (state == fire || state == crouchFire) Bool(enter, "IsFiring", true);
                AnimatorStateTransition leave = To(state, empty, Tuning.ActionBlendOut);
                if (state == hit || state == crouchHit) Bool(leave, "IsHit", false);
                if (state == reload || state == crouchReload) Bool(leave, "IsReloading", false);
                if (state == fire || state == crouchFire) Bool(leave, "IsFiring", false);
                Bool(To(state, empty, 0.02f), "IsDead", true);
            }
        }
    }
}


