# lanes.ps1 [-Runs <dir>] [-Last <n>] [-Check] [-Autosave] -- the coordinator's state in one read-only call, and
# the two compaction hooks (.claude/settings.json). Prints, in this order: a STATE line (ledger_main's last
# commit, CLAUDE.md's blob and size, UNCOMMITTED when dirty); every worktree with ITS lane ledger's last heading
# (DIRTY when uncommitted); the engine chains newer than ledger_main's last commit (verdict kept, seed digits
# dropped); the live workflows (journals of the last 12 h: agents done, running labels, last result); every
# `task <id>` in ledger_main's Running section against %TEMP%\claude\<slug>\*\tasks\<id>.output (running /
# ** LANDED, NOT IN LEDGER ** / recorded); then ledger_main.md itself. Nothing on disk is at risk in a compact;
# this output is what a compact must restore, so the coordinator trusts it over any summary.
# -Check (Stop hook): prints a block decision when ledger_main is uncommitted or a task landed unrecorded, so a
#   reply cannot end with state that lives only in the conversation; a second pass (stop_hook_active) passes.
# -Autosave (PreCompact hook): commits a dirty ledger_main.md and nothing else.
param([string]$Runs = (Join-Path $env:TEMP 'warships_rungs'), [int]$Last = 8, [switch]$Check, [switch]$Autosave)
$root = Split-Path -Parent $PSScriptRoot
$ledger = 'docs/plans/ledger_main.md'
$slug = $root -replace '[:\\_]', '-'
$dirty = [bool](git -C $root status --short -- $ledger)

if ($Autosave) {
  if ($dirty) {
    git -C $root commit -q -o $ledger -m 'ledger_main: autosave before compact'
    'ledger_main autosaved'
  } else { 'ledger_main clean' }
  exit 0
}

$ledgerText = [string](Get-Content (Join-Path $root $ledger) -Raw -ErrorAction SilentlyContinue)
function Section([string]$name) {
  if ($ledgerText -match "(?s)## $name[^\r\n]*\r?\n(.*?)(?=\r?\n## |\z)") { $Matches[1] } else { '' }
}
$tasksDirs = @(Get-ChildItem (Join-Path $env:TEMP "claude\$slug\*\tasks") -Directory -ErrorAction SilentlyContinue)
function TaskLines {
  $out = @()
  foreach ($m in [regex]::Matches((Section 'Running'), 'task\s+(w[0-9a-z]{8})\b')) {
    $id = $m.Groups[1].Value
    $f = $tasksDirs | ForEach-Object { Get-Item (Join-Path $_.FullName "$id.output") -ErrorAction SilentlyContinue } | Where-Object { $_ } | Select-Object -First 1
    if (-not $f) { $out += "  $id  no output file"; continue }
    if ($f.Length -eq 0) { $out += "  $id  running"; continue }
    $line = ''
    try {
      $j = Get-Content $f.FullName -Raw | ConvertFrom-Json
      if ($j.logs -and $j.logs.Count) { $line = [string]$j.logs[-1] } elseif ($j.summary) { $line = [string]$j.summary }
    } catch { $line = [string](Get-Content $f.FullName -TotalCount 1) }
    $line = ($line -split "`r?`n")[0]
    if ($line.Length -gt 160) { $line = $line.Substring(0, 160) }
    $out += ('  {0}  ** LANDED, NOT IN LEDGER ** {1:HH:mm}: {2}' -f $id, $f.LastWriteTime, $line)
  }
  foreach ($m in [regex]::Matches((Section 'Landed'), 'task\s+(w[0-9a-z]{8})\b')) { $out += "  $($m.Groups[1].Value)  recorded" }
  $out
}

if ($Check) {
  $stdin = ''
  try { $stdin = [Console]::In.ReadToEnd() } catch {}
  if ($stdin -match '"stop_hook_active"\s*:\s*true') { exit 0 }
  $reasons = @()
  if ($dirty) { $reasons += 'docs/plans/ledger_main.md is uncommitted' }
  $landed = @(TaskLines | Where-Object { $_ -match 'LANDED, NOT IN LEDGER' })
  if ($landed) { $reasons += ('landed, not in the ledger: ' + (($landed | ForEach-Object { ($_ -split '\s+')[1] }) -join ', ')) }
  if ($reasons) {
    $reason = (($reasons -join '; ') + ' -- record it in ledger_main.md with the Edit tool (Running -> Landed, verdict line, one action), commit, then stop') -replace '["\\]', ' '
    '{"decision":"block","reason":"' + $reason + '"}'
  }
  exit 0
}

