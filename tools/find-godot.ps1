# Resolve the Godot 4.7.2 mono win64 binary, so nobody has to remember where it lives.
#
# Prints the path on stdout and nothing else -- callers capture it. Everything explanatory
# goes to the host stream so it cannot pollute the captured value. Exits 2 if it cannot find one.
#
# Order of preference:
#   1. -Godot on the command line          (an explicit choice always wins)
#   2. $env:WARSHIPS_GODOT                 (a shell or CI setting)
#   3. local.config.ps1 at the repo root   (gitignored; per-machine, survives a move)
#   4. a search of the usual places        (Desktop, Downloads, Program Files, C:\Godot)
param([string]$Godot)

function Ok([string]$p) {
    if (-not $p) { return $null }
    if (-not (Test-Path $p)) { return $null }
    # the runners want the PLAIN exe: they swap themselves to _console.exe, which is the one
    # that actually writes to stdout on Windows
    return (Resolve-Path $p).Path
}

$found = Ok $Godot
if ($found) { Write-Output $found; exit 0 }

$found = Ok $env:WARSHIPS_GODOT
if ($found) { Write-Host "godot: from WARSHIPS_GODOT"; Write-Output $found; exit 0 }

$cfg = Join-Path $PSScriptRoot '..\local.config.ps1'
if (Test-Path $cfg) {
    $Godot = $null
    . $cfg                                   # expected to set $Godot
    $found = Ok $Godot
    if ($found) { Write-Host "godot: from local.config.ps1"; Write-Output $found; exit 0 }
}

# Last resort: look where it usually is. Depth-limited so this stays quick.
$roots = @("$env:USERPROFILE\Desktop", "$env:USERPROFILE\Downloads", 'C:\Godot',
           'C:\Program Files', "$env:LOCALAPPDATA\Programs")
foreach ($r in $roots) {
    if (-not (Test-Path $r)) { continue }
    $hit = Get-ChildItem $r -Recurse -Depth 3 -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction SilentlyContinue |
           Select-Object -First 1
    if ($hit) {
        Write-Host "godot: found at $($hit.FullName)"
        Write-Host "       (put it in local.config.ps1 as `$Godot = '...' to skip this search)"
        Write-Output $hit.FullName
        exit 0
    }
}

Write-Host "NO GODOT 4.7.2 MONO WIN64 FOUND."
Write-Host "Pass -Godot <path>, set WARSHIPS_GODOT, or create local.config.ps1 at the repo root:"
Write-Host "    `$Godot = 'C:\path\to\Godot_v4.7.2-stable_mono_win64.exe'"
exit 2
