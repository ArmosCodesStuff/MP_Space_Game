# MAKE A RELEASE -- the one command that turns this tree into the five assets a player installs.
#
#     powershell -ExecutionPolicy Bypass -File tools\pack.ps1
#     powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -Dirty     # a test build, unclean tree
#
# It writes dist\ and nothing else. dist\ is gitignored: a release is an OUTPUT, and the tree it
# came from is named inside it (the build id carries the commit) so it can always be remade.
#
# WHAT IT PRODUCES
#   dist\release\BUILD.txt      what the release says about itself -- schema, build id, the part
#                               table, and one `in=` line per file saying which part carries it
#   dist\release\BUILD.sha256   one SHA-256 per installed file, sha256sum's exact format
#   dist\release\NOTES.txt      the changelog to show, cut from docs\CHANGES.md
#   dist\release\code.zip       the handful of files that change every release
#   dist\release\runtime.zip    the Godot template and the .NET base library
#
# WHY TWO ZIPS. On an ordinary release only Warships.pck and the four managed files beside it
# change: 5 files of 189, and 9.8 MB zipped of 79.3. Everything else is the export template and
# the .NET base library, which move when Godot or .NET does and not otherwise. The installer
# fetches every part; the split is kept because it costs nothing and an installer that fetched
# only what changed would want it. A THIRD part is a row in this script plus its `in=` lines.
#
# THE PART SPLIT IS DECIDED HERE. BUILD.txt carries the expanded `in=` list -- one explicit line
# per file, no globs -- because a glob is logic, and logic belongs where it can be fixed.
#
# THE VERSION IS NOT STAMPED INTO THE EXE. export_presets.cfg leaves application/file_version and
# product_version empty on purpose: modify_resources patches them into Warships.exe's PE
# resources, which would change the 98 MB exe on every release and move it out of the part that
# stands still. The build id lives in BUILD.txt beside it.
param(
    [string] $Godot,
    [switch] $Dirty,          # allow an unclean tree (a test build; the id is marked)
    [string] $Out = 'dist',
    [int]    $ExportWait = 600   # seconds to wait for the export to EXIT; see below, it may not
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:Path += ';C:\Program Files\Git\cmd'
$repo = (Get-Location).Path

# -- 0 . a release must be reproducible --------------------------------------
# NOT $dirty: PowerShell is case-insensitive, so a local of that name IS the -Dirty switch.
$unclean = @(& git status --porcelain) | Where-Object { $_ }
if ($unclean -and -not $Dirty) {
    Write-Host 'pack: the tree is not clean -- a release nobody can reproduce is not a release.'
    $unclean | ForEach-Object { Write-Host "  $_" }
    Write-Host 'pack: commit, or pass -Dirty for a test build.'
    exit 1
}
$mark = ''
if ($unclean) { $mark = '-dirty' }
$commit = (& git rev-parse --short HEAD).Trim()
$build  = '{0}.{1}{2}' -f (Get-Date -Format 'yyyy-MM-dd'), $commit, $mark
$made   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
Write-Host "pack: build $build"

# IN PROCESS, like the runners do it. The resolver keeps its explanation on the host stream so it
# cannot pollute the path it prints -- but a CHILD powershell merges the two, and the caller ends
# up with the explanation glued to the front of the path. Calling it here keeps the streams apart.
$godotExe = & (Join-Path $PSScriptRoot 'find-godot.ps1') -Godot $Godot
if ($LASTEXITCODE -ne 0 -or -not $godotExe) { Write-Host 'pack: no Godot found'; exit 2 }
$godotExe = (Resolve-Path $godotExe).Path
# THE CONSOLE BUILD, for the same reason the smoke runner uses it and one more: the plain Godot
# .exe on Windows is a GUI-subsystem binary, so it prints nothing AND PowerShell does not wait for
# it. Calling the plain exe here returned before the export had written a byte, and the check below
# reported "no exe was produced" about a file that appeared a moment later.
if ($godotExe -notmatch '_console\.exe$') {
    $console = $godotExe -replace '\.exe$', '_console.exe'
    if (-not (Test-Path $console)) { Write-Host "pack: no console binary beside $godotExe"; exit 2 }
    $godotExe = $console
}

$dist = Join-Path $repo $Out
$game = Join-Path $dist 'game'
$rel  = Join-Path $dist 'release'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force -Confirm:$false }
foreach ($d in @($dist, $game, $rel)) { New-Item -ItemType Directory -Force $d | Out-Null }

