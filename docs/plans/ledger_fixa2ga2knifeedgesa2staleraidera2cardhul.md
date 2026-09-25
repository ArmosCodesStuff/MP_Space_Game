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
