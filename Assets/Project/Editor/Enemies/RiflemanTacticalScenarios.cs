using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using VContainer;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal static readonly string[] TacticalScenarioNames =
        {
            "Single Rifleman baseline", "Three simultaneous contacts", "Suppressor and mover", "Suppressor and flanker", "Low cover", "High cover", "Competing for one cover", "All covers reserved", "Unreachable cover", "Ally blocks firing line", "Reload under pressure", "Player rushes cover", "Lost contact and group search", "Narrow passage", "Suppressor casualty", "Death during cover transition", "Reset and reuse", "Ten-agent stress", "Thirty-agent stress", "No valid tactical position",
            "Exposed head behind low cover", "Occluded contact coordinated flank", "Distant squad encounter alert", "Rifleman survivability"
        };
        public static void RunTacticalScenario(int index)
        {
            if (index < -1 || index >= TacticalScenarioNames.Length) throw new ArgumentOutOfRangeException(nameof(index));
            SessionState.SetInt("EnemyTools.TacticalScenario", index); ValidateStage(100);
        }
        public static void RunTacticalRange(int first)
        {
            if (first < 0 || first >= TacticalScenarioNames.Length) throw new ArgumentOutOfRangeException(nameof(first));
            SessionState.SetInt("EnemyTools.TacticalScenario", 1000 + first); ValidateStage(100);
        }
        private static IEnumerator RiflemanTacticalTests(GameLifetimeScope scope, EnemyWorld world)
        {
            Directory.CreateDirectory(EnemyTools.Evidence);
            var service = scope.Container.Resolve<EnemyCoverService>();
            var originalPoints = new List<EnemyCoverPoint>(service.Points);
            foreach (var point in originalPoints) service.Unregister(point);
            UnityEngine.Random.State priorRandom = UnityEngine.Random.state;
            int selected = SessionState.GetInt("EnemyTools.TacticalScenario", -1); _nextTacticalSample = 0;
            try
            {
                using (var samples = new StreamWriter(EnemyTools.Evidence + "/tactical-samples.csv", false))
                {
                    samples.WriteLine("Scenario,Time,Enemy,State,Intent,Role,Knowledge,DirectLOS,FireLane,Shooter,Mover,Cover,Phase,X,Z,Ammo,SnapshotTime,Decisions,Repaths,CanPressure,CoverValid,CoverRejection");
                    for (int index = 0; index < TacticalScenarioNames.Length; index++)
                    {
                        if (selected >= 1000 ? index < selected - 1000 : selected >= 0 && selected != index) continue;
                        UnityEngine.Random.InitState(4700 + index);
                        AppendResult("TACTICAL SCENARIO " + (index + 1) + ": " + TacticalScenarioNames[index]);
                        int count = index == 0 || index == 9 || index == 19 || index == 20 || index == 22 || index == 23 ? 1 : index == 2 || index == 3 || index == 14 ? 2 : index == 6 || index == 7 ? 4 : index == 17 ? 10 : index == 18 ? 30 : 3;
                        using (var fixture = new RiflemanTacticalFixture(scope, world, count, "Scenario" + (index + 1), index == 2 || index == 3 || index == 14 ? 1 : 2, index == 10 ? 2 : -1))
                        {
                            yield return null;
                            foreach (var brain in fixture.Brains) PlayCheck(brain.Tactics != null && brain.Tactics.Member != null, "Local tactical execution constructed and registered: " + brain.name);
                            IEnumerator test = TacticalCase(index, fixture, world, scope);
                            try { while (test.MoveNext()) { fixture.Observe(); SampleTactical(index, fixture, samples); yield return test.Current; } }
                            finally { (test as IDisposable)?.Dispose(); }
                            fixture.Observe();
                            PlayCheck(fixture.MaximumShooters <= (index == 2 || index == 3 || index == 14 ? 1 : 2) && fixture.MaximumMovers <= 2, "Configured shooter/mover limits were respected throughout scenario");
                            PlayCheck(!fixture.ProtectedShot, "Protected cover phase never fires through its barrier");
                            AppendResult("Scenario " + (index + 1) + " PASS: " + TacticalScenarioNames[index] + " | shots=" + fixture.Shots + ", simultaneous fire/movement=" + fixture.PressureMovementOverlap + ", movers=" + fixture.SawMover + ", flank=" + fixture.SawFlank);
                        }
                        yield return null; yield return null;
                        PlayCheck(service.ReservationCount == 0, "Scenario teardown releases every cover reservation");
                    }
                }
            }
            finally { UnityEngine.Random.state = priorRandom; foreach (var point in originalPoints) service.Register(point); }
        }
        private static float _nextTacticalSample;
        private static void SampleTactical(int index, RiflemanTacticalFixture fixture, StreamWriter writer)
        {
            if (index == 17 || index == 18 || Time.time < _nextTacticalSample) return;
            _nextTacticalSample = Time.time + 0.1f;
            foreach (var brain in fixture.Brains)
            {
                var tactics = brain.Tactics; var member = tactics.Member; if (member == null) continue;
                Vector3 position = brain.transform.position;
                writer.WriteLine(string.Join(",", index + 1, Time.time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), brain.name, brain.States.Current, tactics.Current.Intent, member.Role, tactics.Knowledge, brain.Memory.Visible, tactics.FireLane, member.Shooter, member.Mover,
                    tactics.CurrentCover != null ? tactics.CurrentCover.name : "none", tactics.CoverPhase, position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), brain.Combat.Ammo, brain.Memory.LastSeenTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), tactics.DecisionCount, fixture.Actors[Array.IndexOf(fixture.Brains, brain)].Navigation.RepathCount, member.CanPressure, tactics.CoverRating.Valid, tactics.CoverRating.Rejection));
            }
        }
        private static IEnumerator WatchTactical(RiflemanTacticalFixture fixture, float seconds)
        { float until = Time.time + seconds; while (Time.time < until) { fixture.Observe(); yield return null; } }
        private static IEnumerator TacticalCase(int index, RiflemanTacticalFixture fixture, EnemyWorld world, GameLifetimeScope scope)
        {
            if (index == 20)
            {
                var brain = fixture.Brains[0]; brain.enabled = false; brain.ResetForSpawn();
                var barrier = fixture.Wall(new Vector3(3.2f, .725f, .5f), new Vector3(12f, 1.45f, .5f), false);
                var physics = new EnemyPhysics();
                using (var sensor = new EnemyPerception(fixture.Contexts[0], world))
                {
                    sensor.Start(Time.time); Physics.SyncTransforms(); sensor.Tick(Time.time + brain.Config.Perception.Interval);
                    PlayCheck(!physics.ClearLine(fixture.Actors[0].Eyes.position, fixture.Target, brain.Config.Perception.ObstructionMask, brain.transform), "Low cover blocks the original center-body sight ray");
                    PlayCheck(brain.Memory.Visible && brain.Memory.KnownAimPosition.y > barrier.GetComponent<Collider>().bounds.max.y, "An exposed upper body is detected above low cover");
                    brain.States.Change(EnemyStateId.Combat, "Controlled exposed-target weapon test");
                    var presentation = brain.GetComponent<EnemyAnimationBridge>();
                    float readyBy = Time.time + 2f;
                    while (!presentation.IsAimPoseReady && Time.time < readyBy) yield return null;
                    Physics.SyncTransforms();
                    float health = fixture.Target.Health.CurrentHealth;
                    var weapon = fixture.Actors[0].GetComponent<IEnemyWeapon>();
                    PlayCheck(weapon.CanAttack(fixture.Target), "Raised rifle has a clear physical firing lane above low cover | muzzle=" + fixture.Actors[0].Muzzle.position);
                    PlayCheck(weapon.Attack(fixture.Target, 0f) && fixture.Target.Health.CurrentHealth < health, "Rifle delivers actual damage to the exposed part instead of aiming into cover");
                    barrier.transform.position = new Vector3(3.2f, 1.5f, .5f); barrier.transform.localScale = new Vector3(12f, 3f, .5f);
                    Physics.SyncTransforms(); sensor.Tick(Time.time + brain.Config.Perception.Interval * 2f);
                    PlayCheck(!brain.Memory.Visible && !weapon.CanAttack(fixture.Target), "Complete physical occlusion still blocks sight and rifle fire");
                }
                yield break;
            }
            if (index == 21)
            {
                foreach (var wait in Enumerate(WatchTactical(fixture, 1.5f))) yield return wait;
                int shots = fixture.Shots;
                fixture.Wall(new Vector3(4f, 1.5f, -1f), new Vector3(8f, 3f, .5f), true); Physics.SyncTransforms();
                bool sawHiddenFlanker = false, reacquired = false; float until = Time.time + 8f;
                while (Time.time < until)
                {
                    int flankers = 0;
                    foreach (var brain in fixture.Brains)
                    {
                        var member = brain.Tactics.Member;
                        if (member != null && member.Mover && member.FlankSide != 0)
                        { flankers++; sawHiddenFlanker |= !brain.Memory.Visible && brain.Memory.HasContact; }
                        reacquired |= sawHiddenFlanker && brain.Memory.Visible && fixture.Shots > shots;
                    }
                    PlayFlankLimit(flankers, fixture.Brains[0].Config.Tactics.Squad.MaximumFlankers);
                    fixture.Observe(); yield return null;
                }
                PlayCheck(sawHiddenFlanker && reacquired, "A bounded flanker pursues the cached occluded contact, finds a new angle and restores actual fire");
                yield break;
            }
            if (index == 22)
            {
                using (var distant = new RiflemanTacticalFixture(scope, world, 1, "DistantEncounterSquad"))
                {
                    distant.Target.gameObject.SetActive(false);
                    fixture.Target.transform.position = new Vector3(-14f, 1f, -6f);
                    fixture.Target.GetComponent<CapsuleCollider>().center = Vector3.zero;
                    fixture.Actors[0].transform.position = new Vector3(-14f, 0f, -16f);
                    distant.Actors[0].transform.SetPositionAndRotation(new Vector3(14f, 0f, 14f), Quaternion.identity);
                    fixture.Actors[0].ConfigureEncounter("EncounterAlertRegression"); distant.Actors[0].ConfigureEncounter("EncounterAlertRegression");
                    yield return null;
                    fixture.Brains[0].ResetForSpawn(); distant.Brains[0].ResetForSpawn(); Physics.SyncTransforms();
                    Vector3 began = distant.Actors[0].transform.position;
                    float deadline = Time.time + 1f;
                    while (!distant.Brains[0].Memory.HasContact && Time.time < deadline) yield return null;
                    PlayCheck(distant.Brains[0].Memory.HasContact && !distant.Brains[0].Memory.Visible && distant.Brains[0].Memory.Target == fixture.Target,
                        "A separate distant squad receives confirmed encounter contact without being granted direct sight");
                    PlayCheck(distant.Brains[0].Tactics.Squad != fixture.Brains[0].Tactics.Squad, "Encounter alert preserves independent squad combat roles");
                    PlayCheck(Mathf.Abs(distant.Brains[0].Memory.LastKnownPosition.y - fixture.Target.NavigationPosition.y) < .01f,
                        "A center-pivot player supplies a grounded navigation position distinct from its aim point");
                    foreach (var wait in Enumerate(WatchTactical(distant, 2f))) yield return wait;
                    PlayCheck(Vector3.Distance(began, distant.Actors[0].transform.position) > 2f && distant.Brains[0].Memory.HasContact,
                        "Distant alerted member physically closes the distance instead of waiting outside weapon range");
                }
                yield break;
            }
            if (index == 23)
            {
                var health = fixture.Actors[0].Health;
                var weapon = AssetDatabase.LoadAssetAtPath<Breachpoint.Gameplay.Weapons.WeaponConfig>("Assets/Project/Configs/Weapons/SP6/SP6_WeaponConfig.asset");
                PlayCheck(health.MaximumHealth == 180f, "Rifleman receives the tuned 180 health from its production archetype");
                for (int i = 0; i < 4; i++) health.TakeDamage(new DamageInfo(weapon.Damage, fixture.Actors[0].transform.position, Vector3.forward, fixture.Target.gameObject));
                PlayCheck(!health.IsDead, "Rifleman survives four normal production SP6 hits");
                health.TakeDamage(new DamageInfo(weapon.Damage, fixture.Actors[0].transform.position, Vector3.forward, fixture.Target.gameObject));
                PlayCheck(health.IsDead && fixture.Brains[0].States.Current == EnemyStateId.Dead, "The fifth normal SP6 hit remains lethal and enters the actual death state");
                yield break;
            }
            if (index == 0 || index == 1 || index == 2 || index == 3 || index == 17 || index == 18)
            {
                foreach (var wait in Enumerate(WatchTactical(fixture, 6f))) yield return wait;
                PlayCheck(fixture.Shots > 0 && fixture.Target.Health.CurrentHealth < fixture.Target.Health.MaximumHealth, "Visible target receives actual rifle fire");
                if (index == 0) PlayCheck(fixture.Shots >= 8 && fixture.MovingShots >= 3, "Lone rifleman sustains pressure and fires while physically moving | shots=" + fixture.Shots + ", moving=" + fixture.MovingShots);
                if (index == 2 || index == 3) PlayCheck(fixture.PressureMovementOverlap && fixture.SawMover, "One member fires while another physically relocates");
                if (index == 3) PlayCheck(fixture.SawFlank, "Validated flank sector is executed under pressure");
                if (index == 1) PlayCheck(fixture.SawMover && fixture.MaximumShooters < fixture.Brains.Length, "Open contact uses different roles instead of all enemies shooting together");
                if (index == 17 || index == 18) foreach (var actor in fixture.Actors) PlayCheck(!actor.Health.IsDead && actor.Navigation.RepathCount < 30, "Stress enemy remains valid with throttled navigation");
                yield break;
            }
            if (index >= 4 && index <= 8 || index == 11 || index == 15 || index == 16)
            {
                // Keep the unreachable point beyond the isolated floor's 20 m edge,
                // including the NavMesh sampling tolerance.
                var point = fixture.AddCover(index == 5 ? EnemyCoverKind.High : EnemyCoverKind.Low, 8f, index == 8 ? -22f : -5.9f);
                if (index == 4 || index == 5 || index == 11 || index == 15 || index == 16)
                {
                    // These cases validate cover execution, not ownership competition.
                    // Start the nearest autonomous owner before its pressure allies join;
                    // otherwise native entity IDs/tick order can assign a distant owner
                    // whose allies block each other before the intended probe begins.
                    fixture.Brains[0].enabled = fixture.Brains[1].enabled = false;
                    try
                    {
                        float claimedBy = Time.time + 3f;
                        while (fixture.Brains[2].Tactics.CurrentCover != point && Time.time < claimedBy)
                        { fixture.Observe(); yield return null; }
                        PlayCheck(fixture.Brains[2].Tactics.CurrentCover == point, "Focused cover fixture establishes its nearest owner before pressure allies join");
                    }
                    finally { fixture.Brains[0].enabled = fixture.Brains[1].enabled = true; }
                }
                EnemyContext holder = index == 7 ? fixture.Contexts[3] : null;
                if (holder != null) { fixture.Brains[3].enabled = false; PlayCheck(fixture.Covers.Reserve(point, holder, fixture.Brains[0].Config.Tactics.Cover, Time.time), "External holder reserves all available cover"); }
                Physics.SyncTransforms();
                if (index == 11)
                {
                    // Rush interrupts protected cover; a completed exposure cycle is not a prerequisite.
                    // Dedicated low/high cases still require exposed shots and physical return.
                    EnemyBrain covered = null; float deadline = Time.time + 10f;
                    while (Time.time < deadline)
                    {
                        fixture.Observe();
                        foreach (var brain in fixture.Brains)
                            if (brain.Tactics.CurrentCover == point && brain.Tactics.CoverPhase == EnemyCoverActionPhase.Protected && brain.GetComponent<EnemyStance>().IsCrouching) covered = brain;
                        if (covered != null) break;
                        yield return null;
                    }
                    DescribeCover(fixture);
                    PlayCheck(covered != null, "Rush begins from an actual protected crouching cover owner");
                    Vector3 original = covered.transform.position;
                    fixture.Target.transform.position = original + new Vector3(-2f, 0f, .8f); Physics.SyncTransforms();
                    foreach (var wait in Enumerate(WatchTactical(fixture, 2f))) yield return wait;
                    PlayCheck(covered.Tactics.CurrentCover == null && (covered.transform.position - original).sqrMagnitude > .5f, "Close rush abandons cover and physically falls back/laterally repositions");
                    yield break;
                }
                if (index == 8)
                {
                    fixture.Brains[2].Memory.Target = fixture.Target;
                    var rating = fixture.Covers.Evaluate(point, fixture.Contexts[2], fixture.Target.AimPosition, fixture.Brains[0].Config.Tactics.Cover, new Vector3[0], 0, 2f, fixture.Actors[2].GetComponent<UnityEngine.AI.NavMeshAgent>().areaMask);
                    PlayCheck(!rating.Valid && rating.Rejection == EnemyCoverRejection.Unreachable, "Cover geometry with off-mesh protected point is rejected | reason=" + rating.Rejection);
                }
                if (index == 15)
                {
                    float deadline = Time.time + 7f; EnemyBrain moving = null;
                    while (Time.time < deadline)
                    { fixture.Observe(); foreach (var brain in fixture.Brains) if (brain.Tactics.CurrentCover == point && brain.Tactics.CoverPhase == EnemyCoverActionPhase.MovingToCover && brain.GetComponent<EnemyActor>().Navigation.Velocity.sqrMagnitude > 0.1f) moving = brain; if (moving != null) break; yield return null; }
                    PlayCheck(moving != null, "Enemy has a live moving cover reservation before lethal interruption");
                    moving.GetComponent<EnemyActor>().Health.TakeDamage(new DamageInfo(100000f, moving.transform.position, Vector3.back, fixture.Target.gameObject));
                    PlayCheck(moving.States.Current == EnemyStateId.Dead && fixture.Covers.Reservation(point) == null && moving.Tactics.Member == null, "Death during cover travel atomically releases cover and squad membership");
                    var deathAnimator = moving.GetComponent<EnemyAnimationBridge>().Animator;
                    PlayCheck(moving.GetComponent<EnemyRagdollPresenter>().IsRagdoll && !deathAnimator.enabled && !deathAnimator.GetBool("IsDead") && !deathAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Death"), "Actual moving cover casualty immediately becomes ragdoll without Death animation");
                    foreach (var wait in Enumerate(WaitForDeathTakeover(moving.GetComponent<EnemyRagdollPresenter>()))) { fixture.Observe(); yield return wait; }
                    PlayCheck(moving.GetComponent<EnemyRagdollPresenter>().IsRagdoll, "Cover death still reaches existing ragdoll"); yield break;
                }
                float highAnimationTime = index == 5 ? fixture.Brains[0].GetComponent<EnemyAnimationBridge>().Config.HighCoverTakeDuration * 2f + fixture.Brains[0].GetComponent<EnemyAnimationBridge>().Config.HighCoverLeaveDuration : 0f;
                float until = Time.time + (index == 7 || index == 8 ? 5f : index == 5 ? 16f + highAnimationTime : 10f);
                while (Time.time < until)
                { if (holder != null) fixture.Covers.Pulse(point, holder, fixture.Brains[0].Config.Tactics.Cover, Time.time, false); fixture.Observe(); yield return null; }
                PlayCheck(fixture.Shots > 0, "Covered/all-reserved/unreachable engagement retains actual pressure fire");
                if (index == 4 || index == 5 || index == 16) DescribeCover(fixture);
                if (index == 4 || index == 5 || index == 16)
                    PlayCheck(index == 5 ? fixture.SawProtectedHigh && fixture.SawExposed : fixture.SawProtectedLow && fixture.SawExposed, "Cover reaches protected stance and actual exposure phase");
                if (index == 4 || index == 5)
                {
                    PlayCheck(fixture.CoverExposedShot, "Covered enemy actually fires from exposure, not just entering an exposure phase");
                    PlayCheck(fixture.ReturnedProtectedAfterShot, "The same cover owner physically returns protected after its actual exposed shot");
                }
                if (index == 6) PlayCheck(fixture.MaximumReservations == 1, "Competing enemies never own the same single cover together");
                if (index == 7) PlayCheck(fixture.Covers.Reservation(point)?.Owner == holder && fixture.SawMover, "All reserved cover falls back to other useful positions");
                if (index == 8) PlayCheck(fixture.Covers.Reservation(point) == null, "Unreachable cover is never reserved");
                if (index == 16)
                {
                    foreach (var brain in fixture.Brains)
                    {
                        var actor = brain.GetComponent<EnemyActor>(); brain.gameObject.SetActive(false); brain.gameObject.SetActive(true); brain.ResetForSpawn();
                        PlayCheck(brain.Memory.Target == null && !brain.Memory.HasContact && brain.Combat.Ammo == brain.Config.Combat.MagazineSize && brain.Tactics.CurrentCover == null && !brain.Tactics.Current.Valid && !brain.Tactics.Member.Shooter && !brain.Tactics.Member.Mover && brain.GetComponent<EnemyAnimationBridge>().Animator.enabled && !actor.Health.IsDead, "Reuse clears gameplay, tactical resources, role and presentation");
                    }
                    PlayCheck(fixture.Covers.ReservationCount == 0, "Group reuse retains no cover reservations");
                }
                yield break;
            }
            if (index == 9)
            {
                var actor = fixture.Actors[0]; var friendly = fixture.FriendlyBlocker(world, Vector3.Lerp(actor.Muzzle.position, fixture.Target.AimPosition, 0.5f)); Physics.SyncTransforms();
                PlayCheck(!fixture.Brains[0].Combat.HasFiringLine(fixture.Target), "Friendly blocks actual muzzle/eye firing line");
                foreach (var wait in Enumerate(WatchTactical(fixture, 0.4f))) yield return wait;
                PlayCheck(fixture.Shots == 0 && fixture.Target.Health.CurrentHealth == fixture.Target.Health.MaximumHealth, "Blocked lane delivers no shot or damage");
                friendly.SetActive(false); Physics.SyncTransforms();
                foreach (var wait in Enumerate(WatchTactical(fixture, 4f))) yield return wait;
                PlayCheck(fixture.Shots > 0, "Removing blocker resumes reliable pressure fire"); yield break;
            }
            if (index == 10)
            {
                fixture.AddCover(EnemyCoverKind.Low, 8f, -5.9f); Physics.SyncTransforms();
                EnemyBrain reload = fixture.Brains[2];
                int starts = 0; bool sawReload = false; bool completed = false;
                Action started = () => starts++; reload.Combat.ReloadStarted += started;
                try
                {
                    float deadline = Time.time + 14f;
                    while (Time.time < deadline)
                    {
                        fixture.Observe(); sawReload |= reload.Combat.IsReloading;
                        if (sawReload && !reload.Combat.IsReloading && reload.Combat.Ammo > 0) { completed = true; break; }
                        yield return null;
                    }
                    PlayCheck(starts == 1 && completed && fixture.Shots > 0, "Real short magazine exhausts, tactical reload starts once and completes under other members' pressure");
                }
                finally { reload.Combat.ReloadStarted -= started; }
                yield break;
            }
            if (index == 12)
            {
                foreach (var wait in Enumerate(WatchTactical(fixture, 2f))) yield return wait;
                Vector3 known = fixture.Target.transform.position; fixture.Target.transform.position = new Vector3(fixture.Brains[0].Config.Perception.ViewRange + 100f,0,30); Physics.SyncTransforms();
                foreach (var wait in Enumerate(WatchTactical(fixture, 2f))) yield return wait;
                var destinations = new List<Vector3>();
                foreach (var brain in fixture.Brains)
                {
                    PlayCheck(!brain.Memory.Visible && brain.Memory.LastKnownPosition == known && brain.Tactics.Knowledge != EnemyKnowledge.DirectSight, "Lost contact uses cached location without tracking unseen live target");
                    if (brain.Tactics.Member.Mover && brain.Tactics.Current.Valid) destinations.Add(brain.Tactics.Current.Destination);
                }
                PlayCheck(destinations.Count >= 2 && Vector3.Distance(destinations[0], destinations[1]) >= 1f, "Group pursuit/search reserves diversified hypotheses around cached last sighting");
                foreach (var wait in Enumerate(WatchTactical(fixture, fixture.Brains[0].Config.Perception.MemoryDuration + fixture.Brains[0].Config.Decision.SearchDuration + 1f))) yield return wait;
                foreach (var brain in fixture.Brains) PlayCheck(!brain.Memory.HasContact && brain.States.Group == EnemyStateGroup.Passive && !brain.Tactics.Member.Shooter && !brain.Tactics.Member.Mover, "Expired group memory relaxes and releases combat resources"); yield break;
            }
            if (index == 13)
            {
                fixture.Target.transform.position = new Vector3(4, 0, 14);
                fixture.Wall(new Vector3(-7,1.5f,-3), new Vector3(20,3,0.6f), true);
                fixture.Wall(new Vector3(14,1.5f,-3), new Vector3(18,3,0.6f), true); Physics.SyncTransforms();
                foreach (var wait in Enumerate(WatchTactical(fixture, 10f))) yield return wait;
                bool progressed = false;
                foreach (var actor in fixture.Actors) { progressed |= actor.transform.position.z > -2.5f; PlayCheck(actor.Navigation.RepathCount < 38, "Narrow passage navigation remains throttled"); }
                PlayCheck(progressed, "At least one supported mover traverses authored carved narrow passage"); yield break;
            }
            if (index == 14)
            {
                // Apply the casualty during actual supported travel, after authored readiness.
                // A fixed 1.5-second wait can land between readiness and the next rifle burst.
                EnemyBrain anchor = null; EnemyBrain mover = null;
                float readyDeadline = Time.time + 4f;
                while (Time.time < readyDeadline)
                {
                    fixture.Observe(); anchor = mover = null;
                    foreach (var brain in fixture.Brains) if (brain.Tactics.Member.Shooter) anchor = brain;
                    foreach (var brain in fixture.Brains) if (brain != anchor && brain.Tactics.Member.Mover) mover = brain;
                    if (anchor != null && mover != null && fixture.PressureMovementOverlap && mover.GetComponent<EnemyActor>().Navigation.Velocity.sqrMagnitude > .1f) break;
                    yield return null;
                }
                PlayCheck(anchor != null && mover != null && fixture.PressureMovementOverlap, "Casualty fixture has a live anchor and advancing supported mover");
                PlayCheck(mover.GetComponent<EnemyActor>().Navigation.Velocity.sqrMagnitude > .1f, "Casualty is applied while the supported mover is physically advancing");
                var squad = anchor.Tactics.Squad; anchor.GetComponent<EnemyActor>().Health.TakeDamage(new DamageInfo(100000f, anchor.transform.position, Vector3.back, fixture.Target.gameObject));
                PlayCheck(squad.Casualties == 1 && squad.ActiveShooters == 0 && anchor.Tactics.Member == null, "Suppressor casualty immediately clears its combat slot and membership");
                foreach (var wait in Enumerate(WatchTactical(fixture, 3f))) yield return wait;
                PlayCheck(!mover.GetComponent<EnemyActor>().Health.IsDead && mover.Tactics.Member != null && mover.Tactics.DecisionCount > 2 && !mover.GetComponent<EnemyActor>().Navigation.Failed, "Survivor replans and remains autonomous after pressure loss"); yield break;
            }
            if (index == 19)
            {
                fixture.Actors[0].GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                fixture.Brains[0].Memory.Target = fixture.Target; fixture.Brains[0].Memory.HasContact = true; fixture.Brains[0].Memory.LastKnownPosition = fixture.Target.transform.position; fixture.Brains[0].Memory.KnownAimPosition = fixture.Target.AimPosition; fixture.Brains[0].Memory.LastSeenTime = Time.time; fixture.Brains[0].Memory.Alert = 1f;
                foreach (var wait in Enumerate(WatchTactical(fixture, 2f))) yield return wait;
                PlayCheck(fixture.Brains[0].Tactics.Current.Intent == EnemyTacticalIntent.WaitForLane && fixture.Brains[0].Tactics.Member != null && !fixture.Brains[0].Tactics.Member.Mover, "Unavailable navigation has bounded stationary fallback with no null errors"); yield break;
            }
        }
        private static void PlayFlankLimit(int count, int maximum)
        { if (count > maximum) throw new InvalidOperationException("Squad exceeded its configured simultaneous flanker limit."); }
        private static void DescribeCover(RiflemanTacticalFixture fixture)
        {
            var physics = new EnemyPhysics();
            foreach (var actor in fixture.Actors)
            {
                var brain = actor.GetComponent<EnemyBrain>(); var tactics = brain.Tactics;
                Vector3 delta = fixture.Target.AimPosition - actor.Muzzle.position;
                physics.TryFirstHit(actor.Muzzle.position, delta.normalized, delta.magnitude, brain.Config.Combat.HitMask, actor.transform, out var hit);
                AppendResult("COVER DIAGNOSTIC " + brain.name + " | cover=" + tactics.CurrentCover + " | phase=" + tactics.CoverPhase + " | root=" + actor.transform.position + " | eye=" + actor.Eyes.position + " | muzzle=" + actor.Muzzle.position + " | animatorCull=" + brain.GetComponent<EnemyAnimationBridge>().Animator.cullingMode + " | sameMuzzle=" + (actor.Muzzle == FindUnique(brain.GetComponent<EnemyAnimationBridge>().Animator.transform, "Muzzle")) + " | hand=" + brain.GetComponent<EnemyAnimationBridge>().Animator.GetBoneTransform(HumanBodyBones.RightHand).position + " | eyeClear=" + physics.ClearLine(actor.Eyes.position, fixture.Target, brain.Config.Combat.HitMask, actor.transform) + " | muzzleClear=" + physics.ClearLine(actor.Muzzle.position, fixture.Target, brain.Config.Combat.HitMask, actor.transform) + " | muzzleInside=" + physics.MuzzleBlocked(actor.Muzzle.position, brain.Config.Combat.HitMask, actor.transform) + " | firstHit=" + hit.collider + " | visible=" + brain.Memory.Visible);
            }
        }
        private static IEnumerable<object> Enumerate(IEnumerator routine)
        { try { while (routine.MoveNext()) yield return routine.Current; } finally { (routine as IDisposable)?.Dispose(); } }
    }
}
