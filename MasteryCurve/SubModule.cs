using System;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MasteryCurve
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "MysticQuest.MasteryCurve";

        /// <summary>What character creation hands out. Those levels are never earned in play.</summary>
        private const int StartingSkill = 30;

        private Harmony? _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(SubModule).Assembly);
            }
            catch (Exception exception)
            {
                Debug.Print($"[MasteryCurve] Failed to apply Harmony patches: {exception}");
            }

            // The second row of focus pips. Kept separate so a UI failure cannot take the
            // progression changes down with it.
            try
            {
                var extender = UIExtender.Create(HarmonyId);
                extender.Register(typeof(SubModule).Assembly);
                extender.Enable();
            }
            catch (Exception exception)
            {
                Debug.Print($"[MasteryCurve] Focus pip row unavailable: {exception}");
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (!(game.GameType is Campaign)) return;

            var settings = Settings.Instance;
            if (settings == null || !settings.Enabled)
            {
                Debug.Print("[MasteryCurve] Disabled; leaving vanilla progression in place.");
                return;
            }

            Rebuild(settings);
            gameStarterObject.AddModel(new MasteryCurveModel());
            Debug.Print($"[MasteryCurve] Active. 275 at level {settings.Level275}, 330 at {settings.Level330}, "
                        + $"exponent {settings.CurveExponent}, xp scale {Curve.XpScale:0.000}, limit bonus {Curve.LimitBonus:0.0}");
        }

        internal static void Rebuild(Settings settings)
        {
            var level275 = Math.Min(settings.Level275, settings.Level330 - 1);
            Curve.Rebuild(
                settings.CurveExponent,
                level275,
                settings.Level330,
                settings.CareerXp,
                settings.FocusedSkills,
                settings.PenaltySlope,
                StartingSkill);
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
        }
    }
}
