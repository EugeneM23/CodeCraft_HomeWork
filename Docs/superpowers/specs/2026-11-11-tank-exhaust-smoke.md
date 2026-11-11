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
- Smoke: `realisticSmokePuff_alphaBlended`, near-black (0.07–0.14 grey). Puffs swell 4× over
  1.2–2 s, rise a little (gravity −0.08), are braked by drag (2) and drift on noise. At most 150
  particles a pipe.
- Built with the tank (`Tools/Tank/Build`). `Tools/Tank/Add Exhaust` adds the pipes to the existing
  `Tank_01` prefab without rebuilding the rest (replacing pipes already there).

## Smoke (TankExhaustBehaviour)

- Cranking: over the 2 s the jets grow (smoothstep) from 12 to 130 puffs a second a pipe, start speed
  1.2 → 6 m/s, size 0.3 → 0.75 m, alpha 0.95, flickering ±30% (Perlin) as the cylinders catch.
  Afterwards it eases down to idle over about half a second.
- Running, the load (0 idle … 1 labouring in first gear) comes from gaining speed, not from speed:
  - `gaining` = horizontal acceleration (smoothed, 0.2 s) against 0.6 × the boosted acceleration,
    times the stick, clamped to 0…1.
  - The speed range up to the boosted top speed is split into 4 gears. The top gear smokes half as
    much as the first; within a gear the smoke eases off by 45% as it revs up, and surges back on the
    shift up, with a puff of 6 thick particles from each pipe (only while gaining).
  - Rolling at a steady speed: `stick × (0.15 + 0.15 × speed share)` — a thin stream.
  - Load = the larger of the two.
- Idle → full load: 5 → 80 puffs a second a pipe, start speed 0.6 → 5 m/s, size 0.22 → 0.5 m,
  alpha 0.55 → 0.92. Values rise within about 0.12 s and fall over about 0.5 s.
- Engine off: the pipes stop emitting; smoke already out thins away. It simulates in world space, so
  at speed it is left behind as a trail.
