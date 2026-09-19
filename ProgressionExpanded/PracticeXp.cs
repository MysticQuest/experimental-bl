using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace ProgressionExpanded
{
    /// <summary>
    /// Makes practice fights and tournaments worth training in.
    /// </summary>
    /// <remarks>
    /// Vanilla pays a sixteenth for a practice fight and a third for a tournament, against full
    /// value for a real battle. A sixteenth is a rounding error next to a curve that asks for
    /// millions of XP: an afternoon at the practice ring buys nothing a player can see, which
    /// makes the one safe way to train also the one pointless way.
    ///
    /// A third and two thirds keep the ordering vanilla intended - a real fight is still worth
    /// most, a tournament next, practice least - while putting both within sight of it. Nobody
    /// will grind a skill to 330 in the ring at a third rate, but a morning there is no longer
    /// wasted.
    ///
    /// The method is private and static, which Harmony patches happily; it is the base model's,
    /// so the Naval DLC's own model gets the new figures too, since that one decorates rather
    /// than replaces.
    /// </remarks>
    [HarmonyPatch]
    internal static class PracticeXpPatch
    {
        private const string Model = "TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel";

        /// <summary>A third of a real fight, up from a sixteenth.</summary>
        private const float Practice = 0.33f;

        /// <summary>Two thirds, up from a third.</summary>
        private const float Tournament = 0.66f;

        private static bool Prepare() => Target() != null;

        private static MethodBase? Target() =>
            AccessTools.Method(AccessTools.TypeByName(Model), "GetXpfMultiplierForMissionType");

        private static MethodBase TargetMethod() => Target()!;

        [HarmonyPostfix]
        private static void Raise(CombatXpModel.MissionTypeEnum missionType, ref float __result)
        {
            Guard.Touch("PracticeXp");

            try
            {
                if (!Mod.On || !(Settings.Instance?.Enabled ?? true)) return;

                if (missionType == CombatXpModel.MissionTypeEnum.PracticeFight) __result = Practice;
                else if (missionType == CombatXpModel.MissionTypeEnum.Tournament) __result = Tournament;
            }
            catch (Exception exception)
            {
                Guard.Report("PracticeXp", exception);
            }
        }
    }
}
