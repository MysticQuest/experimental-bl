using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ProgressionExpanded
{
    /// <summary>
    /// The buttons behind the debug section. Everything used here is public game API.
    /// </summary>
    internal static class DebugTools
    {
        /// <summary>How many levels one press is worth.</summary>
        private const int SkillStep = 10;

        /// <summary>
        /// Set while a debug button is granting skill levels, so the award lands unscaled.
        /// </summary>
        /// <remarks>
        /// <c>ChangeSkillLevel</c> works out exactly the raw XP needed for the levels asked for and
        /// hands it to <c>AddSkillXp</c>, which is where the per-skill multiplier lives. Without
        /// this, pressing +10 on Tactics at 1.75x would grant thirteen levels.
        /// </remarks>
        internal static bool GrantingLevels;

        internal static void AddSkillLevels() => Run(developer =>
        {
            var skill = Settings.Instance?.DebugSkillObject();
            if (skill == null) return "that skill is not in this game.";

            var before = developer.Hero.GetSkillValue(skill);
            GrantingLevels = true;
            try
            {
                developer.ChangeSkillLevel(skill, SkillStep, false);
            }
            finally
            {
                GrantingLevels = false;
            }

            var after = developer.Hero.GetSkillValue(skill);
            return $"{skill.Name} {before} -> {after}.";
        });

        internal static void AddFocusPoint() => Run(developer =>
        {
            developer.UnspentFocusPoints += 1;
            return $"{developer.UnspentFocusPoints} focus points unspent.";
        });

        internal static void AddAttributePoint() => Run(developer =>
        {
            developer.UnspentAttributePoints += 1;
            return $"{developer.UnspentAttributePoints} attribute points unspent.";
        });

        internal static void AddCharacterLevel() => Run(developer =>
        {
            developer.SetInitialLevel(developer.Hero.Level + 1);
            developer.CheckLevel(false);
            return $"Character level {developer.Hero.Level}, "
                   + $"{developer.UnspentFocusPoints} focus and {developer.UnspentAttributePoints} attribute points unspent.";
        });

        internal static void ResetPoints() => Run(developer =>
        {
            developer.UnspentFocusPoints = 0;
            developer.UnspentAttributePoints = 0;
            return "Unspent focus and attribute points cleared.";
        });

        /// <summary>
        /// Answers, in game, why the attribute card is or is not showing its lines.
        /// </summary>
        /// <remarks>
        /// Written because this mod's <c>Debug.Print</c> output reaches no log that can be read
        /// from outside the game, which turned three rounds of a simple bug into guesswork.
        /// </remarks>
        internal static void AttributeReport()
        {
            var parts = new List<string>();

            parts.Add($"{SubModule.PatchesApplied} patch classes applied");
            parts.Add(SubModule.PatchFailures.Count == 0
                ? "none failed"
                : "FAILED: " + string.Join(", ", SubModule.PatchFailures.ToArray()));

            parts.Add(AttributeBonus.Active() ? "bonuses on" : "bonuses OFF");

            try
            {
                if (Campaign.Current != null)
                {
                    var cunning = AttributeBonus.Of(Hero.MainHero, DefaultCharacterAttributes.Cunning);
                    parts.Add($"Cunning {cunning}");
                }
            }
            catch (Exception exception)
            {
                parts.Add("probe threw: " + exception.GetType().Name);
            }

            Notify("Progression Expanded: " + string.Join(" | ", parts.ToArray()));
        }

        internal static void CurrentStatus() => Run(developer =>
        {
            var hero = developer.Hero;
            var next = hero.Level + 1;
            return $"Level {hero.Level}, {developer.UnspentFocusPoints} focus and "
                   + $"{developer.UnspentAttributePoints} attribute points unspent. "
                   + $"Level {next} needs {Curve.CharacterXpRequired(next):N0} raw XP. "
                   + $"Skill 100 costs {Curve.SkillXpRequired(100):N0}, "
                   + $"275 costs {Curve.SkillXpRequired(275):N0}, "
                   + $"330 costs {Curve.SkillXpRequired(Curve.SkillCap):N0}. "
                   + $"Skill 275 is reachable from character level {Curve.MasteryLevel:0.0} at full investment.";
        });

        private static void Run(Func<HeroDeveloper, string> action)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.DebugEnabled)
            {
                Notify("Mastery Curve: enable the debug tools first.");
                return;
            }

            try
            {
                // Reached from the main menu there is no campaign at all, and touching
                // Hero.MainHero there throws rather than returning null.
                if (Campaign.Current == null)
                {
                    Notify("Mastery Curve: load a campaign first.");
                    return;
                }

                var developer = Hero.MainHero?.HeroDeveloper;
                if (developer == null)
                {
                    Notify("Mastery Curve: no character to change yet.");
                    return;
                }

                Notify("Mastery Curve: " + action(developer));
            }
            catch (Exception exception)
            {
                Notify("Mastery Curve: load a campaign first.");
                Log.Write($"Debug action failed: {exception}");
            }
        }

        private static void Notify(string message)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
            }
            catch
            {
                // No message system outside a running game; the log line below still lands.
            }

            Log.Write($"" + message);
        }
    }
}
