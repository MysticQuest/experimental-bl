using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace ProgressionExpanded
{
    /// <summary>
    /// A trader's caravans and workshops sometimes have a very good day.
    /// </summary>
    /// <remarks>
    /// Vanilla gives Trade no personal effect at all, and what it does govern - the price you pay
    /// and what your holdings earn - is flat once a caravan is running. This is the one thing a
    /// merchant's judgement plausibly buys that a percentage does not: the occasional windfall.
    ///
    /// The roll is deliberately not random at the moment of asking. Both income methods are called
    /// for the clan finance screen as well as for the actual payout, and often several times a
    /// frame, so a live roll would make the displayed figure flicker and disagree with what you
    /// were paid. Instead the outcome is hashed from the asset and the campaign day: fixed for a
    /// given day, so the screen tells the truth, and freshly drawn the next morning.
    /// </remarks>
    internal static class TradeBonus
    {
        internal static bool Active() => TacticsBonus.Active();

        /// <summary>A stable value in [0,1) for one asset on one day.</summary>
        internal static float RollFor(string id, int day)
        {
            unchecked
            {
                var seed = 17;
                seed = seed * 31 + (id ?? string.Empty).GetHashCode();
                seed = seed * 31 + day;

                // Scrambled so that neighbouring ids and consecutive days do not correlate.
                var x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;

                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }

        internal static int Today()
        {
            try
            {
                return Campaign.Current == null ? 0 : (int)CampaignTime.Now.ToDays;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>True when this asset pays double today.</summary>
        internal static bool PaysDouble(string id, Hero? owner)
        {
            if (!Active() || !AlleyBonus.Ours(owner)) return false;

            var chance = MasteryEffects.PlayerValue(MasteryEffects.DoubleIncomeChance);
            if (chance <= 0f) return false;

            return RollFor(id, Today()) < chance;
        }
    }

    /// <summary>Doubles the day's take when the day happens to be a good one.</summary>
    [HarmonyPatch(typeof(DefaultClanFinanceModel))]
    internal static class WindfallPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateOwnerIncomeFromCaravan))]
        private static void Caravan(MobileParty caravan, ref int __result)
        {
            Guard.Touch("CaravanIncome");
            if (__result <= 0) return;

            try
            {
                if (caravan == null) return;
                if (TradeBonus.PaysDouble(caravan.StringId, caravan.Party?.Owner)) __result *= 2;
            }
            catch
            {
                // The ordinary take still stands.
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateOwnerIncomeFromWorkshop))]
        private static void Workshop(Workshop workshop, ref int __result)
        {
            Guard.Touch("WorkshopIncome");
            if (__result <= 0) return;

            try
            {
                if (workshop == null) return;
                var id = (workshop.Settlement?.StringId ?? "?") + "/" + (workshop.Tag ?? "?");
                if (TradeBonus.PaysDouble(id, workshop.Owner)) __result *= 2;
            }
            catch
            {
                // The ordinary take still stands.
            }
        }
    }
}
