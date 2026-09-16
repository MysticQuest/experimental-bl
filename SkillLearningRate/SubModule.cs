using System;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SkillLearningRate
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "MysticQuest.SkillLearningRate";

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
                Debug.Print($"[SkillLearningRate] Failed to apply Harmony patches: {exception}");
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
        }
    }
}
