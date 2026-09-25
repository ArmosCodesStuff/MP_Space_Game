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
