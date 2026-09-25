# Ledger: class-kits batch, lane B (drives, helm, strafe)

Writer: one agent (opus) in worktree `WarShips_wt_drives`, branch `wt/drives`, started at f168508.
Spec: `kits_v31.md` §3.4 (V: the drive), §3.5 (helm), §6 (F21, F22, F24), §8 (lane B's row);
`kits_v3.md` §3.9 and §5; `kits_v2.md` §5. BUILD PHASE: no engine run; per job typecheck + verify
-Quick + a read of the diff; every check is written now and run in the final test phase.
A PRE with no POST is an interrupted job: compare the hashes, keep only what matches the plan, redo it.

## Decisions taken where the spec is silent (defaults; each also in the return's `open`)

- **B1** The surge's stat ids are `surge_time`, `surge_cooldown`, `surge_lift` (x1.5), not the spec's
  boost_time / boost_cooldown / boost_share: the Dart's barrel roll already owns `boost_time`,
  `boost_speed` and `boost_rof` (Ships.cs), so the spec's names would collide on the Dart's sheet. The
  lift is a multiplier (x1.5) because F1's SpeedStat is one (LiftShares takes lift - 1).
- **B2** Decisions 1, 2, 3, 12, 13 take kits_v31 §10's defaults: Shift + A/D strafe on the nine at half
  top; freighters 85 -> 120; the boost does not break a web; the overshoot is proportional (2 s per
  300 u); an overshoot disables helm, guns, abilities and the drive, PD and craft out keep going.
- **B3** "Refused ANCHORED" is read from the helm, never from a class: any drive press is refused
  ANCHORED while `Held` is 0 (a running row's Hold of x0). Today only the railgun's charge is one; the
  6c Anchor will be the other. So the check proves it with the railgun charge.
- **B4** The drive row sits in `Abilities.For` between the class's own rows and the six open keys,
  so it has a slot on the wire and a box on the bar. It is not in `ClassDef.Abilities`, so no wall
  reaches it (Unlocks reads that list) and a Resupply (6b) never will. `Abilities.Bind` refuses to
  move it off V (V stays fixed): one line in lane A's file, besides Reserved and For.
- **B5** The warp's charge is the OWNER's (it flies); its cooldown is the drive slot's `Cool`, which
  the host sets when it prices a jump (and the owner predicts). The owner locks itself at once on an
  overshoot (its own `Lock`), until the host's Disabled bit arrives.
- **B6** The host prices a jump between two reports of the same live ship: when the charge bit falls
  with the hull moved past the flight it could have made in that time, or on any snap over 600 u; a
  host-caused relocation (Restore + NetPlace of a returning pilot) opens 1 s in which nothing is
  priced; a ship new to a world has no previous report. Only a warp hull is priced.
- **B7** The host's speed clamp (F24) exists from this lane: the velocity the host reads off a report
  is held to hypot(top, strafe_speed) x 1.1, from the host's own lifts, and every cut is counted
  (`SpeedClamps`) for the rung-5 check "never fires on a boosted slide". Slipstream (6d) will read it.
- **B8** The boost's cooldown goes through `Cooling()` (the pilot's cooling points), as every ability.
- **B9** The K window "lists Shift": the boost row's blurb says Shift + A/D strafes, and the controls
  line names the class's drive and, on a strafing hull, Shift. Hints gains rows "warp" (capital
  text), "boost" and "strafe"; a hull meets its own drive's card.

## Jobs (foundations first)

