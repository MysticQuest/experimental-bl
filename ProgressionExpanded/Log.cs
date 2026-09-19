using System;
using System.IO;
using System.Text;

namespace ProgressionExpanded
{
    /// <summary>
    /// Writes the mod's own log file, next to the ones every other mod leaves behind.
    /// </summary>
    /// <remarks>
    /// <c>Debug.Print</c> reaches nothing readable from outside a running game -- not ButterLib's
    /// logs, not the trace file -- which turned a one-line bug into several rounds of guessing at
    /// it. Everything worth knowing after a crash goes here instead, in plain text, appended so a
    /// load that dies halfway still leaves its last line behind.
    /// </remarks>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string? _path;
        private static bool _failed;

        internal static void Write(string message)
        {
            if (_failed) return;

            try
            {
                lock (Gate)
                {
                    var path = Path();
                    if (path == null) return;

                    var line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine;
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch
            {
                // One failure is enough; never let logging become the problem being logged.
                _failed = true;
            }
        }

        internal static void Write(string message, Exception exception) =>
            Write(message + " :: " + exception.GetType().Name + ": " + exception.Message);

        private static string? Path()
        {
            if (_path != null) return _path;

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) return null;

            var folder = System.IO.Path.Combine(documents,
                "Mount and Blade II Bannerlord", "Configs", "ModLogs");
            Directory.CreateDirectory(folder);

            _path = System.IO.Path.Combine(folder,
                "ProgressionExpanded" + DateTime.Now.ToString("yyyyMMdd") + ".log");

            File.AppendAllText(_path,
                Environment.NewLine + "=== session started " + DateTime.Now + " ===" + Environment.NewLine,
                Encoding.UTF8);

            return _path;
        }
    }
}
