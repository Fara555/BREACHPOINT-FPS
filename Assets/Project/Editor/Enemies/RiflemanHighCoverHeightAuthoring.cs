using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    internal static class RiflemanHighCoverHeightAuthoring
    {
        private const int SampleCount = 120;
        private const float ReachMargin = .001f;

        [MenuItem("Breachpoint/Enemies/Animator Authoring/Stabilize High Cover exit height")]
        internal static void Stabilize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before height authoring.");
            var prefab = PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            var meshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            AnimatorOverrideController overrides = null;
            HumanPoseHandler poseHandler = null;
            try
            {
                var animator = prefab.GetComponent<EnemyAnimationBridge>().Animator;
                var rig = animator.GetComponent<RigBuilder>(); rig.Clear(); rig.enabled = false;
                overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = overrides;
                animator.applyRootMotion = false;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                var thighs = new[] { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
                var calves = new[] { animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
                var footBones = new HashSet<Transform>(feet.Concat(new[] { animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes) }));
                foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (renderer.sharedMesh != null) meshes.Add(renderer, new Mesh());
                poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);

                void Sample(AnimationClip clip, float n)
                {
                    overrides["Rifle Idle"] = clip; animator.Rebind();
                    animator.SetInteger("HighCoverPhase", 0); animator.SetBool("IsCombat", false);
                    animator.SetLayerWeight(1, 0); animator.SetLayerWeight(2, 0);
                    animator.speed = 1; animator.Play("Base Layer.SteadyIdle", 0, n); animator.Update(.000001f); animator.speed = 0;
                }
                float Sole()
                {
                    float minimum = float.PositiveInfinity;
                    foreach (var pair in meshes)
                    {
                        var renderer = pair.Key; renderer.BakeMesh(pair.Value);
                        var vertices = pair.Value.vertices; var weights = renderer.sharedMesh.boneWeights; var bones = renderer.bones;
                        for (int index = 0; index < vertices.Length; index++)
                        {
                            var w = weights[index];
                            float Weight(int bone, float value) => footBones.Contains(bones[bone]) ? value : 0;
                            if (Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3) > .5f)
                                minimum = Mathf.Min(minimum, renderer.transform.TransformPoint(vertices[index]).y);
                        }
                    }
                    if (float.IsInfinity(minimum)) throw new InvalidOperationException("Missing foot skin during height authoring.");
                    return minimum;
                }
                var aim = EnemyValidationRunner.SourceClip("idle aiming");
                Sample(aim, 0);
                float endHeight = hips.position.y - Sole();
                var muscleIndices = Enumerable.Range(0, HumanTrait.MuscleCount)
                    .Where(index => HumanTrait.MuscleName[index].Contains(" Leg ") || HumanTrait.MuscleName[index].Contains(" Foot ") || HumanTrait.MuscleName[index].Contains(" Toes ")).ToArray();
                for (int side = 0; side < 2; side++)
                {
                    string folder = EnemyAnimationAssetPaths.Derived + "/HighCover/";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "LeavingHighCover" + (side == 1 ? "Right" : "") + ".anim");
                    var reference = Object.Instantiate(clip);
                    try
                    {
                        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "HighCoverIdle" + (side == 1 ? "Right" : "") + ".anim");
                        Sample(idle, .55f);
                        float startHeight = hips.position.y - Sole();
                        var rootBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
                        var sourceY = AnimationUtility.GetEditorCurve(reference, rootBinding);
                        var rootY = new AnimationCurve();
                        var muscleCurves = muscleIndices.ToDictionary(index => index, index => new AnimationCurve());
                        float beforeMin = float.PositiveInfinity, beforeMax = float.NegativeInfinity;
                        float maximumBodyDelta = 0, maximumReachLoss = 0;
                        for (int sample = 0; sample <= SampleCount; sample++)
                        {
                            float n = sample / (float)SampleCount, time = n * reference.length;
                            Sample(reference, n);
                            float sole = Sole();
                            beforeMin = Mathf.Min(beforeMin, hips.position.y - sole); beforeMax = Mathf.Max(beforeMax, hips.position.y - sole);
                            var before = new HumanPose(); poseHandler.GetHumanPose(ref before);
                            var targets = feet.Select(foot => foot.position - Vector3.up * sole).ToArray();
                            var rotations = feet.Select(foot => foot.rotation).ToArray();
                            float desired = Mathf.Lerp(startHeight, endHeight, Mathf.SmoothStep(0, 1, n));
                            // Respect leg reach without shifting the authored footprints.
                            for (int leg = 0; leg < 2; leg++)
                            {
                                float length = Vector3.Distance(thighs[leg].position, calves[leg].position) + Vector3.Distance(calves[leg].position, feet[leg].position) - ReachMargin;
                                float horizontal = Vector3.ProjectOnPlane(targets[leg] - thighs[leg].position, Vector3.up).sqrMagnitude;
                                float available = Mathf.Sqrt(Mathf.Max(0, length * length - horizontal));
                                float ceiling = targets[leg].y + available - (thighs[leg].position.y - hips.position.y);
                                desired = Mathf.Min(desired, ceiling);
                            }
                            float delta = desired - hips.position.y;
                            maximumBodyDelta = Mathf.Max(maximumBodyDelta, Mathf.Abs(delta));
                            hips.position += Vector3.up * delta;
                            for (int leg = 0; leg < 2; leg++)
                            {
                                SolveLeg(thighs[leg], calves[leg], feet[leg], targets[leg]);
                                feet[leg].rotation = rotations[leg];
                                maximumReachLoss = Mathf.Max(maximumReachLoss, Vector3.Distance(feet[leg].position, targets[leg]));
                            }
                            var after = new HumanPose(); poseHandler.GetHumanPose(ref after);
                            rootY.AddKey(time, sourceY.Evaluate(time) + after.bodyPosition.y - before.bodyPosition.y);
                            foreach (int index in muscleIndices) muscleCurves[index].AddKey(time, after.muscles[index]);
                        }
                        AnimationUtility.SetEditorCurve(clip, rootBinding, rootY);
                        foreach (int index in muscleIndices)
                            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[index]), muscleCurves[index]);
                        EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
                        float afterMin = float.PositiveInfinity, afterMax = float.NegativeInfinity;
                        for (int sample = 0; sample <= SampleCount; sample++)
                        {
                            Sample(clip, sample / (float)SampleCount);
                            float height = hips.position.y - Sole();
                            afterMin = Mathf.Min(afterMin, height); afterMax = Mathf.Max(afterMax, height);
                        }
                        report.AppendLine(FormattableString.Invariant($"side={side} grounded hip before={beforeMin:F4}..{beforeMax:F4} after={afterMin:F4}..{afterMax:F4} bodyDelta={maximumBodyDelta:F4} reachLoss={maximumReachLoss:F5}"));
                    }
                    finally { Object.DestroyImmediate(reference); }
                }
            }
            finally
            {
                poseHandler?.Dispose();
                if (overrides != null) Object.DestroyImmediate(overrides);
                foreach (var mesh in meshes.Values) Object.DestroyImmediate(mesh);
                PrefabUtility.UnloadPrefabContents(prefab);
                Directory.CreateDirectory(EnemyTools.Evidence);
                File.WriteAllText(EnemyTools.Evidence + "/high-cover-height-authoring.txt", report.ToString());
            }
        }

        private static void SolveLeg(Transform thigh, Transform calf, Transform foot, Vector3 target)
        {
            Vector3 root = thigh.position;
            float upper = Vector3.Distance(root, calf.position), lower = Vector3.Distance(calf.position, foot.position);
            Vector3 direction = target - root;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upper - lower) + ReachMargin, upper + lower - ReachMargin);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(calf.position - root, direction).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.ProjectOnPlane(thigh.forward, direction).normalized;
            float along = Mathf.Clamp((upper * upper + distance * distance - lower * lower) / (2 * distance), -upper, upper);
            Vector3 knee = root + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            thigh.rotation = Quaternion.FromToRotation(calf.position - root, knee - root) * thigh.rotation;
            calf.rotation = Quaternion.FromToRotation(foot.position - calf.position, root + direction * distance - calf.position) * calf.rotation;
        }
    }
}
