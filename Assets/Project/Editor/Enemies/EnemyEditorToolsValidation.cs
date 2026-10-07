using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyTools
    {
        [MenuItem("Breachpoint/Enemies/Validation/Validate debug tool contracts")]
        public static void ValidateEditorTools()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editor-only validation.");
            Directory.CreateDirectory(Evidence);
            var report = new StringBuilder();
            int checks = 0;
            void Check(bool passed, string description)
            {
                if (!passed) throw new InvalidOperationException(description);
                checks++; report.AppendLine("PASS: " + description);
            }
            var covered = new HashSet<int>();
            for (int group = 0; group < EnemyTacticalReviewCatalog.Cases.Length; group++)
            {
                int[] cases = EnemyTacticalReviewCatalog.Cases[group];
                Check(cases.Length > 0 && cases.Length == EnemyTacticalReviewCatalog.Names[group].Length, "Review group has matching names and IDs: " + EnemyTacticalReviewCatalog.GroupNames[group]);
                foreach (int index in cases)
                    Check(covered.Add(index) && EnemyTacticalReviewCatalog.GroupFor(index) == group && EnemyTacticalReviewCatalog.Step(EnemyTacticalReviewCatalog.Step(index, 1), -1) == index, "Scenario appears once and Next/Previous stays in its group: " + index);
            }
            Check(covered.Count == EnemyValidationRunner.HumanTacticalNames.Length, "All current human review scenarios are reachable");
            int beforeStage = SessionState.GetInt("EnemyTools.Validation.Stage", 0);
            bool beforeRunning = SessionState.GetBool("EnemyTools.Validation", false);
            bool rejected = false;
            try { EnemyValidationRunner.ValidateStage(999); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && beforeStage == SessionState.GetInt("EnemyTools.Validation.Stage", 0) && beforeRunning == SessionState.GetBool("EnemyTools.Validation", false), "Invalid validation stage is rejected without modifying the editor session");
            Check(EnemyValidationRunner.IsSupportedStage(181) && !EnemyValidationRunner.IsSupportedStage(132), "Only explicit validation stages are supported");
            string fixture = Path.Combine(Evidence, "debug-tool-reader-fixture.tmp");
            try
            {
                var reader = new EnemyValidationReport();
                File.WriteAllText(fixture, "SCENARIO 1: Started\n", Encoding.UTF8);
                Check(reader.Refresh(fixture, 70, true).Contains("RUNNING"), "An active unfinished report shows RUNNING");
                Check(reader.Refresh(fixture, 70, false).Contains("INCOMPLETE"), "An inactive unfinished report is not reported as running or PASS");
                File.WriteAllText(fixture, new string('x', 100000) + "\nSCENARIO 44: Финальная проверка\nRESULT: PASS\n", Encoding.UTF8);
                Check(reader.Refresh(fixture, 70, false).Contains("RESULT: PASS"), "Bounded UTF-8 tail includes the completed verdict after a large report");
                using (var writer = new FileStream(fixture, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    byte[] failure = Encoding.UTF8.GetBytes("FAIL Test diagnostic\nRESULT: FAIL\n");
                    writer.Write(failure, 0, failure.Length); writer.Flush();
                    string summary = reader.Refresh(fixture, 70, false);
                    Check(summary.Contains("RESULT: FAIL") && summary.Contains("FAIL Test diagnostic"), "Status can be read while the validation writer holds the report open");
                }
                File.Delete(fixture);
                Check(reader.Refresh(fixture, 70, false) == "No validation report", "Missing report clears the previous verdict");
            }
            finally { if (File.Exists(fixture)) File.Delete(fixture); }
            report.AppendLine("RESULT: PASS | " + checks + " editor tool checks");
            report.AppendLine(EnemyValidationRunner.ConsoleCounts());
            File.WriteAllText(Path.Combine(Evidence, "editor-tools-checks.txt"), report.ToString());
        }
    }
}
