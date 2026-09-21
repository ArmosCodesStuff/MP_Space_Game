# Regenerate version/MANIFEST.sha256 -- one SHA-256 per tracked file, everything except
# version/ itself (the snapshot and this manifest are OUTPUTS, and hashing a file into itself
# cannot work).
#
#     powershell -ExecutionPolicy Bypass -File tools\manifest.ps1
#
# RUN IT LAST, after the final edit of a change, and after tools\snapshot.ps1. A manifest
# generated before a later doc edit fails its own integrity check in the next session, which
# has happened, and reads as tampering rather than as a stale file.
#
# It lists untracked files too, so a brand-new file is covered on the commit that adds it
# rather than the one after -- the same reason snapshot.ps1 does.
#
# Hashes come from Get-FileHash rather than the shell's sha256sum: the Git for Windows build
# has no -T, and a 128-path command line is close enough to the limit that it is not worth
# finding out where the limit is. The FORMAT is sha256sum's -- lower-case hash, two spaces,
# forward slashes -- because `sha256sum -c` is what checks it, here and on Linux.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:Path += ';C:\Program Files\Git\cmd'

$files = @(& git ls-files --cached --others --exclude-standard) |
         Where-Object { $_ -and $_ -notlike 'version/*' } |
         Sort-Object -Unique

$lines = foreach ($f in $files) {
    $win = $f -replace '/', '\'
    if (-not (Test-Path $win)) { throw "listed but missing: $f" }
    '{0}  {1}' -f (Get-FileHash $win -Algorithm SHA256).Hash.ToLower(), $f
}
# No BOM and LF endings: a BOM makes the first line unparseable to sha256sum -c.
[System.IO.File]::WriteAllText((Join-Path (Get-Location) 'version\MANIFEST.sha256'),
                               (($lines -join "`n") + "`n"),
                               (New-Object System.Text.UTF8Encoding $false))

$sha = 'C:\Program Files\Git\usr\bin\sha256sum.exe'
if (-not (Test-Path $sha)) { Write-Host ("manifest: {0} files written; sha256sum not found, not verified" -f $files.Count); exit 0 }
$check = & $sha -c 'version\MANIFEST.sha256'
$bad = @($check | Where-Object { $_ -notmatch ': OK$' })
Write-Host ("manifest: {0} files, {1} not verifying" -f $files.Count, $bad.Count)
$bad | ForEach-Object { Write-Host "  $_" }
if ($bad.Count -ne 0) { exit 1 }
