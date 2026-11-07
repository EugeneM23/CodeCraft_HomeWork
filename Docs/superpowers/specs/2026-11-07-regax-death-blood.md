# Regax death blood â€” the full KriptoFX Blood7

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
The pack's own materials stay red. On top of the 3.2, the gush (`Blood`) is scaled ×2
(`DEATH_FLUID_SCALE`) and the pool (`Decal`) ×0.8 (`DEATH_DECAL_SCALE`). The builder puts these on in
`AddDeathBlood`, so both Refresh and Build keep them.

**Random heading.** `DeathEffectBehaviour` turns the effect to a random angle around the vertical
just before switching it on, so no two deaths splash the same way. It does not tilt the effect,
because the fall is baked into the animation. The behaviour is shared, so zombies get the random
heading too.

## Checked in Play

Two rifle kills at scale 3.2 gave two large pools with spatter, turned 46Â° and 260Â°. No new errors. After the green and the resize: one rifle kill, green gush above the creature's
height, a smaller green pool, no errors.

## Left as is

- `Regax_Mount.prefab` still carries `Blood7_02` until it is rebuilt (**Tools/Regax/Build Mount**
  starts from `Regax.prefab`). Nothing else uses `Blood7_02`, so it can be deleted once the mount
  is rebuilt.
