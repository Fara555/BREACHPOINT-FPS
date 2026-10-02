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
        public static void ValidateAll() => ValidateStage(70);

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
                _deadline = EditorApplication.timeSinceStartup + (SessionState.GetInt(StageKey, 70) >= 100 ? 600 : SessionState.GetInt(StageKey, 70) == 70 ? 360 : 100);
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
            IEnumerator scenario = stage == 50 ? OriginalGameplayTests() : stage == 60 ? WeaponEffectsTests(scope, world) : stage == 80 ? CoverFoundationTests(scope, world) : stage == 90 ? SquadFoundationTests(scope, world) : stage == 100 ? RiflemanTacticalTests(scope, world) : stage == 110 ? RiflemanPerformanceTests(scope, world) : stage == 120 ? RiflemanEdgeTests(scope, world) : RiflemanAnimationTests(scope, world);
            while (scenario.MoveNext()) yield return scenario.Current;
        }

        private static IEnumerable<object> WaitEnumerable(float seconds)
        {
            IEnumerator wait = WaitFrames(seconds);
            while (wait.MoveNext()) yield return wait.Current;
        }
        private static void CapturePresentation(GameObject enemy, string name)
        {
            var handIk = enemy.GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (name.StartsWith("rework", StringComparison.Ordinal) && handIk != null)
            {
                var builder = enemy.GetComponentInChildren<RigBuilder>(true);
                File.AppendAllText(RiflemanRework.Evidence + "/rig-evidence.txt", name + " | enabled=" + builder.enabled + " | graph=" + builder.graph.IsValid() + " | rigActive=" + builder.layers[1].active + " | rigWeight=" + builder.layers[1].rig.weight + " | IK weight=" + handIk.weight + " | rootBone=" + handIk.data.root.name + " | chainLength=" + (Vector3.Distance(handIk.data.root.position, handIk.data.mid.position) + Vector3.Distance(handIk.data.mid.position, handIk.data.tip.position)) + " | root-target=" + Vector3.Distance(handIk.data.root.position, handIk.data.target.position) + " | tip-target=" + Vector3.Distance(handIk.data.tip.position, handIk.data.target.position) + " | tip=" + handIk.data.tip.position + " | target=" + handIk.data.target.position + "\n");
            }
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
