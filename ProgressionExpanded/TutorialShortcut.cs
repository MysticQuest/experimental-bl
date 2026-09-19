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
        internal static readonly Action FixKit = SwapTheKit;

        /// <summary>
        /// Applies the villagers' swap to a campaign that already took the full set.
        /// </summary>
        private static void SwapTheKit()
        {
            try
            {
                var behaviour = DestituteStartBehavior.Current;
                if (behaviour == null) { Say("No campaign is running."); return; }

                Say(behaviour.SwapNow("stealth_tutorial_set_player"));
            }
            catch (Exception exception)
            {
                Guard.Report("SwapTheKit", exception);
                Say("Could not swap the kit; see the mod log.");
            }
        }

        internal static readonly Action Leave = EndActiveMission;
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
