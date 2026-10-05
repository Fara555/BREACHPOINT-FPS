using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private static readonly HumanBodyBones[] PoseBones = {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        internal static AnimationClip SourceClip(string name)
        {
            if (name == "RifleRaise") return AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyAnimationAssetPaths.DerivedClip("RifleRaise.anim"));
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { EnemyTools.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => string.Equals(c.name,name,StringComparison.OrdinalIgnoreCase));
                if (clip != null) return clip;
            }
            throw new InvalidOperationException("Missing source clip: " + name);
        }

        [MenuItem("Breachpoint/Enemies/Animation Review/Compare source poses with four rig modes")]
        public static void CompareSourcePoses() => ValidateStage(150);

        internal static void FreezePoseComparison()
        {
            ValidateStage(151);
            var gameView = typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Show();
        }

        [MenuItem("Breachpoint/Enemies/Validation/Validate frozen pose controls")]
        internal static void ValidateFrozenPoseControls() => ValidateStage(152);

        private static IEnumerator FrozenPoseTests(GameLifetimeScope scope, bool validateControls)
        {
            int savedPair = SessionState.GetInt("EnemyTools.PosePair", 0);
            int savedMode = SessionState.GetInt("EnemyTools.PoseRig", 3);
            bool savedRaw = SessionState.GetBool("EnemyTools.PoseRaw", false);
            if (validateControls)
            {
                SessionState.SetInt("EnemyTools.PosePair", 0);
                SessionState.SetInt("EnemyTools.PoseRig", 0);
                SessionState.SetBool("EnemyTools.PoseRaw", false);
            }
            var enemies = new GameObject[2]; var controllers = new AnimatorOverrideController[2];
            var animators = new Animator[2]; var rigs = new RigBuilder[2];
            Camera camera = new GameObject("Frozen pose comparison camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            camera.depth = 100; camera.fieldOfView = 38;
            camera.transform.position = AnimationStart + new Vector3(0, 1.7f, 7);
            camera.transform.LookAt(AnimationStart + Vector3.up);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    using (LifetimeScope.EnqueueParent(scope)) enemies[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart + Vector3.right * (i == 0 ? -1.2f : 1.2f), Quaternion.identity);
                }
                yield return null;
                for (int i = 0; i < 2; i++)
                {
                    enemies[i].GetComponent<EnemyBrain>().enabled = false;
                    enemies[i].GetComponent<EnemyAnimationBridge>().enabled = false;
                    enemies[i].GetComponent<EnemyRigPresenter>().enabled = false;
                    animators[i] = enemies[i].GetComponent<EnemyAnimationBridge>().Animator;
                    rigs[i] = animators[i].GetComponent<RigBuilder>(); rigs[i].Clear();
                    controllers[i] = new AnimatorOverrideController(animators[i].runtimeAnimatorController);

                    animators[i].runtimeAnimatorController = controllers[i]; animators[i].speed = 0;
                }
                int previousPair = -1, previousMode = -1; bool previousRaw = false;
                while (EditorApplication.isPlaying)
                {
                    int pair = SessionState.GetInt("EnemyTools.PosePair", 0);
                    int mode = SessionState.GetInt("EnemyTools.PoseRig", 3);
                    bool raw = SessionState.GetBool("EnemyTools.PoseRaw", false);
                    if (pair != previousPair || mode != previousMode || raw != previousRaw)
                    {
                        string[] left = { raw ? "Rifle Raise" : "RifleRaise", "idle aiming", "idle crouching" };
                        string[] right = { "idle aiming", "Fire", "Fire Crouch" };
                        for (int i = 0; i < 2; i++)
                        {

                            rigs[i].Clear(); animators[i].Rebind(); animators[i].speed = 0;
                            rigs[i].layers[0].active = (mode & 1) != 0; rigs[i].layers[1].active = (mode & 2) != 0;
                            var bridge = new SerializedObject(enemies[i].GetComponent<EnemyAnimationBridge>());
                            var aim = (Transform)bridge.FindProperty("_aimTarget").objectReferenceValue;
                            aim.position = enemies[i].transform.position + Vector3.forward * 8 + Vector3.up * (pair == 2 ? 1.05f : 1.5f);
                            IEnumerator sample = !raw && pair > 0 ? SampleRecoilPose(animators[i], rigs[i], pair == 2, i == 1) : SampleSourcePose(animators[i], rigs[i], controllers[i], SourceClip(i == 0 ? left[pair] : right[pair]), pair == 0 && i == 0 ? .99f : 0);
                            while (sample.MoveNext()) yield return sample.Current;
                        }
                        previousPair = pair; previousMode = mode; previousRaw = raw;
                    }
                    if (validateControls)
                    {
                        var hands = new Transform[2]; var positions = new Vector3[2]; var rotations = new Quaternion[2];
                        for (int i = 0; i < 2; i++)
                        {
                            hands[i] = animators[i].GetBoneTransform(HumanBodyBones.RightHand);
                            positions[i] = hands[i].position; rotations[i] = hands[i].rotation;
                            PlayCheck(animators[i].speed == 0 && rigs[i].layers[0].active == ((mode & 1) != 0) && rigs[i].layers[1].active == ((mode & 2) != 0), "Frozen comparison applies requested Animator/rig mode " + mode);
                        }
                        int frame = Time.frameCount;
                        while (Time.frameCount < frame + 3) yield return null;
                        for (int i = 0; i < 2; i++)
                            PlayCheck(Vector3.Distance(positions[i], hands[i].position) < .001f && Quaternion.Angle(rotations[i], hands[i].rotation) < .1f, "Frozen pose remains still, pair " + pair + ", mode " + mode + ", side " + i);
                        if (pair == 2 && mode == 3) break;
                        SessionState.SetInt("EnemyTools.PosePair", mode == 3 ? pair + 1 : pair);
                        SessionState.SetInt("EnemyTools.PoseRig", (mode + 1) % 4);
                    }
                    yield return null;
                }
            }
            finally
            {
                for (int i = 0; i < 2; i++) { if (rigs[i] != null) rigs[i].Clear(); Object.Destroy(enemies[i]); Object.Destroy(controllers[i]); }
                Object.Destroy(camera.gameObject);
                if (validateControls)
                {
                    SessionState.SetInt("EnemyTools.PosePair", savedPair);
                    SessionState.SetInt("EnemyTools.PoseRig", savedMode);
                    SessionState.SetBool("EnemyTools.PoseRaw", savedRaw);
                }
            }
        }

        private static IEnumerator SampleRecoilPose(Animator animator, RigBuilder rig, bool crouch, bool fire)
        {
            rig.Clear(); animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.Rebind(); animator.speed = 1;
            animator.SetBool("IsCombat", true); animator.SetBool("IsCrouching", crouch); animator.SetBool("IsFiring", fire);
            animator.SetLayerWeight(1, 0); animator.SetLayerWeight(2, fire ? 1 : 0);
            animator.Play(crouch ? "Base Layer.CrouchLocomotion" : "Base Layer.StandingLocomotion", 0, 0);
            if (fire) animator.Play(crouch ? "Recoil.CrouchFire" : "Recoil.Fire", 2, 0);
            animator.Update(0); animator.speed = 0; rig.Build(); rig.SyncLayers();
            rig.graph.GetOutput(1).SetWeight(rig.layers[0].active ? 1 : 0); rig.graph.GetOutput(2).SetWeight(rig.layers[1].active ? 1 : 0);
            int frame = Time.frameCount; while (Time.frameCount < frame + 2) yield return null;
        }

        private static IEnumerator SampleSourcePose(Animator animator, RigBuilder rig, AnimatorOverrideController controller, AnimationClip clip, float normalized)
        {
            rig.Clear();
            controller["Rifle Idle"] = clip;
            animator.runtimeAnimatorController = controller; animator.Rebind(); animator.speed = 1f;
            animator.SetLayerWeight(1, 0f);
            animator.Play("Base Layer.SteadyIdle", 0, normalized);
            animator.Update(.001f); animator.speed = 0f;
            rig.Build();
            rig.SyncLayers();
            rig.graph.GetOutput(1).SetWeight(rig.layers[0].active ? 1f : 0f);
            rig.graph.GetOutput(2).SetWeight(rig.layers[1].active ? 1f : 0f);
            int frame = Time.frameCount;
            while (Time.frameCount < frame + 2) yield return null;
        }
        private static IEnumerator PoseIsolationTests(GameLifetimeScope scope)
        {
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            yield return null;
            enemy.GetComponent<EnemyBrain>().enabled = false;
            enemy.GetComponent<EnemyAnimationBridge>().enabled = false;
            enemy.GetComponent<EnemyRigPresenter>().enabled = false;
            Animator animator = enemy.GetComponent<EnemyAnimationBridge>().Animator;
            RigBuilder rig = animator.GetComponent<RigBuilder>();
            var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
            var report = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                foreach (var pair in new[] { ("Rifle Raise", "idle aiming", .99f), ("idle aiming", "Fire", 0f), ("idle crouching", "Fire Crouch", 0f), ("RifleRaise", "idle aiming", .99f) })
                {
                    for (int mode = 0; mode < 4; mode++)
                    {
                        rig.layers[0].active = (mode & 1) != 0; rig.layers[1].active = (mode & 2) != 0;
                        IEnumerator sample = SampleSourcePose(animator, rig, controller, SourceClip(pair.Item1), pair.Item3); while (sample.MoveNext()) yield return sample.Current;
                        var positions = PoseBones.Select(b => animator.GetBoneTransform(b)?.position ?? Vector3.zero).ToArray();
                        var rotations = PoseBones.Select(b => animator.GetBoneTransform(b)?.rotation ?? Quaternion.identity).ToArray();
                        var muzzle = enemy.GetComponent<EnemyActor>().Muzzle;
                        Vector3 weaponPosition = muzzle.position; Quaternion weaponRotation = muzzle.rotation;
                        sample = SampleSourcePose(animator, rig, controller, SourceClip(pair.Item2), 0f); while (sample.MoveNext()) yield return sample.Current;
                        report.AppendLine(pair.Item1 + " -> " + pair.Item2 + " | mode=" + mode + " (0=None,1=Aim,2=Hand,3=Both) | sampled=" + string.Join(";", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name)) + " time=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                        var ik = enemy.GetComponentInChildren<TwoBoneIKConstraint>();
                        report.AppendLine(FormattableString.Invariant($"  Rig active={rig.layers[0].active}/{rig.layers[1].active}, output={rig.graph.GetOutput(1).GetWeight()}/{rig.graph.GetOutput(2).GetWeight()}, gripError={Vector3.Distance(ik.data.tip.position, ik.data.target.position):F4}m"));
                        for (int i = 0; i < PoseBones.Length; i++)
                        {
                            var bone = animator.GetBoneTransform(PoseBones[i]); if (bone == null) continue;
                            report.AppendLine(FormattableString.Invariant($"  {PoseBones[i]}: distance={Vector3.Distance(positions[i], bone.position):F4}m rotation={Quaternion.Angle(rotations[i], bone.rotation):F2}deg"));
                        }
                        report.AppendLine(FormattableString.Invariant($"  Muzzle: distance={Vector3.Distance(weaponPosition, muzzle.position):F4}m rotation={Quaternion.Angle(weaponRotation, muzzle.rotation):F2}deg"));
                        if (pair.Item1 == "RifleRaise")
                            PlayCheck(Vector3.Distance(weaponPosition, muzzle.position) < .005f && Quaternion.Angle(weaponRotation, muzzle.rotation) < 1f, "Derived Raise ending matches IdleAiming, rig mode " + mode);
                    }
                }
                for (int stance = 0; stance < 2; stance++)
                for (int mode = 0; mode < 4; mode++)
                {
                    rig.Clear(); animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                    animator.Rebind(); animator.speed = 1;
                    animator.SetBool("IsCombat", true); animator.SetBool("IsCrouching", stance == 1);
                    animator.SetLayerWeight(1, 0); animator.SetLayerWeight(2, 0);
                    rig.layers[0].active = (mode & 1) != 0; rig.layers[1].active = (mode & 2) != 0;
                    animator.Play(stance == 0 ? "Base Layer.StandingLocomotion" : "Base Layer.CrouchLocomotion", 0, 0);
                    animator.Update(.001f); animator.speed = 0; rig.Build(); rig.SyncLayers();
                    rig.graph.GetOutput(1).SetWeight(rig.layers[0].active ? 1 : 0); rig.graph.GetOutput(2).SetWeight(rig.layers[1].active ? 1 : 0);
                    int frame = Time.frameCount; while (Time.frameCount < frame + 2) yield return null;
                    var muzzle = enemy.GetComponent<EnemyActor>().Muzzle;
                    Vector3 position = muzzle.position; Quaternion rotation = muzzle.rotation;
                    float height = animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
                    animator.SetBool("IsFiring", true);
                    animator.Play(stance == 0 ? "Recoil.Fire" : "Recoil.CrouchFire", 2, 0); animator.SetLayerWeight(2, 1);
                    animator.Update(0); frame = Time.frameCount; while (Time.frameCount < frame + 2) yield return null;
                    float distance = Vector3.Distance(position, muzzle.position), angle = Quaternion.Angle(rotation, muzzle.rotation);
                    report.AppendLine(FormattableString.Invariant($"CURRENT additive first Fire: crouch={stance == 1}, mode={mode}, muzzle={distance:F4}m/{angle:F2}deg, pelvis={Mathf.Abs(height - animator.GetBoneTransform(HumanBodyBones.Hips).position.y):F4}m"));
                    PlayCheck(distance < .005f && angle < 1f && Mathf.Abs(height - animator.GetBoneTransform(HumanBodyBones.Hips).position.y) < .002f, "First additive Fire preserves base stance and weapon pose, rig mode " + mode + ", crouch=" + (stance == 1));
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { EnemyTools.AnimationFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.IndexOf("turn", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                    report.AppendLine("TURN " + path);
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    report.AppendLine("Baked yaw=" + settings.loopBlendOrientation + ", XZ=" + settings.loopBlendPositionXZ + ", Y=" + settings.loopBlendPositionY);
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("RootQ")))
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        report.AppendLine(binding.propertyName + ": " + string.Join(", ", Enumerable.Range(0, 9).Select(i => curve.Evaluate(clip.length * i / 8f).ToString("F3", System.Globalization.CultureInfo.InvariantCulture))));
                    }
                    foreach (var variant in new[] { clip, AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyAnimationAssetPaths.DerivedClip(Path.GetFileNameWithoutExtension(path).Replace(" ", "") + "InPlace.anim")) })
                    {
                        if (variant == null) continue;
                        rig.Clear(); rig.layers[0].active = rig.layers[1].active = false;

                        Vector3 previousLeft = Vector3.zero, previousRight = Vector3.zero; float feetMotion = 0;
                        for (int frame = 0; frame <= 16; frame++)
                        {
                            IEnumerator sample = SampleSourcePose(animator, rig, controller, variant, frame / 16f); while (sample.MoveNext()) yield return sample.Current;
                            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                            Vector3 left = hips.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                            Vector3 right = hips.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
                            if (frame > 0) feetMotion += Vector3.Distance(previousLeft, left) + Vector3.Distance(previousRight, right);
                            previousLeft = left; previousRight = right;
                        }
                        PlayCheck(feetMotion > .05f, "Pose sampler observes real leg movement: " + variant.name);
                        report.AppendLine(FormattableString.Invariant($"{variant.name}: relative feet travel={feetMotion:F3}m"));
                    }
                }

                PlayCheck(true, "Compared Raise and standing/crouch Fire in all four rig modes; measured poses are diagnostic, not visual acceptance");
            }
            finally { Directory.CreateDirectory("Logs/EnemyValidation"); File.WriteAllText("Logs/EnemyValidation/pose-isolation.txt", report.ToString()); rig.Clear(); Object.Destroy(enemy); Object.Destroy(controller); }
        }
    }

    public sealed class RiflemanPoseReviewWindow : EditorWindow
    {
        [MenuItem("Breachpoint/Enemies/Animation Review/Freeze and compare poses")]
        public static void Open() => GetWindow<RiflemanPoseReviewWindow>("Rifleman pose compare");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Game View: left = reference/source ending, right = next pose. Both remain frozen. Compare all four rig modes. State checks do not certify visual acceptance.", MessageType.Info);
            SessionState.SetInt("EnemyTools.PosePair", EditorGUILayout.Popup("Pair", SessionState.GetInt("EnemyTools.PosePair", 0), new[] { "Raise end / IdleAiming", "IdleAiming / first Fire", "CrouchIdle / first CrouchFire" }));
            SessionState.SetInt("EnemyTools.PoseRig", EditorGUILayout.Popup("Rigs", SessionState.GetInt("EnemyTools.PoseRig", 3), new[] { "Animator only", "Aim only", "Hand only", "Aim + Hand" }));
            SessionState.SetBool("EnemyTools.PoseRaw", EditorGUILayout.Toggle("Compare original source clips", SessionState.GetBool("EnemyTools.PoseRaw", false)));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Run frozen comparison")) EnemyValidationRunner.FreezePoseComparison();
            bool owned = SessionState.GetBool("EnemyTools.Validation", false) && SessionState.GetInt("EnemyTools.Validation.Stage", 0) == 151;
            using (new EditorGUI.DisabledScope(!owned)) if (GUILayout.Button("Stop / restore scene")) EditorApplication.isPlaying = false;
        }
    }
}
