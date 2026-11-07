# Mud chunks, tank dirt, deeper mud

## Goal

What the tracks throw up reads as chunks of mud, not specks; the chunks stay where they land and melt
away; turning throws the most; the tank gets dirty; the mud is twice as deep.

## Chunks (MudSurface.Throw, MudGroundBuilder.BuildClumps)

- Mesh particles instead of billboards: `Assets/Game/Models/MudClump.asset`, a low torn splat, not
  a ball: radius swings 0.55–1.35, squashed to about half height, flat underneath, pivot at the
  bottom, smooth-shaded like the ground (hard facets read as rocks set on the mud). Round, glossy,
  orange-brown lumps read as chocolate. `MT_MudClump` is opaque and untextured, smoothness 0.3
  (between the ground's wet and dry sheen). Colours come from the ground's own material at start
  (`MudSurface.TakeGroundColours`): its wet and dry mud mixed at its `_Wetness`, 35% either way
  towards wet and dry, so a clump is the floor's colour. The serialized colours are only a
  fallback. The chunks cast shadows.
- Size 0.18–0.32 m, one in five 1.5× bigger, times the caller's `size`: few and big enough to be
  seen flying from the game camera. Gravity modifier 3: heavy wet mud in a short, fast arc (1.3
  made them float like light flakes, 2.2 still too light). Each one is flattened (3D
  size, height 0.5–0.8 of width) and turned randomly around Y, tilted up to 20°.
- They live 7–11 s. On landing they stop dead (dampen 1, bounce 0, no lifetime lost). Size over
  lifetime, per axis: they keep their shape until about 60%, then slump (height drops first, width
  spreads to 1.12) and sink away.
- The collision plane sits at `thickness × (1 − _clumpSink)` (0.12): just under the surface, so a
  chunk lies bedded in its own splat. At 0.45 the mud swallowed the chunks whole and only their
  splats showed, as if the marks came first.
- `Throw` lifts every start point to at least 0.08 m above the visible mud (collider top +
  thickness). Callers pass points on the collider — a track's contact, a bullet's or blast's hit —
  which lie under the mud: started there, a chunk hit its own floor from below at once and stuck,
  so chunks and splats seemed to appear from nowhere with nothing flying (seen in Play: every
  chunk under 0.3 s old sat at 0.28 m, under the 0.32 m surface).
- Every chunk leaves a mark where it lands. `Throw` works out the landing point and time from the
  arc (gravity × the system's gravity modifier, no drag) down to the clumps' floor (checked in Play
  against a live particle), and 0.08 s after it presses a splat no bigger than the chunk: a rough
  dent `0.32 × size` long (up to 1.3× longer along a fast flight), `0.28 × size` wide,
  `clamp(0.35 × size, 0.03, 0.12)` deep, rim 0.25–0.45. Throws whose landing is off the mud leave
  nothing.
- A rolling track throws a sparse, steady stream off its trailing end, against the way it moves:
  1.5 per rolled metre and never fewer than 1 a second. 70% leave from the track's outer edge
  (0.5–0.8 × track width out) and land just past the rut and its berm, about half a metre out:
  out at 0.9–1.4 m/s; 30% leave from across the track (±0.3 × width) with almost no sideways push
  (±0.15 m/s) and drop back into the rut behind. All of them: back at `0.15 × track speed + 0.3–0.9` m/s, up at `1.4–2 + 0.05 × speed`
  m/s, 0.2 m/s of spread. Thrown inward or straight back, chunks fell into the ruts; thrown harder,
  they flew metres.

## Turns (TankTrackMarksBehaviour.ScrapeClumps)

In a turn each end of each track slides sideways at `yawRate × arm`. Per metre slid it shoves 2.5 chunks
(size 1.15), always out past that track's outer edge (whichever way the end slides), at 0.8–1.4 m/s
plus 0.25× the slide speed, up at `1.2 + 0.15 × slide speed`, carried a little along the slide. Turning on the spot throws mud from all
four track ends.

## Tank dirt (TankDirtBehaviour, Game/TankDirt)

- `MT_Tank_Body` and `MT_Tank_Track` use `Game/TankDirt`: Standard lighting plus a mud layer. At
  `_Dirt` 0 it looks as before.
- The track marks report mud churned: 0.005 per rolled metre, 0.01 per metre slid in a turn
  (per side): spots show within seconds and the tank is at its dirtiest after a minute or so of
  driving. Dirt caps at 1 and never washes off. Every splash makes it wet (glossy, dark); it dries dull over 30 s.
- Spots of mud, never a coat: patches where 3-octave noise (3.5/m) passes a threshold of
  `0.7 − _Dirt × lerp(0.1, 0.36, (1 − height)^1.5)`, so the spots grow and multiply with dirt,
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
