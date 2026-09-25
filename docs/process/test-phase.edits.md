# test-phase.js: the edits (line numbers from the copy read 2026-09-25 13:30; exact text where it is load-bearing)

E1 Schemas (:23-30). `const s = (x, n) => String(x == null ? '' : x).slice(0, n)` at the top; every value from a return passes
   through `s()` before it enters a prompt, `hist` or the workflow's return.
   WORK: `{status, head, verdict:{type:'string',maxLength:200}, ledger:{type:'string',maxLength:120}, open:{type:'string',maxLength:400}, asserted:{type:'string',maxLength:120}}` (required: status, head, verdict, ledger, open).
   GATE: `{pass, defects:[DEFECT], notes:[DEFECT]}` with `DEFECT = {file, line:integer, old:{maxLength:800}, new:{maxLength:800}, why:{maxLength:200}}`; drop `summary` and `problems`.
   RUN1: replace `summary` by `fails:{type:'integer'}` and `failLines:{type:'string',maxLength:600, description:'the header line of each red step .fails.txt and its path'}`.
   TASK: add `expect:{maxLength:120}` and `after:{type:'string'}`; `evidence` maxLength 300, `checks` 200.
E2 Prompt lint. `const P = (t) => { if (t.length > 1500) throw new Error('prompt over 1500 chars: ' + t.slice(0, 60)); return t }`
   wrapped around every `agent(P(...), ...)`. Delete `TESTRULES` (:32-37); `PROCESS` becomes
   `The process: ${MAIN}\\docs\\process\\tester.md (read it first); CLAUDE.md is the common part.` Trim every prompt to the six
   parts (role doc; tree and branch; the job with pointers; the chain; the schema name; scratch) until the lint passes.
E3 runPrompt (:78-85) step 3: `Read summary.txt; green = it ends ALL GREEN. For each red step read the first line of its
   <n>_<step>.fails.txt (its path is beside the log). Return green, head, tag, fails (the FAIL count) and failLines (those header
   lines with their paths). Grep no log.`
E4 triagePrompt (:87-105): replace `${run.summary}` by the five folder paths and `fails=<n>` per chain; step 1 becomes `Read each
   red step's .fails.txt (every verdict line, lane order); open a log only for the frames around an Exception. Two seeds listing
   the same failure = deterministic; a failure at one seed only = seed-dependent (say so in evidence). docs/plans/ledger_test.md
   lists what earlier rounds fixed: a check listed there that fails again is a regression task, kind code.` Step 2 adds: `For a
   kind check task fill expect: the literal the rewritten check must assert and its source (card, numbers section 8, README).
   A failure after a FAIL LANE in its role gets after = that lane's name.` Evidence = `<tag>/<n>_<step>.fails.txt:<line>`, the
   seed and the one asserted literal.
E5 fixSpec (:108-126). Pooled worktrees: `const T = `${DL}\\WarShips_wt_fix${slot}``, slot = the pool index (0..3) passed by pool();
   STEP 0 becomes `if ${T} is missing: git -C ${MAIN} worktree add ${T} version-l; then git -C ${T} checkout -q -B wt/${key}
   version-l and git -C ${T} clean -fdq (never -x)`. The prompt carries `t.checks`, `t.evidence`, `t.expect`, `t.rung` and the
   fails path only (no TESTRULES). Step 3's chain: kind code `quick,${t.rung},${base}`; kind check `quick,${t.rung},${base},${base}`
   (three seeds; a six-role fix `six@<seed>,six,six`). The return sentence: `Return verdict (one line), ledger (the POST
   heading), open (empty on done) and, for kind check, asserted = the literal the check now asserts and the method name.`
E6 fixOne (:128-141). After the first agent: `if (w && w.status === 'stopped_context') w = await agent(f.prompt + '\nContinue from
   the ledger in ' + f.T + ' (skip STEP 0).', { ...first, label: `fix ${t.id} (cont.)`, phase:'Fix', schema: WORK })`, then the
   red escalation as today. Before queueMerge: `if (t.kind === 'check' && s(w.asserted,120) !== s(t.expect,120)) return {id, merged:false,
   why:'asserted != expect'}`. queueMerge's prompt gains: `Refuse (merged=false, note why) when git log -1 --format=%B wt/${key}
   has no Checks: line naming ${t.checks}. In the merge commit append to ${MAIN}\\docs\\plans\\ledger_test.md the row
   `| ${r} | ${t.id} | ${t.kind} | <rung@seeds proved> | ${t.owner} | <fix commit> |` (create the file with a header if missing)
   and git rm the fix lane's docs/plans/ledger_${key}.md.`
E7 rounds() (:143-160). `let prevFails = Infinity`; after the runners: `const fails = ok.reduce((a, x) => a + (x.fails || 0), 0)`;
   `if (fails >= prevFails && hist.length && hist[hist.length-1].fixes) return { green:false, hist, stop: 'not converging: ' + fails + ' fails after merges' }`;
   `prevFails = fails`. Task order: `const now = tri.tasks.filter(t => !t.after).sort((a, b) => (b.title.includes('FAIL LANE') ? 1 : 0) - (a.title.includes('FAIL LANE') ? 1 : 0) || b.unblocks - a.unblocks)`;
   `const deferred = tri.tasks.filter(t => t.after)`; pool(now, 4, ...) with the pool index as the worktree slot; hist gets
   `deferred: deferred.map(t => t.id + ' after ' + t.after)`. The `!fixes.some(merged)` stop stays.
E8 Seed sweep, after the first green round and before auditMerge (:270): `SWEEP = ['quick,solo', 'quick,solo', 'quick,solo', 'quick,six']`
   through the same runner/triage code (a `chains` parameter on rounds(); label 's', maxRounds 2; the triage prompt gains
   `These chains ran at fresh seeds after a green round: a red here is seed-dependent; its fix is kind check (assert the literal
   with no knife edge, stale pick or clock from the input) unless the card says the code is wrong.`).
E9 audit() (:165-211), only with args.audit: step 1 ONE Haiku-low mapping agent writing docs/plans/audit.md (grep `Checks:` lines
   and harness method names per ability; no judgment); Opus writers only for groups the table shows under their risk; each
   audit tree runs `quick,solo@<seed>,solo` on a free slot before merge; `auditP` starts after `rounds('a')` is green, not at :267.
E10 releasePrompt (:259-264): step 0 = the CHANGES fold (Handoff 20 lines; Unreleased <= 150 lines of what it IS; ledger_test.md
   rows into Known broken / fixed, then delete it; previous release section deleted; DESIGN / TRAPS moves), plus the verify.ps1
   text-step caps (Handoff 20, Unreleased 150, ledger heads 40 / POSTs 6, ledger_main 25) in the same commit; the 6-line
   two-machine script appended to NOTES.txt. `${bar.summary.slice(0,1500)}` -> `${s(bar.failLines, 600)}`.
E11 The return (:298): `{ green, head, rounds: R.hist.length, fails: [per round], merged: n, sweep, bar: bar.green, release: rel && rel.url,
   framesForOwner: s(X.frames.note, 400), netOwed: s(X.net.note, 400), stop }` (under 20 lines). `hist` is not returned: the
   script logs one line per round with `log()` (round, fails, tasks by id/kind, merged, deferred), and the journal plus
   `ledger_test.md` hold the detail.
E12 tester.md drift fixed by the doc (this folder's process/tester.md): six rounds, the guard, the pooled worktrees, the sweep.
