# Windows port of run.sh -- headless runtime check: boots the REAL engine, drives real
# input, and runs a host and TWO guests (three players) against each other over localhost.
# Catches what a typecheck cannot: null nodes, bad RPC paths, stale peer ids, scene-change
# ordering.
#
#   powershell -ExecutionPolicy Bypass -File tools\smoketest\run.ps1 <godot mono win64 exe>
#
# Differences from the Linux original, all forced by the platform:
#   * Uses the _console.exe variant. The plain Godot .exe on Windows is a GUI-subsystem
#     binary and does not write to stdout, so every check would be invisible.
#   * user:// is %APPDATA%\Godot\app_userdata\, not ~/.local/share/godot/app_userdata/.
#   * Output is captured to a file and filtered afterwards rather than piped live through
#     grep. A killed run keeps whatever it had already written, which is what the
#     --line-buffered plumbing was protecting.
# SmokeTest.cs.txt itself needs no changes: it contains no POSIX paths.

param([string]$Godot)

$ErrorActionPreference = 'Stop'

# -Godot is optional now: find-godot.ps1 resolves it from an explicit path, WARSHIPS_GODOT,
# local.config.ps1, or a search of the usual places.
if (-not $Godot -or -not (Test-Path $Godot)) {
  $Godot = & (Join-Path $PSScriptRoot '..\find-godot.ps1') -Godot $Godot
  if ($LASTEXITCODE -ne 0 -or -not $Godot) { exit 2 }
}
$Godot = (Resolve-Path $Godot).Path

# The GUI binary prints nothing; swap to the console build beside it.
if ($Godot -notmatch '_console\.exe$') {
  $c = $Godot -replace '\.exe$', '_console.exe'
  if (Test-Path $c) { $Godot = $c }
  else { Write-Host "NO CONSOLE BINARY beside $Godot -- headless output would be invisible."; exit 2 }
}

$nupkgs = Join-Path (Split-Path $Godot) 'GodotSharp\Tools\nupkgs'
if (-not (Test-Path $nupkgs)) { Write-Host "no nupkgs folder at $nupkgs"; exit 2 }
$src = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$W = Join-Path $env:TEMP 'warships_smoke'

# One fixed scratch folder, so two runs cannot overlap -- the second would delete the first's
# files mid-run. Say so plainly: the bare failure is a Remove-Item error on a .err file, which
# reads like a broken harness rather than "something else is already running".
if (Test-Path $W) {
  try { Remove-Item $W -Recurse -Force -ErrorAction Stop }
  catch {
    Write-Host "CANNOT CLEAR $W -- another smoke run is probably still going."
    Write-Host "These runs share one scratch folder and must be run one at a time."
    exit 2
  }
}
New-Item -ItemType Directory -Path $W -Force | Out-Null
# /.godot, bin and obj are build output; copying them just makes the import stale.
robocopy $src $W /E /XD .godot .git bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'SmokeTest.cs.txt') (Join-Path $W 'scripts\_Test.cs') -Force

$pg = Join-Path $W 'project.godot'
$t = Get-Content $pg -Raw
$t = $t -replace '(?m)^Net="\*res://scripts/Net\.cs"', "`$0`n_Test=`"*res://scripts/_Test.cs`""
# Its own user:// folder: the test creates and DELETES characters, and must never
# touch real ones. Wiped each run so migration and first-run paths are exercised.
$t = $t -replace '(?m)^config/name="Warships"', 'config/name="WarshipsSmoke"'
Set-Content $pg $t -Encoding UTF8 -NoNewline

$check = Get-Content $pg -Raw
if ($check -notmatch 'config/name="WarshipsSmoke"') { Write-Host "could not isolate user data; refusing to run"; exit 2 }
if ($check -notmatch '_Test="\*res://scripts/_Test\.cs"') { Write-Host "could not register the test autoload; refusing to run"; exit 2 }

$ud = Join-Path $env:APPDATA 'Godot\app_userdata\WarshipsSmoke'
if (Test-Path $ud) { Remove-Item $ud -Recurse -Force }

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /><add key="godot" value="$nupkgs" /></packageSources></configuration>
"@ | Set-Content (Join-Path $W 'nuget.config') -Encoding UTF8

