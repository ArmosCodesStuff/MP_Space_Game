# Ledger: fix group a2-g-a2bombersarmed-a2supercarrierspacin (round a2, kind code, 4 tasks)

Writer: one agent in `WarShips_wt_fix1`, branch `wt/fixa2ga2bombersarmeda2supercarrierspacin`, cut from version-l 79def73.
Tasks: a2-bombers-armed, a2-supercarrier-spacing, a2-pd-release, a2-anchor-warp-end. One commit per task.

## PRE (79def73)

Reproduced all four at solo@11400714819323392986 (tag ..._a2, FAIL 186): lines 469, 1314-1315, 1397-1399, 1855-1875.
- a2-bombers-armed: the check refits with `SetClass`, which is a class change (FitClass frees and rebuilds the
  whole wing, fresh = armed); the in-service refit is `Restat` (FitWings fresh:false). Lane I (3e57839) swapped
  the original `cl[4]` part + PilotChanged for SetClass. Check wrong, code right (ShipClasses.StartRearm comment).
- a2-supercarrier-spacing: kits_v3 §3.1 "At 20 s they fly home and dock"; the code does. The check assumes the
  patrol is gone at 20.4 s and 0.5 s after SendOut, so run 1/2 count the last run's homing craft at 0.03 s.
- a2-anchor-warp-end: mid-pull/mid-swing read `0.000 s on`, moved 0.0: the Wait (a timer, resumed after the
  frame's ships fly) then one Frame (resumed before they fly) holds no hull tick. Check timing, not HelmMoves.
- a2-pd-release: Turret.Tick drops at Range*1.15 and Acquire skips past Range; static read finds no hold. PDDIAG run.

## POST a2-bombers-armed

Kind check, not code: the check refitted through `SetClass` (a class change: the wing rebuilt fresh, armed). It now
runs the in-service refit, `PlayerShip.Restat`, as a part or purchase does; asserts the same literal "still 2 ready
of 5". Proved solo@11400714819323392986 (tag ..._d): PASS (2 -> 2). Checks: the carrier wing block's rearm check.

## POST a2-supercarrier-spacing

Kind check: kits_v3 §3.1 "At 20 s they fly home and dock", as the code does. `LaneA6aSuperChecks` now waits the
patrol home between runs (so a run counts only its own craft) and asserts at the end: 3 still out at 19.7 s, all
docked by 26 s, cooling 29.4-30, Q COOLING. solo@11400714819323392986 (tag ..._d): runs 0-2 0.03/0.85/1.68 PASS;
the last docked at 22.7 s. Checks: LaneA6aSuperChecks (runs 0-2, the 20.4 s end).
