# Mud chunks, tank dirt, deeper mud

## Goal

What the tracks throw up reads as chunks of mud, not specks; the chunks stay where they land and melt
away; turning throws the most; the tank gets dirty; the mud is twice as deep.

## Chunks (MudSurface.Throw, MudGroundBuilder.BuildClumps)

- Mesh particles instead of billboards: `Assets/Game/Models/MudClump.asset`, a low torn splat, not
  a ball: radius swings 0.55–1.35, squashed to about half height, flat underneath, pivot at the
  bottom, and faceted (no shared vertices) so it shades as broken earth. Round, smooth, glossy lumps
  read as chocolate. `MT_MudClump` is opaque, untextured and matte (smoothness 0.18); the particle
  colour tints it, from the ground's wet-mud browns (0.15/0.095/0.05 to 0.29/0.2/0.115). The chunks
  cast shadows.
- Size 0.12–0.26 m, one in five 1.6× bigger, times the caller's `size`. Each one is flattened (3D
  size, height 0.5–0.8 of width) and turned randomly around Y, tilted up to 20°.
- They live 7–11 s. On landing they stop dead (dampen 1, bounce 0, no lifetime lost). Size over
  lifetime, per axis: they keep their shape until about 60%, then slump (height drops first, width
  spreads to 1.12) and sink away.
- The collision plane sits at `thickness × (1 − _clumpSink)` (0.45): sunk into fresh mud so a
  chunk reads as part of the ground, near the bottom of a rut.
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
- Spots of mud, never a coat: patches where 3-octave noise (3.5/m) passes a threshold of
  `0.72 − _Dirt × lerp(0.04, 0.26, (1 − height)^1.5)`, so the spots grow and multiply with dirt,
  the lower parts far more than the top, plus small flecks between them. Even at `_Dirt` 1 the
  hull and turret keep most of their paint. (A solid band climbing the tank was tried and read as
  the tank being painted over.)
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
