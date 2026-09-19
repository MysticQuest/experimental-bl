using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// What Tactics is worth to the man holding it, rather than to the troops he leads.
    /// </summary>
    /// <remarks>
    /// Vanilla gives Tactics exactly two personal effects: an auto-resolve advantage and a
    /// reduction in the men left behind when breaking off. The first is deliberately untouched -
    /// paying Tactics again there would only widen the gap between simulating a fight and leading
    /// one. The second is reshaped rather than duplicated. The three added here are things nothing
    /// in the game does: parties thinking better of attacking you, reading a stronghold's defences
    /// from a distance, and getting your own men back onto the field faster in a battle you fight
    /// rather than simulate - which is what finally gives Tactics something to say in a battle
    /// you lead yourself.
    /// </remarks>
    internal static class TacticsBonus
    {
        /// <summary>
        /// What the troops you leave behind breaking off a fight come down to at the cap.
        /// </summary>
        /// <remarks>
        /// Vanilla already has this as <c>TacticsTroopSacrificeReduction</c>, at a flat 0.1% a
        /// point reaching 33% at 330. It is not added to - it is replaced, so there is still
        /// exactly one rule, rising just as evenly but worth half your losses at the cap rather
        /// than a third.
        /// </remarks>
        internal const float LossesAtCap = 0.50f;

        /// <summary>How much of their appetite for attacking you is gone, as a share.</summary>
        internal static float Avoidance() => MasteryEffects.PlayerValue(MasteryEffects.Avoidance);

        /// <summary>How far an enemy stronghold can be read, in map units.</summary>
        internal static float IntelReach() => MasteryEffects.IntelReach();

        /// <summary>How much larger your reinforcement waves arrive, as a share.</summary>
        internal static float ReinforcementBonus() => MasteryEffects.PlayerValue(MasteryEffects.Reinforcements);

        internal static bool Active()
        {
            var settings = Settings.Instance;
            return Mod.On && settings != null && settings.SkillBonuses;
        }
    }

    /// <summary>
    /// Decides where each effect we are responsible for ends up at the cap.
    /// </summary>
    /// <remarks>
    /// Every consumer of a skill effect goes through <c>GetSkillEffectValue</c>, including the text
    /// the character screen prints, so reshaping here keeps the number and its description from
    /// ever disagreeing. Every one of these stays a straight line, like the rest of the game -
    /// only the height it reaches changes, and only for our own effects and vanilla's Tactics
    /// sacrifice reduction. The other thirty vanilla personal effects are untouched.
    /// </remarks>
    [HarmonyPatch(typeof(SkillEffect), nameof(SkillEffect.GetSkillEffectValue))]
    internal static class EffectCeilingPatch
    {
        [HarmonyPostfix]
        private static void Reshape(SkillEffect __instance, int skillLevel, ref float __result)
        {
            Guard.Touch("EffectValue");
            try
            {
                var shaped = MasteryEffects.Shaped(__instance, skillLevel);
                if (shaped.HasValue) __result = shaped.Value;
        
                        }
            catch (Exception exception)
            {
                Guard.Report("TacticsBonus.Reshape", exception);
            }
}
    }

    /// <summary>
    /// Parties sizing you up decide they have somewhere else to be.
    /// </summary>
    /// <remarks>
    /// This is the one place the AI puts a number on wanting to avoid a particular enemy against
    /// wanting to attack it, so it is where a reputation belongs. Both scores are nudged rather
    /// than overridden: a party that badly outguns you still comes, it just wants it less.
    /// </remarks>
    [HarmonyPatch(typeof(DefaultMobilePartyAIModel), "CalculateInitiativeScoresForEnemy")]
    internal static class EnemiesGiveYouRoomPatch
    {
        [HarmonyPostfix]
        private static void Weigh(MobileParty enemyParty, ref float avoidScore, ref float attackScore)
        {
            Guard.Touch("PartyAiScores");
            try
            {
                if (!TacticsBonus.Active() || !Mastery.IsPlayerSide(enemyParty)) return;

                var share = TacticsBonus.Avoidance();
                if (share <= 0f) return;

                avoidScore *= 1f + share;
                attackScore *= 1f - Math.Min(0.9f, share);
        
                        }
            catch (Exception exception)
            {
                Guard.Report("TacticsBonus.Weigh", exception);
            }
}
    }

    /// <summary>
    /// You read an enemy stronghold's garrison, militia and walls without having to walk in.
    /// </summary>
    /// <remarks>
    /// This is the gate the settlement tooltip and the encyclopedia both ask before they print a
    /// garrison. Vanilla opens it only for your own faction, for somewhere you are standing next
    /// to, or where you keep an emissary, a workshop or an alley. Tactics opens it by distance
    /// instead - nothing is revealed that a scout could not have brought back, it simply arrives
    /// without the scout.
    /// </remarks>
    [HarmonyPatch(typeof(DefaultInformationRestrictionModel),
        nameof(DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf), new[] { typeof(Settlement) })]
    internal static class KnowTheEnemyPatch
    {
        [HarmonyPostfix]
        private static void Reveal(Settlement settlement, ref bool __result)
        {
            Guard.Touch("SettlementIntel");
            try
            {
                if (__result || settlement == null || !TacticsBonus.Active()) return;

                var reach = TacticsBonus.IntelReach();
                if (reach <= 0f) return;

                var main = MobileParty.MainParty;
                if (main == null) return;

                var from = main.Position.ToVec2();
                var to = settlement.Party.Position.ToVec2();
                if (from.Distance(to) <= reach) __result = true;
        
                        }
            catch (Exception exception)
            {
                Guard.Report("TacticsBonus.Reveal", exception);
            }
}
    }

    /// <summary>
    /// Your fallen are replaced in larger waves, in a battle you fight rather than simulate.
    /// </summary>
    /// <remarks>
    /// The three batch sizings are private and which one runs depends on the mission's
    /// reinforcement method, so all three are patched with one postfix rather than guessing.
    /// The context knows whose side it is through <c>IsPlayerSide</c>, which is what keeps this
    /// off the enemy.
    ///
    /// This is the answer to Tactics being worth nothing in a battle you actually lead: it does
    /// not touch troop quality or the enemy's numbers, only how fast your own get back on the
    /// field, which is the one thing a commander's judgement plausibly buys.
    ///
    /// Applied by hand rather than through <c>PatchAll</c>: the targets are private and found by
    /// name, and a class whose <c>TargetMethods</c> comes back empty aborts the whole sweep,
    /// taking the progression patches down with it.
    /// </remarks>
    internal static class BiggerReinforcementsPatch
    {
        private static readonly Type? Context =
            AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionBattleSideSpawnContext");

        private static readonly MethodInfo? IsPlayerSide =
            Context == null ? null : AccessTools.PropertyGetter(Context, "IsPlayerSide");

        /// <summary>Hooks whichever batch sizings this build actually has.</summary>
        internal static int Apply(Harmony harmony)
        {
            if (Context == null || IsPlayerSide == null) return 0;

            var postfix = new HarmonyMethod(AccessTools.Method(typeof(BiggerReinforcementsPatch), nameof(Enlarge)));
            var hooked = 0;
            foreach (var name in new[] { "ComputeFixedBatch", "ComputeBalancedBatch", "ComputeWaveBatch" })
            {
                var method = AccessTools.Method(Context, name);
                if (method == null) continue;
                harmony.Patch(method, postfix: postfix);
                hooked++;
            }

            return hooked;
        }

        private static void Enlarge(object __instance, ref int __result)
        {
            if (__result <= 0 || IsPlayerSide == null || !TacticsBonus.Active()) return;

            try
            {
                if (!(IsPlayerSide.Invoke(__instance, null) is bool mine) || !mine) return;
            }
            catch
            {
                return;
            }

            var share = TacticsBonus.ReinforcementBonus();
            if (share <= 0f) return;

            __result = Math.Max(__result, (int)Math.Round(__result * (1f + share)));
        }
    }
}
