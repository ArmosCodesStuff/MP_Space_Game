# Ledger: kits lane A, slice 6d (the lights: Dart, Echo, Wraith)

Writer: one agent at a time in worktree `WarShips_wt_kits6d`, branch `wt/kits6d`, started at 3450da4 (version-l).
Spec (newest wins): kits_v2 §3 cards (DART 363 / ECHO 439 / WRAITH 478) + §5 → kits_v3 §3.6 (Ramjet, 265) + §5 →
kits_v31 §2 (the table), §3.4 "the drive against every class's moves" (277-284), §3.6 (walls, 327-329), §6, §7, §8
(566, and the rung-5 second run / rung-4 lists 568-580); numbers_curve_raids_items.md §8 (owns only the Dart's 72.0
sheet: R6, every top-speed lift prices its guns, the boost and gear included). Rulings: version-l's
docs/plans/README.md (36-66: lights 180-220; "Approved as written: ... Echo, Wraith"; Ramjet; Rewind 8 s with hull).
Conventions and D1-D52: ledger_kits.md (D1-D27), ledger_kits5.md (D28-D37), ledger_kits6c.md (D38-D52). This
lane's decisions are **DL1...** (6a/6b number theirs in parallel; DL never collides).
BUILD PHASE: no engine run; per job typecheck + verify -Quick + a read of the own diff; every check written now,
run in the final test phase. A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits,
redo it first. Checks go in NAMED methods `LaneA6d<Thing>Checks` (SmokeTest.cs.txt, solo), `LaneA6d<Thing>Frames`
(Shots.cs.txt), rung-5 pairs `LaneA6d<Thing>HostChecks` / `...GuestChecks`. SmokeTest.cs.txt is LF: write it in
binary. Commit messages: write the file with [IO.File]::WriteAllText (PowerShell's utf8 adds a BOM), `git commit -F`.
6.7: every ability >= 3 distinct checks (effect with the spec's literals; each kits_v31 §7 / §3.4 interaction;
guest role or named frame), each from 3 varied situations (Vary / VaryAngle / VaryNear).

## Decisions taken where the spec is silent or superseded (DL1 on)

- **DL1 Hulls:** Dart 200, Echo 180, Wraith 220 (kits_v2 cards, README "lights 180-220"). Today all three read 90.
  Top 260 unchanged. Any harness check asserting a light's 90 is rewritten (6.3). Strafe 130 / 520 unchanged.
- **DL2 Learn order (ClassDef.Abilities, the walls read it; kits_v31 §3.6):** weapon rows first (never walled), then
  Dart {Rod from God F, Ramjet Q, Slingshot E}; Echo {Reverb F, Rewind Q, EMP E}; Wraith {Veil F, Venom Q,
  Shadow step E}. The walls' order check in SmokeTest (grep `AbilityAt\|LOCKED · L3`) is rewritten for the three.
- **DL3 THE WRAITH'S v1 NUMBERS ARE NOT IN THE REPO** (kits_v2: "everything else v1"; the v1 sign-off was never
  committed, `git log -S` finds no file). Default: derived from the signed power rows (models/power_v31.py:94,
  sheet 77.1 = weapon 65.35 + Veil 5.5 + Venom 6.25; kits_v2 "Point blank 65.3 DPS at 0 u", "Veil stops it being
  chosen, not being hit (F3)", "Shadow step lands it among escorts"; v3 table: blink "about 1000 u (900 + 140)";
  numbers file: "7 pellets"; level_walls: "Shadow step needs a selected target"). Owner question (default built):
  - **Ambush scattergun** (Space, turret path, Ab.Guns + Ab.FireMode kept): 7 pellets (`scatter_pellets`) x 7
    (`main_damage`) every 0.75 s (`main_interval`) = 65.33 DPS point blank; spread ±10° (`scatter_spread`), each
    pellet straight, 320 u (`main_range`), 620 u/s. A new Shots row `pellet` (appended), Stops 1.
  - **Backstab** (passive): x1.5 (`backstab_mult`) on the Wraith's own blows that land from inside the target's
    rear arc (±60° about its tail, `backstab_arc` 60) on a target with a heading (IHittable facing: raiders and
    bosses; a structure / dummy with none takes x1). Through PlayerShip.Outgoing (a stat row, 1 on every other
    class), credit unchanged.
  - **Veil** (F): 5 s Untargetable (the existing F3 path, Status.Untargetable), top x1.35 (`veil_speed`, additive:
    with the boost x1.85 = 481), the first volley fired out of it (or inside it: firing ends it) deals x3
    (`veil_break`); cooldown 18 s (`veil_cooldown`). Ids renamed stealth_* -> veil_*; Ab.Stealth -> Ab.Veil.
  - **Venom** (Q): 6 s coated (`venom_time`); each landed pellet adds a stack on its target (cap 10,
    `venom_cap`), each stack 1.25 a second (`venom_dps`) for 5 s after its last refresh (`venom_last`), ticked on
    the host every 0.5 s through Dealt (credit `venom`); never on a missile; cooldown 22 s (`venom_cooldown`):
    12.5 x 11 / 22 = 6.25 on the sheet.
  - **Shadow step** (E): the selected hostile within 900 u (`step_reach`): the Wraith blinks to 140 u
    (`step_behind`) behind it (its tail, or the far side from the Wraith for a target with no heading), nose on
    it, velocity kept in size; refused NO TARGET / OUT OF REACH / COOLING; cooldown 14 s (`step_cooldown`).
    Owner-side snap (TakesTargets payload gives the host the pick; the host confirms the spot within 30 u).
    A latched web drops by construction (a web holds only within 12 u of its post).
