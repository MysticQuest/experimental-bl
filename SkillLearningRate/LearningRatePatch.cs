using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace SkillLearningRate
{
    /// <summary>
    /// Applies the configured multiplier to the learning rate that
    /// <see cref="DefaultCharacterDevelopmentModel"/> calculates.
    /// </summary>
    /// <remarks>
    /// This patches the vanilla model rather than replacing the registered
    /// <c>CharacterDevelopmentModel</c>, so it stacks with other mods that derive their
    /// own model from the vanilla one and call <c>base.CalculateLearningRate</c>.
    /// </remarks>
    [HarmonyPatch(typeof(DefaultCharacterDevelopmentModel), nameof(DefaultCharacterDevelopmentModel.CalculateLearningRate))]
    internal static class CalculateLearningRatePatch
    {
        private static readonly TextObject MultiplierText = new TextObject("{=SLR001}Skill Learning Rate");

        [HarmonyPostfix]
        private static void ApplyGlobalMultiplier(ref ExplainedNumber __result, bool includeDescriptions)
        {
            var settings = Settings.Instance;
            if (settings is null || !settings.Enabled)
            {
                return;
            }

            var multiplier = settings.GlobalLearningRateMultiplier;
            if (multiplier < 0f || Math.Abs(multiplier - 1f) < 0.0001f)
            {
                return;
            }

            // An ExplainedNumber resolves as BaseNumber * (1 + SumOfFactors), and AddFactor
            // adds into that one shared SumOfFactors -- it does not scale the result. The
            // vanilla learning rate already accumulates large factors (0.4 per attribute
            // point, 1.0 per focus point), so passing a bare `multiplier - 1` here gets
            // diluted to almost nothing: with attributes 6 / focus 3 the factors sum to
            // 5.4, and a 0.01x request would land at 0.85x of the original rate.
            //
            // To genuinely multiply the result, scale our contribution by the factors that
            // are already there:  Base * (1 + F + d) == m * Base * (1 + F)  =>  d = (m - 1) * (1 + F)
            var delta = (multiplier - 1f) * (1f + __result.SumOfFactors);

            __result.AddFactor(delta, includeDescriptions ? MultiplierText : null);
        }
    }
}
