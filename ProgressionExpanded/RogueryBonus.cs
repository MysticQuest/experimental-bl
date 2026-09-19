using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ProgressionExpanded
{
    /// <summary>
    /// What Roguery is worth to the rogue, rather than to the gang he runs.
    /// </summary>
    /// <remarks>
    /// Vanilla gives Roguery three personal effects, all of them about sneaking: sneak damage,
    /// crouched speed and noise suppression. Nothing covers a town brawl unless the player happens
    /// to have taken one particular perk, which is the gap this fills. Frightening people into
    /// surrendering started here too, but it reads people rather than robs them, so it moved to
    /// Cunning - see <see cref="CunningIntimidationPatch"/>.
    /// </remarks>
    internal static class RogueryBonus
    {
        internal static bool Active() => TacticsBonus.Active();
    }

    /// <summary>
    /// The knife you are allowed to carry indoors bites harder.
    /// </summary>
    /// <remarks>
    /// This is the one place the game asks whether the weapon in hand is civilian, which is why
    /// the bonus hangs here rather than on a stat the character carries around. It is checked
    /// against the attacker being the player, so a town guard swinging the same dagger gets
    /// nothing from your Roguery.
    /// </remarks>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel),
        nameof(SandboxAgentApplyDamageModel.ApplyDamageAmplifications))]
    internal static class CivilianBitePatch
    {
        [HarmonyPostfix]
        private static void Sharpen(ref AttackInformation attackInformation, ref float __result)
        {
            Guard.Touch("CivilianDamage");
            if (__result <= 0f || !RogueryBonus.Active()) return;

            try
            {
                if (!(attackInformation.AttackerAgentCharacter is CharacterObject attacker)) return;
                if (!attacker.IsPlayerCharacter) return;

                var item = attackInformation.AttackerWeapon.Item;
                if (item == null || !item.IsCivilian) return;

                var share = MasteryEffects.PlayerValue(MasteryEffects.CivilianDamage);
                if (share > 0f) __result *= 1f + share;
            }
            catch
            {
                // A damage tick is the wrong place to throw; the unmodified number still stands.
            }
        }
    }
}
