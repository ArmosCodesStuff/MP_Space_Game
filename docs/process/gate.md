# Gate (Opus 5.5, high; once per lane)

READ-ONLY on `scripts/`, the harness and the tools: no builds, no engine runs (checks are written in the
build phase and run in the test phase). You MAY edit and commit comments, `docs/` prose, ledger text and
record lines yourself (commit `gate: record fixes`, no `Checks:` line) and then pass.

## What you read

`git -C <tree> diff <base>..wt/<lane>` where `<base>` = `git merge-base version-l wt/<lane>`, skipping
`docs/plans`; the ledger's **Decisions** and **Jobs** (grep the Log only for a job you doubt); the spec
card and `numbers_curve_raids_items.md` section 8; `docs/plans/README.md` rulings. Never a giant file
whole; grep the harness for the lane's methods.

## Pass/fail

- It does what the card and the rulings say; every number from the documents.
- Every change carries its checks: they would fail without the change; 3 varied situations; no knife
  edge, stale pick or clock timed from the input; interactions have their own; changed numbers rewrote
  the old-truth checks; visible changes have named frames; wire changes have guest-role checks; a class
  ability or drive row has as many distinct checks as its risk needs (owner, 2026-09-25: Fable's
  judgment; the blanket 3+ rule is off).
- Authority: world and combat state change only under `Net.Sim` / `Net.IsHost`; guests request by RPC
  and the host checks the sender; every visible state reaches guests.
- Lifetime: everything created or subscribed is released in `_ExitTree`.
- Generalised: rows reached by id or tag; no type test or special case; nothing unused; the old path
  deleted; new ids at the end of their table inside the allocated range; every new constant table is an
  array or row table (hashed by `Net.Fingerprint`), never a List or Dictionary.
- The ledger records typecheck and quick green at HEAD.

## Return (the schema caps are hard)

`pass`; `defects` and `notes`, each a DEFECT `{file, line, old (the exact text there now, at most 800
chars), new (the exact replacement, 800), why (200)}`. Defects: behaviour, authority, lifetime, a missing
or loosened check, a wrong number, an unhashed table; only they fail the gate. Notes: comments, prose,
names, record lines; the merge agent applies them. No summary field: the arrays are the report, and a
finding without `old`/`new` text is not a finding (the fix agent must not need to read). A gate 2 reviews
ONLY `git diff` of the fix commits against the defects it was given; it never re-reviews what gate 1
passed. There is no gate 3.

## Fan-outs

Only for the network protocol and the save format: at most 3 finders with named angles, one skeptic per
claim. Everything else is this single gate.
