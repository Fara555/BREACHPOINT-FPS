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
    }
}
