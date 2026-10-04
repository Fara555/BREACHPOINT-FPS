using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal static void RepairEarlySteadyCancellation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before targeted authoring.");
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "SteadyStartWalk").state;
            var transition = state.transitions.Single(t => t.destinationState != null && t.destinationState.name == "SteadyIdle");
            var conditions = transition.conditions;
            if (conditions.Length != 1 || conditions[0].parameter != "MoveSpeed" || conditions[0].mode != UnityEditor.Animations.AnimatorConditionMode.Less)
                throw new InvalidOperationException("Current early-cancel transition has unexpected user-authored conditions; inspect before changing it.");
            float before = conditions[0].threshold;
            Undo.RecordObject(transition, "Repair early Steady cancellation");
            conditions[0].threshold = .015f; transition.conditions = conditions; EditorUtility.SetDirty(transition);
            AssetDatabase.SaveAssetIfDirty(controller);
            File.WriteAllText(EnemyTools.Evidence + "/final-short-stop-transition.txt", FormattableString.Invariant($"SteadyStartWalk -> SteadyIdle: MoveSpeed Less {before:R} -> .015; zero is unreachable for this non-negative blend. Cancel before the first committed step instead of playing a full stopping stride at .2 m/s. Existing .25-second blend, exit/offset/priority and all other values preserved.\n"));
        }
        internal static void CalibrateFinalSteadyTransitions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before targeted authoring.");
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            var states = controller.layers[0].stateMachine.states;
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            void Duration(string from, string to, float duration, string reason)
            {
                var transition = states.Single(s => s.state.name == from).state.transitions.Single(t => t.destinationState != null && t.destinationState.name == to);
                report.AppendLine(FormattableString.Invariant($"{from} -> {to}: duration {transition.duration:R} -> {duration:R}; {reason}"));
                Undo.RecordObject(transition, "Calibrate measured Steady phase"); transition.duration = duration; EditorUtility.SetDirty(transition);
            }
            Duration("SteadyIdle", "SteadyStartWalk", .16f, "One-second blend conceals first foot commitment while the old navigation timer accelerates");
            var startExit = states.Single(s => s.state.name == "SteadyStartWalk").state.transitions.Single(t => t.destinationState != null && t.destinationState.name == "SteadyWalk");
            report.AppendLine(FormattableString.Invariant($"SteadyStartWalk -> SteadyWalk: exitTime {startExit.exitTime:R} -> .28; missed .001867 exit holds Start for a whole non-looping cycle; blended Walk plants the first advancing foot"));
            Undo.RecordObject(startExit, "Calibrate measured Steady phase"); startExit.exitTime = .28f; EditorUtility.SetDirty(startExit);
            Duration("SteadyStopWalk", "SteadyIdle", .16f, "Existing .49458 exit selects the first stopping stride; a 1.155-second outgoing blend exposed the next recovery step after world rest");
            AssetDatabase.SaveAssetIfDirty(controller);
            File.WriteAllText(EnemyTools.Evidence + "/final-transitions.txt", report.ToString());
        }
        private static IEnumerator FinalShortReview(GameLifetimeScope scope, EnemyWorld world)
        {
            SessionState.SetString("EnemyTools.Review.RecordGroup", "FinalShort");
            SessionState.SetInt("EnemyTools.Review.Scenario", 30);
            SessionState.SetBool("EnemyTools.Review.Record", true);
            IEnumerator review = LiveReviewTests(scope, world, false);
            try { while (review.MoveNext()) yield return review.Current; }
            finally { (review as IDisposable)?.Dispose(); SessionState.SetString("EnemyTools.Review.RecordGroup", ""); }
        }
        private static IEnumerator FinalReviewSuite(GameLifetimeScope scope, EnemyWorld world)
        {
            SessionState.SetString("EnemyTools.Review.RecordGroup", "FinalTargeted");
            try
            {
                foreach (int index in new[] { 0, 30, 27, 68, 69, 70, 71, 72, 73, 10, 11 })
                {
                    SessionState.SetInt("EnemyTools.Review.Scenario", index);
                    SessionState.SetBool("EnemyTools.Review.Record", true);
                    IEnumerator review = LiveReviewTests(scope, world, false);
                    try { while (review.MoveNext()) yield return review.Current; }
                    finally { (review as IDisposable)?.Dispose(); }
                }
            }
            finally { SessionState.SetString("EnemyTools.Review.RecordGroup", ""); }
        }
        private static IEnumerator FinalFocusedReview(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            var presenter = enemy.GetComponent<EnemyRigPresenter>();
            if (index < 3)
            {
                float height = index == 1 ? .6f : index == 2 ? -.6f : 0f;
                target.transform.position = AnimationStart + Vector3.forward * 10 + Vector3.up * height;
                Physics.SyncTransforms(); foreach (var wait in WaitReview(.7f)) yield return wait;
                brain.States.Change(EnemyStateId.Combat, "focused target-aware raise");
                float began = Time.time, maximumLateError = 0, maximumHandoffPitchStep = 0, previousPitch = PosePitch(actor.Muzzle), previousTime = Time.time;
                bool observedRaise = false; int lastFrame = -1;
                while (Time.time < began + 2.8f)
                {
                    float elapsed = Time.time - began;
                    if (index == 2 && elapsed > .32f && elapsed < .75f)
                    {
                        target.transform.position = AnimationStart + Vector3.forward * 10 + Vector3.up * Mathf.Lerp(-.6f, -.25f, Mathf.SmoothStep(0, 1, (elapsed - .32f) / .43f));
                        Physics.SyncTransforms();
                    }
                    if (bridge.IsRaising) observedRaise = true;
                    if (Time.frameCount != lastFrame)
                    {
                        lastFrame = Time.frameCount; float pitch = PosePitch(actor.Muzzle);
                        if (elapsed > .7f && elapsed < 1.2f && Time.time > previousTime)
                            maximumHandoffPitchStep = Mathf.Max(maximumHandoffPitchStep, Mathf.Abs(pitch - previousPitch) / (Time.time - previousTime));
                        if (elapsed > .65f && elapsed < 1.2f) maximumLateError = Mathf.Max(maximumLateError, Mathf.Abs(bridge.VerticalMuzzleErrorDegrees));
                        previousPitch = pitch; previousTime = Time.time;
                    }
                    yield return null;
                }
                AppendResult(FormattableString.Invariant($"RAISE height={height}: late vertical={maximumLateError:F3}deg handoff pitch rate={maximumHandoffPitchStep:F3}deg/s final={bridge.VerticalMuzzleErrorDegrees:F3} coarse={presenter.CoarseWeight:F3}"));
                PlayCheck(observedRaise && maximumLateError < 4f, "Raise already converges during its late gesture, including a moving height target");
                PlayCheck(maximumHandoffPitchStep < 65f && Mathf.Abs(bridge.VerticalMuzzleErrorDegrees) < 3f, "Raise/Combat handoff has no late pitch snap and ends aligned");
                var hand = enemy.GetComponentInChildren<TwoBoneIKConstraint>();
                PlayCheck(Vector3.Distance(hand.data.tip.position, hand.data.target.position) < .03f, "Target-aware Raise retains the authored final hand grip");
                yield break;
            }
            brain.States.Change(EnemyStateId.Combat, "focused Sprint presentation");
            Vector3 goal = AnimationStart + Vector3.forward * 24;
            if (index == 4)
            {
                actor.Navigation.ResetAt(new Vector3(-14, 0, -14)); enemy.transform.rotation = Quaternion.Euler(0, 45, 0);
                goal = new Vector3(14, 0, 14);
            }
            if (index == 5) { actor.Navigation.ResetAt(new Vector3(4, 0, -15)); goal = new Vector3(4, 0, 17); }
            foreach (var wait in WaitReview(1.8f)) yield return wait;
            int phases = index == 5 ? 3 : 1; float maximumHead = -90, minimumHead = 90, maximumCoarse = 0, maximumAim = 0, minimumStride = 10, maximumStride = 0; int samples = 0;
            for (int phase = 0; phase < phases; phase++)
            {
                EnemyMovePace pace = index == 5 && phase != 1 ? EnemyMovePace.Run : EnemyMovePace.Sprint;
                float began = Time.time, duration = index == 4 ? 8f : index == 5 ? 2.2f : 3f;
                while (Time.time < began + duration)
                {
                    actor.Navigation.MoveTo(goal, pace, Time.time);
                    target.transform.position = enemy.transform.position + enemy.transform.forward * 12;
                    Physics.SyncTransforms(); actor.Navigation.Face(target.transform.position, Time.deltaTime);
                    if (pace == EnemyMovePace.Sprint && Time.time > began + 1.4f && bridge.MoveSpeedSmoothed > .99f)
                    {
                        float head = PosePitch(bridge.Animator.GetBoneTransform(HumanBodyBones.Head));
                        maximumHead = Mathf.Max(maximumHead, head); minimumHead = Mathf.Min(minimumHead, head);
                        maximumCoarse = Mathf.Max(maximumCoarse, presenter.CoarseWeight); maximumAim = Mathf.Max(maximumAim, presenter.AimRigWeight);
                        float stride = bridge.Animator.GetFloat("CombatStride"); minimumStride = Mathf.Min(minimumStride, stride); maximumStride = Mathf.Max(maximumStride, stride); samples++;
                    }
                    yield return null;
                }
            }
            AppendResult(FormattableString.Invariant($"SPRINT samples={samples} head={minimumHead:F3}..{maximumHead:F3} coarseMax={maximumCoarse:F4} aimMax={maximumAim:F4} stride={minimumStride:F4}..{maximumStride:F4}"));
            PlayCheck(samples > 20 && maximumCoarse < .01f && maximumAim < .02f, "Full Sprint smoothly releases chest correction and AimRig while retaining hand IK");
            if (index != 4) PlayCheck(maximumHead < 2f && minimumHead > -10f, "Forward Sprint head pitch resembles the source range instead of the upward correction regression");
            PlayCheck(minimumStride > .75f && maximumStride < .84f, "Sprint keeps its measured cadence, without speeding up its half-second source cycle");
            actor.Navigation.RequestStop(); foreach (var wait in WaitReview(.8f)) yield return wait;
            PlayCheck(actor.Navigation.Velocity.magnitude < .03f && presenter.CoarseWeight > .7f && presenter.AimRigWeight > .95f, "Sprint stop restores ordinary Combat aim smoothly");
        }
        private static readonly HumanBodyBones[] FinalPoseBones = { HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Chest, HumanBodyBones.Spine };
        private static Camera CreateFinalCamera()
        {
            var camera = new GameObject("Focused presentation review camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            camera.depth = 100; camera.fieldOfView = 35; camera.nearClipPlane = .03f;
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Show();
            return camera;
        }
        private static void FinalCamera(Camera camera, GameObject enemy)
        {
            camera.transform.position = enemy.transform.position + new Vector3(2.6f, 1.65f, 3.5f);
            camera.transform.LookAt(enemy.transform.position + Vector3.up * .95f);
        }
        private static float PosePitch(Transform bone) => Mathf.Asin(Mathf.Clamp(bone.forward.y, -1, 1)) * Mathf.Rad2Deg;
        private static float CoarseWeight(EnemyRigPresenter presenter) => (float)typeof(EnemyRigPresenter).GetField("_coarseWeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(presenter);
        private static void RigMode(EnemyRigPresenter presenter, EnemyAnimationBridge bridge, int mode)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            // Keep the hand graph alive for the chest job, but isolate the authored IK constraint.
            typeof(EnemyRigPresenter).GetField("_aimActive", flags).SetValue(presenter, mode == 1 || mode == 4);
            var hand = bridge.Animator.GetComponent<RigBuilder>().layers[1].rig.GetComponentInChildren<TwoBoneIKConstraint>();
            hand.weight = mode == 3 || mode == 4 ? 1 : 0;
            var serialized = new SerializedObject(bridge);
            serialized.FindProperty("_config.<CoarseAimWeight>k__BackingField").floatValue = mode == 2 || mode == 4 ? .8f : 0;
            serialized.FindProperty("_config.<CoarseAimPitchWeight>k__BackingField").floatValue = mode == 2 || mode == 4 ? .95f : 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static IEnumerator FinalSourceAnalysis(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var camera = CreateFinalCamera();
            yield return null;
            enemy.GetComponent<EnemyBrain>().enabled = false;
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); bridge.enabled = false;
            enemy.GetComponent<EnemyRigPresenter>().enabled = false;
            var animator = bridge.Animator; var rig = animator.GetComponent<RigBuilder>();
            rig.layers[0].active = rig.layers[1].active = false;
            var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
            var rows = new StringBuilder("Clip,Time,Norm,Length,AverageSpeed,RootX,RootY,RootZ,LeftX,LeftY,LeftZ,RightX,RightY,RightZ,PelvisX,PelvisY,PelvisZ,HeadPitch,NeckPitch,ChestPitch,SpinePitch,WeaponPitch\n");
            string folder = EnemyTools.Evidence + "/FinalSourceFrames"; Directory.CreateDirectory(folder);
            try
            {
                foreach (string name in new[] { "Rifle Idle", "Rifle start walking", "Rifle Walk", "Rifle stop walking", "sprint forward", "turn 180 left", "turn 180 right", "DerivedLeft", "DerivedRight" })
                {
                    AnimationClip clip = name.StartsWith("Derived") ? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Project/Art/Enemies/Adam/Config/turn180" + (name == "DerivedLeft" ? "left" : "right") + "InPlace.anim") : SourceClip(name);
                    for (int i = 0; i <= 100; i++)
                    {
                        float normalized = i / 100f;
                        IEnumerator sample = SampleSourcePose(animator, rig, controller, clip, normalized); while (sample.MoveNext()) yield return sample.Current;
                        Vector3 left = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position), right = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position), hips = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                        Vector3 root = enemy.transform.position - AnimationStart;
                        rows.AppendLine(FormattableString.Invariant($"{name},{normalized*clip.length:F4},{normalized:F3},{clip.length:F4},{clip.averageSpeed.magnitude:F4},{root.x:F4},{root.y:F4},{root.z:F4},{left.x:F4},{left.y:F4},{left.z:F4},{right.x:F4},{right.y:F4},{right.z:F4},{hips.x:F4},{hips.y:F4},{hips.z:F4},{PosePitch(animator.GetBoneTransform(FinalPoseBones[0])):F3},{PosePitch(animator.GetBoneTransform(FinalPoseBones[1])):F3},{PosePitch(animator.GetBoneTransform(FinalPoseBones[2])):F3},{PosePitch(animator.GetBoneTransform(FinalPoseBones[3])):F3},{PosePitch(enemy.GetComponent<EnemyActor>().Muzzle):F3}"));
                        if (i % 10 == 0) { FinalCamera(camera, enemy); ScreenCapture.CaptureScreenshot(folder + "/" + name.Replace(' ', '-') + "-" + i.ToString("D3") + ".png"); yield return null; }
                    }
                }
                PlayCheck(true, "Recorded source Steady foot/body phases, Sprint posture and source/derived left/right on the gameplay avatar");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/final-source.csv", rows.ToString()); rig.Clear(); Object.Destroy(enemy); Object.Destroy(camera.gameObject); Object.Destroy(controller); }
        }
        private static IEnumerator FinalPresentationProbe(GameLifetimeScope scope, EnemyWorld world)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var player = new GameObject("Focused presentation target"); player.layer = 6;
            var collider = player.AddComponent<CapsuleCollider>(); collider.height = 2; collider.center = Vector3.up;
            player.AddComponent<Health>().Configure(1000000, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            var camera = CreateFinalCamera();
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); brain.enabled = false;
            var actor = enemy.GetComponent<EnemyActor>(); var bridge = enemy.GetComponent<EnemyAnimationBridge>();
            var presenter = enemy.GetComponent<EnemyRigPresenter>(); var animator = bridge.Animator; var rig = animator.GetComponent<RigBuilder>();
            string label = SessionState.GetString("EnemyTools.Final.Label", "current"), section = "Steady";
            string folder = EnemyTools.Evidence + "/FinalFrames-" + label; Directory.CreateDirectory(folder);
            var rows = new StringBuilder("Section,Time,Frame,Phase,State,Norm,Next,NextNorm,Transition,Clip,Weight,Actual,Allowed,Requested,Stride,RootX,RootY,RootZ,LeftX,LeftY,LeftZ,RightX,RightY,RightZ,PelvisX,PelvisY,PelvisZ,H,V,Total,Coarse,AimRig,HandRig,HeadPitch,NeckPitch,ChestPitch,SpinePitch,WeaponPitch,Yaw\n");
            var clips = new System.Collections.Generic.List<AnimatorClipInfo>(12); float began = Time.time, next = 0; int frame = -1; float nextCapture = 0;
            void Trace()
            {
                FinalCamera(camera, enemy);
                if (frame == Time.frameCount || Time.time < next) return; frame = Time.frameCount; next = Time.time + .02f;
                var current = animator.GetCurrentAnimatorStateInfo(0); bool transition = animator.IsInTransition(0); var destination = animator.GetNextAnimatorStateInfo(0);
                animator.GetCurrentAnimatorClipInfo(0, clips); AnimatorClipInfo dominant = clips.Count == 0 ? default : clips.OrderByDescending(c => c.weight).First();
                if (transition) { animator.GetNextAnimatorClipInfo(0, clips); foreach (var clip in clips) if (clip.weight > dominant.weight) dominant = clip; }
                Vector3 left = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position, right = animator.GetBoneTransform(HumanBodyBones.RightFoot).position, hips = animator.GetBoneTransform(HumanBodyBones.Hips).position, root = enemy.transform.position - AnimationStart;
                float clipBlend = transition ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0;
                animator.GetCurrentAnimatorClipInfo(0, clips); dominant = clips.Count == 0 ? default : clips.OrderByDescending(c => c.weight).First();
                float dominantWeight = dominant.weight * (1 - clipBlend);
                if (transition) { animator.GetNextAnimatorClipInfo(0, clips); foreach (var c in clips) if (c.weight * clipBlend > dominantWeight) { dominant = c; dominantWeight = c.weight * clipBlend; } }
                float stride = animator.GetFloat(section.StartsWith("Sprint") ? "CombatStride" : "SteadyStride");
                rows.AppendLine(FormattableString.Invariant($"{section},{Time.time-began:F4},{Time.frameCount},{actor.Navigation.MovementPhase},{EnemyAnimationStateNames.Get(current.fullPathHash)},{current.normalizedTime:F4},{(transition ? EnemyAnimationStateNames.Get(destination.fullPathHash) : "-")},{destination.normalizedTime:F4},{(transition ? animator.GetAnimatorTransitionInfo(0).normalizedTime : 0):F4},{dominant.clip?.name},{dominantWeight:F4},{actor.Navigation.Velocity.magnitude:F4},{actor.Navigation.AllowedSpeed:F4},{actor.Navigation.RequestedWorldSpeed:F4},{stride:F4},{root.x:F4},{root.y:F4},{root.z:F4},{left.x:F4},{left.y:F4},{left.z:F4},{right.x:F4},{right.y:F4},{right.z:F4},{hips.x:F4},{hips.y:F4},{hips.z:F4},{bridge.HorizontalMuzzleErrorDegrees:F4},{bridge.VerticalMuzzleErrorDegrees:F4},{bridge.MuzzleAimErrorDegrees:F4},{presenter.CoarseWeight:F4},{presenter.AimRigWeight:F4},{presenter.HandWeight:F4},{PosePitch(animator.GetBoneTransform(FinalPoseBones[0])):F3},{PosePitch(animator.GetBoneTransform(FinalPoseBones[1])):F3},{PosePitch(animator.GetBoneTransform(FinalPoseBones[2])):F3},{PosePitch(animator.GetBoneTransform(FinalPoseBones[3])):F3},{PosePitch(actor.Muzzle):F3},{enemy.transform.eulerAngles.y:F3}"));
                if (Time.time >= nextCapture) { nextCapture = Time.time + .1f; ScreenCapture.CaptureScreenshot(folder + "/" + section + "-" + Time.frameCount.ToString("D7") + ".png"); }
            }
            void Reset(bool combat)
            {
                brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(AnimationStart); enemy.transform.rotation = Quaternion.identity;
                brain.Memory.Target = target; brain.Memory.Visible = brain.Memory.HasContact = true;
                player.transform.position = AnimationStart + Vector3.forward * 12; Physics.SyncTransforms();
                if (combat) brain.States.Change(EnemyStateId.Combat, "focused presentation probe");
                began = Time.time; next = 0; nextCapture = 0;
            }
            try
            {
                Reset(false); foreach (var wait in WaitEnumerable(.6f)) yield return wait;
                began = Time.time;
                float until = Time.time + 8f;
                while (Time.time < until) { actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 20, EnemyMovePace.Walk, Time.time); Trace(); yield return null; }
                section = "SteadyStop"; began = Time.time; actor.Navigation.RequestStop();
                until = Time.time + 3f; while (Time.time < until) { Trace(); yield return null; }
                foreach (float height in new[] { 0f, .6f, -.6f })
                {
                    section = "Raise" + (height < 0 ? "Below" : height > 0 ? "Above" : "Center"); Reset(false);
                    player.transform.position = AnimationStart + Vector3.forward * 10 + Vector3.up * height; Physics.SyncTransforms();
                    foreach (var wait in WaitEnumerable(.7f)) yield return wait;
                    began = Time.time; brain.States.Change(EnemyStateId.Combat, "target-aware raise probe");
                    until = Time.time + 2.8f; while (Time.time < until) { Trace(); yield return null; }
                }
                for (int mode = 0; mode < 5; mode++)
                {
                    section = "SprintMode" + mode; Reset(true); RigMode(presenter, bridge, mode);
                    foreach (var wait in WaitEnumerable(1.8f)) yield return wait; began = Time.time;
                    until = Time.time + 3f;
                    while (Time.time < until) { actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 24, EnemyMovePace.Sprint, Time.time); player.transform.position = enemy.transform.position + Vector3.forward * 12; Physics.SyncTransforms(); actor.Navigation.Face(player.transform.position, Time.deltaTime); Trace(); yield return null; }
                    actor.Navigation.Stop();
                    foreach (int direction in new[] { -1, 1 })
                    {
                        section = "Turn" + (direction < 0 ? "Left" : "Right") + "Mode" + mode; Reset(true);
                        foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
                        player.transform.position = AnimationStart + Quaternion.Euler(0, direction * 175, 0) * Vector3.forward * 10; Physics.SyncTransforms();
                        began = Time.time; actor.Navigation.Face(player.transform.position, Time.deltaTime);
                        until = Time.time + 3.2f; while (Time.time < until) { Trace(); yield return null; }
                    }
                }
                PlayCheck(!float.IsNaN(bridge.MuzzleAimErrorDegrees), "Recorded Steady distance/feet, Raise height errors and five independent Sprint/turn rig modes");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/final-presentation-" + label + ".csv", rows.ToString()); Object.Destroy(enemy); Object.Destroy(player); Object.Destroy(camera.gameObject); }
        }
    }
}