using System;
using System.IO;

namespace ProgressionExpanded
{
    /// <summary>
    /// Whether the module should be doing anything at all.
    /// </summary>
    /// <remarks>
    /// <c>Settings.Instance</c> is null during <c>OnSubModuleLoad</c> - MCM has not built it yet
    /// - so a guard written against it silently passes and every patch is applied anyway. That
    /// made the Enabled switch a lie: turning the mod off still left twenty-odd patches running,
    /// and every attempt to narrow a fault by switching things off was measuring the wrong thing.
    ///
    /// So the setting is read straight out of MCM's own json when the object is not available yet,
    /// and once it is, the object wins.
    /// </remarks>
    internal static class Mod
    {
        private static string? _json;
        private static bool _read;

        internal static bool On => Flag("Enabled", Settings.Instance?.Enabled);

        /// <summary>
        /// A setting read before MCM exists, falling back to its own saved json.
        /// </summary>
        /// <remarks>
        /// Written generically because getting this wrong once cost an evening: a load-time guard
        /// that consults <c>Settings.Instance</c> silently passes, every switch reads as "on", and
        /// the person turning things off to find a fault is testing the same build every time.
        /// </remarks>
        internal static bool Flag(string name, bool? live, bool fallback = true)
        {
            if (live.HasValue) return live.Value;

            var json = Json();
            if (json == null) return fallback;

            var at = json.IndexOf("\"" + name + "\"", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return fallback;

            var colon = json.IndexOf(':', at);
            if (colon < 0) return fallback;

            var tail = json.Substring(colon, Math.Min(24, json.Length - colon));
            if (tail.IndexOf("false", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (tail.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return fallback;
        }

        /// <summary>A numeric setting, read the same way as the flags.</summary>
        internal static int Number(string name, int? live, int fallback)
        {
            if (live.HasValue) return live.Value;

            var json = Json();
            if (json == null) return fallback;

            var at = json.IndexOf("\"" + name + "\"", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return fallback;

            var colon = json.IndexOf(':', at);
            if (colon < 0) return fallback;

            var digits = string.Empty;
            for (var i = colon + 1; i < json.Length && digits.Length < 6; i++)
            {
                var c = json[i];
                if (char.IsDigit(c)) digits += c;
                else if (digits.Length > 0) break;
            }

            return digits.Length > 0 && int.TryParse(digits, out var value) ? value : fallback;
        }

        private static string? Json()
        {
            if (_read) return _json;
            _read = true;

            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord", "Configs", "ModSettings", "Global",
                    "ProgressionExpanded", "ProgressionExpanded_v1.json");

                if (File.Exists(path)) _json = File.ReadAllText(path);
            }
            catch
            {
                // Unreadable settings must not stop the module loading.
            }

            return _json;
        }
    }
}
