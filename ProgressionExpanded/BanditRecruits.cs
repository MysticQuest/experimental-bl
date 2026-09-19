using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ProgressionExpanded
{
    /// <summary>
    /// Roguery: a bandit party sometimes decides it would rather work for you.
    /// </summary>
    /// <remarks>
    /// Off unless asked for. Everything else in this mod adjusts a number the game was going to
    /// calculate anyway; this one destroys a party and finishes a <c>PlayerEncounter</c> from
    /// inside a postfix on that encounter starting, which is a far larger claim on the game's own
    /// state machine. It is the first thing to switch off if a campaign stops staying up.
    ///
    /// Hooked on <c>Start</c> rather than on the battle beginning, because at that moment the
    /// encounter exists but no map event does, so there is nothing half-built to unwind.
    /// </remarks>
    [HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Start))]
    internal static class BanditRecruitsPatch
    {
        [HarmonyPostfix]
        private static void Persuade()
        {
            Guard.Touch("BanditJoin");

            try
            {
                var settings = Settings.Instance;
                if (settings == null || !settings.BanditsMayJoin) return;
                if (!AlleyBonus.Active() || Campaign.Current == null) return;

                var chance = MasteryEffects.PlayerValue(MasteryEffects.BanditJoin);
                if (chance <= 0f) return;

                var them = Eligible();
                if (them == null) return;

                if (MBRandom.RandomFloat >= chance) return;

                Enlist(them);
            }
            catch (Exception exception)
            {
                Guard.Report("BanditJoin", exception);
            }
        }

        /// <summary>The encountered party, when it is one this can apply to at all.</summary>
        private static MobileParty? Eligible()
        {
            if (PlayerEncounter.EncounterSettlement != null) return null;

            var them = PlayerEncounter.EncounteredMobileParty;
            if (them == null || !them.IsBandit || them.IsGarrison) return null;
            if (them.MapEvent != null || them.SiegeEvent != null || them.Army != null) return null;
            if (them.MemberRoster == null || them.MemberRoster.TotalManCount <= 0) return null;

            var main = MobileParty.MainParty;
            if (main == null || main.MapEvent != null) return null;

            return them;
        }

        /// <summary>Takes in whoever fits, and lets the rest scatter.</summary>
        private static void Enlist(MobileParty them)
        {
            var main = MobileParty.MainParty;
            var room = main.Party.PartySizeLimit - main.MemberRoster.TotalManCount;
            if (room <= 0) return;

            var taken = 0;
            var roster = them.MemberRoster.GetTroopRoster();
            for (var i = roster.Count - 1; i >= 0 && room > 0; i--)
            {
                var element = roster[i];
                if (element.Character == null || element.Character.IsHero) continue;

                var healthy = element.Number - element.WoundedNumber;
                if (healthy <= 0) continue;

                var count = Math.Min(healthy, room);
                main.MemberRoster.AddToCounts(element.Character, count);
                them.MemberRoster.AddToCounts(element.Character, -count);
                room -= count;
                taken += count;
            }

            if (taken <= 0) return;

            DestroyPartyAction.Apply(PartyBase.MainParty, them);
            PlayerEncounter.Finish(true);

            var message = new TextObject("{=MCband}{COUNT} bandits decide they would rather ride with you.");
            message.SetTextVariable("COUNT", taken);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
        }
    }
}
