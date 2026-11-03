# Playground test level — design

Date: 2026-11-03
Branch: `atomic-upgrade`

## Purpose

A scene for walking through every gameplay mechanic by hand in Play Mode, in a minute or two,
to see what the audit fixes did and did not repair. Not a regression fixture, not a balance
sandbox: those would be different designs.

## Constraints

- The project has no UI: health, mana, ammo and buff state are invisible in Play Mode.
- The scene must be produced from code. Authoring a `.unity` file by hand is not viable, and the
  editor cannot be driven from this session.
- `GameContext` and `PlayerContext` wiring must not be rebuilt from scratch. It works today, it is
  dozens of private serialized fields deep, and rebuilding it blind is the largest risk in the task.

## Approach

An editor generator, `Tools/Playground/Rebuild Scene`, that **copies the working scene and takes it
apart**, rather than assembling a new one:

1. Open `Game/Scenes/GameScene.unity`, save a copy as `Game/Scenes/Playground.unity`, open the copy.
2. Deactivate the art roots — every root whose hierarchy holds no Game, Modules or Atomic component
   and no camera, light or audio listener. Deactivation rather than deletion, so that a reference
   from a kept object can never be left dangling; the hierarchy still shows them, switched off.
3. Lay a flat floor on the `Ground` layer with low walls, so the jump probe and the fall-off cases
   behave.
4. Build seven stations around the origin from the data in `PlaygroundLayout`.
5. Move the character to the centre, point the enemy spawn at the enemy station, add the overlay.
6. Save.

Everything placed is either an existing prefab, an existing scene object cloned as a template, or a
primitive. The generator creates no new entity object from parts.

### Why a copy

`GameContextInstaller` alone carries the weapon catalog, the player context, the pools parent, the
audio prefab, the entity world, the enemy prefab and the spawn point; `PlayerContextInstaller`
carries four system installers, each with their own references. A copy inherits all of it, already
correct. The cost is that the generator overwrites `Playground.unity` wholesale on every run, so the
scene is a build artefact and hand edits to it do not survive a rebuild.

### What only an editor script can do

`EnemyCoreInstaller._patrolPoints` and `EffectAreaInstaller._entityAspect` are private serialized
fields, and a prefab cannot reference scene transforms. Patrol routes therefore only exist for
scene-placed enemies, assigned through `SerializedObject`. This is also the one way to give the
patrol behaviour real points: `Enemy.prefab` ships two unassigned ones, which is what the guard in
`EnemyPatrolBehaviour.Init` is working around.

## Stations

Start at the origin, each station a root named `Station_<Name>`, spaced so nothing overlaps.

| Station | Where | Holds | Exercises |
|---|---|---|---|
| Weapons | x −18 | AR_01, AR_02, PT_01, Knife_01 pickups; ammo and health items; a box wall | interact markers, pick up and drop, range and melee fire, ammo, bullet/shell/impact pooling |
| Enemies | x −10, z +16 | two enemies with four patrol points each, an `EnemyTrigger` across the approach; the `M` spawn point | patrol, chase, attack, damage, death, the enemy filter |
| JumpTerrain | x +10, z +16 | stepped boxes, a ramp, a gap | jump force, the grounded probe, the fixed-step impulse |
| Abilities | x +18 | a 5 m teleport ring, a dash lane with distance marks, a standing stun dummy | dash, teleport, stun, the mana economy |
| Buffs | x −10, z −16 | one buff pickup per config — extra damage, move speed, jump, stun — and a dash charge pickup | buff apply and expiry, the VFX mapping, charges |
| EffectAreas | x +10, z −16 | areas for move speed, jump, damage and the composite, plus **two overlapping jump areas** | aspect apply and discard, and the refcount fix for overlapping grants |
| Pooling | z −28 | a grid of target boxes | pool churn under sustained fire, visible in the overlay |

## Overlay

One `PlaygroundOverlay` MonoBehaviour, `OnGUI`, wired by the generator to the two contexts directly
rather than through `GameContext.Instance`, so it cannot race the singleton. Every read goes through
`TryGet*`, so an uninitialised context shows blanks instead of throwing.

Shows: nearest station, health, mana, ammo, current weapon and its kind, active buff names, jump
source count, grounded state, enemy count, per-pool counts, and the key bindings.

This needs two new read-only accessors: `GameFactory.Pools` and `EntityPrefabPool.Count`.

## Files

| File | Assembly |
|---|---|
| `Assets/Tools/Playground/Editor/Game.Playground.Editor.asmdef` | new, editor only |
| `Assets/Tools/Playground/Editor/PlaygroundLayout.cs` | layout data: positions, contents, asset paths |
| `Assets/Tools/Playground/Editor/PlaygroundBuilder.cs` | the generator and its menu item |
| `Assets/Game/Scripts/Gameplay/Level/PlaygroundOverlay.cs` | `Game` |

## Out of scope

- PlayMode tests over the stations. `Game/Tests` stays empty; this is a manual scene.
- Reviving any UI. The overlay is debug `OnGUI` and nothing else depends on it.
- The two architectural audit findings the fix pass left alone: the `StunBuff` behaviour-type list
  and buff identity as a free-text string. The playground will show their symptoms, which is the
  point of looking.

## Verification

The generator cannot be run from this session — the editor holds the project. Done means: the menu
item appears, a rebuild produces `Playground.unity`, the scene enters Play Mode with the overlay
reading sane values, and each station exercises what the table claims.
