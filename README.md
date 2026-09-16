# Bannerlord mods

Local workspace for Mount & Blade II: Bannerlord modules.

Target game: **v1.4.8** (compile changeset `119303`, matching the
`Bannerlord.ReferenceAssemblies.*` package versions used by the projects).

The build reads the game location from the `GameFolder` MSBuild property in
`Directory.Build.props`. If your install lives somewhere else, copy
`Directory.Build.props.user.example` to `Directory.Build.props.user` and set the
path there instead of editing the tracked file.

## Skill Learning Rate

A single option: a global multiplier on the skill learning rate — the number
shown on the character screen, which the game multiplies into skill XP gains.

The module has **no external dependencies** — no Harmony, no MCM/MBOptionScreen.
It works by registering a `CharacterDevelopmentModel` that derives from the
vanilla `DefaultCharacterDevelopmentModel` and applies a factor to the result of
`CalculateLearningRate`. Configuration is a plain XML file.

### Usage

1. Build `Mods.sln` (`Release|x64`). The post-build step copies the module into
   the game's `Modules` folder.
2. Enable **Skill Learning Rate** in the launcher.
3. Edit `Modules\SkillLearningRate\ModuleData\settings.xml`:
   - `Enabled` — `true` / `false`
   - `GlobalLearningRateMultiplier` — `1.0` is vanilla, `2.0` doubles the rate,
     `0.5` halves it

Reload the campaign (or restart the game) after changing the XML — settings are
read when the campaign game starts.

### How it hooks in

Verified against the shipped `TaleWorlds.CampaignSystem.dll`:

```
HeroDeveloper.AddSkillXp(skill, rawXp, isAffectedByFocusFactor, shouldNotify)
    xp = rawXp * GenericXpModel.GetXpMultiplier(hero)
    if (isAffectedByFocusFactor)
        xp *= HeroDeveloper.GetFocusFactor(skill)   // <- CalculateLearningRate
```

So the multiplier applies to every XP source that is affected by the focus
factor, which is the overwhelming majority of skill XP. A caller that passes
`isAffectedByFocusFactor: false` bypasses the learning rate entirely, in vanilla
as well as here — those sources are unaffected by this mod by design.

### Compatibility

Any other mod that replaces `CharacterDevelopmentModel` will conflict: whichever
module loads last wins, and the earlier one's changes are discarded. Load
**Skill Learning Rate** after such a mod, or turn off the other mod's
learning-rate option. If stacking ever becomes necessary, the override can be
swapped for a Harmony postfix on `CalculateLearningRate`.
