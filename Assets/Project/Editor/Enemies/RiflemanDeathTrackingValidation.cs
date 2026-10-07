using System.Collections;
using System.Globalization;
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
        private static readonly HumanBodyBones[] DeathTrackedBones = {
            HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm
        };

        // Compare visible physical anatomy with a separate animation-only run.
        // Clip selection and non-zero torque are insufficient regression criteria.
        private static IEnumerator DeathTrackingChecks(GameLifetimeScope scope, EnemyWorld world)
        {
            string label = SessionState.GetString("EnemyTools.DeathTracking.Label", "before");
            float[] times = { .1f, .3f, .7f, 1.2f, 1.8f, 2.4f, 3.7f };
            Vector3[,,,] positions = new Vector3[2, 4, times.Length, DeathTrackedBones.Length];
            var csv = new StringBuilder("mode,direction,time,active,support,lost,legs,torso,clip,normalized,pelvisY,headY,meanTargetError\n");
            var camera = CreateFinalCamera();
            try
            {
                for (int mode = 0; mode < 2; mode++)
                for (int direction = 0; direction < 4; direction++)
                using (var fixture = new RiflemanTacticalFixture(scope, world, 1, "DeathTracking"))
                {
                    yield return null;
                    var brain = fixture.Brains[0]; var actor = fixture.Actors[0];
                    var animator = brain.GetComponent<EnemyAnimationBridge>().Animator;
                    var active = brain.GetComponent<EnemyActiveRagdoll>();
                    brain.enabled = false; brain.ResetForSpawn();
                    actor.Navigation.ResetAt(new Vector3(0, 0, -8));
                    brain.transform.rotation = Quaternion.identity;
                    brain.States.Change(EnemyStateId.Combat, "stationary physical tracking regression");
                    fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
                    foreach (var wait in WaitEnumerable(1.6f)) yield return wait;
                    if (mode == 0)
                    {
                        active.enabled = false;
                        var fallback = new SerializedObject(brain.GetComponent<EnemyRagdollPresenter>());
                        fallback.FindProperty("_takeoverDelay").floatValue = 10f;
                        fallback.FindProperty("_maximumAnimationWait").floatValue = 10f;
                        fallback.ApplyModifiedPropertiesWithoutUndo();
                    }
                    camera.fieldOfView = 40f;
                    camera.transform.position = brain.transform.position + new Vector3(3.8f, 2.3f, 4f);
                    camera.transform.LookAt(brain.transform.position + Vector3.up * .85f);
                    Vector3 travel = direction == 1 ? Vector3.forward : direction == 2 ? Vector3.right : direction == 3 ? Vector3.left : Vector3.back;
                    fixture.Target.transform.position = brain.transform.position - travel * 8;
                    actor.Health.TakeDamage(new DamageInfo(100000, actor.Eyes.position, travel, fixture.Target.gameObject));
                    float start = Time.time;
                    for (int sample = 0; sample < times.Length; sample++)
                    {
                        while (Time.time < start + times[sample]) yield return null;
                        float error = 0;
                        for (int bone = 0; bone < DeathTrackedBones.Length; bone++)
                        {
                            var actual = animator.GetBoneTransform(DeathTrackedBones[bone]);
                            positions[mode, direction, sample, bone] = actual.position - brain.transform.position;
                            if (mode == 1) error += Vector3.Distance(actual.position, active.PoseAnimator.GetBoneTransform(DeathTrackedBones[bone]).position);
                        }
                        var reference = mode == 0 ? animator : active.PoseAnimator;
                        var clips = reference.GetCurrentAnimatorClipInfo(0);
                        csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2:F3},{3},{4},{5},{6:F3},{7:F3},{8},{9:F3},{10:F3},{11:F3},{12:F3}",
                            mode == 0 ? "authored" : "physical", direction, Time.time-start, active.IsActive, active.HasSupport, active.HasLostBalance,
                            active.LegStrength, active.UpperBodyStrength, string.Join(";", clips.Select(c => c.clip.name)), reference.GetCurrentAnimatorStateInfo(0).normalizedTime,
                            positions[mode,direction,sample,0].y, positions[mode,direction,sample,1].y, error/DeathTrackedBones.Length));
                        if (mode == 1 && sample == times.Length - 1 && label != "before")
                            PlayCheck(!active.IsActive && brain.GetComponentsInChildren<Rigidbody>(true).All(b => !b.isKinematic), "Directional fall finishes with passive physics, direction=" + direction);
                        if (sample > 0)
                            ScreenCapture.CaptureScreenshot(EnemyTools.Evidence + "/tracking-" + label + "-" + mode + "-" + direction + "-" + sample + ".png");
                        yield return null;
                    }
                }
            }
            finally { if (camera != null) Object.Destroy(camera.gameObject); File.WriteAllText(EnemyTools.Evidence + "/death-tracking-" + label + ".csv", csv.ToString()); }
            for (int direction = 0; direction < 4; direction++)
            {
                float earlyError = 0;
                for (int sample = 0; sample < 3; sample++)
                for (int bone = 0; bone < DeathTrackedBones.Length; bone++)
                    earlyError += Vector3.Distance(positions[0,direction,sample,bone], positions[1,direction,sample,bone]);
                earlyError /= 3 * DeathTrackedBones.Length;
                AppendResult($"PHYSICAL TRACKING direction={direction}: early mean bone error={earlyError:F3} m; physical head at .3={positions[1,direction,1,1].y:F3}; authored={positions[0,direction,1,1].y:F3}");
                if (label != "before") PlayCheck(earlyError < .22f, "Physical body follows early authored fall, direction=" + direction + " error=" + earlyError);
            }
            if (label != "before")
            {
                float separation = 0f, authoredSeparation = 0f;
                for (int bone = 0; bone < DeathTrackedBones.Length; bone++)
                {
                    separation += Vector3.Distance(positions[1,2,3,bone], positions[1,3,3,bone]);
                    authoredSeparation += Vector3.Distance(positions[0,2,3,bone], positions[0,3,3,bone]);
                }
                separation /= DeathTrackedBones.Length; authoredSeparation /= DeathTrackedBones.Length;
                PlayCheck(separation > .15f && separation > authoredSeparation * .7f,
                    "Opposite stationary impacts retain the authored anatomical separation: physical=" + separation + " authored=" + authoredSeparation);
            }
        }
    }
}
