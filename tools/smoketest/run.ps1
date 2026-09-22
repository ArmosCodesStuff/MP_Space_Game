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

# -Solo runs ONLY the single-player scenario: one engine instead of six, for the loop while a
# change is being built. It is a PARTIAL run and says so in every line it prints, because the one
# thing it cannot cover is the thing this harness exists for -- host and guests disagreeing.
# -Wan runs ONLY the multiplayer scenarios, with every guest joining through tools\smoketest\wan.py:
# an internet path of 90 ms each way, +/- 25 ms of jitter and 2% of datagrams lost -- a friend in
# another part of the country on an ordinary connection, rather than a second process on the same
# machine. The checks are the same ones; what changes is everything they depend on arriving late,
# out of order, or twice.
# -Seed <n> repeats a run's VARIED GEOMETRY exactly: every check that places something at an
# arbitrary spot or angle draws it from the seed the run prints at the top of its log (SEED n).
# Without it each run picks its own, so a check tied to one placement fails on the run that moves it.
# -Fly is not a check run: it is a PILOT. One engine, every class taken out in clear space --
# flown, fired, its whole bar pressed with a target selected, and stood in front of a wave -- and
# what it measured printed as FLY and ISSUE lines. For "what is actually wrong with this class",
# which assertions cannot answer because they only know what we thought to ask.
param([string]$Godot, [switch]$Solo, [switch]$Wan, [switch]$Fly, [string]$Seed)

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
  # SEED is kept: a failure must come back with the number that reproduces its geometry (-Seed n).
  $keepRe = 'PASS|FAIL|DONE|SEED |FLY|ISSUE|Exception|   at |ERROR: [^B]|^  [a-z]|Fatal error'
  # Expected engine chatter, not failures: allocator notes, and Godot's own report that
  # no UPnP router exists (the game falls back and says so).
  $dropRe = 'RID alloc|PagedAlloc'

  function Start-Run {
    param([string[]]$GodotArgs, [string]$Tag, [int]$TimeoutSec)
    $o = Join-Path $W "$Tag.out"; $e = Join-Path $W "$Tag.err"
    $p = Start-Process -FilePath $Godot -ArgumentList $GodotArgs -WorkingDirectory $W `
         -NoNewWindow -PassThru -RedirectStandardOutput $o -RedirectStandardError $e
    $null = $p.Handle      # without touching Handle now, .NET reports no ExitCode after the exit
    [pscustomobject]@{ Proc = $p; Tag = $Tag; Out = $o; Err = $e; Timeout = $TimeoutSec }
  }
  function Complete-Run {
    param($R, [string]$Prefix)
    $killed = $false
    if (-not $R.Proc.WaitForExit($R.Timeout * 1000)) {
      try { $R.Proc.Kill() } catch {}
      $R.Proc.WaitForExit(5000) | Out-Null
      $killed = $true
    }
    $lines = @()
    foreach ($stream in @($R.Out, $R.Err)) {
      if (Test-Path $stream) { $lines += Get-Content $stream -ErrorAction SilentlyContinue }
    }
    # -cmatch, not -match: PowerShell matches case-insensitively by default but grep -E
    # does not, and a case-blind "FAIL" also matches every run's own "fails=0" summary.
    $keep = @($lines | Where-Object { $_ -cmatch $keepRe -and $_ -cnotmatch $dropRe } | ForEach-Object { "$Prefix $_" })
    # A CRASH IS A FAILURE. The test quits with its failure count, so any other exit code means
    # the process did not get to decide how it ended. This was invisible: a crash at exit also
    # cut off the engine's leak report, so the one run that crashed was the one that "passed".
    $code = $R.Proc.ExitCode
    if (-not $killed -and $code -lt 0) { $keep += "$Prefix FAIL: the process crashed (exit code 0x{0:X8})" -f $code }
    $log = Join-Path $W "$($R.Tag).log"
    Set-Content $log ($keep -join "`n") -Encoding UTF8
    $keep
  }

  # the seed every role runs with: given, or each engine picks its own and prints it
  $seedArg = if ($Seed) { @("seed=$Seed") } else { @() }

  & $Godot --headless --import --path $W *> (Join-Path $W 'import.log')

  $all = @()
  $want = 0
  if ($Fly) {
    # the pilot: one engine, no fake routers, no peers
    $all += Complete-Run (Start-Run (@('--headless','--fixed-fps','60','--path',$W,'--','fly') + $seedArg) 'fly' 1800) '[fly] '
    $want = 1
  }
  elseif (-not $Wan) {
    # Two fake routers for the plug-and-play scenarios: the run never searches the real network,
    # and never opens a port on the real router (see NoRouterNoInternet in the test).
    $fake = Start-Process -FilePath python -ArgumentList @((Join-Path $PSScriptRoot 'fakeigd.py'), 19000, 19080, 19351, 1300) `
            -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $W 'fakeigd.out') -RedirectStandardError (Join-Path $W 'fakeigd.err')
    Start-Sleep -Milliseconds 500
    # fixed 60 fps: identical frame timing every run, so the DPS checks are exact
    $all += Complete-Run (Start-Run (@('--headless','--fixed-fps','60','--path',$W,'--','solo') + $seedArg) 'solo' 1200) '[solo] '
    if (-not $fake.HasExited) { try { $fake.Kill() } catch {} }
    $want = 1
  }

  # The internet, for -Wan: one relay per session port, each 1000 above the port it fronts. The
  # guests are told "wan" and join the relay instead of the host.
  $relays = @()
  $gx = @()
  if ($Wan) {
    $wanPy = Join-Path $PSScriptRoot 'wan.py'
    # one-way ms, jitter ms, loss %: WARSHIPS_WAN="150,40,5" for a worse day
    $path = if ($env:WARSHIPS_WAN) { $env:WARSHIPS_WAN -split ',' } else { @(90, 25, 2) }
    Write-Host ("internet: {0} ms each way, +/- {1} ms, {2}% lost" -f $path[0], $path[1], $path[2])
    foreach ($port in 27115, 27125) {
      $relays += Start-Process -FilePath python -ArgumentList (@($wanPy, ($port + 1000), $port) + $path + 200) `
                 -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $W "wan$port.out") -RedirectStandardError (Join-Path $W "wan$port.err")
    }
    $gx = @('wan')
    # Godot's own note that it dropped a packet which overtook the handshake's last (lost, resent)
    # one. Expected on a lossy path, and answered in the game: a joining guest repeats its
    # introduction (Hub.OnSessionChanged), so what was dropped arrives again.
    $dropRe += '|SYS_COMMAND_AUTH'
    Start-Sleep -Milliseconds 300
  }

  if (-not $Solo -and -not $Fly) {
    $host1 = Start-Run (@('--headless','--path',$W,'--','host') + $seedArg)   'host'   120
    Start-Sleep -Milliseconds 500
    $g2 = Start-Run (@('--headless','--path',$W,'--','guest2') + $gx + $seedArg) 'guest2' 120
    $g1 = Start-Run (@('--headless','--path',$W,'--','guest') + $gx + $seedArg)  'guest'  120
    $all += Complete-Run $g1    '[guest] '
    $all += Complete-Run $host1 '[host]  '
    $all += Complete-Run $g2    '[third] '

    # the dedicated two-player arena run: after the three-player run, on its own port
    $ah = Start-Run (@('--headless','--path',$W,'--','ahost') + $seedArg)  'ahost'  120
    Start-Sleep -Milliseconds 500
    $ag = Start-Run (@('--headless','--path',$W,'--','aguest') + $gx + $seedArg) 'aguest' 120
    $all += Complete-Run $ag '[aguest]'
    $all += Complete-Run $ah '[ahost] '
    $want += 5
  }
  foreach ($r in $relays) { if (-not $r.HasExited) { try { $r.Kill() } catch {} } }
  foreach ($port in 27115, 27125) {
    $f = Join-Path $W "wan$port.out"
    if ($Wan -and (Test-Path $f)) { Get-Content $f | ForEach-Object { Write-Host "  $_" } }
  }

  $all | ForEach-Object { Write-Host $_ }
  $bad = @($all | Where-Object { $_ -cmatch 'FAIL|Exception|ERROR' }).Count
  $done = @($all | Where-Object { $_ -cmatch 'DONE' }).Count
  # "SOLO ONLY" in the verdict, always. A partial run that prints the same words as a full one is
  # a partial run that will be mistaken for the bar.
  $what = if ($Solo) { 'SMOKE TEST (SOLO ONLY)' } elseif ($Wan) { 'SMOKE TEST (MULTIPLAYER OVER A SIMULATED INTERNET)' } else { 'SMOKE TEST' }
  if ($bad -gt 0 -or $done -ne $want) {
    Write-Host "$what FAILED ($bad problems, $done/$want runs finished)"; exit 1
  }
  Write-Host "$what PASSED"
} finally { Pop-Location }
