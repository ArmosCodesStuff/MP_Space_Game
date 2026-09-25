# ledger_prep

## PRE prep-1 (tier: harness, medium)
Intent: Lane(name, body) helper in SmokeTest.cs.txt; every lane call in the solo, Mp (host/guest/third) and ArenaMp (ahost/aguest) bodies wrapped so a throwing lane is recorded (FAIL LANE line) and the role continues; end of role FAILED "N lanes threw: ..." via Check.
Files: tools/smoketest/SmokeTest.cs.txt
HEAD: ae8ae99c2e9336e11e3967f21240efcab46b400c
hash tools/smoketest/SmokeTest.cs.txt: 96efdea850d19e9b9bbc0e62cf7db74a9722feda

## POST prep-1
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED. No engine run.
Lane(name, body): static, beside Check; prints "FAIL LANE <name> threw <Type>: <first line> (<s> s)" + 2 stack frames, or "  lane <name> ok (<s> s)"; the runner adds the [role] prefix, so the log reads "[solo]  FAIL LANE ..." (matches rungs.ps1 `\]\s+FAIL `). _Ready, right after the role's own try/catch: Check(false, "N lanes threw: a, b") when any did, so fails>0 -> run.ps1 verdict FAILED as before.
Wrapped (statement-form `await X(args);`, X a class-level method, not a helper): Solo 131, ArenaMp (ahost/aguest) 42, Mp (host/guest/third) 5 = 178. Diff proven mechanically: each wrapped line unwraps to its original exactly; only other changes are the helper (20 lines) and the end-of-role check (1 line).
Not wrapped, on purpose: helpers (Wait, ToSignal, RealTime, SetClass, Park, RailFire, WarpHold), local functions (CheckBays, Hangar, ProveReach, Marker, XFirstBlow, LSettle, Cycle, ToArena, FastRun, ToAway, ...), value-returning lanes (FieldsTearAhead, GuestWalls, R2cJoinByInvite: their result is used after), Fly (already try/catch per class), _Ready's pre-role solo lanes (already each in try/catch).
Checkpoint: this commit. Next: none (test phase proves it: a thrown lane prints FAIL LANE and the role's later lanes still run).
