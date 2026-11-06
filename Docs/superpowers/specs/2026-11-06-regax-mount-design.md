# Regax mount — ride a Regax like the tank or the helicopter

## Goal

A Regax stands in the Playground. The player walks up to it, presses E, climbs on, and now
controls the creature instead of the character. E again climbs off. Only this one new prefab is
rideable; the enemy `Regax.prefab` and every Regax spawned from it stay enemies.

## Prefab

`Assets/Game/Prefabs/Vehicles/Regax_Mount.prefab`, built by **Tools/Regax/Build Mount**
(`Editor/RegaxMountBuilder.cs`) from `Regax.prefab`, which it does not modify. Re-running rebuilds
the prefab and puts the Playground copy back at (5, 0, −7), facing west towards the player's start.

Kept from the enemy: skeleton, animator (`AC_Rapax`), blood, sounds, buff installer, and the claws
as a nested `Regax_Hand` instance.

Changed:

| What | Enemy Regax | Regax_Mount |
| --- | --- | --- |
| Core installer | `EnemyCoreInstaller` (patrol, chase, attack AI) | `MountInstaller` (no AI) |
| Layer | 8 Enemy | 9 Character, like the tank and helicopter |
| Rigidbody | linear damping 10 000, Y rotation free, no interpolation | damping 0, rotation frozen like the player's, interpolated |
| Claws | hit layer 9, 10 dmg, 0.9 m sphere at the feet | hit layer 8, 50 dmg, 1.4 m sphere 1.3 m ahead, 1 m up |
| Extra children | `<PatrolPoints>` | `<CameraPoint>`, `<ExitPoints>` (Left, Right, Back) |
| Highlight | none | pickups' outline on `<Skeleton>` |

## How it rides

`MountInstaller` makes the creature a vehicle to the player context: `VehicleTag`, `Driver`,
`ExitPoints`, `InteractAction` and `VehicleExitAction`, all through `VehicleUseCase`, as the tank
does. Unlike the tank it has no `DriveInput`. It carries the keys a walking character has instead
(`MoveDirection`, `RotateDirection`, `MoveCondition`), so:

- WASD walks it in world directions (`CharacterMoveController`), the body turns to the cursor
  (`CharacterRotateController`), the walk blend tree plays as it does for the enemy.
- It is moved through its Rigidbody (`RigidbodyMoveBehaviour`), not by transform as enemies are,
  so it stops at walls and slides along them. Speed 7 m/s.
- Left mouse button swings the claws (`VehicleFireController` → `CharacterFireAction`). The swing
  animation fires on the weapon's `FireEvent`; the damage lands on the clip's `damage_cast_event`,
  on the first enemy in the sphere. Walking stops while the swing's cooldown runs.
- Camera: `CameraPoint` plus `CameraDistance` 1.25 (the creature is over 4 m tall).
- 300 HP. Enemies hit it like the tank; while ridden they target it instead of the rider.
- When it dies, `MountDeathBehaviour` throws the rider off first, alive, with the controls handed
  back, then the usual `DeathBehaviour` takes the body (death animation, blood). Nobody can climb
  back onto the corpse.

## Code changes

- New: `Entities/Mount/MountInstaller.cs`, `Features/Mount/Model/MountDeathBehaviour.cs`,
  `Editor/RegaxMountBuilder.cs`.
- `VehicleUseCase.Exit`: `DriveInput` is now optional, and a vehicle with a `MoveDirection` has it
  zeroed on exit too, so a dismounted creature does not keep walking.

## Found on the way

The enemy's Rigidbody damping of 10 000 is invisible to an enemy (moved by transform), but it
ate the whole velocity `RigidbodyMoveBehaviour` gives within the same physics step: the creature
played its walk animation on the spot. The builder sets damping to 0.

The enemy's body is not interpolated either. Ridden, it shook: the body moves in 50 Hz physics
steps while the camera follows it every rendered frame, so at ~100 fps every other frame showed it
standing still and the next one jumping twice as far. Measured over 60 frames at 7 m/s: per-frame
speed 0–14.9 m/s (σ 6.0) without interpolation, a flat 7.0 m/s (σ 0) with it. The builder sets
`Interpolate`, as the tank and helicopter have.

## Verified in Play mode

Getting on (character hidden, Character = mount), walking until a wall stops it, a claw swing
taking an enemy Regax from 100 to 50 HP, getting off (rider placed at an exit point, mount stops),
getting back on, and killing the mount while ridden (rider ejected alive with 100 HP, controls
back, mount removed from the world). No errors in the console. Keyboard input was not simulated:
the game reads the legacy Input Manager, so movement was driven by writing `MoveDirection`.
