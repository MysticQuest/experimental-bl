# Bannerlord mods

Local workspace for Mount & Blade II: Bannerlord modules.

Target game: **v1.4.8** (compile changeset `119303`, matching the
`Bannerlord.ReferenceAssemblies.*` package versions used by the projects).

## Building

The build reads the game location from the `GameFolder` MSBuild property in
`Directory.Build.props`, and derives the Steam Workshop folder from it. If your
install lives elsewhere, copy `Directory.Build.props.user.example` to
`Directory.Build.props.user` and override the paths there rather than editing the
tracked file.

Harmony and MCM are referenced straight out of their Steam Workshop folders, by
Workshop item id, with `Private=false` so they are not copied into the module
output — the game already loads them from their own modules. The build fails with
an explanatory message if either is missing.

## Skill Learning Rate

A single option: a global multiplier on the skill learning rate — the number
shown on the character screen, which the game multiplies into skill XP gains.

Configured in-game through **Mod Configuration Menu**
(Options → Mod Options → Skill Learning Rate):

- **Enabled** — turn off to leave the vanilla learning rate untouched
- **Global Learning Rate Multiplier** — `1.00` is vanilla, `2.00` is twice as
  fast, `0.50` is half

Both apply immediately, with no restart or campaign reload.

### Dependencies

Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu v5 — all from the
Steam Workshop.

### How it hooks in

Verified against the shipped `TaleWorlds.CampaignSystem.dll`:

```
HeroDeveloper.AddSkillXp(skill, rawXp, isAffectedByFocusFactor, shouldNotify)
    xp = rawXp * GenericXpModel.GetXpMultiplier(hero)
    if (isAffectedByFocusFactor)
        xp *= HeroDeveloper.GetFocusFactor(skill)   // -> CalculateLearningRate
```

The mod applies a Harmony postfix to
`DefaultCharacterDevelopmentModel.CalculateLearningRate`, adding a factor to the
returned `ExplainedNumber`. That covers both the XP path above and the value
displayed on the character screen.

A caller that passes `isAffectedByFocusFactor: false` bypasses the learning rate
entirely, in vanilla as well as here — those XP sources are unaffected by this
mod by design.

### Compatibility

Patching the vanilla model is deliberate, rather than registering a replacement
`CharacterDevelopmentModel`. Only one model can be registered, so replacing it
silently discards the changes of any other mod that does the same —
`Bannerlord.XPTweaks`, for one, registers its own
`ModifiedCharacterDevelopmentModel`. As a postfix on the vanilla method, this mod
instead stacks with any model that derives from `DefaultCharacterDevelopmentModel`
and calls `base.CalculateLearningRate`.
