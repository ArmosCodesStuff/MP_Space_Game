# Ledger: fix a1-arena-stall (round a1, kind code)

Writer: one agent in `WarShips_wt_fix1`, branch `wt/fixa1a1arenastall`, cut from version-l 66466b6.
Task: arena roles stall; from the guest's curtain on the host sees none of its presses; ahost/aguest never finish.
Evidence: tp4ar1_2/2_six.fails.txt:2071 (seed 11400714819323305746 run), tp4ar1_3:2069. Traces to kits6a Suppress/Grapnel.

## PRE 1 (66466b6): reproduce at six@11400714819323305746 (tag fa1as_r1), then find the root cause
Reproduced (fa1as_r1 six@11400714819323305746: fails 2042/2043/2071/2072, 4/6 runs). Root cause: the harness's
SetClass announced a guest's class mid-fight; the host kept the carrier (Hub.Refit, P8: its gunships hit the dummy,
InCombat) while the guest flew a destroyer, so every press after landed on another class. The curtain lane also
pressed E on the carrier (it leaned on the taunt lane's class). Fix: a guest's SetClass waits for Hub.MayRefit.
fa1as_p1 (six@seed): curtain, suppress, grapnel green both sides; the guest then waited 80+ s in the pepper refit:
hauled 250 u off TargetDummy1, its PD had the practice fighters in reach (combat for good). The grapnel lane now
parks it at BasePos+(2600,2400) before returning.
