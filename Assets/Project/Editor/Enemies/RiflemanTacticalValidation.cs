using System;
using System.IO;
using System.Collections;
using Breachpoint.Composition;
using Breachpoint.Gameplay.Combat;
using VContainer;
using VContainer.Unity;
using UnityEditor;
using Breachpoint.Gameplay.AI;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamPresentationIntegration
    {
        public static void ValidateTacticalPolicy()
        {
            var config = ScriptableObject.CreateInstance<EnemyTacticalConfig>();
            try
            {
                var policy = new EnemyTacticalPolicy(config);
                var current = new EnemyTacticalCandidate(EnemyTacticalIntent.HoldEngage, Vector3.zero, 50f, TacticalReason.CleanLane);
                var candidates = new[] { current, new EnemyTacticalCandidate(EnemyTacticalIntent.FlankLeft, Vector3.left, 90f, TacticalReason.BetterAngle) };
                CheckPolicy(policy.Choose(candidates, 2, current, 0f, 1f, false, out var reason).Intent == current.Intent && reason == TacticalReason.Commitment, "Minimum commitment prevents early role change");
                CheckPolicy(policy.Choose(candidates, 2, current, 0f, 3f, false, out reason).Intent == EnemyTacticalIntent.FlankLeft, "Meaningful improvement can replace committed intent");
                candidates[1].Score = 51f;
                CheckPolicy(policy.Choose(candidates, 2, current, 0f, 3f, false, out reason).Intent == current.Intent && reason == TacticalReason.ImprovementTooSmall, "One-percent improvement does not cause thrashing");
                candidates[1].Score = 50f;
                CheckPolicy(policy.Choose(candidates, 2, default, 0f, 3f, true, out reason).Intent == current.Intent, "Equal score uses stable candidate order");
                candidates[1].Score = 100f; candidates[1].Valid = false;
                CheckPolicy(policy.Choose(candidates, 2, default, 0f, 3f, false, out reason).Intent == current.Intent, "Invalid candidate cannot win");
                CheckPolicy(policy.Choose(candidates, 0, default, 0f, 3f, false, out reason).Intent == EnemyTacticalIntent.WaitForLane && reason == TacticalReason.NoCandidate, "No candidate falls back safely");
                candidates[1].Valid = true;
                CheckPolicy(policy.Choose(candidates, 2, current, 0f, 0f, true, out reason).Intent == EnemyTacticalIntent.FlankLeft, "Safety override bypasses commitment");
                CheckPolicy(policy.Choose(candidates, 2, default, 0f, 0f, false, out reason).Intent == EnemyTacticalIntent.FlankLeft, "Invalid current intent is replaced immediately");
                Directory.CreateDirectory(RiflemanRework.Evidence);
                File.WriteAllText(RiflemanRework.Evidence + "/stage-2-policy.txt", "PASS: 8 deterministic score, tie, invalidity, fallback, commitment, override and improvement checks.\n" + ConsoleCounts());
            }
            finally { Object.DestroyImmediate(config); }
        }
        private static IEnumerator CoverFoundationTests(GameLifetimeScope scope, EnemyWorld world)
        {
            GameObject first; GameObject second;
            using (LifetimeScope.EnqueueParent(scope)) first = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), new Vector3(4,0,-8), Quaternion.identity);
            using (LifetimeScope.EnqueueParent(scope)) second = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), new Vector3(7,0,-8), Quaternion.identity);
            var targetObject = new GameObject("Cover test target"); targetObject.layer = 6;
            var targetCollider = targetObject.AddComponent<CapsuleCollider>(); targetCollider.height = 2f; targetCollider.center = Vector3.up;
            targetObject.AddComponent<Health>().Configure(100000f, false);
            var target = targetObject.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            targetObject.transform.position = new Vector3(4,0,2);
            GameObject lowObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); lowObstacle.name = "Low cover test barrier";
            lowObstacle.transform.position = new Vector3(4,0.65f,-5); lowObstacle.transform.localScale = new Vector3(2,1.3f,0.6f);
            var lowObject = new GameObject("Authored low test point"); lowObject.transform.position = new Vector3(4,0,-5.9f);
            var low = lowObject.AddComponent<EnemyCoverPoint>(); low.Configure(EnemyCoverKind.Low, lowObstacle.GetComponent<Collider>(), null);
            GameObject highObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); highObstacle.name = "High cover test wall";
            highObstacle.transform.position = new Vector3(8,1.5f,-5); highObstacle.transform.localScale = new Vector3(2,3,0.6f);
            var highObject = new GameObject("Authored high test point"); highObject.transform.position = new Vector3(8,0,-5.9f);
            var exposure = new GameObject("High exposure"); exposure.transform.position = new Vector3(9.8f,0,-5.9f);
            var high = highObject.AddComponent<EnemyCoverPoint>(); high.Configure(EnemyCoverKind.High, highObstacle.GetComponent<Collider>(), exposure.transform);
            var config = ScriptableObject.CreateInstance<EnemyCoverConfig>();
            var service = scope.Container.Resolve<EnemyCoverService>(); service.Register(low); service.Register(high);
            yield return null;
            var firstBrain = first.GetComponent<EnemyBrain>(); firstBrain.enabled = false; firstBrain.ResetForSpawn();
            var secondBrain = second.GetComponent<EnemyBrain>(); secondBrain.enabled = false; secondBrain.ResetForSpawn();
            var owner = first.GetComponent<EnemyLifetimeScope>().Container.Resolve<EnemyContext>();
            var other = second.GetComponent<EnemyLifetimeScope>().Container.Resolve<EnemyContext>();
            owner.Memory.Target = target; other.Memory.Target = target;
            var allies = new Vector3[1]; int mask = first.GetComponent<UnityEngine.AI.NavMeshAgent>().areaMask;
            Physics.SyncTransforms();
            try
            {
                var rating = service.Evaluate(low, owner, target.AimPosition, config, allies, 0, 2f, mask);
                PlayCheck(rating.Valid && rating.FiringLane && rating.PathCost > 0f, "Low cover blocks crouched sight, has standing exposure and a complete measured path | rejection=" + rating.Rejection);
                var wrong = service.Evaluate(low, owner, new Vector3(4,1.7f,-12), config, allies, 0, 2f, mask);
                PlayCheck(!wrong.Valid && wrong.Rejection == EnemyCoverRejection.WrongSide, "Target crossing opposite side invalidates protection");
                float now = Time.time;
                PlayCheck(service.Reserve(low, owner, config, now) && !service.Reserve(low, other, config, now), "Atomic cover reservation rejects simultaneous second owner");
                PlayCheck(service.Pulse(low, owner, config, now, false) && service.Reservation(low).Phase == EnemyCoverPhase.Moving, "Reservation advances to moving");
                owner.Navigation.ResetAt(low.ProtectedPosition);
                PlayCheck(service.Pulse(low, owner, config, now, true) && service.Reservation(low).Phase == EnemyCoverPhase.Occupied, "Reaching protected position advances to occupied");
                service.ReleaseOwner(owner);
                PlayCheck(service.Reservation(low) == null && service.Reserve(low, other, config, now), "Explicit reset/abandon release makes cover reusable");
                service.Cleanup(now + config.ReservationLease + 1f);
                PlayCheck(service.Reservation(low) == null, "Unrenewed reservation expires safely");
                now += 20f; service.Reserve(low, owner, config, now);
                for (int i = 1; i < 12; i++) service.Pulse(low, owner, config, now + i, false);
                PlayCheck(service.Reservation(low) == null, "Renewal does not extend original reach deadline forever");
                now += 20f; service.Reserve(low, owner, config, now); low.enabled = false; service.Cleanup(now + 1f);
                PlayCheck(service.Reservation(low) == null, "Disabling cover releases its reservation"); low.enabled = true;
                service.Reserve(low, owner, config, now + 2f); owner.Actor.Health.TakeDamage(new DamageInfo(100000f, owner.Actor.Eyes.position, Vector3.back, targetObject)); service.Cleanup(now + 3f);
                PlayCheck(service.Reservation(low) == null, "Death failsafe releases reservation before ragdoll"); firstBrain.ResetForSpawn(); owner.Memory.Target = target; owner.Navigation.ResetAt(new Vector3(4,0,-8));
                service.Reserve(low, owner, config, now + 4f); first.SetActive(false); service.Cleanup(now + 5f);
                PlayCheck(service.Reservation(low) == null, "Disabled owner cannot retain cover"); first.SetActive(true); yield return null; firstBrain.ResetForSpawn(); owner.Memory.Target = target;
                allies[0] = low.ProtectedPosition;
                rating = service.Evaluate(low, owner, target.AimPosition, config, allies, 1, 2f, mask);
                PlayCheck(!rating.Valid && rating.Rejection == EnemyCoverRejection.Crowded, "Occupied ally spacing rejects clustered cover");
                lowObstacle.transform.localScale = new Vector3(2,3f,0.6f); lowObstacle.transform.position = new Vector3(4,1.5f,-5); Physics.SyncTransforms();
                rating = service.Evaluate(low, owner, target.AimPosition, config, allies, 0, 2f, mask);
                PlayCheck(!rating.Valid && rating.Rejection == EnemyCoverRejection.NoFiringLane, "Protected position without exposure lane cannot be selected to shoot through cover");
                lowObstacle.SetActive(false); rating = service.Evaluate(low, owner, target.AimPosition, config, allies, 0, 2f, mask);
                PlayCheck(!rating.Valid && rating.Rejection == EnemyCoverRejection.Disabled, "Removed protection geometry invalidates point");
                targetObject.transform.position = new Vector3(8,0,2); Physics.SyncTransforms();
                rating = service.Evaluate(high, owner, target.AimPosition, config, allies, 0, 2f, mask);
                PlayCheck(rating.Valid && rating.FiringLane, "High cover has protected standing point plus reachable lateral firing exposure | rejection=" + rating.Rejection);
                high.Configure(EnemyCoverKind.High, highObstacle.GetComponent<Collider>(), null);
                PlayCheck(!high.IsUsable, "High cover without exposure is rejected");
                PlayCheck(!service.Reachable(owner.Actor.transform.position, new Vector3(4,0,-50), mask, out _, out _), "Off-mesh/partial tactical destination fails closed");
                service.Unregister(low); service.Unregister(high);
                PlayCheck(service.ReservationCount == 0, "Fixture unregister leaves no reservations");
            }
            finally
            {
                service.ReleaseOwner(owner); service.ReleaseOwner(other); service.Unregister(low); service.Unregister(high);
                Object.Destroy(first); Object.Destroy(second); Object.Destroy(targetObject); Object.Destroy(lowObject); Object.Destroy(highObject); Object.Destroy(exposure); Object.Destroy(lowObstacle); Object.Destroy(highObstacle); Object.Destroy(config);
            }
            yield return null;
        }
        private static IEnumerator SquadFoundationTests(GameLifetimeScope scope, EnemyWorld world)
        {
            var instances = new GameObject[3]; var contexts = new EnemyContext[3];
            for (int i = 0; i < 3; i++)
                using (LifetimeScope.EnqueueParent(scope)) instances[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), new Vector3(4 + i * 3,0,-8), Quaternion.identity);
            var targetObject = new GameObject("Squad knowledge target"); targetObject.layer = 6;
            var collider = targetObject.AddComponent<CapsuleCollider>(); collider.height = 2f; collider.center = Vector3.up;
            targetObject.AddComponent<Health>().Configure(100000f, false);
            var target = targetObject.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player); targetObject.transform.position = new Vector3(4,0,4);
            var config = ScriptableObject.CreateInstance<EnemySquadConfig>(); var service = scope.Container.Resolve<EnemySquadService>();
            yield return null;
            for (int i = 0; i < 3; i++)
            { var brain = instances[i].GetComponent<EnemyBrain>(); brain.enabled = false; brain.ResetForSpawn(); contexts[i] = instances[i].GetComponent<EnemyLifetimeScope>().Container.Resolve<EnemyContext>(); }
            var squad = service.Join(contexts[0], "test-alpha", config, out var a);
            service.Join(contexts[1], "test-alpha", config, out var b); service.Join(contexts[2], "test-alpha", config, out var c);
            try
            {
                float now = Time.time;
                contexts[0].Memory.Visible = true; contexts[0].Memory.Target = target; contexts[0].Memory.LastKnownPosition = target.transform.position; contexts[0].Memory.KnownAimPosition = target.AimPosition; contexts[0].Memory.LastSeenTime = now;
                squad.UpdateCommunication(a, now);
                PlayCheck(!squad.TryShared(b, now, out _), "Shared contact obeys communication latency");
                squad.UpdateCommunication(b, now + config.CommunicationLatency + 0.01f);
                PlayCheck(squad.TryShared(b, now + 0.5f, out var contact) && contact.Position == new Vector3(4,0,4), "Nearby member receives confirmed cached contact");
                targetObject.transform.position += Vector3.right * 6f;
                PlayCheck(squad.TryShared(b, now + 0.6f, out contact) && contact.Position != targetObject.transform.position && !contexts[1].Memory.Visible, "Shared contact never follows unseen live target position or grants direct LOS");
                instances[1].transform.position = new Vector3(40,0,-8);
                PlayCheck(!squad.TryShared(b, now + 0.6f, out _), "Distant member cannot receive radio contact outside radius"); contexts[1].Navigation.ResetAt(new Vector3(7,0,-8));
                PlayCheck(!squad.TryShared(b, now + config.MemoryDuration + 1f, out _), "Stale confirmed contact expires");
                var separate = service.Join(contexts[2], "test-beta", config, out var separateMember);
                PlayCheck(!separate.TryShared(separateMember, now + 0.6f, out _) && squad.Members.Count == 2, "Two squads retain separate knowledge and membership");
                service.Join(contexts[2], "test-alpha", config, out c);
                a.CanPressure = b.CanPressure = c.CanPressure = true;
                now = Time.time + 20f;
                PlayCheck(squad.TryShooter(a, 20, now) && squad.TryShooter(b, 30, now) && !squad.TryShooter(c, 40, now) && squad.ActiveShooters == 2, "Combat slots enforce maximum concurrent shooters and initial role commitment");
                squad.TryShooter(a, 20, now + 1.5f); squad.TryShooter(b, 30, now + 1.5f);
                PlayCheck(squad.TryShooter(c, 100, now + config.RoleCooldown + 0.2f) && !a.Shooter && squad.ActiveShooters == 2, "Roles rotate by score after cooldown rather than fixed member index");
                targetObject.transform.position = new Vector3(10,0,4); Physics.SyncTransforms();
                contexts[2].Combat.Attack(target, now + 3f); contexts[2].Combat.Attack(target, now + 4f);
                PlayCheck(contexts[2].Combat.RequestReload(now + 4f), "Actual shooter can begin real gameplay reload"); squad.Cleanup(now + 4.2f);
                PlayCheck(!c.Shooter, "Reloading shooter releases combat slot");
                contexts[2].Combat.Reset(); squad.ReleaseSlots(b); squad.ReleaseSlots(c); a.CanPressure = b.CanPressure = c.CanPressure = false;
                now += 30f;
                PlayCheck(squad.TryMover(a, new Vector3(1,0,-5), -1, 2f, now) && !squad.TryMover(b, new Vector3(-3,0,-5), -1, 2f, now), "One squad member owns each flank sector");
                PlayCheck(!squad.TryMover(b, new Vector3(1.5f,0,-5), 1, 2f, now), "Destination reservation prevents clustered movers");
                PlayCheck(squad.TryMover(b, new Vector3(8,0,-4), 1, 2f, now) && !squad.TryMover(c, new Vector3(12,0,-4), 0, 2f, now) && squad.ActiveMovers == 2, "Mover slots bound simultaneous relocation");
                squad.ReleaseSlots(a); squad.ReleaseSlots(b); a.CanPressure = true;
                PlayCheck(!squad.MovementSupported(b, now), "Movement waits when an available anchor has not established pressure");
                squad.TryShooter(a, 40f, now); a.LastShotAt = now;
                PlayCheck(squad.MovementSupported(b, now) && squad.HasPressure(b, now), "Recent anchor fire supports another member's relocation");
                service.Leave(contexts[0], true);
                PlayCheck(!squad.HasPressure(b, now) && squad.Casualties == 1 && squad.ActiveShooters == 0, "Suppressor casualty immediately releases pressure and shooter slot");
                squad.TryMover(b, new Vector3(8,0,-4), 1, 2f, now);
                service.Leave(contexts[1], false);
                PlayCheck(squad.ActiveMovers == 0 && squad.Members.Count == 1 && squad.MovementSupported(c, now), "Despawn releases mover/flank slot; lone survivor remains autonomous");
                service.Leave(contexts[2], false);
                var resetSquad = service.Join(contexts[2], "test-alpha", config, out var resetMember);
                PlayCheck(resetSquad.Members.Count == 1 && !resetMember.Mover && !resetMember.Shooter && resetMember.FlankSide == 0 && resetSquad.SharedContact.Target == null, "Empty squad removal and pooled rejoin retain no stale runtime data");
                resetMember.CanPressure = true; resetSquad.TryShooter(resetMember, 50f, now + 1f); resetSquad.Cleanup(now + config.SlotLease + 2f);
                PlayCheck(resetSquad.ActiveShooters == 0, "Unrenewed slot expires as a failsafe");
            }
            finally
            {
                for (int i = 0; i < contexts.Length; i++) { service.Leave(contexts[i], false); Object.Destroy(instances[i]); }
                Object.Destroy(targetObject); Object.Destroy(config);
            }
            yield return null;
        }
        private static void CheckPolicy(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
