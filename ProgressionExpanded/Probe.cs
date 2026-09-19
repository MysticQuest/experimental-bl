using System;
using System.Collections.Concurrent;

namespace ProgressionExpanded
{
    /// <summary>
    /// Counts calls into the progression model, and shouts when one runs away.
    /// </summary>
    /// <remarks>
    /// The save-load failure produced a dump with no exception stream at all, which means the game
    /// was not throwing - it had stopped responding. A hang leaves no stack and no message, so the
    /// only evidence available is whether something of ours is being called a preposterous number
    /// of times, and with what argument.
    ///
    /// The game's own <c>HeroDeveloper.CheckLevel</c> loops until the required XP for the next
    /// level either exceeds the hero's total or equals <c>GetMaxSkillPoint()</c> exactly. A curve
    /// that never satisfies one of those two never leaves the loop, and this is what would catch
    /// it: the count climbs without bound and the last argument shows where it is stuck.
    /// </remarks>
    internal static class Probe
    {
        private const int Step = 200000;

        private static readonly ConcurrentDictionary<string, long> Counts =
            new ConcurrentDictionary<string, long>();

        internal static void Count(string site, int argument)
        {
            var n = Counts.AddOrUpdate(site, 1, (_, v) => v + 1);
            if (n % Step == 0) Log.Write($"RUNAWAY? {site} called {n:N0} times, last argument {argument}");
        }

        internal static string Summary()
        {
            var parts = new System.Text.StringBuilder();
            foreach (var pair in Counts)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append(pair.Key).Append('=').Append(pair.Value.ToString("N0"));
            }

            return parts.Length == 0 ? "(model never called)" : parts.ToString();
        }
    }
}
