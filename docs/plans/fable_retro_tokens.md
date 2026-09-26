# Token retrospective, 2026-09-25 (Fable 5.1; owner's question at 22:45: "why so much, what can be improved")

Data: `tools\agents.ps1 -Tokens` (276 agents, 22,080 turns, 3,403M in, 9.25M out), the 42 workflow journals, 5 sampled
transcripts, the coordinator's own transcript (not in the report: 6,455 turns, 1,510M in, 10.2M out), `git log` since 00:00
(506 commits, 81 merges, +42,952 / -5,951 lines: scripts +10,376, harness +18,840, docs +12,883). Day total: 4,913M in, 19.5M out.

## 1 Where the 3,403M went

| slice | agents | turns | in | out | share of in |
|---|---|---|---|---|---|
| build: writers | 61 | 9,783 | 1,957M | 4.60M | 57.5% |
| build: gates | 51 | 2,962 | 378M | 1.35M | 11.1% |
| build: gate fixes + merges + waits | 45 | 2,438 | 266M | 0.90M | 7.8% |
| review / critique fan-outs (01:16 verify, critics, Fable reviews, diagnose) | 49 | 1,937 | 239M | 1.06M | 7.0% |
| test phase: fix agents | 19 | 2,956 | 443M | 1.00M | 13.0% |
| test phase: runners 30, triage 3, merges 18 | 51 | 2,004 | 121M | 0.33M | 3.6% |

Per model (from the transcripts): Opus 14,684 turns 2,182M (64%) at 149k per turn; Sonnet 5,387 turns 1,060M (31%) at 197k;
Fable 742 turns 112M; Haiku 1,604 turns 76M at 47k. Cache reads are 97.1% of every input token (cache creation 2.9%, fresh
0.0%): a turn re-reads its whole context, so an agent costs `turns x context`, and the context grows ~1.2k per turn (its
own output ~420 + tool results), i.e. cost grows with the SQUARE of the turn count. Even weighted at API ratios (cache read
0.1x, output 5x) the re-reads are two thirds of the price. The first turn of every agent is 51-58k before it reads anything
(system prompt, tool schemas of 7 MCP servers, CLAUDE.md): 22,417 turns x 52k = ~1,165M, 34% of the day, is that floor.
Turns decide everything: 137 agents under 50 turns cost 225M (7%, 68k per turn); 27 agents at 200+ turns cost 1,644M (48%,
193-254k per turn); the 74 agents over 100 turns cost 2,602M (76%). Big single results are rare (24 text results over 40k
chars all day, 0.29M tokens once); the cost is the length of the run, not one read.
Per unit of work: 118M per merged lane (~22 lanes), 27M per gate cycle (gate + fix + gate 2), ~190M per test round (fixes
150 + merges 17 + triage 12 + runners 12), 89M in / 235k out per 1,000 lines of code + harness (build 2,601M / 29,216 lines).

Top 10 agents (2,047M, 60% of the day). None stopped at the ~150k context writer.md asks for; 9 of 10 passed 300k.
1. art#1 Sonnet 308M: 760 turns, 405k average, ended at 588k; 84 Reads (SmokeTest x11, Shots x6, Hub x4, CHANGES x4, 9 frames), 8 engine runs (before the no-engine rule), 10 sleeps; redone on Opus for 18M.
2. kits#1 Sonnet 124M + 38M (two attempts, 648 turns at 285k); stalled 40 min; redone on Opus for 14M.
3. a2 knife-edges check group Sonnet 96M: 429 turns at 223k; 12 engine starts (~70 engine minutes), SmokeTest read 29 times, 4 tasks.
4. art#2 Sonnet 92M: 364 turns at 252k. 5. raids#1 Opus 71M: 253 turns, 282k -> 457k, the whole lane in one agent (the 4-job unit rule not applied), 0 engine.
6. net#1 prove Sonnet 65M + 50M: 714 turns, an engine-run loop (9 starts) on Sonnet; then net fix 25M.
7. kits6b#1 Opus 64M: 290 turns, 220k -> 375k, 150 shell calls, 0 engine; kits6b#2/#3 added 61M: three agents for one slice.
8. drives#1 Opus 63M: 234 turns at 269k. 9. review fixes net Opus 53M (+3M retry): 257 turns, 4 engine starts incl. six, 124 shell waits.
10. kits6c#6 Opus 51M: the sixth writer of slice 6c (6c: 6 writers 161M + gate 12M + fix 13M = 186M for one slice).
The Sonnet-writer experiment of the night (art, kits, net, walls, slots) cost 745M (22% of the day); the same lanes finished on Opus for 36M.

