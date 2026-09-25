# Windows port of run.sh -- renders real screenshots of the game (select screen, creator,
# both classes in the hub, the K window, a bomber strike) so layout and art can be LOOKED
# AT rather than assumed. The smoke test is headless and cannot see.
#
#   powershell -ExecutionPolicy Bypass -File tools\screens\run.ps1 <godot mono win64 exe>
#
# Differences from the Linux original, all forced by the platform:
#   * No Xvfb. Windows has a real display, so the whole virtual-display dance -- and the
#     stale /tmp/.X99-lock trap that came with it -- does not apply.
#   * Renders on the real GPU with the project's own renderer (Forward+/Vulkan) instead of
#     Mesa software GL. That is the point of running it here: these frames are what the
#     developer actually sees. Pass -Compat to force the Linux path (opengl3 /
#     gl_compatibility) when comparing against a sandbox run.
#   * Shots.cs.txt hard-codes /tmp/shots/; the copy is rewritten to $env:TEMP\shots.
#     The original .txt is never modified.

param(
  [string]$Godot,
  [switch]$Compat
)

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
  else { Write-Host "NO CONSOLE BINARY beside $Godot -- shot/LINT output would be invisible."; exit 2 }
}

$nupkgs = Join-Path (Split-Path $Godot) 'GodotSharp\Tools\nupkgs'
if (-not (Test-Path $nupkgs)) { Write-Host "no nupkgs folder at $nupkgs"; exit 2 }
$src = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$W = Join-Path $env:TEMP 'warships_shots'
$shots = Join-Path $env:TEMP 'shots'

# One fixed scratch folder each, so two sweeps cannot overlap. A file handle can outlive the
# process that held it by a moment (the last run's engine, a build server), so a failed clear is
# retried for a few seconds before it is reported -- as a SWEEP line, so verify.ps1 shows it
# instead of a bare Remove-Item error it filters out.
foreach ($dir in @($W, $shots)) {
  for ($try = 1; Test-Path $dir; $try++) {
    try { Remove-Item $dir -Recurse -Force -ErrorAction Stop }
    catch {
      if ($try -ge 10) {
        Write-Host "SWEEP FAILED (cannot clear $dir -- another sweep is probably still going: $($_.Exception.Message))"
        exit 2
      }
      Start-Sleep -Milliseconds 500
    }
  }
}
New-Item -ItemType Directory -Path $W, $shots -Force | Out-Null
robocopy $src $W /E /XD .godot .git bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
# ROBOCOPY'S EXIT CODE IS A BITFIELD: 0-7 is success (8 = some files did not copy). It was
# discarded, so a half-copied tree went on to build, import and report on whatever arrived.
if ($LASTEXITCODE -ge 8) { Write-Host "copy FAILED (robocopy $LASTEXITCODE): $src -> $W"; exit 2 }

# Rewrite the one POSIX path in the shot writer. Godot's SavePng takes forward slashes
# happily on Windows, so only the prefix changes.
$shotsFwd = $shots -replace '\\', '/'
$shotSrc = Get-Content (Join-Path $PSScriptRoot 'Shots.cs.txt') -Raw
if ($shotSrc -notmatch [regex]::Escape('/tmp/shots/')) {
  Write-Host "Shots.cs.txt no longer writes to /tmp/shots/ -- this port needs updating."; exit 2
}
$shotSrc = $shotSrc.Replace('/tmp/shots/', "$shotsFwd/")
Set-Content (Join-Path $W 'scripts\_Shots.cs') $shotSrc -Encoding UTF8 -NoNewline

$pg = Join-Path $W 'project.godot'
$t = Get-Content $pg -Raw
$t = $t -replace '(?m)^Net="\*res://scripts/Net\.cs"', "`$0`n_Shots=`"*res://scripts/_Shots.cs`""
# its own user:// -- it creates characters
$t = $t -replace '(?m)^config/name="Warships"', 'config/name="WarshipsShots"'
Set-Content $pg $t -Encoding UTF8 -NoNewline

$check = Get-Content $pg -Raw
if ($check -notmatch 'config/name="WarshipsShots"') { Write-Host "could not isolate user data; refusing to run"; exit 2 }
if ($check -notmatch '_Shots="\*res://scripts/_Shots\.cs"') { Write-Host "could not register the shots autoload; refusing to run"; exit 2 }

