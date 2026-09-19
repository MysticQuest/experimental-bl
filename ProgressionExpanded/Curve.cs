using System;

namespace ProgressionExpanded
{
    /// <summary>
    /// The progression maths, solved once at campaign start.
    /// </summary>
    /// <remarks>
    /// Two targets and two unknowns: <c>_limitBonus</c> decides how late a master's fade begins,
    /// which fixes the share of effort sitting between skill 275 and 330; <c>_xpScale</c> then
    /// scales the whole skill curve so 330 lands on its intended character level.
    /// </remarks>
    public static class Curve
    {
        public const int SkillCap = 330;
        public const int MaxSkillLevel = 1024;
        public const int MaxCharacterLevel = 62;

        // Learning limit: limit = C0 + Ka*(attr-1) + Kf*focus, plus a bonus only at 10/10.
        // The three coefficients are not hand-tuned; they are solved from the ceilings we want to
        // pin, so those ceilings stay put when the penalty slope changes.
        private const float UntrainedCap = 46f;   // attribute 4, no focus - matches vanilla
        private const float NearMaxCap = 275f;    // the best a build one point short can ever reach

        private static float _c0 = -12.3f;
        private static float _ka = 13.2f;
        private static float _kf = 8.1f;

        /// <summary>
        /// How softly the rate rounds off as it approaches zero, in units of the rate share.
        /// </summary>
        /// <remarks>
        /// The rate is smoothed with a softplus rather than clamped at a floor. A clamp left the
        /// cost per level flat once it bit, and a piecewise hand-off left a visible corner where
        /// the two halves met. Softplus is linear well above zero, decays exponentially well below
        /// it, and is smooth everywhere in between - so the curve has no corners at all and never
        /// reaches zero.
        /// </remarks>
        private const float RateEase = 0.10f;

        /// <summary>Kept for the ceiling tables: the share at which a build is treated as finished.</summary>
        public const float RateFloor = 0.01f;

        /// <summary>Focus point costs escalate: the nth point costs ceil(n/4).</summary>
        private static readonly int[] FocusCostCumulative = { 0, 1, 2, 3, 4, 6, 8, 10, 12, 15, 18 };

        private static int _maxFocus = 10;
        /// <summary>The pace is calibrated against this many specialisations; career length scales it.</summary>
        private const int CalibrationSkills = 3;

        private static float _limitBonus = 58.9f;
        private static float _slope = 0.0875f;
        private static float _masteryLevel = 37f;
        private static float _xpScale = 20f;
        private static float _charScale = 96f;
        private static int[] _skillXp = new int[MaxSkillLevel + 1];

        public static float LimitBonus => _limitBonus;

        /// <summary>The over-limit penalty slope, derived so the wall always lands on the cap.</summary>
        public static float Slope => _slope;

        /// <summary>Where skill 275 lands, in character levels. A result, not a setting.</summary>
        public static float MasteryLevel => _masteryLevel;
        public static float XpScale => _xpScale;

        public static int MaxFocus => _maxFocus;

        /// <summary>
        /// Focus contributes at half vanilla's weight, evenly per point, and normalised to the
        /// maximum actually allowed - so a lower cap gives fewer, bigger steps rather than
        /// quietly costing the player part of what focus is worth.
        /// </summary>
        /// <remarks>
        /// Deliberately flat rather than shaped like <see cref="FocusPointCost"/>: later points
        /// cost more, but every point is worth the same. Tying the two together made the value of
        /// a point depend on where in the sequence it fell, which is not something a player can
        /// read off the screen.
        /// </remarks>
        public static float FocusTerm(int focus)
        {
            if (focus <= 0) return 0f;
            if (focus > _maxFocus) focus = _maxFocus;
            return 5f * focus / _maxFocus;
        }

        public static int FocusPointCost(int nextPoint) => (int)Math.Ceiling(Math.Max(1, nextPoint) / 4.0);

        public static float LimitValue(float attribute, int focus)
        {
            var limit = _c0 + _ka * (attribute - 1f) + _kf * focus;
            if (attribute >= 9.999f && focus >= _maxFocus) limit += _limitBonus;
            return Math.Max(0f, limit);
        }

        public static float BaseFactor(float attribute, int focus) => 1f + 0.4f * attribute + FocusTerm(focus);

        /// <summary>The whole learning rate, as the game would report it.</summary>
        public static float RateFor(float attribute, int focus, float skillValue, float slope, float extraLimit)
            => 1.25f * BaseFactor(attribute, focus) * RateFraction(attribute, focus, skillValue, slope, extraLimit);

        /// <summary>The surviving share of the base rate at this skill level, floored so it never hits zero.</summary>
        public static float RateFraction(float attribute, int focus, float skillValue, float slope)
            => RateFraction(attribute, focus, skillValue, slope, 0f);