## 2 Process defects of the day and what each cost

- Prompt caps SLICE instead of throwing (`P()`, `PG()` in test-phase.js): the 19:12 4-task check group lost its prove chain and
  return line (the a1 check group then ran 3 times: 0.8 + 10.8 + 16.6 = 28M); 21:17 the a2 brace group's third task "never arrived"
  (328 turns, 37M for 2 of 3 tasks); 22:21 the a3 2-task group blocked on "task text truncated". ~70M and 3 stranded groups.
- The `asserted` string gate: 19:12 a group of 4 with 3 proved rewrites stranded by the fourth's missing line; 20:27 "2 turrets" vs
  "two turrets"; Haiku refused the arena-stall merge on a literal reading of "naming": 3 Haiku + 2 Sonnet merge agents, 14M, ~1 h.
- A resume replays only an unchanged prefix of `agent()` calls: triage a1 ran twice (15.3M + 0.7M), the a1-items-tables fixer
  twice (9.4M + 0.7M); round 1's five chains ran 4 times (tp2/tp3/tp4 tags + two six chains killed by the background-kill trap):
  16 runner agents 23M and ~140 engine minutes.
- A coordinator message to a workflow agent (the a1-arena-stall fixer, 136 turns, 16M) resumed it as a session agent: the workflow
  never got its result; recovered by hand through `args.premerged` plus the 5 merge agents above.
- Per-fix reproduce + prove chains: 136 `rungs.ps1` starts in the test phase across 47 agents (~700 engine minutes, ~450 of them
  per-fix); a fix agent averages 155 turns and 23M (the build-phase fix agents from exact gate text: 94 turns, 12M).
- Wake-ups: the coordinator ran 6,455 turns at 234k average (44 compacts, one per 147 turns) = 1,510M, the most expensive context of
  the day; 124 Workflow + 98 Agent + 70 SendMessage + 35 TaskStop + 30 Monitor calls; 11 workflow launches that died at "launched".
- The 01:16 review fan-out ran 3 verifiers per claim (24 agents, 41M) where gate.md says one skeptic (8 agents, ~14M): 27M.
- Check rewrites on Sonnet (4 groups, 185M at 217k per turn) cost 1.8x the Opus code groups of the same rounds (4 groups, 100M):
  a cheaper model that runs 3x the turns is not cheaper.

## 3 The owner's suggestion: compile + review instead of an engine run per fix

Needs the engine per fix (the check is blind without it): a FAIL LANE or exception cascade (round 1: 994 exceptions, 3 thrown lanes
hid ~200 verdicts), a red seen at one seed only whose cause is unknown, an env fix (slots, ports, rungs.ps1), a six-only authority
red with no solo proxy. Proved well enough by typecheck + `verify -Quick` + a read of the diff + the NEXT round's chains: a literal
or row rewrite (14 of the round-1 reds), a knife-edge / stale-pick / clock-from-input check rewrite (structural: the diff shows it),
a null guard, a bug the triage named with file:line. Today's ledger_test: 12 of 15 merged fixes (80%) were of the second kind.
Batch the proof: fix, merge, measure in the next round; the round's chains replay each task's failing seed (`solo@<seed>` in place of
one fresh solo), and the triage marks a fix whose check is red again as a regression (the rule exists). Risk: a wrong fix is seen one
round later (a round runs anyway: 36 engine minutes, ~25M of runners + triage), and a cascade fixed blind can hide the round: so the
first kind keeps its per-fix run. Saving: a fix group from 23M to ~12M (-11M per group, -45M per round of 4 groups, -130M per
3-round test phase), -27 engine minutes per group, ~-400 engine minutes per day. The build phase already works this way (29,216 lines,
0 engine runs) and paid for it with a first round of 994 exceptions: blind is right for a fix whose check names its literal, not for
a throw.

