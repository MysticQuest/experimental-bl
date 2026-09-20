using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// Keeps the one fact the daily build drift depends on: whether the player has been fighting.
    /// </summary>
    /// <remarks>
    /// Vanilla's own behavior keeps this in a private field. Rather than reach into it - which is
    /// the part most likely to break on a game update - this keeps its own copy off the same public
    /// event, so the daily step can be recomputed without touching vanilla's internals at all. It
    /// is saved, so a reload does not reset the reckoning.
    ///
    /// Weight is left entirely alone; only build is touched.
    /// </remarks>
    internal sealed class BodyDriftBehavior : CampaignBehaviorBase
    {
        private CampaignTime _lastEncounter;

        internal static BodyDriftBehavior? Current { get; private set; }

        public BodyDriftBehavior()
        {
            Current = this;
            _lastEncounter = CampaignTime.Now;
        }

        public override void RegisterEvents()
        {
            try
            {
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.RegisterEvents", exception);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            try { dataStore.SyncData("PE_lastEncounter", ref _lastEncounter); }
            catch (Exception exception) { Guard.Report("BodyDrift.SyncData", exception); }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Current = this;

            // A save made before this mod arrived carries no timestamp, and a zero CampaignTime
            // reads as "hundreds of days ago", which would start every character wasting away.
            if (_lastEncounter.ElapsedDaysUntilNow > 3650f) _lastEncounter = CampaignTime.Now;
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent != null && mapEvent.IsPlayerMapEvent) _lastEncounter = CampaignTime.Now;
            }
            catch (Exception exception) { Guard.Report("BodyDrift.OnMapEventEnded", exception); }
        }

        /// <summary>Whether the player counts as having fought recently, on vanilla's own terms.</summary>
        internal bool FoughtRecently
        {
            get
            {
                try
                {
                    if (MapEvent.PlayerMapEvent != null) return true;
                    if (PlayerSiege.PlayerSiegeEvent != null) return true;
                    return _lastEncounter.ElapsedDaysUntilNow < 2f;
                }
                catch (Exception exception) { Guard.Report("BodyDrift.FoughtRecently", exception); return false; }
            }
        }
    }

    /// <summary>
    /// Has the last word on the daily build drift, after vanilla has had its say.
    /// </summary>
    /// <remarks>
    /// A postfix rather than a prefix returning false. Vanilla still runs, so any other mod
    /// patching the same tick still sees a normal call; its write to build is simply overwritten,
    /// which is safe because <c>Hero.Build</c> is a plain auto-property with no side effects.
    /// Replacing the value outright is what allows the wider range: vanilla clamps to thirty
    /// percent either side of character creation and would otherwise drag anything beyond that
    /// straight back on the following day.
    ///
    /// Weight is never written here, so vanilla's own handling of it stands untouched.
    /// </remarks>
    [HarmonyPatch]
    internal static class DynamicBodyDriftPatch
    {
        private static float _build;
        private static bool _captured;

        private static MethodBase? Target()
        {
            try { return AccessTools.Method(BodyDrift.Behavior, "DailyTick"); }
            catch (Exception exception) { Guard.Report("BodyDrift.Target", exception); return null; }
        }

        private static bool Prepare()
        {
            var found = Target() != null;
            if (!found) Log.Write("Body drift: DynamicBodyCampaignBehavior.DailyTick not found, drift patch off.");
            return found;
        }

        private static MethodBase TargetMethod() => Target()!;

        [HarmonyPrefix]
        private static void Before()
        {
            _captured = false;

            try
            {
                if (!Mod.On) return;

                var settings = Settings.Instance;
                if (settings == null || !settings.BodyDrift) return;

                var hero = Hero.MainHero;
                if (hero == null) return;

                _build = hero.Build;
                _captured = true;
            }
            catch (Exception exception)
            {
                _captured = false;
                Guard.Report("BodyDrift.Before", exception);
            }
        }

        [HarmonyPostfix]
        private static void After()
        {
            if (!_captured) return;
            _captured = false;

            try
            {
                Guard.Touch("BodyDrift.DailyTick");

                var behavior = BodyDriftBehavior.Current;
                var hero = Hero.MainHero;
                if (behavior == null || hero == null) return;

                var step = BodyDrift.VanillaBuildStep(behavior.FoughtRecently);

                // Training multiplies a day of building up, never a day of wasting away.
                if (step > 0f)
                {
                    var athletics = hero.GetSkillValue(DefaultSkills.Athletics);
                    var vigor = hero.GetAttributeValue(DefaultCharacterAttributes.Vigor);
                    step *= BodyDrift.TrainingMultiplier(athletics, vigor);
                }

                // Recomputed from the pre-tick value, so vanilla's narrower clamp never applies.
                hero.Build = BodyDrift.SettleBuild(_build, step);
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.After", exception);
            }
        }
    }
}
