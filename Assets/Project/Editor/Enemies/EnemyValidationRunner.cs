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
    public static partial class EnemyValidationRunner
    {
        private const string RunningKey = "EnemyTools.Validation";
        private const string ScenesKey = RunningKey + ".Scenes";
        private const string StageKey = RunningKey + ".Stage";
        private static readonly HashSet<int> SupportedStages = new HashSet<int> { 50, 60, 70, 80, 90, 100, 110, 120, 130, 131, 150, 151, 152, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 173, 174, 175, 176, 177, 178, 179, 180, 181 };
        internal static bool IsSupportedStage(int stage) => SupportedStages.Contains(stage);
        private static IEnumerator _tests;
        private static double _deadline;
        private static readonly List<string> _runtimeErrors = new List<string>();
        private static bool _priorBackground;
        private static float _priorTimeScale;
        [Serializable] private sealed class SavedScenes { public SceneSetup[] scenes; }

        [InitializeOnLoadMethod]
        private static void SubscribeValidation() => EditorApplication.playModeStateChanged += ValidationModeChanged;

        public static void ValidateStage(int stage)
        {
            if (!IsSupportedStage(stage)) throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy validation stage.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for Unity compilation and import before validation.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; validation preserves it and cannot switch scenes.");
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(EvidencePath + "/validation.txt", "Play Mode stage " + stage + " | " + DateTime.UtcNow.ToString("O") + "\n" + ConsoleCounts() + "\n");
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
                _priorTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                Application.runInBackground = true;
                _tests = PresentationTests(SessionState.GetInt(StageKey, 70));
                _deadline = EditorApplication.timeSinceStartup + (SessionState.GetInt(StageKey, 70) >= 100 ? 600 : SessionState.GetInt(StageKey, 70) == 70 ? 360 : 100);
                EditorApplication.update += ValidationTick;
                if (SessionState.GetBool("EnemyTools.Review.ControlTest", false)) StartReviewControlChecks();
                if (SessionState.GetBool(HumanControlKey, false)) StartHumanControlChecks();
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Dispose while the original domain and live review resources still exist.
                EditorApplication.update -= ValidationTick;
                Application.logMessageReceived -= ValidationLog;
                if (_tests != null) AppendResult("RESULT: STOPPED by user");
                (_tests as IDisposable)?.Dispose(); _tests = null;
                Application.runInBackground = _priorBackground;
                Time.timeScale = _priorTimeScale;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= ValidationTick;
                Application.logMessageReceived -= ValidationLog;
                (_tests as IDisposable)?.Dispose(); _tests = null;
                Application.runInBackground = _priorBackground;
                SessionState.SetBool(RunningKey, false);
                SavedScenes saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(ScenesKey, ""));
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                if (SessionState.GetBool("EnemyTools.Review.ControlStopping", false)) CompleteReviewControlChecks();
                if (SessionState.GetBool(HumanControlKey, false)) CompleteHumanControlChecks();
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
            if (!EditorApplication.isPlaying || _tests == null) return;
            if (SessionState.GetInt(StageKey, 0) == 130 || SessionState.GetInt(StageKey, 0) == 131 || SessionState.GetInt(StageKey, 0) == 169 || SessionState.GetInt(StageKey, 0) == 170 || SessionState.GetInt(StageKey, 0) == 173)
            {
                _deadline = EditorApplication.timeSinceStartup + 600;
                Time.timeScale = SessionState.GetBool("EnemyTools.Review.Paused", false) ? 0f : SessionState.GetFloat("EnemyTools.Review.TimeScale", 1f);
                if (Time.timeScale == 0f) return;
            }
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
            Time.timeScale = _priorTimeScale;
            foreach (string error in _runtimeErrors) AppendResult("FAIL Console: " + error);
            if (exception != null) AppendResult("FAIL " + exception);
            AppendResult(exception == null && _runtimeErrors.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            AppendResult(ConsoleCounts());
            EditorApplication.isPlaying = false;
        }

        private static void AppendResult(string text) => File.AppendAllText(EvidencePath + "/validation.txt", text + "\n");
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
            IEnumerator scenario = CreateStageRoutine(stage, scope, world);
            try { while (scenario.MoveNext()) yield return scenario.Current; }
            finally { (scenario as IDisposable)?.Dispose(); }
        }

        private static IEnumerator CreateStageRoutine(int stage, GameLifetimeScope scope, EnemyWorld world)
        {
            switch (stage)
            {
                case 50: return OriginalGameplayTests();
                case 60: return WeaponEffectsTests(scope, world);
                case 70: return RiflemanAnimationTests(scope, world);
                case 80: return CoverFoundationTests(scope, world);
                case 90: return SquadFoundationTests(scope, world);
                case 100: return RiflemanTacticalTests(scope, world);
                case 110: return RiflemanPerformanceTests(scope, world);
                case 120: return RiflemanEdgeTests(scope, world);
                case 130: return LiveReviewTests(scope, world, false);
                case 131: return LiveReviewTests(scope, world, true);
                case 150: return PoseIsolationTests(scope);
                case 151: return FrozenPoseTests(scope, false);
                case 152: return FrozenPoseTests(scope, true);
                case 160: return MovementPoseAudit(scope);
                case 161: return MovementPolishTests(scope, world);
                case 162: return SteadyMovementProbe(scope);
                case 163: return SynchronizationProbe(scope, world);
                case 164: return SynchronizationSourceProbe(scope);
                case 165: return SynchronizationTests(scope, world);
                case 166: return SourceTurnReview(scope);
                case 167: return FinalSourceAnalysis(scope);
                case 168: return FinalPresentationProbe(scope, world);
                case 169: return FinalReviewSuite(scope, world);
                case 170: return FinalShortReview(scope, world);
                case 171: return CombatStrafeProbe(scope, world);
                case 173: return HumanTacticalReview(scope, world, false);
                case 174: return HumanTacticalReview(scope, world, true);
                case 175: return CrouchEntryProbe(scope, world);
                case 176: return DeathPhysicsProbe(scope, world);
                case 177: return CrouchDeathChecks(scope, world);
                case 178: return DeathClassificationChecks(scope, world);
                case 179: return ActiveDeathChecks(scope, world);
                case 180: return DeathTrackingChecks(scope, world);
                case 181: return MovingImpactChecks(scope, world);
                default: throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy validation stage.");
            }
        }

        private static IEnumerable<object> WaitEnumerable(float seconds)
        {
            IEnumerator wait = WaitFrames(seconds);
            while (wait.MoveNext()) yield return wait.Current;
        }
        private static IEnumerator OriginalGameplayTests()
        {
            // Invoke only existing tests, never the suite's asset-builder/scene-save entry point.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            Type suite = typeof(EnemyValidation);
            Directory.CreateDirectory("Logs");
            const string originalReport = "Logs/EnemyValidation/gameplay-checks.txt";
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