## 4 Importing Godot / C# API documentation: no

0 web look-ups in 276 agents. 440 compile-error lines all day; 4 mention a Godot or Node member, 436 are the project's own names
(renamed members, harness callers, a missing `using`). What the agents read: SmokeTest.cs.txt 301 times, PlayerShip.cs 87, Hub.cs 33,
Abilities.cs 30, Net.cs 25, Ships.cs 24, the cards / numbers / kits docs ~60, CHANGES.md 50 (forbidden whole), tester.md 51 (the role
doc). The turns went to the project's own code, harness and ledgers, never to the engine API. Imported docs would sit in the 52k
per-turn floor for every agent and answer 4 errors in 440. What does reduce the reading: the partial-class split and the scenario
table (`fable_code_read.md` section 6: ~3,700 lines to ~700 per ability red, a 338 s solo to a 20 s scenario).

## 5 Changes, cheapest to make first (savings overlap: the turn cap shrinks the floor and the read costs too)

| # | change | where | saves |
|---|---|---|---|
| 1 | a prompt over its cap THROWS at launch; a task list travels as a path + ids (`round3_tasks.json` already exists) | test-phase.js `P`/`PG` | ~70M/day, 0 stranded groups |
| 2 | coordinator auto-compact at ~150k (today 234k average, a compact per 147 turns) | owner setting | ~-600M/day (1,510M -> ~900M) |
| 3 | hard cap per agent: 100 turns or 180k context -> return with the ledger, a fresh continuation from it (script option if `agent()` has one, else the prompt line + `agents.ps1 -Loop` stops it) | scripts, writer.md | ~-1,000M/day: the 74 agents over 100 turns (2,602M) at ~119k per turn instead of 150-254k; -30M per lane |
| 4 | check rewrites on Opus medium, never Sonnet (the Sonnet ban on writers extended to fixes) | test-phase.js `first` | -90M per test phase |
| 5 | one engine proof per round (section 3): fix groups run typecheck + quick + diff read, the round measures | test-phase.js `fixGroup` | -11M per group, -45M per round, -130M per test phase, -400 engine min/day |
| 6 | merges and chain starts without an agent: `git merge --no-ff` + typecheck + quick from the script, an agent only on conflict; one runner starts all 5 chains | test-phase.js | 41 merge agents 107M + 30 runners 35M -> ~15M: -125M/day, -71 coordinator events |
| 7 | the per-turn floor: MCP servers this project never uses (browser, docs, visualize, scheduler, registry) off for agents; ~20k of the 52k | owner settings | ~-450M/day at today's turns, ~-250M after #3 |
| 8 | fan-outs as gate.md says: one skeptic per claim, no critic stage for a mechanical plan | the review scripts | -27M per review |

## 6 For the owner

The main problem: every turn re-reads the whole context, and agents ran 150-760 turns to 300-590k contexts instead of stopping at
100 turns, so 74 of 276 agents cost 76% of the day (cost grows with the square of an agent's turns).
The fix: a hard cap of 100 turns / 180k per agent with a continuation from the ledger, one engine proof per round, and the
coordinator compacted at 150k: ~-2,000M of 4,913M per day.
What stays expensive: the writing itself (9.3M output tokens, ~235 per landed line), one Opus gate per lane (~7M, every gate found
a defect), one coordinator turn per event (~150k after a compact), and every engine minute an agent waits on at full context.
The build phase cost 2,601M in / 6.9M out for 29,216 lines of code + harness = 89M in and 235k out per 1,000 lines: 61 writers
averaging 160 turns at 200k, of which 745M was the Sonnet-writer experiment finished on Opus for 36M.
Documentation: no; 0 look-ups, 4 of 440 compile errors touched the engine API; the agents' turns went to this project's own files.
