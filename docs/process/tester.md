# Tester (the test phase: runners on Haiku, triage and fixes on Opus, release on Sonnet)

The test phase starts only when every planned lane is merged into `version-l`. Every engine run goes
through `tools\rungs.ps1` (`-Tree` required, so a lane is never tested as version-l by mistake): it runs
the chain in order, stops at the first red, takes the first free of at most 4 engine slots (slot 0 for
`bar`, `wan` and trees without slots; one chain per tree at a time) and writes
`%TEMP%\warships_rungs\<tag>\summary.txt` (UTF-8, ends `ALL GREEN` when green), one log per step (UTF-16:
never grep it from bash) and `<n>_<step>.fails.txt` (UTF-8): a count header `FAIL n / FAIL LANE n /
Exception n / PASS n`, then every verdict line in lane order. Every reader reads the fails file; a log is
opened only for the stack frames around a throw. A tag is used once (the runner clears its folder). Never
start a harness directly; never paste a log.

## Chains

| what was built | chain |
|---|---|
| renames, signatures only | `quick` |
| a rename of anything reached by string (RPC, NodePath, `Call("x")`) | `quick,solo,six` |
| numbers, behaviour, abilities, rows | `quick,solo,solo` |
| anything drawn | add `screens` |
| anything a guest sees, an RPC, authority | add `six,six` |
| everything, before the bar | `quick,solo,solo,six,screens` (`six,six` with new guest checks) |
| a fix of kind code | `quick,<rung@seed>,<base>` (the failing seed and a fresh one) |
| a fix of kind check, or a red seen at one seed only | `quick,<rung@seed>,<base>,<base>` (three seeds) |
| every chain green | the seed sweep, then `bar`, once |

`solo@<n>` and `six@<n>` replay a seed; `wan` is the WAN test (slot 0). Never re-run a higher rung hoping
for a different draw. A third of the first round's reds appeared at one seed only (32 of ~48 signatures
shared by two seeds, 16 not): two seeds do not prove a rewritten check.

## Hand-offs (pointers, never pasted evidence)

