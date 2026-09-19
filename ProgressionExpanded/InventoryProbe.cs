using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace ProgressionExpanded
{
    /// <summary>
    /// Writes down who puts things in the player's packs, and from where.
    /// </summary>
    /// <remarks>
    /// Written because guessing failed repeatedly. A mission reward was arriving as inventory and
    /// four different hypotheses about its source - the tutorial's ending, a quest reward, the
    /// body search - were all wrong, each costing a playthrough to disprove.
    ///
    /// This records the item and the game method that asked for it, so one run names the caller
    /// exactly and the right thing can be patched the first time. Off unless debug tools are on,
    /// and it only ever writes: nothing here changes what the player receives.
    /// </remarks>
    [HarmonyPatch]
    internal static class InventoryProbePatch
    {
        private static bool Prepare() => Target() != null;

        private static MethodBase? Target() =>
            AccessTools.Method(typeof(ItemRoster), nameof(ItemRoster.AddToCounts),
                new[] { typeof(ItemObject), typeof(int), typeof(bool) });

        private static MethodBase TargetMethod() => Target()!;

        /// <summary>One line per caller, however many items it adds.</summary>
        private static readonly HashSet<string> Seen = new HashSet<string>();

        [HarmonyPrefix]
        private static void Note(ItemRoster __instance, ItemObject item, int number)
        {
            try
            {
                if (item == null || number <= 0) return;
                if (!(Settings.Instance?.DebugEnabled ?? false)) return;
                if (__instance != MobileParty.MainParty?.ItemRoster) return;

                var caller = Blame();
                if (!Seen.Add(caller + "|" + item.StringId)) return;

                Log.Write($"Packs: +{number} {item.StringId} ({item.Name}) from {caller}");
            }
            catch
            {
                // A probe that throws is worse than no probe.
            }
        }

        /// <summary>The first frame outside this mod and outside the roster itself.</summary>
        private static string Blame()
        {
            var trace = new StackTrace(2, false);
            for (var i = 0; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                var type = method?.DeclaringType;
                if (type == null) continue;

                var name = type.FullName ?? type.Name;
                if (name.StartsWith("ProgressionExpanded") || name.StartsWith("HarmonyLib")) continue;
                if (type == typeof(ItemRoster)) continue;

                return type.Name + "." + method!.Name;
            }

            return "unknown";
        }
    }
}
