using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// The attribute bonuses in words: what a point buys, and what the points already spent add to.
    /// </summary>
    /// <remarks>
    /// The numbers themselves live in <see cref="AttributeBonus"/>; this is the only place that
    /// turns them into text, so the character screen and character creation cannot drift apart and
    /// changing a constant changes every line that quotes it.
    ///
    /// Every line carries its rate in brackets. A total on its own answers "what do I have", which
    /// the player can already see; the rate answers "what does the next point buy", which is the
    /// question actually being asked at the moment one is spent.
    /// </remarks>
    internal static class AttributeEffects
    {
        /// <summary>One line of an attribute's worth: a rate, what it is called, how to print it.</summary>
        internal readonly struct Effect
        {
            private readonly string _name;
            private readonly float _perPoint;
            private readonly bool _percent;

            /// <summary>
            /// For a bonus the game only hands out whole, the points one unit costs; zero otherwise.
            /// </summary>
            /// <remarks>
            /// Companions arrive one at a time, five points apart. Stating that as "+0.2 per point"
            /// is arithmetically right and tells the player nothing they can act on -- worse, next
            /// to a floored total it reads as "you have none, and a point buys a fifth of one".
            /// A bonus that arrives whole is quoted as the interval it arrives on instead.
            /// </remarks>
            private readonly int _pointsPerUnit;

            internal Effect(string name, float perPoint, bool percent = false, int pointsPerUnit = 0)
            {
                _name = name;
                _perPoint = perPoint;
                _percent = percent;
                _pointsPerUnit = pointsPerUnit;
            }

            private bool Whole => _pointsPerUnit > 0;

            /// <summary>What the attribute is worth at this value, with the rate behind it.</summary>
            internal string At(int value) => $"+{Amount(_perPoint * value, Whole)} {_name} ({Rate})";

            /// <summary>What a point is worth, for a card with no value to show yet.</summary>
            internal string PerPoint => Whole
                ? $"+1 {_name} per {_pointsPerUnit} points"
                : $"+{Amount(_perPoint, false)} {_name} per point";

            /// <summary>The rate alone, as it reads in brackets behind a total.</summary>
            private string Rate => Whole
                ? $"1 per {_pointsPerUnit} points"
                : $"+{Amount(_perPoint, false)} per point";

            /// <remarks>
            /// The rounding belongs to the total and not to the rate, which is why only the total
            /// asks for it: four points towards a companion are real, they just are not a companion.
            /// </remarks>
            private string Amount(float amount, bool whole)
            {
                if (whole) amount = (float)Math.Floor(amount);
                return _percent ? $"{amount * 100f:0.#}%" : $"{amount:0.##}";
            }
        }

        private static readonly Effect[] None = new Effect[0];

        /// <summary>Everything an attribute buys beyond the skills it governs.</summary>
        internal static IList<Effect> For(CharacterAttribute? attribute)
        {
            if (attribute == null) return None;

            if (attribute == DefaultCharacterAttributes.Vigor)
                return new[]
                {
                    new Effect("hit points", AttributeBonus.VigorHitPoints),
                    new Effect("swing carried through a hit", AttributeBonus.VigorMomentum, percent: true),
                    new Effect("chance to knock a man back", AttributeBonus.VigorKnockback, percent: true),
                };

            if (attribute == DefaultCharacterAttributes.Control)
                return new[]
                {
                    new Effect("weapon handling", AttributeBonus.ControlHandling, percent: true),
                    new Effect("stagger resistance", AttributeBonus.ControlStagger, percent: true),
                };

            if (attribute == DefaultCharacterAttributes.Endurance)
                return new[]
                {
                    new Effect("damage resistance", AttributeBonus.EnduranceResistance, percent: true),
                    new Effect("mount speed", AttributeBonus.EnduranceMountSpeed, percent: true),
                    new Effect("running speed", AttributeBonus.EnduranceRunSpeed, percent: true),
                    new Effect("smithing stamina", AttributeBonus.EnduranceStamina),
                };

            if (attribute == DefaultCharacterAttributes.Cunning)
                return new[]
                {
                    new Effect("battle loot", AttributeBonus.CunningBattleLoot, percent: true),
                };

            if (attribute == DefaultCharacterAttributes.Social)
                return new[]
                {
                    new Effect("companion limit", 1f / AttributeBonus.SocialPointsPerCompanion,
                        pointsPerUnit: AttributeBonus.SocialPointsPerCompanion),
                    new Effect("skill XP for the rest of your clan", AttributeBonus.SocialClanLearning,
                        percent: true),
                };

            if (attribute == DefaultCharacterAttributes.Intelligence)
                return new[]
                {
                    new Effect("learning rate on every skill", AttributeBonus.IntelligenceLearning, percent: true),
                    new Effect("learning limit on every skill", AttributeBonus.IntelligenceCeiling),
                };

            return None;
        }

        /// <summary>The attribute's lines at a value, or its rates when nothing has been spent yet.</summary>
        internal static List<string> Lines(CharacterAttribute? attribute, int value)
        {
            var lines = new List<string>();
            var effects = For(attribute);

            for (var i = 0; i < effects.Count; i++)
                lines.Add(value > 0 ? effects[i].At(value) : effects[i].PerPoint);

            return lines;
        }
    }
}
