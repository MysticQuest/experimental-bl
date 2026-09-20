using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProgressionExpanded
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "MysticQuest.ProgressionExpanded";

        /// <summary>What character creation hands out. Those levels are never earned in play.</summary>
        private const int StartingSkill = 30;

        private Harmony? _harmony;

        /// <summary>The live Harmony instance, so the debug tools can ask what actually bound.</summary>
        internal static Harmony? Patcher { get; private set; }

        /// <summary>How many patch classes applied, and the names of any that did not.</summary>
        internal static int PatchesApplied { get; private set; }

        internal static readonly List<string> PatchFailures = new List<string>();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                // Enabled has to mean "behave as if this module were not installed". It did not:
                // the patches were applied regardless, so switching the mod off still left twenty
                // of them running and made the setting useless for narrowing down a fault.
                if (!Mod.On)
                {
                    Log.Write("Disabled in settings; no patches applied, no UI registered");
                    return;
                }

                _harmony = new Harmony(HarmonyId);
                Patcher = _harmony;
                ApplyPatches(_harmony);
            }
            catch (Exception exception)
            {
                Log.Write($"Failed to apply Harmony patches: {exception}");
            }

            // Kept out of PatchAll: private targets resolved by name, and an empty result there
            // would abort the sweep above rather than just this one feature.
            try
            {
                if (!Mod.Flag("MissionPatches", Settings.Instance?.MissionPatches))
                {
                    Log.Write("Reinforcement batching skipped");
                    return;
                }

                var hooked = BiggerReinforcementsPatch.Apply(_harmony!);
                Log.Write($"Reinforcement batching hooked in {hooked} place(s).");
            }
            catch (Exception exception)
            {
                Log.Write($"Reinforcement batching unavailable: {exception}");
            }

            // The second row of focus pips. Kept separate so a UI failure cannot take the
            // progression changes down with it.
            try
            {
                if (!Mod.On || !Mod.Flag("UiChanges", Settings.Instance?.UiChanges))
                {
                    Log.Write("Character screen changes switched off");
                    return;
                }

                var extender = UIExtender.Create(HarmonyId);
                extender.Register(typeof(SubModule).Assembly);
                extender.Enable();
            }
            catch (Exception exception)
            {
                Log.Write($"Focus pip row unavailable: {exception}");
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            Log.Write("OnGameStart: campaign detected");

            // Statics outlive a campaign; the process does not restart between them.
            MasteryEffects.Forget();
            Legacy.Forget();
            if (!(game.GameType is Campaign)) return;

            var settings = Settings.Instance;
            if (settings == null || !settings.Enabled)
            {
                Log.Write($"Disabled; leaving vanilla progression in place.");
                return;
            }

            Log.Write("Rebuilding the curve");
            Rebuild(settings);

            if (settings.UseCurve)
            {
                gameStarterObject.AddModel(new ProgressionModel());
            }
            else
            {
                Log.Write("Custom curve switched off; vanilla progression left in place");
            }
            if (gameStarterObject is CampaignGameStarter campaignStarter)
            {
                if (settings.RepairSkillXp) campaignStarter.AddBehavior(new XpRepairBehavior());
                else Log.Write("Skill XP repair switched off");

                // Always added; the behaviour itself checks the setting and only ever fires on a
                // new campaign, so there is nothing to gate at load time.
                campaignStarter.AddBehavior(new DestituteStartBehavior());
                Log.Write("Behaviours registered");
            }
            Log.Write($"Active. 275 lands at level {Curve.MasteryLevel:0.0}, 330 at {settings.Level330}, "
                        + $"exponent {settings.CurveExponent}, xp scale {Curve.XpScale:0.000}, limit bonus {Curve.LimitBonus:0.0}");
        }

        /// <summary>
        /// Applies each patch class on its own, rather than in one sweep.
        /// </summary>
        /// <remarks>
        /// <c>PatchAll</c> stops dead at the first class it cannot apply, and everything after it
        /// in the assembly is silently left unpatched - which is a very quiet way to lose half a
        /// mod. One class failing should cost that one feature and say so.
        /// </remarks>
        /// <summary>Patch classes that attach to a mission, an agent, or an encounter.</summary>
        private static readonly HashSet<string> MissionPatchClasses = new HashSet<string>
        {
            "AgentAttributePatch", "ControlResistancePatch", "CivilianBitePatch",
            "AgentSpawnProbePatch", "BanditRecruitsPatch", "CunningBattleLootPatch",

            // Hit points belong here too. It is not a mission patch by name, but it is the one
            // thing left that an agent reads while it is being built - and it returns a struct
            // by ref, which is the shape most likely to be mis-wrapped.
            "VigorHitPointsPatch", "AttributeCardPatch", "CharacterCreationAttributeCardPatch",
            "ControlStaggerPatch", "VigorMomentumPatch", "VigorKnockbackPatch",
            "ControlFootingPatch"
        };

        private static void ApplyPatches(Harmony harmony)
        {
            PatchFailures.Clear();
            var applied = 0;
            var missions = Mod.Flag("MissionPatches", Settings.Instance?.MissionPatches);
            if (!missions) Log.Write("Battle and mission patches switched off");

            var limit = Mod.Number("PatchLimit", Settings.Instance?.PatchLimit, 40);
            var names = new List<string>();

            var types = typeof(SubModule).Assembly.GetTypes();
            Array.Sort(types, (x, y) => string.CompareOrdinal(x.Name, y.Name));

            foreach (var type in types)
            {
                if (!missions && MissionPatchClasses.Contains(type.Name)) continue;

                try
                {
                    var processor = harmony.CreateClassProcessor(type);
                    if (processor == null) continue;

                    // Alphabetical, so "the first N" means the same thing every launch.
                    var patched = applied < limit ? processor.Patch() : null;
                    if (patched != null && patched.Count > 0) { applied++; names.Add(type.Name); }
                }
                catch (Exception exception)
                {
                    PatchFailures.Add(type.Name);
                    Log.Write($"Patch class {type.Name} could not be applied: {exception}");
                }
            }

            PatchesApplied = applied;
            Log.Write($"Applied {applied} patch classes (limit {limit}), {PatchFailures.Count} failed.");
            Log.Write("  applied: " + string.Join(", ", names.ToArray()));
        }

        internal static void Rebuild(Settings settings)
        {
            Curve.Rebuild(
                settings.CurveExponent,
                settings.Level330,
                settings.CareerXp,
                settings.PenaltySlope,
                settings.SummitRateAtCap,
                StartingSkill,
                settings.MaxFocusPerSkill);
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
        }
    }
}
