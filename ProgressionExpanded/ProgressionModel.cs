using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ProgressionExpanded
{
    /// <summary>
    /// Replaces vanilla's progression numbers while keeping vanilla's mechanism: the ceiling
    /// still emerges where the learning rate runs out, rather than being clamped anywhere.
    /// </summary>
    public sealed class ProgressionModel : DefaultCharacterDevelopmentModel
    {
        private static readonly TextObject AttributeText = new TextObject("{=MC001}Attribute");
        private static readonly TextObject FocusText = new TextObject("{=MC002}Focus");
        private static readonly TextObject OverLimitText = new TextObject("{=MC003}Beyond your limit");
        private static readonly TextObject IntelligenceText = new TextObject("{=MC004}Intelligence");

        public override int MaxFocusPerSkill { get { Guard.Touch("Model.MaxFocusPerSkill"); return Settings.Instance?.MaxFocusPerSkill ?? 10; } }

        public override int FocusPointsPerLevel { get { Guard.Touch("Model.FocusPerLevel"); return Math.Max(1, Settings.Instance?.FocusPointsPerLevel ?? 1); } }

        public override int LevelsPerAttributePoint { get { Guard.Touch("Model.LevelsPerAttribute"); return Math.Max(1, Settings.Instance?.LevelsPerAttributePoint ?? 4); } }

        public override int GetXpRequiredForSkillLevel(int skillLevel)
        {
            Guard.Touch("Model.SkillXp");
            Probe.Count("SkillXp", skillLevel);
            try { return Curve.SkillXpRequired(skillLevel); }
            catch (Exception exception) { Guard.Report("Model.SkillXp", exception); return base.GetXpRequiredForSkillLevel(skillLevel); }
        }

        public override int SkillsRequiredForLevel(int level)
        {
            Guard.Touch("Model.CharacterXp");
            Probe.Count("CharacterXp", level);
            try { return Curve.CharacterXpRequired(level); }
            catch (Exception exception) { Guard.Report("Model.CharacterXp", exception); return base.SkillsRequiredForLevel(level); }
        }

        public override ExplainedNumber CalculateLearningLimit(
            IReadOnlyPropertyOwner<CharacterAttribute> characterAttributes,
            int focusValue,
            SkillObject skill,
            bool includeDescriptions = false)
        {
            Guard.Touch("Model.LearningLimit");
            Probe.Count("LearningLimit", focusValue);
            var result = new ExplainedNumber(0f, includeDescriptions, null);
            result.Add(Curve.LimitValue(AverageAttribute(characterAttributes, skill), focusValue), AttributeText, null);

            var ceiling = IntelligenceCeiling(characterAttributes);
            if (ceiling > 0f) result.Add(ceiling, includeDescriptions ? IntelligenceText : null, null);

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
            Guard.Touch("Model.LearningRate");
            Probe.Count("LearningRate", skillValue);
            var attribute = AverageAttribute(characterAttributes, skill);
            var slope = Settings.Instance?.PenaltySlope ?? 0.09f;

            var result = new ExplainedNumber(1.25f, includeDescriptions, null);
            result.AddFactor(0.4f * attribute, includeDescriptions ? AttributeText : null);
            result.AddFactor(Curve.FocusTerm(focusValue), includeDescriptions ? FocusText : null);

            // Vanilla's own shape: a linear slide once past the limit. The only differences are a
            // gentler slope and a floor, so the rate approaches zero without ever arriving.
            var baseFactor = Curve.BaseFactor(attribute, focusValue);

            // The same addition the limit itself gets, or the screen would promise a ceiling the
            // penalty had already started eating into.
            var limit = Curve.LimitValue(attribute, focusValue) + IntelligenceCeiling(characterAttributes);
            if (skillValue > limit)
            {
                var penalty = 1f + slope * (skillValue - limit);
                var survived = Math.Max(baseFactor * Curve.RateFloor, baseFactor - penalty);
                result.AddFactor(survived - baseFactor, includeDescriptions ? OverLimitText : null);
            }

            // Intelligence lifts every skill, not just the three it governs. Applied as a true
            // multiplier on what the rest of the rules produced: AddFactor feeds one shared sum,
            // so scaling by the running total is what makes this a clean percentage rather than
            // a number whose worth depends on how good the skill already was.
            if (AttributeBonus.Active())
            {
                var intelligence = characterAttributes == null
                    ? 0f
                    : characterAttributes.GetPropertyValue(DefaultCharacterAttributes.Intelligence);

                if (intelligence > 0f)
                {
                    var boost = AttributeBonus.Rate(AttributeBonus.IntelligenceLearning) * intelligence;
                    result.AddFactor(boost * (1f + result.SumOfFactors),
                                     includeDescriptions ? IntelligenceText : null);
                }
            }

            result.LimitMin(1.25f * Curve.RateFloor);
            return result;
        }

        /// <summary>
        /// Skill levels Intelligence adds to every learning limit, which moves the ceiling itself.
        /// </summary>
        /// <remarks>
        /// The rate bonus pays while a skill is still climbing and is worth nothing once it has
        /// stalled; this one is worth nothing early and everything late. Together they are what
        /// makes Intelligence the attribute of the long game rather than a smaller copy of the
        /// bound-attribute bonus it already grants three skills.
        /// </remarks>
        private static float IntelligenceCeiling(IReadOnlyPropertyOwner<CharacterAttribute> attributes)
        {
            if (attributes == null || !AttributeBonus.Active()) return 0f;

            var intelligence = attributes.GetPropertyValue(DefaultCharacterAttributes.Intelligence);
            return intelligence <= 0f ? 0f : AttributeBonus.Rate(AttributeBonus.IntelligenceCeiling) * intelligence;
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
