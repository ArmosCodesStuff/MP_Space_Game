# The whole bar, in one command:
#
#     powershell -ExecutionPolicy Bypass -File verify.ps1
#
# Runs typecheck, build, analysers, cross-reference, the smoke test THREE TIMES (the protocol
# asks for three in a row -- one run has hidden a flaky check before) and the screenshot sweep,
# then prints a single verdict. Nothing needs to be passed in: the Godot binary is resolved by
# tools\find-godot.ps1 and GodotSharp.dll is fetched from the NuGet cache if it is missing.
#
# THREE GEARS. Use the smallest one that can see the change you just made:
#
#   -Quick   static only: typecheck, build, analysers, cross-reference.   ~1 min
#            Enough for a refactor, a rename, a comment, a doc edit.
#   -Fast    static + ONE solo smoke run + the sweep.                     ~3 min
#            The loop while a change is being built. One engine, not six: it cannot see a
#            host and a guest disagreeing, so it is never the last word.
#   (none)   the bar: static + three full smoke runs + the sweep + integrity.  ~12 min
#            Run before every VERIFIED commit, and only that verdict counts as verified.
#
#   -Update  regenerate version\CODE_SNAPSHOT.txt and version\MANIFEST.sha256 first, in that
#            order, so the integrity step checks the change rather than the last one.
#   -Godot   an explicit engine path, if the search picks the wrong one
param([switch]$Quick, [switch]$Fast, [switch]$Update, [string]$Godot)

$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot
$results = [ordered]@{}
function Step([string]$name, [scriptblock]$body) {
    Write-Host ""
    Write-Host "=== $name ==="
    $ok = & $body
    $results[$name] = [bool]$ok
    Write-Host ("--- {0}: {1}" -f $name, $(if ($ok) { 'OK' } else { 'FAILED' }))
}

Step 'typecheck' {
    $o = & powershell -NoProfile -ExecutionPolicy Bypass -File 'typecheck\typecheck.ps1' 2>&1
    $o | ForEach-Object { Write-Host "  $_" }
    # "0 errors." is necessary but not sufficient: the stub mode is a weaker check and must not
    # pass silently as if it were the real thing.
    ($o -join "`n") -match '0 errors\.' -and ($o -join "`n") -match 'REAL GodotSharp\.dll'
}

Step 'build' {
    $o = & dotnet build -v q -nologo 2>&1
    $o | Select-String 'Warning\(s\)|Error\(s\)|error CS' | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
    ($o -join "`n") -match '0 Warning\(s\)' -and ($o -join "`n") -match '0 Error\(s\)'
}

Step 'analysers' {
    $o = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\analyse\run.ps1' 2>&1
    $o | Select-Object -Last 3 | ForEach-Object { Write-Host "  $_" }
    ($o -join "`n") -match 'ANALYSERS: 0 findings'
}

Step 'cross-reference' {
    $o = & python 'tools\analyse\xref.py' 2>&1
    $o | Select-Object -First 1 | ForEach-Object { Write-Host "  $_" }
    ($o -join "`n") -match 'UNUSED ANYWHERE: 0'
}

if (-not $Quick) {
    $engine = & 'tools\find-godot.ps1' -Godot $Godot
    if ($LASTEXITCODE -ne 0) { Write-Host "cannot run the engine checks without Godot"; exit 2 }

    # One solo run in -Fast, three full runs otherwise. The protocol asks for three in a row
    # because one run has hidden a flaky check before.
    $runs = if ($Fast) { 1 } else { 3 }
    # Built as one explicit list. Splatting an array into `& powershell -File ...` is where the
    # first attempt went wrong: -File takes positional strings and the switch arrived as a value.
    $smokeArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File','tools\\smoketest\\run.ps1')
    if ($Fast) { $smokeArgs += '-Solo' }
    Step $(if ($Fast) { 'smoke test (solo only)' } else { 'smoke test x3' }) {
        $allOk = $true
        foreach ($i in 1..$runs) {
            $o = & powershell @smokeArgs -Godot $engine 2>&1
            $pass = @($o | Where-Object { $_ -cmatch 'PASS ' }).Count
            $done = @($o | Where-Object { $_ -cmatch 'DONE' }).Count
            # Not the runner's own verdict line: "SMOKE TEST FAILED (2 problems...)" contains
            # FAIL and would be counted as a problem on top of the problems it is reporting.
            $bad  = @($o | Where-Object { $_ -cmatch 'FAIL|Exception|ERROR' -and $_ -notmatch 'SMOKE TEST' })
            # No exceptions for "environmental" failures any more: the test builds its own network
            # (no router, no internet, or fake routers), so every check means the same everywhere.
            $want = if ($Fast) { 1 } else { 6 }
            Write-Host ("  run {0}: {1} pass, {2}/{3} runs, {4} problem(s)" -f $i, $pass, $done, $want, $bad.Count)
            $bad | ForEach-Object { Write-Host "    $_" }
            if ($bad.Count -ne 0 -or $done -ne $want) { $allOk = $false }
        }
        $allOk
    }

    Step 'screenshot sweep' {
        $o = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\screens\run.ps1' -Godot $engine 2>&1
        $o | Where-Object { $_ -cmatch '^SWEEP|^frames' } | ForEach-Object { Write-Host "  $_" }
        ($o -join "`n") -match 'SWEEP DONE' -and ($o -join "`n") -match 'LINT: 0'
    }
}

if ($Update) {
    Step 'snapshot + manifest' {
        $a = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\snapshot.ps1' 2>&1
        $b = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\manifest.ps1' 2>&1
        ($a + $b) | ForEach-Object { Write-Host "  $_" }
        ($b -join "`n") -match ', 0 not verifying'
    }
}

Step 'integrity (version/MANIFEST.sha256)' {
    $sha = 'C:\Program Files\Git\usr\bin\sha256sum.exe'
    if (-not (Test-Path $sha)) { Write-Host "  sha256sum not found (Git for Windows); skipped"; return $true }
    $r = & $sha -c 'version\MANIFEST.sha256' 2>&1
    $bad = @($r | Where-Object { $_ -notmatch ': OK$' -and $_ -notmatch 'WARNING' })
    Write-Host ("  {0} files, {1} mismatched" -f @($r | Where-Object { $_ -match ': OK$' }).Count, $bad.Count)
    $bad | ForEach-Object { Write-Host "    $_" }
    $bad.Count -eq 0
}

Write-Host ""
Write-Host "================ VERDICT ================"
foreach ($k in $results.Keys) { Write-Host ("  {0,-34} {1}" -f $k, $(if ($results[$k]) { 'OK' } else { 'FAILED' })) }
$failed = @($results.Keys | Where-Object { -not $results[$_] })
if ($failed.Count -eq 0) { Write-Host "ALL CHECKS PASSED"; exit 0 }
Write-Host ("FAILED: {0}" -f ($failed -join ', ')); exit 1
