# Regax death blood — the full KriptoFX Blood7

## What it was

The Regax death blood (`<VFX>/DeathBlood`, switched on by `DeathEffectBehaviour`) was
`Assets/3rdParty/VolumetricBloodFX/Prefabs/Blood7_02.prefab`. That is a copy of **Blood7** from
KriptoFX Volumetric Blood FX, carried over from another project. It has the same two parts as the
original: `Decal`, the ground pool, and `Blood`, the volumetric gush, a baked 99-frame mesh
animation. The copy differs from the original in three ways:

| | Blood7_02 (copy) | Blood7 (pack) |
| --- | --- | --- |
| `AnimationSpeed` | 4: the 3 s gush is over in 0.75 s | 1 |
| Decal material | `blood7/decal 1.mat` | `blood7/decal.mat` |
| Root scale | 4 (overridden to 1.22 on the Regax) | 1 |

So all that showed at a death was a short squirt and the pool.

## What it is now

`RegaxPrefabBuilder.DEATH_EFFECT` points at the pack's own
`Assets/3rdParty/KriptoFX/VolumetricBloodFX/Prefabs/Blood7.prefab`. It is a nested prefab
instance at 0.3 m above the feet, scaled by `DEATH_EFFECT_SCALE` = 3.2. The pack is sized for a
person, and the creature is half as tall again; at 1.6 the blood read too small, so it is doubled. The pack's settings are kept: animation speed 1,
the decal living 30 s, and ground height detected automatically.

**Tools/Regax/Refresh Death Blood** replaces `DeathBlood` on the built `Regax.prefab` and rewires
`CreatureVfxInstaller._deathEffect`, without rebuilding the creature from the zombie.
**Tools/Regax/Build** puts in the same effect. Refresh refuses to run in Play: there
`BFX_ShaderProperies` swaps the decal material for a runtime copy as soon as the effect is
instantiated, the prefab saves an override to nothing, and the pool draws as a white box.

**Green, and in proportion.** The creature bleeds green. `Assets/Game/Materials/Blood/` holds copies of the
pack's blood7 materials, retinted: `M_RegaxBlood_Fluid` (`_Color` 0.25, 0.75, 0.05 in place of the
pack's 0.75, 0, 0) and `M_RegaxBlood_Decal` (`_TintColor` 0.13, 0.44, 0.02 in place of 0.44, 0, 0).
The pack's own materials stay red. On top of the 3.2, the gush (`Blood`) is scaled �2
(`DEATH_FLUID_SCALE`) and the pool (`Decal`) �0.8 (`DEATH_DECAL_SCALE`). The builder puts these on in
`AddDeathBlood`, so both Refresh and Build keep them.

**Random heading.** `DeathEffectBehaviour` turns the effect to a random angle around the vertical
just before switching it on, so no two deaths splash the same way. It does not tilt the effect,
because the fall is baked into the animation. The behaviour is shared, so zombies get the random
heading too.

## Checked in Play

Two rifle kills at scale 3.2 gave two large pools with spatter, turned 46° and 260°. No new errors. After the green and the resize: one rifle kill, green gush above the creature's
height, a smaller green pool, no errors.

## Left as is

- `Regax_Mount.prefab` still carries `Blood7_02` until it is rebuilt (**Tools/Regax/Build Mount**
  starts from `Regax.prefab`). Nothing else uses `Blood7_02`, so it can be deleted once the mount
  is rebuilt.

## Every effect in the pack (current)

The death blood is no longer a switched-off child of `Regax.prefab`. **`DeathBloodInstaller`** on
`<VFX>` adds a **`DeathBloodBehaviour`**. When Regax dies, the behaviour spawns one effect from the
pack's prefab, at `<VFX>` plus 0.3 m and at a random heading around the vertical, and removes it
after 35 s. (`Features/Health/Vfx/`: `DeathBloodConfig`, `DeathBloodBehaviour`,
`DeathBloodInstaller`.)

- **Which effect.** `Assets/Game/Configs/Vfx/RegaxDeathBlood.asset` lists all 17 effects in
  `KriptoFX/VolumetricBloodFX/Prefabs` that have a gush: Blood1, Blood2, Blood2_Left, Blood2_Right
  and Blood3 to Blood15. AttachedBloodDecal has no gush and is left out, and so are the
  SimplePlaneDecals copies. A death picks one at random. The installer's `_variant` (0 and up)
  pins one effect instead.
- **Size.** The whole effect is 15% smaller than before: 2.72 instead of 3.2. On top of that,
  every gush mesh (`BFX_Blood`) is ×2 and every pool (`BFX_Decal`) is ×0.8. This covers the
  combined effects Blood10 to Blood15 too, which carry several of each.
- **Green.**
  - Gush: `_Color` (0.25, 0.75, 0.05) through a MaterialPropertyBlock.
    `BFX_ManualAnimationUpdate` reads the block back before writing its frame values, so the
    colour stays.
  - Pool: `_TintColor` (0.13, 0.44, 0.02) on the material copy that `BFX_ShaderProperies` makes
    in Awake. A property block on the decal blanked its other instanced values and drew it black.
  - The pack's materials are untouched, so the earlier green copies were removed.
- **Builder.** **Tools/Regax/Refresh Death Blood** rebuilds the config from the pack folder (sizes
  and colours are kept) and puts the installer on `Regax.prefab`, listed in its installers. It
  also clears `CharacterVfxInstaller._deathEffect` and removes the old `DeathBlood` child.
  **Tools/Regax/Build** does the same in `Rewire`.

**Targets.** The 10 `Regax_Target_*` in Playground each have `_variant` pinned: targets 1 to 10
play Blood1 to Blood10, and the effect is in each target's name. Blood11 to Blood15 and
Blood2_Left/Right show up on ordinary Regax deaths.

Checked in Play: all 10 targets killed at once gave 10 green gushes and 10 green pools, and the
targets broke into gibs. No errors.
