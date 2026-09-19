using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// What a high skill is worth to you personally, outside its perks.
    /// </summary>
    /// <remarks>
    /// Most of a skill's perks are spent on the party, the fief or the realm. Points you pour into
    /// a skill for your own sake buy very little, and a few skills - Tactics worst of all - buy
    /// almost nothing. This is the carpet under all of them: every twenty points is a step, and
    /// each step is worth more than the one before it, so the back half of a skill is where the
    /// reward actually lives.
    ///
    /// Step n is worth n units, so k steps are worth k(k+1)/2 out of the 136 a capped skill is
    /// worth. Skill 100 is a tenth of the way there, 200 is two fifths, 275 is two thirds. Nothing
    /// here is a threshold the player has to look up - it just keeps growing, faster near the top.
    /// </remarks>
    internal static class Mastery
    {
        /// <summary>Skill points per step.</summary>
        internal const int Step = 20;

        internal const int FullSteps = Curve.SkillCap / Step;                   // 16
        private const float FullWeight = FullSteps * (FullSteps + 1) / 2f;      // 136

        /// <summary>How far along the reward curve a bare skill level is, from 0 to 1.</summary>
        internal static float ProgressAt(int skillLevel)
        {
            var steps = Math.Max(0, Math.Min(FullSteps, skillLevel / Step));
            return steps <= 0 ? 0f : steps * (steps + 1) / 2f / FullWeight;
        }

        /// <summary>Steps this hero has taken in one skill, 0 to 16.</summary>
        internal static int StepsTaken(Hero? hero, SkillObject? skill)
            => hero == null || skill == null ? 0 : Math.Min(FullSteps, hero.GetSkillValue(skill) / Step);

        /// <summary>How far along the reward curve a hero is in one skill, from 0 to 1.</summary>
        internal static float Progress(Hero? hero, SkillObject? skill)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.SkillBonuses) return 0f;
            if (hero == null || skill == null) return 0f;

            return ProgressAt(hero.GetSkillValue(skill));
        }

        /// <summary>The same, for the player, and safe to call before a campaign exists.</summary>
        internal static float PlayerProgress(SkillObject? skill)
        {
            try
            {
                if (Campaign.Current == null) return 0f;
                return Progress(Hero.MainHero, skill);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>True when this party is the player's, or marches in the player's army.</summary>
        internal static bool IsPlayerSide(MobileParty? party)
        {
            if (party == null || Campaign.Current == null) return false;
            var main = MobileParty.MainParty;
            if (main == null) return false;
            if (party == main) return true;
            return main.Army != null && party.Army == main.Army;
        }
    }
}