| job | what | files |
|---|---|---|
| J1 | F22 helm rows: BB 88/47/20/30, CV `CarrierTop` 99, DD 117/63/27/40.5, FR/TE/BA 120/63.5/28.2/42.4; Stats defaults to the BB's; rewrite the old-speed checks; new LaneBHelmChecks | Ships.cs, Stats.cs, SmokeTest |
| J2 | F24 strafe helm: Helm rows strafe_speed / strafe_thrust (0 by default; the nine's rows), Shift in Reserved, the across law in Steer (lifted, held, 0 under a web or Disabled), LocalFlight reads Shift; LaneBStrafeChecks | Stats.cs, Ships.cs, Abilities.cs (Reserved), PlayerShip (Steer, LocalFlight), SmokeTest |
| J3 | F21 Drives: `Drives.cs` (warp Jump, boost Surge), `ClassDef.Drive`, `For` appends the drive row, the drive's stat rows on the sheet, V presses the drive (Hub), the warp hold/charge/landing/overshoot on the owner, the boost as a slot, the drive's slot and hull-bar readout, the range ring/band/ghost/readout draw, MainMenu's dodge as a scripted hold, Hints rows, the controls line; delete the warp block; every caller fixed; LaneBWarpChecks, LaneBBoostChecks, rewritten menu/hint/snap/For-length checks | Drives.cs, Ships.cs, Stats.cs, Abilities.cs, PlayerShip, Hub, HubNodes, MainMenu, Hints, SmokeTest |
| J4 | Wire: the host prices a guest's jump (B6), the host's speed clamp by hypot (B7); guest-role checks (arena guest: overshoot priced 4 s with its own lock bypassed, a bit-less 3300 u snap priced 6 s, boost and strafe replicate within 20 u with no clamp) | Drives.cs, PlayerShip (ApplyState, Restore), SmokeTest (ahost/aguest) |
| J5 | Frames: warp charging inside the ring, past the safe ring (band, ticks, ghost, readout), the landing, DISABLED; the boost readout | Shots.cs.txt |
| J6 | Record: CHANGES Unreleased + Handoff, DESIGN trap lines; final POST with what the test phase owes | docs |

## Log

### J1 PRE · tier opus · F22 helm rows
Intent: capitals as kits_v3 §3.9 (BB 88/47/20/30, CV CarrierTop 99, DD 117/63/27/40.5), freighters to
120 (thrust 63.5, reverse 28.2, astern 42.4); Stats' Helm defaults follow the BB. Rewrite the checks
asserting 104/130/116.48; new LaneBHelmChecks (tops, DD > CV > BB, thrusts, t95 within 0.05 s of today's).
HEAD 7ffa6d9. Ships.cs c272c9b · Stats.cs fdaff34 · SmokeTest.cs.txt 9949bb6.
### J1 POST · green (typecheck 0 errors, verify -Quick ALL CHECKS PASSED)
Files: Ships.cs, Stats.cs, SmokeTest.cs.txt (LaneBHelmChecks; rewrote the BB 104, DD/CV speeds and the
DD 130 flown check). engine-unproven: rungs owed in the final test phase (solo x2).
Note for the coordinator: Enemies.cs:30 says `Cruise = 100f` is "under every capital ship's top speed
(104 to 130)"; with F22 the BB is 88 and the CV 99, so raiders now cruise faster than two capitals.
Not this lane's file; the comment (or the number) is lane A/G's to settle.
Next: J2.

### J2 PRE · tier opus · F24 strafe helm
Intent: Helm rows strafe_speed / strafe_thrust (sheet default 0: the capitals never strafe, read from
the stat), lights 130/520, heavies 95/380, freighters 60/120; Shift in Reserved; LocalFlight turns A/D
into strafe while Shift is held on a hull whose strafe_speed > 0; Steer's across law (toward
±strafe_speed x lift x hold at strafe_thrust x lift; keel grip otherwise; 0 under a web or Disabled).
HEAD 1b80fee. Stats.cs 954412b · Ships.cs 46cb519 · Abilities.cs 4945b7f · PlayerShip.cs 2a91383 · SmokeTest.cs.txt 267132c.
### J2 POST · green (typecheck 0, verify -Quick ALL CHECKS PASSED)
Files: Stats.cs (strafe rows), Ships.cs (the nine's rows), Abilities.cs (Shift in Reserved), PlayerShip
(LocalFlight Shift, Steer's across law, StrafeTop, SpeedAcross), SmokeTest (LaneBStrafeChecks; J1's
method had landed between lane A's F20 comment and its method: the comment is back above its method).
engine-unproven: rungs owed in the final test phase (solo x2). The boosted slide (195/142.5/90) is J3's.
Next: J3.

### J3 PRE · tier opus · F21 Drives (owner side)
Intent: Drives.cs (warp Jump on BB/CV/DD, boost Surge on the nine; rows warp_safe/rate/cooldown and
surge_time/cooldown/lift), ClassDef.Drive on every hull, For appends the drive row, the sheet adds the
drive's rows, V presses the drive (Hub), the owner's hold/charge/landing/overshoot lock, the drive's
slot + hull-bar readout, the range UI draw, MainMenu's dodge as a scripted hold, Hints rows warp /
boost / strafe, the controls line, Bind keeps a fixed-key row on it; the warp block deleted, every
caller fixed; checks LaneBWarpChecks / LaneBBoostChecks and the rewritten menu, hint, snap-flash and
For-length checks. Host pricing and the clamp are J4.
HEAD ae4bd1f. Drives.cs new · Ships.cs 8798ffa · Stats.cs f1dff0c · Abilities.cs 03823d4 · PlayerShip.cs
8e614d5 · Hub.cs d10481a · HubNodes.cs 60c27d9 · MainMenu.cs d83b334 · Hints.cs 87bce0b · SmokeTest 69f5860.
### J3 POST · green (typecheck 0, verify -Quick ALL CHECKS PASSED)
Files: Drives.cs (new), Ships.cs (ClassDef.Drive x12), Stats.cs (drive rows), Abilities.cs (For, Bind's
fixed-key line, controls line), PlayerShip (drive block replaces the warp block; Disabled = status or
the owner's lock, read by Steer and PressHeld; ChargeHeld/ChargeReach/DriveLock/JumpFlash), Hub (V ->
PressDrive; the drive's and strafe's cards), HubNodes (the drive's readout), MainMenu (the dodge is a
held warp to 500 u, warp_cooldown 7 as a row override), Hints (warp / boost / strafe), SmokeTest
(LaneBWarpChecks, LaneBBoostChecks; rewrote the menu hop, the dodge comment, Warping -> Charging, the
hint card, the snap flash, the BB and DD bar sequences, For lengths +1: 7090, warrior 10, freighter 13),
Shots.cs.txt (LaneBDriveFrames: 40, 40b, 41, 41b, 42, 42b). J5 (frames) is folded in here.
Blurbs carry no digits (the harness's rule); the warp row pressed as an ability says HOLD V (Fly).
Host pricing and the clamp (J4) were cut out of Drives.cs to keep UNUSED at 0 until J4 uses them.
engine-unproven: rungs owed in the final test phase (solo x2, screens).
Next: J4.

### J4 PRE · tier opus · the wire: host pricing, the speed clamp, guest-role checks
Intent: Drives.Priced (a guest's charged jump when its bit falls, any snap over 600 u; quiet for 1 s
after a relocation the host makes -- Hub's two NetPlace sends call PlayerShip.Relocated; only warp
hulls), Drives.Clamp (the host reads a report's speed held to hypot(top, strafe) x 1.1, counted);
`Drives.PriceSnaps`, a harness switch like Net.SkipGoodbye: the host roles turn bit-less snap pricing
off because their guests are moved by hand (warp hulls: Guesty's carrier, Third's battleship, the
arena guest's destroyer), and LaneBHostDrives turns it on for its own window. Guest-role checks in
the arena pair (LaneBGuestDrives / LaneBHostDrives) before "TWO OF THE NINE".
HEAD 9a90f20. Drives.cs 1177514 · PlayerShip.cs 8055f5e · Hub.cs 708ca91 · SmokeTest 4af7b52.
### J4 POST · green (typecheck 0, verify -Quick ALL CHECKS PASSED)
Files: Drives.cs (Priced, Clamp, SpeedCap, PriceSnaps, DriveRun's host fields, Quiet), PlayerShip
(ApplyState: the host prices and clamps a guest's report; Relocated, SpeedClamps, TopNow, StrafeNow),
Hub.cs (both NetPlace sends call Relocated: two one-line hunks), SmokeTest (LaneBHostDrives /
LaneBGuestDrives at the top of the arena pair; `Drives.PriceSnaps = false` at the start of the host
and ahost roles, since their guests' warp hulls are moved by hand; WarpHold gains `driver:`).
Risk for the test phase: the arena pair now runs ~30 s longer before "TWO OF THE NINE"; the host waits
for the guest's freighter with a 70 s (90 s wan) ceiling. The host's copy lags a fast guest by the
smoothing (about v/12 u), so "within 20 u of its report" is read at rest after the boosted slide.
engine-unproven: rungs owed in the final test phase (six x2 for the new guest checks).
Next: J6 (record); J5's frames went in with J3.
