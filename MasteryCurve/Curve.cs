using System;

namespace MasteryCurve
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
        private const float UntrainedCap = 46f;   // attribute 4, no focus -- matches vanilla
        private const float NearMaxCap = 275f;    // the best a build one point short can ever reach

        private static float _c0 = -12.3f;
        private static float _ka = 13.2f;
        private static float _kf = 8.1f;

        /// <summary>Rate never reaches zero; it bottoms out here, so a capped skill crawls rather than freezing.</summary>
        public const float RateFloor = 0.003f;

        /// <summary>Focus point costs escalate: the nth point costs ceil(n/4).</summary>
        private static readonly int[] FocusCostCumulative = { 0, 1, 2, 3, 4, 6, 8, 10, 12, 15, 18 };

        private static float _limitBonus = 58.9f;
        private static float _xpScale = 20f;
        private static float _charScale = 96f;
        private static int[] _skillXp = new int[MaxSkillLevel + 1];

        public static float LimitBonus => _limitBonus;
        public static float XpScale => _xpScale;

        /// <summary>Focus contributes at half vanilla's weight, shaped like its own escalating cost.</summary>
        public static float FocusTerm(int focus)
        {
            if (focus <= 0) return 0f;
            if (focus > 10) focus = 10;
            return 5f * FocusCostCumulative[focus] / FocusCostCumulative[10];
        }

        public static int FocusPointCost(int nextPoint) => (int)Math.Ceiling(Math.Max(1, nextPoint) / 4.0);

        public static float LimitValue(float attribute, int focus)
        {
            var limit = _c0 + _ka * (attribute - 1f) + _kf * focus;
            if (attribute >= 9.999f && focus >= 10) limit += _limitBonus;
            return Math.Max(0f, limit);
        }

        public static float BaseFactor(float attribute, int focus) => 1f + 0.4f * attribute + FocusTerm(focus);

        /// <summary>The surviving share of the base rate at this skill level, floored so it never hits zero.</summary>
        public static float RateFraction(float attribute, int focus, float skillValue, float slope)
        {
            var b = BaseFactor(attribute, focus);
            var limit = LimitValue(attribute, focus);
            var factor = b;
            if (skillValue > limit) factor -= 1f + slope * (skillValue - limit);
            var floor = b * RateFloor;
            return Math.Max(floor, factor) / b;
        }

        public static int SkillXpRequired(int skillLevel)
        {
            if (skillLevel <= 0) return 0;
            if (skillLevel > MaxSkillLevel) skillLevel = MaxSkillLevel;
            return _skillXp[skillLevel];
        }

        public static int CharacterXpRequired(int level)
        {
            if (level <= 0) return 0;
            if (level == 1) return 1;
            if (level > MaxCharacterLevel) return int.MaxValue;
            var v = _charScale * level * level * level;
            return v >= int.MaxValue ? int.MaxValue : (int)v;
        }

        /// <summary>Recomputes every table. Cheap enough to run whenever a setting changes.</summary>
        public static void Rebuild(float exponent, int level275, int level330, float careerXpAtFifty,
                                   int focusedSkills, float slope, int startSkill)
        {
            _charScale = careerXpAtFifty / (50f * 50f * 50f);
            SolveLimitCoefficients(slope);

            var want = Math.Pow((double)level330 / level275, 3.0);
            double lo = 0, hi = 260, mid = 58.9;
            if (EffortRatio(0, exponent, slope, startSkill) >= want &&
                EffortRatio(260, exponent, slope, startSkill) <= want)
            {
                for (var i = 0; i < 60; i++)
                {
                    mid = (lo + hi) / 2;
                    if (EffortRatio((float)mid, exponent, slope, startSkill) > want) lo = mid; else hi = mid;
                }
            }
            else
            {
                mid = EffortRatio(0, exponent, slope, startSkill) < want ? 0 : 260;
            }
            _limitBonus = (float)mid;

            var effort = BuildEffort(exponent, slope);
            var span = effort[SkillCap] - effort[Math.Min(startSkill, SkillCap)];
            if (span <= 0) span = 1;
            var learningRate = 1.25f * BaseFactor(10f, 10);
            _xpScale = (float)(learningRate * _charScale * Math.Pow(level330, 3) / (focusedSkills * span));

            _skillXp = new int[MaxSkillLevel + 1];
            double running = 0;
            for (var s = 1; s <= MaxSkillLevel; s++)
            {
                running += _xpScale * Math.Pow(s, exponent);
                _skillXp[s] = running >= int.MaxValue ? int.MaxValue : (int)running;
            }
        }

        /// <summary>
        /// Pins three ceilings -- untrained, and the two builds a single point short of the
        /// maximum -- then solves the limit line through them. Without this the low-attribute
        /// skills drift below the value character creation already handed the player, and start
        /// the game past their own ceiling.
        /// </summary>
        private static void SolveLimitCoefficients(float slope)
        {
            float Fade(float attribute, int focus) => (BaseFactor(attribute, focus) * (1f - RateFloor) - 1f) / slope;

            var t1 = UntrainedCap - Fade(4f, 0);    // = c0 + 3*ka
            var t2 = NearMaxCap - Fade(10f, 9);     // = c0 + 9*ka + 9*kf
            var t3 = NearMaxCap - Fade(9f, 10);     // = c0 + 8*ka + 10*kf

            var d = t2 - t3;                        // ka - kf
            _kf = (t2 - t1 - 6f * d) / 15f;
            _ka = _kf + d;
            _c0 = t1 - 3f * _ka;
        }

        private static double EffortRatio(float bonus, float exponent, float slope, int startSkill)
        {
            _limitBonus = bonus;
            var e = BuildEffort(exponent, slope);
            var from = e[Math.Min(startSkill, SkillCap)];
            var denom = e[275] - from;
            return denom <= 0 ? double.MaxValue : (e[SkillCap] - from) / denom;
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
