# Camera rises over houses

## Goal

A house never hides the tank (or the character) the camera follows, and the camera never shows
anything through a wall. When a house stands between the camera and the tank, the camera **rises
straight up** until it sees the tank over the roof. It doesn't come closer to the tank for that.
When the house is out of the way, the camera eases back down to where the follow puts it.

Only buildings move the camera: voxel houses, Victorian houses and towers. The ground, trees, plain
voxel walls, RayFire walls and anything moving never do.

## How the camera works

`CameraFollowController` (player context, LateTick) eases `CameraRoot` towards
`CameraPoint + offset`. The offset is `CameraOffset × CameraDistance`, plus the cursor look's shift
and pull-back. The camera is a child of the root; the shake moves it locally. `VoxelSandboxOrbit`
turns `CameraOffset` and the rig, up to nearly straight down over the tank.

## Design

- **Two positions.** The follow eases a position of its own, exactly as it eased the root before.
  The root is then raised from there. If the root itself were eased, each frame would start from
  the raised spot.
- **What counts as a building.** `CameraCollisionUseCase.IsBuilding` accepts a collider under a
  `VoxelStructure` whose new `IsBuilding` getter is true (shape House, Victorian or Tower). Only the
  `Obstacles` layers are searched (Default, where the voxel houses are). These never count:
  - triggers;
  - anything under the followed entity's `Transform`;
  - any non-kinematic rigidbody (knocked-off voxel chunks).
- **Seeing the tank.** `Sees` sweeps a sphere of `ViewRadius` (0.8 m) from the camera towards a
  point `TargetHeight` (1 m) above the camera point, stopping `ViewRadius` short of it. Two reasons:
  - Clearing a thin line to the point on the ground was not enough: the camera rose until that
    point showed while the tank itself stayed hidden behind the roof ridge.
  - Sweeping from the camera, not from the tank, means a wall right next to the tank still counts.
    A sweep from the tank would start inside that wall and skip it.
- **Rising.** `RiseToClear` raises the camera in 0.5 m steps, up to `MaxRise` (30 m), until it
  sees the tank. The first version swung the camera round the tank instead, capped at 80° above the
  horizon. With the sandbox orbit already near vertical there was no room left to swing, so the
  camera fell back to closing in and went **down**. A straight rise has no such cap.
- **Tilt.** The camera child is turned by the same rotation that takes the old tank-to-camera
  direction to the new one, so the tank stays where it was on screen. The root's own rotation (the
  game's angle, the sandbox orbit) is left alone.
- **Easing.** The camera rises at `RiseSpeed` (10/s) and settles back down at `ReturnSpeed` (3/s).
  Both are frame-rate independent.
- **Inside a house.** If no rise sees the tank (it is under a roof), there is no rise. The camera
  comes in along its line, under the ceiling: `Clearance` sweeps a sphere of `Radius` (0.35 m) from
  the camera point. Coming in is instant and going back out is eased. This is the only case where
  the camera closes in.
- **Settings.** `CameraSystemInstaller._collision` is installed as
  `PlayerContextAPI.CameraCollisionSettings`. It is optional: a context without it (the follow
  tests) follows through houses as before.

## Checked

- `CameraCollisionUseCaseTests` (EditMode) cover:
  - the sweep: triggers, moving bodies, the followed body, other layers and filtered-out colliders
    don't stop it; a kinematic body does;
  - the rise: it goes up just enough (half a metre less still doesn't see over), it still goes up
    when the camera is already high over the tank, and it doesn't clear under a roof;
  - the easing.
  The rise version and its tests build without errors outside the editor, but they have not run
  in the editor yet: Play was running and the editor could not recompile. The earlier swing
  version's tests passed in the editor, along with `CameraFollowControllerTests` and
  `CameraFollowUseCaseTests`.
- In VoxelSandbox, the swing version rose with the tank behind House_03 (61° → 70°, distance
  kept), but the tank stayed half hidden behind the ridge. That is what led to `Sees` with a view
  radius and a target height. Then the swing went down instead of up when the orbit was near
  vertical, which led to the straight rise.
