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
# THE PROJECT'S OWN TARGET, not a number written here as well: the .csproj says which framework the
# game is built for, and this rung compiles against THAT one's reference assemblies with THAT one's
# compiler. It was pinned to 8 in three lines, so moving the game to net10 would have left the
# cheapest rung quietly checking it against a framework it is no longer built for.
$tfm = (Select-String -Path (Join-Path $PSScriptRoot '..\Warships.csproj') -Pattern '<TargetFramework>(net[0-9]+\.[0-9]+)</TargetFramework>').Matches[0].Groups[1].Value
$major = ($tfm -replace '^net', '') -replace '\..*$', ''
$csc = Get-ChildItem "$sdkRoot\sdk" -Recurse -Filter csc.dll -ErrorAction SilentlyContinue |
       Where-Object { $_.FullName -like '*Roslyn*' -and $_.FullName -like "*\$major.*" } |
       Select-Object -First 1 -ExpandProperty FullName
$refDir = Get-ChildItem "$sdkRoot\packs\Microsoft.NETCore.App.Ref" -Directory -ErrorAction SilentlyContinue |
          Where-Object { $_.Name -like "$major.*" } | Select-Object -First 1 |
          ForEach-Object { Join-Path $_.FullName "ref\$tfm" }

# No compiler means no check. Without this guard the harness finds nothing, runs
# nothing, and the error grep below turns "no compiler" into "0 errors".
if (-not $csc -or -not $refDir -or -not (Test-Path $refDir)) {
  Write-Host "NO .NET $major SDK FOUND -- nothing was checked. Install dotnet-sdk-$major.0."
  exit 2
}

$refs = Get-ChildItem "$refDir\*.dll" | ForEach-Object { "-r:$($_.FullName)" }
$outDll = Join-Path $env:TEMP 'typecheck.dll'

# THE HARNESS IS SOURCE TOO. SmokeTest.cs.txt and Shots.cs.txt are compiled INTO the game by the
# runners (as scripts/_Test.cs and scripts/_Shots.cs), but `dotnet build` never sees them -- so a
# rename that missed a call in them used to compile clean here and only fail three minutes into an
# engine run, after a full copy and import. They are checked with everything else now: a .txt is
# copied to a .cs beside the temp output and handed to the same compiler.
$harness = @()
foreach ($h in @('..\tools\smoketest\SmokeTest.cs.txt', '..\tools\screens\Shots.cs.txt')) {
  if (-not (Test-Path $h)) { continue }
  $dst = Join-Path $env:TEMP ('typecheck_' + [IO.Path]::GetFileNameWithoutExtension($h))
  Copy-Item $h $dst -Force
  $harness += $dst
}
if (Test-Path $outDll) { Remove-Item $outDll -Force }

# GodotSharp.dll is gitignored -- a build dependency, not game content -- so every fresh clone or
# unzipped copy arrives without it and silently drops to the hand-written stub, which is the weak
# check this harness exists to avoid. It is already on any machine that has built the project, in
# the NuGet cache. Fetch it rather than quietly checking against the stub.
if (-not (Test-Path 'GodotSharp.dll')) {
    $cached = Join-Path $env:USERPROFILE '.nuget\packages\godotsharp\4.7.2\lib\net8.0\GodotSharp.dll'
    if (Test-Path $cached) {
        Copy-Item $cached 'GodotSharp.dll'
        Write-Host "fetched GodotSharp.dll from the NuGet cache (it is gitignored on purpose)"
    }
}

$haveReal = Test-Path 'GodotSharp.dll'
if ($haveReal) {
  $extra = @('-r:GodotSharp.dll')
  if (Test-Path 'GodotSharpEditor.dll') { $extra += '-r:GodotSharpEditor.dll' }
  $src = @(Get-ChildItem '..\scripts\*.cs' | ForEach-Object { $_.FullName }) + $harness
  $mode = 'REAL GodotSharp.dll'
} else {
  $extra = @()
  $src = @(Get-ChildItem '..\scripts\*.cs' | ForEach-Object { $_.FullName }) + $harness + @('GodotStub.cs')
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
