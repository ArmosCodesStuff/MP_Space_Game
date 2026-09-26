# ledger_fixa3a3suppresslocked (round a3, kind code)

## PRE
- Task a3-suppress-locked: WallChecksLive check 4, the level-3 press acts but FailNote still says LOCKED · L3.
- Reproduced: fixa3a3suppresslocked_a solo@314566543, FAIL 117 (Destroyer, HeavyWarden, LightEcho at level 3).
- Cause: PlayerShip.Fail keeps a note 1.5 s (FailShow); a press 0.4 s later that passes the wall never cleared it.
- Fix: UseAbility drops the id's note once past the level wall; any later refusal sets its own.

## POST a3-suppress-locked done
- PlayerShip.UseAbility: `_fails.Remove(id)` once a press is past the level wall.
- Proved: fixa3a3suppresslocked_p quick green, solo@314566543 FAIL 114 (was 117), all 3 level-3 acts PASS.
- Fresh seed: fixa3a3suppresslocked_q solo@2010100316 FAIL 114, all 3 PASS; round solo 127.
- Checks: WallChecksLive check 4 (unchanged; it failed on the old code).
- Seen: never walled, squad tracker jump, broadside 375, second F COOLING (other lanes).
