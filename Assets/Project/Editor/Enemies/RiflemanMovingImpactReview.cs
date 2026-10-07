using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal static readonly string[] MovingImpactNames = {
            "Moving impact — head / front", "Moving impact — head / back",
            "Moving impact — left shoulder / left", "Moving impact — right shoulder / right",
            "Moving impact — chest / left", "Moving impact — chest / right",
            "Moving hit — crouch / left", "Moving hit — crouch / right"
        };

        private static void SetMovingImpactCamera(Camera camera, Vector3 root)
        {
            camera.fieldOfView = 38;
            camera.transform.position = root + new Vector3(3.2f, 1.9f, 4.5f);
            camera.transform.LookAt(root + Vector3.forward * .8f + Vector3.up * 1f);
        }

        private static HumanBodyBones ImpactRegion(int index) => index < 2 ? HumanBodyBones.Head :
            index == 2 ? HumanBodyBones.LeftUpperArm : index == 3 ? HumanBodyBones.RightUpperArm : HumanBodyBones.Chest;
        private static Vector3 ImpactDirection(int index) => index == 0 ? Vector3.back : index == 1 ? Vector3.forward :
            index == 2 || index == 4 || index == 6 ? Vector3.right : Vector3.left;

        private static RaycastHit MovingImpactShot(EnemyBrain brain, Animator animator, int index)
        {
            var bone = animator.GetBoneTransform(ImpactRegion(index));
            var collider = bone.GetComponent<Collider>();
            Vector3 direction = ImpactDirection(index);
            Physics.SyncTransforms();
            Vector3 centre = collider.bounds.center;
            Vector3 point = centre;
            var proxy = animator.GetBoneTransform(HumanBodyBones.Hips).GetComponent<Rigidbody>();
            var anatomy = brain.GetComponentsInChildren<Collider>(true).Where(c => c.attachedRigidbody != null && c.attachedRigidbody != proxy).ToArray();
            bool exposed = false;
            // Aim at an exposed part of the requested anatomical collider. The
            // current rifle pose can put an arm in front of the middle of the chest.
            foreach (float height in new[] { .0f, .65f, -.65f, .85f, -.85f })
            {
                point = Vector3.Lerp(centre, collider.ClosestPoint(centre + Vector3.up * Mathf.Sign(height)), Mathf.Abs(height));
                var ray = new Ray(point - direction * 3f, direction);
                float nearest = 4f; Collider first = null;
                foreach (var candidate in anatomy)
                    if (candidate.enabled && candidate.Raycast(ray, out RaycastHit boneHit, nearest))
                    { nearest = boneHit.distance; first = candidate; }
                if (first != collider) continue;
                exposed = true; break;
            }
            PlayCheck(exposed, "Requested anatomical region has an unobstructed test shot: " + MovingImpactNames[index]);
            PlayCheck(Physics.Raycast(point - direction * 3f, direction, out RaycastHit hit, 4f,
                (1 << brain.gameObject.layer) | (1 << collider.gameObject.layer), QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(brain.transform),
                "Moving impact uses a real owner hitscan: " + MovingImpactNames[index]);
            return hit;
        }

        private static IEnumerator MovingImpactChecks(GameLifetimeScope scope, EnemyWorld world)
        {
            var report = new StringBuilder("mode,case,part,impulse,angularKick,peakLocalAngle,travel,rise,span,meanPoseError\n");
            Quaternion[,] deltas = new Quaternion[2, MovingImpactNames.Length * 2];
            var camera = CreateFinalCamera();
            try
            {
                for (int mode = 0; mode < 2; mode++)
                for (int index = 0; index < MovingImpactNames.Length; index++)
                using (var fixture = new RiflemanTacticalFixture(scope, world, 1, "MovingImpact"))
                {
                    var sequence = HumanMovingImpact(index, fixture, camera, mode == 0, report, deltas, mode);
                    try { while (sequence.MoveNext()) yield return sequence.Current; }
                    finally { (sequence as System.IDisposable)?.Dispose(); }
                }
                for (int index = 0; index < MovingImpactNames.Length; index++)
                {
                    float difference = Quaternion.Angle(deltas[0,index], deltas[1,index]);
                    float neckDifference = Quaternion.Angle(deltas[0,index+MovingImpactNames.Length], deltas[1,index+MovingImpactNames.Length]);
                    PlayCheck(difference > 5f || index >= 4 && neckDifference > 5f, "Authored Hit adds visible physical reaction beyond equally guided locomotion: " + MovingImpactNames[index] + " part=" + difference + " neck=" + neckDifference);
                }
            }
            finally { if (camera != null) Object.Destroy(camera.gameObject); File.WriteAllText(EnemyTools.Evidence + "/moving-impact.csv", report.ToString()); }
        }

        private static IEnumerator HumanMovingImpact(int index, RiflemanTacticalFixture fixture, Camera camera = null,
            bool control = false, StringBuilder report = null, Quaternion[,] deltas = null, int mode = 1)
        {
            yield return null;
            var brain = fixture.Brains[0]; var actor = fixture.Actors[0];
            var animator = brain.GetComponent<EnemyAnimationBridge>().Animator;
            var active = brain.GetComponent<EnemyActiveRagdoll>(); var ragdoll = brain.GetComponent<EnemyRagdollPresenter>();
            brain.enabled = false; brain.ResetForSpawn(); actor.Navigation.ResetAt(new Vector3(0,0,-8));
            brain.transform.rotation = Quaternion.identity;
            brain.States.Change(EnemyStateId.Combat, "moving fatal local impact");
            brain.Memory.Target = fixture.Target; brain.Memory.Visible = brain.Memory.HasContact = true;
            fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
            if (camera != null) SetMovingImpactCamera(camera, brain.transform.position);
            if (index >= 6) brain.GetComponent<EnemyAnimationBridge>().SetCrouching(true);
            foreach (var wait in WaitEnumerable(1.6f)) yield return wait;
            foreach (var wait in Enumerate(ReviewMove(fixture, Vector3.forward, index >= 6 ? EnemyMovePace.Walk : EnemyMovePace.Run, 1.4f))) { if (camera != null) SetMovingImpactCamera(camera, brain.transform.position); yield return wait; }
            var body = animator.GetBoneTransform(ImpactRegion(index)).GetComponent<Rigidbody>();
            var parent = body.GetComponent<CharacterJoint>().connectedBody;
            Quaternion initial = Quaternion.Inverse(parent.rotation) * body.rotation;
            var head = animator.GetBoneTransform(HumanBodyBones.Head).GetComponent<Rigidbody>();
            var headParent = head.GetComponent<CharacterJoint>().connectedBody;
            Quaternion initialHead = Quaternion.Inverse(headParent.rotation) * head.rotation;
            Vector3 velocity = actor.Navigation.Velocity;
            var physical = brain.GetComponentsInChildren<Rigidbody>(true);
            Vector3 centre = RagdollCentre(physical), root = brain.transform.position;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float initialY = hips.position.y;
            if (camera != null)
            {
                SetMovingImpactCamera(camera, root);
            }
            var geometry = MovingImpactShot(brain, animator, index);
            AppendResult("SHOT GEOMETRY: case="+index+" first="+geometry.collider.name+" point="+geometry.point+" intended="+body.name);
            TacticalReviewPhase = "Moving fatal hit / " + ImpactRegion(index);
            actor.Health.TakeDamage(new DamageInfo(100000, geometry.point, ImpactDirection(index), fixture.Target.gameObject, geometry.collider));
            if (control)
            {
                // Identical physical guidance and locomotion, with only the Hit
                // action removed. This isolates authored reaction from inertia.
                active.PoseAnimator.SetBool("IsHit", false);
                active.PoseAnimator.Play("Actions.None", 1, 0f);
            }
            PlayCheck(ragdoll.IsRagdoll && !animator.enabled && !active.IsStationaryDeath && active.DeathTargetClip == null,
                "Moving local hit retains immediate locomotion-pose physics and bypasses stationary Death");
            PlayCheck(Vector3.Distance(ragdoll.AppliedMomentum, Vector3.ProjectOnPlane(velocity, Vector3.up)) < .001f,
                "Local strike preserves independently captured travel inertia");
            PlayCheck(active.HitBody == body, "Hit geometry resolves to requested anatomy: " + active.HitBody?.name + " expected=" + body.name);
            PlayCheck(active.IsMovingHitDeath && active.AppliedHitImpulse == Vector3.zero && active.AppliedMovingHitAngularChange == Vector3.zero,
                "Moving Hit animation replaces the artificial kick; all bodies are dynamic");
            PlayCheck(physical.All(part => !part.isKinematic), "Moving Hit guide leaves every physical body dynamic");
            float started = Time.time, peakAngle = 0, rise = 0, span = 0, travel = 0;
            int captured = 0; float[] samples = { .04f, .1f, .18f, .3f, .6f, 1.2f };
            bool measured = false, hitChecked = false;
            float poseError = 0f; int poseSamples = 0;
            bool[] poseMeasured = new bool[3];
            float[] poseTimes = { .1f, .18f, .3f };
            HumanBodyBones[] upperBones = { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
            while (Time.time < started + 1.4f)
            {
                Quaternion delta = (Quaternion.Inverse(parent.rotation) * body.rotation) * Quaternion.Inverse(initial);
                if (Time.time < started + .3f) peakAngle = Mathf.Max(peakAngle, Quaternion.Angle(Quaternion.identity, delta));
                if (!measured && Time.time >= started + .25f)
                { measured = true; if (deltas != null) { deltas[mode,index] = delta; deltas[mode,index+MovingImpactNames.Length] = (Quaternion.Inverse(headParent.rotation) * head.rotation) * Quaternion.Inverse(initialHead); } }
                if (!control && !hitChecked && Time.time >= started + .18f)
                {
                    hitChecked = true;
                    string clipName = active.HitTargetClip != null ? active.HitTargetClip.name.ToLowerInvariant() : "";
                    string side = index == 2 || index == 4 || index == 6 ? "left" : index == 3 || index == 5 || index == 7 ? "right" : "hit";
                    PlayCheck(active.HitTargetClip != null && active.HitTargetWeight > .1f && active.PoseAnimator.GetCurrentAnimatorStateInfo(1).IsTag("Hit") && clipName.Contains(side) && (index < 6 || clipName.Contains("crouch")),
                        "Existing Hit clip selects correct stance and incoming side: " + clipName);
                }
                for (int sample = 0; sample < poseTimes.Length; sample++)
                {
                    if (poseMeasured[sample] || Time.time < started + poseTimes[sample]) continue;
                    poseMeasured[sample] = true;
                    foreach (var boneId in upperBones)
                    {
                        poseError += Vector3.Distance(animator.GetBoneTransform(boneId).position, active.PoseAnimator.GetBoneTransform(boneId).position);
                        poseSamples++;
                    }
                }
                rise = Mathf.Max(rise, hips.position.y - initialY);
                foreach (var part in physical) span = Mathf.Max(span, Vector3.Distance(part.position, hips.position));
                travel = Mathf.Max(travel, Vector3.Dot(RagdollCentre(physical) - centre, velocity.normalized));
                if (camera != null && (report != null || SessionState.GetBool("EnemyTools.CrouchDeath.Capture", false)) && captured < samples.Length && Time.time >= started + samples[captured])
                    ScreenCapture.CaptureScreenshot(EnemyTools.Evidence + "/moving-impact-" + mode + "-" + index + "-" + captured++ + ".png");
                yield return null;
            }
            PlayCheck(rise < .15f && span < 2.2f && travel > .15f && !active.IsActive && active.DeathTargetClip == null,
                "Moving impact stays grounded/bounded, carries forward and becomes passive | rise="+rise+" span="+span+" travel="+travel);
            float meanPoseError = poseError / Mathf.Max(1, poseSamples);
            PlayCheck(poseSamples == upperBones.Length * poseTimes.Length && meanPoseError < .22f,
                "Dynamic body follows the authored early Hit pose: mean anatomical error=" + meanPoseError);
            report?.AppendLine(System.FormattableString.Invariant($"{mode},{index},{body.name},{active.AppliedHitImpulse.magnitude:F3},{active.AppliedMovingHitAngularChange.magnitude:F3},{peakAngle:F3},{travel:F3},{rise:F3},{span:F3},{meanPoseError:F3}"));
            brain.gameObject.SetActive(false); brain.gameObject.SetActive(true); yield return null; brain.ResetForSpawn(); brain.enabled = false;
            PlayCheck(active.HitBody == null && active.AppliedMovingHitAngularChange == Vector3.zero && !active.IsMovingHitDeath && active.HitTargetClip == null && animator.enabled && physical.All(b => b.isKinematic),
                "Moving impact state clears on pooled reuse");
        }
    }
}
