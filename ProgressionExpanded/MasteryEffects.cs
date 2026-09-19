using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ProgressionExpanded
{
    /// <summary>
    /// The personal rewards, declared as the game's own kind of thing.
    /// </summary>
    /// <remarks>
    /// A <see cref="SkillEffect"/> is not privileged: the character screen builds its effect list
    /// by walking <c>SkillEffect.All</c>, which is just <c>Campaign.AllSkillEffects</c>, and prints
    /// each one through the game's own formatter. Registering ours there means they are listed and
    /// worded exactly like "Simulation advantage: +33.0%" with nothing patched into the UI at all.
    ///
    /// They rise evenly with the skill, like every other effect in the game: the <c>bonus</c>
    /// handed to <c>Initialize</c> is simply the value at the cap divided by it. The one thing
    /// <see cref="EffectCeilingPatch"/> still does is set where vanilla is allowed to end up --
    /// its Tactics sacrifice reduction is lifted from a third to a half.
    /// </remarks>
    internal static class MasteryEffects
    {
        /// <summary>Enemy parties want less to do with you.</summary>
        internal static SkillEffect? Avoidance { get; private set; }

        /// <summary>How much further away your own judgement reads a stronghold's defences.</summary>
        internal static SkillEffect? Intelligence { get; private set; }

        /// <summary>The same, brought back by whoever scouts for the party.</summary>
        internal static SkillEffect? ScoutIntelligence { get; private set; }

        /// <summary>How much larger your reinforcement waves arrive in a battle you fight yourself.</summary>
        internal static SkillEffect? Reinforcements { get; private set; }

        /// <summary>Extra bite from a knife you are allowed to carry into a town.</summary>
        internal static SkillEffect? CivilianDamage { get; private set; }

        /// <summary>What your alleys take in.</summary>
        internal static SkillEffect? AlleyIncome { get; private set; }

        /// <summary>How little heat they draw while doing it.</summary>
        internal static SkillEffect? AlleyQuiet { get; private set; }




        /// <summary>How often a caravan or workshop has an unusually good day.</summary>
        internal static SkillEffect? DoubleIncomeChance { get; private set; }

        /// <summary>How often a bandit party would rather work for you than fight you.</summary>
        internal static SkillEffect? BanditJoin { get; private set; }




        /// <summary>What each effect is worth once the skill is capped.</summary>
        private static readonly ConcurrentDictionary<SkillEffect, float> Targets = new ConcurrentDictionary<SkillEffect, float>();

        /// <summary>The ones we built, and so the only ones we may take away again.</summary>
        private static readonly List<SkillEffect> Created = new List<SkillEffect>();


        private const float AvoidanceAtCap = 0.50f;
        private const float IntelligenceAtCap = 60f;      // the game's own MaximumSeeingRange
        private const float ReinforcementsAtCap = 0.20f;
        private const float CivilianDamageAtCap = 0.10f;
        private const float AlleyIncomeAtCap = 0.30f;
        private const float AlleyQuietAtCap = -0.50f;
        private const float AlleyCapacityAtCap = 5f;
        private const float AlleyWarningAtCap = 0.33f;
        private const float AlleyRecruitsAtCap = 0.30f;
        private const float DoubleIncomeChanceAtCap = 0.15f;
        private const float BanditJoinAtCap = 0.10f;

        private static bool _registered;

        /// <summary>Adds ours to the campaign's list, once per campaign.</summary>
        /// <summary>
        /// Brings the campaign's effect list into line with the setting, either way.
        /// </summary>
        /// <remarks>
        /// Turning the bonuses off has to take them off the character screen too, not merely zero
        /// them -- a row reading "+0%" is worse than no row. Since the screen builds its list by
        /// walking <c>SkillEffect.All</c>, withdrawing ours from that list is what hides them, and
        /// putting them back is what brings them round again.
        /// </remarks>
        internal static void Sync()
        {
            var settings = Settings.Instance;
            // Registering adds to the campaign's list, which is exactly what the game does itself.
            // Taking things back out of it is not, and is not worth the risk at load time, so the
            // off state is handled by zeroing the values and dropping the rows on the screen.
            var wanted = settings != null && settings.Enabled && settings.SkillBonuses;
            if (wanted && !_registered) Register();
        }

        internal static void Register()
        {
            try
            {
                var campaign = Campaign.Current;
                if (campaign == null) return;

                // MBReadOnlyList<T> derives from List<T>, so the campaign's own registry can be
                // appended to directly rather than rebuilt.
                var settings = Settings.Instance;
                if (!(SkillEffect.All is List<SkillEffect> all)) { Log.Write("Effect list is not a List; skipping registration"); return; }
                Log.Write($"Registering into an effect list of {all.Count}");
                if (_registered && all.Contains(Avoidance!)) return;

                Targets.Clear();
                Created.Clear();

                Avoidance = Add(all, "ProgressionExpanded_TacticsAvoidance",
                    "{=MCav}Enemy reluctance to engage you +{a0}%",
                    DefaultSkills.Tactics, AvoidanceAtCap, EffectIncrementType.AddFactor);

                Intelligence = Add(all, "ProgressionExpanded_TacticsIntelligence",
                    "{=MCin}Fortification assessment range +{a0}",
                    DefaultSkills.Tactics, IntelligenceAtCap, EffectIncrementType.Add);

                ScoutIntelligence = Add(all, "ProgressionExpanded_ScoutIntelligence",
                    "{=MCsi}Fortification assessment range +{a0}",
                    DefaultSkills.Scouting, IntelligenceAtCap, EffectIncrementType.Add, PartyRole.Scout);

                Reinforcements = Add(all, "ProgressionExpanded_TacticsReinforcements",
                    "{=MCre}Reinforcement wave size +{a0}%",
                    DefaultSkills.Tactics, ReinforcementsAtCap, EffectIncrementType.AddFactor);

                CivilianDamage = Add(all, "ProgressionExpanded_RogueryCivilianDamage",
                    "{=MCcd}Civilian weapon damage +{a0}%",
                    DefaultSkills.Roguery, CivilianDamageAtCap, EffectIncrementType.AddFactor);

                AlleyIncome = Add(all, "ProgressionExpanded_AlleyIncome",
                    "{=MCai}Alley income +{a0}%",
                    DefaultSkills.Roguery, AlleyIncomeAtCap, EffectIncrementType.AddFactor);

                AlleyQuiet = Add(all, "ProgressionExpanded_AlleyQuiet",
                    "{=MCaq}Alley crime rating -{a0}%",
                    DefaultSkills.Roguery, AlleyQuietAtCap, EffectIncrementType.AddFactor);





                // Only listed when it is actually switched on; a row promising something the
                // setting has turned off is worse than no row.
                if (settings != null && settings.BanditsMayJoin)
                    BanditJoin = Add(all, "ProgressionExpanded_BanditJoin",
                    "{=MCbj}Chance a bandit party joins you instead of fighting +{a0}%",
                        DefaultSkills.Roguery, BanditJoinAtCap, EffectIncrementType.AddFactor);

                DoubleIncomeChance = Add(all, "ProgressionExpanded_DoubleIncome",
                    "{=MCdi}Chance a caravan or workshop pays double +{a0}%",
                    DefaultSkills.Trade, DoubleIncomeChanceAtCap, EffectIncrementType.AddFactor);

                // Vanilla's own, reshaped rather than duplicated. 50% at the cap instead of 33%.
                var sacrifice = DefaultSkillEffects.TacticsTroopSacrificeReduction;
                if (sacrifice != null) Targets[sacrifice] = -TacticsBonus.LossesAtCap;

                _registered = true;
                Log.Write($"Registered {Targets.Count - 1} personal effects.");
            }
            catch (Exception exception)
            {
                Log.Write($"Could not register the personal effects: {exception}");
            }
        }

        private static SkillEffect Add(ICollection<SkillEffect> all, string id, string description,
                                       SkillObject skill, float atCap, EffectIncrementType increment,
                                       PartyRole role = PartyRole.Personal)
        {
            var effect = new SkillEffect(id);
            effect.Initialize(new TextObject(description), skill, role,
                              atCap / Curve.SkillCap, increment, 0f, float.MinValue, float.MaxValue);
            all.Add(effect);
            Created.Add(effect);
            Targets[effect] = atCap;
            return effect;
        }

        /// <summary>Our value for an effect we own, or null if it is none of our business.</summary>
        internal static float? Shaped(SkillEffect? effect, int skillLevel)
        {
            if (effect == null || Targets.Count == 0) return null;
            if (!Targets.TryGetValue(effect, out var atCap)) return null;

            var settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.SkillBonuses) return 0f;

            var reached = Math.Max(0, Math.Min(skillLevel, Curve.SkillCap));
            return atCap * reached / Curve.SkillCap;
        }

        /// <summary>What one of ours is currently worth to the player.</summary>
        internal static float PlayerValue(SkillEffect? effect) => ValueFor(effect, SafeMainHero());

        /// <summary>
        /// A straight line off a skill, for bonuses that are real but not worth a row.
        /// </summary>
        /// <remarks>
        /// Roguery would otherwise carry eleven rows and overflow its panel. These three are the
        /// least interesting to read and the least missed, so they work without being registered
        /// -- which also means nothing has to edit the screen's list after the game has built it.
        /// </remarks>
        internal static float FromSkill(SkillObject? skill, float atCap)
        {
            try
            {
                var settings = Settings.Instance;
                if (settings == null || !settings.Enabled || !settings.SkillBonuses) return 0f;
                if (skill == null || Campaign.Current == null) return 0f;

                var hero = Hero.MainHero;
                if (hero == null) return 0f;

                var level = Math.Max(0, Math.Min(Curve.SkillCap, hero.GetSkillValue(skill)));
                return atCap * level / Curve.SkillCap;
            }
            catch
            {
                return 0f;
            }
        }

        internal static float AlleyCapacityValue() => FromSkill(DefaultSkills.Roguery, AlleyCapacityAtCap);
        internal static float AlleyWarningValue() => FromSkill(DefaultSkills.Roguery, AlleyWarningAtCap);
        internal static float AlleyRecruitsValue() => FromSkill(DefaultSkills.Roguery, AlleyRecruitsAtCap);

        /// <summary>What one of ours is worth in a particular pair of hands.</summary>
        internal static float ValueFor(SkillEffect? effect, Hero? hero)
        {
            try
            {
                if (effect == null || hero == null || Campaign.Current == null) return 0f;
                return effect.GetSkillEffectValue(hero.GetSkillValue(effect.EffectedSkill));
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// How far a stronghold can be read, by your own eye or your scout's -- whichever is better.
        /// </summary>
        /// <remarks>
        /// The scouting half deliberately goes through the party role rather than the player, so
        /// it is the hero actually doing the scouting who earns it. Leave the slot empty and the
        /// game hands the role back to the party leader, which means it falls to you at your own
        /// Scouting -- the same rule vanilla uses for every other party role.
        /// </remarks>
        internal static float IntelReach()
        {
            var mine = PlayerValue(Intelligence);

            var scout = 0f;
            try
            {
                var party = MobileParty.MainParty;
                if (party != null) scout = ValueFor(ScoutIntelligence, party.EffectiveScout);
            }
            catch
            {
                // No party yet; the player's own reach still stands.
            }

            return Math.Max(mine, scout);
        }

        private static Hero? SafeMainHero()
        {
            try
            {
                return Campaign.Current == null ? null : Hero.MainHero;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Drops everything remembered from a previous campaign in this session.
        /// </summary>
        /// <remarks>
        /// All of this is static, and the game keeps the process alive across campaigns. Without
        /// this, starting or loading a second campaign leaves <c>_registered</c> true -- so the new
        /// campaign never gets its effects -- while <c>Targets</c> still holds SkillEffect objects
        /// belonging to a campaign that no longer exists, which are then read on every skill query.
        /// </remarks>
        internal static void Forget()
        {
            _registered = false;
            Targets.Clear();
            Created.Clear();
            Avoidance = null;
            Intelligence = null;
            ScoutIntelligence = null;
            Reinforcements = null;
            CivilianDamage = null;
            AlleyIncome = null;
            AlleyQuiet = null;
            DoubleIncomeChance = null;
            BanditJoin = null;
        }
    }
}
