# ledger_fixa3a3broadsidearcs (round a3, kind code -> check)

## PRE
- Task a3-broadside-arcs: 'every shell lands' reads 343.75 not 375; 'guns 25 DPS ... 52.27' red.
- Reproduced: fixa3a3broadsidearcs_a solo@314566543, FAIL 114, both reds (lines 371, 378).
- Not the arcs: all 6 volleys fire x4 (PASS); abeam every mount bears (card: 0-150 / 30-180).
- Cause 1: dummy 600 u abeam; the far pair's last-volley shells need ~555 u, the check reads after ~541 u (0.83 s).
- Cause 2: 25 + 375/13.75 = 52.2727; `Math.Abs(bsTotal - 52.27) < 0.001` can never pass (the sheet is right).
- Fix: the checks, never loosened: read once the broadside's shells are gone; assert the exact sum, rounded 52.27.

## POST a3-broadside-arcs done (the checks were wrong; game and card agree on 375)
- SmokeTest broadside: reads the dummy once every broadside shell is down (was a fixed 0.6 s); still asserts 375.
- Sheet check: asserts 25 + 375/13.75 exactly and 52.27 rounded (was |x - 52.27| < 0.001, never true).
- Proved: _p quick green, solo@314566543 FAIL 112 (was 114), both PASS; _q solo@2043465805 FAIL 114, both PASS.
- Seen: never walled, squad tracker jump, second F COOLING, Magazine Core tiers, pending invites (other lanes).
