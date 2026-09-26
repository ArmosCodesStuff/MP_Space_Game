# Ledger: fix group a2-g-a2knifeedges-a2staleraider-a2cardhul (round a2, kind check, 4 tasks)

Writer: one agent in `WarShips_wt_fix3`, branch `wt/fixa2ga2knifeedgesa2staleraidera2cardhul`, cut from version-l 0742408.
Tasks: a2-knife-edges (2 checks), a2-stale-raider, a2-card-hull. One commit per task.

## PRE (0742408)

- a2-knife-edges: a ramp row check asserts +0.150 exactly and a mortar check asserts 1.4 s exactly,
  both sampled at a frame boundary that lands exactly on the literal -- a knife edge. Evidence
  tp4ar1_0/2_solo.fails.txt 'a ramp row ... +0.148 (0.150)' and 'mortar ... 1.38 s (1.4)' seed
  11400714819323392986. expect: 0.150 and 1.4 s sampled off the frame boundary. Traces to kits6a
  ramp/mortar. Files tools/smoketest/SmokeTest.cs.txt.
- a2-stale-raider: `FieldsRipYard` follow-on lanes (:977, :1345) hold a raider reference fetched
  before a wait, then call `Node2D.GetPosition` on it after the wait -- the raider is freed in the
  meantime -> ObjectDisposedException. Evidence tp4ar1_1/2_solo.fails.txt:977,1345 seed
  11400714819323333590. expect: the raider re-taken after the wait. Traces to kits6a raider. Files
  tools/smoketest/SmokeTest.cs.txt.
- a2-card-hull: a class card print check asserts hull 300 (stale), Ships.cs:176 now sets hull 500.
  Evidence tp4ar1_0/2_solo.fails.txt 'a class card prints ... (500' asserts 300, seed
  11400714819323392986. expect: 500 (Ships.cs:176). Traces to kits6a class card. Files
  tools/smoketest/SmokeTest.cs.txt.

## POST a2-knife-edges

LaneARampWiringChecks now waits until the clock has strictly passed 1.5 s (not a fixed 90-frame
count that can land short), so its accumulated lift always sits past the 0.150 mark; tolerance
3e-3 to cover the one-frame overshoot. `asserted`: 0.150 (LaneARampWiringChecks). The mortar's
"1.38 s (1.4)" quoted in this task's evidence was already inside LaneA6bMortarChecks' 0.05 s
tolerance in the unmodified check -- nothing there was a frame-boundary literal to fix. Its
"placed" sub-condition (a separate throw-accuracy gap, tens of units at the 400-900 u unclamped
band) is a pre-existing red a tolerance change cannot honestly absorb without loosening what the
check proves; left untouched, kind code, out of this task's scope. Proved: `typecheck.ps1` 0
errors; `verify.ps1 -Quick` OK; PASS at seed 11400714819323392986 and fresh seed 209199496.
Checks: LaneARampWiringChecks.

## POST a2-stale-raider

WingsGunshipChecks and ItemsDoorChecks's web-breaker loop each killed a raider with
TakeDamage(1e12), then several frames later built the Check message off that same freed
raider's `.Position` -- ObjectDisposedException at Node2D.GetPosition, failing the whole lane
(`FAIL LANE ... threw`). Both now capture the distance once, right after spawning and before
the kill, and the message reads that captured value. `asserted`: the raider re-taken (captured)
before the kill, never read live after. Proved: `typecheck.ps1` 0 errors; `verify.ps1 -Quick`
OK; PASS at seed 11400714819323333590 (its own evidence seed), seed 11400714819323392986 and
fresh seed 209199496. Checks: WingsGunshipChecks, ItemsDoorChecks.

## POST a2-card-hull

The class card check now asserts hull 500 (Ships.cs:176), not the stale pre-buff 300; the
comment above it updated to match. `asserted`: 500. Proved: `typecheck.ps1` 0 errors;
`verify.ps1 -Quick` OK; PASS at seed 11400714819323392986 and fresh seed 209199496. Checks: a
class card prints the hull the ship will actually have.
