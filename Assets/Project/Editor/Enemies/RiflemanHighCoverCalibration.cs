using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer.Unity;
using Object = UnityEngine.Object;
namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private const int HighCoverCalibrationSamples = 120;
        private const float ExitFootContactStart = .85f;
        private const float ExitFootContactSettled = .95f;
        internal const string HighProfilePath = EnemyAnimationAssetPaths.Config + "/SO_AdamHighCoverMotion.asset";
        private static IEnumerator HighCoverCalibration(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            yield return null;
            enemy.GetComponent<EnemyBrain>().enabled = false;
            if(enemy.TryGetComponent<EnemyHighCoverPresentation>(out var contact)) contact.enabled=false;
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); bridge.enabled = false;
            enemy.GetComponent<EnemyRigPresenter>().enabled = false;
            var animator = bridge.Animator; var rig = animator.GetComponent<RigBuilder>(); rig.Clear(); rig.enabled = false;
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            var exitToes = new[] { new Vector3[HighCoverCalibrationSamples + 1], new Vector3[HighCoverCalibrationSamples + 1] };
            var meshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
            var profile = AssetDatabase.LoadAssetAtPath<EnemyHighCoverAnimationProfile>(HighProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<EnemyHighCoverAnimationProfile>(); AssetDatabase.CreateAsset(profile, HighProfilePath); }
            try
            {
                foreach (var renderer in enemy.GetComponentsInChildren<SkinnedMeshRenderer>()) if (renderer.sharedMesh != null) meshes.Add(renderer, new Mesh());
                var data = new SerializedObject(profile);
                var footBones = new HashSet<Transform>(new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes }.Select(animator.GetBoneTransform));
                var torsoBones = new HashSet<Transform>(new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }.Select(animator.GetBoneTransform));
                string[] names = { "TakingHighCover", "HighCoverIdle", "LeavingHighCover" };
                for (int side = 0; side < 2; side++)
                {
                    var soles = data.FindProperty(side == 0 ? "_soleLeft" : "_soleRight"); soles.arraySize = 3;
                    for (int phase = 0; phase < 3; phase++)
                    {
                        var curve = new AnimationCurve();
                        for (int sample = 0; sample <= 120; sample++)
                        {
                            float time = sample / 120f;
                            animator.speed = 1f; animator.SetInteger("HighCoverPhase", phase + 1); animator.SetFloat("HighCoverSide", side);
                            animator.SetLayerWeight(1, 0f); animator.SetLayerWeight(2, 0f);
                            animator.Play("Base Layer." + names[phase], 0, time); animator.Update(.00001f); animator.speed = 0f;
                            float minFoot = float.PositiveInfinity, maxBack = float.NegativeInfinity;
                            foreach (var pair in meshes)
                            {
                                var renderer = pair.Key; renderer.BakeMesh(pair.Value);
                                var vertices = pair.Value.vertices; var weights = renderer.sharedMesh.boneWeights; var bones = renderer.bones;
                                for (int v = 0; v < vertices.Length; v++)
                                {
                                    var w = weights[v];
                                    float Foot(int bone, float weight) => footBones.Contains(bones[bone]) ? weight : 0f;
                                    float Torso(int bone, float weight) => torsoBones.Contains(bones[bone]) ? weight : 0f;
                                    Vector3 point = renderer.transform.TransformPoint(vertices[v]) - enemy.transform.position;
                                    if (Foot(w.boneIndex0,w.weight0)+Foot(w.boneIndex1,w.weight1)+Foot(w.boneIndex2,w.weight2)+Foot(w.boneIndex3,w.weight3) > .5f) minFoot = Mathf.Min(minFoot, point.y);
                                    if (Torso(w.boneIndex0,w.weight0)+Torso(w.boneIndex1,w.weight1)+Torso(w.boneIndex2,w.weight2)+Torso(w.boneIndex3,w.weight3) > .5f) maxBack = Mathf.Max(maxBack, point.z);
                                }
                            }
                            if(float.IsInfinity(minFoot)) throw new InvalidOperationException("Missing skinned foot vertices during calibration.");
                            curve.AddKey(time, minFoot);
                            if (phase == 2)
                                exitToes[side][sample] = enemy.transform.InverseTransformPoint(animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes).position);
                            if (phase == 1 && sample == 66)
                            {
                                float headX = (animator.GetBoneTransform(HumanBodyBones.Head).position - enemy.transform.position).x;
                                data.FindProperty(side == 0 ? "_leftReference" : "_rightReference").vector2Value = new Vector2(maxBack, headX);
                                report.AppendLine($"Idle side={side} backDepth={maxBack:F4} headX={headX:F4} sole={minFoot:F4} scale={animator.humanScale:F4}");
                            }
                            if (sample % 20 == 0) yield return null;
                        }
                        soles.GetArrayElementAtIndex(phase).animationCurveValue = curve;
                    }
                }
                // Rest height is needed after the high pose has faded out; fading
                // all height correction to zero reintroduces the baked NavMesh gap.
                animator.speed = 1f; animator.SetInteger("HighCoverPhase", 0); animator.SetBool("IsCombat", true); animator.SetFloat("MoveSpeed", 0f);
                animator.Play("Base Layer.StandingLocomotion", 0, 0); animator.Update(.00001f); animator.speed = 0f;
                float standingSole = float.PositiveInfinity;
                foreach (var pair in meshes)
                {
                    var renderer = pair.Key; renderer.BakeMesh(pair.Value);
                    var vertices = pair.Value.vertices; var weights = renderer.sharedMesh.boneWeights; var bones = renderer.bones;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        var w = weights[index];
                        float Weight(int bone, float value) => footBones.Contains(bones[bone]) ? value : 0f;
                        if (Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3) > .5f)
                            standingSole = Mathf.Min(standingSole, renderer.transform.TransformPoint(vertices[index]).y - enemy.transform.position.y);
                    }
                }
                if (float.IsInfinity(standingSole)) throw new InvalidOperationException("Missing standing foot skin during calibration.");
                data.FindProperty("_standingSoleHeight").floatValue = standingSole;
                report.AppendLine("Standing sole=" + standingSole.ToString("F5"));
                Quaternion Rotation(AnimationClip clip, float t)
                {
                    float Value(string axis) => AnimationUtility.GetEditorCurve(clip, AnimationUtility.GetCurveBindings(clip).First(b => b.propertyName == "RootQ." + axis)).Evaluate(t);
                    return new Quaternion(Value("x"),Value("y"),Value("z"),Value("w")).normalized;
                }
                var taking = RiflemanHighCoverAuthoring.SourceClip("Taking High Cover");
                float startYaw = Rotation(taking,0).eulerAngles.y;
                float coverYaw = Mathf.DeltaAngle(startYaw,Rotation(taking,taking.length).eulerAngles.y);
                foreach (bool take in new[] { true, false })
                {
                    var source = RiflemanHighCoverAuthoring.SourceClip(take ? "Taking High Cover" : "Leaving High Cover");
                    float yaw = take ? -startYaw : coverYaw - Rotation(source,0).eulerAngles.y;
                    var bindings = AnimationUtility.GetCurveBindings(source);
                    var x = AnimationUtility.GetEditorCurve(source,bindings.First(b=>b.propertyName=="RootT.x"));
                    var z = AnimationUtility.GetEditorCurve(source,bindings.First(b=>b.propertyName=="RootT.z"));
                    Vector3 Motion(float n)
                    {
                        float sourceTime = (take ? n : RiflemanHighCoverExitAuthoring.ExitSourceProgress(n)) * source.length;
                        return Quaternion.Euler(0,yaw,0)*new Vector3(x.Evaluate(sourceTime)-x.Evaluate(0),0,z.Evaluate(sourceTime)-z.Evaluate(0))*animator.humanScale;
                    }
                    float contactStart = take ? ExitFootContactStart : RiflemanHighCoverExitAuthoring.ExitPlaybackProgress(ExitFootContactStart);
                    float contactSettled = take ? ExitFootContactSettled : RiflemanHighCoverExitAuthoring.ExitPlaybackProgress(ExitFootContactSettled);
                    for (int side = 0; side < (take ? 1 : 2); side++)
                    {
                        var cx = new AnimationCurve(); var cz = new AnimationCurve();
                        int contactSample = Mathf.RoundToInt(contactStart * HighCoverCalibrationSamples);
                        Vector3 contactMotion = Motion(contactStart);
                        if (side == 1) contactMotion.x = -contactMotion.x;
                        for(int sample=0;sample<=HighCoverCalibrationSamples;sample++)
                        {
                            float n=sample/(float)HighCoverCalibrationSamples;
                            Vector3 motion=Motion(n);
                            if (side == 1) motion.x = -motion.x;
                            if (!take)
                            {
                                // Transfer support to the arriving foot. The pose settles
                                // to IdleAiming, so source travel must also come to rest.
                                Vector3 planted = contactMotion + Vector3.ProjectOnPlane(exitToes[side][contactSample] - exitToes[side][sample], Vector3.up);
                                motion = Vector3.Lerp(motion, planted, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(contactStart, contactSettled, n)));
                            }
                            cx.AddKey(n,motion.x); cz.AddKey(n,motion.z);
                            if(sample==HighCoverCalibrationSamples) report.AppendLine((take?"Take":"Leave side="+side)+" world-frame motion="+motion.ToString("F4"));
                        }
                        if (!take)
                            for (int key = 0; key < cx.length; key++)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(cx,key,AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(cx,key,AnimationUtility.TangentMode.ClampedAuto);
                                AnimationUtility.SetKeyLeftTangentMode(cz,key,AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(cz,key,AnimationUtility.TangentMode.ClampedAuto);
                            }
                        string prefix = take ? "_take" : side == 0 ? "_leave" : "_leaveRight";
                        data.FindProperty(prefix+"X").animationCurveValue=cx;
                        data.FindProperty(prefix+"Z").animationCurveValue=cz;
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                AppendResult("Calibrated immutable High Cover sole/contact/trajectory profile from the existing Adam mesh and clips.");
            }
            finally { File.WriteAllText(EnemyTools.Evidence + "/high-contact-calibration.txt", report.ToString()); foreach(var mesh in meshes.Values) Object.Destroy(mesh); Object.Destroy(enemy); }
        }
    }
}
