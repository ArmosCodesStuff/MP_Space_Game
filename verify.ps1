# The whole bar, in one command:
#
#     powershell -ExecutionPolicy Bypass -File verify.ps1
#
# Runs typecheck, build, analysers, cross-reference, text, the smoke test THREE TIMES (the protocol
# asks for three in a row -- one run has hidden a flaky check before) and the screenshot sweep,
# then prints a single verdict. Nothing needs to be passed in: the Godot binary is resolved by
# tools\find-godot.ps1 and GodotSharp.dll is fetched from the NuGet cache if it is missing.
#
# THREE GEARS. Use the smallest one that can see the change you just made:
#
#   -Quick   static: typecheck, build, analysers, cross-reference, text, checks with code.  ~1 min
#            Enough for a refactor, a rename, a comment, a doc edit.
#   -Fast    static + ONE solo smoke run + the sweep.                     ~3 min
#            The loop while a change is being built. One engine, not six: it cannot see a
#            host and a guest disagreeing, so it is never the last word.
#   (none)   the bar: static + three full smoke runs + the sweep + integrity.  ~12 min
#            Run before every VERIFIED commit, and only that verdict counts as verified.
#
#   -Update  regenerate version\MAP.md, version\CODE_SNAPSHOT.txt and version\MANIFEST.sha256
#            first, in that order, so the integrity step checks the change rather than the last one.
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
    # ANCHORED. '0 errors.' is a SUBSTRING of '10 errors.', so the unanchored form passed a
    # typecheck with ten errors in it. Same family as the analyser step that never ran.
    ($o -join "`n") -match '(^|\s)0 errors\.' -and ($o -join "`n") -match 'REAL GodotSharp\.dll'
}

