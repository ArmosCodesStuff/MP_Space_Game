# merge_lane.ps1 -Main <main tree> -Branch wt/<key> -Name "<merge name>" [-Rows "<row> || <row>"] [-LaneLedger docs/plans/ledger_<key>.md]
# The mechanical half of tester.md's merge (steps 1 and 2), so a merge agent spends one call, not 30-130 turns (fable_retro_tokens.md
# change 6). The judgment (a Checks: line naming the task's checks; a rewritten check against its expect) stays with the agent, BEFORE
# this call. Step 1: if version-l is not an ancestor of the branch, merge version-l into the branch in a TEMPORARY worktree (never the
# lane's tree: a fixer may be live there), typecheck + verify -Quick there, commit. Step 2: version-l clean and unlocked (waits up to
# 15 min), git merge --no-ff --no-commit, the rows appended to docs/plans/ledger_test.md and the lane ledger removed in the same
# commit. Prints ONE JSON line {merged, head, note}; a conflict aborts and names it (the agent resolves in its own temporary worktree
# and calls again). No push.
param([string]$Main, [string]$Branch, [string]$Name, [string]$Rows = '', [string]$LaneLedger = '', [string]$Tree = '')
$ErrorActionPreference = 'Continue'
function Done($m, $h, $n) { [pscustomobject]@{ merged = $m; head = $h; note = $n } | ConvertTo-Json -Compress; exit 0 }
function Git($dir, [string[]]$a) { $o = & git -C $dir @a 2>&1; return (($o | ForEach-Object { "$_" }) -join "`n") }
function Clip($s) { if ($s.Length -gt 250) { $s.Substring(0, 250) } else { $s } }
if (-not $Main -or -not $Branch -or -not $Name) { Done $false '' 'usage: -Main -Branch -Name [-Rows] [-LaneLedger]' }

# 1. the branch carries version-l. A branch checked out in a lane tree cannot be added as a second worktree (git refuses), so step 1
# runs IN the lane tree when -Tree names it (the lane's agent has returned by then: the workflow holds the pool slot until the
# merge is done); without -Tree a temporary worktree is used (a branch checked out nowhere).
& git -C $Main merge-base --is-ancestor version-l $Branch 2>$null
if ($LASTEXITCODE -ne 0) {
  $tmp = if ($Tree) { $Tree } else { Join-Path (Split-Path $Main -Parent) ("WarShips_wt_merge_" + [guid]::NewGuid().ToString('N').Substring(0, 8)) }
  if ($Tree) {
    $cur = Git $Tree @('rev-parse', '--abbrev-ref', 'HEAD')
    if ($cur.Trim() -ne $Branch) { Done $false '' "the lane tree $Tree has $cur checked out, not $Branch" }
    if (Git $Tree @('status', '--short')) { Done $false '' "the lane tree $Tree is dirty" }
  } else {
    $w = Git $Main @('worktree', 'add', '-q', $tmp, $Branch)
    if ($LASTEXITCODE -ne 0) { Done $false '' ("worktree add: " + (Clip $w)) }
  }
  try {
    $m = Git $tmp @('merge', '--no-edit', 'version-l')
    if ($LASTEXITCODE -ne 0) { Git $tmp @('merge', '--abort') | Out-Null; Done $false '' ("conflict merging version-l into ${Branch}: " + (Clip $m)) }
    $tc = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tmp 'typecheck\typecheck.ps1') 2>&1 | Select-Object -Last 1
    if ("$tc" -notmatch '0 errors') { Done $false '' ("typecheck after merging version-l into the branch: " + (Clip "$tc")) }
    Push-Location $tmp
    $q = & powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1 -Quick 2>&1 | Select-Object -Last 1
    Pop-Location
    if ("$q" -notmatch 'ALL CHECKS PASSED') { Done $false '' ("verify -Quick after merging version-l into the branch: " + (Clip "$q")) }
  } finally {
    if (-not $Tree) { Git $Main @('worktree', 'remove', '--force', $tmp) | Out-Null }
  }
}

# 2. version-l: clean and unlocked, then one merge commit with the rows and without the lane ledger
for ($i = 0; $i -lt 15; $i++) {
  $dirty = Git $Main @('status', '--short')
  $lock = (Test-Path (Join-Path $Main '.git\index.lock')) -or (Test-Path (Join-Path $Main '.git\MERGE_HEAD'))
  if (-not $dirty -and -not $lock) { break }
  if ($i -eq 14) { Done $false '' ("main tree busy for 15 min: dirty=[" + (Clip $dirty) + "] lock=$lock") }
  Start-Sleep 60
}
$m = Git $Main @('merge', '--no-ff', '--no-commit', $Branch)
if ($LASTEXITCODE -ne 0) { Git $Main @('merge', '--abort') | Out-Null; Done $false '' ("conflict merging ${Branch} into version-l: " + (Clip $m)) }
$lt = Join-Path $Main 'docs\plans\ledger_test.md'
$utf8 = New-Object System.Text.UTF8Encoding($false)
if (-not (Test-Path $lt)) { [IO.File]::WriteAllText($lt, "| round | task | kind | proved | traces to | commit |`n|---|---|---|---|---|---|`n", $utf8) }
if ($Rows) {
  $text = [IO.File]::ReadAllText($lt)
  if (-not $text.EndsWith("`n")) { $text += "`n" }
  [IO.File]::WriteAllText($lt, $text + (($Rows -split ' \|\| ') -join "`n") + "`n", $utf8)
}
Git $Main @('add', 'docs/plans/ledger_test.md') | Out-Null
if ($LaneLedger -and (Test-Path (Join-Path $Main $LaneLedger))) { Git $Main @('rm', '-q', '-f', $LaneLedger) | Out-Null }
$msg = Join-Path $env:TEMP ("merge_" + [guid]::NewGuid().ToString('N').Substring(0, 8) + ".txt")
[IO.File]::WriteAllText($msg, "Merge $Name`n`nCo-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`n", $utf8)
$c = Git $Main @('commit', '-q', '-F', $msg)
if ($LASTEXITCODE -ne 0) { Done $false '' ("commit: " + (Clip $c)) }
Done $true (Git $Main @('log', '--oneline', '-1')) 'merged'
