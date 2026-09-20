using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProgressionExpanded
{
    /// <summary>
    /// What the body you are carrying is worth.
    /// </summary>
    /// <remarks>
    /// Five readings off two axes and a half. Weight splits into lean and heavy, build into
    /// muscular and its opposite, height into tall and short. Only four of those six pay: low build
    /// deliberately earns nothing, because build is the one figure training can move freely, and a
    /// tier that paid for being unbuilt would make the training pointless.
    ///
    /// Each reading is the bar itself, not the distance from its middle, so the two ends of an axis
    /// always sum to one: an average body draws half of both, and committing to one end trades the
    /// other away rather than buying something from nothing. Ten percent at the far end is the
    /// ceiling for most of them; the forge is the exception, in stamina points rather than a share.
    ///
    /// Height is read out of the face key rather than stored separately - vanilla keeps it as six
    /// bits in <c>KeyPart8</c>, which <c>BodyProperties.ClampHeightMultiplierFaceKey</c> is the only
    /// shipped code to touch. It is fixed at character creation and nothing drifts it, which is why
    /// both of its ends can pay: unlike build, there is no training to discourage.
    /// </remarks>
    internal static class Physique
    {
        /// <summary>The middle of every bar, where a body is worth nothing either way.</summary>
        private const float Centre = 0.5f;

        /// <summary>Where vanilla keeps the height multiplier inside the face key.</summary>
        private const int HeightStartBit = 19;
        private const int HeightBitCount = 6;
        private const float HeightSteps = 63f;

        internal static bool Active()
        {
            try
            {
                var settings = Settings.Instance;
                return Mod.On && settings != null && settings.BodyDrift;
            }
            catch { return false; }
        }

        /// <summary>A bonus share for a reading, at its stated ceiling.</summary>
        internal static float Share(float reading, float ceiling)
        {
            if (!Usable(reading) || !Usable(ceiling) || reading <= 0f) return 0f;
            var share = reading * ceiling;
            return Usable(share) ? Math.Max(0f, share) : 0f;
        }

        /// <summary>
        /// The player, and only the player.
        /// </summary>
        /// <remarks>
        /// Every hero has a build and a weight, but only the player's ever moves - vanilla rolls
        /// one for a companion or a lord at creation and never touches it again. Paying tiers to
        /// them would hand out permanent bonuses nobody chose and nobody can train away, and would
        /// quietly make some companions better hires than others for reasons never shown anywhere.
        /// </remarks>
        internal static Hero? HeroOf(Agent? agent)
        {
            try
            {
                var hero = (agent?.Character as CharacterObject)?.HeroObject;
                return hero != null && hero == Hero.MainHero ? hero : null;
            }
            catch { return null; }
        }

        /// <summary>The whole weight bar read backwards: 1 at nothing, a half at average.</summary>
        internal static float Lean(Hero? hero) => Inverse(Weight(hero));

        /// <summary>The same bar read forwards.</summary>
        internal static float Heavy(Hero? hero) => Weight(hero);

        /// <summary>The build bar. Nothing at the bottom, which is the point of it.</summary>
        internal static float Muscular(Hero? hero) => Build(hero);

        internal static float Tall(Hero? hero) => Height(hero);

        internal static float Short(Hero? hero) => Inverse(Height(hero));

        private static float Weight(Hero? hero)
        {
            try { return hero == null ? Centre : Clamp01(hero.Weight); }
            catch { return Centre; }
        }

        private static float Build(Hero? hero)
        {
            try { return hero == null ? Centre : Clamp01(hero.Build); }
            catch { return Centre; }
        }

        /// <summary>
        /// The height multiplier, dug out of the face key.
        /// </summary>
        /// <remarks>
        /// Read only. Writing it back is possible by the same bit arithmetic, but vanilla's own
        /// writer puts the edited word into the seventh constructor slot after reading the eighth,
        /// which looks like a mistake worth not copying - and nothing here needs to move height
        /// anyway.
        /// </remarks>
        internal static float Height(Hero? hero)
        {
            try
            {
                if (hero == null) return Centre;

                var key = hero.StaticBodyProperties;

                // An unpopulated face key is all zeroes, and zero in the height bits is the bottom
                // of the bar - so without this, a hero whose body was never generated reads as the
                // shortest possible person and collects the full short bonus.
                if (Empty(key)) return Centre;

                var mask = (1UL << HeightBitCount) - 1UL;
                var raw = (key.KeyPart8 >> HeightStartBit) & mask;
                return Clamp01(raw / HeightSteps);
            }
            catch (Exception exception)
            {
                Guard.Report("Physique.Height", exception);
                return Centre;
            }
        }

        /// <summary>Whether a face key was ever generated at all.</summary>
        private static bool Empty(StaticBodyProperties key) =>
            key.KeyPart1 == 0UL && key.KeyPart2 == 0UL && key.KeyPart3 == 0UL && key.KeyPart4 == 0UL
            && key.KeyPart5 == 0UL && key.KeyPart6 == 0UL && key.KeyPart7 == 0UL && key.KeyPart8 == 0UL;

        private static float Inverse(float value) =>
            !Usable(value) ? Centre : Clamp01(1f - value);

        private static bool Usable(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Clamp01(float value) =>
            Usable(value) ? Math.Min(1f, Math.Max(0f, value)) : Centre;
    }

    /// <summary>
    /// What a light frame is worth getting going, and a small one keeping low.
    /// </summary>
    /// <remarks>
    /// Lean pays in acceleration rather than top speed. Less mass is less to get moving, which is
    /// what a light frame actually buys - a heavy man reaches the same pace eventually. The driven
    /// property is how long reaching top speed takes, so the bonus divides it: shorter is quicker.
    ///
    /// Short pays in crouched movement, where a small frame is genuinely at an advantage and a
    /// large one is not.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.UpdateAgentStats))]
    internal static class PhysiqueAgentPatch
    {
        /// <summary>How much quicker a fully lean frame reaches its pace.</summary>
        internal const float LeanAcceleration = 0.10f;

        /// <summary>How much faster a fully short frame moves crouched.</summary>
        internal const float ShortCrouch = 0.20f;

        [HarmonyPostfix]
        private static void Apply(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            Guard.Touch("PhysiqueAgent");
            if (agent == null || agentDrivenProperties == null || agent.IsMount || !Physique.Active()) return;

            try
            {
                var hero = Physique.HeroOf(agent);
                if (hero == null) return;

                var lean = Physique.Share(Physique.Lean(hero), LeanAcceleration);
                if (lean > 0f)
                {
                    // Dividing rather than subtracting keeps it a share however the game retunes
                    // the base figure, and can never drive the duration to zero or below.
                    var duration = agentDrivenProperties.TopSpeedReachDuration;
                    if (duration > 0f) agentDrivenProperties.TopSpeedReachDuration = duration / (1f + lean);
                }

                var small = Physique.Share(Physique.Short(hero), ShortCrouch);
                if (small > 0f) agentDrivenProperties.CrouchedSpeedMultiplier *= 1f + small;
            }
            catch
            {
                // Runs every frame for every agent; a throw here is far worse than a missing bonus.
            }
        }
    }

    /// <summary>
    /// Mass you cannot be moved by.
    /// </summary>
    /// <remarks>
    /// Heavy is deliberately narrow, and defensive only: how hard you are to shove off your line,
    /// and how hard you are to interrupt mid-swing. Dismounting is left alone - it belongs to the
    /// height axis at both ends, and paying the same stat from two tiers made a heavy short
    /// character almost impossible to unhorse.
    /// </remarks>
    [HarmonyPatch]
    internal static class PhysiqueFootingPatch
    {
        /// <summary>What a fully heavy frame adds to staying where it was put.</summary>
        internal const float HeavyFooting = 0.10f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel),
            nameof(SandboxAgentStatCalculateModel.GetKnockBackResistance))]
        private static void StayPut(Agent agent, ref float __result) => Steady(agent, ref __result);

        /// <summary>What a fully short frame adds to staying in the saddle, on top of mass.</summary>
        internal const float ShortDismount = 0.15f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxAgentStatCalculateModel),
            nameof(SandboxAgentStatCalculateModel.GetDismountResistance))]
        private static void StaySeated(Agent agent, ref float __result)
        {
            Guard.Touch("PhysiqueSeat");
            try
            {
                if (!Physique.Active() || agent == null || __result <= 0f) return;

                var small = Physique.Share(Physique.Short(Physique.HeroOf(agent)), ShortDismount);
                if (small > 0f) __result *= 1f + small;
            }
            catch
            {
                // Read whenever a blow might unhorse someone.
            }
        }

        private static void Steady(Agent agent, ref float result)
        {
            Guard.Touch("PhysiqueFooting");

            try
            {
                if (!Physique.Active() || agent == null || result <= 0f) return;

                var heavy = Physique.Share(Physique.Heavy(Physique.HeroOf(agent)), HeavyFooting);
                if (heavy > 0f) result *= 1f + heavy;
            }
            catch
            {
                // Read for every blow that might move someone; a missing bonus beats a throw.
            }
        }
    }

    /// <summary>The same mass, keeping the weapon steady through a blow that lands.</summary>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage))]
    internal static class PhysiqueStaggerPatch
    {
        [HarmonyPostfix]
        private static void Steady(Agent defenderAgent, ref float __result)
        {
            Guard.Touch("PhysiqueStagger");

            try
            {
                if (!Physique.Active() || defenderAgent == null) return;

                var heavy = Physique.Share(Physique.Heavy(Physique.HeroOf(defenderAgent)),
                                           PhysiqueFootingPatch.HeavyFooting);
                if (heavy > 0f) __result *= 1f + heavy;
            }
            catch
            {
                // Runs for every blow landed in a battle.
            }
        }
    }

    /// <summary>
    /// What muscle is worth at the end of an arm, thrown or swung.
    /// </summary>
    /// <remarks>
    /// Both halves are the same ten percent and the same reading; they are separate settings only
    /// because a thrown weapon and a swung one are different enough that someone will want to tune
    /// them apart. Bows and crossbows are deliberately excluded - draw weight was considered and
    /// dropped, so a missile only counts when the arm threw it.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.ApplyDamageAmplifications))]
    internal static class PhysiqueDamagePatch
    {
        internal const float MuscularMelee = 0.10f;
        internal const float MuscularThrowing = 0.10f;

        [HarmonyPostfix]
        private static void Drive(ref AttackInformation attackInformation, ref float __result)
        {
            Guard.Touch("PhysiqueDamage");
            if (__result <= 0f || !Physique.Active()) return;

            try
            {
                var hero = Physique.HeroOf(attackInformation.AttackerAgent);
                var muscle = Physique.Muscular(hero);
                if (muscle <= 0f) return;

                var thrown = IsThrown(attackInformation.AttackerWeapon);
                var share = Physique.Share(muscle, thrown ? MuscularThrowing : MuscularMelee);
                if (share > 0f) __result *= 1f + share;
            }
            catch
            {
                // Never throw on a damage tick.
            }
        }

        /// <summary>A weapon the arm threw, as opposed to one a string loosed.</summary>
        private static bool IsThrown(MissionWeapon weapon)
        {
            try
            {
                var usage = weapon.CurrentUsageItem;
                if (usage == null) return false;

                switch (usage.WeaponClass)
                {
                    case WeaponClass.Javelin:
                    case WeaponClass.ThrowingAxe:
                    case WeaponClass.ThrowingKnife:
                    case WeaponClass.Stone:
                        return true;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }
    }

    /// <summary>Muscle keeps you at the anvil longer.</summary>
    /// <remarks>
    /// Stamina points rather than a share, because this is the one bonus whose scale the player
    /// reads directly off a bar rather than feeling in a fight.
    /// </remarks>
    [HarmonyPatch(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior.GetMaxHeroCraftingStamina))]
    internal static class PhysiqueForgePatch
    {
        /// <summary>Stamina a fully muscular frame adds.</summary>
        internal const float MuscularForge = 50f;

        [HarmonyPostfix]
        private static void Extend(Hero hero, ref int __result)
        {
            Guard.Touch("PhysiqueForge");

            try
            {
                if (!Physique.Active() || hero == null) return;

                var added = Physique.Share(Physique.Muscular(hero), MuscularForge);
                if (added > 0f) __result += (int)Math.Round(added);
            }
            catch (Exception exception)
            {
                Guard.Report("Physique.Forge", exception);
            }
        }
    }

    /// <summary>
    /// Height at the end of a lever.
    /// </summary>
    /// <remarks>
    /// Reach is already free - the engine scales an agent from its body properties, and
    /// <c>Agent.GetArmLength</c> is <c>Monster.ArmLength * AgentScale</c> - so tall is not paid
    /// again for reaching further. What it is paid for is what the longer lever does to a blow:
    /// the tip of the weapon travels further in the same swing, so it arrives faster. That is
    /// strike magnitude, and it is kept deliberately small because it compounds with the free reach
    /// the engine already grants.
    ///
    /// Swing and thrust both, since a longer arm moves the point as well as the edge.
    ///
    /// Patched on the Sandbox model, not the Default one. <c>SandboxStrikeMagnitudeModel</c>
    /// derives straight from the abstract base rather than from <c>DefaultStrikeMagnitudeModel</c>,
    /// and <c>SandBoxSubModule</c> is what registers it - so a patch on the Default class compiles,
    /// applies, and then never runs in a campaign.
    /// </remarks>
    [HarmonyPatch]
    internal static class PhysiqueStrikePatch
    {
        /// <summary>What a fully tall frame adds to a blow.</summary>
        internal const float TallStrike = 0.05f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxStrikeMagnitudeModel),
            nameof(SandboxStrikeMagnitudeModel.CalculateStrikeMagnitudeForSwing))]
        private static void Swing(ref AttackInformation attackInformation, ref float __result) =>
            Lengthen(ref attackInformation, ref __result);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandboxStrikeMagnitudeModel),
            nameof(SandboxStrikeMagnitudeModel.CalculateStrikeMagnitudeForThrust))]
        private static void Thrust(ref AttackInformation attackInformation, ref float __result) =>
            Lengthen(ref attackInformation, ref __result);

        private static void Lengthen(ref AttackInformation attackInformation, ref float result)
        {
            Guard.Touch("PhysiqueStrike");
            if (result <= 0f || !Physique.Active()) return;

            try
            {
                var tall = Physique.Share(Physique.Tall(Physique.HeroOf(attackInformation.AttackerAgent)),
                                          TallStrike);
                if (tall > 0f) result *= 1f + tall;
            }
            catch
            {
                // Runs for every blow struck; a missing bonus beats a throw.
            }
        }
    }

    /// <summary>Height is also what takes a rider off his horse.</summary>
    /// <remarks>
    /// The offensive counterpart to short staying seated: reaching higher is what lets you get a
    /// hand or a hook onto someone above you. Penetration is weighed against the victim's
    /// resistance, so the two ends of the height bar meet on the same scale.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.GetDismountPenetration))]
    internal static class PhysiqueDismountPatch
    {
        /// <summary>What a fully tall frame adds to pulling someone down.</summary>
        internal const float TallDismount = 0.10f;

        [HarmonyPostfix]
        private static void Reach(Agent attackerAgent, ref float __result)
        {
            Guard.Touch("PhysiqueDismount");
            if (__result <= 0f || !Physique.Active()) return;

            try
            {
                var tall = Physique.Share(Physique.Tall(Physique.HeroOf(attackerAgent)), TallDismount);
                if (tall > 0f) __result *= 1f + tall;
            }
            catch
            {
                // Read whenever a blow might unhorse someone.
            }
        }
    }

    /// <summary>A light frame mends quicker.</summary>
    /// <remarks>
    /// The one body bonus paid on the map rather than in a mission. The model is what
    /// <c>Hero.Heal</c> runs every wound through, so it covers resting, surgery and daily recovery
    /// alike rather than one of them.
    /// </remarks>
    [HarmonyPatch(typeof(DefaultPartyHealingModel),
        nameof(DefaultPartyHealingModel.GetHeroesEffectedHealingAmount))]
    internal static class PhysiqueHealingPatch
    {
        /// <summary>What a fully lean frame adds to mending.</summary>
        internal const float LeanHealing = 0.10f;

        [HarmonyPostfix]
        private static void Mend(Hero hero, ref int __result)
        {
            Guard.Touch("PhysiqueHealing");

            try
            {
                if (!Physique.Active() || hero == null || hero != Hero.MainHero || __result <= 0) return;

                var lean = Physique.Share(Physique.Lean(hero), LeanHealing);
                if (lean <= 0f) return;

                // Rounded up, so a small wound never heals at exactly vanilla's rate by rounding.
                __result = (int)Math.Ceiling(__result * (1f + lean));
            }
            catch (Exception exception)
            {
                Guard.Report("Physique.Mend", exception);
            }
        }
    }

    /// <summary>Muscle carries its own armour.</summary>
    /// <remarks>
    /// The same figure the Form Fitting Armour perk reduces, reduced a little further - so this
    /// stacks with the perk rather than competing with it, and a strong character in a perk build
    /// gets both. Encumbrance feeds speed, acceleration and the weapon handling calculation all at
    /// once, which is why a tenth off it is worth more than a tenth of any single stat.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel),
        nameof(SandboxAgentStatCalculateModel.GetEffectiveArmorEncumbrance))]
    internal static class PhysiqueEncumbrancePatch
    {
        /// <summary>How much of the armour a fully muscular frame stops noticing.</summary>
        internal const float MuscularEncumbrance = 0.10f;

        [HarmonyPostfix]
        private static void Carry(Agent agent, ref float __result)
        {
            Guard.Touch("PhysiqueEncumbrance");
            if (__result <= 0f || agent == null || !Physique.Active()) return;

            try
            {
                var muscle = Physique.Share(Physique.Muscular(Physique.HeroOf(agent)), MuscularEncumbrance);
                if (muscle <= 0f) return;

                // Never below zero: the model floors it there itself, and weightless armour would
                // feed a divide further down the speed calculation.
                __result = Math.Max(0f, __result * (1f - Math.Min(0.9f, muscle)));
            }
            catch
            {
                // Read while an agent is being built.
            }
        }
    }
}
