# Where every rule and ruling went (current CLAUDE.md 17.8 KB + docs/plans/README.md rulings -> lean CLAUDE.md + docs/process/*)

C = the lean CLAUDE.md; CO = process/coordinator.md; W = writer.md; G = gate.md; T = tester.md; README = docs/plans/README.md (unchanged, stays the home of every ruling).

## CLAUDE.md, section by section

| current section / rule | new home |
|---|---|
| Header: Godot 4.7.2, host owns the world, repo is the truth | C header |
| Top: BUILD then TEST, no engine in the build phase, per job typecheck + quick + diff read, per lane one Opus gate | C "The two phases" |
| Top: "keep it short, never paste it into a prompt" | C is 6 KB; CO Model policy: prompts name only the rules that bite; scripts point at process docs instead of re-embedding rules |
| 1 Roles: coordinator, lane, batch | CO Words (batch = workflow; unit added) |
| 2 Session start: lanes.ps1 hook, CHANGES Handoff, grep DESIGN/REVIEW/plans, no verify, ADOPT or REVERT | CO Session start (Handoff now 15 lines; REVIEW.md frozen) |
| 3 Plan the batch: order, foundations first, one question batch with defaults, never ask what the repo answers, rulings to README at once; concurrency | CO Plan before any code (+ the plan allocates files, id ranges, scratch, tags, cards) |
| 4.1 The model fits the task; Opus evaluates, Sonnet/Haiku recipes; effort per task; 30-minute watchdog agents.ps1 -Loop | C Models (summary) + CO Model policy table (the full table, the watchdog, Opus idle > 20 min) |
| 4.2 Prompts (paths, job, chain, schema, rules that bite, owner-message line); returns (status/head/10 lines/open) | C (owner-message line) + CO Model policy (prompts, returns) + W Return |
| 4.3 Fan-outs: one gate per lane; fan-out only authority/wire/save; <= 5 agents; one task per agent; no critic for mechanical plans | CO Fan-outs (tightened: protocol and save format only, <= 3 finders, 1 skeptic) + G Fan-outs |
| 4.4 Never paste a log; read slices; the five never-whole files; MAP.md answers what exists | C "Read slices, write little" |
| 4.5 Wake-ups: one per batch, silent verdicts, report when the owner must decide | CO Wake-ups and memory |
| 4.6 Compaction-safe: ledger_main four sections under 40 lines, update first at every event, commit at once, hook re-injects, never ask for a compact | CO Wake-ups and memory (now <= 25 lines, nothing done kept) |
| 4.7 Agent context: stop at a job boundary past ~150k, fresh agent reads the ledger | W The ledger (+ units <= 4 jobs so the boundary is by design) |
| 5 Ledgers: PRE/POST, hashes, commit per job, interrupted job rule, COORDINATOR NOTE n, "applies NOTE n" | W The ledger (shape: Decisions / Jobs + Pointers / Log) |
| 6 Build: compile checks are not tests; engine never in build phase; the harness is source; the seven check kinds; Checks: line; verify refuses scripts change without harness change | C "Every change carries its check" + W Checks (verify refusal, END of role bodies, catch-and-continue helper) |
| 6 The three traps; Vary/VaryAngle/VaryNear, SEED, solo@n; two seeds, three seeds at rung 3; never re-run a higher rung; literals; table proved in one place; mutants | C (traps, Vary, seeds one line) + W Checks (two/three seeds, literals, tables, mutants) + T Chains (seed replay, never re-run a higher rung) |
| 7 Systems: generalise; rows, contracts, one file per thing, id or tag never type; name for the mechanism; extend-before-inventing list; delete what is replaced; no new tool script; scratch subfolder per lane | C "Systems" (+ ids at the END inside the allocated range; constant tables hashed) ; scratch per lane in C Read slices + W traps; "no new tool script unless the job recurs" -> W is silent, kept implicitly by "delete what a change replaces"; add to W if wanted |
| 8 The ladder table; rungs.ps1 line; slots; summary.txt; never start a harness directly | C "The ladder" (table + rungs line) + T (full semantics: slots, tags, logs) |
| 8 Build phase quick only; test-phase chain table; tracing a red through ledgers / git bisect; solo@n, six@n, wan; tag used once; escalate one rung, come back down, two bars = ladder skipped | T Chains + T Order + T Rules; C ladder (escalate / come back down / two bars) |
| 9 Invariants A-D | C Invariants |
| 10 Merge: quick green + gate passed, --no-ff, conflicts from ledgers' intent; after every lane the test phase, bar once, fix lanes, VERIFIED, push both, standing OK releases, never call MP working | CO Merge and release + T Order 5-6; C two phases (push both, release, never call MP working) |
| 11 Replies to the owner | C Read slices (short form) + CO Replies (full) |
| 12 Commands; find-godot; integrity only in -Update | C Commands |
| 13 Record: CHANGES Handoff + Unreleased + Known broken, DESIGN.md, docs/README.md sizes, REVIEW.md | W Record (entries in the ledger POST; lanes stop editing CHANGES/DESIGN/REVIEW) + T Order 6 (release folds them) + CO Merge (Handoff rewritten by the merge agent) |

