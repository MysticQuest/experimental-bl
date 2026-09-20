using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// Lets training shape the body, and widens the band vanilla holds it in.
    /// </summary>
    /// <remarks>
    /// Vanilla already drifts the player daily, in <c>DynamicBodyCampaignBehavior</c>: build rises
    /// while you are fighting and decays while you are not, weight rises while you are in town and
    /// falls on the road. That is worth keeping, and is kept - this runs after it, not instead of
    /// it, and reapplies vanilla's own step before adding anything.
    ///
    /// What is not kept is its clamp on build. Vanilla holds it to thirty percent either side of
    /// what character creation rolled, so a character created slight can never train into a heavy
    /// one: a starting build of 0.3 tops out at 0.39, which would put the muscular tier out of
    /// reach for half of all starting characters. Build is freed across the whole range, and
    /// Athletics multiplies the days it climbs.
    ///
    /// Weight follows vanilla apart from two things: the band is half either side of what character
    /// creation rolled rather than vanilla's thirty percent, and a day spent fighting or at the
    /// forge does not put weight on you.
    ///
    /// Nothing here is allowed to throw. Every entry point is wrapped, every figure is checked for
    /// NaN, and the result is clamped to the engine's own 0..1 whatever the settings say - a body
    /// value out of range is the kind of fault that corrupts a character rather than logging an
    /// error, so the failure mode is always "leave the hero as it was".
    /// </remarks>
    internal static class BodyDrift
    {
        /// <summary>The middle of the weight bar, which training settles you toward.</summary>
        internal const float WeightCentre = 0.5f;

        internal const string BehaviorName =
            "TaleWorlds.CampaignSystem.CampaignBehaviors.DynamicBodyCampaignBehavior";

        // Vanilla's own daily figures, read from the shipped behavior rather than copied, so a game
        // update that retunes them carries straight over. The literals are only the fallback for a
        // version where the fields have been renamed away.
        /// <summary>Athletics at or above this counts double. Fixed: the feature has no dials.</summary>
        internal const int TrainingCap = 200;

        /// <summary>What one point of Vigor adds to what Athletics is worth.</summary>
        internal const float VigorShare = 0.05f;

        /// <summary>How far weight may stray either side of what character creation rolled.</summary>
        internal const float WeightSpread = 0.5f;

        private const float FallbackBuildIncrease = 0.025f;
        private const float FallbackBuildDecrease = -0.015f;
        private const float FallbackWeightIncrease = 0.025f;
        private const float FallbackWeightDecrease = -0.025f;
        private const float FallbackWeightStarving = -0.1f;

        private static bool _read;
        private static float _buildIncrease = FallbackBuildIncrease;
        private static float _buildDecrease = FallbackBuildDecrease;
        private static float _weightIncrease = FallbackWeightIncrease;
        private static float _weightDecrease = FallbackWeightDecrease;
        private static float _weightStarving = FallbackWeightStarving;

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

        /// <summary>The daily weight step vanilla would apply, as a positive magnitude.</summary>
        internal static float WeightIncrease { get { ReadVanillaFigures(); return _weightIncrease; } }

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
                _weightIncrease = Constant(type, "DailyWeightIncrease", FallbackWeightIncrease);
                _weightDecrease = Constant(type, "DailyWeightDecreaseWhenNotStarving", FallbackWeightDecrease);
                _weightStarving = Constant(type, "DailyWeightDecreaseWhenStarving", FallbackWeightStarving);

                Log.Write($"Body drift: vanilla figures build +{_buildIncrease}/{_buildDecrease}, "
                          + $"weight +{_weightIncrease}/{_weightDecrease}/{_weightStarving}");
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

        /// <summary>Vanilla's weight step for today, recomputed from its own conditions.</summary>
        internal static float VanillaWeightStep(bool visitedSettlement, bool starving, bool inSettlement)
        {
            ReadVanillaFigures();
            if (starving && !inSettlement) return _weightStarving;
            return visitedSettlement ? _weightIncrease : _weightDecrease;
        }

        /// <summary>Vanilla's build step for today.</summary>
        internal static float VanillaBuildStep(bool foughtRecently)
        {
            ReadVanillaFigures();
            return foughtRecently ? _buildIncrease : _buildDecrease;
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

        /// <summary>
        /// The band a value is allowed to sit in, with the anchor falling back when it is unusable.
        /// </summary>
        /// <remarks>
        /// Vanilla seeds its anchors to -1 and only fills them on character creation, a body edit,
        /// or a change of player character. On a save where none of those has fired, the anchor is
        /// still -1 and vanilla's own band comes out as min 0 against max -1.3 - a floor above its
        /// own ceiling. An unusable anchor therefore falls back to what the hero currently is,
        /// which is always a sane centre, and to the middle of the bar if even that is broken.
        /// </remarks>
        private static void Band(float anchor, float current, float spread, out float min, out float max)
        {
            if (!Usable(anchor) || anchor <= 0f) anchor = current;
            if (!Usable(anchor) || anchor <= 0f) anchor = WeightCentre;
            if (!Usable(spread) || spread < 0f) spread = 0.5f;

            min = Math.Max(0f, anchor * (1f - spread));
            max = Math.Min(1f, anchor * (1f + spread));

            // A band inverts once spread passes one; nearest-in-bounds still needs an order.
            if (min > max) { var swap = min; min = max; max = swap; }
        }

        /// <summary>Moves a value by a step and settles it at the nearest point inside the band.</summary>
        internal static float Settle(float value, float step, float anchor, float spread)
        {
            if (!Usable(value)) return WeightCentre;
            if (!Usable(step)) step = 0f;

            var moved = value + step;
            if (!Usable(moved)) return Clamp01(value);

            Band(anchor, value, spread, out var min, out var max);
            return Clamp01(Math.Min(Math.Max(moved, min), max));
        }

        /// <summary>Build is free across the whole range; only the engine's own bounds apply.</summary>
        internal static float SettleBuild(float value, float step)
        {
            if (!Usable(value)) return WeightCentre;
            if (!Usable(step)) step = 0f;

            var moved = value + step;
            return Usable(moved) ? Clamp01(moved) : Clamp01(value);
        }

        private static bool Usable(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Sane(float? value, float fallback) =>
            value.HasValue && Usable(value.Value) ? value.Value : fallback;

        private static float Clamp01(float value) =>
            Usable(value) ? Math.Min(1f, Math.Max(0f, value)) : WeightCentre;
    }
}