Push-Location $W
try {
  # a test that does not compile tests nothing: stop here, loudly
  $buildLog = Join-Path $W 'build.log'
  & dotnet build -v q -nologo *> $buildLog
  $buildFailed = ($LASTEXITCODE -ne 0)
  $errs = Get-Content $buildLog | Select-String -Pattern ' error ' | ForEach-Object { ($_.Line -replace '.*[\\/]scripts[\\/]', '') } | Sort-Object -Unique
  if ($buildFailed -or $errs) {
    $errs | Select-Object -First 20 | ForEach-Object { Write-Host $_ }
    Write-Host "SMOKE TEST FAILED (the build failed)"; exit 1
  }
  Get-Content $buildLog | Select-String -Pattern 'Warning\(s\)|Error\(s\)' | ForEach-Object { Write-Host $_.Line.Trim() }

  # NOTE: PowerShell variable names are case-insensitive, so these cannot be $F/$N --
  # a `foreach ($f in ...)` elsewhere in this script silently overwrote the filter with
  # a file path, and the run died parsing it as a regex.
  $keepRe = 'PASS|FAIL|DONE|Exception|   at |ERROR: [^B]|^  [a-z]'
  # Expected engine chatter, not failures: allocator notes, and Godot's own report that
  # no UPnP router exists (the game falls back and says so).
  $dropRe = 'RID alloc|PagedAlloc|find any UPNPDevices'

  function Start-Run {
    param([string[]]$GodotArgs, [string]$Tag, [int]$TimeoutSec)
    $o = Join-Path $W "$Tag.out"; $e = Join-Path $W "$Tag.err"
    $p = Start-Process -FilePath $Godot -ArgumentList $GodotArgs -WorkingDirectory $W `
         -NoNewWindow -PassThru -RedirectStandardOutput $o -RedirectStandardError $e
    [pscustomobject]@{ Proc = $p; Tag = $Tag; Out = $o; Err = $e; Timeout = $TimeoutSec }
  }
  function Complete-Run {
    param($R, [string]$Prefix)
    if (-not $R.Proc.WaitForExit($R.Timeout * 1000)) {
      try { $R.Proc.Kill() } catch {}
      $R.Proc.WaitForExit(5000) | Out-Null
    }
    $lines = @()
    foreach ($stream in @($R.Out, $R.Err)) {
      if (Test-Path $stream) { $lines += Get-Content $stream -ErrorAction SilentlyContinue }
    }
    # -cmatch, not -match: PowerShell matches case-insensitively by default but grep -E
    # does not, and a case-blind "FAIL" also matches every run's own "fails=0" summary.
    $keep = $lines | Where-Object { $_ -cmatch $keepRe -and $_ -cnotmatch $dropRe } | ForEach-Object { "$Prefix $_" }
    $log = Join-Path $W "$($R.Tag).log"
    Set-Content $log ($keep -join "`n") -Encoding UTF8
    $keep
  }

  & $Godot --headless --import --path $W *> (Join-Path $W 'import.log')

  $all = @()
  # fixed 60 fps: identical frame timing every run, so the DPS checks are exact
  $all += Complete-Run (Start-Run @('--headless','--fixed-fps','60','--path',$W,'--','solo') 'solo' 1200) '[solo] '

  $host1 = Start-Run @('--headless','--path',$W,'--','host')   'host'   60
  Start-Sleep -Milliseconds 500
  $g2 = Start-Run @('--headless','--path',$W,'--','guest2') 'guest2' 60
  $g1 = Start-Run @('--headless','--path',$W,'--','guest')  'guest'  60
  $all += Complete-Run $g1    '[guest] '
  $all += Complete-Run $host1 '[host]  '
  $all += Complete-Run $g2    '[third] '

  # the dedicated two-player arena run: after the three-player run, on its own port
  $ah = Start-Run @('--headless','--path',$W,'--','ahost')  'ahost'  60
  Start-Sleep -Milliseconds 500
  $ag = Start-Run @('--headless','--path',$W,'--','aguest') 'aguest' 60
  $all += Complete-Run $ag '[aguest]'
  $all += Complete-Run $ah '[ahost] '

  $all | ForEach-Object { Write-Host $_ }
  $bad = @($all | Where-Object { $_ -cmatch 'FAIL|Exception|ERROR' }).Count
  $done = @($all | Where-Object { $_ -cmatch 'DONE' }).Count
  if ($bad -gt 0 -or $done -ne 6) {
    Write-Host "SMOKE TEST FAILED ($bad problems, $done/6 runs finished)"; exit 1
  }
  Write-Host "SMOKE TEST PASSED"
} finally { Pop-Location }
