# Tank drift on sharp turns

## Goal

A sharp turn at high speed throws the tank into a drift: the tracks break loose and the hull slides
out of the turn, still spinning at the stick's rate, until the turn eases and the slide dies out.
Everything else — normal driving, gentle turns, any turn at ordinary speed, bumps and knocks —
stays planted: the tracks take any sideways slide out at once, as before.

## Drift (TankDriveBehaviour)

- Starts only when both hold, read off the hull's speed along its nose and the ramped turn rate
  (never off a one-step jolt of sideways speed, so the suspension's bumps cannot start one):
  - speed along the hull ≥ `driftSpeed`;
  - speed × turn rate (the sideways pull holding the line would take) > `sideGrip` m/s².
- Drifting (`Drifting`), the tracks take the slide out at only `driftGrip` m/s².
- Ends once the turn no longer qualifies (eased, or the hull slowed) and the sideways slide is
  under 0.4 m/s; from then the slide is taken out at once again.
- The push along the hull is untouched; the speed not being made along the nose is dropped as
  before, so a drift scrubs speed.
- No drift speed given (the default — the tests): never drifts.

## Tuning (TankInstaller)

- `_driftSpeed` = 7.5 m/s — above the normal top speed (6): only Shift (up to 10.8) drifts.
- `_sideGrip` = 6 m/s² — at 7.5 m/s that is about 60 % stick, at full Shift speed about 40 %.
- `_driftGrip` = 4 m/s².

First pass (dropped, seen in Play — "a cow on ice"): the side grip was a cap on the one-step
correction, so any small slip from the springs asked for more than the cap and threw the tank into
a long, weak-grip slide at any speed.

## Tests

- A slide with no sharp turn is taken out in one step, no drift.
- A sharp turn at low speed: no drift.
- A sharp turn at high speed: the tracks break loose and push back only `driftGrip`.
- The turn eased and the slide gone: the tracks bite again.
