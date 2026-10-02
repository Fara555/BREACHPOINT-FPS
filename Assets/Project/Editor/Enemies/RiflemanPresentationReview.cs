using System;
using System.Collections;
using System.IO;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamPresentationIntegration
    {
        internal static readonly string[] ReviewNames = {
            "Steady start / walk / stop", "Combat walk / run / sprint", "Combat eight directions", "Steady / Combat readiness",
            "Steady 90L", "Steady 90R", "Steady 180L", "Steady 180R", "Combat 90L", "Combat 90R", "Combat 180L", "Combat 180R",
            "Crouch 90L", "Crouch 90R", "Crouch 180L", "Crouch 180R", "Crouch enter / move / exit", "Fire idle / moving / crouch",
            "Reload walking / crouch", "Hit moving / crouch", "Death / ragdoll", "Crouch IK eight directions" };
        private static Camera _reviewCamera;
        private static RenderTexture _reviewTexture;
        private static Texture2D _reviewImage;
        private static StreamWriter _reviewMetrics;
        private static string _reviewFolder;
        private static int _reviewFrame;
        private static float _nextReviewFrame;

        [MenuItem("Breachpoint/Enemies/AI Test / Tactical Debug/Validate review controls and restoration")]
        internal static void ValidateReviewControls()
        {
            SessionState.SetFloat("RiflemanPolish.ControlScale", Time.timeScale);
            SessionState.SetBool("RiflemanPolish.ControlRepeat", SessionState.GetBool("RiflemanPolish.Repeat", true));
            RunReview(3, true);
            SessionState.SetBool("RiflemanPolish.ControlTest", true);
        }

        private static void StartReviewControlChecks()
        {
            SessionState.SetBool("RiflemanPolish.ControlTest", false);
            bool originalRepeat = SessionState.GetBool("RiflemanPolish.ControlRepeat", true);
            SessionState.SetBool("RiflemanPolish.Repeat", true);
            int phase = 0;
            double next = EditorApplication.timeSinceStartup;
            float frozen = 0f;
            EditorApplication.update += CheckControls;
            void CheckControls()
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .4;
                try
                {
                    if (phase == 0)
                    {
                        if (!EditorApplication.isPlaying || _reviewMetrics == null) return;
                        SessionState.SetBool("RiflemanPolish.Paused", true);
                    }
                    else if (phase == 1) { Require(Time.timeScale == 0f, "Pause freezes owned review"); frozen = Time.time; }
                    else if (phase == 2)
                    {
                        Require(Mathf.Abs(Time.time - frozen) < .001f, "Paused simulation does not advance");
                        SessionState.SetBool("RiflemanPolish.Paused", false); SessionState.SetFloat("RiflemanPolish.TimeScale", .5f);
                    }
                    else if (phase == 3) { Require(Time.timeScale == .5f, "Half speed applied"); SessionState.SetInt("RiflemanPolish.Scenario", 21); }
                    else if (phase == 4) { Require(_reviewFolder.Contains("21-0"), "Next changes live scenario"); SessionState.SetFloat("RiflemanPolish.TimeScale", 1f); }
                    else if (phase == 5) { Require(Time.timeScale == 1f, "Normal speed restored"); SessionState.SetBool("RiflemanPolish.Paused", true); }
                    else if (phase == 6)
                    {
                        Require(Time.timeScale == 0f, "Stop fixture is paused");
                        EditorApplication.update -= CheckControls;
                        SessionState.SetBool("RiflemanPolish.ControlStopping", true);
                        EditorApplication.isPlaying = false;
                    }
                    phase++;
                }
                catch (Exception error)
                {
                    EditorApplication.update -= CheckControls;
                    SessionState.SetBool("RiflemanPolish.Repeat", originalRepeat);
                    EditorApplication.isPlaying = false;
                    File.WriteAllText(RiflemanPolish.Evidence + "/review-controls.txt", "RESULT: FAIL\n" + error);
                }
            }
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        }

        private static void CompleteReviewControlChecks()
        {
            SessionState.SetBool("RiflemanPolish.ControlStopping", false);
            SessionState.SetBool("RiflemanPolish.Repeat", SessionState.GetBool("RiflemanPolish.ControlRepeat", true));
            bool restored = Time.timeScale == SessionState.GetFloat("RiflemanPolish.ControlScale", 1f) && _reviewMetrics == null && _reviewCamera == null && SessionState.GetBool("RiflemanPolish.Disposed", false);
            File.WriteAllText(RiflemanPolish.Evidence + "/review-controls.txt", (restored ? "RESULT: PASS" : "RESULT: FAIL") + "\nPause, frozen simulation, 0.5x, next, 1x, stop while paused, time/resource restoration\n" + ConsoleCounts());
        }

        internal static void RunReview(int index, bool interactive)
        {
            SessionState.SetFloat("RiflemanPolish.PriorScale", Time.timeScale);
            SessionState.SetBool("RiflemanPolish.Disposed", false);
            SessionState.SetInt("RiflemanPolish.Scenario", index);
            SessionState.SetBool("RiflemanPolish.Paused", false);
            SessionState.SetFloat("RiflemanPolish.TimeScale", 1f);
            ValidateStage(interactive ? 131 : 130);
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Show();
        }

        private static IEnumerator PolishReviewTests(GameLifetimeScope scope, EnemyWorld world, bool interactive)
        {
            float priorScale = SessionState.GetFloat("RiflemanPolish.PriorScale", 1f);
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            var player = new GameObject("Presentation review target"); player.layer = 6;
            var collider = player.AddComponent<CapsuleCollider>(); collider.height = 2f; collider.center = Vector3.up; collider.radius = .4f;
            player.AddComponent<Health>().Configure(1000000f, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            _reviewCamera = new GameObject("Presentation review Game View camera").AddComponent<Camera>();
            _reviewCamera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            _reviewCamera.depth = 100; _reviewCamera.fieldOfView = 35f; _reviewCamera.nearClipPlane = .03f;
            _reviewTexture = new RenderTexture(560, 640, 24); _reviewTexture.Create();
            _reviewImage = new Texture2D(560, 640, TextureFormat.RGB24, false);
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); var actor = enemy.GetComponent<EnemyActor>();
            var bridge = enemy.GetComponent<EnemyAnimationBridge>(); brain.enabled = false;
            int selected = SessionState.GetInt("RiflemanPolish.Scenario", -1);
            try
            {
                do
                {
                    for (int index = 0; index < ReviewNames.Length; index++)
                    {
                        selected = interactive ? SessionState.GetInt("RiflemanPolish.Scenario", 0) : selected;
                        if (selected >= 0 && index != selected) continue;
                        int passes = interactive ? 1 : 2;
                        for (int pass = 0; pass < passes; pass++)
                        {
                            if (!interactive) SessionState.SetFloat("RiflemanPolish.TimeScale", pass == 0 ? 1f : .5f);
                            enemy.SetActive(false); enemy.transform.SetPositionAndRotation(AnimationStart, Quaternion.identity); enemy.SetActive(true);
                            yield return null; brain.ResetForSpawn(); brain.enabled = false;
                            player.transform.position = AnimationStart + Vector3.forward * 10;
                            brain.Memory.Target = target; brain.Memory.Visible = true; brain.Memory.HasContact = true;
                            _reviewFolder = RiflemanPolish.Evidence + (interactive ? "/LiveReview/" : "/Review/") + index.ToString("D2") + "-" + pass;
                            Directory.CreateDirectory(_reviewFolder); _reviewFrame = 0; _nextReviewFrame = 0;
                            _reviewMetrics = new StreamWriter(_reviewFolder + "/motion.csv");
                            _reviewMetrics.WriteLine("Frame,Time,Speed,RootYaw,State,LayerWeight,IKWeight,GripError,ElbowAboveShoulder,LeftFootX,LeftFootY,LeftFootZ,RightFootX,RightFootY,RightFootZ,BodyYaw");
                            AppendResult("REVIEW " + index + " | " + ReviewNames[index] + " | " + SessionState.GetFloat("RiflemanPolish.TimeScale", 1f) + "x");
                            IEnumerator run = ReviewCase(index, enemy, brain, actor, bridge, target);
                            try
                            {
                                while (run.MoveNext())
                                {
                                    FollowReviewCamera(enemy); RecordReviewFrame(enemy, bridge);
                                    if (interactive && SessionState.GetInt("RiflemanPolish.Scenario", index) != index) break;
                                    yield return run.Current;
                                }
                            }
                            finally { (run as IDisposable)?.Dispose(); _reviewMetrics.Dispose(); _reviewMetrics = null; }
                            AppendResult("Recorded continuous review frames: " + _reviewFrame + " | visual acceptance requires inspection");
                        }
                    }
                } while (interactive && SessionState.GetBool("RiflemanPolish.Repeat", true));
            }
            finally
            {
                Time.timeScale = priorScale;
                _reviewMetrics?.Dispose(); _reviewMetrics = null;
                DestroyReviewObject(enemy); DestroyReviewObject(player);
                if (_reviewCamera != null) DestroyReviewObject(_reviewCamera.gameObject);
                if (_reviewTexture != null) { _reviewTexture.Release(); DestroyReviewObject(_reviewTexture); }
                DestroyReviewObject(_reviewImage);
                _reviewCamera = null; _reviewTexture = null; _reviewImage = null;
                SessionState.SetBool("RiflemanPolish.Disposed", true);
            }
        }

        private static void DestroyReviewObject(Object value)
        {
            if (value == null) return;
            if (EditorApplication.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private static void FollowReviewCamera(GameObject enemy)
        {
            _reviewCamera.transform.position = enemy.transform.position + new Vector3(2.6f, 1.65f, 3.5f);
            _reviewCamera.transform.LookAt(enemy.transform.position + Vector3.up * .95f);
        }
        private static void RecordReviewFrame(GameObject enemy, EnemyAnimationBridge bridge)
        {
            if (Time.unscaledTime < _nextReviewFrame) return;
            _nextReviewFrame = Time.unscaledTime + .05f;
            var animator = bridge.Animator;
            var ik = enemy.GetComponentInChildren<TwoBoneIKConstraint>();
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
            float error = Vector3.Distance(ik.data.tip.position, ik.data.target.position);
            float elbow = ik.data.mid.position.y - ik.data.root.position.y;
            int state = animator.enabled && !enemy.GetComponent<EnemyRagdollPresenter>().IsRagdoll ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
            _reviewMetrics.WriteLine(FormattableString.Invariant($"{_reviewFrame},{Time.time},{enemy.GetComponent<EnemyActor>().Navigation.Velocity.magnitude},{enemy.transform.eulerAngles.y},{state},{animator.GetLayerWeight(1)},{enemy.GetComponent<EnemyRigPresenter>().HandWeight},{error},{elbow},{left.x},{left.y},{left.z},{right.x},{right.y},{right.z},{animator.GetBoneTransform(HumanBodyBones.Hips).eulerAngles.y}"));
            var previous = RenderTexture.active;
            try
            {
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = _reviewTexture };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(_reviewCamera, request);
                RenderTexture.active = _reviewTexture; _reviewImage.ReadPixels(new Rect(0, 0, 560, 640), 0, 0); _reviewImage.Apply();
                File.WriteAllBytes(_reviewFolder + "/" + (_reviewFrame++).ToString("D4") + ".png", _reviewImage.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }
        private static IEnumerable WaitReview(float seconds)
        { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private static IEnumerator ReviewCase(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            if (index >= 1 && index != 3 && index < 4 || index >= 8)
                brain.States.Change(EnemyStateId.Combat, "continuous presentation review");
            foreach (var wait in WaitReview(1.4f)) yield return wait;
            if (index >= 4 && index <= 15)
            {
                if (index >= 12) { bridge.SetCrouching(true); foreach (var wait in WaitReview(1.4f)) yield return wait; }
                int turn = (index - 4) % 4; float angle = turn == 0 ? -90 : turn == 1 ? 90 : turn == 2 ? -175 : 175;
                actor.Navigation.Face(enemy.transform.position + Quaternion.Euler(0, angle, 0) * enemy.transform.forward * 10, Time.deltaTime);
                foreach (var wait in WaitReview(3f)) yield return wait;
                yield break;
            }
            if (index == 3)
            {
                brain.States.Change(EnemyStateId.Combat, "readiness"); foreach (var wait in WaitReview(2.5f)) yield return wait;
                brain.Memory.Visible = false; brain.Memory.HasContact = false; brain.States.Change(EnemyStateId.Idle, "relax");
                foreach (var wait in WaitReview(2.5f)) yield return wait; yield break;
            }
            if (index == 20)
            {
                actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, Vector3.back, target.gameObject));
                foreach (var wait in WaitReview(3f)) yield return wait; yield break;
            }
            if (index == 16 || index == 21 || index == 18)
            { bridge.SetCrouching(index != 18); foreach (var wait in WaitReview(1.3f)) yield return wait; }
            if (index == 18)
            {
                brain.Combat.Attack(target, Time.time - 1); brain.Combat.Attack(target, Time.time); brain.Combat.RequestReload(Time.time);
                float until = Time.time + brain.Config.Combat.ReloadDuration + .5f;
                while (Time.time < until)
                { brain.Combat.Tick(Time.time); actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 10, bridge.IsCrouching ? EnemyMovePace.Crouch : EnemyMovePace.Walk, Time.time); if (Time.time > until - 2f) bridge.SetCrouching(true); yield return null; }
            }
            else if (index == 19)
            {
                for (int phase = 0; phase < 2; phase++)
                {
                    bridge.SetCrouching(phase == 1);
                    foreach (var wait in WaitReview(1.3f)) yield return wait;
                    actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 15, phase == 0 ? EnemyMovePace.Run : EnemyMovePace.Crouch, Time.time);
                    foreach (var wait in WaitReview(.5f)) yield return wait;
                    actor.Health.TakeDamage(new DamageInfo(1, actor.Eyes.position, Vector3.back, target.gameObject));
                    foreach (var wait in WaitReview(1.5f)) yield return wait;
                    actor.Navigation.Stop();
                }
            }
            else if (index == 17)
            {
                for (int phase = 0; phase < 3; phase++)
                {
                    bridge.SetCrouching(phase == 2); foreach (var wait in WaitReview(1.3f)) yield return wait;
                    float until = Time.time + 2f;
                    while (Time.time < until)
                    {
                        if (phase == 1) actor.Navigation.MoveTo(AnimationStart + Vector3.forward * 10, EnemyMovePace.Run, Time.time);
                        brain.Combat.Attack(target, Time.time); brain.Combat.Tick(Time.time); yield return null;
                    }
                    actor.Navigation.Stop();
                }
            }
            else
            {
                int directions = index == 2 || index == 21 ? 8 : index == 1 ? 5 : 1;
                for (int direction = 0; direction < directions; direction++)
                {
                    var pace = index == 1 ? (EnemyMovePace)(direction <= 2 ? direction : 4 - direction) : index == 16 || index == 21 ? EnemyMovePace.Crouch : EnemyMovePace.Walk;
                    Vector3 heading = Quaternion.Euler(0, directions == 8 ? direction * 45 : 0, 0) * Vector3.forward;
                    Vector3 goal = index == 1 ? AnimationStart + Vector3.forward * 24 : enemy.transform.position + heading * 5;
                    float until = Time.time + (directions == 8 ? .9f : index == 1 ? 1.2f : 2.5f);
                    while (Time.time < until) { actor.Navigation.MoveTo(goal, pace, Time.time); if (index != 0) actor.Navigation.Face(enemy.transform.position + Vector3.forward * 10, Time.deltaTime); yield return null; }
                }
            }
            actor.Navigation.Stop();
            if (index == 16) bridge.SetCrouching(false);
            foreach (var wait in WaitReview(2f)) yield return wait;
        }
    }
}
