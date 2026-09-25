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
