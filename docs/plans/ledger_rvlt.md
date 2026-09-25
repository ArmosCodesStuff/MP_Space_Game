# ledger_rvlt — Fable review fixes, scope lt (lt-R6d-1..6)

## PRE J1 (review fixes lt-R6d-1..6), tier opus
- intent: apply review_tasks.json entries lt-R6d-1..6 (SkipYaw -> timed window; new/extended checks)
- files: scripts/PlayerShip.cs, scripts/Abilities.cs, tools/smoketest/SmokeTest.cs.txt
- HEAD 1cd733ef758e80eaa84af974c37d394ae4759a96
- hashes: PlayerShip.cs 097c434d, Abilities.cs 98463948, SmokeTest.cs.txt 6d35985a

## POST J1 — commit 1a3d7d5; typecheck 0 errors, verify -Quick ALL CHECKS PASSED
- lt-R6d-1 applied: SkipYaw bool -> SkipYawFor(0.4)/_skipYawUntil (PlayerShip Slung + Stepped, comments, Abilities.cs); NEW LaneA6dSlingHostWatch/HostChecks/GuestChecks (rung 5, after the ramjet pair; guest waits the 20 s ramjet cooldown).
- lt-R6d-2 applied: LaneA6dSlipChecks boost block (3 runs: 10 flat out, past 325 within 0.4 s of the boost showing, then 7.0); NEW LaneA6dSlipHostWatch/HostChecks/GuestChecks (rung 5, after the sling pair: 10 at rest, 7.0 boosted by the report).
- lt-R6d-3 applied, premise corrected: Combat Chip lifts @damage = pepper_damage, so (a)'s dart is 8.1 (7.5 x 1.08, x1 floor), not 7.5; rod 328.43, (b) 422.5/11.25/360, (c) t10 top < 249.6 and rod = Price(180, top x 1.5 + 100). NEW LaneA6dPepperGearChecks after LaneA6dRodRecoilChecks.
- lt-R6d-4 applied: LaneA6dRodChecks webbed block (3 runs, rudder held on one): pinned, <= 75 u/s, heading frozen, rod at 3.0 s down the nose, 249.23.
- lt-R6d-5 NOT applied: false premise. Squads.cs header says the heavy's wait at the map's edge (EdgeSpot) was REPLACED, and a solo check asserts "a lone heavy never waits at the edge"; nothing sends a standoff heavy to the edge when a web drops. Card echo.md vs code needs an owner ruling (build the retreat, or strike it from the card).
- lt-R6d-6 applied: ItemsReconcileChecks Want rod_damage 225, venom_dps 1.5625 (T1) and 1.25 x (1 + 0.25 P(10)) (T10); NEW ItemsVenomLiftChecks after ItemsAnchorLiftChecks (ticks 6.25 / 7.8125 / T10).
- engine: rvlt_a2 quick green; solo red 41 FAIL + 1 throw (ObjectDisposedException) before any lane this job touched; the same reds (never walled, warp 827/2427, battleship 343.75, ...) are on version-l ae8ae99 (tpslots_0/1). six not reached. New checks unproven in the engine.
- next: re-prove quick,solo,six once version-l's solo throw is fixed.