        /// <param name="extraLimit">Skill levels added to the limit, which moves the ceiling itself.</param>
        public static float RateFraction(float attribute, int focus, float skillValue, float slope, float extraLimit)
        {
            var b = BaseFactor(attribute, focus);
            var limit = LimitValue(attribute, focus) + Math.Max(0f, extraLimit);
            var factor = b;
            if (skillValue > limit) factor -= 1f + slope * (skillValue - limit);
            var share = factor / b;
            var x = share / RateEase;
            if (x > 30f) return share;                       // already far above the knee
            return RateEase * (float)Math.Log(1.0 + Math.Exp(x));
        }

        public static int SkillXpRequired(int skillLevel)
        {
            if (skillLevel <= 0) return 0;
            if (skillLevel > MaxSkillLevel) skillLevel = MaxSkillLevel;
            return _skillXp[skillLevel];
        }

        /// <summary>
        /// Total earned XP a character level costs.
        /// </summary>
        /// <remarks>
        /// Level 1 costs nothing, which is the one deliberate departure from vanilla's own table.
        /// Vanilla asks for a single point there, so a character who has earned literally nothing
        /// is level 0 until the first scrap of XP arrives - which it does within a few paces of
        /// leaving the first town, and the level pops for no reason the player can see. Starting
        /// at 1 is what everyone assumes is happening anyway.
        /// </remarks>
        public static int CharacterXpRequired(int level)
        {
            if (level <= 1) return 0;
            if (level > MaxCharacterLevel) return int.MaxValue;
            var v = _charScale * level * level * level;
            return v >= int.MaxValue ? int.MaxValue : (int)v;
        }

        /// <summary>Recomputes every table. Cheap enough to run whenever a setting changes.</summary>
        /// <param name="rateAtCap">
        /// What share of the base learning rate is left at the cap. Low enough and the cap is a
        /// wall in practice; exactly zero and the last levels cost a whole career each, which is
        /// why this is stated as a rate rather than a hard stop.
        /// </param>
        public static void Rebuild(float exponent, int level330, float careerXpAtFifty,
                                   float slope, float rateAtCap, int startSkill, int maxFocus)
        {
            _maxFocus = Math.Max(1, Math.Min(10, maxFocus));
            _charScale = careerXpAtFifty / (50f * 50f * 50f);

            _slope = Math.Max(0.02f, slope);
            SolveLimitCoefficients(_slope);

            // Put the fade start where the rate left at the cap comes out as asked.
            var survive = Math.Max(RateFloor, Math.Min(1f, rateAtCap));
            var fullBase = BaseFactor(10f, _maxFocus);
            var start = SkillCap - (fullBase * (1f - survive) - 1f) / _slope;
            _limitBonus = Math.Max(0f, start - (_c0 + _ka * 9f + _kf * _maxFocus));

            var effort = BuildEffort(exponent, _slope);
            var from = effort[Math.Min(startSkill, SkillCap)];
            var span = effort[SkillCap] - from;
            if (span <= 0) span = 1;

            var learningRate = 1.25f * BaseFactor(10f, _maxFocus);
            _xpScale = (float)(learningRate * _charScale * Math.Pow(level330, 3) / (CalibrationSkills * span));

            var toMastery = effort[275] - from;
            _masteryLevel = toMastery <= 0 ? level330
                : (float)(level330 / Math.Pow(span / toMastery, 1.0 / 3.0));

            _skillXp = new int[MaxSkillLevel + 1];
            double running = 0;
            for (var s = 1; s <= MaxSkillLevel; s++)
            {
                running += _xpScale * Math.Pow(s, exponent);
                _skillXp[s] = running >= int.MaxValue ? int.MaxValue : (int)running;
            }
        }

        /// <summary>
        /// Pins three ceilings - untrained, and the two builds a single point short of the
        /// maximum - then solves the limit line through them. Without this the low-attribute
        /// skills drift below the value character creation already handed the player, and start
        /// the game past their own ceiling.
        /// </summary>
        private static void SolveLimitCoefficients(float slope)
        {
            float Fade(float attribute, int focus) => (BaseFactor(attribute, focus) * (1f - RateFloor) - 1f) / slope;

            // Pinned against the maximum focus actually allowed, so lowering the cap keeps the
            // same ceilings instead of putting 330 out of reach.
            var top = _maxFocus;
            var t1 = UntrainedCap - Fade(4f, 0);            // = c0 + 3*ka
            var t2 = NearMaxCap - Fade(10f, top - 1);       // = c0 + 9*ka + (top-1)*kf
            var t3 = NearMaxCap - Fade(9f, top);            // = c0 + 8*ka + top*kf

            var d = t2 - t3;                               // ka - kf
            _kf = (t2 - t1 - 6f * d) / (5f + top);
            _ka = _kf + d;
            _c0 = t1 - 3f * _ka;
        }

        private static double[] BuildEffort(float exponent, float slope)
        {
            var e = new double[SkillCap + 1];
            for (var k = 1; k <= SkillCap; k++)
                e[k] = e[k - 1] + Math.Pow(k, exponent) / RateFraction(10f, 10, k, slope);
            return e;
        }
    }
}
