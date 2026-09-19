using System;
using System.Collections.Generic;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ProgressionExpanded
{
    /// <summary>
    /// What the six attributes are worth on their own, beyond the skills they govern.
    /// </summary>
    /// <remarks>
    /// Vanilla attributes do exactly two things: raise the learning rate of their bound skills and
    /// raise those skills' ceilings. Both are invisible until much later, which makes spending a
    /// point feel like nothing happened. These give each attribute something that lands the moment
    /// it is bought, chosen so it fits what the attribute already governs.
    ///
    /// Every one of them is a straight line per point, like the rest of the game.
    /// </remarks>
    internal static class AttributeBonus
    {
        internal const int VigorHitPoints = 2;             // hit points
        internal const float ControlHandling = 0.01f;      // weapon handling
        internal const float ControlResistance = 0.01f;    // damage taken
        internal const float EnduranceMountSpeed = 0.01f;  // horse speed
        internal const float EnduranceRunSpeed = 0.01f;    // running speed
        internal const int EnduranceStamina = 5;           // smithing stamina
        internal const int EnduranceHitPoints = 1;         // hit points
        internal const float CunningBattleLoot = 0.02f;    // share of battle loot
        internal const int SocialPointsPerCompanion = 5;   // one companion per five points
        internal const float IntelligenceLearning = 0.01f; // learning rate, every skill
        internal const float IntelligenceCeiling = 1f;     // learning limit, every skill
        internal const float SocialClanLearning = 0.01f;   // skill XP for the rest of your clan

        internal static bool Active()
        {
            var settings = Settings.Instance;
            return Mod.On && settings != null && settings.AttributeBonuses;
        }

        /// <summary>An attribute's value for a hero, or zero when there is no hero to ask.</summary>
        internal static int Of(Hero? hero, CharacterAttribute? attribute)
        {
            try
            {
                if (hero == null || attribute == null) return 0;
                return Math.Max(0, hero.GetAttributeValue(attribute));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// What your Social is worth to someone else in your clan, as a multiplier on their skill XP.
        /// </summary>
        /// <remarks>
        /// Every other bonus here pays the hero who owns the attribute. This one pays everyone
        /// except them: companions, spouse and children learn faster because of your Social, and
        /// your own skills are untouched by it. It suits the attribute -- Social is the one that
        /// governs nothing you do alone -- and it pairs with the companion limit, which decides
        /// how many of them there are rather than how good they get.
        ///
        /// Player clan only. A clan-wide learning multiplier handed to every AI lord in Calradia
        /// would change the pace of the whole campaign, invisibly.
        /// </remarks>
        internal static float ClanLearningBonus(Hero? hero)
        {
            try
            {
                if (!Active() || hero == null || Campaign.Current == null) return 1f;

                var you = Hero.MainHero;
                if (you == null || hero == you || hero.Clan != you.Clan) return 1f;

                var social = Of(you, DefaultCharacterAttributes.Social);
                return social <= 0 ? 1f : 1f + SocialClanLearning * social;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>The hero behind an agent, if there is one.</summary>
        internal static Hero? HeroOf(Agent? agent)
        {
            try
            {
                return (agent?.Character as CharacterObject)?.HeroObject;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Vigor and Endurance both carry you further before you fall over.</summary>
    [HarmonyPatch(typeof(DefaultCharacterStatsModel), nameof(DefaultCharacterStatsModel.MaxHitpoints))]
    internal static class VigorHitPointsPatch
    {
        [HarmonyPostfix]
        private static void Toughen(CharacterObject character, ref ExplainedNumber __result)
        {
            Guard.Touch("MaxHitpoints");
            try
            {
                if (!AttributeBonus.Active()) return;

                var hero = character?.HeroObject;

                var vigor = AttributeBonus.Of(hero, DefaultCharacterAttributes.Vigor);
                if (vigor > 0)
                    __result.Add(vigor * AttributeBonus.VigorHitPoints, new TextObject("{=MCvig}Vigor"));

                var endurance = AttributeBonus.Of(hero, DefaultCharacterAttributes.Endurance);
                if (endurance > 0)
                    __result.Add(endurance * AttributeBonus.EnduranceHitPoints, new TextObject("{=MCend}Endurance"));
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AttributeBonus.Toughen", exception);
            }
}
    }

    /// <summary>
    /// Control steadies whatever is in your hands; Endurance moves you and your horse faster.
    /// </summary>
    /// <remarks>
    /// Handling is the one driven property that every weapon uses -- how quickly it readies, turns
    /// and recovers -- which is what makes it the honest home for a bonus described as global.
    /// A mount is its own agent, so the horse's share is applied when the mount comes past with
    /// its rider attached rather than to the rider's own properties.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.UpdateAgentStats))]
    internal static class AgentAttributePatch
    {
        [HarmonyPostfix]
        private static void Apply(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            Guard.Touch("AgentStats");
            if (agent == null || agentDrivenProperties == null || !AttributeBonus.Active()) return;

            try
            {
                if (agent.IsMount)
                {
                    var rider = AttributeBonus.HeroOf(agent.RiderAgent);
                    var endurance = AttributeBonus.Of(rider, DefaultCharacterAttributes.Endurance);
                    if (endurance > 0)
                        agentDrivenProperties.MaxSpeedMultiplier *= 1f + AttributeBonus.EnduranceMountSpeed * endurance;
                    return;
                }

                var hero = AttributeBonus.HeroOf(agent);
                if (hero == null) return;

                var control = AttributeBonus.Of(hero, DefaultCharacterAttributes.Control);
                if (control > 0)
                    agentDrivenProperties.HandlingMultiplier *= 1f + AttributeBonus.ControlHandling * control;

                var onFoot = AttributeBonus.Of(hero, DefaultCharacterAttributes.Endurance);
                if (onFoot > 0)
                    agentDrivenProperties.MaxSpeedMultiplier *= 1f + AttributeBonus.EnduranceRunSpeed * onFoot;
            }
            catch
            {
                // This runs every frame per agent; a throw here is far worse than a missing bonus.
            }
        }
    }

    /// <summary>Watches whether the player agent is built at all, and nothing else.</summary>
    /// <remarks>
    /// The character creation crash is a null <c>Mission.MainAgent</c> inside the body generator.
    /// Knowing whether the agent was ever constructed separates "the agent failed to spawn" from
    /// "the agent spawned and was torn down early", which are different faults with different
    /// causes. This only writes one line.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.InitializeAgentStats))]
    internal static class AgentSpawnProbePatch
    {
        [HarmonyPostfix]
        private static void Seen() => Guard.Touch("AgentInit");
    }

    /// <summary>
    /// Control also means taking a hit better than you otherwise would.
    /// </summary>
    /// <remarks>
    /// Hung on the amplification step rather than the reduction one purely because this is the
    /// hook already proven to run for every blow with both parties in hand; a multiplier applied
    /// here and one applied a step later come to the same number.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.ApplyDamageAmplifications))]
    internal static class ControlResistancePatch
    {
        /// <summary>However controlled you are, a blow always lands for something.</summary>
        private const float Floor = 0.5f;

        [HarmonyPostfix]
        private static void Absorb(ref AttackInformation attackInformation, ref float __result)
        {
            Guard.Touch("DamageTaken");
            if (__result <= 0f || !AttributeBonus.Active()) return;

            try
            {
                var victim = (attackInformation.VictimAgentCharacter as CharacterObject)?.HeroObject;
                var control = AttributeBonus.Of(victim, DefaultCharacterAttributes.Control);
                if (control <= 0) return;

                __result *= Math.Max(Floor, 1f - AttributeBonus.ControlResistance * control);
            }
            catch
            {
                // Never throw on a damage tick.
            }
        }
    }

    /// <summary>
    /// Cunning takes a larger share of what is left on the field.
    /// </summary>
    /// <remarks>
    /// This was Roguery's, at 0.25% a point reaching 82.5% at skill 330. It belongs to the
    /// attribute now, so vanilla's effect is zeroed rather than left to stack, and only the
    /// player's own entry in the result is touched.
    /// </remarks>
    [HarmonyPatch(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetLootItemChancesForWinnerParties))]
    internal static class CunningBattleLootPatch
    {
        [HarmonyPostfix]
        private static void Enrich(ref MBList<KeyValuePair<MapEventParty, float>> __result)
        {
            Guard.Touch("BattleLoot");
            if (!AttributeBonus.Active() || __result == null) return;

            try
            {
                if (Campaign.Current == null) return;

                var cunning = AttributeBonus.Of(Hero.MainHero, DefaultCharacterAttributes.Cunning);
                if (cunning <= 0) return;

                var share = AttributeBonus.CunningBattleLoot * cunning;
                var mine = PartyBase.MainParty;

                for (var i = 0; i < __result.Count; i++)
                {
                    if (__result[i].Key?.Party != mine) continue;
                    var raised = Math.Min(1f, __result[i].Value * (1f + share));
                    __result[i] = new KeyValuePair<MapEventParty, float>(__result[i].Key, raised);
                    return;
                }
            }
            catch (Exception exception)
            {
                Guard.Report("BattleLoot", exception);
            }
        }
    }

    /// <summary>Endurance keeps you at the anvil longer.</summary>
    [HarmonyPatch(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior.GetMaxHeroCraftingStamina))]
    internal static class EnduranceStaminaPatch
    {
        [HarmonyPostfix]
        private static void Extend(Hero hero, ref int __result)
        {
            Guard.Touch("CraftingStamina");
            try
            {
                if (!AttributeBonus.Active()) return;

                var endurance = AttributeBonus.Of(hero, DefaultCharacterAttributes.Endurance);
                if (endurance > 0) __result += endurance * AttributeBonus.EnduranceStamina;
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AttributeBonus.Extend", exception);
            }
}
    }

    /// <summary>Social keeps more people willing to follow you around.</summary>
    [HarmonyPatch(typeof(DefaultClanTierModel), nameof(DefaultClanTierModel.GetCompanionLimit))]
    internal static class SocialCompanionsPatch
    {
        [HarmonyPostfix]
        private static void Widen(Clan clan, ref int __result)
        {
            Guard.Touch("CompanionLimit");
            try
            {
                if (!AttributeBonus.Active() || clan?.Leader == null) return;

                var social = AttributeBonus.Of(clan.Leader, DefaultCharacterAttributes.Social);
                if (social > 0) __result += social / AttributeBonus.SocialPointsPerCompanion;
        
                        }
            catch (Exception exception)
            {
                Guard.Report("AttributeBonus.Widen", exception);
            }
}
    }
}
