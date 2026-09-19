using System;
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
    /// Hooked on character creation being over rather than on the campaign starting, because the
    /// backstory choices hand out their gear and gold during creation, and anything stripped
    /// before that is simply given back afterwards.
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

        public override void RegisterEvents() =>
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, Strip);

        public override void SyncData(IDataStore dataStore) { }

        private void Strip()
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

                Undress(hero.BattleEquipment, cloth, pebbles);
                Undress(hero.CivilianEquipment, cloth, null);

                if (hero.Gold > 0) hero.ChangeHeroGold(-hero.Gold);

                var party = MobileParty.MainParty;
                if (party != null)
                {
                    // Food included. Starting with nothing means starting hungry, and the first
                    // thing a player does is go and fix that.
                    party.ItemRoster?.Clear();
                }

                Log.Write("Start with nothing: stripped both equipment sets, gold and inventory");

                var message = new TextObject("{=MCpoor}You begin with the clothes you stand in and a handful of stones.");
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
