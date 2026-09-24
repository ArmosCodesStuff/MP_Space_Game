# INSTALL THE PUBLISHED GAME AND RUN IT, on a machine with nothing on it.
#
#     powershell -ExecutionPolicy Bypass -File tools\install.ps1
#
# NOTHING IS REQUIRED THAT WINDOWS DOES NOT ALREADY HAVE. Windows PowerShell 5.1 ships with every
# Windows 10 and 11; Get-FileHash, Expand-Archive and Invoke-WebRequest are all built in. No Godot,
# no .NET SDK, no launcher, no git. Download this repo, run it, play.
#
# IT IS NOT THE LAUNCHER AND DOES NOT USE IT. The launcher is a program a player keeps, with a
# window and buttons, that updates a build in place; this is a script that installs one and starts
# it. They read the same release and the same BUILD.txt, so neither can drift from the other, but
# this one has no window, no dependency and nothing to press.
#
#     -Root <dir>   where to install (default: play\ beside this repo)
#     -NoRun        install and stop, do not start the game
#     -Force        reinstall even if this build is already there
#
# WHAT IT DOES, in order. Nothing outside a temp folder is written until every byte has been
# checked, and nothing is checked against a figure this script carries -- the release states its
# own hashes and this verifies against those.
#   1. read BUILD.txt from the release: the build id, what to launch, and one `part=` row per zip
#      with that zip's SHA-256 and size
#   2. if the installed BUILD.txt already names this build, skip to the end
#   3. download each part to a temp folder and check its SHA-256 against the row
#   4. extract them into the install folder
#   5. write BUILD.txt LAST, so an interrupted install does not claim to be a finished one
#   6. start the game
param([string]$Root, [switch]$NoRun, [switch]$Force)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (-not $Root) { $Root = Join-Path (Get-Location).Path 'play' }

$Base = 'https://github.com/ArmosCodesStuff/MP_Space_Game/releases/latest/download/'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'    # PS 5.1 renders a progress bar per byte otherwise

function Fetch([string]$name, [string]$to) {
    Invoke-WebRequest -Uri ($Base + $name) -OutFile $to -UseBasicParsing
}

# ── 1 . what the release says about itself ───────────────────────────────────
Write-Host 'install: asking the release what it is'
$tmp = Join-Path $env:TEMP ('warships_install_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $tmp | Out-Null
try {
    $infoPath = Join-Path $tmp 'BUILD.txt'
    try { Fetch 'BUILD.txt' $infoPath }
    catch {
        Write-Host "install: cannot reach the release -- $($_.Exception.Message)"
        Write-Host "         $Base"
        exit 1
    }
    # A captive portal or a proxy answers 200 with an HTML page. A release descriptor starts with
    # schema=, and anything else is not one -- checked before a single byte of it is believed.
    $info = Get-Content $infoPath
    if (-not ($info | Select-Object -First 1) -or ($info | Select-Object -First 1) -notmatch '^schema=') {
        Write-Host 'install: what came back is not a release descriptor -- most likely an error page.'
        exit 1
    }
    $build  = ($info | Where-Object { $_ -like 'build=*'  } | Select-Object -First 1) -replace '^build=', ''
    $launch = ($info | Where-Object { $_ -like 'launch=*' } | Select-Object -First 1) -replace '^launch=', ''
    if (-not $launch) { $launch = 'Warships.exe' }
    $parts = @($info | Where-Object { $_ -like 'part=*' } | ForEach-Object {
        $f = ($_ -replace '^part=', '') -split '\|'
        [pscustomobject]@{ Id = $f[0]; Asset = $f[1]; Sha = $f[2]; Bytes = [int64]$f[3] }
    })
    if ($parts.Count -eq 0) { Write-Host 'install: the release names no files to install.'; exit 1 }
    Write-Host ("install: build $build, {0} parts, {1:N0} MB" -f $parts.Count, (($parts | Measure-Object Bytes -Sum).Sum / 1MB))

    # ── 2 . already have it? ─────────────────────────────────────────────────
    $haveInfo = Join-Path $Root 'BUILD.txt'
    $exe = Join-Path $Root $launch
    if (-not $Force -and (Test-Path $haveInfo) -and (Test-Path $exe)) {
        $have = (Get-Content $haveInfo | Where-Object { $_ -like 'build=*' } | Select-Object -First 1) -replace '^build=', ''
        if ($have -eq $build) {
            Write-Host "install: $build is already installed"
            if (-not $NoRun) { Write-Host 'install: starting the game'; Start-Process -FilePath $exe -WorkingDirectory $Root }
            exit 0
        }
        Write-Host "install: replacing $have"
    }

    # ── 3 . download, and check each one against the release's own figure ────
    foreach ($p in $parts) {
        $to = Join-Path $tmp $p.Asset
        Write-Host ("install: {0} ({1:N0} MB)" -f $p.Asset, ($p.Bytes / 1MB))
        try { Fetch $p.Asset $to } catch { Write-Host "install: $($p.Asset) failed -- $($_.Exception.Message)"; exit 1 }
        $sha = (Get-FileHash $to -Algorithm SHA256).Hash.ToLower()
        if ($sha -ne $p.Sha.ToLower()) {
            Write-Host 'install: that download does not match the checksum the release published.'
            Write-Host "         expected $($p.Sha)"
            Write-Host "         got      $sha"
            Write-Host '         Nothing has been installed. Try again later, or on another network.'
            exit 1
        }
    }

    # ── 4 . extract ──────────────────────────────────────────────────────────
    New-Item -ItemType Directory -Force $Root | Out-Null
    foreach ($p in $parts) {
        Write-Host "install: unpacking $($p.Asset)"
        Expand-Archive -Path (Join-Path $tmp $p.Asset) -DestinationPath $Root -Force
    }

    # ── 5 . the descriptor LAST ──────────────────────────────────────────────
    # An install that died halfway must not leave a BUILD.txt claiming it finished: the next run
    # would believe it and start a game that is missing files. Written only once everything else is.
    Copy-Item $infoPath $haveInfo -Force
    if (-not (Test-Path $exe)) {
        Write-Host "install: unpacked, but $launch is not there. The release may be malformed."
        exit 1
    }
    $n = @(Get-ChildItem $Root -Recurse -File).Count
    Write-Host ("install: done -- {0} files in {1}" -f $n, $Root)
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $NoRun) {
    Write-Host 'install: starting the game.'
    Write-Host '         Windows may say it does not recognise it -- nothing here is signed.'
    Write-Host '         Click "More info", then "Run anyway".'
    Start-Process -FilePath (Join-Path $Root $launch) -WorkingDirectory $Root
}
