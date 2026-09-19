using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using TaleWorlds.Library;

namespace ProgressionExpanded
{
    /// <summary>
    /// Ends the tutorial on the spot, so its ending can be tested without playing it.
    /// </summary>
    /// <remarks>
    /// The stealth mission in the village takes several minutes, and everything worth checking
    /// happens in the second after it ends: what the villagers hand over, and whether the sack
    /// survives it. Playing the whole thing for one line of log is the reason this exists.
    ///
    /// It calls the game's own completion, the same one the mission calls, with isSkipped false -
    /// so the mod sees a tutorial that was played rather than one that was skipped. Reached by
    /// reflection because StoryMode is not referenced, and it reports what went wrong rather than
    /// failing quietly, since a test tool that lies is worse than none.
    /// </remarks>
    internal static class TutorialShortcut
    {
        internal static readonly Action Finish = Run;
        internal static readonly Action Leave = EndActiveMission;
        internal static readonly Action Win = WinActiveMission;

        /// <summary>
        /// Routs whoever is fighting you, so the mission ends the way winning it ends.
        /// </summary>
        /// <remarks>
        /// Leaving a mission and winning one are different endings, and only the second pays: the
        /// hideout hands its spoils over on a victory. This drives the game's own
        /// MakeEnemiesFleeCheat rather than ending the mission directly, so the battle resolves
        /// through its normal victory path and everything waiting on that still runs, loot
        /// included.
        /// </remarks>
        private static void WinActiveMission()
        {
            try
            {
                if (Mission.Current == null) { Say("No mission is running."); return; }

                Log.Write("Debug: routing the enemy from the settings screen");
                Mission.MakeEnemiesFleeCheat(new System.Collections.Generic.List<string>());
                Say("The enemy is routed. The mission should end on its own.");
            }
            catch (Exception exception)
            {
                Guard.Report("WinMission", exception);
                Say("Could not rout the enemy; see the mod log.");
            }
        }

        /// <summary>
        /// Ends whatever scene is running and puts the player back on the world map.
        /// </summary>
        /// <remarks>
        /// For getting out of a mission rather than playing it to the end. It calls the game's own
        /// EndMission, so the mission tears itself down the way it always does and whatever is
        /// waiting for it to finish still runs.
        ///
        /// Note it ends the mission, which is not the same as winning it: a scene left this way
        /// counts as left, not completed, and a reward that depends on completing it will not
        /// arrive.
        /// </remarks>
        private static void EndActiveMission()
        {
            try
            {
                var mission = Mission.Current;
                if (mission == null) { Say("No mission is running."); return; }

                Log.Write("Debug: ending the active mission from the settings screen");
                mission.EndMission();
                Say("Mission ended.");
            }
            catch (Exception exception)
            {
                Guard.Report("EndMission", exception);
                Say("Could not end the mission; see the mod log.");
            }
        }

        private static void Run()
        {
            try
            {
                var manager = AccessTools.TypeByName("StoryMode.StoryModeManager");
                var current = manager == null
                    ? null
                    : AccessTools.Property(manager, "Current")?.GetValue(null);

                if (current == null) { Say("StoryMode is not loaded, so there is no tutorial."); return; }

                var storyLine = AccessTools.Property(manager, "MainStoryLine")?.GetValue(current);
                if (storyLine == null) { Say("No story line yet; start a campaign first."); return; }

                var complete = AccessTools.Method(storyLine.GetType(), "CompleteTutorialPhase",
                    new[] { typeof(bool) });
                if (complete == null) { Say("This build of the game has no CompleteTutorialPhase."); return; }

                // false: the game is being told the tutorial was played, not skipped.
                complete.Invoke(storyLine, new object[] { false });

                Say("Tutorial finished. Check the log for what the villagers handed over.");
                Log.Write("Debug: tutorial completed from the settings screen");
            }
            catch (TargetInvocationException invocation)
            {
                Guard.Report("TutorialShortcut", invocation.InnerException ?? invocation);
                Say("The tutorial refused to finish; see the mod log.");
            }
            catch (Exception exception)
            {
                Guard.Report("TutorialShortcut", exception);
                Say("Could not finish the tutorial; see the mod log.");
            }
        }

        private static void Say(string message) =>
            InformationManager.DisplayMessage(new InformationMessage(message));
    }
}
