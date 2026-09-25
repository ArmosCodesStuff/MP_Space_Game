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

## Next: job S2 -- SmokeTest.cs.txt / Shots.cs.txt / screens port-shift (NOT STARTED)
Scope, for the agent picking this up: tools\smoketest\run.ps1 needs [int]$Slot=0, $shift=100*$Slot,
--port-shift=$shift appended after "--" to every role's args, fakeigd.py argv shifted, -Wan refusing
off slot 0 (exit 2). tools\smoketest\SmokeTest.cs.txt needs a static P(port) reading --port-shift=N
once from OS.GetCmdlineUserArgs(), applied to EVERY port literal that binds or is asserted about a
bound port (grep list already run once, see JOB S2 in the original task text -- re-grep at pickup
since line numbers will have moved): lines ~68-109 (Hosting fakes -- these are STUN/router-mock
ports in fixed test strings, read the surrounding NoRouterNoInternet-style test before deciding
which are literal-by-design vs need P()), ~490 (typed address literal, likely stays literal -- it is
a user-typed example, not something bound), ~579 BoxHttp, ~592 SilentA/B stun ports + BoxStun,
~619-620 STUN server literals (likely stay -- external well-known servers), ~635, ~802-808 Net.DefaultPort
27015 + Rendezvous.ListenPorts literals (these ARE the port table under test -- read whether the test
should assert the SHIFTED table), ~1287 Net.I.Host(27123), ~4922-5059 Net.Describe/.ParseAddress
27015-literal tests (many of these test the DEFAULT port as a constant, i.e. Net.DefaultPort itself,
which may not need shifting since it is the unshifted default the code falls back to -- judge each),
~9241 Net.I.Host(27124), ~9336 const Port=27125, ~10052-10402 the host/join tests that assert
Net.I.LanAddress.EndsWith(":27115") etc -- these bind real sockets under solo/six and MUST become
P(27115) etc, or slot>=1 collides with slot 0's real bind. New check: near the start, host's
LanAddress ends with ":" + P(port), proven at 3 slots. tools\screens\Shots.cs.txt:318 Net.I.Host()
needs the same treatment plus tools\screens\run.ps1 -Slot. Run typecheck + quick per edit, solo (and
six once ports are right) before commit; rewritten literal checks are this job's "Changed" checks
(CLAUDE.md 6.3) -- name every one in the commit's Checks: line.

## Next: job S3 -- docs (NOT STARTED, depends on S2 landing so the sentence is true)
CLAUDE.md section 8 rungs.ps1 sentence + section 12 rungs line (one sentence each); docs/DESIGN.md one
trap entry (a harness port literal bypassing P() collides between slots); docs/README.md if it lists
rungs.ps1; docs/CHANGES.md Unreleased + Handoff.

## Next: END OF BATCH proof (NOT STARTED, needs S2 done first -- slot>=1 is a no-op without it)
Steps 1-2 as specified in the task (solo chain at slot 0 unchanged; then 3 concurrent chains at slots
1/2/3 with a lock-holding helper process gating start; record wall times; fall back to -Slots 2 if a
red is CPU-timing, not a collision). Default -Slots to commit once proven.
