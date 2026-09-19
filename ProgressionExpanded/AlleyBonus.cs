using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace ProgressionExpanded
{
    /// <summary>
    /// What running the criminal side of a town is worth to the man running it.
    /// </summary>
    /// <remarks>
    /// Alleys are the one thing in the game that is unambiguously Roguery's, and vanilla asks the
    /// skill about none of it. Every number is a bare constant or a dice roll: income is the
    /// town's prosperity over fifty, crime rating is a flat 0.5 a day, capacity is 10, the warning
    /// before an attack comes only from the thugs already standing there, and the recruits an
    /// alley turns up are a pure roll. A master criminal's alley earns and costs exactly what a
    /// novice's does.
    ///
    /// The crime rating one needs no ownership check: the whole crime model is player-facing -
    /// every other method on it is named for the player - so the only rating that constant ever
    /// reaches is yours.
    /// </remarks>
    internal static class AlleyBonus
    {
        internal static bool Active() => TacticsBonus.Active();

        /// <summary>True when this is one of the player clan's own concerns.</summary>
        internal static bool Ours(Hero? owner)
        {
            try
            {
                return owner != null && Clan.PlayerClan != null && owner.Clan == Clan.PlayerClan;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>A well-run alley takes more and is noticed less.</summary>
    [HarmonyPatch(typeof(DefaultAlleyModel))]
    internal static class AlleyTakingsPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DefaultAlleyModel.GetDailyIncomeOfAlley))]
        private static void Earn(Alley alley, ref int __result)
        {
            Guard.Touch("AlleyIncome");
            try
            {
                if (__result <= 0 || !AlleyBonus.Active() || !AlleyBonus.Ours(alley?.Owner)) return;

                var share = MasteryEffects.PlayerValue(MasteryEffects.AlleyIncome);
                if (share > 0f) __result = (int)Math.Round(__result * (1f + share));
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AlleyBonus.Earn", exception);
            }
}

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DefaultAlleyModel.GetDailyCrimeRatingOfAlley), MethodType.Getter)]
        private static void KeepQuiet(ref float __result)
        {
            Guard.Touch("AlleyCrime");
            try
            {
                if (__result <= 0f || !AlleyBonus.Active()) return;

                // The effect is declared negative, so this reads as a reduction without a sign flip.
                var share = MasteryEffects.PlayerValue(MasteryEffects.AlleyQuiet);
                if (share < 0f) __result = Math.Max(0f, __result * (1f + share));
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AlleyBonus.KeepQuiet", exception);
            }
}

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DefaultAlleyModel.MaximumTroopCountInPlayerOwnedAlley), MethodType.Getter)]
        private static void Hold(ref int __result)
        {
            Guard.Touch("AlleyCapacity");
            try
            {
                if (!AlleyBonus.Active()) return;

                var extra = MasteryEffects.AlleyCapacityValue();
                if (extra > 0f) __result += (int)Math.Round(extra);
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AlleyBonus.Hold", exception);
            }
}

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DefaultAlleyModel.GetAlleyAttackResponseTimeInDays))]
        private static void Warn(ref float __result)
        {
            Guard.Touch("AlleyWarning");
            try
            {
                if (__result <= 0f || !AlleyBonus.Active()) return;

                var share = MasteryEffects.AlleyWarningValue();
                if (share > 0f) __result *= 1f + share;
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AlleyBonus.Warn", exception);
            }
}

        /// <summary>
        /// Better men turn up, by tilting the roll rather than rewriting who it can produce.
        /// </summary>
        /// <remarks>
        /// The roll runs low-is-good - above 0.5 nothing turns up at all, and each band below it
        /// gives more and better troops. Shrinking the number toward zero therefore improves the
        /// draw without touching the table itself, so the rosters stay exactly the ones the game
        /// would ever hand out.
        /// </remarks>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(DefaultAlleyModel.GetTroopsToRecruitFromAlleyDependingOnAlleyRandom))]
        private static void Tilt(Alley alley, ref float random)
        {
            Guard.Touch("AlleyRecruits");
            try
            {
                if (random <= 0f || !AlleyBonus.Active() || !AlleyBonus.Ours(alley?.Owner)) return;

                var share = MasteryEffects.AlleyRecruitsValue();
                if (share > 0f) random *= Math.Max(0f, 1f - share);
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AlleyBonus.Tilt", exception);
            }
}
    }
}
