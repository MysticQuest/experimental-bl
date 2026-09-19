using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
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

        public override void RegisterEvents() =>
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, Strip);

        public override void SyncData(IDataStore dataStore) { }

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
                // The grain stays: two days of food is the difference between a hard start and a
                // character who is already starving before the first town.
                var roster = MobileParty.MainParty?.ItemRoster;
                if (roster != null)
                {
                    var doomed = new List<ItemRosterElement>();
                    foreach (var element in roster)
                        if (element.EquipmentElement.Item?.StringId != Grain) doomed.Add(element);

                    foreach (var element in doomed)
                        roster.AddToCounts(element.EquipmentElement, -element.Amount);
                }

                Log.Write("Burlap sack: stripped all three equipment sets, gold and inventory");

                var message = new TextObject("{=MCpoor}A burlap sack and a handful of stones. Everything else is gone.");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
            catch (Exception exception)
            {
                Guard.Report("StartWithNothing", exception);
            }
        }

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
}
