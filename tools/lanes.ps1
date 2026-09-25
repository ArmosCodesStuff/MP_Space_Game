# lanes.ps1 [-Runs <dir>...] [-Last <n>] -- the coordinator's state in one read-only call (CLAUDE.md section 4):
# every worktree's branch, head and newest ledger heading, the newest engine chains' step lines, then
# docs/plans/ledger_main.md. The SessionStart hook in .claude/settings.json runs it at every start and compact.
param([string[]]$Runs = @(Join-Path $env:TEMP 'warships_rungs'), [int]$Last = 8)
$root = Split-Path -Parent $PSScriptRoot
git -C $root worktree list --porcelain | Select-String '^worktree ' | ForEach-Object {
  $wt = $_.Line.Substring(9)
  $branch = git -C $wt rev-parse --abbrev-ref HEAD
  $head = git -C $wt log --oneline -1
  $dirty = @(git -C $wt status --short).Count
  $led = Get-ChildItem (Join-Path $wt 'docs\plans\ledger_*.md') -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
  $h = if ($led) { $led.Name + ': ' + (Select-String -Path $led.FullName -Pattern '^#{2,3} ' | Select-Object -Last 1).Line } else { '(no ledger)' }
  "{0} [{1}] {2}{3}`n    {4}" -f (Split-Path $wt -Leaf), $branch, $head, $(if ($dirty) { "  ($dirty uncommitted)" } else { '' }), $h
}
"-- newest engine chains"
Get-ChildItem $Runs -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last $Last | ForEach-Object {
  $lines = @(Get-Content (Join-Path $_.FullName 'summary.txt') -ErrorAction SilentlyContinue |
    Where-Object { $_ -match '^(==|\d+ |ALL GREEN|STOPPED)' } | ForEach-Object { ($_ -replace '\s*\|.*$', '').Trim() })
  "{0} {1:HH:mm}: {2}" -f $_.Name, $_.LastWriteTime, ($lines -join ' / ')
}
"-- docs/plans/ledger_main.md"
Get-Content (Join-Path $root 'docs\plans\ledger_main.md') -ErrorAction SilentlyContinue
