# Tank exhaust smoke and engine start

## Goal

Black diesel smoke out of the back of the tank. It pours out while the tank picks up speed — the
engine labouring in the low gears — not at top speed. The engine does not run the moment the driver
gets in: it cranks for two seconds, the pipes belching ever bigger jets, and only then can the tank
move.

## Engine (TankEngine)

- Off while nobody is inside. A driver gets in: `Starting` for 2 s, then `Running`. Getting out, or
  the hull going up, stops it at once; the next driver cranks it again from the top.
- `TankDriveBehaviour` holds the stick at zero until the engine runs (no engine passed — the tests —
  counts as always running). Turret and guns are not held.
- Ticks before the suspension and the drive.

## Pipes (TankFxBuilder.BuildExhaust)

- Two short pipes out of the rear plate of the upper hull, one each side (x ±0.55, y 0.95, z −1.92,
  full size), lying along the hull, a little up at the mouth. Track material, under `HullVisual`, so
  they pitch and roll with the hull on its springs.
- An emitter at each mouth, aimed back and up 20°. Narrow cone (12°), world space, looping,
  `playOnAwake` off.
- Smoke: `realisticSmokePuff_alphaBlended`, near-black (0.07–0.14 grey). One plume, not balls: the
  texture is a puff with a dense round core, so a few big dark particles read as separate balls
  flying out (seen in Play). Instead many small, faint ones start at the mouth and swell 6× (fast at
  first) into one another over 1.6–2.6 s, in a 16° cone, turning ±60°/s, rising a little (gravity
  −0.08), braked by drag (2.5) and torn up by two-octave noise (strength 0.6). At most 400
  particles a pipe.
- Built with the tank (`Tools/Tank/Build`). `Tools/Tank/Add Exhaust` adds the pipes to the existing
  `Tank_01` prefab without rebuilding the rest (replacing pipes already there).

## Smoke (TankExhaustBehaviour)

- Cranking: over the 2 s the jets grow (smoothstep) from 25 to 220 puffs a second a pipe, start speed
  1.2 → 6 m/s, size 0.12 → 0.26 m, alpha 0.3 → 0.55, rate and speed flickering ±30% (Perlin) as the
  cylinders catch.
  Afterwards it eases down to idle over about half a second.
- Running, the load (0 idle … 1 labouring in first gear) comes from gaining speed, not from speed:
  - `gaining` = horizontal acceleration (smoothed, 0.2 s) against 0.6 × the boosted acceleration,
    times the stick, clamped to 0…1.
  - The speed range up to the boosted top speed is split into 4 gears. The top gear smokes half as
    much as the first; within a gear the smoke eases off by 45% as it revs up, and surges back on the
    shift up: +0.5 load, gone over 0.35 s (only while gaining). Not a burst of big particles: those
    were balls again.
  - Rolling at a steady speed: `stick × (0.15 + 0.15 × speed share)` — a thin stream.
  - Load = the larger of the two.
- Idle → full load: 14 → 150 puffs a second a pipe, start speed 0.6 → 5 m/s, size 0.1 → 0.2 m
  (±30%), alpha 0.22 → 0.45. Values rise within about 0.12 s and fall over about 0.5 s.
- While the engine runs each pipe also emits 8 puffs per metre it travels, so the trail has no gaps
  at speed.
- Engine off: the pipes stop emitting; smoke already out thins away. It simulates in world space, so
  at speed it is left behind as a trail.
