# Ledger: kits lane A, slice 6a (the capitals: Battleship, Carrier, Destroyer)

Writer: one agent at a time in worktree `WarShips_wt_kits6a`, branch `wt/kits6a`, cut from version-l 3450da4.
Spec (newest wins): the v1 cards (BB unchanged in v2: "see v1 §3"; v1 = the owner's first sign-off, NOT in the repo,
its capital numbers are copied into A6a-1..A6a-3 below so no one needs it) -> kits_v2 §3 CARRIER / DESTROYER + §5 ->
kits_v3 §3.1 (Supercarrier) / §3.2 (Grapnel pull, swing, rip) / §3.9 + §5 -> kits_v31 §2, §3.2, §3.3, §3.4 (the drive
vs every move), §3.6 (walls), §6, §7, §8; numbers_curve_raids_items.md §8 (R4: the rip). Rulings: version-l's
docs/plans/README.md. Conventions and D1-D52: ledger_kits.md (D1-D27), ledger_kits5.md (D28-D37),
ledger_kits6c.md (D38-D52), ledger_kits4.md (K4-n), ledger_wings.md (W1-W9). This slice's decisions are A6a-n
(6b builds side by side and may take D53+). BUILD PHASE: no engine run; per job typecheck + verify -Quick + a read
of the own diff; every check written now, run in the final test phase.
A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits, redo it first.
Checks go in NAMED methods `LaneA6a<Thing>Checks` in SmokeTest.cs.txt (solo) and `LaneA6a<Thing>Frames` in
Shots.cs.txt; rung-5 pairs `LaneA6a<Thing>HostChecks` / `...GuestChecks`. SmokeTest.cs.txt is LF: write it in binary.
6.7: every ability >= 3 distinct checks (effect with the spec's literals; each kits_v31 §7 interaction; guest role
or named frame), each from 3 varied situations (Vary / VaryAngle / VaryNear). Touch only BB / CV / DD rows.

## Decisions taken where the spec is silent or superseded (A6a-n)