$ud = Join-Path $env:APPDATA 'Godot\app_userdata\WarshipsShots'
if (Test-Path $ud) { Remove-Item $ud -Recurse -Force }

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /><add key="godot" value="$nupkgs" /></packageSources></configuration>
"@ | Set-Content (Join-Path $W 'nuget.config') -Encoding UTF8

Push-Location $W
try {
  $buildLog = Join-Path $W 'build.log'
  & dotnet build -v q -nologo *> $buildLog
  $buildFailed = ($LASTEXITCODE -ne 0)
  $errs = Get-Content $buildLog | Select-String -Pattern ' error ' | ForEach-Object { ($_.Line -replace '.*[\\/]scripts[\\/]', '') } | Sort-Object -Unique
  if ($buildFailed -or $errs) {
    $errs | Select-Object -First 20 | ForEach-Object { Write-Host $_ }
    Write-Host "SWEEP FAILED (the build failed)"; exit 1
  }

  # judged by what it registered, not by its exit code (SPIKE F1); refused without the WebRTC plugin
  & (Join-Path $PSScriptRoot '..\import.ps1') -Godot $Godot -Path $W -Log (Join-Path $W 'import.log') -Require 'res://addons/webrtc_native/webrtc_native.gdextension'
  if ($LASTEXITCODE -ne 0) { Write-Host "SWEEP FAILED (the import)"; exit 1 }

  $render = @('--path', $W, '--windowed', '--resolution', '1600x900')
  if ($Compat) { $render += @('--rendering-driver', 'opengl3', '--rendering-method', 'gl_compatibility') }

  $o = Join-Path $W 'sweep.out'; $e = Join-Path $W 'sweep.err'
  $p = Start-Process -FilePath $Godot -ArgumentList $render -WorkingDirectory $W `
       -NoNewWindow -PassThru -RedirectStandardOutput $o -RedirectStandardError $e
  if (-not $p.WaitForExit(400 * 1000)) { try { $p.Kill() } catch {}; $p.WaitForExit(5000) | Out-Null }

  $lines = @()
  foreach ($stream in @($o, $e)) { if (Test-Path $stream) { $lines += Get-Content $stream -ErrorAction SilentlyContinue } }
  # -cmatch: grep -E is case-sensitive, PowerShell's -match is not.
  $keep = $lines | Where-Object { $_ -cmatch '^shot|^LINT|Exception|^SWEEP' }
  $sweepLog = Join-Path $env:TEMP 'sweep_out.log'
  Set-Content $sweepLog ($keep -join "`n") -Encoding UTF8
  $keep | ForEach-Object { Write-Host $_ }

  # a sweep cut off part-way (a timeout) must never pass as clean
  $bad = 0
  if (-not ($keep | Where-Object { $_ -cmatch '^SWEEP DONE' })) {
    Write-Host "SWEEP INCOMPLETE -- the run was cut off; later frames were not rendered"
    $bad = 1
  }
  $lint = @($keep | Where-Object { $_ -cmatch '^LINT' }).Count
  $frames = @(Get-ChildItem $shots -Filter *.png -ErrorAction SilentlyContinue).Count
  Write-Host "frames: $frames in $shots  |  LINT: $lint"
  # ...AND IT SAYS SO IN ITS EXIT CODE. All three of these were computed, printed and thrown away:
  # SWEEP INCOMPLETE only printed, and $lint and $frames were never asserted at all -- so a sweep
  # whose engine died at frame 0 ended on "frames: 0 | LINT: 0" and exited 0. CLAUDE.md section 12
  # tells a reader to trust LINT: 0 for rung 4, and nothing to draw is nothing to lint.
  # verify.ps1 re-derives this from stdout, so the BAR was covered; rung 4 run on its own was not.
  if ($frames -lt 1) { Write-Host 'the sweep drew NO frames -- LINT: 0 means nothing.'; $bad = 1 }
  if ($lint -ne 0)   { Write-Host "the sweep reported $lint layout problems."; $bad = 1 }
  if ($bad) { Pop-Location; exit 1 }
} finally { Pop-Location }
