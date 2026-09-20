using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// Tracks what the daily body drift depends on: town, battle, and a day's work at the forge.
    /// </summary>
    /// <remarks>
    /// The two timestamps vanilla's own behavior keeps are private to it. Rather than reach into
    /// them - which is the part most likely to break on a game update - this keeps its own copies
    /// off the same public events, so the daily step can be recomputed without touching vanilla's
    /// internals at all. They are saved, so a reload does not reset the reckoning.
    /// </remarks>
    internal sealed class BodyDriftBehavior : CampaignBehaviorBase
    {
        private CampaignTime _lastSettlementVisit;
        private CampaignTime _lastEncounter;
        private CampaignTime _lastForge;

        internal static BodyDriftBehavior? Current { get; private set; }

        public BodyDriftBehavior()
        {
            Current = this;
            _lastSettlementVisit = CampaignTime.Now;
            _lastEncounter = CampaignTime.Now;
            _lastForge = CampaignTime.Zero;
        }

        public override void RegisterEvents()
        {
            try
            {
                CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
                CampaignEvents.OnNewItemCraftedEvent.AddNonSerializedListener(this, OnItemCrafted);
                CampaignEvents.OnEquipmentSmeltedByHeroEvent.AddNonSerializedListener(this, OnSmelted);
                CampaignEvents.OnItemsRefinedEvent.AddNonSerializedListener(this, OnRefined);
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.RegisterEvents", exception);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("PE_lastSettlementVisit", ref _lastSettlementVisit);
                dataStore.SyncData("PE_lastEncounter", ref _lastEncounter);
                dataStore.SyncData("PE_lastForge", ref _lastForge);
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.SyncData", exception);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Current = this;

            // A save made before this mod arrived carries no timestamps, and a zero CampaignTime
            // reads as "hundreds of days ago", which would start every character wasting away.
            if (_lastSettlementVisit.ElapsedDaysUntilNow > 3650f) _lastSettlementVisit = CampaignTime.Now;
            if (_lastEncounter.ElapsedDaysUntilNow > 3650f) _lastEncounter = CampaignTime.Now;
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            try
            {
                if (party != null && party.IsMainParty) _lastSettlementVisit = CampaignTime.Now;
            }
            catch (Exception exception) { Guard.Report("BodyDrift.OnSettlementLeft", exception); }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent != null && mapEvent.IsPlayerMapEvent) _lastEncounter = CampaignTime.Now;
            }
            catch (Exception exception) { Guard.Report("BodyDrift.OnMapEventEnded", exception); }
        }

        /// <summary>Whether the player counts as having been in a settlement today.</summary>
        internal bool VisitedSettlement
        {
            get
            {
                try
                {
                    if (Hero.MainHero?.CurrentSettlement != null)
                    {
                        _lastSettlementVisit = CampaignTime.Now;
                        return true;
                    }
                    return _lastSettlementVisit.ElapsedDaysUntilNow < 1f;
                }
                catch (Exception exception) { Guard.Report("BodyDrift.VisitedSettlement", exception); return false; }
            }
        }

        /// <summary>Whether the player counts as having fought recently.</summary>
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

        /// <summary>
        /// Whether the player has done a day's work at the forge or in a fight.
        /// </summary>
        /// <remarks>
        /// The three crafting events cover every way stamina is actually spent - smithing,
        /// smelting and refining - which is a truer signal than watching the Smithing skill, since
        /// a level there can be weeks apart on this curve.
        /// </remarks>
        internal bool WorkedToday
        {
            get
            {
                try
                {
                    if (FoughtToday) return true;
                    return _lastForge != CampaignTime.Zero && _lastForge.ElapsedDaysUntilNow < 1f;
                }
                catch (Exception exception) { Guard.Report("BodyDrift.WorkedToday", exception); return false; }
            }
        }

        private bool FoughtToday
        {
            get
            {
                if (MapEvent.PlayerMapEvent != null) return true;
                if (PlayerSiege.PlayerSiegeEvent != null) return true;
                return _lastEncounter.ElapsedDaysUntilNow < 1f;
            }
        }

        private void OnItemCrafted(ItemObject item, ItemModifier modifier, bool fromOrder) => Forged();

        private void OnSmelted(Hero hero, EquipmentElement element)
        {
            if (hero == null || hero == Hero.MainHero) Forged();
        }

        private void OnRefined(Hero hero, Crafting.RefiningFormula formula)
        {
            if (hero == null || hero == Hero.MainHero) Forged();
        }

        private void Forged()
        {
            try { _lastForge = CampaignTime.Now; }
            catch (Exception exception) { Guard.Report("BodyDrift.Forged", exception); }
        }

        /// <summary>
        /// Vanilla's own weight anchor, or the current value when it is missing or unusable.
        /// </summary>
        internal static float WeightAnchor(float current)
        {
            try
            {
                var field = DynamicBodyDriftPatch.UnmodifiedWeight;
                var instance = DynamicBodyDriftPatch.VanillaInstance;
                if (field == null || instance == null) return current;

                var value = field.GetValue(instance);
                if (value is float anchor && !float.IsNaN(anchor) && anchor > 0f) return anchor;
                return current;
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.WeightAnchor", exception);
                return current;
            }
        }
    }

    /// <summary>
    /// Has the last word on the daily body drift, after vanilla has had its say.
    /// </summary>
    /// <remarks>
    /// A postfix rather than a prefix returning false. Vanilla still runs, so any other mod
    /// patching the same tick still sees a normal call; its write is simply overwritten, which is
    /// safe because <c>Hero.Build</c> and <c>Hero.Weight</c> are plain auto-properties with no side
    /// effects. Replacing the value outright is what allows the wider band: vanilla clamps to
    /// thirty percent either side of character creation and would otherwise drag anything beyond
    /// that straight back on the following day.
    /// </remarks>
    [HarmonyPatch]
    internal static class DynamicBodyDriftPatch
    {
        private static float _build;
        private static float _weight;
        private static bool _captured;

        internal static FieldInfo? UnmodifiedWeight { get; private set; }
        internal static object? VanillaInstance { get; private set; }

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
        private static void Before(object __instance)
        {
            _captured = false;

            try
            {
                if (!Mod.On) return;

                var settings = Settings.Instance;
                if (settings == null || !settings.BodyDrift) return;

                var hero = Hero.MainHero;
                if (hero == null) return;

                VanillaInstance = __instance;
                if (UnmodifiedWeight == null && __instance != null)
                    UnmodifiedWeight = AccessTools.Field(__instance.GetType(), "_unmodifiedWeight");

                _build = hero.Build;
                _weight = hero.Weight;
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

                var settings = Settings.Instance;
                var behavior = BodyDriftBehavior.Current;
                var hero = Hero.MainHero;
                if (settings == null || behavior == null || hero == null) return;

                var starving = false;
                var inSettlement = false;
                try
                {
                    starving = hero.PartyBelongedTo?.Party?.IsStarving ?? false;
                    inSettlement = hero.CurrentSettlement != null;
                }
                catch (Exception exception) { Guard.Report("BodyDrift.Conditions", exception); }

                var buildStep = BodyDrift.VanillaBuildStep(behavior.FoughtRecently);
                var weightStep = BodyDrift.VanillaWeightStep(behavior.VisitedSettlement, starving, inSettlement);

                // A day spent fighting or at the forge is a day you do not put weight on. Only the
                // gain is cancelled - losing weight on the road is vanilla's business and stays so.
                if (weightStep > 0f && behavior.WorkedToday) weightStep = 0f;

                // Training multiplies a day of building up, never a day of wasting away.
                if (buildStep > 0f)
                {
                    var athletics = hero.GetSkillValue(DefaultSkills.Athletics);
                    var vigor = hero.GetAttributeValue(DefaultCharacterAttributes.Vigor);
                    buildStep *= BodyDrift.TrainingMultiplier(athletics, vigor);
                }

                // Recomputed from the pre-tick values, so vanilla's narrower clamp never applies.
                hero.Build = BodyDrift.SettleBuild(_build, buildStep);
                hero.Weight = BodyDrift.Settle(_weight, weightStep,
                                               BodyDriftBehavior.WeightAnchor(_weight),
                                               BodyDrift.WeightSpread);
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.After", exception);
            }
        }
    }
}
