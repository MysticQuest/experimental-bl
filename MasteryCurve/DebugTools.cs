using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace MasteryCurve
{
    /// <summary>
    /// The buttons behind the debug section. Everything used here is public game API, so nothing
    /// is patched or reflected; the point is to reach a late-career character without playing one.
    /// </summary>
    internal static class DebugTools
    {
        internal static void GiveFocusPoints()
        {
            Run("focus points", (developer, amount) =>
            {
                developer.UnspentFocusPoints += amount;
                return $"{amount} focus points added ({developer.UnspentFocusPoints} unspent).";
            });
        }

        internal static void GiveAttributePoints()
        {
            Run("attribute points", (developer, amount) =>
            {
                developer.UnspentAttributePoints += amount;
                return $"{amount} attribute points added ({developer.UnspentAttributePoints} unspent).";
            });
        }

        internal static void GiveCharacterLevels()
        {
            Run("character levels", (developer, amount) =>
            {
                var target = developer.Hero.Level + amount;
                developer.SetInitialLevel(target);
                developer.CheckLevel(false);
                return $"Now character level {developer.Hero.Level}, "
                       + $"{developer.UnspentFocusPoints} focus and {developer.UnspentAttributePoints} attribute points unspent.";
            });
        }

        internal static void Report()
        {
            Run("report", (developer, _) =>
            {
                var hero = developer.Hero;
                var next = hero.Level + 1;
                return $"Level {hero.Level} · {developer.UnspentFocusPoints} focus, "
                       + $"{developer.UnspentAttributePoints} attribute unspent · "
                       + $"level {next} needs {Curve.CharacterXpRequired(next):N0} raw XP · "
                       + $"skill 100 costs {Curve.SkillXpRequired(100):N0}, "
                       + $"275 costs {Curve.SkillXpRequired(275):N0}, "
                       + $"330 costs {Curve.SkillXpRequired(Curve.SkillCap):N0}.";
            });
        }

        private static void Run(string what, Func<TaleWorlds.CampaignSystem.CharacterDevelopment.HeroDeveloper, int, string> action)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.DebugEnabled)
            {
                Notify("Mastery Curve: turn on the debug tools first.");
                return;
            }

            var hero = Hero.MainHero;
            if (Campaign.Current == null || hero?.HeroDeveloper == null)
            {
                Notify("Mastery Curve: load a campaign first.");
                return;
            }

            try
            {
                var message = action(hero.HeroDeveloper, Math.Max(1, settings.DebugAmount));
                Notify("Mastery Curve: " + message);
            }
            catch (Exception exception)
            {
                Notify($"Mastery Curve: could not change {what}.");
                Debug.Print($"[MasteryCurve] Debug action '{what}' failed: {exception}");
            }
        }

        private static void Notify(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage(message));
            Debug.Print("[MasteryCurve] " + message);
        }
    }
}
