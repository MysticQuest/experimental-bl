using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace SkillLearningRate
{
    public sealed class TweakedCharacterDevelopmentModel : DefaultCharacterDevelopmentModel
    {
        private static readonly TextObject GlobalLearningRateText = new TextObject("{=SLR001}Global Learning Rate");

        public override ExplainedNumber CalculateLearningRate(
            IReadOnlyPropertyOwner<CharacterAttribute> characterAttributes,
            int focusValue,
            int skillValue,
            SkillObject skill,
            bool includeDescriptions = false)
        {
            var result = base.CalculateLearningRate(characterAttributes, focusValue, skillValue, skill, includeDescriptions);

            if (ModSettings.Current.ShouldApplyMultiplier(out var multiplier))
            {
                result.AddFactor(multiplier - 1f, includeDescriptions ? GlobalLearningRateText : null);
            }

            return result;
        }
    }
}