# -- 1 . the game ------------------------------------------------------------
# --headless so no window opens; the preset's own export_path is overridden by the argument, which
# is why the export lands in dist\game and not in the folder above the repo.
Write-Host 'pack: exporting the game (a few minutes)'
# IT IS JUDGED BY ITS OUTPUT, NOT BY ITS EXIT. The headless export sometimes finishes every scrap
# of its work -- savepack prints DONE, all 189 files are on disk -- and then never exits, sitting
# at 0 CPU indefinitely. Waiting on that hangs the release with nothing to read and nothing to
# diagnose. So: wait generously, then stop waiting and let the files decide. ($args is an
# automatic variable in PowerShell and must not be used for this.)
$exportArgs = @('--headless', '--path', $repo, '--export-release', 'Windows Desktop',
                (Join-Path $game 'Warships.exe'))
$proc = Start-Process -FilePath $godotExe -ArgumentList $exportArgs -PassThru -NoNewWindow
if (-not $proc.WaitForExit($ExportWait * 1000)) {
    Write-Host ('pack: the export has not exited after {0} s -- judging it by what it wrote' -f $ExportWait)
    try { $proc.Kill() } catch { }
}
if (-not (Test-Path (Join-Path $game 'Warships.exe'))) { Write-Host 'pack: no exe was produced'; exit 1 }

# -- 3 . every installed file, and which part carries it ---------------------
# CODE is what an ordinary release changes: the resource pack, and the managed assembly and its
# three companions. RUNTIME is the rest -- the export template and the .NET base library.
$files = @(Get-ChildItem $game -Recurse -File |
           ForEach-Object { $_.FullName.Substring($game.Length + 1) -replace '\\', '/' } |
           Sort-Object -Unique)
