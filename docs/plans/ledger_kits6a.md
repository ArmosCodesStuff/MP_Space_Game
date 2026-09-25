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

### kits6a-J2 · PRE · Brace (A6a-2) -- tier opus (agent 2; no COORDINATOR NOTE in the tail)
- Intent: a generic TIMED row foundation -- AbilityDef `Time` (stat id: how long it runs) and `Guard` (stat id: the
  Hardened share it applies for that time), pressed through one door PlayerShip.RunFor(id) (Left = Time, Cool =
  Cooling(Cooldown), Hardened at Guard) -- so Brace (and CIWS / Suppress after it) are rows, not methods. Ab.Brace
  (Q, Hold 0.5, Guard brace_share 0.35, 3 s, 25 s from the press; refused COOLING only); BB rows brace_*; BB
  Abilities = Guns, FireMode, Broadside, Brace. Items "@duration" gains brace_time.
  Checks: NEW LaneA6aBraceChecks, LaneA6aBraceTauntChecks, LaneA6aBraceWarpChecks; the sweep's witness row "brace".
- Files: scripts/Abilities.cs, scripts/Ships.cs, scripts/PlayerShip.cs, scripts/Items.cs, tools/smoketest/SmokeTest.cs.txt.
- HEAD 2301512f0d293c28320336ba980609a187836a0d · Abilities.cs 88abfd0541f772b420ad08ad43d135fdf171d775 · Ships.cs
  ba82e89025910c69d46ed1b6ba95d09fb02ce131 · PlayerShip.cs a99515249a16e6f89903cd9e328738ddf215c2da · Items.cs
  c25c8da8e16f9297cbe1347682b81727e53dc633 · SmokeTest.cs.txt 9c965a24dfbf089b1392baa70c8818266155d428
### kits6a-J2 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: AbilityDef.Time / Guard + PlayerShip.RunFor (the timed-row door: Left, Cooldown from the press, Hardened at
  Guard); Ab.Brace (Q) as a row; BB Rows brace_time 3 / brace_share 0.35 / brace_cooldown 25; BB bar Guns, FireMode,
  Broadside, Brace; Items @duration + brace_time.
- Ruling read (A6a-2 amended): kits_v31 §7's "WD Taunt's guard vs BB Brace" is a DESIGN split (v3 §5: deep-short vs
  shallow-long), not a stack on one hull (neither class carries the other). StatusSet keeps ONE Hardened entry (longest
  time, strongest share), so a 0.67 applied beside a Brace would read 0.35 for its whole time: noted, not changed.
- Checks NEW: LaneA6aBraceChecks (rows; 3 runs still/ahead/turning: 250->87.5, 30->10.5, 12->4.2, whole before and
  after, hold 0.5, <= 44 u/s, 25.0 s, COOLING, bar BRACED), LaneA6aBraceWarpChecks (3 runs: braced mid-charge, the
  jump within 10% of ChargeReach, the guard still runs after it: 100 -> 35), LaneA6aBraceSplitChecks (the split
  literals; 3 runs: 24 broadside shells while braced, a second guard 0.67/0.5/0.2 -> the stronger kept). The sweep's
  witness row "brace". REWRITTEN: the battleship bar order line (+ "brace").
- Owed at rung 3: all of the above. Next: kits6a-J3 (CIWS).

### kits6a-J3 · PRE · CIWS (A6a-3) -- tier opus
- Intent: the SCOPED lift foundation: AbilityDef RateOn (the one interval stat a row's RateStat reaches; null = every
  reload), DamageStat + DamageOn (a damage lift on one damage stat); PlayerShip.Lifts(kind, on), Cadence(stat) and
  DamageOf(stat) read them; Spec(pd) takes DamageOf("pd_damage"). Ab.Ciws (E, timed row: ciws_time 6, ciws_rate 8 on
  pd_interval, ciws_damage 3 on pd_damage, While !Disabled, ciws_cooldown 20 from the press). BB bar + Ciws.
  Checks: NEW LaneA6aCiwsChecks, LaneA6aCiwsDisableChecks, LaneA6aCiwsRankChecks; frame LaneA6aCiwsFrames; witness "ciws".
