using System;
using System.Collections.Generic;
using MCM.Abstractions;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Abstractions.Base;
using MCM.Common;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ProgressionExpanded
{
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        private const string Features = "What the mod does";
        private const string Career = "Your progression";
        private const string Feel = "How it feels";
        private const string Rates = "Skill XP rates";
        private const string Skills = "Global skill bonuses";
        private const string Attributes = "Attribute bonuses";
        private const string Legacy = "Your children";
        private const string Trouble = "Troubleshooting";
        private const string DebugGroup = "Debug tools";

        /// <summary>
        /// The named starting points offered at the top of the settings page.
        /// </summary>
        /// <remarks>
        /// Twenty-odd knobs with no guidance is a page most people close again. "Default" is the
        /// tuning this mod is actually about; "Closer to vanilla" is for someone who wants the
        /// bonuses without the pacing, and moves only the progression knobs - shorter climb,
        /// vanilla's five focus points at a flat price, and its steep wall at the top - while
        /// leaving both bonus groups switched on.
        /// </remarks>
        [SettingPropertyBool("Start with a burlap sack (new campaigns only)", Order = 5, RequireRestart = false,
            HintText = "The bandits who came for your family took the rest of it too - the horse, the purse, your father's sword - and your brother, who still has his, is not in a sharing mood. You begin in a cozy burlap sack. It also makes the villagers of a certain village less generous. Only applies when a new campaign begins; an existing one is never stripped.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public bool StartWithNothing { get; set; } = true;

        [SettingPropertyButton("Information", Content = "Read", Order = 6,
            RequireRestart = false,
            HintText = "What this mod is and how finished it is, plus which settings take hold at once, which need the campaign reloaded, and what may happen to skills you have already earned. Worth a read before changing anything in a campaign you care about.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public Action MidCampaignNote { get; set; } = Guidance.MidCampaign;

        public override IEnumerable<ISettingsPreset> GetBuiltInPresets()
        {
            foreach (var preset in base.GetBuiltInPresets()) yield return preset;

            yield return new Preset(Id, "vanillaish", "Closer to vanilla", () => new Settings
            {
                // Not guessed: these four are the closest fit to vanilla's own effort curve that
                // the dials can reach. Searched over the whole grid against vanilla's cumulative
                // XP for a 10-attribute, 5-focus character, they hold within 20% of it from skill
                // 40 to 320. The values that were here before drifted to a seventh of vanilla's
                // cost by 320, which is not what the name promises.
                Level330 = 32,
                CareerLength = 1.25f,
                EarlySpeed = 5f,
                SummitHarshness = 3f,
                MaxFocusPerSkill = 5,
                EscalatingFocusCost = false,
                FocusPointsPerLevel = 1,
                LevelsPerAttributePoint = 4,
            });
        }

        /// <summary>
        /// One named starting point, built from a whole settings object.
        /// </summary>
        /// <remarks>
        /// MCM builds its own presets with a fluent builder that is internal to the assembly, so
        /// the interface is implemented here instead. It is four members, and handing back a
        /// fully-formed <see cref="Settings"/> is clearer than a list of property names and boxed
        /// values anyway: anything the preset does not mention keeps its declared default, which
        /// is what "closer to vanilla only moves the progression knobs" means in practice.
        /// </remarks>
        private sealed class Preset : ISettingsPreset
        {
            private readonly Func<Settings> _build;

            internal Preset(string settingsId, string id, string name, Func<Settings> build)
            {
                SettingsId = settingsId;
                Id = id;
                Name = name;
                _build = build;
            }

            public string SettingsId { get; }
            public string Id { get; }
            public string Name { get; }

            public BaseSettings LoadPreset() => _build();

            /// <summary>These are fixed, so there is nothing to save over them.</summary>
            public bool SavePreset(BaseSettings settings) => false;
        }

        public override string Id => "ProgressionExpanded_v1";
        public override string DisplayName => "Progression Expanded";
        public override string FolderName => "ProgressionExpanded";
        public override string FormatType => "json2";

        [SettingPropertyBool("Progression Expanded", Order = 0, RequireRestart = true,
            HintText = "Off leaves vanilla progression alone. Reload the campaign after changing this.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyInteger("You can perfect a skill by character level", 30, 52, "0", Order = 2, RequireRestart = false,
            HintText = "The earliest 330 is reachable at all, not a level you arrive at. It assumes everything lines up: 10 in the governing attribute, every focus point in that one skill, and the XP actually going there. Spread your effort wider, or leave a point unspent, and it takes longer - most characters never get there.")]
        [SettingPropertyGroup(Career, GroupOrder = 1)]
        public int Level330 { get; set; } = 40;

        [SettingPropertyFloatingInteger("Progression takes this much longer than vanilla", 0.5f, 6f, "0.0", Order = 3,
            RequireRestart = false,
            HintText = "2.0 is roughly twice as long before you run out of things to improve. Calibrated against specialising in about three skills; spread wider and everything simply takes longer.")]
        [SettingPropertyGroup(Career, GroupOrder = 1)]
        public float CareerLength { get; set; } = 2f;

        [SettingPropertyInteger("Focus points you get per level", 1, 10, "0", Order = 4, RequireRestart = false,
            HintText = "Vanilla gives 1. Raising this only affects levels you gain from now on - it does not hand out points for levels already behind you.")]
        [SettingPropertyGroup(Career, GroupOrder = 1)]
        public int FocusPointsPerLevel { get; set; } = 1;

        [SettingPropertyInteger("Levels per attribute point", 1, 10, "0", Order = 5, RequireRestart = false,
            HintText = "Vanilla gives one attribute point every 4 levels. Lower is more generous. Like the setting above, this only affects levels you gain from now on.")]
        [SettingPropertyGroup(Career, GroupOrder = 1)]
        public int LevelsPerAttributePoint { get; set; } = 4;

        [SettingPropertyFloatingInteger("How fast skills rise early on", 1f, 10f, "0.0", Order = 0, RequireRestart = false,
            HintText = "5 is vanilla, where one skirmish can be worth twenty levels across four skills. Below that the early game slows down; above it goes further the other way than vanilla ever does.")]
        [SettingPropertyGroup(Feel, GroupOrder = 2)]
        public float EarlySpeed { get; set; } = 3f;

        [SettingPropertyFloatingInteger("How punishing the last levels are", 0.5f, 5f, "0.0", Order = 1, RequireRestart = false,
            HintText = "How hard the last stretch bites. 5 is the steepest this curve goes: the slide is 1.6 times vanilla-s and only 7% of your learning rate is left at 330. Vanilla is harsher still, but only by reaching exactly zero, which makes its last level cost infinity - that is why 329 is vanilla-s true maximum and 330 is not. Lower is gentler: 2 leaves 22%, 0.5 leaves 30%. 330 stays within reach at every setting. Where skill 275 lands falls out of this; press Current status to see it.")]
        [SettingPropertyGroup(Feel, GroupOrder = 2)]
        public float SummitHarshness { get; set; } = 2.5f;

        [SettingPropertyInteger("Focus points you can put into one skill", 5, 10, "0", Order = 2, RequireRestart = true,
            HintText = "Vanilla stops at 5. The total pull of full focus is the same at any maximum, so a lower number just means fewer, larger steps: at 10 each point is worth half a vanilla point, at 9 five ninths, at 5 a full one. This is the only setting that needs a game restart, because the pips are drawn once when the UI loads.")]
        [SettingPropertyGroup(Feel, GroupOrder = 2)]
        public int MaxFocusPerSkill { get; set; } = 10;

        [SettingPropertyBool("Later focus points in a skill cost more", Order = 3, RequireRestart = false,
            HintText = "Points 1 to 4 cost 1 each, 5 to 8 cost 2 each, 9 and 10 cost 3 each. 18 points for a mastered skill instead of 10.")]
        [SettingPropertyGroup(Feel, GroupOrder = 2)]
        public bool EscalatingFocusCost { get; set; } = true;

        // --- Skill XP rates. 1.0 is the game's own award rate; higher pays more per event. ---

        [SettingPropertyFloatingInteger("One Handed", 1f, 10f, "0.00", Order = 0, RequireRestart = false,
            HintText = "Earns per combat hit.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float OneHandedMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Two Handed", 1f, 10f, "0.00", Order = 1, RequireRestart = false,
            HintText = "Earns per combat hit.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float TwoHandedMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Polearm", 1f, 10f, "0.00", Order = 2, RequireRestart = false,
            HintText = "Earns per combat hit.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float PolearmMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Bow", 1f, 10f, "0.00", Order = 3, RequireRestart = false,
            HintText = "Earns per hit, scaled by shot difficulty.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float BowMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Crossbow", 1f, 10f, "0.00", Order = 4, RequireRestart = false,
            HintText = "Earns per hit, scaled by shot difficulty.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float CrossbowMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Throwing", 1f, 10f, "0.00", Order = 5, RequireRestart = false,
            HintText = "Earns per hit, scaled by shot difficulty.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float ThrowingMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Riding", 1f, 10f, "0.00", Order = 6, RequireRestart = false,
            HintText = "Earns per mounted hit and per distance ridden.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float RidingMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Athletics", 1f, 10f, "0.00", Order = 7, RequireRestart = false,
            HintText = "Earns per distance travelled on foot and per kill on foot.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float AthleticsMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Smithing", 1f, 10f, "0.00", Order = 8, RequireRestart = false,
            HintText = "Earns from smelting, refining and forging.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float SmithingMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Scouting", 1f, 10f, "0.00", Order = 9, RequireRestart = false,
            HintText = "Earns from terrain crossed, tracks found and hideouts spotted.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float ScoutingMultiplier { get; set; } = 1.1f;

        [SettingPropertyFloatingInteger("Roguery", 1f, 10f, "0.00", Order = 10, RequireRestart = false,
            HintText = "Fed by nineteen separate events, more than any other skill, so it needs no help.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float RogueryMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Medicine", 1f, 10f, "0.00", Order = 11, RequireRestart = false,
            HintText = "Earns a fraction of a point per casualty healed.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float MedicineMultiplier { get; set; } = 1.25f;

        [SettingPropertyFloatingInteger("Leadership", 1f, 10f, "0.00", Order = 12, RequireRestart = false,
            HintText = "Earns per troop recruited and upgraded, and while leading an army.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float LeadershipMultiplier { get; set; } = 1.4f;

        [SettingPropertyFloatingInteger("Steward", 1f, 10f, "0.00", Order = 13, RequireRestart = false,
            HintText = "Five sources, but most of them need a fief first.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float StewardMultiplier { get; set; } = 1.3f;

        [SettingPropertyFloatingInteger("Tactics", 1f, 10f, "0.00", Order = 14, RequireRestart = false,
            HintText = "One source, and only when you let a battle simulate.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float TacticsMultiplier { get; set; } = 1.75f;

        [SettingPropertyFloatingInteger("Charm", 1f, 10f, "0.00", Order = 15, RequireRestart = false,
            HintText = "One source: relation gained.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float CharmMultiplier { get; set; } = 1.75f;

        [SettingPropertyFloatingInteger("Engineering", 1f, 10f, "0.00", Order = 16, RequireRestart = false,
            HintText = "Every source is a siege. Between wars it earns nothing at all.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float EngineeringMultiplier { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Trade", 1f, 10f, "0.00", Order = 17, RequireRestart = false,
            HintText = "Earns half a point per denar of profit and nothing else.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float TradeMultiplier { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Mariner", 1f, 10f, "0.00", Order = 18, RequireRestart = false,
            HintText = "Earns per hit and kill aboard ship, and on the daily tick. Needs the naval content.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float MarinerMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Boatswain", 1f, 10f, "0.00", Order = 19, RequireRestart = false,
            HintText = "Earns when a ship takes damage or is repaired, and on the daily tick. Needs the naval content.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float BoatswainMultiplier { get; set; } = 1.25f;

        [SettingPropertyFloatingInteger("Shipmaster", 1f, 10f, "0.00", Order = 20, RequireRestart = false,
            HintText = "Earns per distance sailed and on the daily tick. Needs the naval content.")]
        [SettingPropertyGroup(Rates, GroupOrder = 3)]
        public float ShipmasterMultiplier { get; set; } = 1.1f;

        // --- Global skill bonuses ---

        [SettingPropertyBool("Global skill bonuses", Order = 2, RequireRestart = false,
            HintText = "Extra rewards for mastering a skill, yours alone. Tactics, Scouting, Roguery and Trade each gain several; each is listed on its own skill card, worded like the game's own. Off, they leave the character screen as well rather than sitting there reading zero.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public bool SkillBonuses { get; set; } = true;

        [SettingPropertyFloatingInteger("Global skill bonus strength", 0f, 2f, "0.00", Order = 1,
            RequireRestart = true,
            HintText = "Scales every skill bonus above at once. 1.00 is the tuning described here, 0.50 halves all of them, 2.00 doubles them. The character screen quotes the scaled figure, so what it says is what you get. Needs a campaign reload, because these are registered with the game once when a campaign starts.")]
        [SettingPropertyGroup(Skills, GroupOrder = 4)]
        public float SkillStrength { get; set; } = 1f;


        [SettingPropertyBool("Bandits may join you outright", Order = 1, RequireRestart = false,
            HintText = "At high Roguery a lone bandit party sometimes falls in behind you instead of fighting. Unlike everything else here it rewrites the encounter rather than adjusting a number, so turn it off first if a campaign starts misbehaving.")]
        [SettingPropertyGroup(Skills, GroupOrder = 4)]
        public bool BanditsMayJoin { get; set; } = true;

        // --- Attribute bonuses ---

        [SettingPropertyBool("Attribute bonuses", Order = 3, RequireRestart = false,
            HintText = "Something each attribute buys the moment the point is spent, on top of the learning rate and ceiling it already gives - three or four apiece, covering how hard you hit, how well you take a hit, how your clan does and how fast you learn. Each attribute lists its own on its card, which needs Character screen changes on.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public bool AttributeBonuses { get; set; } = true;

        [SettingPropertyFloatingInteger("Attribute bonus strength", 0f, 2f, "0.00", Order = 1,
            RequireRestart = false,
            HintText = "Scales every attribute bonus at once. 1.00 is the tuning described above, 0.50 halves all of them, 2.00 doubles them. The attribute card quotes the scaled figure, so what it says is what you get. Takes effect immediately.")]
        [SettingPropertyGroup(Attributes, GroupOrder = 5)]
        public float AttributeStrength { get; set; } = 1f;

        [SettingPropertyBool("Other heroes get them too", Order = 2, RequireRestart = false,
            HintText = "On, an attribute pays whoever owns it - your companions, and every lord in Calradia, the same as you. Off, only your own character benefits. Three of them are yours either way and are unaffected by this: battle loot, cheating death and crime rating are read off your character by construction. Intelligence's learning rate and limit are likewise unaffected, because the game asks for them without saying which hero is asking.")]
        [SettingPropertyGroup(Attributes, GroupOrder = 5)]
        public bool BonusesForOthers { get; set; } = true;

        // --- Your children ---

        [SettingPropertyBool("Children are better than their parents", Order = 4, RequireRestart = false,
            HintText = "Each generation of your clan learns faster, reaches higher and can specialise in more. What they are good at still comes from their own upbringing and their own campaign - only the capacity is inherited. Nothing is capped, so a fifth-generation heir is genuinely remarkable.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public bool GenerationsImprove { get; set; } = true;

        [SettingPropertyFloatingInteger("Faster learning per generation", 0f, 0.5f, "0.00", Order = 1, RequireRestart = false,
            HintText = "0.15 means a grandchild earns 30% more from everything they do than the founder did.")]
        [SettingPropertyGroup(Legacy, GroupOrder = 6)]
        public float GenerationLearningStep { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Higher ceiling per generation", 0f, 30f, "0", Order = 2, RequireRestart = false,
            HintText = "Skill levels added to every ceiling. At 10, a grandchild can push a fully invested skill 20 past where the founder had to stop.")]
        [SettingPropertyGroup(Legacy, GroupOrder = 6)]
        public float GenerationCeilingStep { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Extra focus points per generation", 0f, 10f, "0", Order = 3, RequireRestart = false,
            HintText = "Handed over when the heir comes of age. This is what lets a later generation specialise in more things at once.")]
        [SettingPropertyGroup(Legacy, GroupOrder = 6)]
        public float GenerationFocusStep { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Extra attribute points per generation", 0f, 10f, "0", Order = 4, RequireRestart = false,
            HintText = "Handed over when the heir comes of age.")]
        [SettingPropertyGroup(Legacy, GroupOrder = 6)]
        public float GenerationAttributeStep { get; set; } = 2f;

        // --- Troubleshooting ---

        [SettingPropertyBool("Progression curve (needs restart)", Order = 1, RequireRestart = true,
            HintText = "The XP curve, the learning rates and the ceilings - the pacing half of the mod. Off, the game keeps vanilla progression and EVERYTHING ELSE HERE CARRIES ON: the attribute bonuses, the skill bonuses, the XP rates and your children's inheritance all still apply. This is the supported way to take the extras without the slower climb.")]
        [SettingPropertyGroup(Features, GroupOrder = 0)]
        public bool UseCurve { get; set; } = true;

        [SettingPropertyBool("Repair skill XP when a campaign loads (needs restart)", Order = 1, RequireRestart = true,
            HintText = "Re-seeds any skill whose stored XP has gone negative against the current curve. Harmless normally, but it runs during load, so it is worth being able to switch off.")]
        [SettingPropertyGroup(Trouble, GroupOrder = 7)]
        public bool RepairSkillXp { get; set; } = true;

        [SettingPropertyBool("Character screen changes (needs restart)", Order = 2, RequireRestart = true,
            HintText = "The focus pip row. This is the only part of the mod that edits the game's UI layout, and the only part that cannot be undone without a restart.")]
        [SettingPropertyGroup(Trouble, GroupOrder = 7)]
        public bool UiChanges { get; set; } = true;

        [SettingPropertyBool("Battle and mission changes (needs restart)", Order = 3, RequireRestart = true,
            HintText = "Everything that patches agents, damage, reinforcements and encounters: hit points, weapon handling, damage resistance, mount and running speed, civilian weapon damage, reinforcement waves, and bandits joining you. Switch it off to run the mod with nothing attached to an agent or a mission at all.")]
        [SettingPropertyGroup(Trouble, GroupOrder = 7)]
        public bool MissionPatches { get; set; } = true;

        [SettingPropertyInteger("Patch classes to apply (needs restart)", 0, 40, "0", Order = 4, RequireRestart = true,
            HintText = "Troubleshooting only. Applies the first N patch classes in alphabetical order and skips the rest; the log names every one it applied. Leave at 40 for normal play. To find a faulty patch, halve it until the fault stops, then read the log for the last name that was still included.")]
        [SettingPropertyGroup(Trouble, GroupOrder = 7)]
        public int PatchLimit { get; set; } = 40;

        // --- Debug ---

        [SettingPropertyBool("Enable debug tools", Order = 0, RequireRestart = false,
            HintText = "Turns on the buttons below. They change your character immediately.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public bool DebugEnabled { get; set; } = false;

        [SettingPropertyButton("Focus point", Content = "Add", Order = 1, RequireRestart = false,
            HintText = "Adds one unspent focus point.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action AddFocusPoint { get; set; } = DebugTools.AddFocusPoint;

        [SettingPropertyButton("Attribute point", Content = "Add", Order = 2, RequireRestart = false,
            HintText = "Adds one unspent attribute point.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action AddAttributePoint { get; set; } = DebugTools.AddAttributePoint;

        [SettingPropertyButton("Character level", Content = "Add", Order = 3, RequireRestart = false,
            HintText = "Adds one character level, with the points that come with it.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action AddCharacterLevel { get; set; } = DebugTools.AddCharacterLevel;

        [SettingPropertyDropdown("Skill", Order = 4, RequireRestart = false,
            HintText = "Which skill the button below raises. Naval skills only work with the naval content installed.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Dropdown<string> DebugSkill { get; set; } = new Dropdown<string>(DebugSkillNames, 14);

        [SettingPropertyButton("Skill level", Content = "+10", Order = 5, RequireRestart = false,
            HintText = "Adds ten levels to the skill chosen above. The skill's own XP multiplier is ignored, so ten means ten.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action AddSkillLevels { get; set; } = DebugTools.AddSkillLevels;

        [SettingPropertyButton("Unspent points", Content = "Reset", Order = 6, RequireRestart = false,
            HintText = "Clears all unspent focus and attribute points.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action ResetPoints { get; set; } = DebugTools.ResetPoints;

        [SettingPropertyButton("Finish the tutorial", Content = "Finish", Order = 11, RequireRestart = false,
            HintText = "Ends the village stealth tutorial where you stand, exactly as finishing it would. For testing what the villagers hand over without playing it through each time.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action FinishTutorial { get; set; } = TutorialShortcut.Finish;

        [SettingPropertyButton("Save character*", Content = "Save", Order = 9, RequireRestart = false,
            HintText = "Saves name, face, height, weight, build, attributes, focus and skills, so you need only make a character once. * Not saved: voice, age, culture and anything you own.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action SaveCharacter { get; set; } = CharacterTemplate.Save;

        [SettingPropertyButton("Load character*", Content = "Load", Order = 10, RequireRestart = false,
            HintText = "Makes whoever you are playing into the saved one. A test tool: it writes straight to the hero, so use it on a test campaign rather than a real one.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action LoadCharacter { get; set; } = CharacterTemplate.Load;

        [SettingPropertyButton("Bonus report", Content = "Check", Order = 8, RequireRestart = false,
            HintText = "Reports whether the patches bound and what the attribute card should be showing. Use this if the bonus lines are missing.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action AttributeReport { get; set; } = DebugTools.AttributeReport;

        [SettingPropertyButton("Current status", Content = "Show", Order = 7, RequireRestart = false,
            HintText = "Prints your level, your unspent points and what the next levels cost.")]
        [SettingPropertyGroup(DebugGroup, GroupOrder = 8)]
        public Action CurrentStatus { get; set; } = DebugTools.CurrentStatus;

        // 5 is vanilla on both dials; the scale runs to 10 so it can go further than vanilla too.
        internal float CurveExponent => 0.8f + 0.3f * Clamp(EarlySpeed - 1f, 0f, 9f);

        /// <summary>
        /// The dial runs 0.5 to 5 so that its top is vanilla, but the curve underneath is the same
        /// one it always was: this maps the dial back onto the nine steps the formulas expect.
        /// </summary>
        private float HarshSteps => Clamp(SummitHarshness * 2f - 1f, 0f, 9f);

        internal float PenaltySlope => 0.05f + 0.0125f * HarshSteps;

        /// <summary>
        /// Share of the learning rate still alive at the cap. Never zero - an exact wall makes the
        /// last level cost a whole career - but low enough that pushing past 330 costs several.
        /// </summary>
        /// <summary>
        /// How much learning rate is left at the cap. The harsh end is pushed right down onto the
        /// floor the smoothing imposes, so 5 really is the steepest this curve has - vanilla is
        /// harsher still, but only by reaching exactly zero, which is what makes its last level
        /// cost infinity and the reason 329 is vanilla's true maximum.
        /// </summary>
        internal float SummitRateAtCap => Lerp(0.30f, 0.01f, HarshSteps / 9f);


        /// <summary>A long vanilla campaign is about six million raw XP, so that is the unit.</summary>
        internal float CareerXp => CareerLength * 6000000f;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0f, 1f);

        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>
        /// Built once behind a lock and then only read.
        /// </summary>
        /// <remarks>
        /// <c>_lookup ??= new Dictionary(...)</c> looks harmless and is not. This is reached from
        /// the XP prefix, which the save loader drives across several threads at once: one thread
        /// can be filling the dictionary while another reads it, and a Dictionary torn that way
        /// does not throw - it loops forever inside its own bucket walk. That presents as the
        /// game freezing with no exception and no stack, which is exactly what the crash dumps
        /// showed.
        /// </remarks>
        private volatile Dictionary<string, Func<float>>? _lookup;
        private readonly object _lookupGate = new object();

        /// <summary>
        /// Naval skills are matched by string id rather than a reference, so this still builds and
        /// runs without the naval content installed - those ids simply never turn up.
        /// </summary>
        public float MultiplierFor(SkillObject skill)
        {
            var lookup = _lookup;
            if (lookup == null)
            {
                lock (_lookupGate)
                {
                    lookup = _lookup ?? Build();
                    _lookup = lookup;
                }
            }

            return lookup.TryGetValue(skill.StringId, out var value) ? value() : 1f;
        }

        private Dictionary<string, Func<float>> Build()
        {
            return new Dictionary<string, Func<float>>
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

        }

        /// <summary>The debug dropdown, in the order the XP rate sliders use.</summary>
        private static readonly string[] DebugSkillNames =
        {
            "One Handed", "Two Handed", "Polearm", "Bow", "Crossbow", "Throwing", "Riding", "Athletics",
            "Smithing", "Scouting", "Roguery", "Medicine", "Leadership", "Steward", "Tactics", "Charm",
            "Engineering", "Trade", "Mariner", "Boatswain", "Shipmaster"
        };

        private volatile Dictionary<string, Func<SkillObject?>>? _debugSkills;
        private readonly object _debugSkillsGate = new object();

        /// <summary>
        /// The skill the debug dropdown currently names, or null if this game does not have it.
        /// </summary>
        /// <remarks>
        /// Resolved on demand rather than stored, because the settings object is built before any
        /// game is loaded and the skill registry does not exist yet at that point. The naval three
        /// go through the object manager for the same reason the multiplier table matches them by
        /// id: they are simply absent without the content.
        /// </remarks>
        internal SkillObject? DebugSkillObject()
        {
            var table = _debugSkills;
            if (table == null)
            {
                lock (_debugSkillsGate) { table = _debugSkills ?? BuildDebugSkills(); _debugSkills = table; }
            }

            var label = DebugSkill?.SelectedValue;
            if (string.IsNullOrEmpty(label)) return null;
            return table.TryGetValue(label!, out var resolve) ? resolve() : null;
        }

        private Dictionary<string, Func<SkillObject?>> BuildDebugSkills()
        {
            return new Dictionary<string, Func<SkillObject?>>
            {
                ["One Handed"] = () => DefaultSkills.OneHanded,
                ["Two Handed"] = () => DefaultSkills.TwoHanded,
                ["Polearm"] = () => DefaultSkills.Polearm,
                ["Bow"] = () => DefaultSkills.Bow,
                ["Crossbow"] = () => DefaultSkills.Crossbow,
                ["Throwing"] = () => DefaultSkills.Throwing,
                ["Riding"] = () => DefaultSkills.Riding,
                ["Athletics"] = () => DefaultSkills.Athletics,
                ["Smithing"] = () => DefaultSkills.Crafting,
                ["Scouting"] = () => DefaultSkills.Scouting,
                ["Roguery"] = () => DefaultSkills.Roguery,
                ["Medicine"] = () => DefaultSkills.Medicine,
                ["Leadership"] = () => DefaultSkills.Leadership,
                ["Steward"] = () => DefaultSkills.Steward,
                ["Tactics"] = () => DefaultSkills.Tactics,
                ["Charm"] = () => DefaultSkills.Charm,
                ["Engineering"] = () => DefaultSkills.Engineering,
                ["Trade"] = () => DefaultSkills.Trade,
                ["Mariner"] = () => Named("Mariner"),
                ["Boatswain"] = () => Named("Boatswain"),
                ["Shipmaster"] = () => Named("Shipmaster")
            };

        }

        private static SkillObject? Named(string id)
        {
            try
            {
                return MBObjectManager.Instance?.GetObject<SkillObject>(id);
            }
            catch
            {
                return null;
            }
        }
    }
}
