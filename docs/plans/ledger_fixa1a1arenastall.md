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
fa1as_p2: the same three host reds, and no guest line after grapnel: run.ps1 kills aguest at 150 s and its lanes
through grapnel alone take 136 s (the list grew in the build phase, never run). Limits: aguest 480 (wan 600), ahost 240.
fa1as_p3 (six@seed): 6/6 runs finished; curtain, suppress, grapnel, pepper/rod/ramjet/sling/slip/rewind/emp/step
guest lanes green. Pepper host counted 1159 darts: a steered dart left the set and was counted again; fixed.

## POST 1 (c469da2): stall fixed; six@11400714819323305746 still red on other lanes (fa1as_p4, quick green)
Green there, 6/6 runs: Suppress/Grapnel host+guest, CurtainGuest, every LaneA6d*GuestChecks, Pepper/Rod/Ramjet host.
Red, newly exposed (the 150 s kill hid them): fa1as_p4/2_six_11400714819323305746.fails.txt:2050 SlipGuest 9.93 (10 +- 0.01:
guest regen between reports, kind check); 2056-2116 guest freighter/bubble/lance/gank/EXP/reconnect; 2146-2176 host.
Fresh-seed six not run (4-proof budget). CHANGES: arena guests refit only once Hub.MayRefit; aguest limit 480 s.
