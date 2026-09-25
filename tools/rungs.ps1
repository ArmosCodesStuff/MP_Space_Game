# rungs.ps1 -Tree <checkout> -Tag <name> -Steps <chain> [-Out <dir>] [-Slot <n>] [-Slots <n>]
# The one way to start an engine run (CLAUDE.md section 8). Steps: quick, solo, solo@<seed>, six, six@<seed>,
# screens, wan, bar. Runs them in order in <Tree> (required, so a lane is never tested as version-l by
# mistake), stops at the first red, and writes <Out>\summary.txt plus one log per step (default Out:
# %TEMP%\warships_rungs\<Tag>, cleared first: a tag is used once). Engine steps wait for the engine: an
# exclusive handle on an engine-slot lock file, released by the OS when this process ends, so a dead run
# never holds it. Run it as its own process: powershell -NoProfile -ExecutionPolicy Bypass -File tools\rungs.ps1
#
# ENGINE SLOTS (2-4 chains at once on one PC, from different trees, without sharing a folder, a
# user:// dir, a port or a lock -- see tools\smoketest\run.ps1 -Slot and tools\screens\run.ps1 -Slot).
# Slot 0 is BYTE-FOR-BYTE today's behaviour: %TEMP%\warships_engine.lock, no env overrides, no
# -Slot passed downstream -- older copies of rungs.ps1 running in other lanes still take that same
# file, so slot 0 keeps interlocking with them. Slot n >= 1 gets its own engine lock
# (%TEMP%\warships_engine_<n>.lock), its own %TEMP%/%APPDATA%/%LOCALAPPDATA% under
# <real TEMP>\warships_slot<n>\{temp,appdata,localappdata} (created once, never wiped -- a slot keeps
# its own Godot editor settings and import cache across runs), and -Slot n passed to the harness so
# its ports and scratch folders shift too. A tree only supports slot >= 1 when its own
# tools\smoketest\run.ps1 declares a -Slot parameter; a chain containing bar or wan always runs on
# slot 0 (the bar is the gate everyone waits on anyway, and -Wan refuses off slot 0 downstream).
# -Slots picks how many slots (0..Slots-1) auto-assignment tries: default 4 (owner, 2026-09-25), at
# most 4. -Slot picks one slot explicitly and waits only for it. The engine slot (and a slot >= 1's env
# swap) is taken at the first step that is not quick, so a leading quick step never holds an engine lock.
param([Parameter(Mandatory)][string]$Tree, [Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string[]]$Steps, [string]$Out, [int]$Slot = -1, [int]$Slots = 4)
$ErrorActionPreference = 'Continue'
# At most 4 slots (owner, 2026-09-25): each slot shifts every port by 100, and four bands are what was proven.
$Slots = [Math]::Min($Slots, 4)
if ($Slot -gt 3) { "slot $Slot is out of range 0..3"; exit 2 }

# The REAL %TEMP%, captured before anything below might repoint $env:TEMP for a non-zero slot: the
# out dir, summary.txt and every lock this process itself opens stay under it regardless of slot.
$realTemp = $env:TEMP