- **DL4 The Echo** (kits_v2 card, v1 numbers are ON the card):
  - **Echo repeater** (Space, turret path): 22 (`main_damage`) every 0.5 s (`main_interval`), 500 u, 620 u/s; each
    shell's echo, 11 (`echo_share` 0.5 of the shell), leaves the RECORDED muzzle along the recorded bearing 0.6 s
    later (`echo_delay`), straight (a new Shots row `echo`, appended, not Guided, a ghost tint) = 66 DPS.
  - **Reverb** (F): today's Ab.Echo reworked (the store, OnDealt, Detonate at Slot.At): 5 s (`reverb_time`), guns
    x1.2 (`reverb_rate`, a RateStat), 35% of the store (`reverb_share`) blasts in 220 u (`reverb_radius`) at the
    last hit, cooldown 18 s (`reverb_cooldown`). Ids echo_time / echo_radius / echo_cooldown renamed reverb_*; the
    old echo_share (x1 blast) is replaced by reverb_share 0.35; `echo_share` becomes the repeater's 0.5.
  - **Rewind** (Q): README ruling. A ring of 16 snapshots, one every 0.5 s (`rewind_every`), of position, heading,
    velocity and hull; the press takes the one closest to 8 s ago (`rewind_back`; the oldest with less history).
    The OWNER restores position / heading / velocity (flight is owner-side) and clamps the speed to its current top;
    the HOST restores the hull from its OWN ring (hull is host state; never raised above max, never revives a
    wreck), and drops any web on the Echo (v1 "clears a web"). The boost's time left is not rewound. Cooldown 30 s
    (`rewind_cooldown`). Refused while wrecked or Disabled. No warp exemption (v31: the host prices snaps on warp
    hulls only).
  - **EMP** (E): kits_v2 card. 300 u (`emp_range`), Light|Heavy hostile craft (escorts included), Status.Jammed
    4.0 s (`emp_jam`), a second pulse 0.6 s later (`emp_echo`) from the press point (Slot.At) refreshing it;
    bosses, structures, dummies, missiles immune (Jammed's Spares + a TargetFilter); no damage; cooldown 18 s
    (`emp_cooldown`). Jammed's OutGuards row exists (Statuses.cs:83: Gun 0, HoldsThrow, BlocksLatch, DropsLatch);
    this job adds what the card lists that is not yet read: no missile launch, the turret stops tracking, standoff
    heavies leave a non-pinned target for the edge (by construction once the web drops -- check it), a jam mark
    on each jammed NetId (F9 marks). Fx "emp" / Dealt.Emp are reused if they still exist, else a row appended.
- **DL5 The Dart** (kits_v2 card + v3 §3.6 + v31 §3.4):
  - **Pepperbox** (Space, Hold, Weapon; replaces Ab.Guns + Ab.FireMode + the light cannon on the Dart): two nose
    rails alternate (`pepper_interval` 1/6 s through Cadence), each launches along the NOSE (not a turret) at 520
    (`pepper_speed`) + ship velocity, turns up to 6 rad/s (`shell_turn`) toward the owner's LIVE AimPoint
    (`ShotDef.Command`: steer to Combat.PlayerById(TargetId).AimPoint; flies straight once within 24 u of it, and
    straight for good if the launcher is gone), 750 u (`pepper_range`), first hostile body, never a missile, no
    area. Damage 7.5 (`pepper_damage`) x clamp(top / 260, 1.0, 1.5 `pepper_cap`), top = the HOST's TopNow of its
    own copy at launch (`price_top` 260 on the sheet). Shots row `pepper` appended.
  - **Rod from God** (F): 3.0 s sprint (`sprint_time`): thrust x3 (`sprint_thrust` 3.0 as a thrust-only lift, a NEW
    Lift kind `Thrust` + AbilityDef.ThrustStat; the boost's +50% thrust adds: x3.5), top +100 flat (`sprint_add`,
    F1's SpeedAdd, first user), forced thrust (NEW `AbilityDef.Forces`: `if (Pinned || Forced) throttle = 1`, S
    dead, rudder free). Expire (host) fires the rod along the nose at 300 x nose + ship velocity (`rod_speed`), a
    Shots row `rod` appended, Stops 0, never a missile, 1400 u (`rod_range`); damage 180 (`rod_damage`) x clamp(top /
    260, 1, 2.0 `rod_cap`) priced at the fire. Recoil: owner-side, speed x0.30 (`rod_recoil`). Cooldown 12 s from
    the PRESS (`rod_cooldown`). Wrecked mid-sprint: no rod, slot clear.
  - **Ramjet** (Q): the F1 Ramp row (RampSpec exists, D18; ledger_kits J6 + the Mine/ApplyHostState wiring):
    `ramjet_time` 8, `ramjet_build` 0.10, `ramjet_cap` 0.50, `ramjet_bleed` 0.20, `ramjet_cooldown` 20 from the
    press; Condition: throttle full and speed along the keel within 5% of the CURRENT top. The HOST's pricing copy
    (v3 §3.6 Authority): the host steps its own RampSpec for a guest Dart from the 20 Hz velocity / heading reports,
    yaw clamped to the hull's turn rate, skipping a report in which a Slingshot lands; the host prices only from
    its copy. The owner's copy flies.
  - **Slingshot** (E): TakesPoint; the owner snaps heading AND the whole velocity (strafe part included) onto the
    cursor bearing, up to 180°, keeping its size; cooldown 6 s (`sling_cooldown`). Keeps the Ramjet (a snap, not a
    turn: the host's copy skips the report).
  - **Slipstream** (passive): x0.7 (`slip_guard`) damage taken at >= 325 u/s (`slip_speed`) of actual speed, read
    on the host from the reported speed clamped to hypot(top, strafe_speed) x 1.1 of its own lifts; a stat row
    through PlayerShip.Incoming (1 / 0 on every other class: by the stat, never the class).
  - **Gear**: light_roll_thrusters becomes "Sprint Thrusters" (Needs sprint_time); the Items.cs @output / @area /
    @duration lists drop roll_* / stealth_* / echo_* ids and gain the new rows' ids (DL6).
- **DL6 Replaced in the same edits (invariant C):** Ab.Roll + BarrelRoll + its rows (roll_time, boost_time,
  boost_speed, boost_rof, roll_cooldown) and Status.Evading if nothing else reads it; Ab.Echo -> Reverb; Ab.Stealth
  -> Veil; Dps.Echo -> the Reverb's row; LightCannon kit part once no class carries it (the Warden's flak moved in
  6c; check who still does). Items.cs:102-104 lists move with the ids. Stats.cs:385 (the echo's Dps rate).
- **DL7 Wire:** new Shots rows (`pepper`, `rod`, `echo`, `pellet`) APPEND at the end of Shots.All (6a/6b may append
  too: the merge renumbers only what collides). New Status / NetIds / Fx rows likewise at the end.

## Jobs (foundations before their users; each: PRE, edit + checks, typecheck + quick, POST, commit)

- **kits6d-J1 Dart row + Pepperbox** (DL1, DL2 Dart part, DL5 Pepperbox, DL6 roll): ShotDef.Command + row
  `pepper`, the nose-bore launch with inherited velocity, the host speed price, Ab.Pepperbox; Ab.Roll and its rows
  deleted with every caller. Checks LaneA6dPepperChecks (per missile at top {260, 325, 390, 450} = {7.5, 9.375,
  11.25, 11.25}; landed 45 ± 3% over 6 s at VaryNear 300-700 / VaryAngle within 90°; 749 lands / 751 fizzles;
  cursor switched A -> B mid-flight lands on B; launcher wrecked: straight, no orphans; a light in front of a boss
  takes it, a seeker is flown through; 12 ± 1 launches in 2 s; launch = nose x 520 + ship velocity after a
  sideways slide), the hull 200 check, the walls' order rewrite. Rung 5: LaneA6dPepperHost/GuestChecks (a guest's
  copies within 30 u of the host's). Frame LaneA6dPepperFrames (tracer darts, two rails).
- **kits6d-J2 Sprint + Rod** (DL5): Lift.Thrust + ThrustStat, AbilityDef.Forces, SpeedAdd's first row, row `rod`,
  the recoil. Checks LaneA6dRodChecks (thrust 3.0x from rest over 0.25 s; top {260 -> 360}; S held + W off still
  rises, A/D yaw; leaves at 3.0 s ± 1 frame at VaryAngle; damage {249.2 at 360, 360 cap}; pierces {1, 3} dummies
  once each, a missile untouched; 30% ± 2% after; webbed: down the frozen heading; wrecked mid-sprint: no rod;
  cooldown 12 s from the press; boosted sprint: top 490, rod 339.2, thrust x3.5 -- v31 §3.4). Rung 5 (a guest's
  sprint starts within one RTT; the host's rod price within 1%). Frame (the sprint plume and the rod line).
- **kits6d-J3 Ramjet** (DL5): the Ramp row, its Condition, the host's own copy from reports. Checks
  LaneA6dRamjetChecks ({286, 338, 390, 390} ± 3 at {1, 3, 5, 7} s; 1 s full rudder 390 -> 364; webbed no build;
  0 by 9.0 s; cooldown 20; Pepperbox 11.25 at +50%; rod 339.2 at the end of a lit sprint; boosted top x2.0 = 520,
  the build pausing while it climbs; strafe never bleeds it). Rung 5 (host's rod price for a guest within 1%).
- **kits6d-J4 Slingshot + Slipstream** (DL5). Checks LaneA6dSlingChecks (heading and velocity onto the cursor at
  VaryAngle up to 180°, size kept ± 1%, strafe part included; cooldown 6; the Ramjet kept on owner and host copy;
  re-aims the rod mid-sprint), LaneA6dSlipChecks ({324, 326} u/s; the clamp at hypot(top, strafe) x 1.1 never
  fires on a boosted slide; x0.7 from 3 sources).
- **kits6d-J5 Echo row + repeater + Reverb** (DL1, DL4). Row `echo` appended; the recorded muzzle; Reverb from
  Ab.Echo. Checks LaneA6dRepeaterChecks (22 + 11 at 0.6 s; 66 DPS ± 3% on a still dummy; an echo at a jinking
  raider does not curve; from the recorded muzzle after the Echo moved), LaneA6dReverbChecks (x1.2 rate; 35% of
  the store in 220 u, 219 / 221; cooldown 18).
- **kits6d-J6 Rewind** (DL4). Checks LaneA6dRewindChecks (at Vary 9-20 s of flight the hull, position, heading,
  velocity equal the snapshot nearest 8 s ago; with 3 s of history the oldest; hull never above max; a web drops;
  cooldown 30; refused wrecked; speed clamped to current top; boost time not rewound). Rung 5 (a guest's rewind:
  the host's hull and position agree within 20 u).
- **kits6d-J7 EMP** (DL4). Checks LaneA6dEmpChecks (kits_v2 Proves: {299, 301} u x 6 kinds; 0 strikes and 0
  launches over 4.0 s, still moving; Pinned lapses by 0.3 s; second pulse from the press point; immune boss /
  pylon / base / seeker; 4.0 / 4.6 s; hull unchanged; cooldown 18). Rung 5 (a guest sees the web drop and the
  marks). Frame LaneA6dEmpFrames (the ring, the jam marks).
- **kits6d-J8 Wraith row + scattergun + Backstab** (DL1, DL3). Row `pellet`. Checks LaneA6dScatterChecks (65.3 DPS
  point blank; fewer pellets at range; 7 per volley), LaneA6dBackstabChecks (x1.5 from behind at 3 VaryAngle in the
  arc, x1 at 61°+ and from the front, x1 on a dummy; strafe holds the rear arc -- v31 §3.4).
- **kits6d-J9 Veil** (DL3). Checks LaneA6dVeilChecks (not chosen for 5 s, still hit; top x1.35, boosted x1.85 =
  481; firing ends it; the first volley x3; cooldown 18). Frame (the veiled hull).
- **kits6d-J10 Venom** (DL3): a dose table (a new file named for the mechanism, e.g. `Doses.cs`: a per-target
  stacking damage-over-time row, host-only, credit through Dealt). Checks LaneA6dVenomChecks (stacks to 10;
  12.5 a second at 10; ends 5 s after the last refresh; missiles never; credit `venom`). Frame / rung 5 as
  its mark needs.
- **kits6d-J11 Shadow step** (DL3). Checks LaneA6dStepChecks (140 u behind at 3 VaryAngle / VaryNear within 900;
  refused NO TARGET / OUT OF REACH at 901; nose on it; a web drops; cooldown 14). Rung 5 (a guest's step: host
  position within 30 u).
- **kits6d-J12 Record**: CHANGES.md Unreleased + Handoff, DESIGN.md slice-6d section, final POST (what the test
  phase owes: checks, rungs, frames).

## JOB 0 · POST
- Worktree created from version-l 3450da4. Spec read; decisions DL1-DL7 and the job list above. No code touched.
  Next: kits6d-J1.

## Handover 1: the first agent stops after JOB 0 (context spent reading the spec), at a job boundary
Pointers (3450da4 tree): Ships.cs light rows 522-622 (Dart 523, Echo 553, Wraith 586; LightCannon kit part 154);
Abilities.cs AbilityDef fields 34-150 (RateStat/SpeedStat/StrafeStat 97, SpeedAdd 120, Ramp 125, Dash 128, Stance
131, Charges 140, Lays 142, Pops 144, Cooldown 147, TakesPoint 55, TakesTargets 60, OnDealt 85), RampSpec 159-179,
Ab.Lunge 463 (a Dash row), Ab.Roll 513, Ab.Echo 526, Ab.Stealth 539, Timed() ~550, For() (appends the drive row).
PlayerShip.cs: Lifts 130-200 (Ramp lift at 158, SpeedAdds 164, TopSpeed 178, LiftShares 196), Cadence 207,
NoteDealt 292, the main-gun fire 311 / 1285, DoAbility 618, BarrelRoll 847, StartEcho 859, Detonate 868, GoDark
885, TopNow 935, TickAbilities 1209 (the Ramp step `&& Mine` 1230), Lay 1381, Pop 1391, Stance 1441, StartDash
1490, LocalFlight 1550 (AimPoint 1587), Steer 1629 (TopSpeed 1652), SendHostState 1754, ApplyHostState 1774 (keeps
a Ramp row's Own when Mine, 1789). Shots.cs ShotDef 24-60, Shots.All 67-98 (append after `flak` = 8).
Statuses.cs Jammed 28 / OutGuards 83 / HostOnly 86. Items.cs 100-104 (the category lists). Stats.cs 385 (Dps echo).
Old-kit callers (counts, scripts + harness): LightDart SmokeTest 26 / Shots.cs.txt 4; LightEcho 19 / 1; LightWraith
5 / 1; roll_*|BarrelRoll|Evading PlayerShip 5, Statuses 2, SmokeTest 11; echo_*|StartEcho|Detonate|Dps.Echo
PlayerShip 9, Stats 2, Dealt 1, Fx 1, Items 3, SmokeTest 42; stealth_*|GoDark PlayerShip 4, SmokeTest 4.

## kits6d-J1 · PRE
- tier opus; intent: Dart row (hull 200, learn order weapon rows then F/Q/E as they land), Pepperbox (BoreSpec + ShotDef.Command + row pepper), Ab.Roll and its rows deleted with every caller (Status.Evading KEPT: harness wire-bit checks read it; DL8).
- HEAD 2ea09b2b4effc26028c50cce82a70673dad0d029
- scripts/Abilities.cs 88abfd0541f772b420ad08ad43d135fdf171d775
- scripts/Ships.cs dd6c77c19088e55fc3e815b34640d824b48d30fd
- scripts/Shots.cs c44dd017dc8b6367e2cf09b864a1cb8363728cdf
- scripts/PlayerShip.cs 6d41d138618c4c1423779be23acd25e651ec6e5e
- scripts/Items.cs c25c8da8e16f9297cbe1347682b81727e53dc633
- scripts/Stats.cs 64bf427d6f48ed692346d261a56b32ad5a29f582
- tools/smoketest/SmokeTest.cs.txt f93a02fb2d9489c93e60da27f4f240192bff56ce
- tools/screens/Shots.cs.txt 5bb73590976091ff0fee983dcf2a5b799baef7dc
- new: scripts/Bores.cs

## kits6d-J1 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: scripts/Bores.cs (BoreSpec + Bores.Price/Muzzle/Launch/Tick; the Rod's Expire will reuse Launch), ShotDef.Command +
  row `pepper` (Shots.Pepper = 9, appended), AbilityDef.Bore + PlayerShip.Bored (host), Ab.Pepperbox (Weapon, Hold, Space);
  the Dart row: Fit.None, hull 200, no turret, EVERY Dart row (pepper_*, price_top, sprint_*/rod_*, ramjet_*, sling_cooldown,
  slip_*) so J2-J4 add behaviour only; Dps.Pepper; kit light_pepperbox (new) + light_roll_thrusters KEPT AS AN ID (save
  compat) renamed "Sprint Thrusters", Needs sprint_time; Items role lists gain pepper_*/rod_damage/sprint_time/ramjet_time.
  Deleted: Ab.Roll, PlayerShip.BarrelRoll, roll_*/boost_time/boost_speed/boost_rof rows, the harness's roll block + witness.
- DL8: Status.Evading KEPT (its wire bit 16 and OutGuards row are asserted by the status-wire checks; nothing applies it
  now). AbilityDef.While KEPT (no row sets it after the roll; 6a/6b may, and dropping it would collide on merge).
- checks: LaneA6dPepperRowChecks (rows, hull 200, price literals + 3 varied tops, launch vector x3), LaneA6dPepperChecks
  (45 DPS +-3% x3 with 12+-1 launches, launch after a slide x3, reach 690-740 vs 765-810, cursor A->B x3, wrecked
  launcher straight + no orphans x3, first body / seeker flown through x3, price on ship 7.5 / 11.25 boosted x3); rung 5
  LaneA6dPepperHostWatch/HostChecks + LaneA6dPepperGuestChecks; frame 76b_dart_pepperbox (LaneA6dPepperFrames).
  Rewritten: walls' weaponRows (+pepperbox), sweep witness roll -> pepperbox, Burst Feed on a Dart reads pepper_interval.
- next: kits6d-J2 (Sprint + Rod).

## Handover 2: the second agent stops after kits6d-J1 (context), at a job boundary
- Harness conventions used: `Me`, `SetClass`, `HeldI(at, Enemies.Gunship)` (a held 1e9-hull raider), `Demo = true` then set
  AimPoint / Trigger by hand, `LaneA6dOneDart(dt)` (one frame of trigger, returns the new dart), `DealtBy["<shot row id>"]`.
  Solo calls go after `LaneA6cCurtainChecks(yonder)` (~13384); rung 5: host watch beside `reloadWatch` (~17003), host check
  just before the host's RealTime on the guest's FREIGHTER (~17025, generous timeout: the guest runs anchor..curtain first),
  guest check after `LaneA6cCurtainGuestChecks()`. Frames: after `LaneA6cPrismFrames()` in Shots.cs.txt (~437).
- J2 pointers: PlayerShip LocalFlight `if (Pinned) { throttle = 1f; ... }` (~1566, add `|| Forced` there, rudder free),
  Lifts()/Lift enum (~146, add Lift.Thrust read in Steer where SpeedMult lifts thrust), SpeedAdds (F1, first user = sprint_add),
  Bores.Launch(s, row, rail, damage, weapon) for the rod (a BoreSpec with Shot = new `rod` row, Turn null, PriceCap rod_cap).

## kits6d-J2 · PRE
- tier opus; intent: Rod from God (DL5): Lift.Thrust + AbilityDef.ThrustStat, AbilityDef.Forces (throttle forced open, S dead,
  rudder and slide free), SpeedAdd's first row (sprint_add), BoreSpec.Recoil + AbilityDef.Parting (a round of its own fired
  down the nose as the run ends, priced at the top the run gave: PlayerShip.Part), Shots row `rod` appended (Stops 0), Dps.Rod.
- HEAD e1c136d02446bc6f0c0ba79d242c8bdde5d8a5f6
- scripts/Abilities.cs 2197a875630aac2a29ca26137b3e04f8794cbc17
- scripts/PlayerShip.cs 038c1299100629dd17eb12fc21a0591962f4f67b
- scripts/Shots.cs 827124d407d8f3635ac72a8d880d5de86a4e64eb
- scripts/Bores.cs c12b48743a0f6e2cce3bb7c512f93082055b2422
- scripts/Ships.cs 9994e51721eebdc8ad5fd495568b6c3ce9f05d4b
- scripts/Stats.cs 85fb7d8c7447aa76ec7ebaa5e21b8c3726a19620
- tools/smoketest/SmokeTest.cs.txt e99f684e3448458b5db199dad8969e6a67f7ba80
- tools/screens/Shots.cs.txt d644c09deb2330ff2b51ddcbf0fb8a81baf2c931

## kits6d-J2 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: AbilityDef.ThrustStat (Lift.Thrust; PlayerShip.ThrustMult = Scale(speed shares + thrust shares), Steer's push),
  AbilityDef.Forces (PlayerShip.Forced; LocalFlight forces throttle 1, rudder and slide free), AbilityDef.Parting (BoreSpec +
  Recoil): PlayerShip.Run (a timed press), Part (host, Expire: one round priced at TopOf(def) = TopNow with the row's own
  SpeedAdd, since Left is 0 at Expire; a wreck fires none), Recoil (Elapsed, owner: the kick lands on the NEXT LocalFlight
  frame so the rod launched this frame keeps the run's speed). Ab.Rod (F, ability 1), Shots row `rod` = 10 (Stops 0,
  ShotLook.Rod appended), Dps.Rod (rod_damage / rod_cooldown).
- checks: LaneA6dRodRowChecks (rows, price literals 249.2/339.2/360/360 + 3 varied, the lifts add x3.5 / 490), LaneA6dRodChecks
  (sprint from rest x3 with S held, rudder on run 1: water curve at 0.25 s, peak 360, rod at 3.0 s, 249.2, 30% kept, cool 9,
  second press refused; through {1,3,3} once each + seeker flown through + one past 1400 u; boosted x3.5/490/339.2 x3; wrecked
  mid-sprint no rod x3); sweep witness ["rod"]; rung 5 LaneA6dRodHostWatch/HostChecks (host price within 1%) +
  LaneA6dRodGuestChecks (forced within 0.5 s, past 300 u/s, rod seen); frame 76c_dart_sprint_rod (LaneA6dRodFrames).
- next: kits6d-J3 (Ramjet).

## kits6d-J3 · PRE
- tier opus; intent: Ramjet (DL5): Ab.Ramjet (Q, ability 2) on the Ramp row; Condition PlayerShip.FullAhead (owner: throttle full and keel speed within 5% of the current top; host copy: the reported keel speed alone, DL9); the host's own copy stepped from each guest report (yaw from the reported headings clamped to turn_rate, a snap's report skipped).
- HEAD 46f51002766194bb2cfa9b24c099de4e2d0ac431
- scripts/Abilities.cs 94c9c6c146559d41ef3a376b0b22300ad17eed7f
- scripts/PlayerShip.cs 09e1c5bec79e29955f99385eec0b53a467a97da7
- scripts/Ships.cs 3a847cda2608af38abbf25200f268045060d2ba5
- tools/smoketest/SmokeTest.cs.txt 10002adf2623658315db42784b6d3312cd3a0db2

## kits6d-J3 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: Ab.Ramjet (Q, ability 2; Ramp row ramjet_build/cap/bleed, Run from the press), PlayerShip.FullAhead(share) (owner:
  throttle 1 and keel speed >= 95% of TopNow; host copy: the reported keel speed alone -- **DL9**, the report carries no
  throttle, a wire field was not worth it), PlayerShip.RampsFromReport (host, from ApplyState: yaw from the two reported
  headings clamped to turn_rate, dt capped 0.5 s, SkipYaw for a snap -- J4's Slingshot sets it). Stale "no row uses it"
  comments on SpeedAdd / Ramp and TickAbilities' authority note rewritten.
- checks: LaneA6dRamjetRowChecks, LaneA6dRamjetChecks (build {286,338,390,390} +-3 x3 with the dart 11.25, 0 by 9.0, cool 20,
  refused; full turn 1 s from full yaw -26 +-3 and the slide 1 s nothing x3; webbed no build x3; boosted 520 + rod 339.2 at
  the end of a lit sprint x3; the boost pauses the build x3); sweep witness ["ramjet"]; rung 5 LaneA6dRamjetHostWatch/
  HostChecks (host copy +50%, dart 11.25 within 1%) + LaneA6dRamjetGuestChecks.
- next: kits6d-J4 (Slingshot + Slipstream).

## kits6d-J4 · PRE
- tier opus; intent: Slingshot (DL5: AbilityDef.AtOnce, the owner's snap at the press; host Press: cooldown + SkipYaw) + Slipstream (a stat row read in Guarded: x slip_guard at >= slip_speed of actual speed, the host reading a guest's reported speed clamped to hypot(top, strafe) x 1.1).
- HEAD 2fe5f986a8e14a99ca6a973c13150f69d91bbc4e
- scripts/Abilities.cs 7555e88f4a16f209989cf7a70b7a5ac7cf1b1f0b
- scripts/PlayerShip.cs 6572c4771dc33d21a8f05dc9f4074179f0e42735
- scripts/Ships.cs ed57a6db9a9f2a4b1dbd0877035461f2f19a190d
- tools/smoketest/SmokeTest.cs.txt 8226d756ab2f56ca006660f2a6f39d525e1c2e0d

## kits6d-J4 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: AbilityDef.AtOnce (the owner's own action at the press, UseAbility, Mine && Alive, before the ask), Ab.Slingshot (E,
  ability 3, TakesPoint; AtOnce = PlayerShip.Snap: rotation set + whole velocity onto the bearing, size kept, _yawRate 0;
  Press = Slung: cooldown, SkipYaw on the host's copy of a guest). Slipstream: PlayerShip.SlipShare in Guarded (stat
  slip_speed > 0; Mine reads Velocity, the host's copy SlipSpeed(_netVel, TopNow, StrafeNow) = min(|v|, hypot x 1.1)).
- DL10: the host's SkipYaw lands on the next report after the press RPC; a report that races ahead of the RPC is clamped
  to the hull's turn rate over its age (at most 0.2 x 0.05 s = 1% of the total). Not provable in solo (no report age on the
  owner); the rung-5 list in kits_v31 §8 does not name the Slingshot, so no rung-5 pair was written for it.
- checks: LaneA6dSlingChecks (row; snap forward / sliding / 170-179° with size +-1%, cool 6, refused; ramjet kept x3; rod
  re-aimed down the new nose x3), LaneA6dSlipChecks (ceiling literals + 3 boosted slides unclamped; 3 sources x {under,
  over} 325 = x1 / x0.7; an Echo x1); sweep witness ["slingshot"].
- next: kits6d-J5 (Echo row + repeater + Reverb).

## Handover 3: the third agent stops after kits6d-J4 (context), at a job boundary
- The Dart is complete (J1-J4): Pepperbox, Rod, Ramjet, Slingshot, Slipstream. Next: kits6d-J5 (the Echo), then J6-J12 as listed.
- Harness: insert snippets with a small python helper (read SmokeTest.cs.txt as bytes, replace one exact anchor, write LF). Solo
  calls sit after `await LaneA6dSlipChecks(yonder);` (before `await SetClass(ShipClass.HeavyWarrior);`); new check methods go
  before the `// ── SLICE 6d, RUNG 5: A GUEST'S PEPPERBOX.` comment; rung-5 host watches beside `ramjetWatch`, host checks after
  `LaneA6dRamjetHostChecks`, guest checks after `LaneA6dRamjetGuestChecks()`; sweep witnesses after `["slingshot"]`.
  Frames after `await LaneA6dRodFrames();` in Shots.cs.txt. The harness pilot's Peak is Unlocks.Top (no wall in the way).
- New generic doors from J2-J4 the Echo/Wraith may reuse: PlayerShip.Run(id, time, cool) (a timed press), AbilityDef.AtOnce
  (owner-side at the press: Shadow step's snap), SkipYaw (a host-side snap flag), Guarded's stat rows (SlipShare pattern for
  Backstab's Outgoing twin), AbilityDef.Parting / BoreSpec for a round fired as a run ends.
- J5 pointers: Echo row Ships.cs (grep `ShipClass.LightEcho, Name`), Ab.Echo + StartEcho/Detonate in PlayerShip (grep
  `public void StartEcho`), Dps.Echo in Stats.cs, Items.cs @-lists hold echo_* ids, SmokeTest has ~42 echo_*/StartEcho/Detonate
  callers (grep `"echo"` and `echo_`), the sweep witness ["echo"]. LightCannon kit part still used by Echo and Wraith.

## kits6d-J5 · PRE
- tier opus; intent: the Echo (DL1 hull 180, DL2 Reverb F first, DL4): repeater 22 / 0.5 s / 500 u + its echo round (TurretSpec.RepeatShare/RepeatDelay: the turret records muzzle + bearing, fires Shots row `echo` = 11 at 0.5 x 0.6 s later, straight, ShotLook.Ghost), Ab.Echo -> Ab.Reverb (reverb_time 5, reverb_rate 1.2, reverb_share 0.35, reverb_radius 220, reverb_cooldown 18), Dealt.Echo -> Dealt.Reverb, Fx.Echo -> Fx.Reverb, Dps.Echo -> Dps.Repeat + Dps.Reverb; harness callers rewritten.
- HEAD 5d4c59cb85f04350158c88c8bfd15e21a12c5298
- scripts/Shots.cs fb71bfa693a68a67c20f6ceb1a952ae39cae6259
- scripts/Turrets.cs 471d84e4463dbde1f3fd779b4001e3a59af8de63
- scripts/PlayerShip.cs 2edf5a52d65a34bc947363a0dabe0a1cb983464e
- scripts/Abilities.cs afb09b82b5cbc69f98b54e2a4bf2df03540b97c5
- scripts/Ships.cs 1b83010da6ae5fda94aec5bb6129aa2ffa843587
- scripts/Stats.cs af113b280a136df201deaf68e3c7bd92760c762a
- scripts/Items.cs ea0c98576437a0659370a2eee14841655bd60af0
- scripts/Dealt.cs 055f7437d37ece17f634b81388c61e424ca01b2a
- scripts/Fx.cs a25d422d218d95daacfddc128f68432cf86c0a02
- tools/smoketest/SmokeTest.cs.txt e9130f06fb1c97f93fd59531cc513d2371aa9e18
- tools/screens/Shots.cs.txt dfaa3b325a48f0601c53ddcd1d2603b2ff93ebd3

## kits6d-J5 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: Echo row (hull 180, repeater 22 / 0.5 s / 500 u / 620, Damage level 1.1, learn order guns, firemode, reverb);
  TurretSpec.RepeatShare/RepeatDelay + Turret.Repeat (the turret records muzzle, bearing, speed, reach at Shoot, fires Shots
  row `echo` = 11 (ShotLook.Ghost appended) on its own clock; host only); rows echo_share 0.5, echo_delay 0.6;
  Ab.Echo -> Ab.Reverb (id "reverb", RateStat reverb_rate 1.2, reverb_time 5 / share 0.35 / radius 220 / cooldown 18);
  StartEcho -> StartReverb; Dealt.Echo -> Dealt.Reverb ("reverb"); Fx.Echo -> Fx.Reverb (same index 5); Dps.Echo -> Dps.Repeat +
  Dps.Reverb; Items: role lists moved to reverb_*, Repeats {Dealt.Reverb}, PrimaryShots gains "echo" (A10); kit Echo Core ->
  "Reverb Core" (id kept, Needs reverb_time).
- checks: LaneA6dRepeaterRowChecks, LaneA6dRepeaterChecks (396 +-3% in 6.25 s x3; echo from the recorded muzzle/bearing at
  0.6 s, 11 of 22, straight past a jinking raider after the Echo slid and turned x3), LaneA6dReverbChecks (row; 6 vs 5 rounds in
  2.3 s x3; 35% at mark + 219 u, 0 at 221 u, cool 18, refused x3; stores rounds + echoes, blast 35% x3); frames 76d_echo_repeater,
  76e_echo_reverb (LaneA6dEchoFrames). Rewritten: every Sl("echo")/UseAbility("echo")/echo_* harness caller -> reverb; Echo
  SustainedDps literal 66 + 7.7; chip hull literal 90 -> 180; the Echo blast 90 -> 31.5 (35%); sweep witness ["reverb"].
- next: kits6d-J6 (Rewind).

## kits6d-J6 · PRE
- tier opus; intent: Rewind (DL4): scripts/Trails.cs (Trail ring of Marks, Note/Back), PlayerShip records a mark every rewind_every (by stat), Ab.Rewind (Q, ability 2): AtOnce = owner flight back (speed held to TopNow), Press = host cooldown + hull from its own mark (<= max, never a wreck) + every web let go (LetGoWebs, shared with Whirl); rows rewind_back 8 / rewind_every 0.5 / rewind_cooldown 30.
- HEAD 21a39a6b73143311d7b9ca54df44dc27becce19d
- scripts/PlayerShip.cs 467c8d7a88318b39645d1c3530eb8fccb7a8e7f8
- scripts/Abilities.cs e864e2e94ec98dde65beb16073b1d3d1a8c5c675
- scripts/Ships.cs 63e82fcc231a879405159877c0884c291e88988f
- tools/smoketest/SmokeTest.cs.txt 4d6e7dda20211083009c73663baf806bc2425f4a
- tools/screens/Shots.cs.txt fe2eeb57c1e3316c5013eaec6de94d0cff3efec1
- new: scripts/Trails.cs

## kits6d-J6 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: scripts/Trails.cs (Mark + Trail: Note keeps a mark every `every` s, Back = the mark nearest now - back, the oldest with
  less; Size = ceil(back/every) + 4); PlayerShip records a mark each frame-boundary past rewind_every while Alive (by stat; every
  peer, each side's own fields matter: the owner's flight, the host's hull); Past (the checks' door); Rewind (owner, AtOnce:
  position, heading, velocity, speed held to TopNow) and Rewound (host Press: cooldown 30, hull = min(max, the host's own mark),
  LetGoWebs -- now shared with Whirl). Ab.Rewind (Q, ability 2); rows rewind_back 8 / rewind_every 0.5 / rewind_cooldown 30.
  Refused wrecked (DoAbility + AtOnce's Alive gate) and Disabled (PressHeld), both generic.
- checks: LaneA6dRewindRowChecks (rows, learn order, the trail pure x3 + short history x3), LaneA6dRewindChecks (effect x3 with
  blows: the frame 8 +- 0.25 s ago in position/heading/velocity/hull, cool 30; the oldest with 2-4 s x3; boosted mark clamped
  to 260 + the boost's clocks untouched x3; a web freed and staying free x3; refused cooling/disabled/wrecked); rung 5
  LaneA6dRewindHostWatch/HostChecks (host hull back to its own log 8 s before, copy within 20 u) + LaneA6dRewindGuestChecks;
  sweep witness ["rewind"].
- next: kits6d-J7 (EMP).

## kits6d-J7 · PRE
- tier opus; intent: EMP (DL4): Ab.Emp (E, ability 3), PlayerShip.Pulse (host: Jammed 4.0 s on Targeting.Jammable craft within 300 u, a second pulse 0.6 s later from the press point via the row's Expire; no damage; 18 s), OutGuard.HoldsAim (a jammed raider's turret stops tracking), Raider.FlagJam = 16 (the jam mark on the wire, drawn on every peer; DL11).
- HEAD 1c009c06e1720eb9dae5b6ca8dc88c8d7f830523
- scripts/PlayerShip.cs 6239712d0b56af2513363bea621622a7de275d07
- scripts/Abilities.cs b1a56f9254ae14ad77e9a6c75db82e78575f3c76
- scripts/Ships.cs f44ce6c454d92f04904c6ef3ec2d789eedb7c225
- scripts/Statuses.cs c3330825b54d86acfda110584b74c27b4c18f124
- scripts/Raider.cs 7be25c1f6814bb06c0c0b5cc99fbd1536a32cfbf
- scripts/Targeting.cs 69a37580fc092a9879e713c690656f2a4639da3b
- tools/smoketest/SmokeTest.cs.txt 81fa30d8bb5b5e62876b4de4dbd63bb825774b71
- tools/screens/Shots.cs.txt fe2eeb57c1e3316c5013eaec6de94d0cff3efec1

## kits6d-J7 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: Ab.Emp (E, ability 3; Press StartEmp: cool 18, first pulse, Slot.At = the press point, Left = emp_echo; Expire =
  the second Pulse from Slot.At); PlayerShip.Pulse (Jammed emp_jam on Targeting.Jammable within emp_range; no damage;
  Fx.Emp, reused); Targeting.Jammable (Light|Heavy, never Boss/Structure/Dummy/Missile/Hulled); OutGuard.HoldsAim +
  StatusSet.HoldsAim (Jammed: Raider's turret stops tracking); Raider.FlagJam = 16 + JamMarked + a drawn jam mark
  (**DL11**: the wire flag is the next free bit after FlagLock 8 and below the squad bits; another lane taking 16 collides
  at merge -- renumber there). Rows emp_range 300 / emp_jam 4.0 / emp_echo 0.6 / emp_cooldown 18. Echo Blurb/Hint name all 3.
- checks: LaneA6dEmpRowChecks, LaneA6dEmpChecks (6 kinds x 299/301 x3: jammed/free, 3.80 left at 0.8 s, jammed 4.4 / free
  4.8, hulls untouched, cool 18, refused; the second pulse from the press point x3; 3 latched webs + a gunship x3: freed
  <= 0.3 s, 0 struck and no seeker over 3.5 s, the turret's own rotation held); rung 5 LaneA6dEmpHostWatch/HostChecks +
  LaneA6dEmpGuestChecks (the jam mark on the guest within 0.5 s, gone by 5.2 s); frame 76f_echo_emp (LaneA6dEmpFrames);
  sweep witness ["emp"].
- next: kits6d-J8 (Wraith row + scattergun + Backstab).

## Handover 4: the fourth agent stops after kits6d-J7 (context), at a job boundary
- The Echo is complete (J5-J7). Next: kits6d-J8 (the Wraith), then J9-J12 as listed.
- Anchors as in Handover 3, now after the Echo's: solo calls after `await LaneA6dEmpChecks(yonder);`; host watches after
  `empWatch`, host checks after `LaneA6dEmpHostChecks`, guest checks after `LaneA6dEmpGuestChecks();`; sweep witnesses after
  `["emp"]`; frames after `await LaneA6dEmpFrames();` (methods before `LaneA6cBladeFrames`).
- Doors from J5-J7 the Wraith may reuse: TurretSpec fields read by Turret.Shoot (a pellet spread is another field there),
  Items.PrimaryShots (add "pellet"), Trail/Mark, LetGoWebs (Shadow step "a web drops"), Targeting filters, OutGuard rows.
- Wraith today (Ships.cs `ShipClass.LightWraith, Name`): hull 90, LightCannon, Ab.Guns + Ab.FireMode + Ab.Stealth, rows
  stealth_time/speed/rof/cooldown; PlayerShip.GoDark; Items @duration holds stealth_time; SmokeTest has ~4 stealth callers and
  a `── WRAITH: five seconds nothing hostile can pick it ──` block after the Echo's in the ability tour.

## kits6d-J8 · PRE
- tier opus; intent: the Wraith row (DL1 hull 220) + Ambush scattergun (DL3: 7 pellets x 7 every 0.75 s, ±10°, 320 u, 620 u/s; TurretSpec.Pellets/Fan fanned by Turret.Shoot; Shots row pellet appended; MainDpsPerBarrel x scatter_pellets) + Backstab (x1.5 inside ±60° of the tail, IHittable.Facing: Raider/Boss; PlayerShip.Outgoing reads backstab_mult / backstab_arc, 0 on every other sheet); own weapon part light_scattergun; LightCannon becomes the Echo's alone (Needs echo_share: the J5 rename left it naming echo_time, which no sheet has).
- HEAD c43f2feffbca96afba32903b0b6d5130f69660b6
- scripts/Shots.cs b14641663b185157eff4dd45b69ffce782d3c953
- scripts/Turrets.cs a066ab4c1af53c550faa323862d128d718f6bfa4
- scripts/PlayerShip.cs 45b1a4dcbc23be0c34e5406e5bfeab8267db6086
- scripts/Ships.cs cef6c8a91a2238529eb6e806abe9465e9e2835e4
- scripts/Stats.cs d548d0291381e5cbec49b186b6021a6267233f77
- scripts/Items.cs 98c235fb2bcf1661e945d94a9eb58aecb265fd48
- scripts/ShipClasses.cs 6b0afaa3460039d471cd903855cc383326d2b54e
- scripts/Raider.cs 57c93802c6e3d46a8e144ce0bec813b8454deed4
- scripts/Boss.cs 75d6ea1da2f119d47f9e6a56155e127964a3c2ad
- tools/smoketest/SmokeTest.cs.txt 4049ba0752e5ba0422786a9cb66bd965270ed9f5
- tools/screens/Shots.cs.txt e43f7d32a17821733cedb479d78bdbbeece7823a

## kits6d-J8 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: Wraith row hull 220, Shot = Shots.Pellet (row 12 `pellet`, appended), 7 x 7 every 0.75 s to 320 u (Damage level 0.35);
  TurretSpec.Pellets/Fan (Turret.Shoot fans n rounds evenly -Fan..+Fan; 0/1 one round); rows scatter_pellets 7 / scatter_spread 10;
  ShipStats.MainDpsPerBarrel x max(1, scatter_pellets) = 65.33; Items.PrimaryShots + "pellet"; Backstab: IHittable.Facing (Raider,
  Boss), PlayerShip.Backstab (rows backstab_mult 1.5 / backstab_arc 60, 0 elsewhere) in Outgoing. Kit: `light_scattergun` (the
  Wraith's); the shared LightCannon static is gone -- `light_main_gun` is the Echo's alone, renamed "Mk I Echo Repeater", Needs
  echo_share (**DL12**: J5's rename left it Needing echo_time, which no sheet has, so the Echo could not wear its own gun).
- checks: LaneA6dScatterRowChecks, LaneA6dScatterChecks (7 pellets fanned -10..+10 x3; 392 in 6.375 s point blank x3; 3-6 of 7
  at 250-300 u and 0 at 360-420 x3), LaneA6dBackstabChecks (inside/just outside/front x3; dummy x1 from 3 sides; an Echo x1 by
  the stat x3; strafe across the tail x1.5 every volley x3); frame 78b_wraith_scatter (LaneA6dWraithFrames). Rewritten: the
  kit-carry check (21 parts, light_main_gun Echo only, pepperbox Dart, scattergun Wraith).
- next: kits6d-J9 (Veil).

## kits6d-J9 · PRE
- tier opus; intent: Veil (DL3): Ab.Stealth -> Ab.Veil (id veil, F): 5 s Untargetable (AbilityDef.Wears), top x1.35 (SpeedStat veil_speed, additive with the boost: x1.85 = 481), the next volley primed x3 (AbilityDef.Primes veil_break, spent by FireControl on the first volley veiled or after), firing ends the run (AbilityDef.FireEnds; host); cooldown 18. Rows stealth_* deleted; Items @duration -> veil_time; kit part id light_stealth_veil kept (a save id), named Veil Emitter, Needs veil_time. **DL13** the primed volley waits for the first volley after the press, however long (the card: 'the first volley fired out of it').
- HEAD 80e13863c6d76e89262e74dc0840cfac5496b6c4
- scripts/Abilities.cs b07b3d0ab60413a789154b93910dceb4fc8dc40f
- scripts/PlayerShip.cs 12bfb05a796b80e72acc6a03aac0eccd13df5d92
- scripts/Ships.cs f773a39450f236765c299741fc55ed8c013f1853
- scripts/Items.cs f642b448b20697225deebcdff2bb3937d0ab3f2f
- tools/smoketest/SmokeTest.cs.txt 0e8f4cc542b2723cde6f0424d67a22f9dbba072b
- tools/screens/Shots.cs.txt 9128a95d8e58e01fdf05f8d1a10722bb8c9194bc

## kits6d-J9 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: Ab.Stealth -> Ab.Veil (id veil, F, SpeedStat veil_speed); PlayerShip.Veil (host: Untargetable veil_time, cool 18, Prime
  veil_break), Unveil, Prime/Primed (the next main volley's multiple, spent once by FireControl); AbilityDef.OnFire (a running
  row hears its ship's main volley first, host: the Veil ends); the veiled hull drawn at PlayerShip.VeiledAlpha 0.35 with a
  shimmer on every peer (status bits). Rows veil_time 5 / veil_speed 1.35 / veil_break 3 / veil_cooldown 18; stealth_* deleted;
  Items @duration veil_time; kit light_stealth_veil (save id kept) -> "Veil Emitter", Needs veil_time. DL13 (the prime waits for
  the first volley after the press).
- checks: LaneA6dVeilRowChecks, LaneA6dVeilChecks (hidden 4.7 / seen 5.3, a slug lands in full, top 351 / 260, cool 18, refused
  x3; veil + boost 481 x3; the volley out of it 147 then 49, inside (ends it) and after it lapsed x3; veil x backstab 220.5 x3);
  frame 78c_wraith_veil. Rewritten: walls-by-position (Ab.Veil), the defaults' message (Veil Emitter), the tour's press (veil),
  sweep witness ["veil"].
- next: kits6d-J10 (Venom).

## kits6d-J10 · PRE
- tier opus; intent: Venom (DL3): scripts/Doses.cs (DoseDef row + a ship's Doses: per-target stacks, host, ticked every 0.5 s through Dealt with the row's credit, ending Last s after the last refresh); Ab.Venom (Q, ability 2): 6 s coated, each landed primary round on a hostile body (never a round in flight) adds a stack (cap 10, 1.25/s each, 5 s after the last), cooldown 22; Dealt.Venom in Items.Repeats (a tick passes as it is: no backstab, no prime); Dps.Venom 6.25; Fx row venom appended (a tick drawn on every peer).
- HEAD 908d5758586b66154495e229cba7ea9754b61f71
- scripts/Abilities.cs e6a396e6eb5525b96fed929cea9a1ea4396a3906
- scripts/PlayerShip.cs abb15054df6e5ab7a9d08fa395a2f772b96dda0f
- scripts/Ships.cs 8d42094ba4cd85a33d2b8e26b71e777192843141
- scripts/Items.cs 8a74d9ab02b67a60bc4d9b880f8c92b4231af725
- scripts/Dealt.cs f9f38d759793f6e4e412ba5a58cd455dceb7d0a0
- scripts/Stats.cs 7de88eec1fa85cc8a279f7c51666c436203e2354
- scripts/Fx.cs 6bfa4408e5484e9b3695121fd480c05147880fd0
- tools/smoketest/SmokeTest.cs.txt c66aadf3e3178495617bc9cec0a890ebcba92bb8
- tools/screens/Shots.cs.txt 8f30da64468a72c042460995ee0005380099b6d0
- new: scripts/Doses.cs

## kits6d-J10 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: scripts/Doses.cs (DoseDef row: Id/Cap/Rate/Last/Every/Fx; DoseRows.Venom; a ship's Doses: Add caps and refreshes,
  Tick deals Stacks x Rate x Every through Dealt every 0.5 s from the first stack until Last s after the last, raising Fx.Venom
  on the hull; host, cleared in _ExitTree). Ab.Venom (Q, ability 2): PlayerShip.Coat (6 s, 22 s), OnDealt -> PlayerShip.Dose
  (primary rounds only, never Tag.Missile|Hulled). Dealt.Venom in Items.Repeats (a tick passes as it is). Rows venom_time 6 /
  venom_cap 10 / venom_dps 1.25 / venom_last 5 / venom_cooldown 22; @duration venom_time. Fx row venom (16, appended). Dps.Veil
  (5.44) and Dps.Venom (6.25) on the Wraith's sheet: 65.33 + 5.44 + 6.25 = 77.03 (the signed 77.1).
- checks: LaneA6dVenomRowChecks, LaneA6dVenomChecks (7 then 10 doses, ticks 6.25 every 0.5 s, the last 4.4-5.0 s after the
  last pellet, front / behind / out of the veil x3; the 6 s coat and 22 s cooldown x3; never on a cruise missile's hull, never
  from a non-primary blow, the dummy dosed and credited venom x3); frame 78d_wraith_venom; sweep witness ["venom"].
- next: kits6d-J11 (Shadow step).

## kits6d-J11 · PRE
- tier opus; intent: Shadow step (DL3): Ab.Step (E, ability 3): refused NO TARGET / OUT OF REACH (900) / COOLING; the owner blinks (AtOnce, PlayerShip.PressTarget = the press's target) to StepSpot = 140 u behind it (its tail; no heading: the far side), pushed clear of its hull, nose on it, speed kept; the host (Stepped) cooldown 14, SkipYaw for a guest, every web let go, its reach judged with 1.25x slack for a guest's lag. **DL14** the host takes the owner's spot (flight is owner-side, as the Slingshot and the Rewind); the 30 u agreement is proved at rung 5, not enforced.
- HEAD a2edf11f6f8f12eb2211ab418be8ea761000f997
- scripts/Abilities.cs 9b3ebdcb61d189d70d02131e918e265154992333
- scripts/PlayerShip.cs 7adabadf366f04b9960f29bf79a74b0c502a8fe3
- scripts/Ships.cs 7adccbfab061bb3083ee003b64b34441623e68a7
- scripts/Items.cs ad167e0c10c50a769f428ddeca9386e716d49afb
- tools/smoketest/SmokeTest.cs.txt ffb1a8114067f6ff785e9f5fae304d5aad33a8fe
- tools/screens/Shots.cs.txt 9855637615cd6f6c24f6c9708f471e69198285d7

## kits6d-J11 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- built: Ab.Step (E, ability 3; refused NO TARGET / OUT OF REACH / COOLING); PlayerShip.PressTarget (the press's target, set in
  UseAbility before AtOnce), StepSpot (pure: 140 u off the tail, or the far side for no heading, pushed clear of the hull),
  Steppable, Step (owner: onto the spot, nose on it, speed kept), Stepped (host: cooldown 14, Slot.At, SkipYaw for a guest, every
  web let go, reach with 1.25x slack). Rows step_reach 900 / step_behind 140 / step_cooldown 14; venom_dps joins @output. The
  Wraith's Blurb and Hint name all three; it learns guns, firemode, veil, venom, step. DL14 (the host takes the owner's spot).
- checks: LaneA6dStepRowChecks, LaneA6dStepChecks (the blink x3: < 1 u of the spot, nose on, speed kept, x1.5 there, cool 14;
  the dummy's far side x3; refused none / 905-960 u / cooling x3; two latched webs let go <= 0.3 s x3); rung 5
  LaneA6dStepHostWatch/HostChecks (counted, the guest's hull within 30 u of the host's spot) + LaneA6dStepGuestChecks;
  sweep witness ["step"].
- next: kits6d-J12 (record).

## kits6d-J12 · PRE
- tier opus; intent: the record -- CHANGES.md Handoff + Unreleased (Known broken honest) + the abilities table's three light rows; DESIGN.md slice-6d section; final POST listing what the test phase owes.
- HEAD b399a8a32d865a18f8f3a1af6451e58d39ace0b1
- docs/CHANGES.md da5597d82222a106aed81b6abd3b45079edbae36
- docs/DESIGN.md 7a1b07f95074213d332987e451ab544cecb8acf0

## kits6d-J12 · POST
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- written: CHANGES.md Handoff + Unreleased (Known broken: engine-unproven, DL3 derived numbers, no Slingshot rung-5 pair,
  the kit-carry check's earlier staleness) + the abilities table's Dart / Echo / Wraith rows; DESIGN.md "slice 6d: the lights".
- THE TEST PHASE OWES (nothing here has run): rung 3 `quick,solo,solo` -- LaneA6d{Pepper,Rod,Ramjet,Sling,Slip,Repeater,Reverb,
  Rewind,Emp,Scatter,Backstab,Veil,Venom,Step}Checks and their Row checks, the rewritten walls / kit-carry / defaults / tour
  / sweep witnesses; rung 4 `screens` -- frames 76b-76f, 78b_wraith_scatter, 78c_wraith_veil, 78d_wraith_venom (one look by
  eye for the new art: the veil's fade, the venom flare, the pellet fan); rung 5 `six,six` -- the Pepperbox, Rod, Ramjet,
  Rewind, EMP and Step host/guest pairs.
- SLICE 6d DONE: J1-J12 committed. Open: DL3 (the Wraith's numbers, owner), DL11 (FlagJam 16 at the merge), DL14.

## kits6d-J13 · PRE (kits6d gate fix)
- tier opus; intent: the opus merge gate's two findings. (1) the rod's recoil fires on the owner's own falling edge of its forcing row (LocalFlight), not from Elapsed, so a guest whose Left the host's packet zeroes first still keeps 30%; (2) delete Status.Evading + its Guards row, AbilityDef.While + its four readers, and name the reverb's blast in three comments; rewrite the harness's Evading checks (bit 16 kept unassigned).
- HEAD 8ca33b117445612d47a6c743b29830e220697836
- scripts/PlayerShip.cs bc1671fb28c31cfb7d2d2fd9bfea43f9abb0fe2f
- scripts/Abilities.cs d0d58b7a01387f66f846f8c6e65d235b81199587
- scripts/Statuses.cs 84731c95c3839b6f1a2299a5bc5598418bd8b684
- scripts/Fx.cs dff544826bc7ad9d536f5a0d0c7b8b93cb94e851
- tools/smoketest/SmokeTest.cs.txt 2c4b349fc7f01007f0df333d0687839a65086226
- docs/CHANGES.md 436fac54a6a42912298851f526294eb719ad9fcd

## kits6d-J13 · POST (kits6d gate fix)
- verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- (1) the rod's recoil: `PlayerShip.Forcing` (the running Forces row; `Forced` reads it) and `_forcedBy`; LocalFlight applies the
  last helm frame's forcing row's Parting.Recoil the frame it stops running (owner, alive, still the class's row), whichever of
  the countdown or ApplyHostState zeroed Left. `Recoil`/`_kick` and the rod's Elapsed hook deleted.
- (2) deleted Status.Evading (bit 16 unassigned, said in the enum header) + its Guards row (the table's one row: Hardened 0.5);
  AbilityDef.While + its four readers; the three comments name the reverb's blast; Guarded's "evasion first" comment gone.
- checks: NEW LaneA6dRodRecoilChecks (rung 3, after LaneA6dRodChecks: Left zeroed 0.8-2.4 s in, not counted down -> 30% +-2
  kept next frame, once; from rest / sliding sideways / boost lit, varied headings; fails on the old code: no Elapsed, no kick);
  LaneA6dRodGuestChecks gains the recoil (after the rod, its sprint over, SpeedAhead <= 0.32 x peak; one varied heading: the
  guest cannot reset its 12 s cooldown, so no loop -- default taken); REWRITTEN the guard-table check (the Evading row -> the
  table is Hardened 0.5 alone, an Untargetable battleship takes 100 whole), the wire-status check (five wire statuses 1 2 4 8 32
  pack into 47, 16 names nothing, total 10 -- the old total==10 was already stale with Unwebbed added), and the two sweep
  status-clear lists (Evading dropped).
- next: none; slice 6d ready for its re-gate.

## kits6d-merge2 · PRE (merge of version-l)
- Intent: git merge version-l (bcab9c3, kits6b merged) into wt/kits6d; HEAD bd1d796ab942e0c560f194df3e740a9d5d99bf28. Conflicts: CHANGES.md, Dealt.cs, Fx.cs,
  Items.cs, PlayerShip.cs, Shots.cs, SmokeTest.cs.txt.

## kits6d-merge2 · POST
- Verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. No engine run.
- Dealt.cs: both sides' ids (this lane's Reverb/Venom rename kept; 6b's Tot/Mortar/Lance added).
- Fx.cs: wire ids -- version-l's Tot = 16 kept, this lane's Venom renumbered to 17; row order tot then venom.
- Shots.cs: version-l's Spotter = 9 / Buster = 10 kept; Pepper 11 / Rod 12 / Echo 13 / Pellet 14 renumbered to the end;
  both sides' ShotDef fields (Command; Paint, Versus, VersusMult, Through) and rows.
- Items.cs: PrimaryShots is the union (echo, pellet, spotter, mortar, lance); the "reverb" comment kept.
- PlayerShip.cs: Lifts uses version-l's LiftStat, extended with Lift.Thrust => ThrustStat; AbilityDef.While (deleted
  here in J13) dropped from both the own-row and 6b's Aura loop; Spec keeps version-l's Kind (pd -> Shell) plus this
  lane's RepeatShare/Pellets; FireControl keeps OnFire + Prime's k and calls 6b's FireOnce(k), whose default branch
  shoots Shoot(k).
- SmokeTest.cs.txt: sustained-DPS check takes this lane's Echo formula with 6b's freighter wording; Fx checks list
  Reverb/Tot/Venom, Fx.All.Length 18 with Fx.Venom == 17; Shots.All.Length 15 with Pepper/Rod/Echo/Pellet 11-14.
- CHANGES.md: both handoffs and both Unreleased sections kept; this lane's wire ids updated to the renumbered ones.