## docs/plans/README.md rulings (all stay in README; pointers below)

| ruling | also in |
|---|---|
| Kits and classes (hulls, Supercarrier, Grapnel, Taunt, Ramjet, active reload replaces Overcharge, Time on Target, warp capital-only + V boost, rate buffs add, point defence, Rewind, supers, enemy heavies) | the spec cards docs/plans/cards/<class>.md quote them verbatim where they override the sign-offs; W "What you read" points at README |
| Chips, walls, balance (6 slots 3+3, walls table, balance around base classes, respawn 24 s) | cards / numbers file section 8; README |
| Raids and bosses (adds, hull not trimmed, curve, salvage, level skipping) | numbers file section 8; README |
| Items (hull categories, tiers, after the kits); existing saves disregarded | W Rulings that bite a writer |
| Network: webrtc-native, no third-party infrastructure (STUN only), invite codes, OPEN defaults, standing OK releases, push only on a green bar to both, two-machine play never worked | C two phases (push both, release, never call MP working) + CO Merge and release (no third-party infra, backup branch) + T Rules (two-machine test owed) |
| Sprites (34 sprites, the owner's picks, bosses 2x and red/black, camera repositioned) | README only (design, not process); the art card |
| Agents: build everything then test | C two phases + CO Words |
| Agents: proceed and push when due; recommendations approved; one task per agent | CO Merge and release + CO Model policy |
| Agents: the model fits the task; 30-minute watchdog | C Models + CO Model policy |
| Agents: several engine instances at once, slots, the PC is not the limit | T Rules + CO Merge (slots at most 4) |
| Agents: kits slices side by side, items alongside, reconcile, never skip a review, be efficient | CO Merge and release + W Rulings |
| Agents: Fable orchestrates everything and assigns models; critique + lean CLAUDE.md; report to the owner first | C Models + CO header |
| Testing: 3+ engine checks per class ability and drive row with an audit; everything clean = push both + release | C check kind 7 + T Order 2 and 6 |
| Testing: every change carries its check; python scenario tools after the features | C checks + T Rules |
| Compaction-safe ledger_main; never ask for a compact (today only in CLAUDE.md 4.6) | CO Wake-ups; ADD one line to README's Agents list when the lean CLAUDE.md lands |
| At most 4 engine slots (today in CLAUDE.md 8 + rungs.ps1:25) | T Rules + CO Merge and release |

## New rules (none replaces a ruling)

Waits are promises, relaunch with resumeFromRunId (CO); gate defects vs notes, no gate 3 (CO, G); id ranges allocated in the plan (CO, C); spec cards and the spec extract (CO, W); ledger shape and the mandatory pointer block (W); units <= 4 jobs, <= 2 agents (CO, W); CHANGES entry in the POST, the Handoff rewritten by the merge agent, the release agent folds (W, CO, T); test-phase order: harness + slots proof first, audit merged after the first green, one waiter per chain, stop on a zero-task triage (T); edit-tool traps (C, W); CLAUDE.md changes apply at a batch boundary and are named by hash (CO); the catch-and-continue helper round every lane call in the harness (W).
