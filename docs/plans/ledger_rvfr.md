# ledger_rvfr — Fable review fixes fr-R6b-1..8

## PRE rvfr-1 (opus, review fixes)
- intent: apply review_tasks.json fr-R6b-1..8 (checks + code fixes), then quick, then rungs quick,solo,six (tag rvfr_a)
- head: 1cd733ef758e80eaa84af974c37d394ae4759a96
- files: scripts/Abilities.cs 98463948b56b01d00e657d4bcdc4f90ac51d2918 · scripts/PlayerShip.cs 097c434df09c3a96ff76f1cf28fd4745791a54a3 · tools/smoketest/SmokeTest.cs.txt 6d35985a0fb27c294a42de8f2ec1a210ad1d0293 · docs/plans/cards/freighter.md fca322db7ca05b072ea47ccee57f6429a38fbfa2

## POST rvfr-1 (commits 977fd71, c92ebde, f45257a)
- fr-R6b-1 applied: Ab.Buster/Deploy/Well TakesPoint; FireBuster reads Sl("buster").At, Deploy presses at Sl("deploy").At, Lay reads Sl(id).At for a TakesPoint row. Checks: arena guest first throw (cursor held elsewhere, jumps to the point for R, moves 300+ u the frame after; lands on the press point) + arena host t1 >250 u from the guest's AimPoint; NEW pair LaneA6bPointHost/LaneA6bPointGuest (bastion buster + well, same moving-cursor pattern).
- fr-R6b-2 applied: CountFlights() keeps Sl("deploy").N = throws + recalls in flight (DeployTurret, recall, Redeploy, TickThrows); TurretsOut reads N off-host. Check: arena guest LateSampler sees TurretsOut 1 with none standing. NOT asserted: Show "2/3" -- while the throw flies the deploy clock is running, so Show prints the clock (false premise in the entry's literal).
- fr-R6b-3 applied (the card is binding): _home list, the recall flies deploy_flight home, still out, then stowed. Checks: LaneASentryThrowChecks recall block (3 out at once, ALL OUT meanwhile, 2 out 0.8+-0.05 s from the press, hull kept); solo freighter recall (home 0.8 s later); arena guest recall waits TurretsOut 1.
- fr-R6b-4 applied: LaneA6bFieldsHost holds the lance 2.5 s on a gunship (BURN, length within its radius); LaneA6bFieldsGuest sawBeam.
- fr-R6b-5 applied: LaneASentryPreferChecks 4th situation (painted 700-780 u keeps the seeker; 560-620 u takes it in 2 frames).
- fr-R6b-6 applied, numbers differ from the entry: an on-spotter-line sentry's line also crosses the light (onCross 2), so instead two own sentries + a battleship pilot on one ray 52-69 deg off (outer's line over inner and the pilot): lines ks+3, credited 40(ks+4). Also fixed a latent knife edge the new draws exposed: in-reach sentries now 0.4-1.3 rad off the hull bearing; ray sentries dropped after the in-flight throw (3-out cap).
- fr-R6b-7 applied: second tender mate (seat 4): its resupply untouched, overdrive -8; NOTHING COOLING with only its resupply cooling.
- fr-R6b-8 applied: (a) LaneA6bWellChecks: Q throws a latched webber off (unlatched next frame, 900+ u, its Cc gone within 0.3 s; the entry's 0.2 s is shorter than the webber's 0.25 s touch). (b) LaneA6bSiegeChecks: rip (1% max + 10 via Dealt.Deal "grapnel") + buster = 360 + rip, 3 pylons.
- proof: quick green x3. solo (seeds rvfr_a, 2 x2): every check above PASS; solo red is version-l's own (~206 FAIL, 3 FAIL LANE: FieldsRipYardChecks, WingsGunshipChecks, ItemsDoorChecks; rvcap_a/rvhv_a same). The siege section (LaneA6bSiegeChecks: 8b) is not reached in solo on this baseline -- unproven. six: see rvfr_d.
- CHANGES entry: Buster, Sentry and Gravity well land where the key went down (the press carries the point); a guest's sentry count includes throws in flight; a recalled sentry flies 0.8 s home before it can be thrown again.
- lesson: never mix a script write and the Edit tool on one file (the Edit tool rewrote a stale copy and dropped three edits; redone in c92ebde).
