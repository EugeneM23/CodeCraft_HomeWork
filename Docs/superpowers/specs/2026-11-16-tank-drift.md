# Tank drift on sharp turns

## Goal

The tank drifts only in a sharp turn at high speed: the hull slides out of the turn a little and
grips again as soon as the turn eases. At ordinary speeds, and in gentle turns at any speed, the
tracks hold exactly as before: any sideways slide is gone in one step.

## Side grip (TankDriveBehaviour)

- Holding the line in a turn takes speed × turn rate sideways.
- At or above `driftSpeed` along the hull, the tracks take the sideways slide out at no more than
  `sideGrip` m/s². A turn asking more slides out by the excess only; ease the turn and the slide is
  taken out at `sideGrip` again. No latch, no low-grip "loose" state.
- Below `driftSpeed` the limit climbs as `sideGrip · (driftSpeed / speed)²`, and under half of
  `driftSpeed` it is off: a drift that slows past the line grips back smoothly, not in one snap.
- `Drifting`: last step the limit cut in.
- No drift speed given (the default — the tests): never drifts.

## Tuning (TankInstaller)

- `_driftSpeed` = 7.5 m/s — above the normal top speed (6, 7.2 on dry ground in the sandbox): only
  Shift (10.8, 13 on dry ground) gets there.
- `_sideGrip` = 10 m/s² — at 13 m/s full stick asks 17.7, so the hull slides out by 7.7 m/s²; half
  stick (8.8) still holds.
- A bump's small slip (< 0.2 m/s) is still gone in one step at 10 m/s².

## Dropped (seen in Play)

1. Side grip as a 6 m/s² cap on the one-step correction, with a 0.5 latch: any spring jolt asked for
   more than the cap and threw the tank into a long weak-grip slide at any speed — "a cow on ice".
2. A drift latch on speed × turn rate, holding only 4 m/s² while loose and letting go only once the
   slide was under 0.4 m/s: at Shift speed a turn asks 17.7 m/s², so the hull spun while its velocity
   barely followed — even more sliding.

## Tests

- High speed, small slip: gone in one step.
- High speed, slip past the side grip: only `sideGrip` taken out, `Drifting`.
- Low speed, big slip: gone in one step.
- Just below the drift speed: the limit is `sideGrip · (driftSpeed / speed)²`.
