using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// Lets training shape the body, and widens the band vanilla holds it in.
    /// </summary>
    /// <remarks>
    /// Vanilla already drifts the player daily, in <c>DynamicBodyCampaignBehavior</c>: build rises
    /// while you are fighting and decays while you are not. That is worth keeping, and is kept -
    /// this runs after it, not instead of it, and reapplies vanilla's own step before scaling it.
    ///
    /// What is not kept is its clamp on build. Vanilla holds it to thirty percent either side of
    /// what character creation rolled, so a character created slight can never train into a heavy
    /// one: a starting build of 0.3 tops out at 0.39, which would put the muscular tier out of
    /// reach for half of all starting characters. Build is freed across the whole range, and
    /// Athletics multiplies the days it climbs.
    ///
    /// Weight is not touched at all: vanilla's own handling of it stands, band and all.
    ///
    /// Nothing here is allowed to throw. Every entry point is wrapped, every figure is checked for
    /// NaN, and the result is clamped to the engine's own 0..1 whatever the settings say - a body
    /// value out of range is the kind of fault that corrupts a character rather than logging an
    /// error, so the failure mode is always "leave the hero as it was".
    /// </remarks>
    internal static class BodyDrift
    {
        /// <summary>The value anything unusable falls back to.</summary>
        private const float Neutral = 0.5f;

        internal const string BehaviorName =
            "TaleWorlds.CampaignSystem.CampaignBehaviors.DynamicBodyCampaignBehavior";

        // Vanilla's own daily figures, read from the shipped behavior rather than copied, so a game
        // update that retunes them carries straight over. The literals are only the fallback for a
        // version where the fields have been renamed away.
        /// <summary>Athletics at or above this counts double. Fixed: the feature has no dials.</summary>
        internal const int TrainingCap = 200;

        /// <summary>
        /// The share of vanilla's daily build figures this mod uses, both directions alike.
        /// </summary>
        /// <remarks>
        /// A share, not a rate: vanilla's +0.025 and -0.015 are multiplied by it, giving roughly
        /// +0.0076 and -0.0046 a day here.
        ///
        /// Vanilla moves build fast enough that a fighting character reaches the top of the bar
        /// inside a month, which makes the muscular tier the default state of any campaign rather
        /// than something a character grew into. Solved instead so that a fully trained character
        /// fighting on seven days in ten crosses the whole bar in one campaign year - 84 days, at
        /// 7 days to the week and 3 weeks to the season. An untrained one takes three years, so
        /// the training multiplier decides whether you get there at all rather than merely when.
        ///
        /// Both directions are scaled by the same figure deliberately. Shrinking the gain alone
        /// would leave the decay many times larger than a day's climbing, and build could never
        /// rise at all.
        /// </remarks>
        internal const float DriftShare = 0.304f;

        /// <summary>What one point of Vigor adds to what Athletics is worth.</summary>
        internal const float VigorShare = 0.05f;


        private const float FallbackBuildIncrease = 0.025f;
        private const float FallbackBuildDecrease = -0.015f;

        private static bool _read;
        private static float _buildIncrease = FallbackBuildIncrease;
        private static float _buildDecrease = FallbackBuildDecrease;

        internal static Type? Behavior
        {
            get
            {
                try { return AccessTools.TypeByName(BehaviorName); }
                catch (Exception exception) { Guard.Report("BodyDrift.Behavior", exception); return null; }
            }
        }

        /// <summary>The daily build step vanilla would apply, as a positive magnitude.</summary>
        internal static float BuildIncrease { get { ReadVanillaFigures(); return _buildIncrease; } }


        /// <summary>
        /// Pulls vanilla's five daily constants out of the shipped behavior.
        /// </summary>
        /// <remarks>
        /// They are <c>private const</c>, which the compiler still writes into metadata as literal
        /// fields, so <c>GetRawConstantValue</c> reaches them without the game running. Reading
        /// them beats hard-coding: this mod expresses its own contribution as a share of vanilla's
        /// pace, and that share should stay honest across a patch.
        /// </remarks>
        private static void ReadVanillaFigures()
        {
            if (_read) return;
            _read = true;

            try
            {
                var type = Behavior;
                if (type == null)
                {
                    Log.Write("Body drift: vanilla behavior not found, using fallback figures.");
                    return;
                }

                _buildIncrease = Constant(type, "DailyBuildIncrease", FallbackBuildIncrease);
                _buildDecrease = Constant(type, "DailyBuildDecrease", FallbackBuildDecrease);

                Log.Write($"Body drift: vanilla build figures +{_buildIncrease}/{_buildDecrease}");
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.ReadVanillaFigures", exception);
            }
        }

        private static float Constant(Type type, string name, float fallback)
        {
            try
            {
                var field = AccessTools.Field(type, name);
                if (field == null || !field.IsLiteral) return fallback;

                var value = field.GetRawConstantValue();
                if (value is float number && Usable(number)) return number;
                return fallback;
            }
            catch (Exception exception)
            {
                Guard.Report("BodyDrift.Constant:" + name, exception);
                return fallback;
            }
        }


        /// <summary>Vanilla's build step for today, at this mod's slower pace.</summary>
        internal static float VanillaBuildStep(bool foughtRecently)
        {
            ReadVanillaFigures();
            return (foughtRecently ? _buildIncrease : _buildDecrease) * DriftShare;
        }

        /// <summary>
        /// What training multiplies a day of building up by.
        /// </summary>
        /// <remarks>
        /// Applied to vanilla's daily step rather than paid out per skill level, because a level and
        /// a day are not the same clock. On this curve an Athletics level late on can take longer
        /// than a season, while the decay is charged every single day - so a per-level payment is
        /// noise against it, and the body would track how recently you fought rather than how hard
        /// you trained.
        ///
        /// One times at an untrained skill, two at the cap. Vigor amplifies what Athletics is
        /// worth rather than standing on its own, so at the cap and Vigor 10 a day of building up
        /// counts for two and a half. An untrained character gets nothing from Vigor at all, which is the
        /// point: the attribute is what your training is worth, not a substitute for it.
        ///
        /// Only the positive step is multiplied. Decay is untouched, so a character who never
        /// fights still wastes away however trained they are.
        /// </remarks>
        internal static float TrainingMultiplier(int athletics, int vigor)
        {
            try
            {
                var share = Math.Min(Math.Max(athletics, 0), TrainingCap) / (float)TrainingCap;
                var multiplier = 1f + share * (1f + VigorShare * Math.Max(0, vigor));

                return Usable(multiplier) ? Math.Max(0f, multiplier) : 1f;
            }
            catch (Exception exception) { Guard.Report("BodyDrift.TrainingMultiplier", exception); return 1f; }
        }



        /// <summary>Build is free across the whole range; only the engine's own bounds apply.</summary>
        internal static float SettleBuild(float value, float step)
        {
            if (!Usable(value)) return Neutral;
            if (!Usable(step)) step = 0f;

            var moved = value + step;
            return Usable(moved) ? Clamp01(moved) : Clamp01(value);
        }

        private static bool Usable(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Sane(float? value, float fallback) =>
            value.HasValue && Usable(value.Value) ? value.Value : fallback;

        private static float Clamp01(float value) =>
            Usable(value) ? Math.Min(1f, Math.Max(0f, value)) : Neutral;
    }
}
