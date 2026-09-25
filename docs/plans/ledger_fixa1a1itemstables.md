# Ledger: fix a1-items-tables (round a1, kind code; traces to lane I items)

Tree WarShips_wt_fix2, branch wt/fixa1a1itemstables from version-l 66466b6. Evidence tp4ar1_0/2_solo.fails.txt:183,581,585,587,588,589 (all 4 seeds).
Repro chain: quick,solo@11400714819323392986 (tag fixa1items_repro).

## Log

PRE 1: RowsOf names every line's condition rows (Spin-up Feed is priced, not Conditional); checks: lean with rider, caps count per rider key, floor 88 (F22), kit count 22, fingerprint gear on chip_combat_t1. HEAD 66466b6; Items.cs 7d9f2b8f; SmokeTest.cs.txt d7fae328

## POST 1
Verdict: done at 52f1473. Items.RowsOf names every fitting line's condition rows (spinup_damage on the 3 freighters); 5 stale checks rewritten (rider lean, per-key +n, floor 88, kits 22, gear chip_combat_t1).
Proved: repro red at solo@11400714819323392986 (41 fails) -> 35, the 6 verdicts PASS, no new FAIL; solo seeds 1327702642 and 764922009: all 7 item verdicts PASS.
Checks: ItemsTableChecks, ItemsDoorChecks (spin-up row), build fingerprint gear.
Owed (not this task): FieldsRipYardChecks NRE, WingsGunshipChecks/ItemsDoorChecks ObjectDisposed, class card 500 -- same on baseline tp4ar1_1.
