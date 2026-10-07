using System;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEngine;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private const string HumanControlKey = "EnemyTools.HumanControl";
        private static int _humanControlPhase;
        private static double _humanControlAfter;
        private static double _humanControlDeadline;
        private static string _previousReviewEntity;
        private static float _pausedHumanTime;

        [MenuItem("Breachpoint/Enemies/Validation/Validate Tactical review controls")]
        internal static void ValidateHumanReviewControls()
        {
            SessionState.SetBool(HumanControlKey, true);
            SessionState.SetString(HumanControlKey + ".Error", "");
            try { RunHumanTacticalReview(0, true); }
            catch { SessionState.SetBool(HumanControlKey, false); throw; }
        }
        private static void StartHumanControlChecks()
        {
            _humanControlPhase = 0;
            _humanControlDeadline = EditorApplication.timeSinceStartup + 20;
            EditorApplication.update += CheckHumanControls;
        }
        private static void CheckHumanControls()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > _humanControlDeadline) throw new TimeoutException("Tactical review control validation timed out.");
                if (_humanControlPhase == 0)
                {
                    var brain = TacticalReviewBrain;
                    if (!TacticalReviewRunning || brain == null || brain.Config == null) return;
                    PlayCheck(HumanTacticalIndex == 0 && HumanTacticalRepeating, "Tactical Repeat starts the selected controlled scenario");
                    _previousReviewEntity = brain.GetEntityId().ToString();
                    RunHumanTacticalReview(49, false);
                    _humanControlPhase = 1;
                }
                else if (_humanControlPhase == 1)
                {
                    var brain = TacticalReviewBrain;
                    if (brain == null || brain.GetEntityId().ToString() == _previousReviewEntity) return;
                    PlayCheck(HumanTacticalIndex == 49 && !HumanTacticalRepeating, "Changing review category replaces the live fixture and clears Repeat");
                    SessionState.SetFloat("EnemyTools.Review.TimeScale", .5f);
                    _humanControlAfter = now + .15;
                    _humanControlPhase = 2;
                }
                else if (_humanControlPhase == 2 && now >= _humanControlAfter)
                {
                    PlayCheck(Mathf.Approximately(Time.timeScale, .5f), "Tactical review applies slow playback");
                    SessionState.SetBool("EnemyTools.Review.Paused", true);
                    _humanControlAfter = now + .15;
                    _humanControlPhase = 3;
                }
                else if (_humanControlPhase == 3 && now >= _humanControlAfter)
                {
                    PlayCheck(Time.timeScale == 0f, "Tactical review uses the owned pause clock");
                    _pausedHumanTime = Time.time;
                    _humanControlAfter = now + .15;
                    _humanControlPhase = 4;
                }
                else if (_humanControlPhase == 4 && now >= _humanControlAfter)
                {
                    PlayCheck(Mathf.Approximately(Time.time, _pausedHumanTime), "Paused review does not advance simulation time");
                    SessionState.SetBool("EnemyTools.Review.Paused", false);
                    SessionState.SetFloat("EnemyTools.Review.TimeScale", 1f);
                    EditorApplication.update -= CheckHumanControls;
                    StopHumanTacticalReview();
                }
            }
            catch (Exception error)
            {
                EditorApplication.update -= CheckHumanControls;
                SessionState.SetString(HumanControlKey + ".Error", error.ToString());
                StopHumanTacticalReview();
            }
        }
        private static void CompleteHumanControlChecks()
        {
            EditorApplication.update -= CheckHumanControls;
            SessionState.SetBool(HumanControlKey, false);
            string error = SessionState.GetString(HumanControlKey + ".Error", "");
            if (error.Length == 0 && _runtimeErrors.Count == 0 && Mathf.Approximately(Time.timeScale, _priorTimeScale) && Application.runInBackground == _priorBackground && TacticalReviewBrain == null)
            {
                AppendResult("PASS: Tactical Stop restores time/background and releases the review fixture");
                AppendResult("RESULT: PASS");
            }
            else
            {
                foreach (string runtimeError in _runtimeErrors) AppendResult("FAIL Console: " + runtimeError);
                AppendResult("FAIL: Tactical control restoration | " + error);
                AppendResult("RESULT: FAIL");
            }
            AppendResult(ConsoleCounts());
        }
    }
}
