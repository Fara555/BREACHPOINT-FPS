using System;
using System.Collections;
using System.IO;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private static IEnumerator SteadyMovementProbe(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); brain.enabled = false;
            var actor = enemy.GetComponent<EnemyActor>(); var bridge = enemy.GetComponent<EnemyAnimationBridge>();
            var left = bridge.Animator.GetBoneTransform(HumanBodyBones.LeftFoot); var right = bridge.Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                foreach (var wait in WaitEnumerable(.5f)) yield return wait;
                Vector3 leftStart = enemy.transform.InverseTransformPoint(left.position), rightStart = enemy.transform.InverseTransformPoint(right.position), start = enemy.transform.position;
                float began = Time.time, footAt = -1, distanceAt = -1;
                int lastFrame = -1;
                while (Time.time < began + 1.4f)
                {
                    actor.Navigation.MoveTo(start + Vector3.forward * 8, EnemyMovePace.Walk, Time.time);
                    if (Time.frameCount != lastFrame)
                    {
                        lastFrame = Time.frameCount;
                        float feet = Vector3.Distance(leftStart, enemy.transform.InverseTransformPoint(left.position)) + Vector3.Distance(rightStart, enemy.transform.InverseTransformPoint(right.position));
                        if (footAt < 0 && feet > .025f) { footAt = Time.time - began; distanceAt = Vector3.Distance(start, enemy.transform.position); }
                        report.AppendLine(FormattableString.Invariant($"{Time.time - began:F3}s travel={Vector3.Distance(start, enemy.transform.position):F4} speed={actor.Navigation.Velocity.magnitude:F3} feetMotion={feet:F4} state={EnemyAnimationStateNames.Get(bridge.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash)}"));
                    }
                    yield return null;
                }
                report.AppendLine(FormattableString.Invariant($"FIRST FOOT RESPONSE: {footAt:F3}s after {distanceAt:F4}m translation"));
                PlayCheck(footAt >= 0 && footAt < .25f && distanceAt < .08f, "Steady foot movement begins before noticeable idle translation");
                actor.Navigation.Stop(); foreach (var wait in WaitEnumerable(2.8f)) yield return wait;
                PlayCheck(StateMatches(bridge.Animator, "Base Layer.SteadyIdle"), "Steady start/walk/stop recovers to Idle with current manual exit times");
                actor.Navigation.Face(enemy.transform.position + Vector3.right * 10, Time.deltaTime);
                foreach (var wait in WaitEnumerable(3.2f)) yield return wait;
                var rig = bridge.Animator.GetComponent<RigBuilder>();
                PlayCheck(!actor.Navigation.IsTurning && rig.graph.GetOutput(1).GetWeight() > .99f, "Passive turn restores AimRig after authored footwork finishes");
                brain.ResetForSpawn(); brain.enabled = false; brain.States.Change(EnemyStateId.Combat, "turn during stance probe");
                foreach (var wait in WaitEnumerable(1.4f)) yield return wait;
                bridge.SetCrouching(true); foreach (var wait in WaitEnumerable(.25f)) yield return wait;
                Vector3 beforeFacing = enemy.transform.forward, wantedFacing = Quaternion.Euler(0, 90, 0) * beforeFacing;
                actor.Navigation.Face(enemy.transform.position + wantedFacing * 10, Time.deltaTime);
                float turnDeadline = Time.time + 4f, waitingYaw = 0f; bool observedTurn = false;
                while (Time.time < turnDeadline)
                {
                    if (bridge.Animator.GetCurrentAnimatorStateInfo(0).IsTag("Stance")) waitingYaw = Mathf.Max(waitingYaw, Vector3.Angle(beforeFacing, enemy.transform.forward));
                    if (actor.Navigation.IsTurning && HasTurnPose(bridge.Animator)) observedTurn = true;
                    yield return null;
                }
                PlayCheck(waitingYaw < 3f, "Pending facing preserves world heading during authored stance playback");
                PlayCheck(observedTurn && !actor.Navigation.IsTurning && Vector3.Angle(enemy.transform.forward, wantedFacing) < 4f, "Turn requested during stance plays authored footwork and reaches its heading");
                brain.ResetForSpawn(); brain.enabled = false; brain.States.Change(EnemyStateId.Combat, "changed pending heading probe");
                foreach (var wait in WaitEnumerable(1.4f)) yield return wait;
                bridge.SetCrouching(true); foreach (var wait in WaitEnumerable(.25f)) yield return wait;
                beforeFacing = enemy.transform.forward;
                actor.Navigation.Face(enemy.transform.position + Quaternion.Euler(0, 90, 0) * beforeFacing * 10, Time.deltaTime);
                foreach (var wait in WaitEnumerable(.1f)) yield return wait;
                wantedFacing = Quaternion.Euler(0, 10, 0) * beforeFacing;
                actor.Navigation.Face(enemy.transform.position + wantedFacing * 10, Time.deltaTime);
                foreach (var wait in WaitEnumerable(3f)) yield return wait;
                PlayCheck(!actor.Navigation.IsTurning && Vector3.Angle(enemy.transform.forward, wantedFacing) < 4f, "A small updated heading cancels an obsolete queued large turn");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/steady-start-probe.txt", report.ToString()); Object.Destroy(enemy); }
        }

        private static IEnumerator FocusedMovementReview(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            if (index >= 7 && index <= 10)
            {
                IEnumerator turn = ReviewCase(index - 3, enemy, brain, actor, bridge, target);
                while (turn.MoveNext()) yield return turn.Current;
                yield break;
            }
            if (index >= 11) brain.States.Change(EnemyStateId.Combat, "focused movement review");
            if (index >= 25 && index <= 28 || index == 33) bridge.SetCrouching(true);
            foreach (var wait in WaitReview(1.8f)) yield return wait;
            if (index == 0) { foreach (var wait in WaitReview(3f)) yield return wait; yield break; }
            if (index >= 29 && index <= 33)
            {
                for (int phase = 0; phase < 2; phase++)
                {
                    float yaw = index == 29 ? 0 : index == 30 ? -25 : index == 31 ? 25 : phase == 0 ? -25 : 25;
                    float until = Time.time + 2.5f;
                    while (Time.time < until)
                    {
                        target.transform.position = enemy.transform.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10;
                        actor.Navigation.Face(target.AimPosition, Time.deltaTime);
                        if (index == 32) { brain.Combat.Attack(target, Time.time); brain.Combat.Tick(Time.time); }
                        yield return null;
                    }
                }
                yield break;
            }
            int phases = index == 6 ? 4 : index >= 18 && index <= 24 ? 2 : 1;
            for (int phase = 0; phase < phases; phase++)
            {
                EnemyMovePace pace = index >= 25 && index <= 28 ? EnemyMovePace.Crouch : index >= 15 && index <= 17 || index >= 22 && index <= 24 ? EnemyMovePace.Run : EnemyMovePace.Walk;
                if (index >= 18 && index <= 21)
                    pace = index == 18 ? (phase == 0 ? EnemyMovePace.Walk : EnemyMovePace.Run) : index == 19 ? (phase == 0 ? EnemyMovePace.Run : EnemyMovePace.Walk) : index == 20 ? (phase == 0 ? EnemyMovePace.Run : EnemyMovePace.Sprint) : (phase == 0 ? EnemyMovePace.Sprint : EnemyMovePace.Run);
                Vector3 direction = index == 12 || index == 16 || index == 26 ? Vector3.left : index == 13 || index == 17 || index == 27 ? Vector3.right : index == 14 ? new Vector3(-1, 0, 1).normalized : Vector3.forward;
                if (index == 22) direction = phase == 0 ? Vector3.left : Vector3.right;
                if (index == 23) direction = phase == 0 ? Vector3.forward : Vector3.left;
                if (index == 24) direction = phase == 0 ? Vector3.left : new Vector3(-1, 0, 1).normalized;
                float distance = index == 34 ? 2f : index == 37 ? 15f : index == 38 ? 7f : 9f;
                if (index >= 34) pace = EnemyTacticalController.MovementPace(index == 37 ? EnemyTacticalIntent.FlankLeft : index == 38 ? EnemyTacticalIntent.Fallback : index == 36 ? EnemyTacticalIntent.TakeCover : EnemyTacticalIntent.Reposition, distance);
                Vector3 goal = enemy.transform.position + direction * distance;
                float until = Time.time + (index == 6 ? .65f : index == 1 ? .7f : index == 2 ? 1.5f : index == 3 ? 10f : 2.2f);
                while (Time.time < until)
                {
                    actor.Navigation.MoveTo(goal, pace, Time.time);
                    if (index >= 11) actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime);
                    yield return null;
                }
                if (index <= 6 || index == 28)
                {
                    actor.Navigation.RequestStop();
                    if (index == 6)
                    {
                        foreach (var wait in WaitReview(.45f)) yield return wait;
                        PlayCheck(StateMatches(bridge.Animator, "Base Layer.SteadyIdle") && actor.Navigation.Velocity.magnitude < .03f,
                            "Early Steady cancellation returns to Idle without a full stopping stride: repeat " + (phase + 1));
                        foreach (var wait in WaitReview(2.05f)) yield return wait;
                    }
                    else foreach (var wait in WaitReview(2.5f)) yield return wait;
                }
            }
            actor.Navigation.RequestStop(); foreach (var wait in WaitReview(2.5f)) yield return wait;
        }

        private static IEnumerator MovementPolishTests(GameLifetimeScope scope, EnemyWorld world)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var player = new GameObject("Movement polish aim target"); player.layer = 6;
            player.AddComponent<Health>().Configure(1000000f, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); var actor = enemy.GetComponent<EnemyActor>();
            var bridge = enemy.GetComponent<EnemyAnimationBridge>();
            brain.enabled = false;
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                foreach (var pace in new[] { EnemyMovePace.Walk, EnemyMovePace.Run, EnemyMovePace.Crouch })
                foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.forward, new Vector3(-1, 0, 1).normalized })
                {
                    actor.Navigation.StopAndClearFacing(); brain.ResetForSpawn(); brain.enabled = false;
                    brain.States.Change(EnemyStateId.Combat, "directional start probe"); bridge.SetCrouching(pace == EnemyMovePace.Crouch);
                    foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
                    Vector3 start = enemy.transform.position, goal = start + direction * 8;
                    float began = Time.time, directionAt = -1, distanceAt = -1;
                    while (Time.time < began + .8f)
                    {
                        actor.Navigation.MoveTo(goal, pace, Time.time); actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime);
                        Vector2 actual = new Vector2(bridge.Animator.GetFloat("MoveX"), bridge.Animator.GetFloat("MoveY"));
                        if (directionAt < 0 && actual.magnitude > .03f && Vector2.Dot(actual.normalized, new Vector2(direction.x, direction.z)) > .95f)
                        { directionAt = Time.time - began; distanceAt = Vector3.Distance(start, enemy.transform.position); }
                        yield return null;
                    }
                    report.AppendLine(FormattableString.Invariant($"START {pace}/{direction}: response={directionAt:F3}s travelBeforeDirection={distanceAt:F3}m tier={actor.Navigation.DesiredMovementTier} actualSpeed={actor.Navigation.Velocity.magnitude:F3} blend={bridge.MoveSpeedSmoothed:F3}"));
                    PlayCheck(directionAt >= 0 && directionAt < .18f && distanceAt < .08f, "Directional presentation precedes noticeable translation: " + pace + "/" + direction);
                    float before = bridge.MoveSpeedSmoothed; actor.Navigation.Stop(); yield return null;
                    PlayCheck(bridge.MoveSpeedSmoothed > 0 && bridge.MoveSpeedSmoothed <= before + .02f, "Stop retains short continuous visual decay: " + pace);
                    foreach (var wait in WaitEnumerable(.8f)) yield return wait;
                    PlayCheck(bridge.MoveSpeedSmoothed < .01f && actor.Navigation.Velocity.sqrMagnitude < .001f, "Stop settles without gameplay drift: " + pace);
                }
                actor.Navigation.StopAndClearFacing(); brain.ResetForSpawn(); brain.enabled = false;
                brain.States.Change(EnemyStateId.Combat, "aim correction probe");
                brain.Memory.Target = target; brain.Memory.Visible = true; brain.Memory.HasContact = true;
                float baselineTotal = 0, correctedTotal = 0;
                for (int pose = 0; pose < 7; pose++)
                foreach (float yaw in new[] { 0f, -25f, 25f })
                {
                    bridge.SetCrouching(pose >= 4);
                    foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                    float[] errors = new float[2];
                    for (int correction = 0; correction < 2; correction++)
                    {
                        var data = new SerializedObject(bridge);
                        data.FindProperty("_config.<CoarseAimWeight>k__BackingField").floatValue = correction == 0 ? 0 : .8f;
                        data.ApplyModifiedPropertiesWithoutUndo();
                        float until = Time.time + .65f, samples = 0, total = 0;
                        while (Time.time < until)
                        {
                            player.transform.position = enemy.transform.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10;
                            if (pose == 1 || pose == 2 || pose == 5)
                                actor.Navigation.MoveTo(enemy.transform.position + Vector3.forward * 8, pose == 2 ? EnemyMovePace.Run : pose == 5 ? EnemyMovePace.Crouch : EnemyMovePace.Walk, Time.time);
                            else actor.Navigation.Stop();
                            actor.Navigation.Face(enemy.transform.position + Vector3.forward * 20, Time.deltaTime);
                            if (pose == 3 || pose == 6) { brain.Combat.Attack(target, Time.time); brain.Combat.Tick(Time.time); }
                            yield return null;
                            if (Time.time > until - .3f) { total += bridge.MuzzleAimErrorDegrees; samples++; }
                        }
                        errors[correction] = total / Mathf.Max(1, samples);
                    }
                    baselineTotal += errors[0]; correctedTotal += errors[1];
                    PlayCheck(Vector3.Distance(bridge.AimTarget.position, target.AimPosition) < .25f, "AimTarget follows actual target after Animator evaluation: pose=" + pose + "/yaw=" + yaw);
                    PlayCheck(errors[1] <= errors[0] + 2f, "Coarse correction does not introduce severe pose-specific aim offset: pose=" + pose + "/yaw=" + yaw);
                    report.AppendLine(FormattableString.Invariant($"AIM runtime pose={pose} yaw={yaw:F0} body={bridge.BodyAimErrorDegrees:F2} intendedBody={Vector3.Angle(enemy.transform.forward, Vector3.ProjectOnPlane(target.AimPosition - enemy.transform.position, Vector3.up)):F2} targetError={Vector3.Distance(bridge.AimTarget.position, target.AimPosition):F3} visible={brain.Memory.Visible} alive={target.IsAlive} muzzleBefore={errors[0]:F2} muzzleAfter={errors[1]:F2}"));
                }
                PlayCheck(correctedTotal < baselineTotal * .9f, "Bounded coarse yaw reduces average weapon aim error across idle / walk / run / Fire / crouch poses");
                PlayCheck(EnemyTacticalController.MovementPace(EnemyTacticalIntent.Reposition, 2) == EnemyMovePace.Walk && EnemyTacticalController.MovementPace(EnemyTacticalIntent.Reposition, 10) == EnemyMovePace.Run, "Short reposition Walk, ordinary long reposition Run");
                PlayCheck(EnemyTacticalController.MovementPace(EnemyTacticalIntent.FlankLeft, 8) == EnemyMovePace.Run && EnemyTacticalController.MovementPace(EnemyTacticalIntent.FlankLeft, 15) == EnemyMovePace.Sprint && EnemyTacticalController.MovementPace(EnemyTacticalIntent.Fallback, 7) == EnemyMovePace.Sprint, "Sprint reserved for long flank / urgent fallback");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/movement-polish.txt", report.ToString()); Object.Destroy(enemy); Object.Destroy(player); }
        }
        // Sample the actual Humanoid/rig stream; never change source imports or the controller.
        private static IEnumerator MovementPoseAudit(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            yield return null;
            enemy.GetComponent<EnemyBrain>().enabled = false;
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); bridge.enabled = false;
            enemy.GetComponent<EnemyRigPresenter>().enabled = false;
            var animator = bridge.Animator;
            var rig = animator.GetComponent<RigBuilder>(); rig.Clear();
            var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = controller;
            var aim = (Transform)new SerializedObject(bridge).FindProperty("_aimTarget").objectReferenceValue;
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                foreach (string clipName in new[] { "idle aiming", "walk forward", "walk left", "run forward", "Fire", "idle crouching", "walk crouching left", "Fire Crouch" })
                foreach (float yaw in new[] { 0f, -25f, 25f })
                {
                    rig.layers[0].active = rig.layers[1].active = true;
                    aim.position = enemy.transform.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10 + Vector3.up * (clipName.IndexOf("crouch", StringComparison.OrdinalIgnoreCase) >= 0 ? 1.05f : 1.5f);
                    IEnumerator sample = SampleSourcePose(animator, rig, controller, SourceClip(clipName), .25f);
                    while (sample.MoveNext()) yield return sample.Current;
                    var muzzle = enemy.GetComponent<EnemyActor>().Muzzle;
                    Vector3 direction = aim.position - muzzle.position;
                    report.AppendLine(FormattableString.Invariant($"AIM {clipName} targetYaw={yaw:F0} body={Vector3.Angle(enemy.transform.forward, Vector3.ProjectOnPlane(direction, Vector3.up)):F2} muzzle={Vector3.Angle(muzzle.forward, direction):F2} signedYaw={Vector3.SignedAngle(Vector3.ProjectOnPlane(muzzle.forward, Vector3.up), Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up):F2}"));
                }
                foreach (string suffix in new[] { "90 left", "90 right", "180 left", "180 right" })
                {
                    var clip = SourceClip("Steady rifle turn " + suffix);
                    rig.layers[0].active = rig.layers[1].active = false;
                    Vector3 previous = Vector3.zero, firstHeading = Vector3.zero, lastHeading = Vector3.zero; float pelvisYaw = 0; Vector3 previousHeading = Vector3.zero; float travel = 0, minY = float.MaxValue, maxY = float.MinValue;
                    for (int frame = 0; frame <= 20; frame++)
                    {
                        IEnumerator sample = SampleSourcePose(animator, rig, controller, clip, frame / 20f);
                        while (sample.MoveNext()) yield return sample.Current;
                        var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                        Vector3 across = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                        lastHeading = Vector3.Cross(across, Vector3.up).normalized;
                        if (frame == 0) firstHeading = lastHeading; else pelvisYaw += Vector3.SignedAngle(previousHeading, lastHeading, Vector3.up); previousHeading = lastHeading;
                        Vector3 local = hips.InverseTransformPoint(foot.position);
                        if (frame > 0) travel += Vector3.Distance(previous, local);
                        previous = local; minY = Mathf.Min(minY, foot.position.y); maxY = Mathf.Max(maxY, foot.position.y);
                    }
                    report.AppendLine(FormattableString.Invariant($"SOURCE {clip.name} leftFootRelativeTravel={travel:F3} footLift={maxY - minY:F3} pelvisYaw={pelvisYaw:F2} length={clip.length:F3}"));
                }
                PlayCheck(true, "Measured current source Steady footwork and aim offsets across eight poses / three targets");
            }
            finally
            {
                File.WriteAllText(EnemyTools.Evidence + "/movement-pose-audit.txt", report.ToString());
                rig.Clear(); Object.Destroy(enemy); Object.Destroy(controller);
            }
        }
    }
}