- Files: scripts/Abilities.cs, scripts/Ships.cs, scripts/PlayerShip.cs, scripts/Items.cs, tools/smoketest/SmokeTest.cs.txt, tools/screens/Shots.cs.txt.
- HEAD d9b813cfd2306c12b04a9a36f1cef983aa335364 · Abilities.cs 787b4a439c949686314b071e38c3c215f8dd2d20 · Ships.cs fd3651accee7e3ae3c95fbaa9cb3663c43db4fa3 · PlayerShip.cs 4ed54a73c44efb45bfb6ed1c8d2a3b80952771e0 · Items.cs 99fc23d4c32be6ae7e7daa285253d2e9650a11a7 · SmokeTest.cs.txt 3bc33d2917ae334ca5f2d2bddeffc5a9fadcf595 · Shots.cs.txt 5bb73590976091ff0fee983dcf2a5b799baef7dc
### kits6a-J3 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: AbilityDef RateOn / DamageStat / DamageOn (the scoped lift); PlayerShip.Lifts(kind, on), Cadence(stat) passes
  its stat, FireRate takes unscoped rows alone, DamageOf(stat); Spec(pd).Damage = DamageOf("pd_damage"). Ab.Ciws (E,
  timed row via RunFor, While !Disabled); BB rows ciws_time 6 / ciws_rate 8 / ciws_damage 3 / ciws_cooldown 20; BB bar
  + Ciws (learn order Broadside, Brace, CIWS); Items @duration + ciws_time.
- Checks NEW: LaneA6aCiwsChecks (rows; 3 runs: 48 +- 3 in 1.0 s from a blow, PD 1.5 / 0.0625, mains 2.0, 20.0 s,
  COOLING, bar CIWS, back to 0.5 / 0.5 after 6 s), LaneA6aCiwsDisableChecks (3 runs: Disabled 1.0-1.5 s -> PD 0.5 /
  0.5 and still firing, x3 / x8 again once it clears), LaneA6aCiwsPreyChecks (3 runs: a gunship never taken, a
  60-hull webifier down < 2.5 s); frame LaneA6aBbFrames (85a_bb_brace, 85b_bb_ciws); witness "ciws". REWRITTEN: the
  battleship bar order line (+ "ciws").
- Owed: rung 3 the three checks; rung 4 the two frames. Next: kits6a-J4 (CV row).

### kits6a-J4 · PRE · CV row (A6a-4) -- tier opus
- Intent: Carrier Nums hull 425, turn_radius 140, pd_count 2 (Art.Pds loses the stern mount, D8), fighter_damage 3.5,
  torpedo_damage 60 (bomber_ammo 4 already); harness literals for 200 hull / 3 PD / 137.5 / 2.5 / the sheet and measured
  DPS rewritten. Checks: NEW LaneA6aCarrierRowChecks.
- Files: scripts/Ships.cs, tools/smoketest/SmokeTest.cs.txt.
- HEAD 16e01d85f722411406ea9c992af0cc8298a3afb4 · Ships.cs 160dcd39b846a85f2cf0ad0fdc2b3763cac2f95a · SmokeTest.cs.txt 011ce70c6fe21923f841812f285bfd274416ee10
### kits6a-J4 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: Carrier hull 425, turn_radius 140, pd_count 2 + Art.Pds without the stern mount; fighter_damage 3.5 and
  torpedo_damage 60 as the SHEET's defaults in Stats.cs (the carrier is the only Wing hull; one truth, not a Nums
  override -- Stats.cs was touched beyond the PRE's list for that).
- Checks NEW: LaneA6aCarrierRowChecks (sheet 425/140/2/3.5/60 x 4 x 2, 25.0 fighter DPS, sustained 44.45; 3 live runs:
  hull 425, two mounts abreast amidships, both on a light). REWRITTEN (6.3): the PD table's carrier (2 mounts), "carrier
  has two PD turrets", the stock torpedo 60, the measured carrier DPS reference 93.6 -> 61.5 (scaled by the two
  ratios: re-measure at rung 3 if it misses the +-35%), the kit literal (425 / 3.5 / 60), the sustained / Bomber line
  (480 / 27.5 = 17.45, fighters 25, PD 2), the Heavy Battery T2 fighter 3.5 x 1.275.
- Next: kits6a-J5 (warp gunships E).

