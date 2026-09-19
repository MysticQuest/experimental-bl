using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
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
        internal static readonly Action Succeed = CompleteQuests;

        /// <summary>
        /// Completes every quest the player currently holds, successfully.
        /// </summary>
        /// <remarks>
        /// The village mission takes several minutes to play and the only interesting second is
        /// the one where it pays out. This drives the same completion the mission itself calls, so
        /// whatever a quest hands over on success is handed over now, probe and all.
        ///
        /// Every active quest, not a chosen one: the quest worth testing is rarely the only one
        /// running, and picking through them by name is a worse tool than finishing the lot on a
        /// test campaign.
        /// </remarks>
        private static void CompleteQuests()
        {
            try
            {
                var manager = Campaign.Current?.QuestManager;
                if (manager == null) { Say("No campaign, so no quests."); return; }

                var quests = new List<QuestBase>();
                foreach (var quest in manager.Quests)
                    if (quest != null && !quest.IsFinalized) quests.Add(quest);

                if (quests.Count == 0) { Say("No active quests to complete."); return; }

                foreach (var quest in quests)
                {
                    Log.Write("Debug: completing quest " + quest.GetType().Name + " - " + quest.Title);
                    quest.CompleteQuestWithSuccess();
                }

                Say($"Completed {quests.Count} quest(s). The log lists what they paid.");
            }
            catch (TargetInvocationException invocation)
            {
                Guard.Report("CompleteQuests", invocation.InnerException ?? invocation);
                Say("A quest refused to complete; see the mod log.");
            }
            catch (Exception exception)
            {
                Guard.Report("CompleteQuests", exception);
                Say("Could not complete the quests; see the mod log.");
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