$Steps = @($Steps | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
if (-not $Out) { $Out = Join-Path $realTemp "warships_rungs\$Tag" }
if (Test-Path $Out) { Remove-Item -Recurse -Force (Join-Path $Out '*') }
New-Item -ItemType Directory -Force $Out | Out-Null
$sum = Join-Path $Out 'summary.txt'
# UTF-8, not PowerShell 5.1's default UTF-16LE for Tee-Object -Append: bash grep/cat and an agent's
# poll loop never match UTF-16, so a finished run could wait forever to be noticed. Add-Content
# appends UTF-8 by hand; the console still gets every line via Write-Host.
function Log([string]$line) {
  Write-Host $line
  Add-Content -Path $sum -Value $line -Encoding UTF8
}

Set-Location $Tree

# A chain containing bar or wan always runs on slot 0 -- the bar is the gate every lane waits on, and
# -Wan refuses off slot 0 downstream anyway.
$forceZero = @($Steps | Where-Object { $_ -eq 'bar' -or $_ -eq 'wan' }).Count -gt 0
$slotsSupported = $false
try {
  $rp = Get-Command (Join-Path $Tree 'tools\smoketest\run.ps1') -ErrorAction Stop
  $slotsSupported = $rp.Parameters.ContainsKey('Slot')
} catch {}

# Per-tree lock: two chains must never touch the SAME tree at the same time -- the quick step writes
# repo-root obj/, bin/ and .editorconfig. Keyed by the resolved, lower-cased -Tree path so two tags
# pointed at one tree still serialize. Held for the whole chain, opened before step 1.
$treeKey = ([System.BitConverter]::ToString(
  [System.Security.Cryptography.MD5]::Create().ComputeHash(
    [System.Text.Encoding]::UTF8.GetBytes((Resolve-Path $Tree).Path.ToLowerInvariant())
  )
) -replace '-', '').Substring(0, 12).ToLowerInvariant()
$treeLockPath = Join-Path $realTemp "warships_tree_$treeKey.lock"
$treeLock = $null
$treeWaited = Get-Date
while (-not $treeLock) { try { $treeLock = [System.IO.File]::Open($treeLockPath, 'OpenOrCreate', 'ReadWrite', 'None') } catch { Start-Sleep 5 } }
$tw = [int]((Get-Date) - $treeWaited).TotalSeconds
if ($tw -gt 5) { Log "   (waited ${tw}s for the tree lock)" }

# The engine slot. Slot 0's lock path and behaviour are UNCHANGED from before slots existed. Taken
# lazily by Enter-EngineSlot at the first step that is not quick, as the runner before slots did: a
# leading quick step never waits for or holds an engine lock; once taken, it is held to the end.
function Try-EngineLock([int]$n) {
  $p = if ($n -eq 0) { Join-Path $realTemp 'warships_engine.lock' } else { Join-Path $realTemp "warships_engine_$n.lock" }
  try { [System.IO.File]::Open($p, 'OpenOrCreate', 'ReadWrite', 'None') } catch { $null }
}

$engineLock = $null
$assignedSlot = 0
function Enter-EngineSlot {
  if ($forceZero -or -not $slotsSupported -or $Slots -le 1) {
    $script:assignedSlot = 0
  } elseif ($Slot -ge 0) {
    $script:assignedSlot = $Slot
  } else {
    $script:assignedSlot = -1
  }

  $engineWaited = Get-Date
  if ($script:assignedSlot -ge 0) {
    # a specific slot: wait for that one only
    while (-not $script:engineLock) { $script:engineLock = Try-EngineLock $script:assignedSlot; if (-not $script:engineLock) { Start-Sleep 15 } }
  } else {
    # auto: try slots 0..Slots-1 in order, first lock that opens wins, else sleep and retry the sweep
    while (-not $script:engineLock) {
      for ($n = 0; $n -lt $Slots -and -not $script:engineLock; $n++) {
        $script:engineLock = Try-EngineLock $n
        if ($script:engineLock) { $script:assignedSlot = $n }
      }
      if (-not $script:engineLock) { Start-Sleep 15 }
    }
  }
  $ew = [int]((Get-Date) - $engineWaited).TotalSeconds
  if ($ew -gt 5) { Log "   (waited ${ew}s for engine slot $script:assignedSlot)" }

  # Slot n >= 1: isolate this process's TEMP/APPDATA/LOCALAPPDATA so a slot's Godot editor settings,
  # import cache and scratch folders never collide with slot 0 or another slot -- created once, never
  # wiped, so a slot keeps its cache warm across runs. This covers every later step, dotnet included:
  # NuGet's global packages folder is under USERPROFILE and untouched, the harness nuget.config files
  # clear their sources down to the local Godot feed, and only NuGet's user config and HTTP cache
  # become per slot.
  if ($script:assignedSlot -ge 1) {
    $slotRoot = Join-Path $realTemp "warships_slot$script:assignedSlot"
    $slotTemp = Join-Path $slotRoot 'temp'
    $slotAppData = Join-Path $slotRoot 'appdata'
    $slotLocalAppData = Join-Path $slotRoot 'localappdata'
    New-Item -ItemType Directory -Force $slotTemp, $slotAppData, $slotLocalAppData | Out-Null
    $env:TEMP = $slotTemp
    $env:TMP = $slotTemp
    $env:APPDATA = $slotAppData
    $env:LOCALAPPDATA = $slotLocalAppData
  }
  Log "   engine slot $script:assignedSlot"
}

Log "== $Tag at $(git -C $Tree rev-parse --short HEAD) $(Get-Date -Format T)"
$i = 0
try {
foreach ($s in $Steps) {
  $i++
  if ($s -ne 'quick' -and -not $engineLock) { Enter-EngineSlot }
  $log = Join-Path $Out ("{0}_{1}.log" -f $i, ($s -replace '[^\w]', '_'))
  $t0 = Get-Date
  $name = $s
  $slotArgs = if ($assignedSlot -ge 1) { @('-Slot', $assignedSlot) } else { @() }
  $screensSlotArgs = @()
  if ($assignedSlot -ge 1) {
    try {
      $sp = Get-Command (Join-Path $Tree 'tools\screens\run.ps1') -ErrorAction Stop
      if ($sp.Parameters.ContainsKey('Slot')) { $screensSlotArgs = @('-Slot', $assignedSlot) }
    } catch {}
  }
  if ($s -match '^solo@(\d+)$') { & "$Tree\tools\smoketest\run.ps1" -Solo -Seed $Matches[1] @slotArgs *> $log; $name = 'seeded' }
  elseif ($s -match '^six@(\d+)$') { & "$Tree\tools\smoketest\run.ps1" -Seed $Matches[1] @slotArgs *> $log; $name = 'seeded' }
  switch ($name) {
    'quick'   { & "$Tree\verify.ps1" -Quick *> $log }
    'solo'    { & "$Tree\tools\smoketest\run.ps1" -Solo @slotArgs *> $log }
    'six'     { & "$Tree\tools\smoketest\run.ps1" @slotArgs *> $log }
    'wan'     { & "$Tree\tools\smoketest\run.ps1" -Wan *> $log }
    'screens' { & "$Tree\tools\screens\run.ps1" @screensSlotArgs *> $log }
    'bar'     { & "$Tree\verify.ps1" -Update *> $log }
    'seeded'  { }
    default   { Log "unknown step '$s'"; exit 2 }
  }
  $code = $LASTEXITCODE
  # <i>_<step>.fails.txt (UTF-8, no BOM): EVERY verdict line of the step in log order (PASS, FAIL, FAIL LANE,
  # Exception, LINT, SEED, the verdict), with a count header, so no reader decodes the UTF-16 log that *> writes
  # and no cap hides a red. The summary keeps its first 12 FAIL lines and gains the counts.
  $text = [IO.File]::ReadAllText($log)   # detects the UTF-16 BOM
  $vl = @([regex]::Matches($text, '(?m)^[^\r\n]*(\]\s+(PASS|FAIL) |FAIL LANE|LINT: \d+|Exception|SEED \d+|SMOKE TEST.*(PASSED|FAILED)|ALL CHECKS PASSED|FAILED: )[^\r\n]*') | ForEach-Object { $_.Value.Trim() })
  $nFail = @($vl | Where-Object { $_ -match '\]\s+FAIL ' }).Count
  $nLane = @($vl | Where-Object { $_ -match 'FAIL LANE' }).Count
  $nExc  = @($vl | Where-Object { $_ -match 'Exception' }).Count
  $nPass = @($vl | Where-Object { $_ -match '\]\s+PASS ' }).Count
  $failsFile = Join-Path $Out ("{0}_{1}.fails.txt" -f $i, ($s -replace '[^\w]', '_'))
  [IO.File]::WriteAllText($failsFile, (("FAIL $nFail / FAIL LANE $nLane / Exception $nExc / PASS $nPass -- $s at $(Split-Path $Tree -Leaf)`n" + ($vl -join "`n") + "`n")), (New-Object System.Text.UTF8Encoding $false))
  $fails = @($vl | Where-Object { $_ -match '\]\s+FAIL |FAIL LANE|FAILED|LINT: [1-9]|Exception' } | Select-Object -First 12 | ForEach-Object { '   ' + $_.Substring(0, [Math]::Min(300, $_.Length)) })
  $seed = (Select-String -Path $log -Pattern 'SEED \d+' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Value })
  $verdict = (Select-String -Path $log -Pattern 'SMOKE TEST.*(PASSED|FAILED)|ALL CHECKS PASSED|FAILED: |LINT: \d+' | Select-Object -Last 1 | ForEach-Object { $_.Line.Trim() })
  Log "$i $s exit=$code $([int]((Get-Date) - $t0).TotalSeconds)s $seed fails=$nFail lanes=$nLane exc=$nExc | $verdict"
  $fails | ForEach-Object { Log $_ }
  if ($code -ne 0) { Log "STOPPED at $i $s"; exit $code }
}
Log "ALL GREEN"
} finally {
  if ($engineLock) { $engineLock.Dispose() }
  if ($treeLock) { $treeLock.Dispose() }
}
