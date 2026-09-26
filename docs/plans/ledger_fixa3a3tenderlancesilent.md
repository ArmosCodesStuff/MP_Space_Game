# Ledger: fix a3-tender-lance-silent (round a3, kind code)

Writer: one agent in `WarShips_wt_fix2`, branch `wt/fixa3a3tenderlancesilent`, cut from version-l 485652f.

## PRE (485652f)

- The tender's held lance lands no tick: LaneA6bLanceTriggerChecks runs 0-2 (first tick -1.00 s, line LANCE),
  overdrive 'the lance TICKS' (0 then 0 ticks), sweep TENDER LANCE. tp4ar3_0/2_solo.fails.txt:1173 seed 314566543.
- Reproduce at solo@314566543 first (tag fixa3a3tenderlancesilent_a).

## POST (44da017): lance ticks; code (refit keeps old gun reload) + 3 wrong checks rewritten

- Code: FitClass zeroes `_gunCd` (a refit fits the new gun loaded); the sweep's 3-frame press saw cd 0.117 from the freighter.
- Checks rewritten, not loosened: LaneA6bLanceTriggerChecks / LaneA6bFieldsHost held Space on a Demo ship (trigger never read);
  overdrive TICKS set AimPoint by hand (mouse overwrote it). Now AimWorld per frame. New LaneA6bLanceRefitChecks runs 0-2.
- Proved: solo@314566543 (116, round 117), solo@197420354 (115), six (157, round 160): every named check PASS; quick green.
- CHANGES entry: switching class fits the new class's main gun loaded (the tender's lance no longer waits out a battleship reload).
