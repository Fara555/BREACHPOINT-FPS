using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamPresentationIntegration
    {
        private const string RunningKey = "AdamPresentation.Validation";
        private const string ScenesKey = RunningKey + ".Scenes";
        private const string StageKey = RunningKey + ".Stage";
        private static IEnumerator _tests;
        private static double _deadline;
        private static readonly List<string> _runtimeErrors = new List<string>();
        private static bool _priorBackground;
        [Serializable] private sealed class SavedScenes { public SceneSetup[] scenes; }

        [InitializeOnLoadMethod]
        private static void SubscribeValidation() => EditorApplication.playModeStateChanged += ValidationModeChanged;

        [MenuItem("Breachpoint/Enemies/Adam presentation/Validate all in Play Mode")]
        public static void ValidateAll() => ValidateStage(4);

        public static void ValidateStage(int stage)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; validation preserves it and cannot switch scenes.");
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(EvidencePath + "/stage-" + stage + "-play.txt", "Play Mode stage " + stage + " | " + DateTime.UtcNow.ToString("O") + "\n" + ConsoleCounts() + "\n");
            SessionState.SetString(ScenesKey, JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetInt(StageKey, stage);
            SessionState.SetBool(RunningKey, true);
            EditorSceneManager.OpenScene("Assets/Project/Enemies/EnemyArena.unity");
            EditorApplication.isPlaying = true;
        }

        private static void ValidationModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _runtimeErrors.Clear();
                Application.logMessageReceived += ValidationLog;
                _priorBackground = Application.runInBackground;
                Application.runInBackground = true;
                _tests = PresentationTests(SessionState.GetInt(StageKey, 4));
                _deadline = EditorApplication.timeSinceStartup + 100;
                EditorApplication.update += ValidationTick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(RunningKey, false);
                SavedScenes saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(ScenesKey, ""));
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                AppendResult("Editor scene setup restored.");
            }
        }

        private static void ValidationLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
                _runtimeErrors.Add(type + ": " + message + "\n" + stack);
        }

        private static void ValidationTick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Presentation validation timed out.");
                if (!_tests.MoveNext()) FinishValidation(null);
            }
            catch (Exception exception) { FinishValidation(exception); }
        }

        private static void FinishValidation(Exception exception)
        {
            EditorApplication.update -= ValidationTick;
            Application.logMessageReceived -= ValidationLog;
            (_tests as IDisposable)?.Dispose();
            _tests = null;
            Application.runInBackground = _priorBackground;
            foreach (string error in _runtimeErrors) AppendResult("FAIL Console: " + error);
            if (exception != null) AppendResult("FAIL " + exception);
            AppendResult(exception == null && _runtimeErrors.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            AppendResult(ConsoleCounts());
            EditorApplication.isPlaying = false;
        }

        private static void AppendResult(string text) => File.AppendAllText(EvidencePath + "/stage-" + SessionState.GetInt(StageKey, 4) + "-play.txt", text + "\n");
        private static void PlayCheck(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            AppendResult("PASS: " + description);
        }

        private static IEnumerator WaitFrames(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        private static IEnumerator PresentationTests(int stage)
        {
            EnemyGameplaySetup.DisableArenaPlayer();
            foreach (EnemyBrain existing in Object.FindObjectsByType<EnemyBrain>()) existing.gameObject.SetActive(false);
            GameLifetimeScope scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            EnemyWorld world = scope.Container.Resolve<EnemyWorld>();
            if (stage == 50)
            {
                IEnumerator original = OriginalGameplayTests();
                while (original.MoveNext()) yield return original.Current;
                yield break;
            }
            GameObject staging = new GameObject("Inactive presentation test staging");
            staging.SetActive(false);
            GameObject enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), staging.transform);
            enemy.transform.SetPositionAndRotation(new Vector3(0f, 0f, -14f), Quaternion.identity);
            // Before ragdoll integration, freeze only the test instance to isolate animation checks.
            if (stage < 4) foreach (Rigidbody body in enemy.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            using (LifetimeScope.EnqueueParent(scope)) enemy.transform.SetParent(null, true);
            yield return null;
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            EnemyActor actor = enemy.GetComponent<EnemyActor>();
            EnemyAnimationBridge bridge = enemy.GetComponent<EnemyAnimationBridge>();
            Animator animator = enemy.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            RigBuilder rig = animator.GetComponent<RigBuilder>();
            Transform aimTarget = FindUnique(animator.transform, "AimTarget");
            PlayCheck(actor.Navigation.Ready, "Rifleman is on existing arena NavMesh");
            PlayCheck(!animator.applyRootMotion && animator.isHuman && animator.avatar.isValid, "Valid Humanoid; navigation owns movement");
            PlayCheck(rig.layers.Count == 2 && rig.layers[0].rig.name == "AimRig" && rig.layers[1].rig.name == "LeftHandRig", "Authored rig ordering preserved");
            if (stage == 40)
            {
                GameObject inactiveTarget = new GameObject("Inactive death fixture target");
                inactiveTarget.AddComponent<Health>();
                PerceptionTarget deathTarget = inactiveTarget.AddComponent<PerceptionTarget>();
                deathTarget.Initialize(world, Faction.Player);
                inactiveTarget.SetActive(false);
                IEnumerator deathOnly = DeathPresentationTests(enemy, brain, animator, deathTarget);
                while (deathOnly.MoveNext()) yield return deathOnly.Current;
                Object.Destroy(enemy); Object.Destroy(inactiveTarget); Object.Destroy(staging);
                yield break;
            }
            brain.enabled = false;
            foreach (var wait in WaitEnumerable(0.4f)) yield return wait;
            PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle Steady"), "Passive gameplay presents Steady idle");
            actor.Navigation.MoveTo(new Vector3(0f, 0f, 14f), false, Time.time);
            foreach (var wait in WaitEnumerable(0.9f)) yield return wait;
            PlayCheck(animator.GetFloat("MoveSpeed") > 0.1f && animator.GetFloat("MoveSpeed") < 0.4f && animator.GetFloat("MoveY") > 0.5f, "Actual patrol walk drives normalized speed and local forward");
            float walkDeadline = Time.time + 12f;
            bool sawStartWalk = false;
            while (Time.time < walkDeadline && !animator.GetCurrentAnimatorStateInfo(0).IsName("SteadyWalk"))
            {
                sawStartWalk |= animator.GetCurrentAnimatorStateInfo(0).IsName("Start Walk") || animator.GetNextAnimatorStateInfo(0).IsName("Start Walk");
                yield return null;
            }
            PlayCheck(sawStartWalk && animator.GetCurrentAnimatorStateInfo(0).IsName("SteadyWalk"), "Authored Steady start-walk and walk clips play through preserved exit timing");
            actor.Navigation.Stop();
            float stopDeadline = Time.time + 4f;
            bool sawStopWalk = false;
            while (Time.time < stopDeadline && !animator.GetCurrentAnimatorStateInfo(0).IsName("Idle Steady"))
            {
                sawStopWalk |= animator.GetCurrentAnimatorStateInfo(0).IsName("Stop Walk") || animator.GetNextAnimatorStateInfo(0).IsName("Stop Walk");
                yield return null;
            }
            PlayCheck(sawStopWalk && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle Steady"), "Authored Steady stop-walk returns to idle");
            PlayCheck(animator.GetFloat("MoveSpeed") < 0.05f, "Stopping returns speed below authored stop threshold");
            brain.ResetForSpawn();
            actor.Navigation.ResetAt(new Vector3(0f, 0f, -14f));
            brain.States.Change(EnemyStateId.Stunned, "presentation fixture");
            foreach (var wait in WaitEnumerable(0.3f)) yield return wait;
            PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("StandingLocomotion"), "Alert/disabled state uses Combat presentation without AI decisions");
            enemy.transform.rotation = Quaternion.identity;
            actor.Navigation.Face(enemy.transform.position + Vector3.left * 10f, 0f);
            foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
            PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("turn 90 left") || animator.GetNextAnimatorStateInfo(0).IsName("turn 90 left"), "Gameplay heading notification triggers authored left turn");
            foreach (var wait in WaitEnumerable(1.5f)) yield return wait;
            actor.Navigation.MoveTo(enemy.transform.position + Vector3.forward * 5f, true, Time.time);
            foreach (var wait in WaitEnumerable(0.8f)) yield return wait;
            PlayCheck(animator.GetFloat("MoveSpeed") > 0.5f && animator.GetFloat("MoveSpeed") <= 0.7f, "Configured run speed maps to authored run range");
            actor.Navigation.Stop();
            foreach (var wait in WaitEnumerable(2.4f)) yield return wait;
            bridge.SetCrouching(true);
            float stanceDeadline = Time.time + 5f;
            while (Time.time < stanceDeadline && !animator.GetCurrentAnimatorStateInfo(0).IsName("CrouchLocomotion")) yield return null;
            PlayCheck(animator.GetBool("IsCrouching") && animator.GetCurrentAnimatorStateInfo(0).IsName("CrouchLocomotion"), "Explicit stance presentation input reaches authored crouch locomotion (current=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash + ", next=" + animator.GetNextAnimatorStateInfo(0).fullPathHash + ")");
            bridge.SetCrouching(false);
            foreach (var wait in WaitEnumerable(1.4f)) yield return wait;
            PlayCheck(!animator.GetBool("IsCrouching"), "Standing input clears crouch presentation");
            GameObject player = new GameObject("Presentation validation target");
            player.layer = 6;
            player.transform.position = enemy.transform.position + Vector3.forward * 10f;
            CapsuleCollider collider = player.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2f;
            Health playerHealth = player.AddComponent<Health>(); playerHealth.Configure(10000f, false);
            PerceptionTarget target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            brain.Memory.Target = target; brain.Memory.Visible = true;
            foreach (var wait in WaitEnumerable(0.6f)) yield return wait;
            PlayCheck(Vector3.Distance(aimTarget.position, target.AimPosition) < 0.15f, "Existing AimTarget smoothly follows gameplay aim point");
            brain.Memory.Visible = false; brain.Memory.HasContact = false;
            player.SetActive(false);
            foreach (var wait in WaitEnumerable(0.5f)) yield return wait;
            PlayCheck(Vector3.Distance(aimTarget.position, actor.Eyes.position + enemy.transform.forward * 8f) < 0.15f, "No-target aim returns forward rather than world origin");
            foreach (var turn in new[] { (-90f, "turn 90 left"), (90f, "turn 90 right"), (-170f, "Turn 180 left"), (170f, "Turn 180 right") })
            {
                brain.ResetForSpawn(); brain.Stun(30f);
                enemy.transform.rotation = Quaternion.identity;
                foreach (var wait in WaitEnumerable(0.4f)) yield return wait;
                Vector3 heading = Quaternion.Euler(0f, turn.Item1, 0f) * Vector3.forward;
                actor.Navigation.Face(enemy.transform.position + heading * 10f, 0f);
                foreach (var wait in WaitEnumerable(0.35f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName(turn.Item2), "Authored turn selection: " + turn.Item2);
                float progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                float until = Time.time + 0.2f;
                while (Time.time < until) { actor.Navigation.Face(enemy.transform.position + heading * 10f, 0f); yield return null; }
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > progress, "Repeated heading requests do not restart " + turn.Item2);
            }
            brain.ResetForSpawn(); brain.Stun(30f);
            foreach (var wait in WaitEnumerable(0.4f)) yield return wait;
            if (stage >= 3)
            {
                IEnumerator combatTests = CombatPresentationTests(enemy, brain, animator, target);
                while (combatTests.MoveNext()) yield return combatTests.Current;
            }
            if (stage >= 4)
            {
                IEnumerator deathTests = DeathPresentationTests(enemy, brain, animator, target);
                while (deathTests.MoveNext()) yield return deathTests.Current;
            }
            Object.Destroy(enemy); Object.Destroy(player); Object.Destroy(staging);
            yield return null;
        }

        private static IEnumerable<object> WaitEnumerable(float seconds)
        {
            IEnumerator wait = WaitFrames(seconds);
            while (wait.MoveNext()) yield return wait.Current;
        }

        private static IEnumerator CombatPresentationTests(GameObject enemy, EnemyBrain brain, Animator animator, PerceptionTarget target)
        {
            EnemyActor actor = enemy.GetComponent<EnemyActor>();
            actor.Navigation.Stop();
            enemy.transform.rotation = Quaternion.identity;
            target.gameObject.SetActive(true);
            target.transform.position = enemy.transform.position + Vector3.forward * 10f;
            brain.Memory.Target = target; brain.Memory.Visible = true;
            foreach (var wait in WaitEnumerable(0.5f)) yield return wait;
            Physics.SyncTransforms();
            int ammunition = brain.Combat.Ammo;
            brain.Combat.Attack(target, Time.time - 1f);
            brain.Combat.Attack(target, Time.time);
            PlayCheck(brain.Combat.Ammo == ammunition - 1 && animator.GetBool("IsFiring"), "Actual combat shot consumes ammo and starts fire presentation");
            foreach (var wait in WaitEnumerable(0.3f)) yield return wait;
            PlayCheck(!animator.GetBool("IsFiring"), "Fire presentation expires between gameplay shots");
            actor.Health.TakeDamage(new DamageInfo(1f, actor.Eyes.position, Vector3.right, target.gameObject));
            float hitDeadline = Time.time + 1.5f;
            while (Time.time < hitDeadline && !animator.GetCurrentAnimatorStateInfo(0).IsName("Hit Locomotion") && !animator.GetNextAnimatorStateInfo(0).IsName("Hit Locomotion")) yield return null;
            PlayCheck(animator.GetFloat("HitDirection") == 1f && (animator.GetCurrentAnimatorStateInfo(0).IsName("Hit Locomotion") || animator.GetNextAnimatorStateInfo(0).IsName("Hit Locomotion")), "Health event selects nonlethal left hit without another damage system (direction=" + animator.GetFloat("HitDirection") + ", state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash + ")");
            foreach (var wait in WaitEnumerable(1.7f)) yield return wait;
            int reloadEvents = 0;
            Action reloading = () => reloadEvents++;
            brain.Combat.ReloadStarted += reloading;
            try
            {
                // Only the existing combat clock/ammo implementation controls the reload.
                float now = Time.time + 10f;
                for (int i = 0; i < brain.Config.Combat.MagazineSize + 2; i++)
                {
                    brain.Combat.Attack(target, now);
                    now += 2f;
                }
                PlayCheck(brain.Combat.IsReloading && reloadEvents == 1, "Gameplay starts reload exactly once for an empty magazine");
                foreach (var wait in WaitEnumerable(0.25f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("ReloadLocomotion") || animator.GetNextAnimatorStateInfo(0).IsName("ReloadLocomotion"), "ReloadStarted drives the authored Reload state");
                brain.Combat.Tick(now + brain.Config.Combat.ReloadDuration + 1f);
                PlayCheck(!brain.Combat.IsReloading && brain.Combat.Ammo == brain.Config.Combat.MagazineSize && reloadEvents == 1, "Reload gameplay completes without animation events or Animator exit");
            }
            finally { brain.Combat.ReloadStarted -= reloading; }
            var optional = new SerializedObject(enemy.GetComponent<EnemyVfxPresenter>());
            foreach (string field in new[] { "_muzzleFlash", "_hitEffect", "_tracer", "_audioSource", "_shotClip", "_deathClip" }) optional.FindProperty(field).objectReferenceValue = null;
            optional.ApplyModifiedPropertiesWithoutUndo();
            brain.Combat.Reset();
            brain.Combat.Attack(target, Time.time - 1f); brain.Combat.Attack(target, Time.time);
            PlayCheck(brain.Combat.Ammo == brain.Config.Combat.MagazineSize - 1, "Missing optional VFX/audio references do not interrupt gameplay shots");
        }

        private static IEnumerator DeathPresentationTests(GameObject enemy, EnemyBrain brain, Animator animator, PerceptionTarget target)
        {
            target.gameObject.SetActive(false);
            EnemyActor actor = enemy.GetComponent<EnemyActor>();
            EnemyAnimationBridge bridge = enemy.GetComponent<EnemyAnimationBridge>();
            EnemyRagdollPresenter ragdoll = enemy.GetComponent<EnemyRagdollPresenter>();
            RigBuilder rig = animator.GetComponent<RigBuilder>();
            Rigidbody[] bodies = enemy.GetComponentsInChildren<Rigidbody>();
            Transform skeleton = FindUnique(animator.transform, "Adam_Reference");
            Transform[] bones = skeleton.GetComponentsInChildren<Transform>();
            Transform weapon = FindUnique(animator.transform, "SCIFIAR");
            Transform weaponParent = weapon.parent;
            PlayCheck(ragdoll != null && bodies.Length == 12, "Focused presenter uses all 12 authored ragdoll bodies");
            brain.enabled = true;
            brain.ResetForSpawn();
            foreach (Rigidbody body in bodies) PlayCheck(body.isKinematic, "Alive authored body is kinematic: " + body.name);
            // Direction, crouching, momentum and reuse are tested through the shared Health pipeline.
            for (int test = 0; test < 5; test++)
            {
                brain.ResetForSpawn();
                enemy.transform.rotation = Quaternion.identity;
                bridge.SetCrouching(test == 4);
                brain.Stun(30f);
                foreach (var wait in WaitEnumerable(test == 4 ? 1.6f : 0.3f)) yield return wait;
                actor.Navigation.MoveTo(enemy.transform.position + Vector3.forward * 3f, true, Time.time);
                foreach (var wait in WaitEnumerable(0.3f)) yield return wait;
                Vector3 motion = actor.Navigation.Velocity;
                Vector3 travel = test == 0 ? Vector3.back : test == 1 ? Vector3.right : test == 2 ? Vector3.left : Vector3.forward;
                actor.Health.TakeDamage(new DamageInfo(10000f, actor.Eyes.position, travel, null));
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                PlayCheck(actor.Health.IsDead && brain.States.Current == EnemyStateId.Dead && !brain.Combat.IsAiming && !brain.Combat.IsReloading, "Lethal hit " + test + " stops gameplay immediately");
                Vector3 deadPosition = enemy.transform.position;
                int deadAmmo = brain.Combat.Ammo;
                PlayCheck(animator.GetFloat("HitDirection") == (test < 4 ? test : 3f) && !animator.GetBool("IsHeadshot"), "Death direction " + test + " comes from final hit; unsupported headshot stays false");
                string expected = test == 4 ? "DeathCrouching" : "DeathTree";
                float deathStarted = Time.time;
                while (Time.time < deathStarted + 0.85f && !animator.GetCurrentAnimatorStateInfo(0).IsName(expected)) yield return null;
                PlayCheck(Vector3.Distance(deadPosition, enemy.transform.position) < 0.001f && agent.isStopped && !agent.hasPath && actor.Navigation.Velocity.sqrMagnitude < 0.001f && brain.Combat.Ammo == deadAmmo, "Dead gameplay remains motionless and cannot fire on subsequent AI frames (ready=" + actor.Navigation.Ready + ", stopped=" + agent.isStopped + ", path=" + agent.hasPath + ", velocity=" + actor.Navigation.Velocity + ", displacement=" + Vector3.Distance(deadPosition, enemy.transform.position) + ")");
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName(expected) && !ragdoll.IsRagdoll, "Authored " + expected + " plays before physical takeover");
                foreach (var wait in WaitEnumerable(0.25f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.04f, "Consumed death request lets authored death clip advance");
                if (test == 0) CapturePresentation(enemy, "death-animation");
                Pose[] preceding = new Pose[bones.Length];
                while (!ragdoll.IsRagdoll && Time.time < deathStarted + 2f)
                {
                    for (int i = 0; i < bones.Length; i++) preceding[i] = new Pose(bones[i].localPosition, bones[i].localRotation);
                    yield return null;
                }
                PlayCheck(ragdoll.IsRagdoll && !animator.enabled && !rig.enabled, "Timer disables Animator and RigBuilder then activates physics");
                float largestJump = 0f;
                for (int i = 0; i < bones.Length; i++) largestJump = Mathf.Max(largestJump, Quaternion.Angle(preceding[i].rotation, bones[i].localRotation));
                AppendResult("Observed maximum bone rotation between final animated frame and first ragdoll frame: " + largestJump + " degrees.");
                PlayCheck(largestJump < 55f, "Takeover preserves the death pose without a bind-pose reset");
                foreach (Rigidbody body in bodies) PlayCheck(!body.isKinematic, "Death activates authored body: " + body.name);
                PlayCheck(weapon.parent == weaponParent && weaponParent.name == "WeaponSocket", "Weapon remains attached to authored right-hand socket");
                if (motion.sqrMagnitude > 0.1f) PlayCheck(Vector3.Dot(bodies[0].linearVelocity, motion) > 0f, "Ragdoll inherits captured navigation momentum");
                foreach (var wait in WaitEnumerable(0.4f)) yield return wait;
                if (test == 0) CapturePresentation(enemy, "ragdoll");
                enemy.SetActive(false);
                enemy.transform.SetPositionAndRotation(new Vector3(0f, 0f, -14f), Quaternion.identity);
                enemy.SetActive(true);
                foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
                PlayCheck(!actor.Health.IsDead && !ragdoll.IsRagdoll && animator.enabled && rig.enabled && !animator.GetBool("IsDead") && !animator.GetBool("IsHeadshot") && !animator.GetBool("IsFiring") && !animator.GetBool("IsCrouching"), "Existing inactive reuse clears death, ragdoll, stance and fire state");
                foreach (Rigidbody body in bodies) PlayCheck(body.isKinematic, "Reuse freezes authored body: " + body.name);
                if (test == 0) CapturePresentation(enemy, "reset");
            }
            animator.enabled = false;
            actor.Health.TakeDamage(new DamageInfo(10000f, actor.Eyes.position, Vector3.back, null));
            yield return null;
            PlayCheck(ragdoll.IsRagdoll, "Disabled Animator uses immediate physical fallback without animation events");
            brain.ResetForSpawn();
            yield return null;
            PlayCheck(!ragdoll.IsRagdoll && animator.enabled && rig.enabled, "Explicit reset restores presentation after the fallback path");
        }

        private static void CapturePresentation(GameObject enemy, string name)
        {
            Camera camera = new GameObject("Temporary Adam evidence camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            var volume = new GameObject("Temporary still capture settings").AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true; volume.priority = 100000;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            profile.Add<UnityEngine.Rendering.HighDefinition.MotionBlur>(true).intensity.Override(0f);
            volume.sharedProfile = profile;
            camera.nearClipPlane = 0.03f;
            camera.fieldOfView = 42f;
            camera.transform.position = enemy.transform.position + new Vector3(3f, 2.2f, 4.5f);
            camera.transform.LookAt(enemy.transform.position + new Vector3(0f, 0.7f, 0.8f));
            RenderTexture texture = new RenderTexture(1000, 1100, 24, RenderTextureFormat.ARGB32);
            texture.Create();
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = texture };
                for (int i = 0; i < 3; i++) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = texture;
                image = new Texture2D(1000, 1100, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1000, 1100), 0, 0); image.Apply();
                File.WriteAllBytes(EvidencePath + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Object.DestroyImmediate(image);
                texture.Release(); Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(volume.gameObject); Object.DestroyImmediate(profile);
            }
        }

        private static IEnumerator OriginalGameplayTests()
        {
            // Invoke only existing tests, never the suite's asset-builder/scene-save entry point.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            Type suite = typeof(EnemyValidation);
            if (suite.GetField("_routine", flags).GetValue(null) != null)
                throw new InvalidOperationException("The original enemy suite is already running.");
            Directory.CreateDirectory("Logs");
            const string originalReport = "Logs/EnemyValidation.txt";
            int offset = File.Exists(originalReport) ? File.ReadAllText(originalReport).Length : 0;
            try
            {
                suite.GetField("_failed", flags).SetValue(null, false);
                suite.GetMethod("StateMachineTests", flags).Invoke(null, null);
                suite.GetMethod("ValidatePrefabs", flags).Invoke(null, null);
                var runtime = (IEnumerator)suite.GetMethod("RuntimeTests", flags).Invoke(null, null);
                while (runtime.MoveNext()) yield return runtime.Current;
            }
            finally
            {
                if (File.Exists(originalReport)) AppendResult(File.ReadAllText(originalReport).Substring(offset));
            }
        }
    }
}
