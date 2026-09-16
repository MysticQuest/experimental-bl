# Mastery Curve

A progression rebuild for Bannerlord v1.4.8, aimed at a career that reads like a life: no free
levels for stabbing five men, a long honest middle, and a summit only total dedication reaches.

Nothing here is a new mechanic. The ceiling still emerges where the learning rate runs out, exactly
as in vanilla — no clamp on skill level, no change to how XP is awarded. Only the numbers move.

## What it changes

| | Vanilla | Here |
|---|---|---|
| XP per skill level | second-difference, cubic total | `scale × level^exponent` |
| XP per character level | `inc += 1000 + inc/5` (geometric) | `scale × level³` |
| Over-limit penalty | `1 + 0.10 × (skill − limit)`, slides to exactly zero | same shape, gentler slope, floored |
| Learning limit | `(attr−1)×10 + focus×30` | linear, solved from pinned ceilings |
| Max focus per skill | 5 | 10 |
| Focus point cost | always 1 | `ceil(n/4)` — a mastered skill costs 18, not 10 |
| Focus weight in the rate | 1.0 per point | 0.5 per point, shaped like its own cost |
| Per-skill XP | flat | scaled for skills with rare opportunities |

Two facts drove the design, both read off the shipped assemblies rather than assumed:

- **Vanilla's character curve is geometric.** Level 50 costs 227 million raw XP, 39× level 30, so
  no career ever reaches it. Raw XP is taken before the learning-rate multiplier, so no mod shortens it.
- **Vanilla's own maximum is 329, not 330.** Its wall for a maxed build sits exactly at 330 and the
  rate is zero there, so the last level costs infinity. Here the wall sits past the cap, and 330
  actually arrives.

## Ceilings

Three are pinned, and the limit line is solved through them so they hold when the slope changes:

- attribute 4, no focus → **46** (matching vanilla, and safely above the ~30 character creation gives)
- attribute 10 with focus 9, or attribute 9 with focus 10 → **275**
- attribute 10 with focus 10 → **330**

Past its ceiling a skill does not freeze. The rate bottoms out at a floor rather than zero, so an
unfocused skill keeps inching — abysmally, but visibly.

## Per-skill catch-up

From `DefaultSkillLevelingManager`, opportunities are wildly uneven:

- **Trade** earns `0.5 × profit` in denars and nothing else. Mastering it means millions in trade.
- **Engineering** has four sources and every one is a siege. Between wars it earns nothing at all.
- **Charm** and **Tactics** have one source each.
- **Roguery** has nineteen.

The multipliers in the settings compensate. They are reasoned from the formulas and the source
counts, not measured from play, and they want testing before anyone trusts them.

## The second row of focus pips

Focus runs to 10, but the character screen only ever drew five. UIExtenderEx adds a second row of
five above the first, at 0.4 the height so both fit the space one row used to occupy.

`SkillPointsContainerListPanel` lights child *i* whenever `CurrentFocusLevel` reaches *i + 1*, and
it loops over its own child count rather than a hardcoded five — so a second panel works untouched,
except that it would light its first pip at focus 1. A view-model mixin on `SkillVM` exposes
`CurrentFocusLevelUpper`, the value minus five, and the upper row binds to that instead.

Both the small pips on each skill tile and the large ones on the inspected skill are covered. The UI
registration is wrapped separately from the progression changes, so if a prefab patch ever fails
against a future game version the curve still works and only the pips revert.

## Settings

In **Mod Options → Mastery Curve**. Everything is phrased as something you can notice while playing
— which character level you master a skill at, how long a whole career runs, how fast skills rise
early — rather than as the exponents underneath.

There is a **Debug tools** group, off by default, with buttons to grant focus points, attribute
points and character levels, and to print where you stand.

## Compatibility

**Do not run this with Skill Learning Rate.** That mod postfixes
`DefaultCharacterDevelopmentModel.CalculateLearningRate`; this one overrides the method outright
without calling base, so the postfix never runs and its multiplier silently does nothing. Pick one.

The same applies to any other mod that replaces `CharacterDevelopmentModel` — `Bannerlord.XPTweaks`
registers its own. Whichever module loads last wins.