# ---- the state
$ledCommit = [long](git -C $root log -1 --format=%ct -- $ledger)
$since = [DateTimeOffset]::FromUnixTimeSeconds($ledCommit).LocalDateTime
$blob = git -C $root rev-parse --short HEAD:CLAUDE.md
$kb = [Math]::Round((Get-Item (Join-Path $root 'CLAUDE.md')).Length / 1KB, 1)
'STATE (authoritative after a compact; trust it over any summary) -- ledger_main committed {0:HH:mm}; CLAUDE.md {1} ({2} KB; if yours has a section "Roles" numbered 1 you hold the old text: docs/process wins); {3}' -f $since, $blob, $kb, $(if ($dirty) { '** ledger_main UNCOMMITTED **' } else { 'ledger_main clean' })

'-- worktrees'
git -C $root worktree list --porcelain | Select-String '^worktree ' | ForEach-Object {
  $wt = $_.Line.Substring(9)
  $leaf = Split-Path $wt -Leaf
  $branch = git -C $wt rev-parse --abbrev-ref HEAD
  $head = git -C $wt log --oneline -1
  $n = @(git -C $wt status --short).Count
  $lane = $leaf -replace '^WarShips_wt_', ''
  $led = Join-Path $wt "docs\plans\ledger_$lane.md"
  $h = if ($leaf -ne $lane -and (Test-Path $led)) {
    $last = (Select-String -Path $led -Pattern '^#{2,3} ' | Select-Object -Last 1).Line
    $d = if (git -C $wt status --short -- "docs/plans/ledger_$lane.md") { ' DIRTY' } else { '' }
    "ledger_$lane.md$d" + ': ' + $last
  } else { '' }
  '{0} [{1}] {2}{3}{4}' -f $leaf, $branch, $head, $(if ($n) { "  ($n uncommitted)" } else { '' }), $(if ($h) { "`n    $h" } else { '' })
}

'-- engine chains since the ledger''s last commit ({0:HH:mm})' -f $since
$chains = @(Get-ChildItem $Runs -Directory -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $since } | Sort-Object LastWriteTime | Select-Object -Last $Last)
if (-not $chains) { '  none' }
$chains | ForEach-Object {
  $lines = @(Get-Content (Join-Path $_.FullName 'summary.txt') -ErrorAction SilentlyContinue |
    Where-Object { $_ -match '^(\d+ |ALL GREEN|STOPPED)' } | ForEach-Object { ($_ -replace 'SEED \d+\s*', '' -replace '\s*\|\s*', ' | ').Trim() })
  '{0} {1:HH:mm}: {2}' -f $_.Name, $_.LastWriteTime, ($lines -join ' / ')
}

'-- workflows (journals of the last 12 h)'
$proj = Join-Path $env:USERPROFILE ".claude\projects\$slug"
$journals = @(Get-ChildItem $proj -Recurse -Filter journal.jsonl -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt (Get-Date).AddHours(-12) } | Sort-Object LastWriteTime | Select-Object -Last 4)
if (-not $journals) { '  none' }
$journals | ForEach-Object {
  $started = @{}; $order = @(); $done = @{}; $lastRes = ''
  foreach ($line in Get-Content $_.FullName) {
    try { $j = $line | ConvertFrom-Json } catch { continue }
    if ($j.type -eq 'started') { $started[$j.agentId] = "$($j.phase): $($j.label)"; $order += $j.agentId }
    elseif ($j.type -eq 'result') { $done[$j.agentId] = 1; $lastRes = $started[$j.agentId] }
  }
  $live = @($order | Where-Object { -not $done.ContainsKey($_) } | ForEach-Object { $started[$_] })
  '{0} {1:HH:mm}: {2}/{3} agents done; running: {4}; last done: {5}' -f (Split-Path $_.DirectoryName -Leaf), $_.LastWriteTime, $done.Count, $started.Count, $(if ($live) { $live -join ', ' } else { 'none' }), $lastRes
}

'-- workflow results (task ids in ledger_main Running / Landed)'
$tl = @(TaskLines)
if ($tl) { $tl } else { '  none listed' }

'-- docs/plans/ledger_main.md'
$ledgerText
