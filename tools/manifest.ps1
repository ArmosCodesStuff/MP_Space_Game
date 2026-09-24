# HASH A LIST OF FILES UNDER A ROOT INTO A MANIFEST -- one SHA-256 per file, in sha256sum's own
# format, so `sha256sum -c` is what checks it, here and on Linux.
#
#     powershell -ExecutionPolicy Bypass -File tools\manifest.ps1
#         the repo's SOURCE manifest: every tracked (and untracked) file except version/ itself,
#         into version\MANIFEST.sha256. This is what verify.ps1 -Update calls and what its
#         integrity check reads. RUN IT LAST, after the final edit of a change and after
#         tools\snapshot.ps1: a manifest generated before a later doc edit fails its own
#         integrity check in the next session, which has happened, and reads as tampering
#         rather than as a stale file.
#
#     powershell -ExecutionPolicy Bypass -File tools\manifest.ps1 -Root dist\game -Out BUILD.sha256
#         a RELEASE manifest: every file of an exported build, into a manifest beside it. This is
#         tools\pack.ps1's caller. The release manifest is NOT version\MANIFEST.sha256 -- that one
#         is the source tree and contains no build output at all, not one .dll.
#
# ONE MECHANISM, TWO CALLERS. -Root says where to hash from, -Out where to write, -Files what to
# hash (a list of root-relative forward-slash paths). Every default is exactly the source
# manifest's old behaviour, so the existing caller is unchanged.
#
# It lists untracked files too, so a brand-new file is covered on the commit that adds it rather
# than the one after -- the same reason snapshot.ps1 does.
#
# Hashes come from Get-FileHash rather than the shell's sha256sum: the Git for Windows build has
# no -T, and a 128-path command line is close enough to the limit that it is not worth finding
# out where the limit is. The FORMAT is sha256sum's -- lower-case hash, two spaces, forward
# slashes, LF, no BOM (a BOM makes the first line unparseable to sha256sum -c).
param(
    [string]   $Root  = '',                          # '' = the repo root
    [string]   $Out   = 'version\MANIFEST.sha256',   # relative to $Root
    [string[]] $Files = $null                        # $null = ask git, the source-manifest default
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:Path += ';C:\Program Files\Git\cmd'

if ($Root) { Set-Location $Root }

if ($null -eq $Files) {
    # THE SOURCE TREE. version/ is excluded because the snapshot and this manifest are OUTPUTS,
    # and hashing a file into itself cannot work.
    $Files = @(& git ls-files --cached --others --exclude-standard) |
             Where-Object { $_ -and $_ -notlike 'version/*' }
}
$Files = @($Files) | Where-Object { $_ } | Sort-Object -Unique

$lines = foreach ($f in $Files) {
    $win = $f -replace '/', '\'
    if (-not (Test-Path $win)) { throw "listed but missing: $f" }
    '{0}  {1}' -f (Get-FileHash $win -Algorithm SHA256).Hash.ToLower(), $f
}
[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Out),
                               (($lines -join "`n") + "`n"),
                               (New-Object System.Text.UTF8Encoding $false))

$sha = 'C:\Program Files\Git\usr\bin\sha256sum.exe'
if (-not (Test-Path $sha)) { Write-Host ("manifest: {0} files written; sha256sum not found, not verified" -f $Files.Count); exit 0 }
# A FLOOR, BECAUSE ZERO OF ZERO VERIFIES PERFECTLY. With an empty list this printed
# "manifest: 0 files, 0 not verifying" and exited 0 -- while sha256sum itself had exited 1 with
# nothing on stdout. That is how a release manifest that hashed ONE of 189 files passed its own
# check earlier today: nothing here ever asked how many there were meant to be.
if ($Files.Count -lt 1) { Write-Host 'manifest: nothing to hash -- refusing to write an empty manifest.'; exit 1 }
$check = & $sha -c $Out
$shaExit = $LASTEXITCODE
$bad = @($check | Where-Object { $_ -notmatch ': OK$' })
$okLines = @($check | Where-Object { $_ -match ': OK$' }).Count
Write-Host ("manifest: {0} files, {1} not verifying" -f $Files.Count, $bad.Count)
$bad | ForEach-Object { Write-Host "  $_" }
# ...AND THE CHECKER AGREED, AND CHECKED THEM ALL. Its exit code was never read, and a run that
# printed no OK lines at all counted zero failures and passed.
if ($shaExit -ne 0) { Write-Host "manifest: sha256sum exited $shaExit"; exit 1 }
if ($okLines -ne $Files.Count) {
    Write-Host ("manifest: {0} files hashed but only {1} verified -- the checker did not read them all." -f $Files.Count, $okLines)
    exit 1
}
if ($bad.Count -ne 0) { exit 1 }
