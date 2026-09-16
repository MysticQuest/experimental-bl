using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;

namespace MasteryCurve
{
    /// <summary>
    /// The buttons behind the debug section. Everything used here is public game API.
    /// </summary>
    internal static class DebugTools
    {
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

        internal static void CurrentStatus() => Run(developer =>
        {
            var hero = developer.Hero;
            var next = hero.Level + 1;
            return $"Level {hero.Level}, {developer.UnspentFocusPoints} focus and "
                   + $"{developer.UnspentAttributePoints} attribute points unspent. "
                   + $"Level {next} needs {Curve.CharacterXpRequired(next):N0} raw XP. "
                   + $"Skill 100 costs {Curve.SkillXpRequired(100):N0}, "
                   + $"275 costs {Curve.SkillXpRequired(275):N0}, "
                   + $"330 costs {Curve.SkillXpRequired(Curve.SkillCap):N0}.";
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
                Debug.Print($"[MasteryCurve] Debug action failed: {exception}");
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

            Debug.Print("[MasteryCurve] " + message);
        }
    }
}
