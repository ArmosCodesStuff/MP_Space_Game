# rungs.ps1 -Tree <checkout> -Tag <name> -Steps <chain> [-Out <dir>]
# The one way to start an engine run (CLAUDE.md section 8). Steps: quick, solo, solo@<seed>, six, six@<seed>,
# screens, wan, bar. Runs them in order in <Tree> (required, so a lane is never tested as version-l by
# mistake), stops at the first red, and writes <Out>\summary.txt plus one log per step (default Out:
# %TEMP%\warships_rungs\<Tag>, cleared first: a tag is used once). Engine steps wait for the engine: an
# exclusive handle on %TEMP%\warships_engine.lock, released by the OS when this process ends, so a dead run
# never holds it. Run it as its own process: powershell -NoProfile -ExecutionPolicy Bypass -File tools\rungs.ps1
param([Parameter(Mandatory)][string]$Tree, [Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string[]]$Steps, [string]$Out)
$ErrorActionPreference = 'Continue'
$Steps = @($Steps | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
if (-not $Out) { $Out = Join-Path $env:TEMP "warships_rungs\$Tag" }
if (Test-Path $Out) { Remove-Item -Recurse -Force (Join-Path $Out '*') }
New-Item -ItemType Directory -Force $Out | Out-Null
$sum = Join-Path $Out 'summary.txt'
Set-Location $Tree
"== $Tag at $(git -C $Tree rev-parse --short HEAD) $(Get-Date -Format T)" | Tee-Object -Append $sum
$i = 0
$lock = $null
foreach ($s in $Steps) {
  $i++
  $log = Join-Path $Out ("{0}_{1}.log" -f $i, ($s -replace '[^\w]', '_'))
  if ($s -ne 'quick' -and -not $lock) {
    $lockPath = Join-Path $env:TEMP 'warships_engine.lock'
    $waited = Get-Date
    while (-not $lock) { try { $lock = [System.IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') } catch { Start-Sleep 15 } }
    $w = [int]((Get-Date) - $waited).TotalSeconds
    if ($w -gt 5) { "   (waited ${w}s for the engine)" | Tee-Object -Append $sum }
  }
  $t0 = Get-Date
  $name = $s
  if ($s -match '^solo@(\d+)$') { & "$Tree\tools\smoketest\run.ps1" -Solo -Seed $Matches[1] *> $log; $name = 'seeded' }
  elseif ($s -match '^six@(\d+)$') { & "$Tree\tools\smoketest\run.ps1" -Seed $Matches[1] *> $log; $name = 'seeded' }
  switch ($name) {
    'quick'   { & "$Tree\verify.ps1" -Quick *> $log }
    'solo'    { & "$Tree\tools\smoketest\run.ps1" -Solo *> $log }
    'six'     { & "$Tree\tools\smoketest\run.ps1" *> $log }
    'wan'     { & "$Tree\tools\smoketest\run.ps1" -Wan *> $log }
    'screens' { & "$Tree\tools\screens\run.ps1" *> $log }
    'bar'     { & "$Tree\verify.ps1" -Update *> $log }
    'seeded'  { }
    default   { "unknown step '$s'" | Tee-Object -Append $sum; exit 2 }
  }
  $code = $LASTEXITCODE
  $fails = @(Select-String -Path $log -Pattern '\]\s+FAIL |FAILED|LINT: [1-9]|Exception' | Select-Object -First 12 | ForEach-Object { $t = $_.Line.Trim(); '   ' + $t.Substring(0, [Math]::Min(300, $t.Length)) })
  $seed = (Select-String -Path $log -Pattern 'SEED \d+' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Value })
  $verdict = (Select-String -Path $log -Pattern 'SMOKE TEST.*(PASSED|FAILED)|ALL CHECKS PASSED|FAILED: |LINT: \d+' | Select-Object -Last 1 | ForEach-Object { $_.Line.Trim() })
  "$i $s exit=$code $([int]((Get-Date) - $t0).TotalSeconds)s $seed | $verdict" | Tee-Object -Append $sum
  $fails | Tee-Object -Append $sum
  if ($code -ne 0) { "STOPPED at $i $s" | Tee-Object -Append $sum; exit $code }
}
"ALL GREEN" | Tee-Object -Append $sum
