using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEngine;
using VContainer;
namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamPresentationIntegration
    {
        private static IEnumerator RiflemanPerformanceTests(GameLifetimeScope scope, EnemyWorld world)
        {
            var lines = new List<string> { "Agents,Seconds,Frames,Shots,PerceptionChecks,TacticalDecisions,Repaths,CoverPathQueries,MeasuredDecisions,MeanDecisionMs,DecisionBytes,MedianEditorFrameMs,P95EditorFrameMs" };
            foreach (int count in new[] { 1, 3, 10, 30 })
            {
                using (var fixture = new RiflemanTacticalFixture(scope, world, count, "Measured" + count))
                {
                    yield return null;
                    foreach (var brain in fixture.Brains) brain.Tactics.MeasureDecisionCost = true;
                    foreach (var wait in Enumerate(WatchTactical(fixture, 2f))) yield return wait;
                    int checks = 0, decisions = 0, repaths = 0, measured = 0, shots = fixture.Shots;
                    long bytes = 0; double milliseconds = 0;
                    SumPerformance(fixture, ref checks, ref decisions, ref repaths, ref measured, ref bytes, ref milliseconds);
                    int paths = fixture.Covers.PathQueries, frame = -1;
                    var frames = new List<float>(2048);
                    float began = Time.time, until = began + 8;
                    while (Time.time < until)
                    {
                        fixture.Observe();
                        if (Time.frameCount != frame) { frame = Time.frameCount; frames.Add(Time.unscaledDeltaTime * 1000); }
                        yield return null;
                    }
                    int endChecks = 0, endDecisions = 0, endRepaths = 0, endMeasured = 0;
                    long endBytes = 0; double endMilliseconds = 0;
                    SumPerformance(fixture, ref endChecks, ref endDecisions, ref endRepaths, ref endMeasured, ref endBytes, ref endMilliseconds);
                    frames.Sort();
                    PlayCheck(frames.Count > 10 && fixture.Shots > shots, "Measured active combat with " + count + " agents");
                    foreach (var brain in fixture.Brains)
                        PlayCheck(brain.GetComponent<EnemyAnimationBridge>().Animator.cullingMode == AnimatorCullingMode.AlwaysAnimate && !brain.GetComponent<EnemyDebugView>().Settings.Enabled, "Gameplay muzzle updates offscreen; debug visualization disabled");
                    var culture = System.Globalization.CultureInfo.InvariantCulture;
                    double mean = endMeasured > measured ? (endMilliseconds - milliseconds) / (endMeasured - measured) : 0;
                    string row = string.Join(",", count, (Time.time - began).ToString("F3", culture), frames.Count, fixture.Shots - shots, endChecks - checks, endDecisions - decisions, endRepaths - repaths, fixture.Covers.PathQueries - paths, endMeasured - measured, mean.ToString("F4", culture), endBytes - bytes, frames[frames.Count / 2].ToString("F2", culture), frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * 0.95f))].ToString("F2", culture));
                    lines.Add(row); AppendResult("MEASUREMENT " + row);
                }
                yield return null;
            }
            File.WriteAllLines(RiflemanRework.Evidence + "/performance.csv", lines);
        }
        private static void SumPerformance(RiflemanTacticalFixture fixture, ref int checks, ref int decisions, ref int repaths, ref int measured, ref long bytes, ref double milliseconds)
        {
            foreach (var brain in fixture.Brains)
            {
                checks += brain.PerceptionCheckCount; decisions += brain.Tactics.DecisionCount;
                repaths += brain.GetComponent<EnemyActor>().Navigation.RepathCount;
                measured += brain.Tactics.MeasuredDecisions; bytes += brain.Tactics.DecisionAllocatedBytes; milliseconds += brain.Tactics.DecisionMilliseconds;
            }
        }
        private static IEnumerator RiflemanEdgeTests(GameLifetimeScope scope, EnemyWorld world)
        {
            using (var fixture = new RiflemanTacticalFixture(scope, world, 3, "LifecycleEdges"))
            {
                yield return null;
                foreach (var wait in Enumerate(WatchTactical(fixture, 1.5f))) yield return wait;
                var brain = fixture.Brains[0]; var actor = fixture.Actors[0];
                var debug = brain.GetComponent<EnemyDebugView>();
                PlayCheck(!debug.Settings.Enabled && debug.Settings.Logs == EnemyDebugCategory.None, "Every debug category starts disabled");
                var squadBefore = brain.Tactics.Squad;
                actor.ConfigureSquad("IsolatedEdges");
                foreach (var wait in Enumerate(WatchTactical(fixture, 0.4f))) yield return wait;
                PlayCheck(brain.Tactics.Squad != squadBefore && brain.Tactics.Squad.Members.Count == 1 && squadBefore.Members.Count == 2, "Changing squad ID releases old membership and creates autonomous single-member group");
                brain.Stun(0.4f);
                PlayCheck(!brain.Tactics.Member.Shooter && !brain.Tactics.Member.Mover && brain.Tactics.CurrentCover == null, "Stun interrupts tactical resources immediately");
                foreach (var wait in Enumerate(WatchTactical(fixture, 0.8f))) yield return wait;
                PlayCheck(brain.States.Current != EnemyStateId.Stunned && brain.Tactics.DecisionCount > 0, "Enemy resumes autonomous decisions after stun");
                int shotsBefore = fixture.Shots;
                brain.GetComponent<EnemyAnimationBridge>().enabled = false;
                brain.GetComponent<EnemyVfxPresenter>().enabled = false;
                foreach (var wait in Enumerate(WatchTactical(fixture, 2f))) yield return wait;
                PlayCheck(fixture.Shots > shotsBefore && !actor.Health.IsDead, "Optional presentation can be disabled while gameplay continues");
                actor.Health.TakeDamage(new DamageInfo(100000, actor.transform.position, Vector3.back, fixture.Target.gameObject));
                int shotsAtDeath = fixture.Shots; int deadAmmo = brain.Combat.Ammo;
                for (int i = 0; i < 4; i++)
                {
                    brain.Combat.Attack(fixture.Target, Time.time + i * 10);
                    brain.Combat.AttackSuppression(fixture.Target.AimPosition, fixture.Target, Time.time + i * 10);
                    PlayCheck(!brain.Combat.RequestReload(Time.time + i * 10), "Dead source rejects queued reload " + i);
                    actor.Health.TakeDamage(new DamageInfo(100, actor.transform.position, Vector3.back, fixture.Target.gameObject));
                }
                PlayCheck(fixture.Shots == shotsAtDeath && brain.Combat.Ammo == deadAmmo && brain.States.Current == EnemyStateId.Dead && brain.Tactics.Member == null, "Queued direct/suppression fire and repeated damage cannot resurrect or spend dead actor ammunition");
                var point = fixture.AddCover(EnemyCoverKind.Low, 8, -5.9f);
                var owner = fixture.Contexts[1]; fixture.Brains[1].enabled = false;
                PlayCheck(fixture.Covers.Reserve(point, owner, brain.Config.Tactics.Cover, Time.time), "Edge fixture reserves usable cover");
                point.Protection.enabled = false;
                fixture.Covers.Cleanup(Time.time + 0.3f);
                PlayCheck(fixture.Covers.Reservation(point) == null && !point.IsUsable, "Disabled protection collider invalidates and releases cover");
                point.Protection.enabled = true;
                PlayCheck(fixture.Covers.Reserve(point, owner, brain.Config.Tactics.Cover, Time.time + 0.4f), "Restored cover can be reserved again");
                point.gameObject.SetActive(false); fixture.Covers.Cleanup(Time.time + 0.8f);
                PlayCheck(fixture.Covers.Reservation(point) == null, "Disabled cover GameObject releases its lease");
                point.gameObject.SetActive(true);
                brain.GetComponent<EnemyAnimationBridge>().enabled = true; brain.GetComponent<EnemyVfxPresenter>().enabled = true;
                brain.gameObject.SetActive(false); brain.gameObject.SetActive(true); brain.ResetForSpawn();
                PlayCheck(!actor.Health.IsDead && brain.Combat.Ammo == brain.Config.Combat.MagazineSize && brain.Tactics.Member != null && !brain.Memory.HasContact && !brain.Tactics.Current.Valid, "Death followed by pooling reset clears stale resources and memory");
            }
            yield return null;
        }
    }
}