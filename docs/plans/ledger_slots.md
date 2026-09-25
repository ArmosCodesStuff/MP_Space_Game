# ledger_slots (lane wt/slots)

## PRE job S1 -- rungs.ps1 engine slots
tier: sonnet, effort medium. intent: add -Slot/-Slots to tools\rungs.ps1 (real %TEMP% capture, UTF-8
summary.txt, per-tree lock, slot-0-unchanged + slot n>=1 locks, tag line names the slot).
files: tools/rungs.ps1
HEAD: f3876fb7fbda3e08ac126a4194f6f725665ed2d1
hashes (git hash-object):
  tools/rungs.ps1                    f9f43cbc26007ebadb5158c20df033d6fb025227
  tools/smoketest/run.ps1            ad134d9c0ffbe6ddba0fb3016534699cebf9a2be
  tools/screens/run.ps1              69d9eed4f43a60cccdd1a7309b521803f8ece593
  tools/smoketest/SmokeTest.cs.txt   406da91977a89940daaa72fc3e721ef56d18d618
  tools/screens/Shots.cs.txt         eea15c110cef1c995d09b0344368c59a5123e150
  docs/DESIGN.md                     9637c92641e946f8d9f756cc9e7b07bfcc7d00e1
  docs/README.md                     c6c0a1c13e8eff821ee87e3600bb3eb49b942776
  docs/CHANGES.md                    44ca0246e80b7e3f4c7dd03ab52201f53168a8ac
  CLAUDE.md                          93f6b61e4938c3fffbd05ecad5cc82e853395b5d

## POST job S1
verdict: green. files: tools/rungs.ps1, docs/plans/ledger_slots.md (new).
checkpoint commit: c9d953a "rungs.ps1: engine slots (S1) -- -Slot/-Slots, per-tree lock, UTF-8 summary"
typecheck: 0 errors. verify.ps1 -Quick: ALL CHECKS PASSED. Functional smoke: `rungs.ps1 -Steps quick`
end to end -- slot 0 assigned (no smoketest\run.ps1 -Slot param yet so slots force to 0), summary
first line "== slots_smoke0 at f3876fb ... slot 0", file is UTF-8 (EF BB BF BOM then ASCII, greppable),
ALL GREEN. Slot 0 path is provably unchanged: same lock file, no env overrides, no -Slot passed down.
next: S2.

## PRE job S2 -- SmokeTest.cs.txt / Shots.cs.txt / wan.py / screens port-shift
tier: sonnet, effort medium. intent: route every port literal the harness binds, joins or asserts
through P(port) = port + Shift (Shift read once from --port-shift=N).
files: tools/smoketest/run.ps1, tools/smoketest/SmokeTest.cs.txt, tools/smoketest/wan.py,
tools/screens/run.ps1, tools/screens/Shots.cs.txt
HEAD: 985d4d8
hashes (git hash-object):
  tools/smoketest/run.ps1            ad134d9c0ffbe6ddba0fb3016534699cebf9a2be
  tools/smoketest/SmokeTest.cs.txt   406da91977a89940daaa72fc3e721ef56d18d618
  tools/smoketest/wan.py             (unchanged since repo HEAD -- first touch this lane)
  tools/screens/run.ps1              69d9eed4f43a60cccdd1a7309b521803f8ece593
  tools/screens/Shots.cs.txt         eea15c110cef1c995d09b0344368c59a5123e150

## POST job S2
verdict: green. found one collision the task's list did not name: tools/smoketest/wan.py's own box
(the STUN responder 127.0.41.1:3478, silent UDP 19482/19483, silent TCP 19481) runs for EVERY run,
not just -Wan, on fixed ports -- added wan.py --shift, applied in run.ps1's boxArgs (--http shifted
too) and Stop-Box's quit URL. All 5 files now hashed:
  tools/smoketest/run.ps1            b965bc8808d4f2652f1b85842979ccee5e43ecfd
  tools/smoketest/SmokeTest.cs.txt   c56bd6f309bac3c7873bc5e805fcac98e92336b7
  tools/smoketest/wan.py             29c35b3be1e5039dc98cba130b6113ab92677031
  tools/screens/run.ps1              7cabb783c02a1b0402057ac8aaf458c6329f34f9
  tools/screens/Shots.cs.txt         dd77ac43eb848eb192a9da98f8656647f61deaaf
