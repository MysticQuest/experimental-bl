using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// Focus points get dearer inside a skill. Vanilla hardcodes this to 1, and both
    /// <c>CanAddFocusToSkill</c> and <c>AddFocus</c> route through it, so a postfix is enough.
    /// </summary>
    [HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.GetRequiredFocusPointsToAddFocus))]
    internal static class EscalatingFocusCostPatch
    {
        [HarmonyPostfix]
        private static void Escalate(HeroDeveloper __instance, SkillObject skill, ref int __result)
        {
            Guard.Touch("FocusCost");
            try
            {
                if (!Mod.On || Settings.Instance == null || !Settings.Instance.EscalatingFocusCost) return;
                __result = Curve.FocusPointCost(__instance.GetFocus(skill) + 1);
        
                        }
            catch (Exception exception)
            {
                Guard.Report("Patches.Escalate", exception);
            }
}
    }

    /// <summary>
    /// Keeps the unspent focus balance from going negative.
    /// </summary>
    /// <remarks>
    /// <c>AddFocus</c> subtracts the required cost without checking it can be paid. Vanilla's cost
    /// is always 1, so the balance walks down to zero and stops; with escalating costs a hero
    /// holding one point can be charged two and end up at minus one. Character creation hands out
    /// and spends focus through this path, and a negative balance there is a number the rest of
    /// the game never expects to see.
    ///
    /// Guarding <c>CanAddFocusToSkill</c> covers the callers that ask first. This covers the ones
    /// that do not, and costs the player nothing they had actually earned.
    /// </remarks>
    [HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.AddFocus))]
    internal static class NonNegativeFocusPatch
    {
        [HarmonyPostfix]
        private static void Clamp(HeroDeveloper __instance)
        {
            Guard.Touch("AddFocus");
            if (!Mod.On || __instance == null) return;

            try
            {
                if (__instance.UnspentFocusPoints < 0)
                {
                    Log.Write($"Unspent focus went to {__instance.UnspentFocusPoints}; clamped to 0");
                    __instance.UnspentFocusPoints = 0;
                }
            }
            catch (Exception exception)
            {
                Guard.Report("AddFocus", exception);
            }
        }
    }

    /// <summary>
    /// Stops a hero being offered a focus point they cannot pay for.
    /// </summary>
    /// <remarks>
    /// <c>HeroDeveloper.DistributeUnspentFocusPoints</c> loops while the hero has points left,
    /// asking the model for the next skill and calling <c>AddFocus</c>. <c>AddFocus</c> subtracts
    /// the required cost unconditionally, and vanilla's cost is always 1, so the count always
    /// falls and the loop always ends. Escalating costs break that assumption: a hero holding one
    /// point whose best skill costs two has the balance driven negative instead.
    ///
    /// Vanilla picks the next skill from those <c>CanAddFocusToSkill</c> allows, so requiring the
    /// hero to actually afford it is what makes the model return null when nothing is affordable
    /// -- which is the loop's only clean exit, and keeps the balance at zero rather than below it.
    /// </remarks>
    [HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.CanAddFocusToSkill))]
    internal static class AffordableFocusPatch
    {
        [HarmonyPostfix]
        private static void RequireAffordable(HeroDeveloper __instance, SkillObject skill, ref bool __result)
        {
            Guard.Touch("CanAddFocus");
            if (!__result || __instance == null || skill == null) return;

            try
            {
                var settings = Settings.Instance;
                if (settings == null || !settings.Enabled || !settings.EscalatingFocusCost) return;

                var cost = Curve.FocusPointCost(__instance.GetFocus(skill) + 1);
                if (cost > __instance.UnspentFocusPoints) __result = false;
            }
            catch (Exception exception)
            {
                Guard.Report("CanAddFocus", exception);
            }
        }
    }

    /// <summary>
    /// Makes the character screen quote the cost of the point you are about to buy.
    /// </summary>
    /// <remarks>
    /// The screen lets you spend focus without committing, so <c>SkillVM.CurrentFocusLevel</c> runs
    /// ahead of what the hero actually has. Vanilla's cost lookup asks the hero, which is always 1
    /// there and so never disagreed. With an escalating cost it disagrees constantly: the tooltip
    /// quotes the price of a point you already bought. Reading the pending level instead is what
    /// stops the number needing an add-and-undo to catch up.
    /// </remarks>
    [HarmonyPatch(typeof(CharacterDeveloperHeroItemVM),
        nameof(CharacterDeveloperHeroItemVM.GetRequiredFocusPointsToAddFocusWithCurrentFocus))]
    internal static class PendingFocusCostPatch
    {
        [HarmonyPostfix]
        private static void UsePendingFocus(CharacterDeveloperHeroItemVM __instance, SkillObject skill, ref int __result)
        {
            Guard.Touch("PendingFocusCost");
            try
            {
                var settings = Settings.Instance;
                if (!Mod.On || settings == null || !settings.EscalatingFocusCost || skill == null || __instance?.Skills == null) return;

                foreach (var item in __instance.Skills)
                {
                    if (item?.Skill != skill) continue;
                    __result = Curve.FocusPointCost(item.CurrentFocusLevel + 1);
                    return;
                }
        
                        }
            catch (Exception exception)
            {
                Guard.Report("Patches.UsePendingFocus", exception);
            }
}
    }

    /// <summary>
    /// Greys out the focus button when the point on offer costs more than you hold.
    /// </summary>
    /// <remarks>
    /// The button reads <c>SkillVM.CanAddFocus</c>, which comes straight from this predicate, and
    /// vanilla asks only whether the balance is above zero -- correct when every point costs one.
    /// The cost is fetched a few lines earlier in the same refresh, but only to write the hint
    /// text, so with escalating costs the screen would explain that you cannot afford the point
    /// and leave the button live next to it, and a hero holding one point could buy a point
    /// costing two.
    ///
    /// The argument is the pending focus level, the one the screen is showing rather than the one
    /// the hero has committed, so the cost quoted here is the cost of the point actually on offer.
    /// </remarks>
    [HarmonyPatch(typeof(CharacterDeveloperHeroItemVM),
        nameof(CharacterDeveloperHeroItemVM.CanAddFocusToSkillWithFocusAmount))]
    internal static class AffordableFocusButtonPatch
    {
        [HarmonyPostfix]
        private static void RequireAffordable(CharacterDeveloperHeroItemVM __instance, int currentFocusAmount,
            ref bool __result)
        {
            Guard.Touch("FocusButton");
            if (!__result || __instance == null) return;

            try
            {
                var settings = Settings.Instance;
                if (!Mod.On || settings == null || !settings.EscalatingFocusCost) return;

                if (Curve.FocusPointCost(currentFocusAmount + 1) > __instance.UnspentCharacterPoints)
                    __result = false;
            }
            catch (Exception exception)
            {
                Guard.Report("FocusButton", exception);
            }
        }
    }

    /// <summary>
    /// Some skills are paid far less often than others -- Engineering only earns during a siege,
    /// Trade only against your own profit -- so their awards are scaled before anything else.
    /// </summary>
    [HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.AddSkillXp))]
    internal static class PerSkillMultiplierPatch
    {
        [HarmonyPrefix]
        private static void Scale(HeroDeveloper __instance, SkillObject skill, ref float rawXp)
        {
            Guard.Touch("SkillXpMultiplier");
            try
            {
                var settings = Settings.Instance;
                if (!Mod.On || settings == null || skill == null || rawXp <= 0f) return;
                if (DebugTools.GrantingLevels) return;
                rawXp *= settings.MultiplierFor(skill) * Legacy.LearningBonus(__instance?.Hero);
        
                        }
            catch (Exception exception)
            {
                Guard.Report("Patches.Scale", exception);
            }
}
    }

    /// <summary>
    /// Lifts a later generation's ceiling, not just their pace.
    /// </summary>
    /// <remarks>
    /// The model never receives the hero, so the limit cannot be raised there. This is the one
    /// place with both the hero and the skill in hand, and it is what actually gates XP gain.
    /// The number the character screen prints is still the unmodified one -- the skill really does
    /// climb past where the screen implies it should stop.
    /// </remarks>
    [HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.GetFocusFactor))]
    internal static class GenerationCeilingPatch
    {
        [HarmonyPostfix]
        private static void RaiseCeiling(HeroDeveloper __instance, SkillObject skill, ref float __result)
        {
            Guard.Touch("FocusFactor");
            try
            {
                var hero = __instance?.Hero;
                var extra = Legacy.CeilingBonus(hero);
                if (extra <= 0f || skill == null || hero == null) return;

                var attributes = skill.Attributes;
                if (attributes == null || attributes.Length == 0) return;

                var total = 0f;
                foreach (var attribute in attributes) total += hero.GetAttributeValue(attribute);
                var average = total / attributes.Length;

                var settings = Settings.Instance;
                if (settings == null || __instance == null) return;

                __result = Curve.RateFor(average, __instance.GetFocus(skill), hero.GetSkillValue(skill),
                                         settings.PenaltySlope, extra);
        
                        }
            catch (Exception exception)
            {
                Guard.Report("Patches.RaiseCeiling", exception);
            }
}
    }

    /// <summary>Hands an heir their generational head start the day they come of age.</summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.EducationCampaignBehavior), "OnHeroComesOfAge")]
    internal static class ComingOfAgePatch
    {
        [HarmonyPostfix]
        private static void Grant(Hero? hero) => Legacy.GrantComingOfAge(hero);
    }
}
