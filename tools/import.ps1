# IMPORTING A PROJECT FOLDER, JUDGED BY WHAT IT REGISTERED (docs/plans/network_webrtc.md section 7, section 8).
#
#   powershell -ExecutionPolicy Bypass -File tools\import.ps1 -Godot <console exe> -Path <project folder>
#              [-Log <file>] [-Require res://addons/<name>/<name>.gdextension[,...]]
#
# `--headless --import` is the step that registers a GDExtension: it writes
# .godot\extension_list.cfg. Its exit code is NOT the verdict. The first import after an extension
# appears crashes AT EXIT (0xC0000005) after every step printed [ DONE ], with the import complete
# and the list written (SPIKE F1: 3 of 3 on a fresh tree, 1 of 1 with the addon newly added; the
# next import exited 0 every time). So an import that exits non-zero is run once more, and the
# verdict is what the folder holds afterwards:
#   * every extension named by -Require is in the folder;
#   * every library file an extension names is in the folder -- which is also what keeps a vendored
#     .gdextension trimmed to the libraries actually vendored (addons\webrtc_native: the two
#     Windows x86_64 lines): a line for a file that is not there is refused, not left to fail at load;
#   * every extension in the folder is listed in .godot\extension_list.cfg.
# Exit 0 when all hold, 2 with the reason otherwise. Every tool that imports a folder calls this.
param(
  [Parameter(Mandatory = $true)][string]$Godot,
  [Parameter(Mandatory = $true)][string]$Path,
  [string]$Log,
  [string[]]$Require = @()
)

$ErrorActionPreference = 'Stop'
$Path = (Resolve-Path $Path).Path
if (-not $Log) { $Log = Join-Path $Path 'import.log' }

function Get-ResPath([string]$file) { 'res://' + ($file.Substring($Path.Length).TrimStart('\', '/') -replace '\\', '/') }
function Get-DiskPath([string]$res) { Join-Path $Path (($res -replace '^res://', '') -replace '/', '\') }

$exts = @()
$addons = Join-Path $Path 'addons'
if (Test-Path $addons) { $exts = @(Get-ChildItem $addons -Recurse -Filter '*.gdextension' | ForEach-Object { Get-ResPath $_.FullName }) }

foreach ($r in $Require) {
  if ($exts -notcontains $r) {
    Write-Host "IMPORT REFUSED: $r is not in $Path. It is vendored by hand (docs\CHANGES.md, Handoff, says what goes there)."
    exit 2
  }
}
foreach ($e in $exts) {
  $dir = Split-Path (Get-DiskPath $e)
  foreach ($line in Get-Content (Get-DiskPath $e)) {
    if ($line -match '^\s*[\w.]+\s*=\s*"([^"]+\.(dll|so|dylib|wasm))"') {
      $lib = $Matches[1]
      $file = if ($lib -like 'res://*') { Get-DiskPath $lib } else { Join-Path $dir ($lib -replace '/', '\') }
      if (-not (Test-Path $file)) {
        Write-Host "IMPORT REFUSED: $e names $lib, which is not there. Vendor it, or delete that line: only the libraries shipped belong in it."
        exit 2
      }
    }
  }
}

Set-Content $Log '' -Encoding UTF8
$code = 0
$tries = 0
for ($try = 1; $try -le 2; $try++) {
  $tries = $try
  # Continue, not Stop, around the engine: under PowerShell 5.1 a native stderr line redirected
  # with *> is a terminating error when Stop is set (SPIKE F8).
  $ErrorActionPreference = 'Continue'
  & $Godot --headless --import --path $Path *>> $Log
  $code = $LASTEXITCODE
  $ErrorActionPreference = 'Stop'
  if ($code -eq 0) { break }
  Add-Content $Log "import attempt $try exited $code; importing again (SPIKE F1: the first import after an extension appears crashes at exit)"
}

$list = Join-Path $Path '.godot\extension_list.cfg'
$registered = if (Test-Path $list) { @(Get-Content $list | ForEach-Object { $_.Trim() }) } else { @() }
$missing = @($exts | Where-Object { $registered -notcontains $_ })
if ($missing.Count -gt 0) {
  Write-Host "IMPORT FAILED: not registered after $tries import(s), last exit $code`: $($missing -join ', ') (log: $Log)"
  exit 2
}
Write-Host ("import: {0} extension(s) registered{1}; last exit {2} after {3} import(s)" -f $exts.Count, $(if ($exts) { " ($($exts -join ', '))" } else { '' }), $code, $tries)
exit 0
