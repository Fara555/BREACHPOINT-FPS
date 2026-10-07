using System.Collections;
using System.IO;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyTools
    {
        [MenuItem("Breachpoint/Enemies/Animator Authoring/Configure active Rifleman death")]
        public static void ConfigureActiveDeath()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode before authoring.");
            const string path = "Assets/Project/Enemies/Prefabs/Rifleman.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<EnemyActiveRagdoll>() == null) root.AddComponent<EnemyActiveRagdoll>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            File.WriteAllText(Evidence + "/active-death-wiring.txt", "Rifleman: cached transform-only pose target and physical muscle controller configured. CharacterJoints and alive Animator graph preserved.\n");
        }
    }

    public static partial class EnemyValidationRunner
    {
        private static IEnumerator ActiveDeathChecks(GameLifetimeScope scope, EnemyWorld world)
        {
            for (int index = 0; index < 9; index++)
            using (var fixture = new RiflemanTacticalFixture(scope, world, 1, "ActiveDeath"))
            {
                yield return null;
                var brain = fixture.Brains[0]; var actor = fixture.Actors[0];
                var bridge = brain.GetComponent<EnemyAnimationBridge>(); var animator = bridge.Animator;
                var active = brain.GetComponent<EnemyActiveRagdoll>(); var ragdoll = brain.GetComponent<EnemyRagdollPresenter>();
                brain.enabled = false; brain.ResetForSpawn();
                actor.Navigation.ResetAt(new Vector3(0, 0, -8));
                brain.States.Change(EnemyStateId.Combat, "active death fixture");
                foreach (var wait in WaitEnumerable(1.6f)) yield return wait;
                if (index == 0 || index == 2)
                    foreach (var wait in Enumerate(ReviewMove(fixture, Vector3.forward, EnemyMovePace.Run, 1.4f))) yield return wait;
                var bones = animator.GetComponentsInChildren<Transform>(true);
                var poses = bones.Select(t => new Pose(t.localPosition, t.localRotation)).ToArray();
                var physical = brain.GetComponentsInChildren<Rigidbody>(true);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var hit = animator.GetBoneTransform(HumanBodyBones.Chest)?.GetComponent<Collider>();
                if (hit == null) hit = animator.GetBoneTransform(HumanBodyBones.Spine).GetComponent<Collider>();
                Vector3 hitPoint = hit != null ? hit.bounds.center : Vector3.zero;
                Vector3 hitDirection = Vector3.right;
                if (index == 5)
                {
                    Physics.SyncTransforms();
                    hitDirection = Vector3.back;
                    PlayCheck(Physics.Raycast(brain.transform.position + Vector3.up * 1.25f + Vector3.forward * 3f, hitDirection, out RaycastHit geometry, 4f, 1 << brain.gameObject.layer, QueryTriggerInteraction.Ignore) &&
                        geometry.collider.transform == brain.transform, "Real hitscan geometry first hits the existing alive navigation capsule");
                    hit = geometry.collider; hitPoint = geometry.point;
                }
                PlayCheck(hit != null && active.PoseAnimator != null && !active.PoseAnimator.gameObject.activeSelf, "Clean animation target is cached and asleep while alive");
                var start = hips.position; var root = brain.transform.position;
                Vector3 velocity = Vector3.ProjectOnPlane(actor.Navigation.Velocity, Vector3.up);
                if (index == 6)
                {
                    // Exercise support loss and regained contact with real physical
                    // bodies on the floor. Only this fixture's sensing mask changes.
                    var settings = new SerializedObject(active);
                    settings.FindProperty("_supportMask").intValue = 0;
                    settings.FindProperty("_hitImpulse").floatValue = 0f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                actor.Health.TakeDamage(new DamageInfo(100000, hitPoint, hitDirection, fixture.Target.gameObject, hit));
                PlayCheck(ragdoll.IsActiveRagdoll && ragdoll.IsRagdoll && !animator.enabled && brain.States.Current == EnemyStateId.Dead, "Physical death begins synchronously with animation isolated from all physical bones");
                PlayCheck(bones.Select((t,i) => Vector3.Distance(t.localPosition, poses[i].position) < .0001f && Quaternion.Angle(t.localRotation, poses[i].rotation) < .01f).All(v => v), "First-frame physical pose preserves live animation and weapon rig");
                PlayCheck(Vector3.Distance(ragdoll.AppliedMomentum, velocity) < .001f && active.HitBody != null && (index == 5 ? active.HitBody.transform.IsChildOf(hips) : active.HitBody == hit.attachedRigidbody) && active.AppliedHitImpulse.magnitude <= (active.IsStationaryDeath ? 1.501f : active.MaximumMovingHitImpulse + .001f), "Travel velocity and bounded collider-specific hit impulse remain independent");
                PlayCheck(active.PoseAnimator.GetComponentsInChildren<Renderer>(true).Length == 0 && active.PoseAnimator.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Pose target contains neither renderers nor duplicate physical bodies");
                if (index == 5) AppendResult("CAPSULE HIT MAP: " + active.HitBody.name + " / impulse=" + active.AppliedHitImpulse);
                if (index == 3)
                {
                    brain.gameObject.SetActive(false); brain.gameObject.SetActive(true); yield return null;
                    brain.ResetForSpawn(); brain.enabled = false;
                    PlayCheck(!active.IsActive && !active.PoseAnimator.gameObject.activeSelf && !ragdoll.IsRagdoll && physical.All(b => b.isKinematic), "Pooling before first physics update cancels target and muscles cleanly");
                    continue;
                }
                if (index == 1)
                {
                    // Move the physical corpse clear of the floor without moving AI root.
                    animator.transform.position += Vector3.up * 2f;
                    Physics.SyncTransforms();
                }
                GameObject wall = null;
                if (index == 2 || index == 7)
                {
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Active death collision fixture";
                    wall.transform.position = index == 7 ? root + Vector3.back * .45f + Vector3.up * .8f : start + Vector3.forward * .8f + Vector3.up * .1f;
                    wall.transform.localScale = new Vector3(4f, index == 7 ? 1.6f : 3f, .2f);
                    Physics.SyncTransforms();
                }
                float started = Time.time, maximumRise = 0f, maximumSpan = 0f, maximumTorque = 0f;
                float strongerLegs = 0f, targetAdvance = 0f, maximumForward = 0f;
                Quaternion previous = active.PoseAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation;
                bool supported = false, regainedSupport = false;
                float observedLossAt = -1f, passiveAfterLoss = -1f, maximumPenetration = 0f;
                try
                {
                    while (Time.time < started + active.ActiveDuration + .4f)
                    {
                        if (index == 8 && Time.time > started + .3f && !active.HasLostBalance)
                        {
                            var settings = new SerializedObject(active);
                            settings.FindProperty("_supportMask").intValue = 0;
                            settings.ApplyModifiedPropertiesWithoutUndo();
                        }
                        if (index == 7)
                            foreach (var body in physical)
                            {
                                var collider = body.GetComponent<Collider>();
                                if (collider != null && Physics.ComputePenetration(collider, body.position, body.rotation,
                                    wall.GetComponent<Collider>(), wall.transform.position, wall.transform.rotation, out _, out float depth))
                                    maximumPenetration = Mathf.Max(maximumPenetration, depth);
                            }
                        if (index == 6 && active.IsActive && active.HasLostBalance)
                        {
                            if (observedLossAt < 0f)
                            {
                                observedLossAt = Time.time;
                                var settings = new SerializedObject(active);
                                settings.FindProperty("_supportMask").intValue = ~0;
                                settings.ApplyModifiedPropertiesWithoutUndo();
                            }
                            else regainedSupport |= active.HasSupport;
                        }
                        if (index == 6 && observedLossAt >= 0f && !active.IsActive && passiveAfterLoss < 0f)
                            passiveAfterLoss = Time.time - observedLossAt;
                        supported |= active.IsActive && active.HasSupport;
                        if (active.IsActive)
                        {
                            strongerLegs = Mathf.Max(strongerLegs, active.LegStrength - active.UpperBodyStrength);
                            var targetLeg = active.PoseAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                            targetAdvance += Quaternion.Angle(previous, targetLeg.localRotation); previous = targetLeg.localRotation;
                            maximumTorque = Mathf.Max(maximumTorque, active.LastMotorTorque);
                        }
                        maximumRise = Mathf.Max(maximumRise, hips.position.y - start.y);
                        maximumForward = Mathf.Max(maximumForward, hips.position.z - start.z);
                        foreach (var body in physical) maximumSpan = Mathf.Max(maximumSpan, Vector3.Distance(body.position, hips.position));
                        yield return null;
                    }
                }
                finally { if (wall != null) Object.Destroy(wall); }
                PlayCheck(!active.IsActive && active.LastMotorTorque == 0f && active.LegStrength == 0f && !active.PoseAnimator.gameObject.activeSelf && physical.All(b => !b.isKinematic), "Active phase always ends with zero muscle drive and entirely passive physics");
                if (index != 1)
                {
                    PlayCheck(supported && (index == 6 || index == 7 || index == 8 || strongerLegs > .1f) && maximumTorque > .01f && targetAdvance > .1f, "Physical death uses animated targets and torque; unblocked grounded falls retain legs longer than torso");
                    PlayCheck(maximumRise < .15f && maximumSpan < 2.2f, "Physical motors do not launch the body or stretch limbs beyond human dimensions");
                }
                else PlayCheck(!supported, "Airborne death loses support and relaxes without standing in the air | support="+supported+" torque="+maximumTorque);
                if (index == 6) PlayCheck(regainedSupport && passiveAfterLoss >= .10f && passiveAfterLoss <= .22f,
                    "Regained floor contact preserves the irreversible smooth loss fade | seconds="+passiveAfterLoss.ToString("F3"));
                if (index == 7) PlayCheck(maximumPenetration < .06f, "Stationary animation guidance respects a blocking wall | depth="+maximumPenetration);
                if (index == 8) PlayCheck(active.HasLostBalance, "Removing support during an authored fall irreversibly releases trajectory guidance");
                if (index == 2) PlayCheck(maximumForward < .85f, "Wall contact blocks corpse travel while motors are active");
                PlayCheck(maximumSpan < 2.2f, "All physical cases retain bounded limb dimensions | span="+maximumSpan+" bodies="+string.Join(";", physical.Select(b=>b.name+":"+Vector3.Distance(b.position,hips.position).ToString("F3"))));
                PlayCheck(Vector3.Distance(root, brain.transform.position) < .01f && !actor.Navigation.HasMovementRequest, "AI root stays stopped throughout physical assistance and collisions");
                AppendResult($"ACTIVE DEATH: case={index} support={supported} legDifference={strongerLegs:F3} targetAdvance={targetAdvance:F2} peakTorque={maximumTorque:F3} rise={maximumRise:F3} span={maximumSpan:F3}");
                brain.gameObject.SetActive(false); brain.gameObject.SetActive(true); yield return null; brain.ResetForSpawn(); brain.enabled = false;
                PlayCheck(!active.IsActive && animator.enabled && physical.All(b => b.isKinematic) && brain.GetComponentsInChildren<Animator>(true).Length == 2, "Pool reset restores alive rig and reuses exactly one target Animator");
            }
        }
    }
}
