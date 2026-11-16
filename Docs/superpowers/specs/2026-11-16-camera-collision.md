# Camera keeps out of walls

## Goal

The follow camera (on foot and in a vehicle) never shows anything through a wall or a house. When a
wall, a voxel house or a standing RayFire wall gets between the camera point and the camera, the
camera moves in along that line to just in front of it. When the obstacle is gone, the camera eases
back out to its normal distance.

## How the camera works

`CameraFollowController` (player context, LateTick) eases `CameraRoot` towards
`CameraPoint + offset`. The offset is `CameraOffset × CameraDistance`, plus the cursor look's shift
and pull-back. The camera sits at the root (local zero); the shake moves the camera locally.
`VoxelSandboxOrbit` turns `CameraOffset` and the rig.

## Design

- **Two positions.** The follow eases a position of its own, exactly as it eased the root before.
  The root is then set on the line from the camera point to that eased position, as far out as the
  walls allow. If the root itself were eased, each frame would start from the pulled-in spot.
- **Sweep.** `CameraCollisionUseCase.Clearance` sweeps a sphere (`Physics.SphereCastNonAlloc`,
  radius 0.35 m, wider than the near plane) from the camera point towards the camera, and returns
  the distance to the first obstacle. These hits are skipped:
  - triggers;
  - anything under the followed entity's `Transform` (the tank hull);
  - any non-kinematic rigidbody: loose RayFire pieces, knocked-off voxel chunks, other tanks and
    casings, so the camera doesn't jump every time a brick flies past;
  - hits already overlapping at the start (distance 0). The camera point sits at ground level, so
    it would otherwise always hit the ground.
- **Layers** (`CameraCollisionSettings.Obstacles`): Default (voxel houses are BoxColliders on
  Default), Ground, Platform and Destructible (standing RayFire walls). Characters, bullets, Debris
  and Rubble are left out. Instanced debris cubes have no colliders.
- **Easing.** What is eased is how far the walls pull the camera in, not the distance itself.
  Pulling in is instant, so a wall is never seen through. Easing back out runs at `ReturnSpeed`
  (4/s, frame-rate independent). The follow's own zoom (look pull-back, `CameraDistance`) still
  passes straight through.
- **No minimum distance.** If the camera point is right under a ceiling, the camera goes right up
  to it. Holding it farther out would put it inside the wall.
- **Settings.** `CameraSystemInstaller._collision` (`Enabled`, `Radius`, `ReturnSpeed`,
  `Obstacles`) is installed as `PlayerContextAPI.CameraCollisionSettings`. It is optional: a
  context without it (the follow tests) follows through walls as before.

## Checked

- `CameraCollisionUseCaseTests` (EditMode): wall stops the camera; triggers, moving bodies, the
  followed body and other layers don't; a kinematic body does; pulling in is instant and the
  return is gradual. These and the existing `CameraFollowControllerTests` pass.
- VoxelSandbox, in Play, against the real voxel houses:
  - Camera point behind House_02: the camera stops 2 m out, in front of the wall, instead of 16.8 m
    out behind the house.
  - Camera point in an empty room of House_02: the camera stays under the ceiling, 2 m out, and
    shows the room.
  - Normal driving: the camera keeps its full distance.
