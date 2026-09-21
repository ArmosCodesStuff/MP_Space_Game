# Play Warships.
#
#     powershell -ExecutionPolicy Bypass -File play.ps1          one window
#     powershell -ExecutionPolicy Bypass -File play.ps1 -Two     two windows, for multiplayer
#     powershell -ExecutionPolicy Bypass -File play.ps1 -Editor  open it in the Godot editor instead
#
# THIS IS NOT A STANDALONE BUILD. A Godot .NET project needs the engine to run it: this script
# finds the engine, compiles the C#, and launches the project. Anyone you send the folder to needs
# Godot 4.7.2 .NET (mono) as well -- see "Sending it to someone" at the bottom.
#
#   -Two launches two windows so you can host in one and join 127.0.0.1 in the other. They share
#   one user:// and therefore one character list, which is fine for testing but means both windows
#   write the same files: give each window its OWN character before you host, or the second to
#   save wins. (That exact collision is what made a whole afternoon of save bugs look real.)
param([switch]$Two, [switch]$Editor, [string]$Godot)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$Godot = & (Join-Path $PSScriptRoot 'tools\find-godot.ps1') -Godot $Godot
if ($LASTEXITCODE -ne 0 -or -not $Godot) {
    Write-Host ""
    Write-Host "Could not find Godot 4.7.2 .NET (mono)."
    Write-Host "Download it from https://godotengine.org/download -- the .NET build, not the standard one --"
    Write-Host "then either put it somewhere obvious, set WARSHIPS_GODOT to its path, or create"
    Write-Host "local.config.ps1 beside this script containing:  `$Godot = 'C:\path\to\Godot_v4.7.2-stable_mono_win64.exe'"
    exit 2
}

if ($Editor) {
    Write-Host "opening the editor: $Godot"
    Start-Process -FilePath $Godot -ArgumentList @('--editor', '--path', $PSScriptRoot)
    exit 0
}

# The engine runs a compiled assembly; without this the first launch fails or runs stale code.
Write-Host "building..."
$out = & dotnet build -v q -nologo 2>&1
if ($LASTEXITCODE -ne 0) { $out | ForEach-Object { Write-Host $_ }; Write-Host "BUILD FAILED"; exit 1 }

$n = if ($Two) { 2 } else { 1 }
for ($i = 1; $i -le $n; $i++) {
    Write-Host "launching window $i of $n"
    Start-Process -FilePath $Godot -ArgumentList @('--path', $PSScriptRoot)
    if ($i -lt $n) { Start-Sleep -Milliseconds 1200 }   # let the first one claim the window focus
}

if ($Two) {
    Write-Host ""
    Write-Host "Two windows up. In one: MULTIPLAYER (top left) -> HOST THIS WORLD."
    Write-Host "In the other: MULTIPLAYER -> type 127.0.0.1 -> JOIN."
    Write-Host "Give each window its own character first -- they share one save folder."
}

# ── Sending it to someone ────────────────────────────────────────────────────────────────
# They need the same zip AND Godot 4.7.2 .NET: the build handshake refuses a peer on a different
# build, deliberately, because two builds disagree about every number.
#
# To make a real .exe instead, open the editor (-Editor), install the export templates it offers,
# then Project -> Export -> Windows Desktop. That is a one-time setup per machine and is not
# scripted here, because nothing in this repo has been exported or tested that way yet.
