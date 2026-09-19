using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ProgressionExpanded
{
    /// <summary>
    /// Keeps one character on disk so the same one can be stamped onto a fresh campaign.
    /// </summary>
    /// <remarks>
    /// Testing anything that only happens on a new campaign means making a character again, and
    /// the game has no way to save one: its character code covers the face and nothing else, so
    /// the attributes, the focus and the skills have to be clicked through every time.
    ///
    /// This stores all of it in one small text file next to the settings. Make a character once,
    /// press Save; from then on, make any character at all and press Load to become that one.
    /// It is a development tool - it writes straight to the hero with none of the game's own
    /// bookkeeping, which is fine for a test campaign and not something to use on a real one.
    /// </remarks>
    internal static class CharacterTemplate
    {
        internal static readonly Action Save = Store;
        internal static readonly Action Load = Apply;

        private const string FileName = "character.txt";

        private static void Store()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null) { Say("No character to save."); return; }

                var text = new StringBuilder();
                text.AppendLine("name=" + hero.Name);
                text.AppendLine("female=" + hero.IsFemale);
                text.AppendLine("body=" + hero.BodyProperties);

                foreach (var attribute in Attributes())
                    text.AppendLine("attr." + attribute.StringId + "=" + hero.GetAttributeValue(attribute));

                foreach (var skill in Skills())
                {
                    text.AppendLine("skill." + skill.StringId + "=" + hero.GetSkillValue(skill));
                    text.AppendLine("focus." + skill.StringId + "=" + hero.HeroDeveloper.GetFocus(skill));
                }

                var path = Path();
                if (path == null) { Say("Nowhere to write the character file."); return; }

                File.WriteAllText(path, text.ToString(), Encoding.UTF8);
                Say("Character saved. Load it on any new campaign.");
                Log.Write("Character template saved to " + path);
            }
            catch (Exception exception)
            {
                Guard.Report("CharacterTemplate.Save", exception);
                Say("Could not save the character; see the mod log.");
            }
        }

        private static void Apply()
        {
            try
            {
                var hero = Hero.MainHero;
                var path = Path();
                if (hero == null || path == null || !File.Exists(path))
                {
                    Say("No saved character yet. Press Save on one first.");
                    return;
                }

                var values = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(path))
                {
                    var split = line.IndexOf('=');
                    if (split > 0) values[line.Substring(0, split)] = line.Substring(split + 1);
                }

                if (values.TryGetValue("name", out var name) && !string.IsNullOrEmpty(name))
                {
                    var text = new TextObject(name);
                    hero.SetName(text, text);
                }

                // Only the static half is settable, and it is the half that holds the face.
                if (values.TryGetValue("body", out var body)
                    && BodyProperties.FromString(body, out var properties))
                {
                    hero.StaticBodyProperties = properties.StaticProperties;
                }

                // Attributes are cleared wholesale and rebuilt, because the game has no public
                // setter for one: it only knows how to add and remove.
                hero.ClearAttributes();
                foreach (var attribute in Attributes())
                    if (Number(values, "attr." + attribute.StringId, out var value) && value > 0)
                        hero.HeroDeveloper.AddAttribute(attribute, value, false);

                foreach (var skill in Skills())
                {
                    if (Number(values, "skill." + skill.StringId, out var level))
                        hero.SetSkillValue(skill, level);

                    var focus = hero.HeroDeveloper.GetFocus(skill);
                    if (Number(values, "focus." + skill.StringId, out var wanted) && wanted != focus)
                        hero.HeroDeveloper.AddFocus(skill, wanted - focus, false);
                }

                hero.HeroDeveloper.UnspentAttributePoints = 0;
                hero.HeroDeveloper.UnspentFocusPoints = 0;

                Say("Character loaded.");
                Log.Write("Character template applied from " + path);
            }
            catch (Exception exception)
            {
                Guard.Report("CharacterTemplate.Load", exception);
                Say("Could not load the character; see the mod log.");
            }
        }

        private static IEnumerable<CharacterAttribute> Attributes() =>
            MBObjectManager.Instance?.GetObjectTypeList<CharacterAttribute>() ?? (IEnumerable<CharacterAttribute>)new CharacterAttribute[0];

        private static IEnumerable<SkillObject> Skills() =>
            MBObjectManager.Instance?.GetObjectTypeList<SkillObject>() ?? (IEnumerable<SkillObject>)new SkillObject[0];

        private static bool Number(IDictionary<string, string> values, string key, out int number)
        {
            number = 0;
            return values.TryGetValue(key, out var text) && int.TryParse(text, out number);
        }

        /// <summary>Beside the settings file, so the two travel together.</summary>
        private static string? Path()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) return null;

            var folder = System.IO.Path.Combine(documents, "Mount and Blade II Bannerlord",
                "Configs", "ModSettings", "Global", "ProgressionExpanded");
            Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, FileName);
        }

        private static void Say(string message) =>
            InformationManager.DisplayMessage(new InformationMessage(message));
    }
}