- **A6a-1 BATTLESHIP (v1 card, unchanged by v2/v3/v3.1 but hull, top and warp):** hull 500; top 88, thrust 47,
  turn 1.08, radius 107 (lane B's rows, kept). Main battery (Space, Weapon, Hold; R = fire mode salvo/stagger, the
  BB alone keeps FireMode): 4 x **12.5** every **2.0 s** = 25.0 DPS, 1000 u, shells 650 u/s. **Arcs:** the fore
  pair (the mounts at y -74.28) never fires within 30° of the stern; the aft pair (y -36.27) never within 30° of
  the bow; so all 4 bear 30-150° off the bow, 2 inside 30° of bow or stern. A per-mount row (ClassArt `MainArcs`
  / a TurretSpec `Blind` {centre, half-angle} read by the turret's fire gate), never a class test.
  **Broadside (F):** 0.5 s wind-up, **6** volleys x 4 guns 0.25 s apart at **x1.25** = 375 side-on (187.5 bow-on:
  the arcs apply), cooldown **12 s** (27.3 DPS; 13.75 s cycle). Rows broadside_volleys 6, broadside_mult 1.25,
  broadside_cooldown 12 (today 3 / 1.0 / 14). A broadside fires mid-warp-charge (kits_v31 §3.4).
- **A6a-2 Brace (Q):** 3 s (brace_time) at x**0.35** damage taken (brace_share: Hardened at the applier's share,
  D9 -- two hardenings keep the stronger), Hold **0.5** (top speed x0.5, a share after the sum, F1), cooldown
  **25 s** from the press (brace_cooldown). Never refused but COOLING. The guard runs through a jump (§3.4).
  No frame is listed for it (§8 step 7), and Hardened is already on the wire: its third check is the Taunt pair
  (§7 "WD Taunt's guard vs BB Brace": x0.35 wins over x0.67 while both run, x0.67 after 3 s) and a guest check
  that a guest BB's Brace halves the host's copy's speed report ward (rung 5) -- or, if that proves empty, the
  bar frame `LaneA6aCapitalBarFrames` showing BRACE lit.
- **A6a-3 CIWS (E):** 6 s (ciws_time): both PD mounts fire x**8** rate and x**3** damage at their own 460 u
  (1.5 every 0.0625 s = 24 DPS a mount, 48 in all), cooldown **20 s** (ciws_cooldown). PD's own target rule
  (Targeting.PointDefence: missiles, light craft, fighters; ranked by Turret.Rank -- never a heavy, boss or
  siege cruise missile). A running CIWS STOPS under an overshoot's Disabled (decision 13: it is a gun) while the
  plain PD keeps firing. Needs an F1 lift SCOPED TO THE PD MOUNT: new AbilityDef fields `PdRateStat` /
  `PdDamageStat` (ciws_rate 8, ciws_damage 3) summed by PlayerShip.Cadence("pd_interval") and Spec(pd) only,
  with `While = s => !s.Disabled` -- a row, never `if (id == "ciws")`. Field look: none listed; its drawn proof
  is the bar frame (CIWS lit) plus the PD flashes in `LaneA6aCiwsFrames`.
- **A6a-4 CARRIER row (v1 + v2 + v3.1):** hull **425**, turn 0.9, radius **140**, top 99 (CarrierTop, lane B),
  thrust 49.5 (kept, lane B's scaling). PD **2** mounts (3 -> 2, D8: the stern mount goes; the flank pair stays).
  Fighters 3.5 a shot (fighter_damage 2.5 -> 3.5 on the Carrier's Nums: 25.0 DPS sheet); bombers 2 x 4 torpedoes
  x **60** (torpedo_damage 137.5 -> 60; bomber_ammo 4 if not already). ClassDef.Abilities in LEARN ORDER:
  Attack (Space, weapon), Recall (R, weapon), Bombers (F, ability 1 L1), **Gunships (E, ability 2 L3)**,
  **Supercarrier (Q, ability 3 L6)** (kits_v31 §3.6; the bar reads F E Q, the keys stay where the pilot put them).
- **A6a-5 Warp gunships (E):** `Ab.Gunships` id "gunships", TakesTargets (1-3 picks, ClassDef.Targets 3),
  Press: for each pick `Sortie(WingKind.Gunship, pick)` (W5: 2 a target, 240 u orbit, 12 s, 10 DPS each, the
  3000 u leash at launch only). Cooldown **25 s** from the press (gunship_cooldown). Refused NO TARGET (no pick,
  and no selected hostile), OUT OF RANGE (every pick past gunship_leash), COOLING. Slot line `GUNSHIPS 11 s`
  while any is out (the wing list's own time).
- **A6a-6 Supercarrier (Q):** `Ab.Supercarrier` id **"super"** (Fx's patrol Field row is keyed on slot "super":
  the ring draws from this slot on every peer, no Fx edit). Press: Sl("super").Left = patrol_time 20;
  Sortie(WingKind.Patrol) once (3 craft 0.83 s apart = the fighter's own spacing); the patrol takes whatever
  Turret.Rank puts first inside patrol_range 600 u, MISSILES INCLUDED (kits_v31 §3.2; the Patrol filter already
  = Sentry, W4). Cooldown **30 s from the END** (super_cooldown; set in the row's Elapsed, as a stance's). Never
  refused but RUNNING / COOLING. Slot `SUPER 14 s`. The patrol flies back after a jump (leashed, §3.4).
- **A6a-7 DESTROYER row (v1 + v2 + v3.1):** hull **395**, top 117 (lane B), turn **1.2**, radius **95**.
  **Director battery** (Space, Weapon, Hold): 2 x **11.25** every **0.5 s** = 45.0 DPS, **700 u**, shells
  **760 u/s**, turrets **2.09 rad/s** (main_turn); with a hostile SELECTED within **840 u** (1.2 x main_range:
  row director_lead 1.2) the guns lead it by themselves (Aim lead on the selected target's velocity), else they
  follow the cursor. Fit.Missiles, Ab.Missile, Ab.Reload and the DD's FireMode LEAVE (v1 "Leaves"); the
  missile_* rows and Fit.Missiles go if nothing else reads them (the analyser decides; the Warden's hunters
  have their own hunter_* rows -- check before deleting). **Long Lance (F):** one torpedo off the bow along the
  heading, **300** (lance_damage), **170 u/s** (lance_speed), **3000 u** (lance_range), stops on the first
  hostile it touches, never a missile (a Shots row: `Lance`, appended at the END of Shots.All), cooldown **18 s**
  (lance_cooldown). Gear: the four ids of v2 (dd_salvo Rapid Reload, dd_buster Heavy Warhead, dd_seeker
  Long-Run Tubes, dd_loader Deck Winch) exist nowhere in today's tree: built as the DD's Kit rows if the kit
  table carries them, else recorded as owed to lane I (items). Kit parts dd_main_battery / dd_missile_rack
  re-pointed at lance_damage.
- **A6a-8 Suppressing fire (Q):** for **6 s** (suppress_window) every hostile a DIRECTOR SHELL hits (the main
  gun's weapon id, heard through the row's OnDealt) gets Status.Suppressed until **3 s** (suppress_time) after
  its last hit; the Lance and the PD never apply it (their weapon ids differ). Status.Suppressed / OutGuards
  already exist (Statuses.cs:26, :81: Gun 0.5, Move 0.7, Super 1.0, HoldsThrow). Cooldown **20 s** from the press
  (suppress_cooldown). The grey chevron: a Fields/mark row on the suppressed body if lane D's table has a mark
  look; else an Fx row `chevron` raised per application (one raise, no new RPC). Rung 5: the chevron on the
  other guest.
- **A6a-9 Grapnel (E):** needs a selected hostile within **700 u** (grapnel_reach). Immovable anchor
  (Targeting.Immovable = Boss|Structure|Dummy): HelmMoves.Tether with HelmNums from the DD's rows: Bite 0.15
  (grapnel_bite), Pull 450 (grapnel_pull), Stop 250 (grapnel_stop), Clear 150 (grapnel_clear), Reach 700, Reel 100
  (grapnel_reel), Time 5 (grapnel_swing). A craft: Towing.Tow (row Grapnel: 160 u off the bow in 0.5 s, up to 3 s,
  hurled 600 u/s up to 900 u, 60 / 120 each). Refused NO TARGET, OUT OF RANGE, a missile ("NO HOLD"), WEBBED (the
  swing only), COOLING. Cooldown **16 s from cast-off** (grapnel_cooldown). **The rip** at cast-off (a second E,
  5 s, a web, the DD's own warp -- HelmEnd Pressed / Time / Webbed / Disabled / Drive; NOT AnchorLost /
  AnchorWarped / Wrecked): FIRST Fx.Tear(anchor, hook, toward the DD) (lane D's trap: raise before the hit), THEN
  Dealt.Deal(anchor, **0.01 x anchor.MaxHp + 10**, credit "grapnel") on the host -- grapnel_rip_share 0.01,
  grapnel_rip_flat 10 rows. The rip applies to bosses, structures (stations, pylons) and dummies only (the
  Immovable filter, never a type test); never a craft.
- **A6a-10 Witness rows:** every new ability id gets its witness in the fittings sweep ("every ability on every
  bar has a witness"), the Fit rows table there edited in the same job (Fit.Missiles row goes with the DD's
  missiles; a Fit that has no ability left leaves the table), and the walls' order check (Unlocks) reads the
  three capitals' learn order. Hull literals asserted anywhere in the harness for 300 / 200 / 250 are rewritten.
- **A6a-11 Open question defaults:** v3.1 decision 10 (Supercarrier timed 20 s / 30 s from end), 11 (grapnel:
  pull, then swing; the rip at cast-off), 12 (proportional overshoot), 13 (the overshoot disables CIWS, PD and
  craft keep going) -- the defaults are built.

## Jobs (foundations before their users; each: PRE, edit + checks, typecheck + quick, POST, commit)

- **kits6a-J1 BB row + main battery arcs + Broadside numbers** (A6a-1). Ships.cs BB row (hull 500, main_damage
  12.5), the arc row (ClassArt / TurretSpec) and the main-gun fire gate (Turret.Shoot / PlayerShip's salvo),
  Stats broadside rows. Checks LaneA6aArcChecks (bearings {0, 29, 31, 90, 149, 151, 180}° with VaryAngle hull
  headings: 2 / 4 guns bear; range 999 / 1001), LaneA6aBroadsideChecks (exactly 24 shells from turret angles
  {0, 90, 180} x {still, full ahead, turning}; 375 side-on on a parked dummy, 187.5 bow-on; 12.0 s cooldown;
  fires mid warp charge). Rewrite every harness line asserting the BB's 300 hull, 17.9 shell, 3 volleys or 14 s.
- **kits6a-J2 Brace** (A6a-2). Ab.Brace, rows. Checks LaneA6aBraceChecks (beam tick / rock 250 -> 87.5 / ring /
  slug x {before, inside at VaryNear 0.5-2.9 s, after 3.1 s}; top 44 at full ahead; 25.0 s cooldown),
  LaneA6aBraceTauntChecks (Brace + a Taunt guard: 0.35 then 0.67), LaneA6aBraceWarpChecks (the guard survives a
  jump; a charge does not end it).
- **kits6a-J3 CIWS** (A6a-3): the PD-scoped lift foundation (AbilityDef.PdRateStat / PdDamageStat), Ab.Ciws.
  Checks LaneA6aCiwsChecks (48 DPS +- 2 on two light targets at VaryNear 300-450 u; tags {missile, light, fighter,
  heavy, boss, cruise} x {459, 461} u), LaneA6aCiwsDisableChecks (an overshoot's Disabled stops the x8 while PD
  keeps its 1 DPS), LaneA6aCiwsRankChecks (vs the patrol: both rank by Turret.Rank, a seeker first), frame
  LaneA6aCiwsFrames.
- **kits6a-J4 CV row** (A6a-4): hull 425, radius 140, PD 2, fighter 3.5, torpedo 60 x 4; rewrite harness literals
  (carrier 200 hull, 3 PD mounts, 137.5 torpedo, fighter 2.5). Checks LaneA6aCarrierRowChecks.
- **kits6a-J5 Warp gunships (E)** (A6a-5). Checks LaneA6aGunshipChecks ({1, 2, 3} picks -> {2, 4, 6} craft,
  240 +- 20 u, 12 s; 3001 u refused OUT OF RANGE; 25.0 s cooldown; ByKey(X) null on all 12; E rebinds by swap),
  rung 5 LaneA6aGunshipHost/GuestChecks (a guest's picks reach the host; craft snap in on the other guest).
- **kits6a-J6 Supercarrier (Q)** (A6a-6). Checks LaneA6aSuperChecks (3 craft 0.83 +- 0.05 s apart; a raider at
  VaryNear 590 u engaged, 610 u not; a seeker at 590 u engaged first and downed; 20.0 s up, cooldown 30 s from the
  end; the first wing still attacks at 1400 u: 6 out), LaneA6aSuperWarpChecks (after a jump the patrol returns to
  its ring), frame LaneA6aSuperFrames (the dashed ring + amber craft), rung 5 LaneA6aSuperHost/GuestChecks.
- **kits6a-J7 DD row + director battery + Long Lance** (A6a-7): Shots row Lance, Ab.Lance, the lead, missiles /
  reload / DD firemode deleted with their callers. Checks LaneA6aDirectorChecks (selected / none x lateral
  {0, 100, 300} u/s x range {100, 700, 839, 841}; 45 DPS sheet), LaneA6aLanceChecks (300 on a dummy at VaryAngle;
  a light in front of the boss takes it; never a missile; 18.0 s cooldown).
- **kits6a-J8 Suppressing fire** (A6a-8). Checks LaneA6aSuppressChecks (v2 Proves: shell / Lance / PD on a
  webifier, only the shell; the literal table {webifier 1.0->0.5, gunship 1.25->0.625, pylon 9->4.5, base 18->9,
  Lancer guns 3.6->2.52, wave 45->31.5, Drake slug 6->4.2, beam 50->50, rock 250->250} at {2.9, 3.1} s after the
  last hit; the held throw; 6.1 s applies nothing; two DDs x0.5; 20 s), rung 5 LaneA6aSuppressHost/GuestChecks.
- **kits6a-J9 Grapnel: pull, swing, tow, rip** (A6a-9). Checks LaneA6aGrapnelPullChecks (v3 Proves on {Lancer,
  Drake, pylon, base, dummy} from VaryNear 400 / 699 u), LaneA6aGrapnelRipChecks (hull drops by 0.01 x MaxHp + 10
  at cast-off on {Lancer, Drake, pylon, base} at VaryAngle; one Fx raise; none on a craft, none when the anchor
  dies; party of 2 = 1% of the party-scaled hull), LaneA6aGrapnelTowChecks (v2 tow / hurl lines, WEBBED refusal,
  a warp casts off WITH a rip), frame LaneA6aRipFrames (chunk mid-tumble + scar), rung 5
  LaneA6aGrapnelHost/GuestChecks (host damage; the chunk on a guest within 20 u).
- **kits6a-J10 Record**: CHANGES.md Unreleased + Handoff, DESIGN.md slice-6a section (the ship-class table's BB /
  CV / DD rows), the final POST (what the test phase owes: checks, rungs, frames).

## Code pointers (3450da4)

Ships.cs: BB row 160-187, CV 189-216, DD 218-245 (Nums, Kit, Art, Abilities). Abilities.cs: AbilityDef fields
33-160 (Hold, RateStat, While, OnDealt, TakesTargets, Stance, Cooldown), Ab.Guns/FireMode/Broadside 205-238,
Missile/Reload 240-265, Attack/Recall/Bombers 267-302, Timed() ~595. Stats.cs: Broadside 159-163, Missile 169-175,
PD 182-186, Fighters 193, Bombers 205-207, Gunships 217-228, Patrol 229-233, BroadsideDamage 272. PlayerShip.cs:
Spec(pd) 305, Sortie 543, StartBroadside 658, OrderStrike 893, FireMissile 1027. Turrets.cs: TurretSpec 31, Prefer
86, hold guard ~152, Rank ~201. HelmMoves.cs: HelmNums 38, HelmEnd 48, Begin 155, Fly 191, End 203, Confirm 214.
Towing.cs: Grapnel row 54-60, Tow 72, Hurl 79. Fx.cs: Tear 173 (rip rows 128-136), Fields patrol row 502 (slot
"super"). Statuses.cs: Suppressed 26, OutGuards 81. Targeting.cs: Immovable ~72, Patrol 84. SmokeTest.cs.txt: the
fittings sweep's witness table ~13755, fitRows ~13797.

## JOB 0 · POST
- Worktree created from version-l 3450da4 (step 0: it did not exist). Spec read; decisions and job list above.
  No code touched. Next: kits6a-J1.

### kits6a-J1 · PRE · BB row + main battery arcs + Broadside numbers (A6a-1) -- tier opus
- Intent: BB hull 500, main_damage 12.5 (4 x 12.5 / 2.0 s = 25 DPS); ClassArt.MainBears (per main mount, the
  bearings off the bow its barrel may fire within: fore pair 0-150°, aft pair 30-180°), Turret.Bears + pure
  Turret.InArc, Shoot holds fire outside it; broadside_volleys 6, broadside_mult 1.25, broadside_cooldown 12.
  Checks: NEW LaneA6aArcChecks, NEW LaneA6aBroadsideChecks; harness lines asserting the old BB truths rewritten.
- Files: scripts/Ships.cs, scripts/Turrets.cs, scripts/PlayerShip.cs, scripts/Stats.cs, tools/smoketest/SmokeTest.cs.txt.
- HEAD 8d9023affdd9f544a525e71f8c3c3ef1b753aeab · Ships.cs dd6c77c19088e55fc3e815b34640d824b48d30fd · Turrets.cs
  471d84e4463dbde1f3fd779b4001e3a59af8de63 · PlayerShip.cs 6d41d138618c4c1423779be23acd25e651ec6e5e · Stats.cs
  64bf427d6f48ed692346d261a56b32ad5a29f582 · SmokeTest.cs.txt f93a02fb2d9489c93e60da27f4f240192bff56ce
### kits6a-J1 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: BB hull 500, main_damage 12.5; `ClassArt.MainBears` (fore pair (0, 150), aft pair (30, 180)); `Turret.Bears`
  set at the mount (PlayerShip.AddTurret), pure `Turret.InArc`, `Turret.BearingOffBow` / `Bearing`; `Turret.Shoot`
  holds fire outside the arc (the barrel still swings; every other host's mount keeps (0, 180)). Stats broadside
  rows 6 / 1.25 / 12.
- Checks NEW: LaneA6aArcChecks (the table at 0/29/31/90/149/151/180 deg -> 2/2/4/4/4/2/2; 3 live VaryAngle cases:
  abeam 4, bow 2, stern 2 shells a salvo), LaneA6aBroadsideChecks (sheet 375 / 13.75 s / 27.27 / 25 DPS / hull 500;
  5 live runs: turrets {0, 90, 180} deg away x {still, ahead, turning} -> 24 shells in 6 volleys, bow-on 12,
  mid warp charge 24 with Charging true; 12.0 s cooldown). Called after LaneA4/ItemsDoor in the solo list.
- Checks REWRITTEN (6.3): the menu demo's six volleys; the fingerprint `sheet Battleship.hull=500`; ChipChecks'
  battleship 500; the K window lines (25.00 / 6.25 / "6 volleys × 4 shells × 15.63 ... = 375.0 damage, every
  13.8 s" / 27.27 / 54.27); the F block (6 volleys at x1.25, 12 s, 375 landed, the dummy put ABEAM); "a shell lands
  for the stock 12.5"; the balance 25 + 375 / 13.75 = 52.27; the kit literal (BB half; the CV half is J4's);
  the refit (60/500, T10 794.75, T1 625); the salvo == staggered block parks the dummy abeam (a bow dummy would
  take half under the arcs).
- Owed at rung 3 (`quick,solo,solo`): all of the above. Traps: the K-window strings are format-sensitive
  (15.625 -> "15.63", 13.75 -> "13.8"); LaneA6aBroadsideChecks' warp run releases V after the volleys (a short jump
  in open water at `yonder`).
- Checkpoint: the commit after this entry. Next: kits6a-J2 (Brace).

## Handover 1: the first agent stops after kits6a-J1 (context spent on the spec read), at a job boundary
- Next agent: read this ledger only (decisions A6a-1..11, the job list, the code pointers), then kits6a-J2.
- Harness helpers seen: SetClass 1997, Park 2008, Shells() 2005, AimWorld 81, Vary/VaryAngle/VaryNear 1136-1139,
  WarpHold ~3236 (s.DriveHeld / PressDrive / Charging / ChargeHeld); the solo call list for slice 6a sits just
  before "── WARRIOR: the blade (slice 6c) ──" (~13480); rung-5 guest calls ~17420 (LaneA6c*GuestChecks).
- Edit SmokeTest.cs.txt with a python script from a file (LF, binary); bash heredocs with long C# fail here.
