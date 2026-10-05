using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        [MenuItem("Breachpoint/Enemies/Animator Authoring/Repair verified Steady synchronization transitions")]
        internal static void RepairSynchronizationTransitions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before targeted authoring.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var states = controller.layers[0].stateMachine.states;
            var changes = new StringBuilder();
            foreach (var setting in new[] { ("SteadyIdle", "SteadyStartWalk", .16f), ("SteadyStartWalk", "SteadyWalk", .18f), ("SteadyStartWalk", "SteadyStopWalk", .18f), ("SteadyWalk", "SteadyStopWalk", .2f), ("SteadyStopWalk", "SteadyIdle", .2f) })
            {
                var transition = states.First(s => s.state.name == setting.Item1).state.transitions.Single(t => t.destinationState != null && t.destinationState.name == setting.Item2);
                changes.AppendLine(FormattableString.Invariant($"{setting.Item1} -> {setting.Item2}: duration {transition.duration:R} -> {setting.Item3:R}; exit={transition.exitTime:R}, offset={transition.offset:R}, all other values preserved"));
                Undo.RecordObject(transition, "Repair verified Steady synchronization"); transition.duration = setting.Item3; EditorUtility.SetDirty(transition);
            }
            AssetDatabase.SaveAssetIfDirty(controller);
            File.WriteAllText(EnemyTools.Evidence + "/synchronization-transitions.txt", changes.ToString());
        }

        [MenuItem("Breachpoint/Enemies/Animation Review/Diagnostics/Record movement synchronization trace")]
        internal static void RunSynchronizationProbe() => ValidateStage(163);

        [MenuItem("Breachpoint/Enemies/Animation Review/Diagnostics/Record source 180 turns without rigs")]
        internal static void RunSourceTurnReview() => ValidateStage(166);
        private static IEnumerator SourceTurnReview(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            yield return null;
            enemy.GetComponent<EnemyBrain>().enabled = false;
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); bridge.enabled = false;
            enemy.GetComponent<EnemyRigPresenter>().enabled = false;
            enemy.GetComponent<EnemyActor>().Navigation.StopAndClearFacing();
            var rig = bridge.Animator.GetComponent<RigBuilder>(); rig.layers[0].active = rig.layers[1].active = false;
            var controller = new AnimatorOverrideController(bridge.Animator.runtimeAnimatorController);
            _reviewCamera = new GameObject("Source turn diagnostic Game View camera").AddComponent<Camera>();
            _reviewCamera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            _reviewCamera.depth = 100; _reviewCamera.fieldOfView = 35f; _reviewCamera.nearClipPlane = .03f;
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Show();
            SessionState.SetBool("EnemyTools.Review.Record", true); SessionState.SetFloat("EnemyTools.Review.TimeScale", 1f);
            _nextReviewCapture = 0f; _lastReviewCaptureFrame = -1;
            try
            {
                int index = 76;
                foreach (string name in new[] { "turn 180 left", "turn 180 right", "crouching turn 180 left", "crouching turn 180 right" })
                {
                    _reviewActiveScenario = index++;
                    var clip = SourceClip(name); AppendResult("SOURCE REVIEW " + _reviewActiveScenario + " " + name + " (Animator only; no corrective rigs)");
                    for (int sample = 0; sample <= 60; sample++)
                    {
                        IEnumerator pose = SampleSourcePose(bridge.Animator, rig, controller, clip, sample / 60f);
                        while (pose.MoveNext()) yield return pose.Current;
                        FollowReviewCamera(enemy); yield return null;
                    }
                }
                PlayCheck(true, "Recorded all four source 180 turns on the gameplay avatar without rig corrections for visual comparison");
            }
            finally
            {
                _reviewActiveScenario = -1; SessionState.SetBool("EnemyTools.Review.Record", false);
                DestroyReviewObject(enemy); DestroyReviewObject(controller);
                if (_reviewCamera != null) DestroyReviewObject(_reviewCamera.gameObject);
                _reviewCamera = null;
            }
        }
        private static IEnumerator VerticalAimReview(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            brain.States.Change(EnemyStateId.Combat, "vertical aim review"); bridge.SetCrouching(index == 4);
            foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
            Vector3 targetOffset = target.transform.TransformVector(target.GetComponent<CapsuleCollider>().center);
            int phases = index >= 3 ? 3 : 1;
            for (int phase = 0; phase < phases; phase++)
            {
                // Each height compares real Fire, rather than exhausting the magazine into Reload.
                if (index >= 3) brain.Combat.Reset();
                int ammoBefore = brain.Combat.Ammo;
                float pitch = index == 1 || index >= 3 && phase == 1 ? 3f : index == 2 || index >= 3 && phase == 2 ? -3f : 0f;
                float until = Time.time + 2.5f;
                while (Time.time < until)
                {
                    Vector3 chest = bridge.Animator.GetBoneTransform(HumanBodyBones.Chest).position;
                    // PerceptionTarget adds its own aim-height offset to the target root.

                    target.transform.position = chest + Vector3.forward * 10 + Vector3.up * (Mathf.Tan(pitch * Mathf.Deg2Rad) * 10) - targetOffset;
                    actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime);
                    if (index >= 3) { brain.Combat.Attack(target, Time.time); brain.Combat.Tick(Time.time); }
                    yield return null;
                }
                if (index >= 3)
                {
                    AppendResult("VERTICAL FIRE pitch=" + pitch + " shots=" + (ammoBefore - brain.Combat.Ammo));
                    PlayCheck(ammoBefore > brain.Combat.Ammo && !brain.Combat.IsReloading, "Every vertical review height contains actual Fire without unrelated Reload");
                }
            }
        }
        [MenuItem("Breachpoint/Enemies/Animation Review/Diagnostics/Inspect synchronization curves")]
        internal static void AuditSynchronizationCurves()
        {
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n" + ConsoleCounts() + "\n");
            var config = AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath).GetComponent<EnemyAnimationBridge>().Config;
            foreach (var pair in new[] { ("turn 180 left", "CombatTurn180Left"), ("turn 180 right", "CombatTurn180Right"), ("crouching turn 180 left", "CrouchTurn180Left"), ("crouching turn 180 right", "CrouchTurn180Right") })
            {
                var source = SourceClip(pair.Item1);
                var derived = AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyAnimationAssetPaths.DerivedClip(pair.Item1.Replace(" ", "") + "InPlace.anim"));
                var profile = config.GetTurnProfile(pair.Item2);
                report.AppendLine(FormattableString.Invariant($"TURN {pair.Item2} source={source.length:F4} derived={derived.length:F4} profile={profile.Duration:F4} p55={profile.Evaluate(.55f):F4} p92={profile.Evaluate(.92f):F4}"));
                foreach (var clip in new[] { source, derived })
                {
                    var curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("RootQ", StringComparison.Ordinal)).OrderBy(b => "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1])).Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
                    Quaternion previous = Quaternion.identity; float previousYaw = 0, yaw = 0, maxDelta = 0, minNorm = float.MaxValue; int negativeDots = 0;
                    for (int sample = 0; sample <= 240; sample++)
                    {
                        float time = clip.length * sample / 240f;
                        var raw = new Quaternion(curves[0].Evaluate(time), curves[1].Evaluate(time), curves[2].Evaluate(time), curves[3].Evaluate(time));
                        minNorm = Mathf.Min(minNorm, Mathf.Sqrt(Quaternion.Dot(raw, raw)));
                        var rotation = raw.normalized;
                        if (sample > 0) { maxDelta = Mathf.Max(maxDelta, Quaternion.Angle(previous, rotation)); if (Quaternion.Dot(previous, rotation) < 0) negativeDots++; yaw += Mathf.DeltaAngle(previousYaw, rotation.eulerAngles.y); }
                        previous = rotation; previousYaw = rotation.eulerAngles.y;
                    }
                    report.AppendLine(FormattableString.Invariant($"  {clip.name} yaw={yaw:F3} maximumQuaternionStep={maxDelta:F3} minimumNorm={minNorm:F4} negativeDots={negativeDots}"));
                }
            }
            File.WriteAllText(EnemyTools.Evidence + "/synchronization-curves.txt", report.ToString());
        }

        private static IEnumerator SynchronizationProbe(GameLifetimeScope scope, EnemyWorld world)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var player = new GameObject("Synchronization target"); player.layer = 6;
            player.AddComponent<Health>().Configure(1000000f, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); brain.enabled = false;
            var actor = enemy.GetComponent<EnemyActor>(); var bridge = enemy.GetComponent<EnemyAnimationBridge>(); var animator = bridge.Animator;
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot); var otherFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var rows = new StringBuilder("Phase,Time,Tier,Requested,Limit,Actual,Raw,Smoothed,Stride,State,Normalized,Transition,Clip,EffectiveSpeed,WorldYaw,FootMotion,Horizontal,Vertical,Total,MovementPhase,AllowedSpeed,FootMotionDetected\n");
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            Vector3 lastFoot = Vector3.zero, lastOther = Vector3.zero; float nextSample = 0;
            void Trace(string phase)
            {
                if (Time.time < nextSample) return; nextSample = Time.time + .02f;
                var state = animator.GetCurrentAnimatorStateInfo(0); var clips = animator.GetCurrentAnimatorClipInfo(0);
                string clip = clips.Length == 0 ? "None" : clips.OrderByDescending(c => c.weight).First().clip.name;
                var left = enemy.transform.InverseTransformPoint(foot.position); var right = enemy.transform.InverseTransformPoint(otherFoot.position);
                float feet = Vector3.Distance(lastFoot, left) + Vector3.Distance(lastOther, right); lastFoot = left; lastOther = right;
                Vector3 direction = (target.AimPosition - actor.Muzzle.position).normalized;
                float horizontal = Vector3.SignedAngle(Vector3.ProjectOnPlane(actor.Muzzle.forward, Vector3.up), Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
                float vertical = Mathf.Asin(Mathf.Clamp(actor.Muzzle.forward.y, -1, 1)) * Mathf.Rad2Deg - Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
                rows.AppendLine(FormattableString.Invariant($"{phase},{Time.time:F4},{actor.Navigation.DesiredMovementTier},{actor.Navigation.RequestedWorldSpeed:F4},{actor.Navigation.DesiredSpeed:F4},{actor.Navigation.Velocity.magnitude:F4},{bridge.MoveSpeedRaw:F4},{bridge.MoveSpeedSmoothed:F4},{state.speedMultiplier:F4},{EnemyAnimationStateNames.Get(state.fullPathHash)},{state.normalizedTime:F4},{animator.IsInTransition(0)},{clip},{state.speed * state.speedMultiplier:F4},{enemy.transform.eulerAngles.y:F3},{feet:F5},{horizontal:F3},{vertical:F3},{bridge.MuzzleAimErrorDegrees:F3},{actor.Navigation.MovementPhase},{actor.Navigation.AllowedSpeed:F4},{bridge.FootMotionDetected}"));
            }
            try
            {
                foreach (var wait in WaitEnumerable(.6f)) yield return wait;
                var goal = AnimationStart + Vector3.forward * 11;
                float until = Time.time + 8f;
                while (Time.time < until) { actor.Navigation.MoveTo(goal, EnemyMovePace.Walk, Time.time); Trace("SteadyCruise"); yield return null; }
                actor.Navigation.RequestStop(); until = Time.time + 2.8f;
                while (Time.time < until) { Trace("SteadyStop"); yield return null; }
                actor.Navigation.StopAndClearFacing(); brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(AnimationStart); enemy.transform.rotation = Quaternion.identity;
                brain.States.Change(EnemyStateId.Combat, "deceleration trace"); foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
                foreach (var pace in new[] { EnemyMovePace.Walk, EnemyMovePace.Run, EnemyMovePace.Sprint, EnemyMovePace.Run, EnemyMovePace.Walk })
                {
                    until = Time.time + 1.2f;
                    while (Time.time < until) { actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 22, pace, Time.time); actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime); Trace("Ramp" + pace); yield return null; }
                }
                actor.Navigation.RequestStop(); until = Time.time + 1.2f; while (Time.time < until) { Trace("RampStop"); yield return null; }
                foreach (bool crouch in new[] { false, true }) foreach (float angle in new[] { -175f, 175f })
                {
                    actor.Navigation.StopAndClearFacing(); brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(AnimationStart); enemy.transform.rotation = Quaternion.identity;
                    brain.States.Change(EnemyStateId.Combat, "left-right trace"); brain.Memory.Target = target; brain.Memory.Visible = brain.Memory.HasContact = true; player.transform.position = AnimationStart + Vector3.forward * 10; bridge.SetCrouching(crouch); foreach (var wait in WaitEnumerable(2.8f)) yield return wait;
                    actor.Navigation.Face(enemy.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * 10, Time.deltaTime);
                    until = Time.time + 3.5f; while (Time.time < until) { Trace((crouch ? "Crouch" : "Combat") + (angle < 0 ? "Left" : "Right")); yield return null; }
                }
                actor.Navigation.StopAndClearFacing(); brain.ResetForSpawn(); brain.enabled = false;
                brain.States.Change(EnemyStateId.Combat, "vertical aim trace"); brain.Memory.Target = target; brain.Memory.Visible = brain.Memory.HasContact = true;
                foreach (bool crouch in new[] { false, true }) foreach (float height in new[] { -1f, 0f, 1f }) foreach (float yaw in new[] { -25f, 0f, 25f })
                {
                    bridge.SetCrouching(crouch); until = Time.time + 1.5f;
                    while (Time.time < until) { player.transform.position = enemy.transform.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10 + Vector3.up * height; actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime); if (Time.time > until - .2f) Trace("Aim" + (crouch ? "Crouch" : "Stand") + height + "Y" + yaw); yield return null; }
                }
                PlayCheck(true, "Recorded current synchronization, left/right turns and signed horizontal/vertical aim without changing production tuning");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/synchronization-trace.csv", rows.ToString()); File.WriteAllText(EnemyTools.Evidence + "/synchronization-probe.txt", report.ToString()); Object.Destroy(enemy); Object.Destroy(player); }
        }

        private static IEnumerator SynchronizationSourceProbe(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            yield return null;
            enemy.GetComponent<EnemyBrain>().enabled = false;
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); bridge.enabled = false;
            enemy.GetComponent<EnemyRigPresenter>().enabled = false;
            var animator = bridge.Animator; var rig = animator.GetComponent<RigBuilder>();
            var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
            var bones = PoseBones.Where(b => animator.GetBoneTransform(b) != null).ToArray();
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            var footRows = new StringBuilder("Clip,Seconds,LeftX,LeftY,LeftZ,RightX,RightY,RightZ,HipsX,HipsY,HipsZ,MaxBoneStep\n");
            try
            {
                foreach (int mode in new[] { 0, 1, 2, 3 })
                {
                    rig.layers[0].active = (mode & 1) != 0; rig.layers[1].active = (mode & 2) != 0;
                    IEnumerator pose = SampleSourcePose(animator, rig, controller, SourceClip("Rifle Idle"), .5f); while (pose.MoveNext()) yield return pose.Current;
                    var before = bones.Select(b => animator.GetBoneTransform(b).rotation).ToArray();
                    var muzzleBefore = enemy.GetComponent<EnemyActor>().Muzzle.rotation;
                    pose = SampleSourcePose(animator, rig, controller, SourceClip("RifleRaise"), 0f); while (pose.MoveNext()) yield return pose.Current;
                    report.AppendLine("READINESS mode=" + mode + " (None/Aim/Hand/Both)");
                    for (int bone = 0; bone < bones.Length; bone++) report.AppendLine(FormattableString.Invariant($"  {bones[bone]} delta={Quaternion.Angle(before[bone], animator.GetBoneTransform(bones[bone]).rotation):F3}"));
                    report.AppendLine(FormattableString.Invariant($"  Muzzle delta={Quaternion.Angle(muzzleBefore, enemy.GetComponent<EnemyActor>().Muzzle.rotation):F3}"));
                }
                rig.layers[0].active = rig.layers[1].active = false;
                foreach (string name in new[] { "Rifle start walking", "Rifle stop walking", "Rifle Walk", "turn 180 left", "turn 180 right", "crouching turn 180 left", "crouching turn 180 right" })
                {
                    var clip = SourceClip(name); Quaternion[] previous = null; float maximum = 0, at = 0;
                    for (int frame = 0; frame <= 60; frame++)
                    {
                        IEnumerator pose = SampleSourcePose(animator, rig, controller, clip, frame / 60f); while (pose.MoveNext()) yield return pose.Current;
                        var rotations = bones.Select(b => animator.GetBoneTransform(b).rotation).ToArray();
                        float step = previous == null ? 0 : rotations.Select((q, i) => Quaternion.Angle(previous[i], q)).Max();
                        if (step > maximum) { maximum = step; at = clip.length * frame / 60f; }
                        previous = rotations;
                        Vector3 left = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position), right = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position), hips = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                        footRows.AppendLine(FormattableString.Invariant($"{name},{clip.length * frame / 60f:F4},{left.x:F4},{left.y:F4},{left.z:F4},{right.x:F4},{right.y:F4},{right.z:F4},{hips.x:F4},{hips.y:F4},{hips.z:F4},{step:F4}"));
                    }
                    report.AppendLine(FormattableString.Invariant($"SOURCE {name} length={clip.length:F4} maxBoneStep={maximum:F3} at={at:F4}"));
                }
                PlayCheck(true, "Compared readiness in all four authored rig modes and measured source foot phases and left/right continuity");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/synchronization-source.txt", report.ToString()); File.WriteAllText(EnemyTools.Evidence + "/synchronization-feet.csv", footRows.ToString()); rig.Clear(); Object.Destroy(enemy); Object.Destroy(controller); }
        }

        private static IEnumerator SynchronizationTests(GameLifetimeScope scope, EnemyWorld world)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var player = new GameObject("Synchronization acceptance target"); player.layer = 6;
            var body = player.AddComponent<CapsuleCollider>(); body.center = Vector3.up; body.height = 2f;
            player.AddComponent<Health>().Configure(1000000f, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); brain.enabled = false;
            var actor = enemy.GetComponent<EnemyActor>(); var bridge = enemy.GetComponent<EnemyAnimationBridge>();
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                foreach (var wait in WaitEnumerable(.6f)) yield return wait;
                Vector3 start = enemy.transform.position; float began = Time.time, until = began + 8f, longestFreeze = 0, frozenSince = -1, firstFeet = -1, travelAtFeet = -1;
                while (Time.time < until)
                {
                    actor.Navigation.MoveTo(start + Vector3.forward * 12, EnemyMovePace.Walk, Time.time);
                    if (bridge.FootMotionDetected && firstFeet < 0 && StateMatches(bridge.Animator, "SteadyStartWalk")) { firstFeet = Time.time - began; travelAtFeet = Vector3.Distance(start, enemy.transform.position); }
                    if (actor.Navigation.Velocity.magnitude > .2f && !bridge.FootMotionDetected) { if (frozenSince < 0) frozenSince = Time.time; longestFreeze = Mathf.Max(longestFreeze, Time.time - frozenSince); } else frozenSince = -1;
                    yield return null;
                }
                PlayCheck(firstFeet >= 0 && firstFeet < .3f && travelAtFeet < .08f, "Steady visual response precedes meaningful world displacement");
                PlayCheck(longestFreeze < .25f && bridge.Animator.GetCurrentAnimatorStateInfo(0).IsName("SteadyWalk"), "Eight seconds of actual Steady movement never freeze the locomotion pose");
                report.AppendLine(FormattableString.Invariant($"START firstFeet={firstFeet:F4}s worldTravel={travelAtFeet:F4}m maximumMovingFreeze={longestFreeze:F4}s"));
                start = enemy.transform.position; began = Time.time; actor.Navigation.RequestStop();
                PlayCheck(actor.Navigation.MovementPhase == EnemyMovementPhase.Stopping && actor.Navigation.Velocity.magnitude > .2f, "Ordinary Steady stop starts while the body still moves");
                float zeroAt = -1, finalFootAt = 0, midSpeed = -1; until = Time.time + 2f;
                while (Time.time < until)
                {
                    if (zeroAt < 0 && actor.Navigation.Velocity.magnitude < .03f) zeroAt = Time.time - began;
                    if (bridge.FootMotionDetected && StateMatches(bridge.Animator, "SteadyStopWalk")) finalFootAt = Time.time - began;
                    if (midSpeed < 0 && Time.time > began + .5f) midSpeed = actor.Navigation.Velocity.magnitude;
                    yield return null;
                }
                report.AppendLine(FormattableString.Invariant($"STOP observed zeroAt={zeroAt:F4}s finalFeet={finalFootAt:F4}s drift={Vector3.Distance(start, enemy.transform.position):F4}m middleSpeed={midSpeed:F4} state={EnemyAnimationStateNames.Get(bridge.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash)}"));
                PlayCheck(midSpeed > .1f && zeroAt > .7f && zeroAt < 1.3f, "World deceleration spans the meaningful authored stopping phase");
                PlayCheck(finalFootAt <= zeroAt + .35f && Vector3.Distance(start, enemy.transform.position) < .7f, "Final stopping feet and world rest agree without long drift");
                PlayCheck(actor.Navigation.MovementPhase == EnemyMovementPhase.Idle && StateMatches(bridge.Animator, "SteadyIdle"), "Ordinary stop settles navigation and presentation to Idle");
                report.AppendLine(FormattableString.Invariant($"STOP zeroAt={zeroAt:F4}s finalFeet={finalFootAt:F4}s drift={Vector3.Distance(start, enemy.transform.position):F4}m middleSpeed={midSpeed:F4}"));
                actor.Navigation.MoveTo(enemy.transform.position + Vector3.forward * 4, EnemyMovePace.Walk, Time.time); foreach (var wait in WaitEnumerable(.5f)) yield return wait;
                Vector3 cancelledAt = enemy.transform.position;
                actor.Navigation.Stop();
                PlayCheck(enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped && actor.Navigation.MovementPhase == EnemyMovementPhase.Idle, "Hard cancellation immediately clears the movement phase and stops the agent");
                yield return null;
                PlayCheck(actor.Navigation.Velocity.sqrMagnitude < .0001f && Vector3.Distance(cancelledAt, enemy.transform.position) < .005f, "Hard cancellation has no physical drift at the next navigation update");
                brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(AnimationStart); enemy.transform.rotation = Quaternion.identity;
                brain.States.Change(EnemyStateId.Combat, "continuous speed check"); foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
                foreach (var pace in new[] { EnemyMovePace.Sprint, EnemyMovePace.Run, EnemyMovePace.Walk })
                {
                    float rawBefore = bridge.MoveSpeedRaw;
                    actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 24, pace, Time.time);
                    foreach (var wait in WaitEnumerable(.025f)) yield return wait;
                    if (pace != EnemyMovePace.Sprint) PlayCheck(Mathf.Abs(bridge.MoveSpeedRaw - rawBefore) < .10f, "Tier request does not snap the continuous world-speed blend: " + pace);
                    until = Time.time + 1f; while (Time.time < until) { actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 24, pace, Time.time); actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime); yield return null; }
                }
                actor.Navigation.RequestStop(); foreach (var wait in WaitEnumerable(.7f)) yield return wait;
                PlayCheck(actor.Navigation.Velocity.sqrMagnitude < .001f && bridge.MoveSpeedSmoothed < .02f, "Ordinary Combat stop settles world motion and continuous blend");
                foreach (var pace in new[] { EnemyMovePace.Sprint, EnemyMovePace.Run, EnemyMovePace.Walk, EnemyMovePace.Crouch })
                {
                    brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(AnimationStart); enemy.transform.rotation = Quaternion.identity;
                    brain.States.Change(EnemyStateId.Combat, "direct ordinary stop"); bridge.SetCrouching(pace == EnemyMovePace.Crouch);
                    foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
                    until = Time.time + 2f;
                    while (Time.time < until) { actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 24, pace, Time.time); yield return null; }
                    float initialSpeed = actor.Navigation.Velocity.magnitude;
                    actor.Navigation.RequestStop();
                    float acceleration = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().acceleration;
                    float lastStoppingSpeed = initialSpeed, previousSpeed = initialSpeed, previousTime = Time.time, maximumDrop = 0, excessDrop = 0;
                    until = Time.time + .8f;
                    while (Time.time < until)
                    {
                        yield return null;
                        if (Time.time <= previousTime) continue;
                        float speed = actor.Navigation.Velocity.magnitude;
                        float drop = Mathf.Max(0f, previousSpeed - speed);
                        maximumDrop = Mathf.Max(maximumDrop, drop);
                        excessDrop = Mathf.Max(excessDrop, drop - acceleration * Mathf.Max(.025f, Time.time - previousTime));
                        if (actor.Navigation.MovementPhase == EnemyMovementPhase.Stopping) lastStoppingSpeed = speed;
                        previousSpeed = speed; previousTime = Time.time;
                    }
                    report.AppendLine(FormattableString.Invariant($"DIRECT STOP {pace}: initial={initialSpeed:F4} acceleration={acceleration:F4} lastStopping={lastStoppingSpeed:F4} maxDrop={maximumDrop:F4} excessDrop={excessDrop:F4} final={actor.Navigation.Velocity.magnitude:F4}"));
                    PlayCheck(initialSpeed > .5f && lastStoppingSpeed < Mathf.Max(.3f, initialSpeed * .08f) && excessDrop < .08f, "Ordinary " + pace + " stop reaches near-zero speed before ending its braking phase, without a final velocity cut");
                    PlayCheck(actor.Navigation.MovementPhase == EnemyMovementPhase.Idle && actor.Navigation.Velocity.sqrMagnitude < .001f, "Direct " + pace + " stop settles the authoritative navigation body");
                }
                float verticalBefore = 0, verticalAfter = 0, horizontalBefore = 0, horizontalAfter = 0, totalBefore = 0, totalAfter = 0; int count = 0;
                foreach (bool crouch in new[] { true, false }) foreach (bool fire in new[] { true, false }) foreach (float pitch in new[] { -3f, 0f, 3f }) foreach (float yaw in new[] { 25f, 0f, -25f })
                {
                    var errors = new float[2, 3]; var shotsByRun = new int[2];
                    for (int correction = 0; correction < 2; correction++)
                    {
                        // Restart both paired runs at the same stance and gameplay Fire phase.
                        // Sequential Fire windows otherwise compare different recoil phases.
                        brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(AnimationStart); enemy.transform.rotation = Quaternion.identity;
                        brain.States.Change(EnemyStateId.Combat, "paired vertical aim"); bridge.SetCrouching(crouch); brain.Memory.Target = target; brain.Memory.Visible = brain.Memory.HasContact = true;
                        var serialized = new SerializedObject(bridge); serialized.FindProperty("_config.<CoarseAimPitchLimit>k__BackingField").floatValue = correction == 0 ? 0 : 18f; serialized.ApplyModifiedPropertiesWithoutUndo();
                        until = Time.time + 2.5f;
                        while (Time.time < until)
                        {
                            Vector3 chest = bridge.Animator.GetBoneTransform(HumanBodyBones.Chest).position;
                            player.transform.position = chest + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10 + Vector3.up * (Mathf.Tan(pitch * Mathf.Deg2Rad) * 10) - (target.AimPosition - player.transform.position);
                            Physics.SyncTransforms(); yield return null;
                        }
                        int shotsBefore = brain.Combat.Ammo;
                        until = Time.time + .7f; int samples = 0; float next = 0;
                        while (Time.time < until)
                        {
                            Vector3 chest = bridge.Animator.GetBoneTransform(HumanBodyBones.Chest).position;
                            player.transform.position = chest + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10 + Vector3.up * (Mathf.Tan(pitch * Mathf.Deg2Rad) * 10) - (target.AimPosition - player.transform.position);
                            actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime); Physics.SyncTransforms();
                            if (fire) { brain.Combat.Attack(target, Time.time); brain.Combat.Tick(Time.time); }
                            yield return null;
                            if (Time.time > until - .25f && Time.time >= next) { next = Time.time + .02f; samples++; errors[correction, 0] += Mathf.Abs(bridge.HorizontalMuzzleErrorDegrees); errors[correction, 1] += Mathf.Abs(bridge.VerticalMuzzleErrorDegrees); errors[correction, 2] += bridge.MuzzleAimErrorDegrees; }
                        }
                        for (int axis = 0; axis < 3; axis++) errors[correction, axis] /= Mathf.Max(1, samples);
                        shotsByRun[correction] = shotsBefore - brain.Combat.Ammo;
                    }
                    report.AppendLine(FormattableString.Invariant($"OBSERVED AIM crouch={crouch} fire={fire} pitch={pitch} yaw={yaw}: H={errors[0, 0]:F3}->{errors[1, 0]:F3} V={errors[0, 1]:F3}->{errors[1, 1]:F3} Total={errors[0, 2]:F3}->{errors[1, 2]:F3} realShots={shotsByRun[0]}/{shotsByRun[1]}"));
                    PlayCheck(errors[1, 1] < errors[0, 1] * .7f && errors[1, 1] < 8f, "Pitch correction reduces vertical barrel error: crouch=" + crouch + "/fire=" + fire + "/pitch=" + pitch + "/yaw=" + yaw);
                    PlayCheck(errors[1, 0] <= errors[0, 0] + 2f && errors[1, 2] <= errors[0, 2] + 2f, "Pitch correction preserves horizontal alignment and total aim");
                    if (fire) PlayCheck(shotsByRun[0] > 0 && shotsByRun[1] > 0, "Aim/Fire comparison contains real gameplay shots in both matched runs");
                    horizontalBefore += errors[0, 0]; horizontalAfter += errors[1, 0]; verticalBefore += errors[0, 1]; verticalAfter += errors[1, 1]; totalBefore += errors[0, 2]; totalAfter += errors[1, 2]; count++;
                    report.AppendLine(FormattableString.Invariant($"AIM crouch={crouch} fire={fire} pitch={pitch} yaw={yaw}: H={errors[0, 0]:F3}->{errors[1, 0]:F3} V={errors[0, 1]:F3}->{errors[1, 1]:F3} Total={errors[0, 2]:F3}->{errors[1, 2]:F3}"));
                }
                report.AppendLine(FormattableString.Invariant($"MEAN {count} cases: H={horizontalBefore/count:F3}->{horizontalAfter/count:F3} V={verticalBefore/count:F3}->{verticalAfter/count:F3} Total={totalBefore/count:F3}->{totalAfter/count:F3}"));
                PlayCheck(verticalAfter < verticalBefore * .5f, "Vertical correction halves average barrel divergence across heights, angles, standing/crouch and real Fire");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/synchronization-acceptance.txt", report.ToString()); Object.Destroy(enemy); Object.Destroy(player); }
        }
    }
}
