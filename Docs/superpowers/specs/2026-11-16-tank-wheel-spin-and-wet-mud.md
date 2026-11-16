# Wet mud, wheel spin on Shift, full-gas ram

## Wet mud (Game/Voxel shader)

- Every voxel colour carries wetness in its alpha: 255 dry, lower wetter, never 0 (that marks a
  free palette slot). `MeshVoxelizer.WithWetness` encodes it; `FillShades` takes it per material.
- Ground (`VoxelStructure`): wet mud 1, dry mud 0.45, muddy grass 0.35, everything else 0. Mud
  splashed onto the tank keeps the ground's wetness (the palette keeps alpha and tells colours
  apart by it), and so do clods and crumbs (debris instance alpha).
- The shader darkens wet voxels by `_WetDarken` (0.4) and lifts smoothness toward `_WetSmoothness`
  (0.65, lowered from 0.86 after a look: too much glare). Top faces shine most, sides a quarter
  less; world-space noise (`_WetPatches`, `_WetPatchScale`) breaks the sheen into puddles and
  drier crust.

## Wheel spin (TankWheelSpinBehaviour)

- Shift on the gas, wheels on the ground, engine running: `Spin` rises to 1 at a standstill and
  eases to 0.35 at the boosted top speed; it rises at 8/s and dies at 4/s once Shift or the gas is
  released. A look only: the drive's push is unchanged.
- `TankTrackView` runs the road wheels `Spin × 9` m/s faster than the ground goes by.
- `VoxelTrackRuts` (`WheelSpin`, `SpinDirection`), even with the tank standing:
  - a rooster tail of crumbs off the rear end of the tracks (the front in reverse): 480/s at full
    spin, 5–11 cm, 4–10 m/s back, 3–7.5 m/s up, fanned out up to 3 m/s sideways, the colour of
    the ground (mud or earth; on
    grass four in ten are green turf);
  - clods of a few voxels, 32/s in mud, a third of that on hard ground;
  - in mud the spinning tracks dig in: 10 bites/s, no deeper than the rut limit.

## Full-gas ram

- Full gas: Shift with the stick at 0.9 or more.
- Voxel walls (`VoxelRammer.FullGas`, set by `TankWheelSpinBehaviour`): bites 1.45× bigger,
  thrown 1.7× harder, 0.6× the interval, always across the whole bow, plus one more bite further
  into the wall.
- RayFire walls (`TankRamBehaviour`): breach radius 1.5×, pieces thrown 1.6× faster.

## Changed after a look

- More earth flying at full gas: crumbs 220 → 480/s, 4.5–9 → 5–11 cm, back 3–7.5 → 4–10 m/s,
  up 2–5.5 → 3–7.5 m/s, sideways fan −0.4…1.6 → −1…3 m/s; clods 14 → 32/s.
