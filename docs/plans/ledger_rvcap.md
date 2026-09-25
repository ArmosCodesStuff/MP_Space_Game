# ledger_rvcap — Fable review fixes, scope cap (cap-R1..R6)

## PRE rvcap-1
- tier: opus high; intent: apply review_tasks.json cap-R1..cap-R6 (one code fix, checks added/strengthened)
- files: scripts/PlayerShip.cs, tools/smoketest/SmokeTest.cs.txt
- HEAD 1cd733ef758e80eaa84af974c37d394ae4759a96
- PlayerShip.cs 097c434df09c3a96ff76f1cf28fd4745791a54a3; SmokeTest.cs.txt 6d35985a0fb27c294a42de8f2ec1a210ad1d0293

## POST rvcap-1 (commits dd361e3, a4e73ba; typecheck 0 errors, quick green x2)
- cap-R1 applied: PlayerShip.HookWatch rips on `_drive.Remote` too; LaneA6aGrapnelGuestChecks gains the warp-charge block (waits out the HOST's 16 s cooldown: the host re-checks Refuse, so a guest cannot zero it). six: red, not provable -- the guest's abilities are never marked by the host on this tree (the existing grapnel/suppress/pepper guest checks red too).
- cap-R2 applied: Cool = 0 before each press; F8 picks 1/2/3 PASS on seeds 11400714819323326533 and 1.
- cap-R3 applied: new LaneA6aSuperWarpChecks (after LaneA6aSuperChecks), counts only its own press's craft, 0.5 s hold on the circle; 3/3 PASS on both seeds.
- cap-R4 applied: selected-only block in LaneA6aGunshipChecks; PASS on both seeds.
- cap-R5 applied: speed = distance / clock; Lance run 0 reads 170.0 on both seeds; runs 1/2 red on both, pre-existing (2 Lances alive: an earlier run's round, count not touched by this fix).
- cap-R6 applied: new LaneA6aGrapnelHostChecks + LaneA6aGrapnelHostWatch (host role, started with the suppress watch, awaited after it); red at six, same root cause as R1 (hooked here False).
- Proof: rvcap_a (quick,solo: solo red 205, pre-existing), rvcap_b (quick,six: 241 fails, 4/6 runs finished), rvcap_c (solo seed 1: 203). Reds on version-l too: 3 FAIL LANE (FieldsRipYardChecks NRE, WingsGunshipChecks/ItemsDoorChecks ObjectDisposed), 4975 exceptions, warp distances +27 u, broadside 343.75, Supercarrier runs 1/2 and 20.4 s, patrol vs missile, guest abilities never marked by the host at six.
- CHANGES entry: the host now rips a guest destroyer's grapnel on its reported warp charge (was: at the slot's 5 s expiry); checks for the Supercarrier patrol through a warp, the gunships' selected-target path, a guest's rip damage on the host.
- next: merge after the guest-ability path (six) is fixed; re-prove R1/R6 at six then.
