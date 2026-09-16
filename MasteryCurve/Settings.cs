using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;

namespace MasteryCurve
{
    /// <summary>
    /// Everything here is phrased as something the player can notice while playing.
    /// The curve maths behind it lives in <see cref="Curve"/>; none of it is exposed directly.
    /// </summary>
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        private const string Career = "Your career";
        private const string Feel = "How it feels";
        private const string Practise = "Skills that are hard to practise";
        private const string DebugGroup = "Debug tools";

        public override string Id => "MasteryCurve_v1";
        public override string DisplayName => "Mastery Curve";
        public override string FolderName => "MasteryCurve";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enabled", RequireRestart = true,
            HintText = "Turn off to play with vanilla progression. Reload the campaign after changing this.")]
        [SettingPropertyGroup(Career)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyInteger("You master a skill at character level", 25, 44, "0", RequireRestart = false,
            HintText = "Your best skill reaches 275 here. Nobody who spreads themselves thin will ever pass that number.")]
        [SettingPropertyGroup(Career)]
        public int Level275 { get; set; } = 37;

        [SettingPropertyInteger("You perfect a skill at character level", 30, 52, "0", RequireRestart = false,
            HintText = "Your best skill reaches 330 here. Needs 10 focus and 10 in the attribute that governs it.")]
        [SettingPropertyGroup(Career)]
        public int Level330 { get; set; } = 44;

        [SettingPropertyFloatingInteger("A whole career takes this much longer than vanilla", 0.5f, 6f, "0.0",
            RequireRestart = false,
            HintText = "2.0 means roughly twice as long as a normal campaign before you run out of things to improve.")]
        [SettingPropertyGroup(Career)]
        public float CareerLength { get; set; } = 2f;

        [SettingPropertyInteger("Skills you plan to specialise in", 2, 6, "0", RequireRestart = false,
            HintText = "Used to set the pace. Spread yourself wider than this and everything simply takes longer.")]
        [SettingPropertyGroup(Career)]
        public int FocusedSkills { get; set; } = 3;

        [SettingPropertyFloatingInteger("How fast skills rise early on", 1f, 5f, "0.0", RequireRestart = false,
            HintText = "1 is slow and believable. 5 is vanilla, where a single skirmish can be worth twenty levels in four skills at once.")]
        [SettingPropertyGroup(Feel)]
        public float EarlySpeed { get; set; } = 3.3f;

        [SettingPropertyFloatingInteger("How punishing the last levels are", 1f, 5f, "0.0", RequireRestart = false,
            HintText = "5 is vanilla, where the final handful of levels cost more than everything before them. Lower makes the summit a climb rather than a wall.")]
        [SettingPropertyGroup(Feel)]
        public float SummitHarshness { get; set; } = 3f;

        [SettingPropertyInteger("Focus points you can pour into one skill", 5, 10, "0", RequireRestart = true,
            HintText = "Vanilla stops at 5. The character screen only draws five pips, so anything above that is real but not yet shown.")]
        [SettingPropertyGroup(Feel)]
        public int MaxFocusPerSkill { get; set; } = 10;

        [SettingPropertyBool("Later focus points in a skill cost more", RequireRestart = false,
            HintText = "Turns mastering one skill into a real commitment: 18 points rather than 10.")]
        [SettingPropertyGroup(Feel)]
        public bool EscalatingFocusCost { get; set; } = true;

        [SettingPropertyBool("Pay more for skills you rarely get to practise", RequireRestart = false,
            HintText = "Engineering only earns during a siege. Trade only earns against your own profit. This pays them more per opportunity.")]
        [SettingPropertyGroup(Practise)]
        public bool PerSkillMultipliers { get; set; } = true;

        [SettingPropertyFloatingInteger("Trade", 1f, 6f, "0.0", RequireRestart = false,
            HintText = "Earns half a point per denar of profit and nothing else. Mastering it means millions in trade.")]
        [SettingPropertyGroup(Practise)]
        public float TradeMultiplier { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Engineering", 1f, 6f, "0.0", RequireRestart = false,
            HintText = "Every single source is a siege. Between wars it earns nothing at all.")]
        [SettingPropertyGroup(Practise)]
        public float EngineeringMultiplier { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Tactics", 1f, 6f, "0.0", RequireRestart = false,
            HintText = "Only pays when you let a battle simulate, and then only a sliver.")]
        [SettingPropertyGroup(Practise)]
        public float TacticsMultiplier { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Charm", 1f, 6f, "0.0", RequireRestart = false,
            HintText = "Pays only when someone actually warms to you.")]
        [SettingPropertyGroup(Practise)]
        public float CharmMultiplier { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Steward", 1f, 6f, "0.0", RequireRestart = false,
            HintText = "Most of what feeds it needs a fief first.")]
        [SettingPropertyGroup(Practise)]
        public float StewardMultiplier { get; set; } = 1.6f;

        [SettingPropertyFloatingInteger("Medicine", 1f, 6f, "0.0", RequireRestart = false,
            HintText = "Steady, but the awards are tiny -- a fraction of a point per casualty.")]
        [SettingPropertyGroup(Practise)]
        public float MedicineMultiplier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Roguery", 0.5f, 6f, "0.0", RequireRestart = false,
            HintText = "Fed by nineteen different things, more than any other skill. Below 1.0 slows it back down.")]
        [SettingPropertyGroup(Practise)]
        public float RogueryMultiplier { get; set; } = 0.8f;

        [SettingPropertyBool("Enable debug tools", RequireRestart = false,
            HintText = "Turns on the buttons below. They change your character immediately and cannot be undone.")]
        [SettingPropertyGroup(DebugGroup)]
        public bool DebugEnabled { get; set; } = false;

        [SettingPropertyInteger("How much each button gives", 1, 20, "0", RequireRestart = false,
            HintText = "Points or levels added per press.")]
        [SettingPropertyGroup(DebugGroup)]
        public int DebugAmount { get; set; } = 5;

        [SettingPropertyButton("Focus points", Content = "Give", RequireRestart = false,
            HintText = "Adds unspent focus points to your character.")]
        [SettingPropertyGroup(DebugGroup)]
        public Action GiveFocusPoints { get; set; } = () => DebugTools.GiveFocusPoints();

        [SettingPropertyButton("Attribute points", Content = "Give", RequireRestart = false,
            HintText = "Adds unspent attribute points to your character.")]
        [SettingPropertyGroup(DebugGroup)]
        public Action GiveAttributePoints { get; set; } = () => DebugTools.GiveAttributePoints();

        [SettingPropertyButton("Character levels", Content = "Give", RequireRestart = false,
            HintText = "Raises your character level directly, with the focus and attribute points that come with it.")]
        [SettingPropertyGroup(DebugGroup)]
        public Action GiveCharacterLevels { get; set; } = () => DebugTools.GiveCharacterLevels();

        [SettingPropertyButton("Report where you stand", Content = "Show", RequireRestart = false,
            HintText = "Prints your level, points and the XP the next few skill levels will cost.")]
        [SettingPropertyGroup(DebugGroup)]
        public Action ReportState { get; set; } = () => DebugTools.Report();

        internal float CurveExponent => Lerp(0.8f, 2.1f, (EarlySpeed - 1f) / 4f);

        internal float PenaltySlope => Lerp(0.05f, 0.12f, (SummitHarshness - 1f) / 4f);

        /// <summary>Vanilla's own "long campaign" is about six million raw XP, so that is the unit.</summary>
        internal float CareerXp => CareerLength * 6000000f;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));

        public float MultiplierFor(SkillObject skill)
        {
            var id = skill.StringId;
            if (id == DefaultSkills.Trade.StringId) return TradeMultiplier;
            if (id == DefaultSkills.Engineering.StringId) return EngineeringMultiplier;
            if (id == DefaultSkills.Tactics.StringId) return TacticsMultiplier;
            if (id == DefaultSkills.Charm.StringId) return CharmMultiplier;
            if (id == DefaultSkills.Steward.StringId) return StewardMultiplier;
            if (id == DefaultSkills.Medicine.StringId) return MedicineMultiplier;
            if (id == DefaultSkills.Roguery.StringId) return RogueryMultiplier;
            return 1f;
        }
    }
}
