using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace MasteryCurve
{
    /// <summary>
    /// Replaces vanilla's progression numbers while keeping vanilla's mechanism: the ceiling
    /// still emerges where the learning rate runs out, rather than being clamped anywhere.
    /// </summary>
    public sealed class MasteryCurveModel : DefaultCharacterDevelopmentModel
    {
        private static readonly TextObject AttributeText = new TextObject("{=MC001}Attribute");
        private static readonly TextObject FocusText = new TextObject("{=MC002}Focus");
        private static readonly TextObject OverLimitText = new TextObject("{=MC003}Beyond your limit");

        public override int MaxFocusPerSkill => Settings.Instance?.MaxFocusPerSkill ?? 10;

        public override int GetXpRequiredForSkillLevel(int skillLevel) => Curve.SkillXpRequired(skillLevel);

        public override int SkillsRequiredForLevel(int level) => Curve.CharacterXpRequired(level);

        public override ExplainedNumber CalculateLearningLimit(
            IReadOnlyPropertyOwner<CharacterAttribute> characterAttributes,
            int focusValue,
            SkillObject skill,
            bool includeDescriptions = false)
        {
            var result = new ExplainedNumber(0f, includeDescriptions, null);
            result.Add(Curve.LimitValue(AverageAttribute(characterAttributes, skill), focusValue), AttributeText, null);
            result.LimitMin(0f);
            return result;
        }

        public override ExplainedNumber CalculateLearningRate(
            IReadOnlyPropertyOwner<CharacterAttribute> characterAttributes,
            int focusValue,
            int skillValue,
            SkillObject skill,
            bool includeDescriptions = false)
        {
            var attribute = AverageAttribute(characterAttributes, skill);
            var slope = Settings.Instance?.PenaltySlope ?? 0.09f;

            var result = new ExplainedNumber(1.25f, includeDescriptions, null);
            result.AddFactor(0.4f * attribute, includeDescriptions ? AttributeText : null);
            result.AddFactor(Curve.FocusTerm(focusValue), includeDescriptions ? FocusText : null);

            // Vanilla's own shape: a linear slide once past the limit. The only differences are a
            // gentler slope and a floor, so the rate approaches zero without ever arriving.
            var baseFactor = Curve.BaseFactor(attribute, focusValue);
            var limit = Curve.LimitValue(attribute, focusValue);
            if (skillValue > limit)
            {
                var penalty = 1f + slope * (skillValue - limit);
                var survived = Math.Max(baseFactor * Curve.RateFloor, baseFactor - penalty);
                result.AddFactor(survived - baseFactor, includeDescriptions ? OverLimitText : null);
            }

            result.LimitMin(1.25f * Curve.RateFloor);
            return result;
        }

        private static float AverageAttribute(IReadOnlyPropertyOwner<CharacterAttribute> attributes, SkillObject skill)
        {
            var owned = skill?.Attributes;
            if (owned == null || owned.Length == 0 || attributes == null) return 0f;

            var total = 0f;
            foreach (var attribute in owned) total += attributes.GetPropertyValue(attribute);
            return total / owned.Length;
        }
    }
}
