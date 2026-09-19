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
        internal const float VigorMomentum = 0.01f;        // swing carried through a hit
        internal const float VigorKnockback = 0.01f;       // knockback, as a share of the base
        internal const float ControlHandling = 0.01f;      // weapon handling
        internal const float ControlStagger = 0.01f;       // damage needed to stagger you
        internal const float ControlGuard = 0.01f;         // recovery after a block, both ways
        internal const float ControlFooting = 0.01f;       // knocked back, down or out of the saddle
        internal const float EnduranceMountSpeed = 0.01f;  // horse speed
        internal const float EnduranceRunSpeed = 0.01f;    // running speed
        internal const int EnduranceStamina = 5;           // smithing stamina
        internal const float EnduranceResistance = 0.01f;  // damage taken

        /// <summary>
        /// Vanilla's own floor for how hard a man is to knock back, before Athletics is counted.
        /// </summary>
        /// <remarks>
        /// Knockback is decided by weighing a blow against the victim's resistance less the
        /// attacker's penetration, and that penetration is zero for everyone without the polearm
        /// perk that grants it. A percentage of zero is zero, so Vigor's share is a percentage of
        /// this instead: +1% a point means a tenth off a typical man's footing at Vigor 10.
        /// </remarks>
        internal const float BaseKnockBackResistance = 0.15f;
        internal const float CunningBattleLoot = 0.02f;    // share of battle loot
        internal const float CunningCheatDeath = 0.02f;    // share of lethal blows survived
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

    /// <summary>Vigor is what there is of you to cut through.</summary>
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

    /// <summary>
    /// Control is also what keeps you on your feet when something lands.
    /// </summary>
    /// <remarks>
    /// The model returns the damage a blow must do to stagger the defender, so raising it is
    /// resistance: the same hit that used to interrupt your swing no longer does. It is the
    /// defender-side counterpart to the handling bonus -- one keeps the weapon steady between
    /// swings, the other keeps it steady through one.
    ///
    /// The <c>in</c> parameter is declared <c>ref</c> because that is the shape Harmony matches.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage))]
    internal static class ControlStaggerPatch
    {
        [HarmonyPostfix]
        private static void Steady(Agent defenderAgent, ref float __result)
        {
            Guard.Touch("StaggerThreshold");

            try
            {
                if (!AttributeBonus.Active() || defenderAgent == null) return;

                var hero = AttributeBonus.HeroOf(defenderAgent);
                var control = AttributeBonus.Of(hero, DefaultCharacterAttributes.Control);
                if (control > 0) __result *= 1f + AttributeBonus.ControlStagger * control;
            }
            catch
            {
                // Runs for every blow landed in a battle; a missing bonus beats a thrown exception.
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
    /// Endurance is what lets you take a hit and keep going.
    /// </summary>
    /// <remarks>
    /// Hung on the amplification step rather than the reduction one purely because this is the
    /// hook already proven to run for every blow with both parties in hand; a multiplier applied
    /// here and one applied a step later come to the same number.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.ApplyDamageAmplifications))]
    internal static class EnduranceResistancePatch
    {
        /// <summary>However tough you are, a blow always lands for something.</summary>
        private const float Floor = 0.5f;

        [HarmonyPostfix]
        private static void Absorb(ref AttackInformation attackInformation, ref float __result)
        {
            Guard.Touch("DamageTaken");
            if (__result <= 0f || !AttributeBonus.Active()) return;

            try
            {
                var victim = (attackInformation.VictimAgentCharacter as CharacterObject)?.HeroObject;
                var endurance = AttributeBonus.Of(victim, DefaultCharacterAttributes.Endurance);
                if (endurance <= 0) return;

                __result *= Math.Max(Floor, 1f - AttributeBonus.EnduranceResistance * endurance);
            }
            catch
            {
                // Never throw on a damage tick.
            }
        }
    }

    /// <summary>
    /// Control is how fast your guard comes back after you stop something.
    /// </summary>
    /// <remarks>
    /// A blocked blow freezes both men for a moment. The defender's share of that is the half
    /// second that gets people killed -- the block held, and the counter was too late anyway.
    /// Control shortens yours and lengthens theirs, so stopping a blow starts to be worth
    /// something rather than merely not costing you anything.
    ///
    /// Both sides are read from the defender's Control, because both are the same act: it is
    /// their guard that absorbed the blow and their weapon that recovers from it. Every weapon
    /// qualifies, shield or not, which is what the handling and stagger bonuses cannot claim.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.CalculateDefendedBlowStunMultipliers))]
    internal static class ControlGuardPatch
    {
        [HarmonyPostfix]
        private static void Recover(Agent defenderAgent, ref float attackerStunPeriod, ref float defenderStunPeriod)
        {
            Guard.Touch("BlockRecovery");

            try
            {
                if (!AttributeBonus.Active() || defenderAgent == null) return;

                var control = AttributeBonus.Of(AttributeBonus.HeroOf(defenderAgent), DefaultCharacterAttributes.Control);
                if (control <= 0) return;

                var share = AttributeBonus.ControlGuard * control;
                defenderStunPeriod *= Math.Max(0.5f, 1f - share);
                attackerStunPeriod *= 1f + share;
            }
            catch
            {
                // Never throw mid-blow.
            }
        }
    }

    /// <summary>
    /// Control is also what keeps you where you were standing, or sitting.
    /// </summary>
    /// <remarks>
    /// The three resistances are one idea in three places: how hard you are to knock back, to put
    /// on the ground, and to take out of the saddle. Vanilla builds each from a flat base and the
    /// victim's Athletics; Control scales the finished figure, so it compounds with Athletics
    /// rather than replacing what that skill was already worth.
    ///
    /// This is the defensive half of what Vigor now does going the other way.
    /// </remarks>
    [HarmonyPatch]
    internal static class ControlFootingPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel),
            nameof(SandboxAgentStatCalculateModel.GetKnockBackResistance))]
        private static void StayUp(Agent agent, ref float __result) => Steady(agent, ref __result);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel),
            nameof(SandboxAgentStatCalculateModel.GetKnockDownResistance))]
        private static void StayStanding(Agent agent, ref float __result) => Steady(agent, ref __result);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel),
            nameof(SandboxAgentStatCalculateModel.GetDismountResistance))]
        private static void StaySeated(Agent agent, ref float __result) => Steady(agent, ref __result);

        private static void Steady(Agent agent, ref float result)
        {
            Guard.Touch("Footing");

            try
            {
                if (!AttributeBonus.Active() || agent == null || result <= 0f) return;

                var control = AttributeBonus.Of(AttributeBonus.HeroOf(agent), DefaultCharacterAttributes.Control);
                if (control > 0) result *= 1f + AttributeBonus.ControlFooting * control;
            }
            catch
            {
                // Read for every blow that might move someone; a missing bonus beats a throw.
            }
        }
    }

    /// <summary>
    /// Vigor drives the swing on through the man it just hit.
    /// </summary>
    /// <remarks>
    /// Momentum is what is left of a blow after it lands, and it decides whether the same swing
    /// reaches the next man. It is continuous -- no threshold, no roll -- so it scales smoothly
    /// instead of switching on, which is what makes it the honest way to spend strength.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.CalculateRemainingMomentum))]
    internal static class VigorMomentumPatch
    {
        [HarmonyPostfix]
        private static void Carry(Agent attacker, ref float __result)
        {
            Guard.Touch("Momentum");

            try
            {
                if (__result <= 0f || !AttributeBonus.Active() || attacker == null) return;

                var vigor = AttributeBonus.Of(AttributeBonus.HeroOf(attacker), DefaultCharacterAttributes.Vigor);
                if (vigor > 0) __result *= 1f + AttributeBonus.VigorMomentum * vigor;
            }
            catch
            {
                // Never throw mid-blow.
            }
        }
    }

    /// <summary>
    /// Vigor also means the man you hit gives ground.
    /// </summary>
    /// <remarks>
    /// Knockback is weighed against the victim's resistance, which starts at 0.15 and climbs with
    /// their Athletics; the attacker's side of the sum is a penetration figure vanilla leaves at
    /// zero. Vigor adds to it, scaled against that base so the number means what the card says.
    ///
    /// Knockback rather than knockdown, deliberately. Knockback costs the victim a step and their
    /// next swing; knockdown usually costs them the fight.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.GetKnockBackPenetration))]
    internal static class VigorKnockbackPatch
    {
        [HarmonyPostfix]
        private static void Drive(Agent attackerAgent, ref float __result)
        {
            Guard.Touch("KnockBack");

            try
            {
                if (!AttributeBonus.Active() || attackerAgent == null) return;

                var vigor = AttributeBonus.Of(AttributeBonus.HeroOf(attackerAgent), DefaultCharacterAttributes.Vigor);
                if (vigor > 0)
                    __result += AttributeBonus.VigorKnockback * vigor * AttributeBonus.BaseKnockBackResistance;
            }
            catch
            {
                // Never throw mid-blow.
            }
        }
    }

    /// <summary>
    /// Cunning is also knowing when to lie very still.
    /// </summary>
    /// <remarks>
    /// The game decides a downed hero's fate with a survival roll. This does not raise that
    /// chance directly -- it takes a share of what is left of it, so the figure on the card means
    /// what it says: at Cunning 10, one lethal blow in five turns out not to have been. A hero
    /// already certain to live gains nothing, and the roll can never be pushed past certainty.
    ///
    /// You only. Handing it to every lord in Calradia would quietly stop the world's nobility
    /// dying, which is a change to the whole campaign rather than a bonus on your card.
    /// </remarks>
    [HarmonyPatch(typeof(DefaultPartyHealingModel), nameof(DefaultPartyHealingModel.GetSurvivalChance))]
    internal static class CunningCheatDeathPatch
    {
        [HarmonyPostfix]
        private static void Duck(CharacterObject character, ref float __result)
        {
            Guard.Touch("CheatDeath");

            try
            {
                if (!AttributeBonus.Active() || __result >= 1f) return;

                var hero = character?.HeroObject;
                if (hero == null || hero != Hero.MainHero) return;

                var cunning = AttributeBonus.Of(hero, DefaultCharacterAttributes.Cunning);
                if (cunning <= 0) return;

                var saved = AttributeBonus.CunningCheatDeath * cunning;
                __result += (1f - __result) * Math.Min(1f, saved);
            }
            catch (Exception exception)
            {
                Guard.Report("CheatDeath", exception);
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