$mb = (Get-ChildItem $game -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ('pack: {0} files, {1:N1} MB' -f $files.Count, $mb)

$codeNames = @('Warships.pck', 'Warships.dll', 'Warships.pdb', 'Warships.deps.json', 'Warships.runtimeconfig.json')
$partOf = @{}
foreach ($f in $files) {
    if ($codeNames -contains (Split-Path $f -Leaf)) { $partOf[$f] = 'code' } else { $partOf[$f] = 'runtime' }
}

# -- 4 . the release manifest, by the one tool that hashes anything ----------
# IN PROCESS. A child `powershell -File ... -Files $files` flattens the array on the command line
# and the tool hashed exactly ONE of the 189 files, writing a 108-byte manifest that verified
# perfectly -- a green line about almost nothing. Calling it with & hands it the real array.
& (Join-Path $PSScriptRoot 'manifest.ps1') -Root $game -Out 'BUILD.sha256' -Files $files
if ($LASTEXITCODE -ne 0) { Write-Host 'pack: the manifest did not verify'; exit 1 }
$hashed = @([System.IO.File]::ReadAllLines((Join-Path $game 'BUILD.sha256'))) | Where-Object { $_ }
if ($hashed.Count -ne $files.Count) {
    Write-Host ('pack: the manifest has {0} lines for {1} files' -f $hashed.Count, $files.Count); exit 1
}

# -- 5 . the two archives ----------------------------------------------------
Add-Type -AssemblyName System.IO.Compression.FileSystem
$parts = @()
foreach ($id in @('code', 'runtime')) {
    $mine = @($files | Where-Object { $partOf[$_] -eq $id })
    $stage = Join-Path $dist "stage_$id"
    New-Item -ItemType Directory -Force $stage | Out-Null
    foreach ($f in $mine) {
        $dst = Join-Path $stage ($f -replace '/', '\')
        New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
        Copy-Item (Join-Path $game ($f -replace '/', '\')) $dst -Force
    }
    $zip = Join-Path $rel "$id.zip"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip,
        [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Remove-Item $stage -Recurse -Force -Confirm:$false
    $parts += [pscustomobject]@{
        Id    = $id
        Asset = "$id.zip"
        Sha   = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
        Bytes = (Get-Item $zip).Length
    }
    Write-Host ('pack: {0}.zip -- {1} files, {2:N1} MB' -f $id, $mine.Count, ((Get-Item $zip).Length / 1MB))
}

# -- 6 . the changelog, cut from the record that is already kept -------------
# docs\CHANGES.md's Unreleased section: from the line after "## Unreleased" to the SECOND "### "
# heading, which starts the entry before this batch's. Cutting markdown is logic, and it lives
# HERE, in the tool that already reads the record.
#
# WHAT A PLAYER READS IS A SET OF SECTIONS, decided here. A section is a heading in square
# brackets on its own line, and NOTES.txt is plain text: it is what GitHub shows as the release
# body and what a player opens beside the game. A third section is a row below and nothing else.
#
# (These brackets were once read by a launcher that drew each heading bold in its own colour.
# There is no launcher now. They stayed because they read perfectly well as plain text, which
# is all they ever needed to do.)
$sections = @(
    @{ Head = 'FIRST TIME'; Lines = @(
        'Windows will say it does not recognise this program. It is unsigned -- a',
        'certificate is a few hundred a year and this is a game for friends.',
        'Click "More info", then "Run anyway".',
        'You will see it again each time an update replaces the game itself.'
    )},
    @{ Head = 'IF WINDOWS BLOCKS IT OUTRIGHT'; Lines = @(
        'A few Windows 11 machines have SMART APP CONTROL switched on. It blocks',
        'unsigned programs with no "Run anyway" -- if the box says "Smart App Control',
        'blocked an app" and offers you no way through, that is this.',
        '',
        'Most machines do not have it. It is only ever on for CLEAN INSTALLS of',
        'Windows 11 22H2 or later; an upgraded machine does not get it. If you cannot',
        'find the setting below, you do not have it, and your problem is something',
        'else -- say so rather than hunting for it.',
        '',
        'To turn it off:',
        '  Settings > Privacy & security > Windows Security > App & browser control',
        '  > Smart App Control settings > Off',
        '',
        'READ THIS BEFORE YOU DO: turning it off CANNOT BE UNDONE. Windows only lets',
        'it go one way, and the only route back is reinstalling Windows. It is a real',
        'security feature and you are switching it off for good, for a game. Nobody',
        'will mind if you decide that is not a trade you want to make.'
    )}
)
$preamble = @()
foreach ($sec in $sections) { $preamble += ('[' + $sec.Head + ']'); $preamble += $sec.Lines; $preamble += '' }
$preamble += '[GAME UPDATES]'
$md = [System.IO.File]::ReadAllLines((Join-Path $repo 'docs\CHANGES.md'))
$start = -1
for ($i = 0; $i -lt $md.Count; $i++) { if ($md[$i] -eq '## Unreleased') { $start = $i; break } }
$notes = @("WARSHIPS $build", '') + $preamble
if ($start -ge 0) {
    $heads = 0
    for ($i = $start + 1; $i -lt $md.Count; $i++) {
        if ($md[$i] -like '## *') { break }
        if ($md[$i] -like '### *') { $heads++ }
        if ($heads -ge 2) { break }
        $notes += $md[$i]
    }
} else {
    $notes += '(no notes: docs\CHANGES.md has no Unreleased section)'
}
[System.IO.File]::WriteAllText((Join-Path $rel 'NOTES.txt'),
                               (($notes -join "`n").TrimEnd() + "`n"),
                               (New-Object System.Text.UTF8Encoding $false))

# -- 7 . what the release says about itself ----------------------------------
$txt = @('schema=1', "build=$build", "made=$made", 'launch=Warships.exe',
         'notes=NOTES.txt', 'manifest=BUILD.sha256')
foreach ($p in $parts) { $txt += 'part={0}|{1}|{2}|{3}' -f $p.Id, $p.Asset, $p.Sha, $p.Bytes }
foreach ($f in $files) { $txt += 'in={0}|{1}' -f $partOf[$f], $f }
[System.IO.File]::WriteAllText((Join-Path $rel 'BUILD.txt'),
                               (($txt -join "`n") + "`n"),
                               (New-Object System.Text.UTF8Encoding $false))
Copy-Item (Join-Path $game 'BUILD.sha256') (Join-Path $rel 'BUILD.sha256') -Force

# -- 8 . say exactly what to upload ------------------------------------------
$fileUrl = 'file:///' + ($rel -replace '\\', '/') + '/'
Write-Host ''
Write-Host "pack: $build"
Write-Host '  upload all five to the GitHub release, with these exact names -- tools\install.ps1'
Write-Host '  fetches them from releases/latest/download, so a renamed asset is one it cannot find:'
Get-ChildItem $rel -File | Sort-Object Name | ForEach-Object {
    Write-Host ('    {0,-22} {1,12:N0} bytes' -f $_.Name, $_.Length)
}
Write-Host '  a player needs nothing installed: PLAY.bat runs tools\install.ps1, which reads these.'
Write-Host "  to test before uploading: unpack $rel by hand into play\"