### kits6a-J5 · PRE · Warp gunships E (A6a-5) -- tier opus
- Intent: a SORTIE row door: AbilityDef Sends (a WingKind) + CoolAfter (cooldown from the END of its time), pressed
  through PlayerShip.Launch(id, sel) (a targeted wing row: one Sortie per pick, the picks or else the selected; a
  ToHull row: one Sortie; nothing sent = nothing spent; Left = the row's LifeStat, Cool from the press or the end).
  RunFor takes CoolAfter too. Ab.Gunships (E, TakesTargets, gunship_cooldown 25 from the press; refused COOLING / NO
  TARGET / OUT OF RANGE on the owner from its hub's picks or the selected); CV Rows gunship_cooldown; CV bar Attack,
  Recall, Bombers, Gunships. Checks: NEW LaneA6aGunshipChecks, LaneA6aGunshipRebindChecks, rung 5
  LaneA6aGunshipGuestChecks; witness "gunships".
- Files: scripts/Abilities.cs, scripts/Ships.cs, scripts/PlayerShip.cs, tools/smoketest/SmokeTest.cs.txt.
- HEAD 1535f70f3c46857729fe194ce02cb9748457f5b8 · Abilities.cs 2ddc97fedb88edafc9e7d29808fed7cfd4ddab78 · Ships.cs 7a15042070eca75243ce7103a4715e1d2ca9a07e · PlayerShip.cs 7e468f52b793072e6ed858a61a6bd7a8dda8e64f · SmokeTest.cs.txt 8352cad9bf063d7ae2ef932a8e01596d80b9adc7
### kits6a-J5 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: AbilityDef.CoolAfter + Sends; PlayerShip.Engage (time + cooldown, from the press or the end) shared by RunFor
  and the new PlayerShip.Launch (the sortie door: Picked, else the selected; ToHull rows once; nothing sent spends
  nothing). Ab.Gunships (E, TakesTargets; Refuse on the owner reads its hub's live picks, else the selected: COOLING /
  NO TARGET / OUT OF RANGE). CV Rows gunship_cooldown 25; CV bar + Gunships (ability 2).
- Checks NEW: LaneA6aGunshipChecks ({1,2,3} picks -> {2,4,6} craft on 240 +- 25 u circles, 12 s up, 25.0 cooling,
  COOLING, bar GUNSHIPS, none left at 12.6 s; OUT OF RANGE at 3050-3400 u sending nothing, NO TARGET), 
  LaneA6aGunshipFireChecks (3 runs: 60 +- 10 in 3.0 s; the carrier sailing 3200-3600 u off leaves them on station; a
  dead pick is left), rung 5 LaneA6aGunshipGuestChecks (a guest's E at the dummy: 2 craft on the circle here, 12 / 25
  arrive, COOLING); witness "gunships". REWRITTEN: the K window keys-tab count for the battleship 9 -> 11 (J2/J3's
  Brace and CIWS rows).
- Default taken: the checks set the hub's picks (H.AddTarget) as a pilot would, so Refuse and the press agree.
- Next: kits6a-J6 (Supercarrier Q).

### kits6a-J6 · PRE · Supercarrier Q (A6a-6) -- tier opus
- Intent: Ab.Supercarrier id "super" (Q, Sends Patrol through Launch, CoolAfter, super_cooldown 30 from the END;
  refused RUNNING / COOLING); CV Rows super_cooldown; CV bar + Supercarrier (ability 3). Checks: NEW LaneA6aSuperChecks,
  LaneA6aSuperMissileChecks, LaneA6aSuperWingsChecks; frame LaneA6aSuperFrames; witness "super".
- Files: scripts/Abilities.cs, scripts/Ships.cs, tools/smoketest/SmokeTest.cs.txt, tools/screens/Shots.cs.txt.
- HEAD ef8366ab275c55b97a9683cc3c97aba36d1c1c6f · Abilities.cs 5774583e442fed2ae5ed61018aca05285b6cc3a4 · Ships.cs 27316c47a59f977cba7522a252c898a58e7202dc · SmokeTest.cs.txt 125a42d101cab9b9485d6685fc0e4270fd94ba1e · Shots.cs.txt f043b28080eb3656d2553f301f45cc932f18d62c
### kits6a-J6 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: Ab.Supercarrier (id "super", Q, Sends Patrol via Launch, CoolAfter: Cool = 20 + Cooling(30) at the press;
  refused RUNNING / COOLING); CV Rows super_cooldown 30; CV bar Attack, Recall, Bombers, Gunships, Supercarrier
  (learn order F E Q). The Fx patrol ring (slot "super") draws from this slot with no Fx edit.
- Checks NEW: LaneA6aSuperChecks (rows; 3 runs: 3 craft 0.83 +- 0.05 s apart, a webifier 250-450 u taken, one 850-1100
  u never, 20 / 50 at the press, RUNNING, bar SUPER; run 0 to the end: none out, 29.4-30 cooling, COOLING),
  LaneA6aSuperMissileChecks (3 runs: a seeker from 480-560 u taken <= 1.5 s before a webifier beside it, downed, hull
  untouched, credited "patrol"), LaneA6aSuperWingsChecks (3 runs: fighters on attack + 2 gunships on the circle + 3
  patrol within 700 u of the carrier, 6 fighters+patrol out); frame LaneA6aSuperFrames (85c_cv_supercarrier);
  witness "super".
- Not built: a rung-5 Supercarrier pair (lane E's WingsHostSortie / WingsGuestWatch already prove a host-sent patrol
  on a guest's carrier); a warp-return check (the patrol's leash back to the ring after a jump is lane E's Wing law).
- Next: kits6a-J7 (DD row + director battery + Long Lance).

## Handover 2: agent 2 stops after kits6a-J6 (context near the limit), at a job boundary
- Done J2-J6 (Brace, CIWS, CV row, gunships, Supercarrier). Next: kits6a-J7 (DD row + director + Long Lance), then
  J8 Suppress, J9 Grapnel, J10 Record. Doors a later job reuses: AbilityDef Time/Guard/CoolAfter + PlayerShip.RunFor
  (a timed row: Suppress's 6 s window = RunFor + OnDealt), Engage (time + cooldown), Launch (sortie rows), the scoped
  lift RateOn/DamageStat/DamageOn + PlayerShip.DamageOf.
- J7 map of what the missile removal touches (3450da4 + this lane): PlayerShip.cs MissilesLoaded/Reloading/
  CanFireMissile ~267, Sl("missile").N = missile_mag ~389 and ~444, StartReload ~665, BurstSides/BurstSplay/
  FireMissile ~1052-1076; Stats.cs Fit.Missiles group 165-175, MissileDps 267-269, Dps.Missiles 330-334; Ships.cs DD
  row (Fit.Missiles, missile_* Damage/Reach/Cycle, Dps.Missiles, kit parts' "missile_mag", Abilities Missile/Reload);
  Abilities.cs Ab.Missile / Ab.Reload (~250-280); ActiveReload.cs 31/44/129-143 (read before deleting: may be the
  railgun's own "reload" words); Items.cs:101 @output "missile_damage"; Sfx "missile_whoosh" (the torpedo also uses it:
  keep). Harness: ~60 lines at SmokeTest.cs.txt 1564 1724 6106 6150 6386-6432 8014-8037 9420 9427 9858 11508 11586
  11716-11812 (the DD missile block) 13746 13750 13991 (the sweep's "missile"/"reload" witnesses and fitRows'
  Fit.Missiles row) 14452-14695 16624-16757 17802-17816 -- each REWRITTEN to the director / Lance truth (6.3), none
  deleted without its replacement. Shots.cs.txt: 16 missile mentions (grep; the DD closeups at 471 / 690 / 802).

### kits6a-J7 · PRE · DD row + director battery + Long Lance (A6a-7) -- tier opus (agent 3; no COORDINATOR NOTE in the tail)
- Intent: DD Nums hull 395, turn 1.2, radius 95, main 2 x 11.25 / 0.5 s, 700 u, shells 760, main_turn 2.09, row
  director_lead 1.2 (PlayerShip.Directed: the owner's aim leads a selected hostile within main_range x director_lead,
  Lead + Missiles.Predict; rides the state report, no new wire). A BOW-SHOT row foundation: AbilityDef.Bow (a Shots.All
  kind + damage / speed / range / cooldown stat ids) pressed through PlayerShip.FireAlong(id); Ab.Lance (F) = Bow on
  Shots row 4, renamed in place "missile" -> "lance" (A6a-12: its one user leaves, the wire index stays, no append);
  Dps.Lance. DELETED: Fit.Missiles, Ab.Missile, Ab.Reload, the DD's FireMode, PlayerShip missile / reload members,
  BurstSides / FireMissile, Stats' Missile group + MissileDps + Dps.Missiles, LaunchTorpedo's heavy flag; Items @output
  missile_damage -> lance_damage; kit parts Need lance_damage. DD bar: Guns, Lance.
  Checks: NEW LaneA6aDirectorChecks, LaneA6aLanceChecks, LaneA6aLanceHitChecks; every harness line on the missiles /
  reload / DD 250 hull rewritten (6.3); the sweep's witnesses "lance" and fitRows.
- Files: scripts/Ships.cs, Abilities.cs, PlayerShip.cs, Stats.cs, Shots.cs, Combat.cs, Items.cs, Dealt.cs,
  tools/smoketest/SmokeTest.cs.txt, tools/screens/Shots.cs.txt.
- HEAD 8d2c8b8570aa2936a7a6820b06268122a9004b9a · Ships.cs f55ab21b · Abilities.cs c1453e69 · PlayerShip.cs 0307607f ·
  Stats.cs 985dc811 · Shots.cs c44dd017 · Combat.cs b1c74172 · Items.cs 69ab6bab · Dealt.cs 055f7437 · SmokeTest.cs.txt
  510f58d9 · Shots.cs.txt ba103b7f
### kits6a-J7 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase.
- Built: DD row (hull 395, turn 1.2 / radius 95, 2 x 11.25 / 0.5 s, 700 u, 760 u/s, main_turn 2.09, Rows director_lead
  1.2 + lance_damage 300 / lance_speed 170 / lance_range 3000 / lance_cooldown 18; Fit Guns | Pd; Weapons Main, Lance,
  Pd; kit parts Need lance_damage, dd_missile_rack renamed "Torpedo Tube", id kept). PlayerShip.Directed (owner-side
  lead, 8 fixed-point passes on Missiles.Predict; AimPoint rides the state report). AbilityDef.Bow + BowShot +
  PlayerShip.FireAlong; Ab.Lance (F); Shots row 4 "missile" -> "lance" (A6a-12, unguided, Sweep 6); Dps.Lance. A refit
  onto a hull without Ab.FireMode clears Staggered (a BB left staggered came up as a DD with no key to undo it).
  DELETED: Fit.Missiles, Ab.Missile / Ab.Reload, MissilesLoaded / Reloading / StartReload / BurstSides / FireMissile,
  Stats Missile group / MissileDps / Dps.Missiles, LaunchTorpedo's heavy flag (callers: PlayerShip hunters,
  TargetDummy.cs -- outside the PRE's list, a caller fix -- and two harness lines). Items @output lance_damage.
- A6a-12 (new decision): the Lance is Shots row 4 renamed in place, not an appended row: the missile burst was its one
  user and leaves; the wire index stays. The four v2 gear ids (dd_salvo / buster / seeker / loader) do not exist in
  this tree's item table (Items.cs lines are category stems): owed to lane I (items), not built.
- Checks NEW: LaneA6aDirectorChecks (rows; aim within 2 u of the 760 u/s intercept at {0, 100-150, 250-300} u/s x
  {150-300, 600-690, 800-835} u, cursor at 845-900 u / nothing selected / the BB; a BB left staggered comes up a DD
  firing salvoes; 3 live runs on a crossing gunship: selected >= 6 of 10 shells land, cursor-aimed <= 2),
  LaneA6aLanceChecks (rows, learn order; 3 runs: one unguided Lance on the heading at 170 +- 3, exactly 300 credited
  "lance", 18.0 s, COOLING, no second), LaneA6aLanceHitChecks (3 runs, PD reach off: a seeker on the line passed, a
  webifier takes 300, the dummy behind 0). Frames REWRITTEN: 43_lance_away, 43b_lance_closeup,
  23b_bar_destroyer_lance_cooling, 23c_bar_destroyer_lance_refused (were 43_missile_burst, 43b_missile_closeup, 23b/23c
  magazine / reload). The sweep: witness "lance" (missile / reload gone), fitRows (Guns -> guns; Fit.Missiles row gone;
  firemode never without guns), reach case "lance_range" (missile_range gone).
- Checks REWRITTEN (6.3): the DD solo block (bar guns / lance / warp, 395 hull, turn 1.2 / 95, the carrier's radius 140
  -- J4 had missed it --, 45 + 16.67 + 2 DPS, the Lance on F, Salvo Core T1 300 -> 375, the kit tube back to 300);
  LaneA4TargetsChecks lends Ab.Lance TakesTargets (was Ab.Reload); the weapon-rows list and the walls' kit; the BB
  total without MissileDps; EveryDamageStat (lance_damage for missile_damage); the Shots table (Lance heavy, unguided,
  not interceptable); rung 5: the arena guest's DD 395 x 1.16 = 458.2 with the Lance past 300 (was 290 / 6 bursts),
  the third player's BB 500 x 1.08 = 540 (was 324, 5 lines), the guest carrier 425 x 1.24 = 527 (was 248) -- hull
  literals J1 / J4 had left.
- Owed at rung 3: all three checks + the DD block; rung 4: the four DD frames; rung 5: the 458.2 / 540 / 527 lines.
- Next: kits6a-J8 (Suppress). The DD's ability 2 and 3 walls log PEND until J8 / J9.

## Handover 3: agent 3 stops after kits6a-J7 (context past ~150k), at a job boundary
- Done J7 (87c85dc). Next: J8 Suppress, J9 Grapnel, J10 Record. No COORDINATOR NOTE seen.
- J8 plan worked out (not started, nothing edited): Ab.Suppress (Q) = a timed row via RunFor (Time "suppress_window" 6,
  Cooldown "suppress_cooldown" 20 from the press) whose OnDealt calls a NEW generic PlayerShip.Afflict(t, Status.Suppressed,
  "suppress_time", Fx.Chevron) only when the weapon id == Shots.Of(Shots.Shell).Id ("shell"; the Lance's is "lance", PD's
  Dealt.Pd). Afflict: host only, t is IStatused, ApplyStatus (Raider / Emplacement / Boss already check Reaches and read
  StatusSet.OutGuards: Gun 0.5, Move 0.7, Super 1.0, HoldsThrow), and raise the mark only when the status was new or had
  < suppress_time - 0.5 left (one raise, not one a shell). The chevron: FxShape.Chevron + Fx const Chevron = 16 + an
  Fx.All row appended at the END (grey, Life 3.0, Cap 1: a re-raise replaces it) + Fx.Mark(id, IHittable) raising
  {At 0, Size HitRadius, Anchor NetId} (Hub.AddFx parents it to the anchor); draw with DrawSetTransform(-GlobalRotation)
  a grey chevron HitRadius + 12 u above the hull. DD Rows suppress_window 6 / suppress_time 3 / suppress_cooldown 20; DD
  Abilities Guns, Lance, Suppress; Items @duration + suppress_window. Checks planned: LaneA6aSuppressChecks (3 targets:
  Has at 2.9 s / not at 3.1 s after the last shell hit, target.Statuses.Out(1, Gun) 0.5 then 1.0, 20.0 s, COOLING),
  LaneA6aSuppressWeaponChecks (Lance / PD / shell on a webifier: only the shell; a shell at 6.1 s applies nothing;
  applied twice still x0.5), the literal table in one place (StatusSet with Suppressed: 1.0/1.25/9/18 x0.5, 3.6/45/6
  x0.7, 50/250 x1.0), frame LaneA6aSuppressFrames (85d_dd_suppressed: the chevron), witness "suppress" in the sweep.
- Harness notes: python edit scripts written to the scratchpad file first (bash heredocs holding long C# broke); every
  tracked file is LF. typecheck ~20 s; verify -Quick > 2 min (run it with a 600 s timeout).

### kits6a-J8 · PRE · Suppressing fire (A6a-8) -- tier opus (agent 4; no COORDINATOR NOTE in the tail)
- Intent: Ab.Suppress (Q) = a timed row (RunFor: suppress_window 6, suppress_cooldown 20 from the press) whose OnDealt
  hands every hostile a SHELL hits (weapon id Shots.Of(Shots.Shell).Id; the Lance "lance" and PD "pd" differ) to a new
  generic PlayerShip.Afflict(target, Status.Suppressed, "suppress_time", Fx.Chevron): host only, IStatused only, the
  status via ApplyStatus (Reaches / OutGuards unchanged), the mark raised only when the status is new or has run down
  0.5 s. Fx: FxShape.Chevron, Fx.Chevron = 16 appended at the END, row "chevron" (grey, Life 3, Cap 1), Fx.Mark(id, h)
  raising on the hull's NetId; drawn upright HitRadius + 12 u above the hull. DD Rows suppress_window / suppress_time /
  suppress_cooldown; DD Abilities Guns, Lance, Suppress; Items @duration + suppress_window.
  Checks NEW: LaneA6aSuppressChecks, LaneA6aSuppressWeaponChecks, LaneA6aSuppressGunChecks, frame LaneA6aSuppressFrames
  (85d_dd_suppressed), rung 5 LaneA6aSuppressGuestChecks; the sweep's witness "suppress".
- Files: scripts/Abilities.cs, Ships.cs, PlayerShip.cs, Fx.cs, Items.cs, tools/smoketest/SmokeTest.cs.txt,
  tools/screens/Shots.cs.txt.
- HEAD 7ae29dd8e7fd9ea3993c28576e3a9f9b483bea98 · Abilities.cs 4d6035da · Ships.cs d40e1d14 · PlayerShip.cs 14d5308d ·
  Fx.cs a25d422d · Items.cs e111996e · SmokeTest.cs.txt ec3a7496 · Shots.cs.txt 71b74484
