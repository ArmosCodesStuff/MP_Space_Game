# PLAY THE GAME -- from a clone, on a machine with nothing installed, or anywhere between.
#
#     powershell -ExecutionPolicy Bypass -File tools\play.ps1
#     ...or double-click PLAY.bat at the root, which is that line and nothing else.
#
# TWO WAYS TO PLAY, AND IT PICKS THE ONE THAT WILL WORK.
#
#   FROM SOURCE, if this machine has the developer tools. The engine runs the project in this
#   folder, so what you play is the code you are looking at. Needs Godot 4.7.2 MONO and the .NET
#   SDK -- about 330 MB of tooling, which is why it is not asked of someone who only wants to play.
#
#   THE BUILT GAME, otherwise -- handed to tools\install.ps1, which downloads the published build,
#   checks it against the release's own checksums and starts it. Needs NOTHING installed: no Godot,
#   no .NET, no engine, not even the launcher. A player never needs the other path.
#
# So: a collaborator given the repo link clones it, double-clicks PLAY.bat, and plays. If they
# later install the tools, the same double-click starts running their own code instead.
#
#     -Editor   open the Godot editor instead of running the game (source only)
#     -Game     skip the source path and use the built game, even if the tools are here
#     -Godot    an explicit path to the engine, if the search cannot find it
param([switch]$Editor, [switch]$Game, [string]$Godot)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$repo = (Get-Location).Path

# ── what this machine has ────────────────────────────────────────────────────
$hasDotnet = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
$engine = $null
if (-not $Game) {
    # In process: the resolver keeps its explanation on the host stream so it cannot pollute the
    # path it prints, and a CHILD powershell merges the two and hands back both glued together.
    try { $engine = & (Join-Path $PSScriptRoot 'find-godot.ps1') -Godot $Godot } catch { $engine = $null }
    if ($LASTEXITCODE -ne 0) { $engine = $null }
    # THE MONO BUILD OR NOTHING. The plain export of Godot carries no C# runtime at all: it opens
    # this project and then fails to load a single script, which reads like the project is broken.
    if ($engine -and $engine -notmatch 'mono') { $engine = $null }
}
$fromSource = -not $Game -and $hasDotnet -and $engine

# ── the source path ──────────────────────────────────────────────────────────
if ($fromSource) {
    $engine = (Resolve-Path $engine).Path
    Write-Host "play: from source, with $(Split-Path $engine -Leaf)"
    if ($Editor) {
        Write-Host 'play: opening the editor. Press F5 in it to run.'
        & $engine --path $repo -e
    } else {
        Write-Host 'play: a FIRST run on a fresh clone takes a minute or two -- every asset is'
        Write-Host '      imported and the C# is built before the window opens. It has not hung.'
        & $engine --path $repo
    }
    exit 0
}

# ── the built game ───────────────────────────────────────────────────────────
if ($Editor) {
    Write-Host 'play: -Editor needs the developer tools, and they are not here.'
    Write-Host '      Godot 4.7.2 MONO: https://godotengine.org/download/archive/4.7.2-stable/'
    Write-Host '      .NET 8 SDK:       https://dotnet.microsoft.com/download/dotnet/8.0'
    exit 2
}

if (-not $Game) {
    Write-Host 'play: this machine has no developer tools, so it will play the BUILT game instead.'
    $missing = @()
    if (-not $hasDotnet) { $missing += '.NET SDK' }
    if (-not $engine)    { $missing += 'Godot 4.7.2 mono' }
    Write-Host ("      (missing: {0} -- you do not need either of them to play.)" -f ($missing -join ', '))
}

# THE INSTALLER, not the launcher. tools\install.ps1 needs nothing this machine does not already
# have -- Windows PowerShell 5.1 ships with Windows, and Get-FileHash, Expand-Archive and
# Invoke-WebRequest are all built in -- so it goes from a bare machine to a running game with
# nothing pressed. The launcher is a program a player KEEPS, for updating in place; it is not what
# a first install should need, and it cannot be what gets one started.
& (Join-Path $PSScriptRoot 'install.ps1')
exit $LASTEXITCODE
