# Windows port of typecheck.sh -- type-checks every script with the real Roslyn compiler.
#
# PREFERRED: if GodotSharp.dll is present in this folder, it is referenced directly
# and the hand-written stub is ignored entirely. That makes this check identical to
# what Godot itself compiles -- same reference assembly, same compiler.
#
# FALLBACK: GodotStub.cs, which only proves shape. Errors in the stub are FATAL,
# because Roslyn binds declarations before method bodies: one bad declaration in the
# stub means no method body is ever checked, and every script error vanishes.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$sdkRoot = 'C:\Program Files\dotnet'
$csc = Get-ChildItem "$sdkRoot\sdk" -Recurse -Filter csc.dll -ErrorAction SilentlyContinue |
       Where-Object { $_.FullName -like '*Roslyn*' -and $_.FullName -like '*\8.*' } |
       Select-Object -First 1 -ExpandProperty FullName
$refDir = Get-ChildItem "$sdkRoot\packs\Microsoft.NETCore.App.Ref" -Directory -ErrorAction SilentlyContinue |
          Where-Object { $_.Name -like '8.*' } | Select-Object -First 1 |
          ForEach-Object { Join-Path $_.FullName 'ref\net8.0' }

# No compiler means no check. Without this guard the harness finds nothing, runs
# nothing, and the error grep below turns "no compiler" into "0 errors".
if (-not $csc -or -not $refDir -or -not (Test-Path $refDir)) {
  Write-Host 'NO .NET 8 SDK FOUND -- nothing was checked. Install dotnet-sdk-8.0.'
  exit 2
}

$refs = Get-ChildItem "$refDir\*.dll" | ForEach-Object { "-r:$($_.FullName)" }
$outDll = Join-Path $env:TEMP 'typecheck.dll'
if (Test-Path $outDll) { Remove-Item $outDll -Force }

$haveReal = Test-Path 'GodotSharp.dll'
if ($haveReal) {
  $extra = @('-r:GodotSharp.dll')
  if (Test-Path 'GodotSharpEditor.dll') { $extra += '-r:GodotSharpEditor.dll' }
  $src = Get-ChildItem '..\scripts\*.cs' | ForEach-Object { $_.FullName }
  $mode = 'REAL GodotSharp.dll'
} else {
  $extra = @()
  $src = @(Get-ChildItem '..\scripts\*.cs' | ForEach-Object { $_.FullName }) + @('GodotStub.cs')
  $mode = 'hand-written stub (weaker -- drop GodotSharp.dll here to fix)'
}

$argsList = @('-target:library','-nologo','-langversion:12.0','-nostdlib+','-define:GODOT',"-out:$outDll") +
            $src + $extra + $refs
$out = & dotnet $csc $argsList | Select-String -Pattern 'error CS' | ForEach-Object { $_.Line }

if (-not $env:GODOT_QUIET) { Write-Host "checking against: $mode" }

if (-not $haveReal) {
  $stub = @($out | Where-Object { $_ -match 'GodotStub' })
  if ($stub.Count -gt 0) {
    Write-Host 'STUB IS BROKEN -- script checking did NOT run. Fix these first:'
    $stub | ForEach-Object { Write-Host $_ }
    exit 1
  }
}
if ($out) {
  $out | Where-Object { $_ -ne '' } | ForEach-Object { Write-Host $_ }
  exit 1
}
# Zero errors only counts if the compiler actually produced its output.
if (-not (Test-Path $outDll)) {
  Write-Host 'COMPILER PRODUCED NOTHING -- result is not trustworthy.'
  exit 2
}
Write-Host '0 errors.'
