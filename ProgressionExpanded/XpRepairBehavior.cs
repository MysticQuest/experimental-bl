using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ProgressionExpanded
{
    /// <summary>
    /// Realigns stored skill XP with the curve that is actually running.
    /// </summary>
    /// <remarks>
    /// The game stores an absolute XP total per skill and shows progress as
    /// <c>stored - GetXpRequiredForSkillLevel(currentLevel)</c>. Change the curve underneath a
    /// character -- by installing this, or by moving any setting and reloading -- and that
    /// subtraction goes negative, which is why the character screen can read something like
    /// "-134154 / 9862 xp". Re-seeding the skill to the floor of its current level fixes the
    /// display and costs at most the partial progress toward the next level.
    /// </remarks>
    internal sealed class XpRepairBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents() =>
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // The personal effects have to exist before anything reads or lists them, and the
            // campaign list they live in only exists once the session is up.
            Log.Write("Session launched: syncing effects");
            MasteryEffects.Sync();
            Log.Write("Session launched: repairing skill xp");
            Repair();
            Log.Write("Session launched: done. Model calls so far: " + Probe.Summary());
        }

        internal static void Repair()
        {
            try
            {
                var developer = Hero.MainHero?.HeroDeveloper;
                if (developer == null) return;

                var repaired = 0;
                foreach (var skill in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
                {
                    if (developer.GetSkillXpProgress(skill) >= 0) continue;
                    developer.InitializeSkillXp(skill);
                    repaired++;
                }

                if (repaired > 0)
                {
                    Log.Write($"Realigned {repaired} skills to the current curve.");
                }
            }
            catch (Exception exception)
            {
                Log.Write($"Could not realign skill XP: {exception}");
            }
        }
    }
}
