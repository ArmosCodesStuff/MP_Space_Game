# Tester (the test phase: runners on Haiku, triage and fixes on Opus, release on Sonnet)

The test phase starts only when every planned lane is merged into `version-l`. Every engine run goes
through `tools\rungs.ps1` (`-Tree` required, so a lane is never tested as version-l by mistake): it runs
the chain in order, stops at the first red, takes the first free of at most 4 engine slots (slot 0 for
`bar`, `wan` and trees without slots; one chain per tree at a time) and writes
`%TEMP%\warships_rungs\<tag>\summary.txt` (UTF-8, ends `ALL GREEN` when green) plus one log per step. A
tag is used once (the runner clears its folder). Never start a harness directly; never paste a log.

## Chains

| what was built | chain |
|---|---|
| renames, signatures only | `quick` |
| a rename of anything reached by string (RPC, NodePath, `Call("x")`) | `quick,solo,six` |
| numbers, behaviour, abilities, rows | `quick,solo,solo` |
| anything drawn | add `screens` |
| anything a guest sees, an RPC, authority | add `six,six` |
| everything, before the bar | `quick,solo,solo,six,screens` (`six,six` with new guest checks) |
| every chain green | `bar`, once |

`solo@<n>` and `six@<n>` replay a seed; `wan` is the WAN test (slot 0). A new or rewritten check passes
on two seeds; a flaky one is fixed at rung 3 until three seeds pass. Never re-run a higher rung hoping
for a different draw.

## Order of the test phase

1. **Harness proof and slots proof** first: `quick,solo` on two test trees on slots 1 and 2 at once
   (one Haiku runner each). Red = a `rungs.ps1` or harness task before anything else; loop until solo is
   green at least once. Nothing else has run in the engine since the build phase began.
2. **The ability audit** (owner: 3+ engine checks per class ability and drive row): a Haiku agent maps
   every `ClassDef.Abilities` and drive row to its checks (grep the `Checks:` lines and the harness
   methods into a table, no judgment); Opus writes the missing checks per hull tier; one gate; merged
   only after step 1 is green.
3. **Rounds**: five chains at once (`quick,solo` x2, `quick,six` x2, `quick,screens`) on the 4 slots,
   one Haiku waiter per chain (start ONE chain, wait under 10 minutes per call, return its summary lines
   and the FAIL / Exception / LINT lines of its red step grouped by check method, at most 40 lines).
   Triage (Opus high, ONE agent) reads the union and returns tasks, each traced to a lane through the
   ledgers' job lists (`git bisect` over the job commits if unclear), tagged: code wrong (Opus fix, high)
   or check wrong (knife edge, stale pick, clock from the input: Sonnet fix with the exact rewrite). At
   most 4 fixes in flight; each proved at the lowest rung that covers it, in its own worktree, then
   merged (Haiku). A triage with zero tasks and no green STOPS the workflow (never re-run the same five
   chains). The round is re-run once after the fixes merge.
4. **Extras**: frames by eye (Opus medium; trust `LINT: 0` for layout, read a frame only for new art,
   frames to the owner: Drake, Rusty, siege, player ships); the network lane's owed guest checks.
5. **The bar** once (`verify.ps1 -Update`: rungs 1-5 x3, map, snapshot, manifest, integrity; ~13 min,
   slot 0). Red: `git checkout -- .` drops its regenerated files, then one triage; the fix is proved at
   the lowest rung, never by another bar; then the bar once more. Two bars for one fix means the ladder
   was skipped.
6. Green: a `VERIFIED:` commit; push BOTH `version-l` and `main`; pack; GitHub release (standing OK).
   The release agent folds every merged ledger's `CHANGES entry` into Unreleased and one honest Known
   broken, deletes the previous release section (the CHANGES protocol), and moves durable reasoning to
   `DESIGN.md` / traps to `docs/TRAPS.md`.

## Rules that bite here

- Escalate one rung only when the lower one is blind to it or has failed twice; always come back down.
- A test-phase red is fixed in a fix lane from `version-l`, never in the test trees.
- Never call multiplayer "working": rung 5 is loopback; the two-machine test with the owner and a friend
  is owed after every release.
- After the features: python scenario tools and smoke tests covering every mechanic for every class,
  every ability, and the interactions between heavies, bosses and lights for every weapon and platform
  (owner, standing).
- Engine slots: at most 4; the PC is not the limit, shared folders and ports were.
