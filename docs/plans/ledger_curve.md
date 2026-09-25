# Ledger: lane F, the curve

Writer: one agent (opus) in worktree `WarShips_wt_curve`, branch `wt/curve`, started at f168508.
Spec: `numbers_curve_raids_items.md` (§8 first; it owns every curve, boss, raid number),
`progression_curve.md` (approved defaults; numbers superseded), `kits_v31.md` §4-5, §8 lane F row,
`models/numbers_v2.py`. BUILD PHASE: no engine run; per job typecheck + verify -Quick + own diff read.
A PRE with no POST is an interrupted job: compare hashes, revert half-made edits, redo it first.

## Decisions taken where the spec is silent (defaults; each also in the lane's return)

- **D1 Par is a table of the model's per-level rows, not a live re-derivation.** The model's Par reads the
  item pass's `Tiers` / `Loot` rows, which are not built (lane I). §4 "Order" allows it: Par.cs holds
  `numbers_v2.py`'s reference pilot at L1-80 (its hull H, its DPS against craft Dc, the Lancer's 60 s hull
  Boss) and every scale is a ratio of two rows. Lane I re-runs the model on its rows and re-literals.
  Fractional levels (a raider's threat) interpolate; past L80 flat.
- **D2** Salvage levels keyed by CORE SLOT name (`Weapon`, `Engines`, `Shield`, `Hull`, `Utility`) in the
  same `Character.GearLevel` dictionary: the identity wire keeps its shape (ids + levels). Chip slots
  take no level (§8 R7). Old part-id keys on disk are dropped (saves disregarded, owner ruling).
- **D3** The ladder's cap is min(40, highest level cleared on EITHER ladder + 1), checked at purchase.
- **D4** Siege: base hull = the Lancer's row (1.0x, 3222), pylon 0.125x (403); both x HullScale.
  Credits follow HullScale. Siege `Pay 2` / `Crates 2` (progression_curve §2.4) are reward rows not named
  by this lane: left for the owner/coordinator (open).
- **D5** Skip: `Missions.SkipAhead = 2`; the TIO's arrow and Hub's two clamps stop at Unlocked + 2.
- **D6** Boss escorts (lane G deletes them): only their scale reads change (Strength = the level).

## Jobs (foundations first)

- J1 Par.cs (the table + queries) and its literal proof (harness `CurveParChecks`).
- J2 Scale switch: Missions (S, LevelStep deleted; HullMult/DamageMult/BountyEach via Par; boss rows
  3222/2968), Boss escort scale line, Emplacements siege rows, Waves (Strength a level; ThreatStrength
  deleted; Standing via Par.LevelOfHull), Raider Strength as a level (one hunk), TioWindow text; harness
  old-truth rewrites.
- J3 Lancer / Drake move rows cut (x0.744 / x0.787; burn and rock kept); harness rewrites + new check.
- J4 Level skipping +2 (Missions.SkipAhead, TioWindow, Hub clamps); checks.
- J5 Salvage ladder on the slot: 500 x 1.10^n, +3% a level, cap; Equipment, Character, Yard,
  EquipmentWindow, RecyclerPanel; harness rewrites.
- J6 CHANGES.md Unreleased + Handoff; final POST with what the test phase owes.

## J1 PRE — tier opus
Intent: Par.cs, the reference pilot's rows (numbers_v2.py, L1-80) and HullScale / CraftScale /
DamageScale / LevelAtHull; the literal proof CurveParChecks. Files: scripts/Par.cs (new),
tools/smoketest/SmokeTest.cs.txt. HEAD a02fc10. Hashes: SmokeTest 9949bb6009a1bf028fa1a6c78bb9facb92693c3a.
## J1 POST
Verdict: typecheck 0 errors, quick ALL CHECKS PASSED. Files: scripts/Par.cs, SmokeTest.cs.txt
(CurveParChecks, called after BossScaleChecks). engine-unproven: rungs owed in the final test phase.
Next: J2.

## J2 PRE — tier opus
Intent: the scale switch (Missions.S/LevelStep deleted; HullMult/DamageMult/BountyEach via Par; boss
rows 3222/2968; siege base 1.0x / pylon 0.125x the Lancer row; Waves + Raider Strength a level;
ThreatStrength deleted, Standing via Par.LevelAtHull; TIO text); harness old-truth rewrites.
HEAD aad83ea. Hashes:
  scripts/Missions.cs e1afdde80028cfa002f541da418c2d1353546c7f
  scripts/Boss.cs 85c742d66f5a2b475ea3bc1ace5a3ee6b0b053d8
  scripts/Emplacements.cs deab186ba364edf207f5c6949b6267beedda37aa
  scripts/Waves.cs 728a92f569938cb4bb75c6451b21fa2635b72a6e
  scripts/Raider.cs ae7dd1a11793580c1cb8974cd962308cd66898e7
  scripts/TioWindow.cs f104c80d32a526e20887e75bc50c2e3b0f6b9b78
  tools/smoketest/SmokeTest.cs.txt a5a3852e3367a82dd173b44a59d8c3fc3ad081e3
## J2 POST
Verdict: typecheck 0 errors, quick ALL CHECKS PASSED; diff read. Files: Missions, Boss (2 escort lines +
comment), Emplacements, Waves, Raider (Strength comment, MaxHull, new Volley used by both Strikes),
TioWindow, SmokeTest. engine-unproven: rungs owed in the final test phase (solo x2: the rewritten boss,
raid, siege, escort and TIO checks; six: the guest bounty share 1500 x HullScale(2); frame 35 TIO text).
Next: J3.

## J3 PRE — tier opus
Intent: Lancer rows guns 2.68, trident 11.2, wave 33.5, ram 29.8 (beam 50 kept); Drake gun 4.72, scrap
14.76 (rock 250 kept); harness rewrites + CurveBossRowChecks. HEAD 519954a. Hashes:
  scripts/Lancer.cs 08ac04023b42b8bdf60310057cf5e3f62dd23381
  scripts/Drake.cs 15db8b36bfee548da969d8b6228cdf2aed7d9ebd
  tools/smoketest/SmokeTest.cs.txt ea7ae1ed4d662509016ea35c7a52f21e89d1237c
## J3 POST
Verdict: typecheck 0 errors, quick ALL CHECKS PASSED; diff read. Files: Lancer.cs, Drake.cs (rows + headers),
SmokeTest (CurveBossRowChecks new; row literals, L1 guns DPS, ram 29.8, shockwave 33.5 / Suppressed 23.45,
Drake L2 gun 4.72 / scrap 14.76 rewritten). engine-unproven: rungs owed in the final test phase (solo x2).
Next: J4.
