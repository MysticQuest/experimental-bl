using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ProgressionExpanded
{
    /// <summary>
    /// Each generation of a clan is a little more capable than the one before it.
    /// </summary>
    /// <remarks>
    /// Deliberately nothing to do with <em>what</em> the parent was good at. A child's build comes
    /// from their own education and their own campaign; what they inherit is capacity, not a trade.
    /// Nothing is capped: a fifth-generation heir is meant to be remarkable.
    ///
    /// Generation is counted by walking the bloodline rather than stored, so it survives saves,
    /// needs no migration, and cannot drift out of step with the family tree.
    /// </remarks>
    internal static class Legacy
    {
        private const int MaxWalk = 24;
        /// <summary>
        /// Concurrent because party AI is stepped on several threads, and a plain dictionary
        /// written from two of them at once corrupts its buckets - which shows up as a crash to
        /// desktop with no managed trace, not as an exception anyone can catch.
        /// </summary>
        private static readonly ConcurrentDictionary<Hero, int> Cache = new ConcurrentDictionary<Hero, int>();

        /// <summary>1 for a founder, 2 for their children, and so on.</summary>
        internal static int GenerationOf(Hero? hero)
        {
            if (hero == null) return 1;
            if (Cache.TryGetValue(hero, out var known)) return known;

            var depth = 1;
            var current = hero;
            for (var step = 0; step < MaxWalk; step++)
            {
                var parent = Elder(current.Father, current.Mother);
                if (parent == null) break;
                depth++;
                current = parent;
            }

            Cache[hero] = depth;
            return depth;
        }

        /// <summary>The parent further back in the line, so half-siblings do not reset the count.</summary>
        private static Hero? Elder(Hero? father, Hero? mother)
        {
            if (father == null) return mother;
            if (mother == null) return father;
            return father.BirthDay < mother.BirthDay ? father : mother;
        }

        /// <summary>Generations past the founder. Zero for the founder themselves.</summary>
        private static int Steps(Hero? hero) => Math.Max(0, GenerationOf(hero) - 1);

        private static Settings? Live(Hero? hero)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.GenerationsImprove) return null;
            return hero != null && hero.Clan == Clan.PlayerClan ? settings : null;
        }

        /// <summary>Multiplier on every scrap of XP the hero earns.</summary>
        internal static float LearningBonus(Hero? hero)
        {
            var settings = Live(hero);
            return settings == null ? 1f : 1f + settings.GenerationLearningStep * Steps(hero!);
        }

        /// <summary>Extra skill levels added to every learning limit, which lifts the ceiling itself.</summary>
        internal static float CeilingBonus(Hero? hero)
        {
            var settings = Live(hero);
            return settings == null ? 0f : settings.GenerationCeilingStep * Steps(hero!);
        }

        /// <summary>Focus and attribute points handed over when the heir comes of age.</summary>
        internal static void GrantComingOfAge(Hero? hero)
        {
            var settings = Live(hero);
            if (settings == null || hero?.HeroDeveloper == null) return;

            var steps = Steps(hero);
            if (steps <= 0) return;

            var focus = (int)Math.Round(settings.GenerationFocusStep * steps);
            var attribute = (int)Math.Round(settings.GenerationAttributeStep * steps);
            if (focus <= 0 && attribute <= 0) return;

            hero.HeroDeveloper.UnspentFocusPoints += focus;
            hero.HeroDeveloper.UnspentAttributePoints += attribute;

            {
                var message = new TaleWorlds.Localization.TextObject(
                    "{=MCGEN}{NAME} comes of age as generation {GEN} of the family, with {FOCUS} extra focus and {ATTR} extra attribute points.");
                message.SetTextVariable("NAME", hero.Name);
                message.SetTextVariable("GEN", GenerationOf(hero));
                message.SetTextVariable("FOCUS", focus);
                message.SetTextVariable("ATTR", attribute);
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
        }

        internal static void Forget() => Cache.Clear();
    }
}
