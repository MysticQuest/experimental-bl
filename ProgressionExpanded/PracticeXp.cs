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
    /// Both are sliders, defaulting to a third and two thirds. Those keep the ordering vanilla
    /// intended - a real fight worth most, a tournament next, practice least - while putting both
    /// within sight of it. Nobody grinds a skill to 330 in the ring at a third rate, but a
    /// morning there is no longer wasted.
    ///
    /// The method is private and static, which Harmony patches happily; it is the base model's,
    /// so the Naval DLC's own model gets the new figures too, since that one decorates rather
    /// than replaces.
    /// </remarks>
    [HarmonyPatch]
    internal static class PracticeXpPatch
    {
        private const string Model = "TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel";

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
                var settings = Settings.Instance;
                if (!Mod.On || settings == null) return;

                if (missionType == CombatXpModel.MissionTypeEnum.PracticeFight)
                    __result = settings.PracticeXpRate;
                else if (missionType == CombatXpModel.MissionTypeEnum.Tournament)
                    __result = settings.TournamentXpRate;
            }
            catch (Exception exception)
            {
                Guard.Report("PracticeXp", exception);
            }
        }
    }
}
