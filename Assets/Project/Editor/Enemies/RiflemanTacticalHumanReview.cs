using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private const string TacticalReviewKey = "EnemyTools.TacticalReview";
        internal static EnemyBrain TacticalReviewBrain { get; private set; }
        internal static string TacticalReviewPhase { get; private set; } = "Ready";
        internal static int HumanTacticalIndex => SessionState.GetInt(TacticalReviewKey + ".Index",0);
        internal static bool HumanTacticalRepeating => SessionState.GetBool(TacticalReviewKey + ".Repeat",false);
        internal static bool TacticalReviewRunning => EditorApplication.isPlaying && SessionState.GetBool(RunningKey, false) && SessionState.GetInt(StageKey, 0) == 173;
        internal static readonly string[] HumanTacticalNames = BuildHumanTacticalNames();
        private static string[] BuildHumanTacticalNames()
        {
            return new[] {
                "Walk — sustained left", "Walk — sustained right", "Run — sustained left", "Run — sustained right",
                "Idle → left — repeated short starts", "Idle → right — repeated short starts",
                "Forward → left → forward", "Forward → right → forward", "Left → right reversal", "Right → left reversal",
                "Diagonals — four directions", "Fire → lateral reposition → stop/fire",
                "Contact — one rifleman", "Contact — three riflemen", "Squad — suppressor and mover", "Squad — suppressor and flanker",
                "Cover — low protect/expose/return", "Cover — high protect/expose/return", "Cover — competing for one slot", "Cover — every slot reserved",
                "Cover — unreachable position", "Fire lane — ally blocks then clears", "Reload — under pressure / protected",
                "Cover — player rush / abandon", "Contact loss — shared search / timeout", "Navigation — narrow obstacle passage",
                "Squad — suppressor casualty", "Death — during cover travel", "Lifecycle — reset and reuse",
                "Stress — ten agents", "Stress — thirty agents", "Fallback — no useful tactical position"
            }.Concat(CrouchDeathNames).Concat(MovingImpactNames).ToArray();
        }
        internal static string HumanTacticalPurpose(int index)
        {
            if (index >= 49) return "Moving fatal Hit: play the existing directional/stance Hit reaction, preserve travel inertia, then smoothly release every body into passive ragdoll.";
            if (index >= 32) return index < 38 ? "Controlled crouch entry and stop. Observe idle centering, feet, left hand and weapon through stance changes." : index <= 41 || index == 44 ? "Stationary lethal hit: physical muscles follow a directional death target, then fully relax." : "Moving lethal hit: existing Hit animation guides the dynamic body, preserving travel inertia, then every muscle releases into passive ragdoll.";
            if (index < 4) return "Controlled Combat locomotion: a long lateral move followed by an ordinary stop.";
            if (index < 6) return "Controlled Combat locomotion: readiness entry, short movement, stop and restart.";
            if (index < 8) return "Controlled Combat locomotion: forward → lateral → forward without stopping between directions.";
            if (index < 10) return "Controlled Combat locomotion: reverse lateral direction while already moving.";
            if (index == 10) return "Controlled Combat locomotion: blend between all four diagonal directions.";
            if (index == 11) return "Controlled movement with real rifle fire: stationary fire → move/fire → stop/fire → immediate reposition.";
            return _tacticalPurposes[index - 12];
        }
        internal static string HumanTacticalWatch(int index) => index >= 32 ? "Watch the close Game View: smooth stance, correct impact direction, no pose flash, no upward launch, attached weapon and natural settling." : index < 12
            ? "Expect responsive direction, feet before obvious translation, matched cadence and a clean stop. Watch feet, body and muzzle in Game View."
            : _tacticalWatch[index - 12];
        private static readonly string[] _tacticalPurposes = {
            "One autonomous enemy acquires and engages the visible target.", "Three autonomous enemies share contact and allocate different roles.",
            "One enemy maintains pressure while its partner relocates.", "Suppression supports a validated flank route.",
            "Low cover cycles through protection, exposure, actual fire and return.", "High cover cycles through protection, exposure, actual fire and return.",
            "Multiple enemies compete for the same cover slot.", "A reserved cover slot forces useful alternative positions.",
            "An off-mesh cover position must be rejected.", "An ally obstructs the lane and then moves clear.",
            "A short magazine forces a reload during engagement and cover use.", "A close target rush compromises an occupied cover position.",
            "The target breaks sight; the group investigates and eventually gives up.", "A carved obstacle constrains the route to a narrow passage.",
            "The suppressor dies while its partner is relocating.", "An enemy dies while holding a moving cover reservation.",
            "Active enemies are disabled, enabled and reset for reuse.", "Ten agents engage with throttled decisions and navigation.",
            "Thirty agents engage with throttled decisions and navigation.", "Invalid positioning choices require a stable fallback."
        };
        private static readonly string[] _tacticalWatch = {
            "Expect target acquisition and actual rifle fire; watch readiness, aim and recovery.", "Expect shooters and movers, not everyone firing; watch spacing and role changes.",
            "Expect simultaneous pressure and travel; watch the mover's feet and facing.", "Expect a real flank under pressure; watch route, firing lane and role changes.",
            "Expect deceleration, smooth crouch entry and centered protected idle, then exposed shots and return; watch feet, left hand and weapon.", "Expect protected standing and exposed shots; watch movement between cover positions.",
            "Expect one owner at a time and smooth low-cover crouch entry/exit, without a directional pose at rest; watch the owner and competing movers.", "Expect the holder to retain its slot; watch other enemies choose alternatives.",
            "Expect rejection without reservation; watch fallback rather than a navigation lock.", "Expect no friendly fire, then resumed pressure; watch lane clearance.",
            "Expect an actual reload and recovery; watch protection and firing interruptions.", "Expect cover abandonment and physical retreat/reposition; watch stance recovery.",
            "Expect shared last-known contact and eventual search termination; watch state/intent.", "Expect a valid route through the opening; watch corners and motion sync.",
            "Expect surviving roles to recover; watch uninterrupted relocation and pressure.", "Expect released cover/squad resources and the existing death/ragdoll sequence.",
            "Expect cleared contact, roles, reservations, ammo and presentation; watch the live fields.", "Expect valid agents, bounded repaths and responsive roles; use advanced statistics if needed.",
            "Expect valid agents without decision/repath flooding; use advanced statistics if needed.", "Expect explicit fallback without invented cover or invalid movement."
        };
        public static void RunHumanTacticalReview(int index, bool repeat)
        {
            if (index < 0 || index >= HumanTacticalNames.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (EditorApplication.isPlayingOrWillChangePlaymode && !TacticalReviewRunning)
                throw new InvalidOperationException("Exit the current Play Mode session before starting Tactical review.");
            SessionState.SetBool("EnemyTools.CrouchDeath.Capture",false);
            SessionState.SetInt(TacticalReviewKey + ".Index", index);
            SessionState.SetBool(TacticalReviewKey + ".Repeat", repeat);
            SessionState.SetInt(TacticalReviewKey + ".Revision", SessionState.GetInt(TacticalReviewKey + ".Revision", 0) + 1);
            if (!TacticalReviewRunning)
            {
                SessionState.SetBool("EnemyTools.Review.Paused", false); SessionState.SetFloat("EnemyTools.Review.TimeScale", 1f);
                ValidateStage(173);
            }
        }
        public static void StopHumanTacticalReview()
        { if (TacticalReviewRunning) EditorApplication.isPlaying = false; }
        private static IEnumerator HumanTacticalReview(GameLifetimeScope scope, EnemyWorld world, bool focusedChecks)
        {
            var service = scope.Container.Resolve<EnemyCoverService>();
            var original = new List<EnemyCoverPoint>(service.Points);
            foreach (var point in original) service.Unregister(point);
            var priorRandom = UnityEngine.Random.state;
            var camera = focusedChecks ? null : CreateFinalCamera();
            int automaticIndex = 0;
            try
            {
                do
                {
                    int index = focusedChecks ? automaticIndex : SessionState.GetInt(TacticalReviewKey + ".Index", 0);
                    int revision = SessionState.GetInt(TacticalReviewKey + ".Revision", 0);
                    UnityEngine.Random.InitState(4700 + Mathf.Max(0,index-12));
                    AppendResult("HUMAN REVIEW: " + HumanTacticalNames[index]);
                    int tactical = index >= 32 ? -1 : index - 12;
                    int count = tactical < 0 || tactical == 0 || tactical == 9 || tactical == 19 ? 1 : tactical == 2 || tactical == 3 || tactical == 14 ? 2 : tactical == 6 || tactical == 7 ? 4 : tactical == 17 ? 10 : tactical == 18 ? 30 : 3;
                    using (var fixture = new RiflemanTacticalFixture(scope, world, count, "HumanReview", tactical == 2 || tactical == 3 || tactical == 14 ? 1 : 2, tactical == 10 ? 2 : -1))
                    {
                        TacticalReviewBrain = fixture.Brains[0];
                        IEnumerator review = index >= 49 ? HumanMovingImpact(index-49,fixture,camera) : index >= 32 ? HumanCrouchDeath(index-32,fixture) : index < 12 ? HumanCombatMotion(index, fixture) : TacticalCase(tactical, fixture, world);
                        try
                        {
                            while ((focusedChecks || revision == SessionState.GetInt(TacticalReviewKey + ".Revision", 0)) && review.MoveNext())
                            {
                                fixture.Observe();
                                if (camera != null && index < 49)
                                {
                                    if (index == 16 || index == 18)
                                    {
                                        var owner = fixture.Brains.FirstOrDefault(b => b.Tactics.CurrentCover != null) ?? fixture.Brains[0];
                                        TacticalReviewBrain = owner;
                                        camera.fieldOfView = 35f;
                                        camera.transform.position = owner.transform.position + new Vector3(-2.6f,1.65f,-3.5f);
                                        camera.transform.LookAt(owner.transform.position + Vector3.up*.85f);
                                    }
                                    else if (index == 42 || index == 43 || index >= 45)
                                    {
                                        // Frame the travel corridor so the faster corpse remains
                                        // visible without following it and hiding its inertia.
                                        Vector3 root = fixture.Brains[0].transform.position;
                                        Vector3 direction = index == 46 ? Vector3.left : index == 47 ? Vector3.right : Vector3.forward;
                                        camera.fieldOfView = 45f;
                                        camera.transform.position = root + new Vector3(5f,2.4f,7f);
                                        camera.transform.LookAt(root + direction * 1.8f + Vector3.up * .8f);
                                    }
                                    else if (index < 12 || index >= 32) { camera.fieldOfView = 35f; FinalCamera(camera, fixture.Brains[0].gameObject); }
                                    else { camera.fieldOfView = 55f; camera.transform.position = new Vector3(12,16,12); camera.transform.LookAt(new Vector3(0,0,-5)); }
                                }
                                if (tactical >= 0) TacticalReviewPhase = "Autonomous AI";
                                yield return review.Current;
                            }
                        }
                        finally { (review as IDisposable)?.Dispose(); }
                    }
                    TacticalReviewBrain = null;
                    yield return null;
                    PlayCheck(service.ReservationCount == 0, "Human review releases its cover reservations");
                    if (focusedChecks) { if (++automaticIndex >= 12) break; }
                    else if (revision == SessionState.GetInt(TacticalReviewKey + ".Revision", 0) && !SessionState.GetBool(TacticalReviewKey + ".Repeat", false)) break;
                } while (true);
            }
            finally
            {
                TacticalReviewBrain = null; TacticalReviewPhase = "Ready";
                if (camera != null) Object.Destroy(camera.gameObject);
                UnityEngine.Random.state = priorRandom;
                foreach (var point in original) service.Register(point);
            }
        }
        private static IEnumerator HumanCombatMotion(int index, RiflemanTacticalFixture fixture)
        {
            var brain = fixture.Brains[0]; var actor = fixture.Actors[0]; var nav = actor.Navigation;
            brain.enabled = false; brain.ResetForSpawn(); brain.enabled = false;
            nav.ResetAt(new Vector3(0,0,-8)); brain.transform.rotation = Quaternion.identity;
            brain.States.Change(EnemyStateId.Combat, "controlled human locomotion review");
            brain.Memory.Target = fixture.Target; brain.Memory.Visible = brain.Memory.HasContact = true;
            fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
            TacticalReviewPhase = "Readiness entry";
            if (index != 4 && index != 5) foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
            Vector3 left = Vector3.left, right = Vector3.right, forward = Vector3.forward;
            var routes = index < 6 ? new[] { index % 2 == 0 ? left : right } : index == 6 ? new[] { forward,left,forward } : index == 7 ? new[] { forward,right,forward } : index == 8 ? new[] { left,right } : index == 9 ? new[] { right,left } : index == 10 ? new[] { (forward+left).normalized,(forward+right).normalized,(-forward+right).normalized,(-forward+left).normalized } : new[] { left,right };
            bool fire = index == 11;
            if (fire) foreach (var wait in Enumerate(HumanCombatStop(fixture, 2f, true))) yield return wait;
            int movingShots = 0;
            Action shot = () => { if (nav.Velocity.magnitude > .2f) movingShots++; };
            brain.Combat.Fired += shot;
            try
            {
                int repetitions = index == 4 || index == 5 ? 3 : 1;
                for (int repeat = 0; repeat < repetitions; repeat++)
                foreach (Vector3 direction in routes)
                {
                    float duration = index < 4 ? 4.5f : index < 6 ? repeat == 0 ? 2.3f : .8f : index == 10 ? 2.5f : fire ? 3.5f : 3f;
                    Vector3 goal = brain.transform.position + direction * 16f;
                    float until = Time.time + duration;
                    TacticalReviewPhase = (index == 2 || index == 3 ? "Run " : "Walk ") + direction + (fire ? " / firing" : "");
                    while (Time.time < until)
                    {
                        nav.MoveTo(goal, index == 2 || index == 3 ? EnemyMovePace.Run : EnemyMovePace.Walk, Time.time);
                        fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
                        nav.Face(fixture.Target.transform.position, Time.deltaTime);
                        if (fire) { Physics.SyncTransforms(); brain.Combat.Tick(Time.time); brain.Combat.Attack(fixture.Target,Time.time); }
                        yield return null;
                    }
                    var movement = brain.GetComponent<EnemyAnimationBridge>();
                    PlayCheck(Vector2.Dot(movement.MoveDirectionSmoothed.normalized,new Vector2(direction.x,direction.z)) > .9f, "Local presentation follows the executed direction after startup/reversal");
                    PlayCheck(!nav.Failed && !actor.Health.IsDead, "Controlled route remains navigable and alive: " + HumanTacticalNames[index]);
                    if (index == 4 || index == 5 || fire) foreach (var wait in Enumerate(HumanCombatStop(fixture,1.3f,fire))) yield return wait;
                }
                foreach (var wait in Enumerate(HumanCombatStop(fixture,1.3f,fire))) yield return wait;
                PlayCheck(nav.Velocity.magnitude < .03f && brain.GetComponent<EnemyAnimationBridge>().MoveSpeedSmoothed < .015f, "Human review ends in a settled ordinary stop");
                if (fire) PlayCheck(movingShots > 0 && fixture.Shots > movingShots, "Human review includes actual moving and stationary shots");
            }
            finally { brain.Combat.Fired -= shot; }
        }
        private static IEnumerator HumanCombatStop(RiflemanTacticalFixture fixture, float duration, bool fire)
        {
            var brain = fixture.Brains[0]; var nav = fixture.Actors[0].Navigation;
            nav.RequestStop(); TacticalReviewPhase = fire ? "Stop / fire" : "Stop / idle";
            float until = Time.time + duration;
            while (Time.time < until)
            {
                if (fire) { brain.Combat.Tick(Time.time); brain.Combat.Attack(fixture.Target,Time.time); }
                yield return null;
            }
        }
    }
}
