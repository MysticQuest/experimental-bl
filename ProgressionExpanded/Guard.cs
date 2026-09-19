using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ProgressionExpanded
{
    /// <summary>
    /// Runs patch bodies so that a fault in one can never take the game down.
    /// </summary>
    /// <remarks>
    /// These patches sit on some of the hottest paths in the campaign - party AI scoring, damage,
    /// hit points, every skill effect read. An unhandled exception in a Harmony patch on one of
    /// those is not a misbehaving feature, it is a crash to desktop, and a crash that leaves no
    /// managed trace in the dump.
    ///
    /// Each site is reported once and then stays quiet, because a fault on a path that runs
    /// thousands of times a second would otherwise write a log file faster than the game runs.
    /// </remarks>
    internal static class Guard
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static readonly ConcurrentDictionary<string, byte> Touched = new ConcurrentDictionary<string, byte>();
        private static readonly object Gate = new object();

        internal static void Run(string site, Action body)
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                Report(site, exception);
            }
        }

        /// <summary>
        /// Notes the first time a patch site runs, so a crash leaves a trail of what it reached.
        /// </summary>
        /// <remarks>
        /// A crash to desktop takes no exception with it, so the only evidence left is how far the
        /// game got. Once per site, then silent - these sit on paths that run thousands of times
        /// a second.
        /// </remarks>
        internal static void Touch(string site)
        {
            // Lock free: these sit on paths that run thousands of times a second.
            if (Touched.TryAdd(site, 0)) Log.Write("reached " + site);
        }

        internal static void Report(string site, Exception exception)
        {
            var first = false;
            lock (Gate) first = Reported.Add(site);
            if (first) Log.Write("Patch " + site + " threw and was suppressed", exception);
        }
    }
}
