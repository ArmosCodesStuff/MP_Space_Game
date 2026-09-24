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
#   THE BUILT GAME, otherwise. It fetches WarshipsLauncher.exe from the latest release and runs it;
#   the launcher downloads the game itself and keeps it up to date from then on. Needs NOTHING
#   installed -- no Godot, no .NET, no engine at all. A player never needs the other path.
#
# So: a collaborator given the repo link clones it, double-clicks PLAY.bat, and plays. If they
# later install the tools, the same double-click starts running their own code instead.
#
#     -Editor   open the Godot editor instead of running the game (source only)
#     -Game     skip the source path and use the built game, even if the tools are here
#     -Yes      do not ask before downloading (for a script; a person should be asked)
#     -Godot    an explicit path to the engine, if the search cannot find it
param([switch]$Editor, [switch]$Game, [switch]$Yes, [string]$Godot)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$repo = (Get-Location).Path

# Where the launcher is published. The SAME fixed redirect the launcher itself uses for its
# assets, so there is one URL shape to keep true and it always points at the newest release.
$LauncherUrl  = 'https://github.com/ArmosCodesStuff/MP_Space_Game/releases/latest/download/WarshipsLauncher.exe'
# NOT dist\. The launcher installs the game BESIDE ITSELF, and tools\pack.ps1 deletes dist\ whole
# every time it makes a release -- so a player's installed game would be destroyed by the next
# build. play\ is gitignored, belongs to whoever is playing, and no tool here ever touches it.
$LauncherPath = Join-Path $repo 'play\WarshipsLauncher.exe'

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

if (-not (Test-Path $LauncherPath)) {
    Write-Host ''
    Write-Host 'play: it will download the launcher, about 66 MB:'
    Write-Host "        $LauncherUrl"
    Write-Host '      The launcher then downloads the game itself and keeps it updated.'
    if (-not $Yes) {
        $answer = Read-Host '      Download it? [Y/n]'
        if ($answer -and $answer -notmatch '^[Yy]') { Write-Host 'play: stopped, nothing downloaded.'; exit 1 }
    }
    New-Item -ItemType Directory -Force (Split-Path $LauncherPath) | Out-Null
    # A partial download must never be left looking like a finished one, so it lands under a temp
    # name and is only moved into place once it has been checked.
    $part = "$LauncherPath.part"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'      # PS 5.1 renders a progress bar per BYTE otherwise
        Write-Host '      downloading...'
        Invoke-WebRequest -Uri $LauncherUrl -OutFile $part -UseBasicParsing
    } catch {
        Write-Host "play: the download failed -- $($_.Exception.Message)"
        Write-Host '      Get it by hand from the Releases page and put it in play\.'
        if (Test-Path $part) { Remove-Item $part -Force -Confirm:$false }
        exit 1
    }
    # IT MUST BE AN EXECUTABLE. A captive portal, a proxy or a signed-out link returns an HTML page
    # with a 200, and saving that as an .exe and running it is the failure that looks like success.
    $head = [byte[]]::new(2)
    $fs = [IO.File]::OpenRead($part)
    try { $null = $fs.Read($head, 0, 2) } finally { $fs.Close() }
    if ($head[0] -ne 0x4D -or $head[1] -ne 0x5A) {      # 'MZ'
        Write-Host 'play: what came back is not a Windows program -- most likely an error page.'
        Write-Host '      Check the Releases page in a browser.'
        Remove-Item $part -Force -Confirm:$false
        exit 1
    }
    Move-Item $part $LauncherPath -Force
    Write-Host ("      got it: {0:N0} bytes" -f (Get-Item $LauncherPath).Length)
}

Write-Host 'play: starting the launcher. Press UPDATE in it, then PLAY.'
Write-Host '      Windows may warn that it does not recognise it -- the launcher is unsigned.'
Write-Host '      "More info", then "Run anyway". The release notes in it say more.'
Start-Process -FilePath $LauncherPath -WorkingDirectory (Split-Path $LauncherPath)
