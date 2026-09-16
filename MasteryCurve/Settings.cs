using System;
using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;

namespace MasteryCurve
{
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        private const string Career = "Your career";
        private const string Feel = "How it feels";
        private const string Rates = "Skill XP rates";
        private const string DebugGroup = "Debug tools";

        public override string Id => "MasteryCurve_v1";
        public override string DisplayName => "Mastery Curve";
        public override string FolderName => "MasteryCurve";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = true,
            HintText = "Off leaves vanilla progression alone. Reload the campaign after changing this.")]
        [SettingPropertyGroup(Career, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyInteger("You master a skill at character level", 25, 44, "0", Order = 1, RequireRestart = false,
            HintText = "Your best skill reaches 275 here. A character who spreads themselves thin never passes it.")]
        [SettingPropertyGroup(Career, GroupOrder = 0)]
        public int Level275 { get; set; } = 37;

        [SettingPropertyInteger("You perfect a skill at character level", 30, 52, "0", Order = 2, RequireRestart = false,
            HintText = "Your best skill reaches 330 here. Needs 10 focus and 10 in the governing attribute.")]
        [SettingPropertyGroup(Career, GroupOrder = 0)]
        public int Level330 { get; set; } = 44;

        [SettingPropertyFloatingInteger("A career takes this much longer than vanilla", 0.5f, 6f, "0.0", Order = 3,
            RequireRestart = false,
            HintText = "2.0 is roughly twice as long before you run out of things to improve.")]
        [SettingPropertyGroup(Career, GroupOrder = 0)]
        public float CareerLength { get; set; } = 2f;

        [SettingPropertyInteger("Skills you plan to specialise in", 2, 6, "0", Order = 4, RequireRestart = false,
            HintText = "Sets the pace. Spread wider than this and everything takes longer.")]
        [SettingPropertyGroup(Career, GroupOrder = 0)]
        public int FocusedSkills { get; set; } = 3;

        [SettingPropertyFloatingInteger("How fast skills rise early on", 1f, 5f, "0.0", Order = 0, RequireRestart = false,
            HintText = "1 is slow. 5 is vanilla, where one skirmish can be worth twenty levels across four skills.")]
        [SettingPropertyGroup(Feel, GroupOrder = 1)]
        public float EarlySpeed { get; set; } = 3.3f;

        [SettingPropertyFloatingInteger("How punishing the last levels are", 1f, 5f, "0.0", Order = 1, RequireRestart = false,
            HintText = "5 is vanilla, where the final levels cost more than everything before them.")]
        [SettingPropertyGroup(Feel, GroupOrder = 1)]
        public float SummitHarshness { get; set; } = 3f;

        [SettingPropertyInteger("Focus points you can put into one skill", 5, 10, "0", Order = 2, RequireRestart = true,
            HintText = "Vanilla stops at 5.")]
        [SettingPropertyGroup(Feel, GroupOrder = 1)]
        public int MaxFocusPerSkill { get; set; } = 10;

        [SettingPropertyBool("Later focus points in a skill cost more", Order = 3, RequireRestart = false,
            HintText = "Points 1 to 4 cost 1 each, 5 to 8 cost 2 each, 9 and 10 cost 3 each. 18 points for a mastered skill instead of 10.")]
        [SettingPropertyGroup(Feel, GroupOrder = 1)]
        public bool EscalatingFocusCost { get; set; } = true;

        // --- Skill XP rates. 1.0 is the game's own award rate; higher pays more per event. ---

        [SettingPropertyFloatingInteger("One Handed", 0.25f, 6f, "0.00", Order = 0, RequireRestart = false,
            HintText = "Earns per combat hit.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float OneHandedMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Two Handed", 0.25f, 6f, "0.00", Order = 1, RequireRestart = false,
            HintText = "Earns per combat hit.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float TwoHandedMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Polearm", 0.25f, 6f, "0.00", Order = 2, RequireRestart = false,
            HintText = "Earns per combat hit.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float PolearmMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Bow", 0.25f, 6f, "0.00", Order = 3, RequireRestart = false,
            HintText = "Earns per hit, scaled by shot difficulty.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float BowMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Crossbow", 0.25f, 6f, "0.00", Order = 4, RequireRestart = false,
            HintText = "Earns per hit, scaled by shot difficulty.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float CrossbowMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Throwing", 0.25f, 6f, "0.00", Order = 5, RequireRestart = false,
            HintText = "Earns per hit, scaled by shot difficulty.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float ThrowingMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Riding", 0.25f, 6f, "0.00", Order = 6, RequireRestart = false,
            HintText = "Earns per mounted hit and per distance ridden.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float RidingMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Athletics", 0.25f, 6f, "0.00", Order = 7, RequireRestart = false,
            HintText = "Earns per distance travelled on foot and per kill on foot.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float AthleticsMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Smithing", 0.25f, 6f, "0.00", Order = 8, RequireRestart = false,
            HintText = "Earns from smelting, refining and forging.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float SmithingMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Scouting", 0.25f, 6f, "0.00", Order = 9, RequireRestart = false,
            HintText = "Earns from terrain crossed, tracks found and hideouts spotted.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float ScoutingMultiplier { get; set; } = 1.2f;

        [SettingPropertyFloatingInteger("Roguery", 0.25f, 6f, "0.00", Order = 10, RequireRestart = false,
            HintText = "Fed by nineteen separate events, more than any other skill. Below 1.0 slows it to match the rest.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float RogueryMultiplier { get; set; } = 0.8f;

        [SettingPropertyFloatingInteger("Medicine", 0.25f, 6f, "0.00", Order = 11, RequireRestart = false,
            HintText = "Earns a fraction of a point per casualty healed.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float MedicineMultiplier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Leadership", 0.25f, 6f, "0.00", Order = 12, RequireRestart = false,
            HintText = "Earns per troop recruited and upgraded, and while leading an army.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float LeadershipMultiplier { get; set; } = 1.8f;

        [SettingPropertyFloatingInteger("Steward", 0.25f, 6f, "0.00", Order = 13, RequireRestart = false,
            HintText = "Five sources, but most of them need a fief first.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float StewardMultiplier { get; set; } = 1.6f;

        [SettingPropertyFloatingInteger("Tactics", 0.25f, 6f, "0.00", Order = 14, RequireRestart = false,
            HintText = "One source, and only when you let a battle simulate.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float TacticsMultiplier { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Charm", 0.25f, 6f, "0.00", Order = 15, RequireRestart = false,
            HintText = "One source: relation gained.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float CharmMultiplier { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Engineering", 0.25f, 6f, "0.00", Order = 16, RequireRestart = false,
            HintText = "Every source is a siege. Between wars it earns nothing at all.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float EngineeringMultiplier { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Trade", 0.25f, 6f, "0.00", Order = 17, RequireRestart = false,
            HintText = "Earns half a point per denar of profit and nothing else.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float TradeMultiplier { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Mariner", 0.25f, 6f, "0.00", Order = 18, RequireRestart = false,
            HintText = "Earns per hit and kill aboard ship, and on the daily tick. Needs the naval content.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float MarinerMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Boatswain", 0.25f, 6f, "0.00", Order = 19, RequireRestart = false,
            HintText = "Earns when a ship takes damage or is repaired, and on the daily tick. Needs the naval content.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float BoatswainMultiplier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Shipmaster", 0.25f, 6f, "0.00", Order = 20, RequireRestart = false,
            HintText = "Earns per distance sailed and on the daily tick. Needs the naval content.")]
        [SettingPropertyGroup(Rates, GroupOrder = 2)]
        public float ShipmasterMultiplier { get; set; } = 1.2f;

        // --- Debug ---

        [SettingPropertyBool("Enable debug tools", Order = 0, RequireRestart = false,
            HintText = "Turns on the buttons below. They change your character immediately.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 3)]
        public bool DebugEnabled { get; set; } = false;

        [SettingPropertyButton("Focus point", Content = "Add", Order = 1, RequireRestart = false,
            HintText = "Adds one unspent focus point.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 3)]
        public Action AddFocusPoint { get; set; } = DebugTools.AddFocusPoint;

        [SettingPropertyButton("Attribute point", Content = "Add", Order = 2, RequireRestart = false,
            HintText = "Adds one unspent attribute point.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 3)]
        public Action AddAttributePoint { get; set; } = DebugTools.AddAttributePoint;

        [SettingPropertyButton("Character level", Content = "Add", Order = 3, RequireRestart = false,
            HintText = "Adds one character level, with the points that come with it.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 3)]
        public Action AddCharacterLevel { get; set; } = DebugTools.AddCharacterLevel;

        [SettingPropertyButton("Unspent points", Content = "Reset", Order = 4, RequireRestart = false,
            HintText = "Clears all unspent focus and attribute points.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 3)]
        public Action ResetPoints { get; set; } = DebugTools.ResetPoints;

        [SettingPropertyButton("Current status", Content = "Show", Order = 5, RequireRestart = false,
            HintText = "Prints your level, your unspent points and what the next levels cost.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 3)]
        public Action CurrentStatus { get; set; } = DebugTools.CurrentStatus;

        internal float CurveExponent => Lerp(0.8f, 2.1f, (EarlySpeed - 1f) / 4f);

        internal float PenaltySlope => Lerp(0.05f, 0.12f, (SummitHarshness - 1f) / 4f);

        /// <summary>A long vanilla campaign is about six million raw XP, so that is the unit.</summary>
        internal float CareerXp => CareerLength * 6000000f;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));

        private Dictionary<string, Func<float>>? _lookup;

        /// <summary>
        /// Naval skills are matched by string id rather than a reference, so this still builds and
        /// runs without the naval content installed -- those ids simply never turn up.
        /// </summary>
        public float MultiplierFor(SkillObject skill)
        {
            _lookup ??= new Dictionary<string, Func<float>>
            {
                ["Mariner"] = () => MarinerMultiplier,
                ["Boatswain"] = () => BoatswainMultiplier,
                ["Shipmaster"] = () => ShipmasterMultiplier,
                [DefaultSkills.OneHanded.StringId] = () => OneHandedMultiplier,
                [DefaultSkills.TwoHanded.StringId] = () => TwoHandedMultiplier,
                [DefaultSkills.Polearm.StringId] = () => PolearmMultiplier,
                [DefaultSkills.Bow.StringId] = () => BowMultiplier,
                [DefaultSkills.Crossbow.StringId] = () => CrossbowMultiplier,
                [DefaultSkills.Throwing.StringId] = () => ThrowingMultiplier,
                [DefaultSkills.Riding.StringId] = () => RidingMultiplier,
                [DefaultSkills.Athletics.StringId] = () => AthleticsMultiplier,
                [DefaultSkills.Crafting.StringId] = () => SmithingMultiplier,
                [DefaultSkills.Scouting.StringId] = () => ScoutingMultiplier,
                [DefaultSkills.Roguery.StringId] = () => RogueryMultiplier,
                [DefaultSkills.Medicine.StringId] = () => MedicineMultiplier,
                [DefaultSkills.Leadership.StringId] = () => LeadershipMultiplier,
                [DefaultSkills.Steward.StringId] = () => StewardMultiplier,
                [DefaultSkills.Tactics.StringId] = () => TacticsMultiplier,
                [DefaultSkills.Charm.StringId] = () => CharmMultiplier,
                [DefaultSkills.Engineering.StringId] = () => EngineeringMultiplier,
                [DefaultSkills.Trade.StringId] = () => TradeMultiplier
            };

            return _lookup.TryGetValue(skill.StringId, out var value) ? value() : 1f;
        }
    }
}
