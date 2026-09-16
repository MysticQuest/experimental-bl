using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace MasteryCurve
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
            if (Settings.Instance == null || !Settings.Instance.EscalatingFocusCost) return;
            __result = Curve.FocusPointCost(__instance.GetFocus(skill) + 1);
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
        private static void Scale(SkillObject skill, ref float rawXp)
        {
            if (Settings.Instance == null || !Settings.Instance.PerSkillMultipliers) return;
            if (skill == null || rawXp <= 0f) return;
            rawXp *= Settings.Instance.MultiplierFor(skill);
        }
    }
}
