# Tank Shift: engine power, not a shove

## Goal

Shift does not push the tank forward by itself. It is a stronger engine: everything the stick asks
for comes quicker and goes further, turning as well as driving. A boosted turn traces the same
arc as an unboosted one, only faster.

## Drive (TankDriveBehaviour)

- Shift held with the stick off centre (either axis): `boostMultiplier` times
  - the top speed along the hull and the acceleration up to it (as before);
  - the turn rate and the turn acceleration (new; before, Shift left turning alone and applied
    only with the throttle).
- Braking (letting off, reversing) is the brakes', not the engine's: unchanged.
- Stick centred: Shift asks for nothing: no push, no spin.
- Speed and turn rate both scale by the boost, so the radius (speed / turn rate) stays the same.

## Drift retune (TankInstaller, Tank_01)

A turn asks speed × turn rate sideways. With both boosted, a Shift turn now asks 1.8 times what
it asked at the same speed before: at 13 m/s full stick 31.9 m/s² (was 17.7).

- `_sideGrip` 10 → 18 m/s² (= 10 × 1.8): the boosted drift keeps the shape it had. The hull's
  heading still turns about 1.8 times as fast as its velocity (2.45 against 1.38 rad/s, was 1.36
  against 0.77). Half stick (15.9) still holds.
- At the normal top speed (7.2 on dry ground) a Shift turn asks 17.6 m/s². That is under the
  limit there (18 · (7.5 / 7.2)² = 19.5), so the tank still drifts only at Shift speed.
- Releasing Shift mid-drift asks 17.7 against 18: the tracks grip again.

## Unchanged

Wheel spin and roost, the exhaust, the full-gas ram and the track view read Shift as before. The
tracks already turn with the hull's real yaw rate, so a faster turn shows on them by itself.
The helicopter's Shift is unchanged.

## Tests (TankDriveBehaviourTests)

- Shift turns faster: first step at `TURN_ACCELERATION × BOOST`.
- Shift turns to `TURN_SPEED × BOOST`.
- Shift picks up speed at `ACCELERATION × BOOST`.
- Shift with the stick centred: no force, no spin.
- Shift brakes no harder: reversing sheds speed at `BRAKING`.
