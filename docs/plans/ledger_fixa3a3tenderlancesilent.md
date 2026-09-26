# Ledger: fix a3-tender-lance-silent (round a3, kind code)

Writer: one agent in `WarShips_wt_fix2`, branch `wt/fixa3a3tenderlancesilent`, cut from version-l 485652f.

## PRE (485652f)

- The tender's held lance lands no tick: LaneA6bLanceTriggerChecks runs 0-2 (first tick -1.00 s, line LANCE),
  overdrive 'the lance TICKS' (0 then 0 ticks), sweep TENDER LANCE. tp4ar3_0/2_solo.fails.txt:1173 seed 314566543.
- Reproduce at solo@314566543 first (tag fixa3a3tenderlancesilent_a).
