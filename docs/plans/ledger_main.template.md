# Coordinator state -- last event hh:mm: <what happened>
Under 25 lines. Edit tool only; commit as the FIRST call after every event. The hook re-injects this after a compact and
cross-checks every `task <id>` below against its output file. Facts go to README / CHANGES, not here.

## Running (one row per workflow; the form `task <id> (<run id>)` is what tools/lanes.ps1 parses)
- <name> / task <id> (<run id>): <what it does>. lands: <the one action to take when it lands>

## Landed (verdict first; delete the row once its action is done)
- <name> / task <id> hh:mm <status>: <verdict line> -> <action>

## Next (1 is the exact next call, copy-pasteable)
1. <call>

## Owner questions (one line each, with its default)
- <question>? default: <x>

## Notes (merge risks, promised follow-ups; nothing done, nothing historical)
- <note>
