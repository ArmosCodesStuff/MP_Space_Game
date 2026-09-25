# Ledger: fix a1-arena-stall (round a1, kind code)

Writer: one agent in `WarShips_wt_fix1`, branch `wt/fixa1a1arenastall`, cut from version-l 66466b6.
Task: arena roles stall; from the guest's curtain on the host sees none of its presses; ahost/aguest never finish.
Evidence: tp4ar1_2/2_six.fails.txt:2071 (seed 11400714819323305746 run), tp4ar1_3:2069. Traces to kits6a Suppress/Grapnel.

## PRE 1 (66466b6): reproduce at six@11400714819323305746 (tag fa1as_r1), then find the root cause
