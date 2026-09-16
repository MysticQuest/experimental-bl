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
            if (multiplier <= 0f || Math.Abs(multiplier - 1f) < 0.0001f)
            {
                return;
            }

            // AddFactor takes the delta from 1.0, so 2.0x is passed as +1.0.
            __result.AddFactor(multiplier - 1f, includeDescriptions ? MultiplierText : null);
        }
    }
}
