# Camera rises over houses

## Goal

A house never hides the tank (or the character) the camera follows, and the camera never shows
anything through a wall. When a house stands between the camera and the tank, the camera **rises
straight up** until it sees the tank over the roof. It doesn't come closer to the tank for that.
When the house is out of the way, the camera eases back down to where the follow puts it.

Only buildings move the camera: voxel houses, Victorian houses and towers. The ground, trees, plain
voxel walls, RayFire walls and anything moving never do. And only what is **below** the camera moves
it, and only **up**: a roof or an overhang over the camera never pushes it down or in.

## How the camera works

`CameraFollowController` (player context, LateTick) eases `CameraRoot` towards
`CameraPoint + offset`. The offset is `CameraOffset × CameraDistance`, plus the cursor look's shift
and pull-back. The camera is a child of the root; the shake moves it locally. `VoxelSandboxOrbit`
turns `CameraOffset` and the rig round the tank with the mouse (see
`2026-11-16-sandbox-mouse-look.md`).

## Design

- **Two positions.** The follow eases a position of its own, exactly as it eased the root before.
  The root is then raised from there. If the root itself were eased, each frame would start from
  the raised spot.
- **What counts as a building.** `CameraCollisionUseCase.IsBuilding` accepts a collider under a
  `VoxelStructure` whose new `IsBuilding` getter is true (shape House, Victorian or Tower). Only the
  `Obstacles` layers are searched (Default, where the voxel houses are). These never count
  (`Blocks`):
  - triggers;
  - anything under the followed entity's `Transform`;
  - any non-kinematic rigidbody (knocked-off voxel chunks).
- **Seeing the tank.** `Sees` sweeps a sphere of `ViewRadius` (0.8 m) from the camera towards a
  point `TargetHeight` (1 m) above the camera point, stopping `ViewRadius` short of it. Two reasons:
  - Clearing a thin line to the point on the ground was not enough: the camera rose until that
    point showed while the tank itself stayed hidden behind the roof ridge.
  - Sweeping from the camera, not from the tank, means a wall right next to the tank still counts.
    A sweep from the tank would start inside that wall and skip it.
- **Only from below.** `Sees` leaves out what lies above the camera (`IsAbove`). A hit along the
  sweep is above when its point is. A collider the sphere already overlaps at the start comes with
  no point (Unity reports it at distance 0); it is above when its nearest point to the camera is
  (`Collider.ClosestPoint`, or the bounds for a concave mesh). A camera inside a building is its own
  nearest point, so that still counts and the camera rises out.
- **Rising.** `RiseToClear` raises the camera in 0.5 m steps, up to `MaxRise` (30 m), until it
  sees the tank. The first version swung the camera round the tank instead, capped at 80° above the
  horizon. With the sandbox orbit already near vertical there was no room left to swing, so the
  camera fell back to closing in and went **down**. A straight rise has no such cap.
- **Tilt.** The camera child is turned by the same rotation that takes the old tank-to-camera
  direction to the new one, so the tank stays where it was on screen. The root's own rotation (the
  game's angle, the sandbox orbit) is left alone. The swing goes on top of the camera's own local
  rotation from the scene, kept from `Init`. Until 2026-11-16 the swing alone was written, so a
  camera tilted under the root in the scene (VoxelSandbox's, −24.5°) looked straight along the root
  in Play.
- **Easing.** The camera rises at `RiseSpeed` (10/s) and settles back down at `ReturnSpeed` (3/s).
  Both are frame-rate independent.
- **Nothing clears it** (the tank is inside a house, under its roof): no rise, and the camera stays
  where the follow puts it.
- **Settings.** `CameraSystemInstaller._collision` is installed as
  `PlayerContextAPI.CameraCollisionSettings`. It is optional: a context without it (the follow
  tests) follows through houses as before.

## Removed: the pull-in

Until 2026-11-16 the camera came in along its line under the ceiling when no rise saw the tank
(`Clearance` swept a sphere of `Radius` 0.35 m out from the camera point). That was the one move
that took the camera down, and an overlap with a building over the camera could trigger it: the
rise never cleared, so the camera dived towards the tank. The user asked that only what is under
the camera move it. `Clearance`, the `Radius` setting and the pull-in are gone.

## Checked

- `CameraCollisionUseCaseTests` (EditMode) cover:
  - `Sees` from a camera over the pivot: triggers, moving bodies, the followed body, other layers
    and filtered-out colliders don't hide the target; a wall below and a kinematic body do; a slab
    over the camera that the sphere touches does not; a camera inside a house does not see;
  - the rise: it goes up just enough (half a metre less still doesn't see over), it still goes up
    when the camera is already high over the tank, it doesn't clear under a roof, and an overhang
    over the camera makes no rise;
  - the easing.
  The `Clearance` tests were turned into the `Sees` ones above. The suite was **not run** on
  2026-11-16: VoxelSandbox had unsaved edits, and a test run in the shared editor opens a modal
  save dialog. The same overhang / wall / inside-a-house cases were run by hand in Play
  (`script_eval`) against the compiled code: sees over the overhang (rise 0), the wall below hides,
  inside a house hides.
- In VoxelSandbox, in Play, with the rise version (before the pull-in was removed):
  - Tank 1.8 m behind the 13 m House_03, camera almost straight above. Without a rise the house
    hides the tank. The camera rose straight up from 16 m to 46 m (the 30 m cap), tilted 13.5°,
    and showed the tank over the roof. Against a house that tall and that close, only that much
    height sees over the ridge.
  - Tank moved into the open: the camera came back down to its normal height in about 1.5 s, and
    the tilt went back to 0.
- Earlier, the swing version rose with the tank behind House_03 (61° → 70°, distance
  kept), but the tank stayed half hidden behind the ridge. That is what led to `Sees` with a view
  radius and a target height. Then the swing went down instead of up when the orbit was near
  vertical, which led to the straight rise.
