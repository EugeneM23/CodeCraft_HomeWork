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
instance at 0.3 m above the feet, scaled by `DEATH_EFFECT_SCALE` = 1.6: the pack is sized for a
person, and the creature is half as tall again. The pack's settings are kept: animation speed 1,
the decal living 30 s, and ground height detected automatically.

**Tools/Regax/Refresh Death Blood** replaces `DeathBlood` on the built `Regax.prefab` and rewires
`CreatureVfxInstaller._deathEffect`, without rebuilding the creature from the zombie.
**Tools/Regax/Build** puts in the same effect.

## Checked in Play

A rifle kill shows the full gush at the body, with droplets thrown well above the creature's
height. It settles into a pool with spatter around it, alongside the gibs. No new errors.

## Left as is

- `Regax_Mount.prefab` still carries `Blood7_02` until it is rebuilt (**Tools/Regax/Build Mount**
  starts from `Regax.prefab`). Nothing else uses `Blood7_02`, so it can be deleted once the mount
  is rebuilt.
- The pack's readme asks for the effect to be turned along the hit direction, because gravity is
  baked into the animation. The death blood keeps the creature's own rotation.
