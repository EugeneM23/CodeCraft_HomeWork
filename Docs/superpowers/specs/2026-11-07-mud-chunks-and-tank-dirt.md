# Mud chunks, tank dirt, deeper mud

## Goal

What the tracks throw up reads as chunks of mud, not specks; the chunks stay where they land and melt
away; turning throws the most; the tank gets dirty; the mud is twice as deep.

## Chunks (MudSurface.Throw, MudGroundBuilder.BuildClumps)

- Mesh particles instead of billboards: `Assets/Game/Models/MudClump.asset`, a lumpy low-poly blob,
  flat underneath, pivot low. `MT_MudClump` is opaque and untextured; the particle colour tints it.
  The chunks cast shadows.
- Size 0.12–0.26 m, one in five 1.6× bigger, times the caller's `size`. Each one is flattened (3D
  size, height 0.5–0.8 of width) and turned randomly around Y, tilted up to 20°.
- They live 7–11 s. On landing they stop dead (dampen 1, bounce 0, no lifetime lost). Size over
  lifetime, per axis: they keep their shape until about 60%, then slump (height drops first, width
  spreads to 1.12) and sink away.
- The collision plane sits at `thickness × (1 − _clumpSink)` (0.3): half-sunk in fresh mud, near the
  bottom of a rut.
- Tracks throw 4 per rolled metre (was 6; the chunks are much bigger).

## Turns (TankTrackMarksBehaviour.ScrapeClumps)

In a turn each end of each track slides sideways at `yawRate × arm`. Per metre slid it shoves 9 chunks
(size 1.15) out from the edge it pushes into, outwards at 1.1× the slide speed and up. Turning on the
spot now throws mud from all four track ends.

## Tank dirt (TankDirtBehaviour, Game/TankDirt)

- `MT_Tank_Body` and `MT_Tank_Track` use `Game/TankDirt`: Standard lighting plus a mud layer. At
  `_Dirt` 0 it looks as before.
- The track marks report mud churned: 0.004 per rolled metre, 0.015 per metre slid in a turn. Dirt
  caps at 1 and never washes off. Every splash makes it wet (glossy, dark); it dries dull over 30 s.
- The mud climbs from the bottom: caked up to `0.8 × _Dirt` of the tank's height, ragged by 3D noise,
  with splatter spots a little above the line.
- Each renderer gets a fixed frame into the tank's space at start (property block), so the pattern
  sticks to the part: it turns with the wheels and swings with the turret.

## Depth

`_MudThickness` 0.16 → 0.32, `_BermHeight` 0.06 → 0.12 (MT_MudGround and the shader defaults). The
mud mesh's bounds (`MAX_LIFT`) go 0.3 → 0.55 so the lifted mud is not culled.

## Not done

- No tests (asked to go straight to code).
- The Playground scene's MudClumps object was rebuilt in the editor but the scene was not saved:
  it already had other unsaved changes.
