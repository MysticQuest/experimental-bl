using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace ProgressionExpanded
{
    /// <summary>
    /// Lists what an attribute buys, in the paragraph below the bound-skill icons.
    /// </summary>
    /// <remarks>
    /// This is the third attempt at the same paragraph, and the differences matter.
    ///
    /// The first wrote it from a postfix on <c>RefreshValues</c> and <c>RefreshWithCurrentValues</c>
    /// - the view model's refresh pipeline. It never displayed a single line, and merely having
    /// those two wrapped crashed character creation; a bisect over the patch set is what finally
    /// pinned it. The second appended to the attribute's own <c>Description</c>, which is safe but
    /// lands above the icons and gets glued to vanilla's "The skills that are bound to..." sentence.
    ///
    /// This one hangs off <c>ExecuteInspectAttribute</c> - the click that opens the card. It is a
    /// command handler rather than part of the refresh and binding machinery, it runs only when a
    /// player opens the card, and it never runs during character creation's automated flow. It is
    /// also gated by the character screen setting, so it is one switch away from being gone.
    /// </remarks>
    [HarmonyPatch(typeof(CharacterAttributeItemVM), nameof(CharacterAttributeItemVM.ExecuteInspectAttribute))]
    internal static class AttributeCardPatch
    {
        [HarmonyPostfix]
        private static void Describe(CharacterAttributeItemVM __instance)
        {
            Guard.Touch("AttributeCard");

            try
            {
                if (__instance == null || !AttributeBonus.Active()) return;
                if (!Mod.Flag("UiChanges", Settings.Instance?.UiChanges)) return;

                var attribute = __instance.AttributeType;
                if (attribute == null) return;

                var lines = LinesFor(attribute, __instance.AttributeValue);
                if (lines.Count == 0) return;

                // The leading blank line is the gap above the block. The card has its own spacing
                // there, but it is the first thing the layout gives up when the panel is full -
                // Control's description runs to four lines rather than three, and that one extra
                // line was enough to leave the bonuses sitting directly against the icon captions
                // while every other attribute had room. Spelling the gap out makes it the same
                // everywhere instead of a function of how long vanilla's description happens to be.
                var block = Environment.NewLine
                            + string.Join(Environment.NewLine, lines.ToArray());
                var current = __instance.IncreaseHelpText ?? string.Empty;

                // Vanilla's sentence is restored by every refresh, so this only guards against two
                // clicks arriving without one in between.
                if (current.StartsWith(block, StringComparison.Ordinal)) return;

                __instance.IncreaseHelpText = string.IsNullOrEmpty(current)
                    ? block
                    : block + Environment.NewLine + Environment.NewLine + current;
            }
            catch (Exception exception)
            {
                Guard.Report("AttributeCard", exception);
            }
        }

        /// <summary>What the attribute is worth right now, at the value the card is showing.</summary>
        private static List<string> LinesFor(CharacterAttribute attribute, int value) =>
            AttributeEffects.Lines(attribute, value);
    }

    /// <summary>
    /// The same rates, on the attribute rows shown while a character is being made.
    /// </summary>
    /// <remarks>
    /// This is where the numbers matter most, because character creation is the one place the game
    /// asks for attribute points before it has shown what they do. It is also the part of the UI
    /// that this mod has crashed before, so the touch is as small as it can be: a postfix on the
    /// row's constructor that swaps the tooltip's callback for one wrapping whatever was there.
    ///
    /// Nothing is computed in the postfix itself. The text is built when the player hovers the row,
    /// by which point the creation flow that the earlier attempt broke is long finished, and the
    /// settings the block is gated on are readable. A row with no tooltip of its own gets one.
    ///
    /// Rates rather than totals: during creation the value on the row is whatever the last choice
    /// granted, and what the player wants to know is what the attribute is for.
    /// </remarks>
    [HarmonyPatch(typeof(CharacterCreationGainedAttributeItemVM), MethodType.Constructor,
        new[] { typeof(CharacterAttribute) })]
    internal static class CharacterCreationAttributeCardPatch
    {
        private static readonly FieldInfo? AttributeOf =
            AccessTools.Field(typeof(CharacterCreationGainedAttributeItemVM), "_attributeObj");

        private static readonly FieldInfo? CallbackOf =
            AccessTools.Field(typeof(BasicTooltipViewModel), "_hintProperty");

        [HarmonyPostfix]
        private static void Describe(CharacterCreationGainedAttributeItemVM __instance)
        {
            Guard.Touch("CreationAttributeCard");

            try
            {
                if (__instance == null) return;

                var attribute = AttributeOf?.GetValue(__instance) as CharacterAttribute;
                if (attribute == null) return;

                var vanilla = CallbackOf?.GetValue(__instance.Hint) as Func<string>;
                __instance.Hint = new BasicTooltipViewModel(() => Compose(vanilla, attribute));
            }
            catch (Exception exception)
            {
                Guard.Report("CreationAttributeCard", exception);
            }
        }

        /// <summary>Vanilla's tooltip, then the rates, built at the moment of the hover.</summary>
        private static string Compose(Func<string>? vanilla, CharacterAttribute attribute)
        {
            var text = string.Empty;

            try
            {
                text = vanilla?.Invoke() ?? string.Empty;
            }
            catch
            {
                // Vanilla's own hint failing is not a reason to lose ours.
            }

            try
            {
                if (!AttributeBonus.Active()) return text;
                if (!Mod.Flag("UiChanges", Settings.Instance?.UiChanges)) return text;

                var lines = AttributeEffects.Lines(attribute, 0);
                if (lines.Count == 0) return text;

                var block = string.Join(Environment.NewLine, lines.ToArray());
                return string.IsNullOrEmpty(text)
                    ? block
                    : text + Environment.NewLine + Environment.NewLine + block;
            }
            catch (Exception exception)
            {
                Guard.Report("CreationAttributeCard.Compose", exception);
                return text;
            }
        }
    }
}
