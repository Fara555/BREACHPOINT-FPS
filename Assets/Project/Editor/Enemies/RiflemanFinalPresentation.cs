using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private static IEnumerator FinalShortReview(GameLifetimeScope scope, EnemyWorld world)
        {
            SessionState.SetString("EnemyTools.Review.RecordGroup", "FinalShort");
            SessionState.SetInt("EnemyTools.Review.Scenario", 30);
            SessionState.SetBool("EnemyTools.Review.Record", true);
            IEnumerator review = LiveReviewTests(scope, world, false);
            try { while (review.MoveNext()) yield return review.Current; }
            finally { (review as IDisposable)?.Dispose(); SessionState.SetString("EnemyTools.Review.RecordGroup", ""); }
        }
        private static IEnumerator FinalReviewSuite(GameLifetimeScope scope, EnemyWorld world)
        {
            SessionState.SetString("EnemyTools.Review.RecordGroup", "FinalTargeted");
            try
            {
                foreach (int index in new[] { 0, 30, 27, 68, 69, 70, 71, 72, 73, 10, 11 })
                {
                    SessionState.SetInt("EnemyTools.Review.Scenario", index);
                    SessionState.SetBool("EnemyTools.Review.Record", true);
                    IEnumerator review = LiveReviewTests(scope, world, false);
                    try { while (review.MoveNext()) yield return review.Current; }
                    finally { (review as IDisposable)?.Dispose(); }
                }
            }
            finally { SessionState.SetString("EnemyTools.Review.RecordGroup", ""); }
        }
        private static IEnumerator FinalFocusedReview(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            var presenter = enemy.GetComponent<EnemyRigPresenter>();
            if (index < 3)
            {
                float height = index == 1 ? .6f : index == 2 ? -.6f : 0f;
                target.transform.position = AnimationStart + Vector3.forward * 10 + Vector3.up * height;
                Physics.SyncTransforms(); foreach (var wait in WaitReview(.7f)) yield return wait;
                brain.States.Change(EnemyStateId.Combat, "focused target-aware raise");
                float began = Time.time, maximumLateError = 0, maximumHandoffPitchStep = 0, previousPitch = PosePitch(actor.Muzzle), previousTime = Time.time;
                bool observedRaise = false; int lastFrame = -1;
                while (Time.time < began + 2.8f)
                {
                    float elapsed = Time.time - began;
                    if (index == 2 && elapsed > .32f && elapsed < .75f)
                    {
                        target.transform.position = AnimationStart + Vector3.forward * 10 + Vector3.up * Mathf.Lerp(-.6f, -.25f, Mathf.SmoothStep(0, 1, (elapsed - .32f) / .43f));
                        Physics.SyncTransforms();
                    }
                    if (bridge.IsRaising) observedRaise = true;
                    if (Time.frameCount != lastFrame)
                    {
                        lastFrame = Time.frameCount; float pitch = PosePitch(actor.Muzzle);
                        if (elapsed > .7f && elapsed < 1.2f && Time.time > previousTime)
                            maximumHandoffPitchStep = Mathf.Max(maximumHandoffPitchStep, Mathf.Abs(pitch - previousPitch) / (Time.time - previousTime));
                        if (elapsed > .65f && elapsed < 1.2f) maximumLateError = Mathf.Max(maximumLateError, Mathf.Abs(bridge.VerticalMuzzleErrorDegrees));
                        previousPitch = pitch; previousTime = Time.time;
                    }
                    yield return null;
                }
                AppendResult(FormattableString.Invariant($"RAISE height={height}: late vertical={maximumLateError:F3}deg handoff pitch rate={maximumHandoffPitchStep:F3}deg/s final={bridge.VerticalMuzzleErrorDegrees:F3} coarse={presenter.CoarseWeight:F3}"));
                PlayCheck(observedRaise && maximumLateError < 4f, "Raise already converges during its late gesture, including a moving height target");
                PlayCheck(maximumHandoffPitchStep < 65f && Mathf.Abs(bridge.VerticalMuzzleErrorDegrees) < 3f, "Raise/Combat handoff has no late pitch snap and ends aligned");
                var hand = enemy.GetComponentInChildren<TwoBoneIKConstraint>();
                PlayCheck(Vector3.Distance(hand.data.tip.position, hand.data.target.position) < .03f, "Target-aware Raise retains the authored final hand grip");
                yield break;
            }
            brain.States.Change(EnemyStateId.Combat, "focused Sprint presentation");
            Vector3 goal = AnimationStart + Vector3.forward * 24;
            if (index == 4)
            {
                actor.Navigation.ResetAt(new Vector3(-14, 0, -14)); enemy.transform.rotation = Quaternion.Euler(0, 45, 0);
                goal = new Vector3(14, 0, 14);
            }
            if (index == 5) { actor.Navigation.ResetAt(new Vector3(4, 0, -15)); goal = new Vector3(4, 0, 17); }
            foreach (var wait in WaitReview(1.8f)) yield return wait;
            int phases = index == 5 ? 3 : 1; float maximumHead = -90, minimumHead = 90, maximumCoarse = 0, maximumAim = 0, minimumStride = 10, maximumStride = 0; int samples = 0;
            for (int phase = 0; phase < phases; phase++)
            {
                EnemyMovePace pace = index == 5 && phase != 1 ? EnemyMovePace.Run : EnemyMovePace.Sprint;
                float began = Time.time, duration = index == 4 ? 8f : index == 5 ? 2.2f : 3f;
                while (Time.time < began + duration)
                {
                    actor.Navigation.MoveTo(goal, pace, Time.time);
                    target.transform.position = enemy.transform.position + enemy.transform.forward * 12;
                    Physics.SyncTransforms(); actor.Navigation.Face(target.transform.position, Time.deltaTime);
                    if (pace == EnemyMovePace.Sprint && Time.time > began + 1.4f && bridge.MoveSpeedSmoothed > .99f)
                    {
                        float head = PosePitch(bridge.Animator.GetBoneTransform(HumanBodyBones.Head));
                        maximumHead = Mathf.Max(maximumHead, head); minimumHead = Mathf.Min(minimumHead, head);
                        maximumCoarse = Mathf.Max(maximumCoarse, presenter.CoarseWeight); maximumAim = Mathf.Max(maximumAim, presenter.AimRigWeight);
                        float stride = bridge.Animator.GetFloat("CombatStride"); minimumStride = Mathf.Min(minimumStride, stride); maximumStride = Mathf.Max(maximumStride, stride); samples++;
                    }
                    yield return null;
                }
            }
            AppendResult(FormattableString.Invariant($"SPRINT samples={samples} head={minimumHead:F3}..{maximumHead:F3} coarseMax={maximumCoarse:F4} aimMax={maximumAim:F4} stride={minimumStride:F4}..{maximumStride:F4}"));
            PlayCheck(samples > 20 && maximumCoarse < .01f && maximumAim < .02f, "Full Sprint smoothly releases chest correction and AimRig while retaining hand IK");
            if (index != 4) PlayCheck(maximumHead < 2f && minimumHead > -10f, "Forward Sprint head pitch resembles the source range instead of the upward correction regression");
            PlayCheck(minimumStride > .75f && maximumStride < .84f, "Sprint keeps its measured cadence, without speeding up its half-second source cycle");
            actor.Navigation.RequestStop(); foreach (var wait in WaitReview(.8f)) yield return wait;
            PlayCheck(actor.Navigation.Velocity.magnitude < .03f && presenter.CoarseWeight > .7f && presenter.AimRigWeight > .95f, "Sprint stop restores ordinary Combat aim smoothly");
        }
        private static Camera CreateFinalCamera()
        {
            var camera = new GameObject("Focused presentation review camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            camera.depth = 100; camera.fieldOfView = 35; camera.nearClipPlane = .03f;
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Show();
            return camera;
        }
        private static void FinalCamera(Camera camera, GameObject enemy)
        {
            camera.transform.position = enemy.transform.position + new Vector3(2.6f, 1.65f, 3.5f);
            camera.transform.LookAt(enemy.transform.position + Vector3.up * .95f);
        }
        private static float PosePitch(Transform bone) => Mathf.Asin(Mathf.Clamp(bone.forward.y, -1, 1)) * Mathf.Rad2Deg;
    }
}