Step 'build' {
    # -t:Rebuild, OR THIS STEP IS A NO-OP. An incremental build of an unchanged tree emits no
    # warnings whatever the code contains: measured on a project with a live CS0219, cold
    # build said '1 Warning(s)' and the very next build said '0 Warning(s)'. Every bar after
    # the first was therefore asking a question that could only be answered clean.
    # tools\analyse\run.ps1 already knew this and had the flag; this step did not.
    $o = & dotnet build -t:Rebuild -v q -nologo 2>&1
    $o | Select-String 'Warning\(s\)|Error\(s\)|error CS' | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
    # ANCHORED, for the same reason: '10 Warning(s)' and '10 Error(s)' both CONTAIN the
    # unanchored pattern, so a build with ten of each reported clean.
    ($o -join "`n") -match '(^|\s)0 Warning\(s\)' -and ($o -join "`n") -match '(^|\s)0 Error\(s\)'
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

Step 'text' {
    # NO CONTROL CHARACTER IN A TRACKED TEXT FILE. Backslash paths written through something that
    # read them as escapes turn into characters that show as nothing: tools\find-godot.ps1 lost its
    # \f to a form feed, and tools\analyse\run.ps1 its \a and \r to a bell and a carriage return,
    # which split a comment in this file into a command. Tab and newline are text, and so is a
    # carriage return directly before a newline (PLAY.bat is CRLF, as a .bat should be). version/
    # is generated. A file with a NUL in its first 8000 bytes is binary (git's own test) and is not
    # text, whatever its extension: a list of extensions let the vendored plugin DLLs in, and the
    # millions of matches in 4 MB of machine code held this step for half an hour. Not git's
    # w/-text, which also calls a lone carriage return binary and would skip the very file this
    # step exists to catch.
    $env:Path += ';C:\Program Files\Git\cmd'
    $files = @(& git ls-files --cached --others --exclude-standard | Where-Object { $_ })
    $read = 0
    $binary = 0
    $bad = @()
    foreach ($f in $files) {
        if ($f -like 'version/*' -or -not (Test-Path -LiteralPath $f)) { continue }
        $path = Join-Path $PSScriptRoot $f
        $head = New-Object byte[] 8000
        $fs = [System.IO.File]::OpenRead($path)
        try { $n = $fs.Read($head, 0, $head.Length) } finally { $fs.Dispose() }
        if ([Array]::IndexOf($head, [byte]0, 0, $n) -ge 0) { $binary++; continue }
        $read++
        $t = [System.IO.File]::ReadAllText($path)
        foreach ($m in [regex]::Matches($t, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]|\r(?!\n)')) {
            $bad += ('{0}:{1} U+{2:X4}' -f $f, $t.Substring(0, $m.Index).Split("`n").Count, [int][char]$m.Value)
        }
    }
    $bad | ForEach-Object { Write-Host "    $_" }
    # NONE READ IS NOT CLEAN. A git that listed nothing would otherwise pass this with 0 of 0.
    Write-Host ("  {0} text files read, {1} binary skipped, {2} control characters" -f $read, $binary, $bad.Count)
    $read -gt 0 -and $bad.Count -eq 0
}

Step 'checks with code' {
    # NO CODE CHANGE WITHOUT A CHECK CHANGE (CLAUDE.md section 6: every change carries its check).
    # Measured since the last VERIFIED: commit, working tree included, so a batch of slices is judged
    # as one: if any line of code under scripts/ that is not a comment moved and neither harness
    # file did, something new or changed went in with nothing proving it. Coarse on purpose -- it
    # cannot tell WHICH check covers which change; that is the Checks: line in the commit and the
    # reviewer's job. What it can do is make the lazy case impossible to miss.
    $env:Path += ';C:\Program Files\Git\cmd'
    $base = (& git log --grep='^VERIFIED:' -1 --format=%H 2>$null | Select-Object -First 1)
    if (-not $base) { Write-Host '  no VERIFIED: commit to measure from'; return $true }
    $code = 0
    foreach ($line in @(& git diff $base -- scripts/ 2>$null)) {
        if ($line -match '^[+-](?![+-])\s*(.*)$' -and $Matches[1] -ne '' -and $Matches[1] -notmatch '^//') { $code++ }
    }
    $newCode = @(& git ls-files --others --exclude-standard -- scripts/ 2>$null | Where-Object { $_ -like '*.cs' })
    $code += $newCode.Count
    $harness = @(& git diff --name-only $base -- tools/smoketest/SmokeTest.cs.txt tools/screens/Shots.cs.txt 2>$null | Where-Object { $_ })
    Write-Host ("  since {0}: {1} code lines moved under scripts/, {2} harness file(s) changed" -f $base.Substring(0, 7), $code, $harness.Count)
    if ($code -gt 0 -and $harness.Count -eq 0) { Write-Host '  code changed with no check added or rewritten' }
    $code -eq 0 -or $harness.Count -gt 0
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
        $counts = @()
        foreach ($i in 1..$runs) {
            $o = & powershell @smokeArgs -Godot $engine 2>&1
            $pass = @($o | Where-Object { $_ -cmatch 'PASS ' }).Count
            $done = @($o | Where-Object { $_ -cmatch 'DONE' }).Count
            $counts += $pass
            # THE SEEDS, HERE, WHERE THEY SURVIVE. Every run wipes the scratch folder, so run
            # 3 destroys run 2's logs and with them the number that reproduces run 2's geometry.
            # Printed per run, any failure can be put back with run.ps1 -Seed <n>.
            $seeds = @($o | Where-Object { $_ -cmatch 'SEED \d' } |
                       ForEach-Object { ($_ -split 'SEED ')[-1].Trim() })
            if ($seeds.Count) { Write-Host ("  run {0} seeds: {1}" -f $i, ($seeds -join ", ")) }
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
        # ...AND THE SUITE DID NOT QUIETLY SHRINK. $pass was computed and thrown away, so six
        # DONE lines with twelve checks behind them passed exactly like six with eleven
        # hundred: this step measured COMPLETION and never COVERAGE. There is no number to
        # keep up to date -- the runs are compared against EACH OTHER, so it fails when
        # checks stop running rather than when somebody adds some.
        if (($counts | Select-Object -Unique).Count -ne 1) {
            Write-Host ("  the runs do not agree on how many checks ran: {0}" -f ($counts -join ", "))
            Write-Host "  a check that runs sometimes is a check that proves nothing."
            $allOk = $false
        }
        if ($counts[0] -lt 1) { Write-Host "  no checks ran at all."; $allOk = $false }
        Write-Host ("  {0} checks, the same in every run" -f $counts[0])
        $allOk
    }

    Step 'screenshot sweep' {
        $o = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\screens\run.ps1' -Godot $engine 2>&1
        $o | Where-Object { $_ -cmatch '^SWEEP|^frames' } | ForEach-Object { Write-Host "  $_" }
        $ok = ($o -join "`n") -match 'SWEEP DONE' -and ($o -join "`n") -match 'LINT: 0'
        # ...AND IT DREW SOMETHING. The frame count was printed and never read, so a sweep
        # that rendered NOTHING still said SWEEP DONE and LINT: 0 -- nothing to draw is
        # nothing to lint, and an empty pass looked exactly like a clean one.
        $frames = 0
        if (($o -join "`n") -match 'frames:\s*(\d+)') { $frames = [int]$Matches[1] }
        if ($frames -lt 1) { Write-Host "  the sweep drew no frames, so LINT: 0 means nothing."; $ok = $false }
        # a failure that printed no SWEEP line (a stopped script) would otherwise be silent
        if (-not $ok) { $o | Select-Object -Last 8 | ForEach-Object { Write-Host "    $_" } }
        $ok
    }
}

if ($Update) {
    Step 'map + snapshot + manifest' {
        $m = & python 'tools\map.py' 2>&1
        $a = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\snapshot.ps1' 2>&1
        $b = & powershell -NoProfile -ExecutionPolicy Bypass -File 'tools\manifest.ps1' 2>&1
        ($m + $a + $b) | ForEach-Object { Write-Host "  $_" }
        ($b -join "`n") -match ', 0 not verifying'
    }
}

# Only after -Update, which has just rebuilt the manifest. In -Quick and -Fast the manifest is
# still the LAST change's, so this could only ever say FAILED -- a red line in every mid-session
# run, which is how a real failure gets waved through. It is not skipped there, it is not asked.
if ($Update) {
Step 'integrity (version/MANIFEST.sha256)' {
    $sha = 'C:\Program Files\Git\usr\bin\sha256sum.exe'
    if (-not (Test-Path $sha)) { Write-Host "  sha256sum not found (Git for Windows); skipped"; return $true }
    $r = & $sha -c 'version\MANIFEST.sha256' 2>&1
    $bad = @($r | Where-Object { $_ -notmatch ': OK$' -and $_ -notmatch 'WARNING' })
    Write-Host ("  {0} files, {1} mismatched" -f @($r | Where-Object { $_ -match ': OK$' }).Count, $bad.Count)
    $bad | ForEach-Object { Write-Host "    $_" }
    $bad.Count -eq 0
}
}

Write-Host ""
Write-Host "================ VERDICT ================"
foreach ($k in $results.Keys) { Write-Host ("  {0,-34} {1}" -f $k, $(if ($results[$k]) { 'OK' } else { 'FAILED' })) }
$failed = @($results.Keys | Where-Object { -not $results[$_] })
if ($failed.Count -eq 0) { Write-Host "ALL CHECKS PASSED"; exit 0 }
Write-Host ("FAILED: {0}" -f ($failed -join ', ')); exit 1
