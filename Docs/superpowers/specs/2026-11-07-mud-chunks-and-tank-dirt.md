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
- Chunks go to the sides, never back onto the ruts: each leaves from past the track's outer edge
  (0.4–0.7 × track width out) at 2.8–4.8 m/s outwards, plus the old backwards share, with only
  0.35 m/s of random spread. They land about 1–1.5 m out, clear of the rut and its berm.

## Turns (TankTrackMarksBehaviour.ScrapeClumps)

In a turn each end of each track slides sideways at `yawRate × arm`. Per metre slid it shoves 9 chunks
(size 1.15), always out past that track's outer edge (whichever way the end slides), at 2.8–4.8 m/s
plus 0.6× the slide speed, carried a little along the slide. Turning on the spot throws mud from all
four track ends.

## Tank dirt (TankDirtBehaviour, Game/TankDirt)

- `MT_Tank_Body` and `MT_Tank_Track` use `Game/TankDirt`: Standard lighting plus a mud layer. At
  `_Dirt` 0 it looks as before.
- The track marks report mud churned: 0.0015 per rolled metre, 0.003 per metre slid in a turn
  (per side; the first rates caked the whole tank in seconds of turning). Dirt caps at 1 and never
  washes off. Every splash makes it wet (glossy, dark); it dries dull over 30 s.
- The mud climbs from the bottom as one even band: solid up to `0.8 × _Dirt` of the tank's height
  with a short edge ragged only by fine grain (9/m), and a faint film of dust 0.2 above it. No large
  blotches or splatter spots: those read as camouflage, not mud.
- Each renderer gets a fixed frame into the tank's space at start (property block), so the pattern
  sticks to the part: it turns with the wheels and swings with the turret.
- The tank's bottom and top come from the meshes through those frames, not from renderer bounds,
  which are empty for a renderer that is off or not yet drawn and threw the band off.

## Depth

`_MudThickness` 0.16 → 0.32, `_BermHeight` 0.06 → 0.12 (MT_MudGround and the shader defaults). The
mud mesh's bounds (`MAX_LIFT`) go 0.3 → 0.55 so the lifted mud is not culled.

## Not done

- No tests (asked to go straight to code).
- The Playground scene's MudClumps object was rebuilt in the editor but the scene was not saved:
  it already had other unsaved changes.
