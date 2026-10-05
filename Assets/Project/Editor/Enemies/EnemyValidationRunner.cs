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
                _tests = PresentationTests(SessionState.GetInt(StageKey, 4));
                _deadline = EditorApplication.timeSinceStartup + (SessionState.GetInt(StageKey, 70) >= 100 ? 600 : SessionState.GetInt(StageKey, 70) == 70 ? 360 : 100);
                EditorApplication.update += ValidationTick;
                if (SessionState.GetBool("EnemyTools.Review.ControlTest", false)) StartReviewControlChecks();
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
                AppendResult("Editor scene setup restored.");
                if (SessionState.GetBool("EnemyTools.Review.ControlStopping", false)) CompleteReviewControlChecks();
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
            IEnumerator scenario = stage == 178 ? DeathClassificationChecks(scope, world) : stage == 177 ? CrouchDeathChecks(scope, world) : stage == 176 ? DeathPhysicsProbe(scope, world) : stage == 175 ? CrouchEntryProbe(scope, world) : stage == 174 ? HumanTacticalReview(scope, world, true) : stage == 173 ? HumanTacticalReview(scope, world, false) : stage == 171 ? CombatStrafeProbe(scope, world) : stage == 170 ? FinalShortReview(scope, world) : stage == 169 ? FinalReviewSuite(scope, world) : stage == 168 ? FinalPresentationProbe(scope, world) : stage == 167 ? FinalSourceAnalysis(scope) : stage == 166 ? SourceTurnReview(scope) : stage == 165 ? SynchronizationTests(scope, world) : stage == 164 ? SynchronizationSourceProbe(scope) : stage == 163 ? SynchronizationProbe(scope, world) : stage == 162 ? SteadyMovementProbe(scope) : stage == 161 ? MovementPolishTests(scope, world) : stage == 160 ? MovementPoseAudit(scope) : stage == 152 ? FrozenPoseTests(scope, true) : stage == 151 ? FrozenPoseTests(scope, false) : stage == 150 ? PoseIsolationTests(scope) : stage >= 130 ? LiveReviewTests(scope, world, stage == 131) : stage == 50 ? OriginalGameplayTests() : stage == 60 ? WeaponEffectsTests(scope, world) : stage == 80 ? CoverFoundationTests(scope, world) : stage == 90 ? SquadFoundationTests(scope, world) : stage == 100 ? RiflemanTacticalTests(scope, world) : stage == 110 ? RiflemanPerformanceTests(scope, world) : stage == 120 ? RiflemanEdgeTests(scope, world) : RiflemanAnimationTests(scope, world);
            try { while (scenario.MoveNext()) yield return scenario.Current; }
            finally { (scenario as IDisposable)?.Dispose(); }
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
