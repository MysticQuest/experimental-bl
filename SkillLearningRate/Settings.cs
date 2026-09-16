using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace SkillLearningRate
{
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        private const string GeneralGroup = "General";

        public override string Id => "SkillLearningRate_v1";

        public override string DisplayName => "Skill Learning Rate";

        public override string FolderName => "SkillLearningRate";

        public override string FormatType => "json2";

        [SettingPropertyBool(
            "Enabled",
            RequireRestart = false,
            HintText = "Turn off to leave the vanilla learning rate completely untouched.")]
        [SettingPropertyGroup(GeneralGroup)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Global Learning Rate Multiplier",
            0.01f,
            20f,
            "0.00",
            RequireRestart = false,
            HintText = "Multiplies the learning rate of every skill for every hero. 1.00 is vanilla, 2.00 is twice as fast, 0.50 is half.")]
        [SettingPropertyGroup(GeneralGroup)]
        public float GlobalLearningRateMultiplier { get; set; } = 1f;
    }
}
