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
    /// Written because guessing failed repeatedly: four hypotheses about where a mission's reward
    /// came from, each costing a playthrough to disprove. A scan of every campaign assembly found
    /// no quest that hands items over at all, which means the reward arrives by a path none of
    /// those guesses covered.
    ///
    /// So it watches all four ways an item can enter a roster rather than the one I assumed. The
    /// first version named a signature that does not exist - AddToCounts(item, number, bool) - so
    /// Prepare returned false and the probe was never applied at all. Listing the real overloads
    /// is the difference between a tool and a silence.
    ///
    /// Off unless debug tools are on, and it only ever writes.
    /// </remarks>
    [HarmonyPatch]
    internal static class InventoryProbePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(ItemRoster)))
                if (method.Name == "AddToCounts" || method.Name == "Add")
                    yield return method;
        }

        /// <summary>One line per caller and item, however often it runs.</summary>
        private static readonly HashSet<string> Seen = new HashSet<string>();

        [HarmonyPrefix]
        private static void Note(ItemRoster __instance, object[] __args)
        {
            try
            {
                if (!(Settings.Instance?.DebugEnabled ?? false)) return;
                if (__instance != MobileParty.MainParty?.ItemRoster) return;

                var what = Describe(__args);
                if (what == null) return;

                var caller = Blame();
                if (!Seen.Add(caller + "|" + what)) return;

                Log.Write($"Packs: {what} from {caller}");
            }
            catch
            {
                // A probe that throws is worse than no probe.
            }
        }

        /// <summary>The item behind whichever overload was called.</summary>
        private static string? Describe(object[] args)
        {
            if (args == null) return null;

            foreach (var argument in args)
            {
                switch (argument)
                {
                    case ItemObject item:
                        return item.StringId;
                    case EquipmentElement element when element.Item != null:
                        return element.Item.StringId;
                    case ItemRosterElement roster when roster.EquipmentElement.Item != null:
                        return roster.EquipmentElement.Item.StringId;
                }
            }

            return "several items";
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
