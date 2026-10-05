using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal static void AuditCombatStrafe()
        {
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n" + ConsoleCounts() + "\n");
            foreach (string pace in new[] { "walk", "run" })
            foreach (string direction in new[] { "forward", "backward", "left", "right", "forward left", "forward right", "backward left", "backward right" })
            {
                var clip = SourceClip(pace + " " + direction);
                report.AppendLine(FormattableString.Invariant($"{clip.name}: length={clip.length:F4}s average={clip.averageSpeed} natural={clip.averageSpeed.magnitude:F4}m/s strideDistance={clip.length*clip.averageSpeed.magnitude:F4}m"));
            }
            File.WriteAllText(EnemyTools.Evidence + "/strafe-clips.txt", report.ToString());
        }
        private static IEnumerator CombatStrafeProbe(GameLifetimeScope scope, EnemyWorld world)
        {
            using (var fixture = new RiflemanTacticalFixture(scope, world, 1, "StrafeProbe"))
            {
                yield return null;
                var brain = fixture.Brains[0]; var actor = fixture.Actors[0]; var bridge = brain.GetComponent<EnemyAnimationBridge>();
                brain.enabled = false;
                var clips = new List<AnimatorClipInfo>(10); var nextClips = new List<AnimatorClipInfo>(10); var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
                bool acceptance = SessionState.GetBool("EnemyTools.Strafe.Acceptance", false);
                string label = SessionState.GetString("EnemyTools.Strafe.Label", "before");
                try
                {
                    for (int caseIndex = 0; caseIndex < 7; caseIndex++)
                    {
                        bool cold = caseIndex == 6;
                        var pace = caseIndex >= 3 && !cold ? EnemyMovePace.Run : EnemyMovePace.Walk;
                        Vector3 direction = caseIndex % 3 == 0 || cold ? Vector3.left : caseIndex % 3 == 1 ? Vector3.right : new Vector3(-1,0,1).normalized;
                        brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(new Vector3(0,0,-12)); brain.transform.rotation = Quaternion.identity;
                        brain.States.Change(EnemyStateId.Combat, "combat strafe probe");
                        brain.Memory.Target = fixture.Target; brain.Memory.Visible = brain.Memory.HasContact = true;
                        fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
                        foreach (var wait in WaitEnumerable(cold ? .05f : 1.6f)) yield return wait;
                        var left = bridge.Animator.GetBoneTransform(HumanBodyBones.LeftFoot); var right = bridge.Animator.GetBoneTransform(HumanBodyBones.RightFoot);
                        Vector3 leftLocal = brain.transform.InverseTransformPoint(left.position), rightLocal = brain.transform.InverseTransformPoint(right.position);
                        Vector3 start = brain.transform.position, goal = start + direction * 16, previousLeft = left.position, previousRight = right.position;
                        float groundLeft = left.position.y, groundRight = right.position.y;
                        float began = Time.time, translatedAt = -1, directionAt = -1, clipAt = -1, footAt = -1, distanceBeforeFoot = -1;
                        float speedSum = 0, strideSum = 0, weightSum = 0, phaseSum = 0, elapsedSum = 0, naturalSum = 0, previousNorm = 0, previousTime = Time.time;
                        int samples = 0, lastFrame = -1; var slip = new List<float>(); string dominantName = "none"; float dominantLength = 0;
                        while (Time.time < began + 3.6f)
                        {
                            actor.Navigation.MoveTo(goal, pace, Time.time);
                            fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
                            actor.Navigation.Face(fixture.Target.transform.position, Time.deltaTime);
                            if (lastFrame != Time.frameCount)
                            {
                                lastFrame = Time.frameCount; float elapsed = Time.time - began, travel = Vector3.Distance(start, brain.transform.position);
                                Vector2 desired = new Vector2(direction.x,direction.z), blend = bridge.MoveDirectionSmoothed;
                                if (translatedAt < 0 && travel > .001f) translatedAt = elapsed;
                                if (directionAt < 0 && Vector2.Dot(desired,blend) > .7f) directionAt = elapsed;
                                bridge.Animator.GetCurrentAnimatorClipInfo(0, clips);
                                bool transitioning = bridge.Animator.IsInTransition(0);
                                float transitionWeight = transitioning ? Mathf.Clamp01(bridge.Animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0f;
                                nextClips.Clear();
                                if (transitioning) bridge.Animator.GetNextAnimatorClipInfo(0, nextClips);
                                float movingWeight = 0, weightedNatural = 0, weightedLength = 0;
                                void Accumulate(AnimatorClipInfo clip, float stateWeight)
                                {
                                    if (!clip.clip.name.StartsWith("walk ", StringComparison.OrdinalIgnoreCase) && !clip.clip.name.StartsWith("run ", StringComparison.OrdinalIgnoreCase)) return;
                                    float weight = clip.weight * stateWeight;
                                    movingWeight += weight; weightedNatural += clip.clip.averageSpeed.magnitude * weight; weightedLength += clip.clip.length * weight;
                                    if (weight > .5f) { dominantName = clip.clip.name; dominantLength = clip.clip.length; }
                                }
                                foreach (var clip in clips) Accumulate(clip,1f-transitionWeight);
                                foreach (var clip in nextClips) Accumulate(clip,transitionWeight);
                                if (clipAt < 0 && movingWeight > .15f) clipAt = elapsed;
                                float feet = Vector3.Distance(leftLocal,brain.transform.InverseTransformPoint(left.position)) + Vector3.Distance(rightLocal,brain.transform.InverseTransformPoint(right.position));
                                if (footAt < 0 && movingWeight > .15f && feet > .025f) { footAt = elapsed; distanceBeforeFoot = travel; }
                                float norm = bridge.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, dt = Time.time - previousTime;
                                if (elapsed > 1.7f && elapsed < 3.3f && dt > 0)
                                {
                                    samples++; speedSum += actor.Navigation.Velocity.magnitude; strideSum += bridge.Animator.GetFloat("CombatStride"); weightSum += movingWeight; naturalSum += weightedNatural / Mathf.Max(.001f,movingWeight);
                                    phaseSum += (norm - previousNorm) * weightedLength / Mathf.Max(.001f,movingWeight); elapsedSum += dt;
                                    if (left.position.y < groundLeft+.025f && previousLeft.y < groundLeft+.025f) slip.Add(Mathf.Abs(Vector3.Dot(left.position-previousLeft,direction))/dt);
                                    if (right.position.y < groundRight+.025f && previousRight.y < groundRight+.025f) slip.Add(Mathf.Abs(Vector3.Dot(right.position-previousRight,direction))/dt);
                                }
                                previousLeft = left.position; previousRight = right.position; previousNorm = norm; previousTime = Time.time;
                            }
                            yield return null;
                        }
                        float actual = speedSum / Mathf.Max(1,samples), stride = strideSum / Mathf.Max(1,samples), natural = naturalSum / Mathf.Max(1,samples);
                        float effective = phaseSum / Mathf.Max(.001f,elapsedSum);
                        slip.Sort(); float median = slip.Count > 0 ? slip[slip.Count/2] : -1;
                        report.AppendLine(FormattableString.Invariant($"{pace}/{direction}/cold={cold}: worldStart={translatedAt:F4}s direction={directionAt:F4}s movingClip={clipAt:F4}s firstFoot={footAt:F4}s beforeFoot={distanceBeforeFoot:F4}m actual={actual:F4} stride={stride:F4} effective={effective:F4} natural={natural:F4} cadence={dominantLength/Mathf.Max(.001f,effective):F4}s contactSlip={median:F4}m/s clip={dominantName} weight={weightSum/Mathf.Max(1,samples):F4}"));
                        if (acceptance)
                        {
                            PlayCheck(footAt >= 0 && footAt < (cold ? 2.4f : .2f) && distanceBeforeFoot < .01f && footAt <= translatedAt + .06f && directionAt < .08f && clipAt < (cold ? 2.4f : .12f), "Combat strafe intent and feet precede significant translation: " + pace + "/" + direction);
                            PlayCheck(samples > 20 && Mathf.Abs(actual - (pace == EnemyMovePace.Run ? brain.Config.Movement.RunSpeed : brain.Config.Movement.WalkSpeed)) < .12f, "Combat strafe retains configured gameplay speed: " + pace + "/" + direction);
                            PlayCheck(Mathf.Abs(natural*effective-actual) < actual*.18f, "Directional source cadence matches sustained translation: " + pace + "/" + direction);
                        }
                        actor.Navigation.RequestStop(); foreach (var wait in WaitEnumerable(.8f)) yield return wait;
                        if (acceptance) PlayCheck(actor.Navigation.Velocity.magnitude < .03f && bridge.MoveSpeedSmoothed < .015f, "Combat strafe ordinary stop settles without a movement lock");
                    }
                    if (!acceptance) PlayCheck(true, "Combat strafe baseline recorded without changing gameplay or calibration");
                }
                finally { File.WriteAllText(EnemyTools.Evidence + "/strafe-" + label + ".txt", report.ToString()); }
            }
        }
    }
}