typecheck: 0 errors (caught + fixed a name collision: a local `int P(string n)` inside the sound-effects
check shadowed the new static P(int) in the same method -- renamed the local to `Plays`).
verify.ps1 -Quick: ALL CHECKS PASSED.
Left literal (by design, not bound/not this run's port): Net.Describe/.ParseAddress synthetic-address
tests (~4929-5066), typed-address example at ~497, Net.DefaultPort/Rendezvous.ListenPorts table proof
at ~812 (asserts the actual unshifted constants, no bind), external STUN servers google/cloudflare at
~626, Net.I.Reachable(...,"203.0.113.7:27015",...) UI-text injection at ~10116/10120 (a fabricated
display string, not a real bind).
Rewritten as the mandated "Changed" checks + new check (LanAddress ends with P(port), proven at 3
slots by the end-of-batch proof): RouterScenarios' 9 Hosting() scenarios (single/double/double-quiet/
refuse/none/pmp/pcp/cgnat/vpn), the box-up check (STUN/silent ports), Net.I.Host(P(27115/27116/
27123/27124)) + HostAt(P(27115)) joins, ArenaMp's Port, the "hosting starts at once" LanAddress check,
"forward UDP" status check.
checkpoint commit: (about to commit) "harness: port-shift P() through smoketest, wan.py's box, and
screens (S2)"
next: S3.

## Next: job S3 -- docs (NOT STARTED, depends on S2 landing so the sentence is true)
CLAUDE.md section 8 rungs.ps1 sentence + section 12 rungs line (one sentence each); docs/DESIGN.md one
trap entry (a harness port literal bypassing P() collides between slots -- name wan.py's box as the
one CLAUDE.md's own collision list missed); docs/README.md if it lists rungs.ps1; docs/CHANGES.md
Unreleased + Handoff.

## Next: END OF BATCH proof (NOT STARTED, needs S2 done first -- slot>=1 is a no-op without it)
Steps 1-2 as specified in the task (solo chain at slot 0 unchanged; then 3 concurrent chains at slots
1/2/3 with a lock-holding helper process gating start; record wall times; fall back to -Slots 2 if a
red is CPU-timing, not a collision). Default -Slots to commit once proven.

## PRE job S3 -- the record
tier: opus (owner: "do the tasks with opus 5.5"), effort medium. intent: CLAUDE.md s8 rungs sentence + s12
rungs line; DESIGN.md one trap; README.md (does not list rungs.ps1: untouched); CHANGES.md Unreleased + Handoff.
HEAD: df869599b3f1a772a01b2f1a05929aa7c6c1a282
hashes: CLAUDE.md 93f6b61e4938c3fffbd05ecad5cc82e853395b5d; docs/DESIGN.md 9637c92641e946f8d9f756cc9e7b07bfcc7d00e1;
docs/CHANGES.md 44ca0246e80b7e3f4c7dd03ab52201f53168a8ac

## POST job S3
verdict: green. files: CLAUDE.md (s8 sentence, s12 rungs line), docs/DESIGN.md (trap: port literal bypassing
P(), naming wan.py's box), docs/CHANGES.md (Handoff + Unreleased). docs/README.md does not list rungs.ps1: untouched.
typecheck 0 errors; verify -Quick ALL CHECKS PASSED. next: the proof.

## COORDINATOR NOTE 1 (owner, 2026-09-25): run several engines at once
The owner: "feel free to run multiple instances of the engine and game at the same time, my PC can handle it no prob". If the 3-chain
proof (slots 1-3 at once) is ALL GREEN, the default -Slots is 4 (slot 0 plus the three proven). A red that is CPU timing is still a
bug in that check (CLAUDE.md 6): fix it rather than lowering -Slots.
