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

## Running gear cleans itself on dry ground (VoxelTrackRuts, VoxelBody.Wash)

- Off the mud (fewer than half the wheels in it), the voxel wheels, idlers and sprockets fling
  their mud off as they turn: a share of what is left goes per metre, so 95% is gone after
  `_gearCleanDistance` (12 m).
- Loose clumps on them (`Deposit`) fly off as clods (`ShakeOff`), out from the part's hub at up to
  `_gearFling` (3.5 m/s) plus the part's own motion.
- Stains (`Paint`) wash off too: a moving body remembers what each stained voxel was
  (`VoxelBody._beneath`) and `Wash` puts it back; half the washed voxels flake off as crumbs.
  A stained voxel shot away is forgotten; one since covered with loose mud is left for
  `ShakeOff` (see below). The ground keeps no record (static bodies are never washed).
- The track belts lose their mud colour over 12 m instead of 30.
- The rest of the hull still sheds only 2% of its loose mud per metre, as before.

## A shot shakes the tank like a wet dog (VoxelSandboxStart, VoxelTrackRuts.Shudder)

Asked for: blood and mud fly off the tank the moment it fires, like water off a dog; three shots
leave it clean.

- Every cannon shot (the weapon's fire event, in VoxelSandbox) shakes off a share of everything on
  the tank's voxel skin, every part (hull, turret, gun, wheels, track ends): stains (`Wash`) and
  loose clumps (`ShakeOff`), mud and blood alike. Before, a shot shook off 30% of the loose clumps
  only, and the stains stayed.
- Three shots in a row leave the tank clean: shot n of a row takes 1/(4 − n) of what is left
  (`VoxelSandboxStart.ShotShare`): a third, then half the rest, then all of it. Each shot takes a
  third of the dirt the row started with. A row is shots no more than 15 s apart
  (`_shotSeriesGap`; the cannon reloads in 2 s); after the third, the next shot starts a new
  row. `_shotsToClean` (3) replaces `_mudShakenPerShot`, renamed so the value kept by the open
  scene gives way.
- Like a dog's shake: clods and drops fly out flat from the tank's centre at up to 4.5 m/s
  (`_shudderKick`) and round it at up to 3 m/s (`_shudderTwist`), one way on odd shots and the
  other way on even ones, plus 0.3–1.2 m/s up and the part's own motion. Up to 700 clods per
  part per shot (`_shudderClods`; half that for stains, of which every other one flies), the
  rest just goes. The track belts lose the same share of their mud colour, and so does the
  shader dirt (`TankDirtBehaviour.Shed`).
- **The plate stays whole.** `Deposit` used to turn surface voxels of the plate into loose mud
  with no record of what they were, so `ShakeOff` took them away with the mud: the hull lost a
  layer wherever mud had been, and a thin part (the gun, 2–3 voxels across) could vanish once
  three shots shook everything off. Now `Deposit` remembers the plate under it (`_beneath`, as
  `Paint` does, on moving bodies only), and `ShakeOff` gives the plate back instead of
  removing it. Only mud added beside the plate is removed. This also stops the slow erosion
  from the 2%-per-metre shedding on dry ground.
- Tests: `VoxelBodyShakeTests` (PlayMode). A hull and a two-voxel gun, splashed 60 times with
  mud and blood, shaken by 1/3, 1/2 and 1, are voxel for voxel what they were, and their solid
  counts match. One shake by a third leaves roughly two thirds of the dirt.
  `TankShotShakeTests` (EditMode) checks the shares 1/3, 1/2 and 1, each a third of the start.
  `VoxelTankCrushTests` 5/5.
- Checked in Play (VoxelSandbox) with three Regax crushed at the tank's nose and three shots
  through the cannon's fire event, measured against the tank's voxels at start. The tank had
  123,121 voxels changed by blood and mud. After shot 1, 85,709 were left; after shot 2,
  45,568; after shot 3, none. Its voxel count was back to 144,474, the same as at start, and no
  plate voxel was lost at any step. Shot 1 threw ~7,300 clods and drops. Red drops fly off in a
  ring round the tank (`Screenshots/tank-shake-0-dirty.png`, `tank-shake-1.png`).
- Not done: only VoxelSandbox wires the shot to the shake (as before). On Playground the tank's
  shot does not shake it.
