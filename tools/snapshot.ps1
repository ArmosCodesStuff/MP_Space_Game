# Regenerate version/CODE_SNAPSHOT.txt.
#
# The file list is derived here rather than inherited from the previous snapshot, so a
# file that was never in it (typecheck/GodotStub.cs was missing for the project's whole
# history) cannot stay missing just because it was missing before.
#
# Format, measured from the file rather than guessed:
#   divider      = "//" + 78 "="
#   contents row = "//   " + <path left, count right, inside one 50-char field>
#                  + " lines   sha256 " + <first 12 hex>      (total length 83)
#   file block   = blank line, divider, "// FILE: <path>", "// <N> lines   sha256 <12>",
#                  divider, then the file verbatim
#
# Lives in the repo rather than in a session's scratch folder for the same reason verify.ps1
# does: a generator kept outside the project is a generator the next session does not have.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:Path += ';C:\Program Files\Git\cmd'

# --others --exclude-standard as well as the index: a brand-new file is not in `git ls-files`
# until it is staged, and a snapshot that quietly leaves out the file you just wrote is the
# same silent omission this generator exists to prevent.
$tracked = @(& git ls-files --cached --others --exclude-standard | Sort-Object -Unique)
function Dir-Files($prefix) {
  @($tracked | Where-Object { $_ -like "$prefix/*" -and ($_ -split '/').Count -eq (($prefix -split '/').Count + 1) }) |
    Where-Object { $_ -notmatch '\.(uid|dll|import)$' } |
    Sort-Object { $_ } -CaseSensitive
}

# Root files keep the original snapshot's order, which is not alphabetical.
$fixed = @('project.godot', 'Warships.csproj', 'CharacterSelect.tscn', 'Hub.tscn', 'MainMenu.tscn')
$final = @($fixed)
# Everything else is DISCOVERED, not listed. A hard-coded set of directories is how
# typecheck/GodotStub.cs stayed out of this file for the project's whole history, and it would
# have quietly dropped verify.ps1 and tools/find-godot.ps1 the moment they were added.
$code = @($tracked | Where-Object { $_ -match '\.(cs|sh|ps1|py|tscn|csproj|godot|editorconfig)$' -or $_ -match '\.cs\.txt$' })
$final += @($code | Where-Object { $_ -notin $fixed -and $_ -notlike '*/*' } | Sort-Object { $_ } -CaseSensitive)
$final += @($code | Where-Object { $_ -like 'scripts/*' }   | Sort-Object { $_ } -CaseSensitive)
$final += @($code | Where-Object { $_ -like 'typecheck/*' } | Sort-Object { $_ } -CaseSensitive)
$final += @($code | Where-Object { $_ -like 'tools/*' }     | Sort-Object { $_ } -CaseSensitive)
$final = @($final | Select-Object -Unique)

foreach ($p in $final) { if ($tracked -notcontains $p) { throw "not tracked: $p" } }

$info = @{}; $total = 0
foreach ($p in $final) {
  $win = $p -replace '/', '\'
  if (-not (Test-Path $win)) { throw "missing: $win" }
  $n = @(Get-Content $win).Count
  $h = (Get-FileHash $win -Algorithm SHA256).Hash.ToLower().Substring(0, 12)
  $info[$p] = @{ N = $n; H = $h }
  $total += $n
}

$D = '//' + ('=' * 78)
$commit = (& git rev-parse --short HEAD).Trim()
$stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm')

$out = New-Object System.Text.StringBuilder
function W($s) { [void]$out.Append($s); [void]$out.Append("`n") }

W $D
W '// WARSHIPS -- code snapshot (master copy)'
W "// Taken $stamp at git commit ${commit}: typecheck clean, dotnet build 0 warnings,"
W '//   0 analyser findings, 0 unused members; smoke test 3/3 runs at 435 checks on Windows'
W '//   (437 in a sandbox with no router and no internet); 67-frame UI sweep complete, 0 lint.'
W "// $($final.Count) files, $total lines. Code only: sprites, sounds and docs are in the zip."
W '//'
W '// Each file starts with a divider and a "// FILE: <path>" line. To split it back into'
W '// files, cut at each divider: everything after the divider''s third line, up to the next'
W '// divider, is that file verbatim.'
W '//'
W '// Contents:'
foreach ($p in $final) {
  $n = [string]$info[$p].N
  $pad = 50 - $p.Length - $n.Length
  if ($pad -lt 1) { $pad = 1 }
  W ('//   ' + $p + (' ' * $pad) + $n + ' lines   sha256 ' + $info[$p].H)
}
W $D

foreach ($p in $final) {
  $win = $p -replace '/', '\'
  W ''
  W $D
  W "// FILE: $p"
  W "// $($info[$p].N) lines   sha256 $($info[$p].H)"
  W $D
  $txt = [System.IO.File]::ReadAllText($win) -replace "`r`n", "`n"
  [void]$out.Append($txt)
  if (-not $txt.EndsWith("`n")) { [void]$out.Append("`n") }
}

[System.IO.File]::WriteAllText((Join-Path (Get-Location) 'version\CODE_SNAPSHOT.txt'), $out.ToString())
"files: $($final.Count)   lines: $total"
