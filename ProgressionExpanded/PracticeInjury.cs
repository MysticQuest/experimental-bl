using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProgressionExpanded
{
    /// <summary>
    /// Going down in the practice ring costs real health.
    /// </summary>
    /// <remarks>
    /// Practice is free in vanilla: you are knocked out, you stand up, you go again, and the only
    /// cost is the time. Paying a third rate for it - as this mod now does - would make the ring
    /// the obvious way to train every skill, with nothing to stop a player spending a week there.
    ///
    /// A fifth of your health per knockdown is what stops it. Five in a row and there is nothing
    /// left to lose, and long before that the wound threshold takes you out of the ring until you
    /// have healed. It is the same limit a real fight imposes, only without the dying.
    ///
    /// Health is never taken below one point: the ring should end a training session, not a
    /// campaign.
    /// </remarks>
    [HarmonyPatch]
    internal static class PracticeInjuryPatch
    {
        private const string Controller = "SandBox.Missions.MissionLogics.Arena.ArenaPracticeFightMissionController";

        /// <summary>A fifth of the hero's full health, so five knockdowns empty it.</summary>
        private const float Share = 0.2f;

        private static bool Prepare() => Target() != null;

        private static MethodBase? Target() =>
            AccessTools.Method(AccessTools.TypeByName(Controller), "OnAgentRemoved");

        private static MethodBase TargetMethod() => Target()!;

        [HarmonyPostfix]
        private static void Wound(Agent affectedAgent)
        {
            Guard.Touch("PracticeInjury");

            try
            {
                var settings = Settings.Instance;
                if (!Mod.On || settings == null || !settings.PracticeInjuries) return;

                if (affectedAgent == null || !affectedAgent.IsMainAgent) return;

                var hero = Hero.MainHero;
                if (hero == null) return;

                var lost = Math.Max(1, (int)(hero.MaxHitPoints * Share));
                var left = Math.Max(1, hero.HitPoints - lost);
                if (left == hero.HitPoints) return;

                lost = hero.HitPoints - left;
                hero.HitPoints = left;

                // No notification: the health bar is the notification.
                Log.Write($"Practice: lost {lost} health going down, {left} of {hero.MaxHitPoints} left");
            }
            catch (Exception exception)
            {
                Guard.Report("PracticeInjury", exception);
            }
        }
    }
}
