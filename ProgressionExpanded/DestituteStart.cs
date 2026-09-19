using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ProgressionExpanded
{
    /// <summary>
    /// Takes away everything character creation just handed out, so a campaign starts at nothing.
    /// </summary>
    /// <remarks>
    /// The rest of this mod lengthens the climb. This sets where it starts from, which matters
    /// more than it sounds: vanilla opens with a horse, a decent weapon and a few hundred denars,
    /// which is most of the early game already solved.
    ///
    /// Hooked on the last stage of new-game creation, which is later than it sounds it needs to
    /// be. Character creation being "over" is not the end of the handouts: the starting purse and
    /// the first few days of food arrive after it, so stripping there left a thousand denars and
    /// a couple of grain behind. This fires once everything has been given out.
    ///
    /// New campaigns only. There is no way to un-start a campaign that has already run, and
    /// stripping a going concern on load would be a different and much crueller feature.
    /// </remarks>
    internal sealed class DestituteStartBehavior : CampaignBehaviorBase
    {
        /// <summary>Two body armour, and the poorest thing in the game that counts as clothed.</summary>
        private const string Cloth = "burlap_sack_dress";

        /// <summary>Worth two denars, and technically a ranged weapon.</summary>
        private const string Pebbles = "throwing_stone";

        /// <summary>The one thing kept back, so the first days are hard rather than fatal.</summary>
        private const string Grain = "grain";

        /// <summary>
        /// The feeblest Empire dagger and the feeblest Empire leg armour the game has loaded.
        /// </summary>
        /// <remarks>
        /// Picked by walking the item list rather than by naming ids. Tier is computed from an
        /// item's stats when the game loads it and is nowhere in the XML, so choosing by name
        /// meant guessing: the blade I had picked turned out to be tier 2. Sorting the real
        /// objects by tier cannot guess wrong, and it survives the game rebalancing its items.
        /// </remarks>
        private static ItemObject? Feeblest(ItemObject.ItemTypeEnum kind)
        {
            var all = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
            if (all == null) return null;

            ItemObject? best = null;
            foreach (var item in all)
            {
                if (item == null || item.ItemType != kind) continue;
                if (item.Culture?.StringId != "empire") continue;
                if (kind == ItemObject.ItemTypeEnum.OneHandedWeapon
                    && item.PrimaryWeapon?.WeaponClass != WeaponClass.Dagger) continue;

                if (best == null || item.Tier < best.Tier
                    || (item.Tier == best.Tier && item.Value < best.Value))
                    best = item;
            }

            return best;
        }

        /// <summary>
        /// Set when a new campaign strips, cleared by the sweep that follows it an hour later.
        /// </summary>
        /// <remarks>
        /// The belt to the braces. New-game creation is where the purse and the food arrive, but
        /// the opening is scripted for a while yet - the brother, the clan, the banner - and a
        /// handout hiding in any of those would land after the strip and never be seen again.
        /// So the window runs on the real-time tick instead: anything handed over goes back on
        /// the floor the same second it appears, which is the moment the world map comes up. The
        /// window closes at the first hourly tick, by which point the player could be earning.
        ///
        /// Saved, so it means "this campaign stripped and has not been swept". A campaign that
        /// predates the feature has it false and is never touched, which is the whole point.
        /// </remarks>
        private bool _sweepPending;

        /// <summary>The live behaviour, so the map-ready patch can reach it.</summary>
        internal static DestituteStartBehavior? Current { get; private set; }

        /// <summary>Whether the opening is still handing things out.</summary>
        internal bool WindowOpen => _sweepPending && Mod.On && (Settings.Instance?.StartWithNothing ?? false);

        /// <summary>
        /// Set once the world map has finished loading, and never saved.
        /// </summary>
        /// <remarks>
        /// This is what tells a skipped tutorial from a played one. Skipping runs the tutorial's
        /// own finish during campaign creation, before there is a map at all; playing it runs the
        /// same code long after. Campaign time cannot tell them apart - the tutorial barely
        /// advances the clock, which is why an hour-long window called a real tutorial a skip.
        /// </remarks>
        internal static bool MapReady { get; set; }

        /// <summary>
        /// True from the first moment of a campaign until the villagers pay for the tutorial.
        /// </summary>
        /// <remarks>
        /// Stripping once is not enough: something dresses the hero again after the tutorial ends,
        /// so the sack has to be kept on rather than merely put on. While this is set, the tick
        /// puts back anything that reappears, and gold is refused.
        /// </remarks>
        private bool _tutorialPending;

        public override void RegisterEvents()
        {
            Current = this;
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, Strip);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CloseWindow);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ProgressionExpanded_SweepPending", ref _sweepPending);
            dataStore.SyncData("ProgressionExpanded_TutorialPending", ref _tutorialPending);
        }

        /// <summary>Whether the sack still has to be kept on by force.</summary>
        internal bool Enforcing => _tutorialPending && Mod.On
                                   && (Settings.Instance?.StartWithNothing ?? false);

        /// <summary>Puts the sack back on if anything has dressed the hero since the last tick.</summary>
        private void Enforce()
        {
            if (!Enforcing) return;

            var hero = Hero.MainHero;
            var cloth = Item(Cloth);
            if (hero?.BattleEquipment == null || cloth == null) return;

            var body = hero.BattleEquipment[EquipmentIndex.Body].Item;
            var head = hero.BattleEquipment[EquipmentIndex.Head].Item;
            if (body == cloth && head == null) return;

            Rekit(false);
            Log.Write("Burlap sack: something dressed the hero again; the sack is back on");
        }

        /// <summary>Real time, so anything handed over lands back on the floor the same second.</summary>
        private void OnTick(float dt)
        {
            Sweep();
            Enforce();
        }

        /// <summary>An hour in, the scripted opening is done and the window closes for good.</summary>
        private void CloseWindow()
        {
            Sweep();
            _sweepPending = false;
        }

        /// <summary>
        /// Takes back whatever arrived after the strip. Safe to call as often as you like.
        /// </summary>
        internal void Sweep()
        {
            if (!_sweepPending) return;

            try
            {
                var hero = Hero.MainHero;
                if (hero == null || !Mod.On) return;

                var settings = Settings.Instance;
                if (settings == null || !settings.StartWithNothing) return;

                var taken = hero.Gold;
                if (taken <= 0 && !HasLoot()) return;

                if (taken > 0) hero.ChangeHeroGold(-taken);
                SweepInventory();

                if (taken > 0) Log.Write($"Burlap sack: {taken} gold arrived after the strip and was taken too");
            }
            catch (Exception exception)
            {
                Guard.Report("BurlapSweep", exception);
            }
        }

        private void Strip(CampaignGameStarter starter)
        {
            Guard.Touch("StartWithNothing");

            try
            {
                var settings = Settings.Instance;
                if (!Mod.On || settings == null || !settings.StartWithNothing) return;

                var hero = Hero.MainHero;
                if (hero == null) return;

                var cloth = MBObjectManager.Instance?.GetObject<ItemObject>(Cloth);
                var pebbles = MBObjectManager.Instance?.GetObject<ItemObject>(Pebbles);

                // Three sets, not two. The stealth set is the one that is easy to miss, because
                // nothing shows it until the first time you sneak into a town and find yourself
                // better dressed than you were an hour ago.
                Undress(hero.BattleEquipment, cloth, pebbles);
                Undress(hero.CivilianEquipment, cloth, null);
                Undress(hero.StealthEquipment, cloth, null);

                if (hero.Gold > 0) hero.ChangeHeroGold(-hero.Gold);

                // Last, so anything the equipment changes handed back is swept with the rest.
                SweepInventory();

                _sweepPending = true;
                _tutorialPending = true;
                Log.Write("Burlap sack: stripped all three equipment sets, gold and inventory");

                var message = new TextObject("{=MCpoor}A burlap sack and a handful of stones. Everything else is gone.");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
            catch (Exception exception)
            {
                Guard.Report("StartWithNothing", exception);
            }
        }

        /// <summary>Whether the packs hold anything but the grain.</summary>
        private static bool HasLoot()
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return false;

            foreach (var element in roster)
                if (element.EquipmentElement.Item?.StringId != Grain) return true;

            return false;
        }

        /// <summary>
        /// Empties the packs, keeping the grain.
        /// </summary>
        /// <remarks>
        /// Two days of food is the difference between a hard start and a character who is already
        /// starving before they reach the first town, which is punishing rather than lean.
        /// </remarks>
        private static void SweepInventory()
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return;

            var doomed = new List<ItemRosterElement>();
            foreach (var element in roster)
                if (element.EquipmentElement.Item?.StringId != Grain) doomed.Add(element);

            foreach (var element in doomed)
                roster.AddToCounts(element.EquipmentElement, -element.Amount);
        }

        /// <summary>
        /// Back to the sack and nothing else, for a tutorial that was never played.
        /// </summary>
        internal void Bare()
        {
            Rekit(false);
            _tutorialPending = false;
        }

        /// <summary>
        /// Strips the sets again and hands back a knife and a pair of shoes.
        /// </summary>
        internal void Rekit()
        {
            _tutorialPending = false;
            Rekit(true);
        }

        private void Rekit(bool earned)
        {
            var hero = Hero.MainHero;
            if (hero == null) return;

            var cloth = Item(Cloth);
            var pebbles = Item(Pebbles);
            var knife = Feeblest(ItemObject.ItemTypeEnum.OneHandedWeapon);
            var shoes = Feeblest(ItemObject.ItemTypeEnum.LegArmor);

            Undress(hero.BattleEquipment, cloth, pebbles);
            Undress(hero.CivilianEquipment, cloth, null);
            Undress(hero.StealthEquipment, cloth, null);

            if (earned)
                foreach (var set in new[] { hero.BattleEquipment, hero.CivilianEquipment, hero.StealthEquipment })
                {
                    if (set == null) continue;
                    if (shoes != null) set[EquipmentIndex.Leg] = new EquipmentElement(shoes);
                    if (knife != null) set[EquipmentIndex.Weapon1] = new EquipmentElement(knife);
                }

            SweepInventory();
            Log.Write(earned
                ? $"Burlap sack: villagers gave {knife?.Name} (tier {knife?.Tier}) and {shoes?.Name} (tier {shoes?.Tier})"
                : "Burlap sack: tutorial skipped, so the sack is all there is");
        }

        private static ItemObject? Item(string id) => MBObjectManager.Instance?.GetObject<ItemObject>(id);

        /// <summary>Empties every slot, then puts back the one thing decency requires.</summary>
        private static void Undress(Equipment set, ItemObject? cloth, ItemObject? pebbles)
        {
            if (set == null) return;

            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                set[slot] = default(EquipmentElement);

            if (cloth != null) set[EquipmentIndex.Body] = new EquipmentElement(cloth);
            if (pebbles != null) set[EquipmentIndex.Weapon0] = new EquipmentElement(pebbles);
        }
    }

    /// <summary>
    /// Sweeps the purse the moment the map has loaded, before it is drawn.
    /// </summary>
    /// <remarks>
    /// The real-time tick already catches a late handout, but only once the map is running -
    /// which means a frame or two where a thousand denars is sitting there in plain sight. This
    /// runs at the end of the map's own loading, so the first world screen a player ever sees
    /// already reads zero.
    /// </remarks>
    [HarmonyPatch(typeof(MapState), nameof(MapState.OnLoadingFinished))]
    internal static class MapReadyPatch
    {
        [HarmonyPostfix]
        private static void Sweep()
        {
            Guard.Touch("MapReady");

            try
            {
                DestituteStartBehavior.MapReady = true;
                DestituteStartBehavior.Current?.Sweep();
            }
            catch (Exception exception)
            {
                Guard.Report("MapReady", exception);
            }
        }
    }

    /// <summary>
    /// Cuts the kit the tutorial hands over at its end down to a knife and a pair of shoes.
    /// </summary>
    /// <remarks>
    /// Finishing the tutorial refills both equipment sets outright - the game calls FillFrom on
    /// each of them - which puts a full set of gear on a character who was meant to own a sack.
    /// It is the single largest handout in the opening and it undoes the burlap start on its own.
    ///
    /// Rather than block the reward, this lets it happen and then trims it, so nothing the
    /// tutorial expects to have done is left half finished. What survives is the plainest blade
    /// in the game and the second-worst shoes, which is enough to stop being a threat to nobody
    /// and not enough to skip the early game.
    /// </remarks>
    [HarmonyPatch]
    internal static class TutorialRewardPatch
    {
        private const string Behaviour =
            "StoryMode.GameComponents.CampaignBehaviors.TutorialPhaseCampaignBehavior";

        /// <summary>
        /// Found by name, because StoryMode is not referenced.
        /// </summary>
        /// <remarks>
        /// Adding a reference for one method would tie the whole mod to the campaign's story
        /// assembly, and a player running without it would get a load failure rather than a
        /// missing trim. This way the patch quietly does not apply instead.
        /// </remarks>
        private static bool Prepare() => Target() != null;

        private static MethodBase? Target() =>
            AccessTools.Method(AccessTools.TypeByName(Behaviour), "FinalizeTutorialPhase");

        private static MethodBase TargetMethod() => Target()!;

        [HarmonyPostfix]
        private static void Trim()
        {
            Guard.Touch("TutorialReward");

            try
            {
                var settings = Settings.Instance;
                if (!Mod.On || settings == null || !settings.StartWithNothing) return;

                // The same method restores the player's own gear whether the tutorial was
                // played or skipped. Skipping runs it during campaign creation, before the map
                // exists; playing it runs the same code once the map has long been up.
                var behaviour = DestituteStartBehavior.Current;
                if (behaviour == null) return;

                if (DestituteStartBehavior.MapReady) behaviour.Rekit();
                else behaviour.Bare();
            }
            catch (Exception exception)
            {
                Guard.Report("TutorialReward", exception);
            }
        }
    }

    /// <summary>
    /// Refuses gold outright while the opening is still handing things out.
    /// </summary>
    /// <remarks>
    /// Taking the purse back a moment later worked, but the player saw it: a thousand denars sat
    /// on the world map for as long as it took the next sweep to run. Every gold change routes
    /// through here, so a handout during the window is simply declined and the number never
    /// moves off zero.
    ///
    /// Only while the window is open, which closes at the first hourly tick. After that the
    /// player can actually earn money, and refusing it would be a different feature entirely.
    /// </remarks>
    [HarmonyPatch(typeof(Hero), nameof(Hero.ChangeHeroGold))]
    internal static class RefuseGoldPatch
    {
        [HarmonyPrefix]
        private static void Decline(Hero __instance, ref int changeAmount)
        {
            Guard.Touch("RefuseGold");

            try
            {
                if (changeAmount <= 0 || __instance != Hero.MainHero) return;
                if (DestituteStartBehavior.Current?.Enforcing != true) return;

                Log.Write($"Burlap sack: declined {changeAmount} gold before it arrived");
                changeAmount = 0;
            }
            catch (Exception exception)
            {
                Guard.Report("RefuseGold", exception);
            }
        }
    }
}
