using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TaleWorlds.Library;

namespace SkillLearningRate
{
    public sealed class ModSettings
    {
        public const string ModuleId = "SkillLearningRate";

        public bool Enabled { get; set; } = true;

        public float GlobalLearningRateMultiplier { get; set; } = 1f;

        public static ModSettings Current { get; private set; } = new ModSettings();

        public static void Load()
        {
            var settings = new ModSettings();
            var path = GetSettingsPath();

            if (!File.Exists(path))
            {
                Current = settings;
                Debug.Print($"[{ModuleId}] settings.xml not found at {path}. Using defaults.");
                return;
            }

            try
            {
                var document = XDocument.Load(path);
                var root = document.Root;
                if (root != null)
                {
                    settings.Enabled = ReadBool(root, "Enabled", true);
                    settings.GlobalLearningRateMultiplier = Math.Max(0f, ReadFloat(root, "GlobalLearningRateMultiplier", 1f));
                }

                Current = settings;
                Debug.Print($"[{ModuleId}] Loaded settings. Enabled={settings.Enabled}, GlobalLearningRateMultiplier={settings.GlobalLearningRateMultiplier}");
            }
            catch (Exception exception)
            {
                Current = new ModSettings();
                Debug.Print($"[{ModuleId}] Failed to load settings.xml: {exception.Message}. Using defaults.");
            }
        }

        public bool ShouldApplyMultiplier(out float multiplier)
        {
            multiplier = GlobalLearningRateMultiplier;
            return Enabled && Math.Abs(multiplier - 1f) > 0.0001f;
        }

        private static string GetSettingsPath()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(ModSettings).Assembly.Location) ?? ".";
            return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "ModuleData", "settings.xml"));
        }

        private static bool ReadBool(XElement root, string name, bool fallback)
        {
            var value = root.Element(name)?.Value;
            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static float ReadFloat(XElement root, string name, float fallback)
        {
            var value = root.Element(name)?.Value;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
