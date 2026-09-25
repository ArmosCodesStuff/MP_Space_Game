# Ledger: class-kits batch, lane E (wings)

Writer: one agent in worktree `WarShips_wt_wings`, branch `wt/wings`, started at f168508.
Spec: `kits_v31.md` §3.2 (Carrier), §6 (F5, F13), §8 (lane E); `kits_v3.md` §3.1 + §5 (F13);
`kits_v2.md` CARRIER card (gunships) + §5. Build phase: no engine run; every check written now,
run in the final test phase. A PRE with no POST is an interrupted job (CLAUDE.md §5).

## Decisions taken where the spec is silent (defaults, each the smallest reading)

- **W1** The lane builds the MACHINERY, not the keys: `Wings.All` rows `gunship` (2) and `patrol`
  (3), the Orbit way, the ring pick, `PlayerShip.Sortie(kind, target)`, the stat rows. Lane A's
  slice 6a adds the Carrier's E (gunships, targets[]) and Q (Supercarrier) ability rows, their
  cooldowns (`gunship_cooldown` 25, `super_cooldown` 30) and the bar slots, and calls `Sortie`.
- **W2** Patrol and gunship lifetimes are wing stat rows (`patrol_time` 20, `gunship_time` 12), so
  a sortie is ONE call; 6a reads them for its slot line.
- **W3** Patrol target = best by (`Turret.Rank`, fallback last, then distance from the CARRIER's
  centre) among hostiles whose HULL EDGE is within `patrol_range` of the carrier's centre; held
  until it dies or leaves the ring, giving way only to a better RANK (a seeker arriving), the
  turret's held rule. The whole patrol shares no order: each craft picks by the same rule, so
  they converge. v3's "a webber first" is superseded by v3.1's "whatever Turret.Rank puts first".
- **W4** The `Patrol` TargetFilter row IS `Targeting.Sentry`'s contents (forbid nothing, Dummy as
  fallback): declared `Patrol = Sentry`, one truth, two names for two users.
- **W5** Gunships WARP: at launch they appear on their orbit round the target (the 3000 u leash is
  checked at launch only, kits_v31 §3.4 table), orbit at `gunship_orbit` 240 u, fire hitscan at
  10 DPS each (2.5 a shot / 0.25 s) while inside `gunship_range` 300 u, and at `gunship_time` or
  when the target dies they warp out (freed). 2 a target (`gunship_count`).
- **W6** A wing's hit is credited by its ROW's id through Dealt.Deal ("fighter", "patrol",
  "gunship"), the door's own rule; `Dealt.Fighter` is deleted.
- **W7** Wire: the wing report's state code carries the craft's row (`kind * 1000 + state`); a
  guest adds and drops SORTIE craft to match the host's report. No new RPC (v3 §3.1).
- **W8** Visible: the patrol's amber livery is the row's `Livery` (sprite modulate). The dashed
  600 u ring and the chevron are F9 (lane D) / 6a, not here.
- **W9** `patrol_range` joins the Carrier's Reach list (a Targeting Chip / REACH points lift it);
  the reach sweep gains its case and its totals 13 ids / 31 pairs become 14 / 32.

## Jobs (foundations first)

- **J1** rows: WingKind Gunship/Patrol, WingWay.Orbit, WingDef fields (Sortie/LifeStat, EngageStat,
  Picks, OrbitStat, Livery), `Targeting.Patrol`, `Turret.RankIn`, Stats rows, Reach, Dealt by row
  id. Checks: table literals; the reach sweep's patrol_range case.
- **J2** the machine: patrol (circle the carrier, ring pick, strafing passes, no rest, home and
  freed at the end), gunships (warp in, orbit, fire, warp out), `PlayerShip.Sortie`, bombers fitted
  before sortie craft. Checks: solo `WingsPatrolChecks`, `WingsGunshipChecks`.
- **J3** wire + look: kind in the state code, the guest's reconcile, the livery; checks: guest
  role `WingsGuestSees` / host `WingsHostSortie`, frame `wings_patrol` in Shots.cs.txt.
- **J4** docs/CHANGES.md Unreleased + Handoff; the final POST lists what the test phase owes.

## Log

JOB 0 (opus): spec read, job list above. HEAD f168508.

### J1 PRE (opus) -- rows AND the machine in one job (an Orbit way nothing flies is an unused member: rung 2)
Intent: J1+J2 of the list above as one commit (WingKind/WingWay members must have a game user).
Files @ HEAD 72bcef4: ShipClasses.cs d8bc3db7, Stats.cs fdaff349, Ships.cs c272c9bf,
Targeting.cs 226e78b5, Turrets.cs 2f876a91, Dealt.cs e3f8ce08, PlayerShip.cs 2a913835,
SmokeTest.cs.txt 9949bb60, Shots.cs.txt 20b45d75.
- **W10** gunship art borrows the bomber's airframe (wing_bomber.png, 32 u, steel livery) until
  lane H casts one; gunships leave 0.25 s apart (they warp; no deck), a row literal.
### J1 POST: typecheck 0 errors, quick ALL CHECKS PASSED. Engine-unproven: rungs owed in the final test phase.
Built: rows gunship (2, Orbit) / patrol (3, Strafe + Picks + OrbitStat), WingDef EngageStat/ToHull/
Picks/OrbitStat/LifeStat/Livery, `Wings.Reach/Within/Pick`, `Turret.RankIn`, `Targeting.Patrol`,
`PlayerShip.Sortie` (+ Done craft freed, bombers fitted before sorties), Gunships + Patrol stat
rows, `patrol_range` on the CV Reach list, wing hits credited by row id (`Dealt.Fighter` gone).
Checks written: WingsRowsChecks, WingsPatrolChecks, WingsGunshipChecks (solo, after
LaneAHeavyRowsChecks), reach sweep case `patrol_range` + totals 14 / 32 (rewritten).
Next: J2 (wire + livery frame).

### J2 PRE (opus) -- wire + look (was J3 in the list; J1 took J2's machine)
Intent: the wing report's state code carries the row (kind*1000 + state); a guest adds/drops
sortie craft to the host's report; guest-role check (host sends a patrol on the guest's carrier
and a seeker at it; the guest sees 2 x fighter_count craft and the seeker gone); frames
10e_carrier_patrol / 10f_carrier_gunships. Files @ HEAD (ledger commit follows f2a7106):
ShipClasses.cs 743245cb, PlayerShip.cs 4454da50, SmokeTest.cs.txt 5f27c473, Shots.cs.txt 20b45d75.
### J2 POST: typecheck 0 errors, quick ALL CHECKS PASSED. Engine-unproven: rungs owed in the final test phase.
Built: `Wing.RowCode` (state code = row * 1000 + state), `PlayerShip.MatchSorties` on the guest
(adds / drops sortie craft to the host's report), `DropWing` (RemoveWing uses it).
Checks written: guest role `WingsGuestWatch` (started beside pdWatch, awaited after the PD check),
host `WingsHostSortie(g)` (started after the guest-PD block, awaited before the host's 15 s stay);
frames `10e_carrier_patrol`, `10f_carrier_gunships` (`WingsFrames`, after 10d) in Shots.cs.txt.
Next: J3 docs.
