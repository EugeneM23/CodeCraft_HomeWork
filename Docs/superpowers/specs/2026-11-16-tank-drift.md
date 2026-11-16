# Tank drift on sharp turns

## Goal

A sharp turn at speed throws the tank into a drift: the tracks break loose and the hull slides out
of the turn, still spinning at the stick's rate, until the slide dies out and the tracks bite again.
Gentle turns, and any turn at a crawl, stay planted as before.

## Side grip (TankDriveBehaviour)

- The push that takes the sideways slide out of the hull is capped at `sideGrip` m/s² (times the
  share of wheels on the ground). A turn holds the hull's velocity at speed × turn rate sideways, so
  full stick at full speed (6 m/s × 78°/s ≈ 8.2 m/s²) breaks loose; half stick (≈ 4.1) does not;
  Shift on the gas (≈ 14.7) drifts hard.
- Once loose (`Drifting`), the tracks hold only `slideGrip` of that. The drift goes on until the
  sideways slide is under 0.4 m/s, then the full grip comes back.
- The push along the hull is untouched. The speed the hull is not making along its nose is dropped
  as before, so a drift scrubs speed.
- No side grip given (the default — the tests): the slide is taken out at once, as before.

## Tuning (TankInstaller)

- `_sideGrip` = 6 m/s², `_slideGrip` = 0.5.
- 6 m/s² is about a 37° slope's sideways pull: a tank standing across a hill does not slide off.

## Tests

- A slide within the side grip is taken out in one step, no drift.
- A slide past it: the tracks break loose and push back only sideGrip × slideGrip.
- Drifting, the slide falls under 0.4 m/s: the tracks bite again.
