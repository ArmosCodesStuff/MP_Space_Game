# Fable's report on how Warships is being built (2026-09-25)

Where the 23M subagent tokens went in 31 hours: 18% in eight killed-and-relaunched workflows (a "wait" agent cannot wait, so a unit was skipped and the run restarted from scratch); 12% in one 28-agent review of one lane; ~40% of every build workflow in gate + fix + gate 2 + merge, where gate 2 fell three times on a single stale comment; ~1.5M in fresh writers re-reading 250 KB of spec (slice 6c needed six agents); Sonnet writers stalling 40 minutes; every merge serialized through one 1.9 MB harness file. The gates themselves earned their cost: every one found a real defect. The ledger discipline, one Opus gate per lane, the rung ladder and build-then-test all stay.

## Change NOW (before the test phase launches; about 4-5 hours of agent work, no engine)

1. Harness: wrap each of the ~99 lane calls in the solo/six role bodies in a catch-and-continue helper. Today the first exception ends the whole role, so each test round would surface ONE failure per role; with the helper round 1 lists every failure at once. Saves an estimated 2-3 rounds (35-60 min and 1.5-3M tokens each). Costs one mechanical Opus job, typecheck + quick.
2. Test-phase order: prove the harness and the engine slots with one `quick,solo` chain first; run the ability audit in parallel but merge it only after the first green; five small waiters (one per chain) instead of one 30-minute waiter; stop on a triage with no tasks instead of re-running the same five chains. Prevents the one known way a workflow has died and 1.5-2M tokens of audit checks written onto unproven ground. Costs a script edit.
3. Waits are promises inside one workflow; a stopped workflow is relaunched with resumeFromRunId so finished agents cost nothing. Removes the whole "killed" column (~15-18% of spend). Costs nothing.
4. Gate returns defects and notes: notes (comments, prose, names) are applied by the merge agent and never re-gated; a defect gets one Sonnet fix with the gate's exact text and a gate 2 scoped to that diff; no gate 3. Saves one Opus-high fix + gate per lane (0.3-0.5M tokens, 20-40 min); every review still happens.
5. Model policy below: Fable coordinates only, Opus writes/gates/triages, Sonnet/Haiku run recipes under the 30-minute watchdog. Saves ~5x on merges, runners and exact-text fixes; ends the Sonnet-writer stalls.
6. Lean CLAUDE.md (6 KB, replacing 17.8 KB) plus four role files (`docs/process/coordinator|writer|gate|tester.md`); scripts point at them instead of re-pasting rules. Every agent turn is ~3k tokens lighter; the coordinator's wake ~60% cheaper. Costs one commit at a batch boundary (a running lane keeps the old text until it ends).
7. One 6 KB spec card per class, resolved from the three sign-off files and your rulings, written once by Sonnet; writers, auditors and fix agents read the card, never the 250 KB. Halves a fresh agent's fixed cost; fewer hand-offs. Costs ~12 small Sonnet jobs.
8. The plan allocates each lane's file set, its id ranges (NetIds, Spawns, Fx, Shots rows), its scratch folder and rungs tags. Removes renumbering at merges and the scratch collisions. Costs a row in each ledger.
9. Ledgers keep a 40-line head (Decisions, Jobs, a pointer block for the next agent) over an append-only log; ledger_main drops its Done history (25 lines). Gate and hand-off reads fall ~5x.
10. Lanes stop editing CHANGES.md/DESIGN.md: the entry goes in the last POST; the merge agent rewrites a 15-line Handoff; the release folds the rest. Ends the conflict on nearly every merge; session start reads 2 KB instead of 52 KB.
11. Fan-outs only for the network protocol and the save format, at most 3 finders and one skeptic per claim; a diagnosis is one Opus agent. Saves ~2M tokens per wave; 6 of 8 claims survived three skeptics, one would have done.
12. ledger_main records per workflow its tokens and agents; the two numbers to hold: tokens per merged lane (today ~1.0-1.5M) and per gate cycle (~0.3-0.5M).

## Change AFTER this release (moving code mid-test-phase would break the tracing of reds)

- Split SmokeTest.cs.txt (1.9 MB, one file every lane appends to) into one partial-class file per system; three copy lines in run.ps1 / typecheck.ps1 / verify.ps1 loop a folder. Ends harness merge conflicts; a gate reads 30-60 KB instead of grepping 1.9 MB.
- Fx, Shots, Spawns ids become enums (a merge keeps both sides' members; nothing is renumbered); the five List/Dictionary tables the net fingerprint cannot hash become arrays.
- CHANGES.md Unreleased compiled at release from the ledgers; DESIGN.md per-lane sections become pointers; traps move to a grep-able docs/TRAPS.md; REVIEW.md is frozen.
- PlayerShip.cs (147 KB) and Hub.cs (127 KB) split by subsystem into partial classes so two lanes can touch them side by side.

## Model policy (task -> model, effort)

| task | model, effort |
|---|---|
| plan a wave, read verdicts, ledger_main, the batch script | Fable 5.1, medium (inline; one wake per workflow) |
| lane JOB 0 (card + job list); build a feature with its checks | Opus 5.5, medium (high only on the one escalation) |
| gate; triage of engine reds; diagnosis (ONE agent) | Opus 5.5, high |
| frames by eye; writing missing audit checks | Opus 5.5, medium |
| fix from exact text a gate or triage gave | Sonnet 5, medium (Haiku 4.5, low for a pure replacement) |
| merge; run a chain and read summary.txt; the bar; grep-to-table mappings | Haiku 4.5, low (Sonnet 5 high on a merge conflict; Opus on the second failure) |
| release: VERIFIED commit, push both, pack, gh release | Sonnet 5, medium |

Every Sonnet/Haiku agent runs under tools\agents.ps1 -Loop (30 min); an Opus agent idle past 20 min gets the same look.

## Your decisions (one line each; the default is what I will do)

- Apply the lean CLAUDE.md and role files now, before test-phase.js launches? Default yes (standing OK; lane-kits-finish keeps the old text until it ends).
- Merge the 3+ checks-per-ability audit after the first green solo round rather than before any engine run? Default yes (coverage unchanged, only the order).
- Harness split now as test-phase step 0, or after the release? Default after (five worktrees are still open; a move mid-test-phase breaks `git log -S` tracing).
- One solo chain at each lane's MERGE during the build phase (not per job or batch), only to learn which checks start? Default no (your build-then-test ruling stands).
- Cap each build workflow's token budget so a runaway unit throws instead of draining the week? Default yes, if the harness exposes it.
