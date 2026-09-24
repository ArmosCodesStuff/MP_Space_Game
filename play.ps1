# Play Warships.
#
#     powershell -ExecutionPolicy Bypass -File play.ps1          one window
#     powershell -ExecutionPolicy Bypass -File play.ps1 -Two     two windows, for multiplayer
#     powershell -ExecutionPolicy Bypass -File play.ps1 -Editor  open it in the Godot editor instead
#
# TWO WAYS TO PLAY, AND IT PICKS THE ONE THAT WILL WORK.
#
#   FROM SOURCE, if this machine has the developer tools: it finds the engine, compiles the
#   C# and launches the project, so you play the code in this folder. Needs Godot 4.7.2
#   .NET (mono) and the .NET SDK -- about 330 MB of tooling.
#
#   THE PUBLISHED BUILD otherwise, handed to tools\install.ps1, which downloads it, checks it
#   against the release own checksums and starts it. NEEDS NOTHING INSTALLED: Windows
#   PowerShell is the whole requirement. Someone who only wants to play never needs the
#   developer tools at all.
#
# So: download this repo, double-click PLAY.bat, play. If the tools appear later, the same
# double-click starts running your own code instead.
#
#   -Two launches two windows so you can host in one and join 127.0.0.1 in the other. They share
#   one user:// and therefore one character list, which is fine for testing but means both windows
#   write the same files: give each window its OWN character before you host, or the second to
#   save wins. (That exact collision is what made a whole afternoon of save bugs look real.)
param([switch]$Two, [switch]$Editor, [string]$Godot)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$hasDotnet = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
try { $Godot = & (Join-Path $PSScriptRoot 'tools\find-godot.ps1') -Godot $Godot } catch { $Godot = $null }
if ($LASTEXITCODE -ne 0) { $Godot = $null }
# THE MONO BUILD OR NOTHING: the plain export of Godot carries no C# runtime at all, so it
# opens this project and then fails to load a single script -- which reads as the project
# being broken, and is not.
if ($Godot -and $Godot -notmatch 'mono') { $Godot = $null }

# NO TOOLS? PLAY THE BUILT GAME rather than refuse. This used to exit 2 with a download
# link, which is the right answer for someone who came to WORK on it and the wrong one for
# everyone else.
if (-not $Godot -or -not $hasDotnet) {
    if ($Editor) {
        Write-Host "-Editor needs the developer tools, and they are not here."
        Write-Host "  Godot 4.7.2 MONO: https://godotengine.org/download/archive/4.7.2-stable/"
        Write-Host "  .NET 8 SDK:       https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 2
    }
    $missing = @()
    if (-not $hasDotnet) { $missing += '.NET SDK' }
    if (-not $Godot)     { $missing += 'Godot 4.7.2 mono' }
    Write-Host ("no developer tools here ({0}), so this plays the BUILT game instead." -f ($missing -join ", "))
    Write-Host "you need neither of them to play."
    & (Join-Path $PSScriptRoot 'tools\install.ps1')
    exit $LASTEXITCODE
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
# ...but someone who only wants to PLAY needs none of that: point them at the repo, or at
# the Releases page. tools\install.ps1 fetches the published build and needs no engine.
#
# To MAKE a release: tools\pack.ps1, one command. See docs\README.md under Releasing.
