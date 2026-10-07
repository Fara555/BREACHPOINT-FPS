using System;
using System.IO;
using System.Text;

namespace Breachpoint.Editor.Enemies
{
    // A bounded, cached tail reader. Validation can append while the window reads.
    internal sealed class EnemyValidationReport
    {
        private const int MaximumTailBytes = 32768;
        private DateTime _writtenAt;
        private long _length = -1;
        private int _stage = -1;
        private bool _running;
        private string _summary = "No validation report";

        internal string Refresh(string path, int stage, bool running)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) { _length = -1; return _summary = "No validation report"; }
                if (_writtenAt == file.LastWriteTimeUtc && _length == file.Length && _stage == stage && _running == running) return _summary;
                string result = running ? "RUNNING" : "INCOMPLETE";
                string checkpoint = "";
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long offset = Math.Max(0, stream.Length - MaximumTailBytes);
                    stream.Seek(offset, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        if (offset > 0) reader.ReadLine(); // Discard a partial UTF-8 line.
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("RESULT:", StringComparison.Ordinal)) result = line;
                            if (line.StartsWith("SCENARIO ", StringComparison.Ordinal) || line.StartsWith("TACTICAL SCENARIO ", StringComparison.Ordinal) || line.StartsWith("MEASUREMENT ", StringComparison.Ordinal) || line.StartsWith("FAIL ", StringComparison.Ordinal)) checkpoint = line;
                        }
                    }
                }
                _writtenAt = file.LastWriteTimeUtc; _length = file.Length;
                _stage = stage; _running = running;
                return _summary = "Stage " + stage + " | " + result + (checkpoint.Length > 0 ? "\n" + checkpoint : "");
            }
            catch (IOException) { return _summary; } // Retry after the writer/import finishes.
            catch (UnauthorizedAccessException) { return "Validation report is unavailable"; }
        }
    }
}
