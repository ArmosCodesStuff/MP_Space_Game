# Ledger: fix group a1-g-a1ripdummy4-a1shotsstalecam-a1warp2f (round a1, kind check, 4 tasks)

Writer: one agent in `WarShips_wt_fix0`, branch `wt/fixa1ga1ripdummy4a1shotsstalecama1warp2f`, cut from version-l 66466b6.
Tasks: a1-rip-dummy4, a1-shots-stale-cam, a1-warp-2frames, a1-heavy-target. One commit per task.

## PRE (66466b6)

- a1-rip-dummy4: `FieldsRipYardChecks` tears `TargetDummy4` (Hub.PracticeTargets row 4 is a fighter,
  named `PracticeFighter1`, not a fourth hulk) -> `H.GetNode<TargetDummy>("TargetDummy4")` returns
  null -> NRE in `Fx.LengthOf`. tp4ar1_1/2_solo.fails.txt:837 seed 11400714819323333590.
- a1-shots-stale-cam: `LaneA6dRodFrames` zooms the `cam` it fetched before the class swap; the dart's
  3 s sprint runs long enough for `ApplyLocalIdentity` to free that Camera2D and hand the viewport a
  fresh one, so `cam.Zoom = z0` throws ObjectDisposedException and the sweep dies at 76c (frame 47) and
  everything after. tp4ar1_4/2_screens.fails.txt:2.
- a1-warp-2frames: `WarpHold`'s release lookahead anticipates only 1 frame of input latency between
  `KeyUp` and `Input.IsKeyPressed(V)` going false, but a queued `InputEventKey` actually takes 2; every
  charge runs 2 frames (~27 u at 800 u/s) past its target. tp4ar1_0/2_solo.fails.txt:385 seed
  11400714819323392986: 827 u want 800 (+27 u on every held value, all 4 seeds).
- a1-heavy-target: the heavy-raider block spawns `hv` at `hm.Position + Vector2.Right.Rotated(VaryAngle()) * Vary(2600,3400)`
  with an UNRESTRICTED bearing; `hm` sits only 2000 u from the base, so a bearing pointed back at the
  base can land `hv` past it, inside the base's own guns -- it takes another target and dies there
  instead of cruising in on `hm`. tp4ar1_0/2_solo.fails.txt:691,698 seed ..392986: burns 2990 u
  (want 2250), posts 2171 u off; SmokeTest.cs.txt:18484 `hv.Position` on a freed `hv`.

## POST a1-rip-dummy4

`FieldsRipYardChecks` now tears `TargetDummy1`, `TargetDummy3` (the spec's 14.72 u literal) and
`PracticeFighter1` (row 4, sized off its own length via `float.NaN`) -- three seeded tears, the last
watched to the end, then the scar cap. `asserted`: 14.72 u chunk on TargetDummy1/TargetDummy3
(`FieldsRipChecks`, kits_v3 §3.2). Proved: `typecheck.ps1` 0 errors; `verify.ps1 -Quick` (see below).
Checks: FieldsRipYardChecks.

## POST a1-shots-stale-cam

`LaneA6dRodFrames` re-fetches `GetViewport().GetCamera2D()` after `ApplyLocalIdentity`'s class swap
instead of zooming the pre-swap reference, guarded by `IsInstanceValid`. `asserted`: 76c_dart_sprint_rod
and every later frame rendered, LINT 0. Proved: `typecheck.ps1` 0 errors; `verify.ps1 -Quick`.
Checks: _Shots.LaneA6dRodFrames (76c) and every frame after it.

## POST a1-warp-2frames

`WarpHold`'s release lookahead is now latency-aware: 2 frames for a raw key hold (the queued
`InputEventKey` path), 1 frame for a `driver` hold (`DriveHeld` is read directly, no event queue).
`asserted`: 800 u at 2.0 s held, 2400 u at 4.0 s (Drives.Reach, kits_v31 §3.4). Proved: `typecheck.ps1`
0 errors; `verify.ps1 -Quick`. Checks: WarpHold, LaneBWarpChecks.

## POST a1-heavy-target

`hv`'s spawn bearing is now held within ~57 deg of straight out from the base (`hmOutward.Angle() +
Vary(-1,1)`), so it always lands farther from `Hub.BasePos` than `hm` and never on the base's side of
it. `asserted`: 2250 u burn-in distance (3 s at 700% + 150 u reach), posted astern within tolerance.
Proved: `typecheck.ps1` 0 errors; `verify.ps1 -Quick`. Checks: Solo() raiders (6b) heavy block.
