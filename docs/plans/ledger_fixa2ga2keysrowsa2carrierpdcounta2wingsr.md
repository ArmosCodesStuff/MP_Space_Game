# Ledger: fix group a2-g-a2keysrows-a2carrierpdcount-a2wingsr (round a2, kind check, 4 tasks)

Writer: one agent in `WarShips_wt_fix2`, branch `wt/fixa2ga2keysrowsa2carrierpdcounta2wingsr`, cut from version-l 0742408.
Tasks: a2-keys-rows, a2-carrier-pd-count, a2-wings-range, a2-ring-timing. One commit per task.

## PRE (0742408)

Reproduced all four at solo@11400714819323392986 (tag ..._a, FAIL 186): lines 348, 472, 1407-1408; ring-timing
not among this run's fails (quantized overshoot lands on a different bucket run to run -- see below).
- a2-keys-rows: Abilities.For = class abilities + Drives.RowOf (the drive row) + Open; battleship has 5 named
  abilities + 1 drive row (warp) + 6 open = 12 rows. Check asserted 11 and its message omitted the drive row.
- a2-carrier-pd-count: Ships.cs:218 carrier row has pd_count 2, not 3. Check asserted three turrets on three
  distinct LIGHT targets; a carrier can never have more than 2. Code right, check wrong.
- a2-wings-range: gunship spawned Vary(1300,2200) u off, but fighter control_range is 1500 (Stats.cs:186); a
  spot beyond 1500 u leaves fighters unable to reach it (0 fighters away), correctly. Check wrong: narrowed the
  spawn to Vary(1300,1450), safely inside control_range.
- a2-ring-timing: the OVERSHOOT block ANDs off against the nominal bucket `secs` (+-0.15 s) and against
  `byRule` (+-0.05 s, computed from the actual moved distance). Frame-quantized held duration lands the real
  jump a few u either side of `over` (150->173, 600->627 in one observed run), so `off` tracks `byRule`
  correctly but knife-edges past the tight nominal window -- same class of bug the HOLDS block above already
  guards against with `wantOff`. Fix: assert against `byRule` only, drop the nominal comparison from the
  boolean (kept in the message for readability).