- **Runner -> triage**: `green`, `head`, `tag`, `fails` (the FAIL count) and `failLines` (the header line
  of each red step's `.fails.txt` with its path, at most 600 chars). The runner greps nothing else.
- **Triage -> fix**: one TASK per root cause: `kind` (code / check / env / review), `rung` (the lowest step
  with its seed), `checks` (the method names, 200), `evidence` (`<tag>/<n>_<step>.fails.txt:<line>` plus
  the seed and the one asserted literal, 300), `expect` (for kind check: the literal the rewritten check
  must assert and its source, e.g. `375; cards/battleship.md broadside`), `after` (the FAIL LANE it
  follows, or empty), `owner` (the lane and job it traces to), `files`, `unblocks`.
- **Fix -> merge**: `verdict` (200), `ledger` (the POST heading), `asserted` (kind check: the literal the
  rewritten check now asserts and the method). The merge refuses a kind-check fix whose `asserted` differs
  from `expect` or whose commit has no `Checks:` line naming the method; the merge commit appends one row
  to `docs/plans/ledger_test.md` (`| round | task | kind | rung@seeds proved | traces to | commit |`) and
  deletes the fix lane's ledger file.

## Order of the test phase

1. **Harness proof and slots proof** first: `quick,solo` on two test trees on slots 1 and 2 at once
   (one Haiku runner each). Red = a `rungs.ps1` or harness task before anything else; loop until solo is
   green at least once. Nothing else has run in the engine since the build phase began.
2. **Review fixes** (`args.tasks`, kind review: nothing to reproduce; make the check fail on the old code
   first, then fix) before round 1. The blanket 3+ coverage audit is OFF (owner, 2026-09-25: the check
   count is Fable's judgment); with `args.audit` it runs only after round a is green: ONE Haiku mapping
   agent (grep `Checks:` lines and method names to a table, no judgment), Opus writers only for the
   groups with a gap, each audit tree proved by `quick,solo@<seed>,solo` on a free slot before its merge.
3. **Rounds** (at most 6): five chains at once (`quick,solo` x2, `quick,six` x2, `quick,screens`) on the
   4 slots, one Haiku runner per chain (start ONE chain, wait under 10 minutes per call). Triage (Opus
   high, ONE agent) reads the five `.fails.txt` files (and `ledger_test.md`: a check listed there that
   fails again is a regression task, kind code) and returns at most 8 tasks. A FAIL LANE task is fixed
   first; tasks with a non-empty `after` are deferred to the next round (a thrown lane leaves the world
   broken for the lanes after it). Fixes, grouped (a round's wrong checks to ONE Sonnet agent, code tasks by
   lane to ONE Opus agent, at most 4 tasks each; never one agent per problem): at most 4 groups in flight
   in the pooled worktrees `wt_fix0..3`
   (`git checkout -B wt/<key> version-l` + `clean -fdq`, never `-x`: a cold tree costs 100 s of import
   per `quick`), Opus for code, Sonnet for a wrong check (the rewrite asserts `expect`; never loosened),
   each reproduced at the lowest rung, proved per the chain table, then merged (Haiku). A `stopped_context`
   gets one continuation agent from its ledger; a red gets the one Opus-high escalation. A triage with
   zero tasks and no green STOPS the workflow; so does a round whose fails count did not shrink after
   merges (not converging: the coordinator decides).
4. **Seed sweep** after the first green round: `solo` x3 at fresh seeds and `six` x1 on the 4 slots
   (~6 min). A red is one triage flagged seed-dependent; its fixes are kind check unless the card says
   otherwise; then one more round.
5. **Extras**: frames by eye (Opus medium; trust `LINT: 0` for layout, read a frame only for new art,
   frames to the owner: Drake, Rusty, siege, player ships); the network lane's owed guest checks.
6. **The bar** once (`verify.ps1 -Update`: rungs 1-5 x3, map, snapshot, manifest, integrity; ~13 min,
   slot 0). Red: `git checkout -- .` drops its regenerated files, then one triage; the fix is proved at
   the lowest rung, never by another bar; then the bar once more. Two bars for one fix means the ladder
   was skipped.
7. Green: the release agent first folds CHANGES (every merged ledger's `CHANGES entry` into Unreleased, at
   most 150 lines of what it IS; the Handoff rewritten to 20 lines: merged, owed, where the detail is;
   `ledger_test.md`'s rows folded into Known broken / fixed and the file deleted; the previous release
   section deleted per the CHANGES protocol; durable reasoning to `DESIGN.md`, traps to `docs/TRAPS.md`),
   adds the `verify.ps1` text-step caps (Handoff 20, Unreleased 150, ledger head 40 / POST 6, ledger_main
   25) in the same commit, then the `VERIFIED:` commit; push BOTH `version-l` and `main`; pack; GitHub
   release (standing OK). `NOTES.txt` carries the 6-line two-machine script (host, invite, join, what to
   look at, where the SEED line is, paste both log tails into `docs/plans/ledger_net_owed.md`).

## Rules that bite here

- Escalate one rung only when the lower one is blind to it or has failed twice; always come back down.
- A test-phase red is fixed in a fix lane from `version-l`, never in the test trees.
- Never call multiplayer "working": rung 5 is loopback; the two-machine test with the owner and a friend
  is owed after every release and is recorded as a ledger row, not prose.
- After the release: the scenarios lane (owner, standing): a scenario table in the harness (class,
  ability, foe, geometry, expected literal) reached by `scenario=<id>` through `run.ps1`, and
  `tools/scenarios.py` generating rows from the cards and reading a `.fails.txt` into a table; the
  interactions matrix (heavies x bosses x lights per weapon and platform) is rows of that table.
  Conversion (owner, 2026-09-25): one method at a time, each row proved against the method it replaces
  (both run on the same seed and agree three times) before that method is deleted; a red on a row is
  then a row to read, never a method. The `PlayerShip`/`Hub` split follows and is proved by the rows.
- Engine slots: at most 4; the PC is not the limit, shared folders and ports were.
- A tool call that is moved to the background (past its timeout) kills its child processes when it ends: the call that
  starts `rungs.ps1` with Start-Process returns at once (PID only); every wait is a separate call under 10 minutes.
  Two round-1 `six` chains died this way on 2026-09-25 with no verdict and no fails file.
- Prompts point at this file and carry at most 1,500 characters; the rules above are not re-pasted.
- One engine proof per round (owner, 2026-09-25; `fable_retro_tokens.md` section 3: 12 of 15 merged fixes needed no per-fix
  run, ~27 engine minutes and ~11M tokens each): a fix runs the engine only when its check is blind without it: a thrown
  lane or exception cascade, an env fix, a six-only authority red, a task unblocking 30+ lines. Every other fix proves by
  typecheck + `verify -Quick` + a read of its own diff against the card; the next round's chains replay its seed and a
  red seen again is a regression task. Check rewrites are Opus too (Sonnet check groups cost 1.8x the Opus code groups).
- A merge is judgment then ONE call: the agent reads the branch's commit messages (a Checks: line naming the task's
  checks) and, for a rewritten check, its diff against `expect`; then `tools\merge_lane.ps1` does both steps and the
  ledger_test.md rows in one commit (a conflict comes back as a note; resolve in a temporary worktree, never the lane's).
- Until the bar every step is red on other lanes and `rungs.ps1` stops at the first red, so a fix's chain is never green:
  a fix is proved when every check its task names is PASS in the rung step's `.fails.txt` at its seed and at one fresh
  seed of the base rung, with the step's FAIL count not above the round's; it returns done with the other reds in `open`
  as `seen:`. A coordinator never messages a workflow agent: the message resumes it as a session agent and the workflow
  never receives its result (2026-09-25, the a1-arena-stall fix; recovered through `args.premerged`).
