# Windows port of run.sh -- code analysers over the game scripts (unused private members,
# unread fields, unused assignments and parameters, needless usings, unreachable code).
#
# Unlike the Linux version this does NOT need the smoke test's restored build folder:
# NuGet is reachable on Windows, so it builds the project in place. The temporary
# .editorconfig is always removed again, even if the build fails.
# Prints each finding; the last line is the count.

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ec = Join-Path $root '.editorconfig'

if (Test-Path $ec) {
  Write-Host "REFUSING: $ec already exists -- it would be overwritten and then deleted."
  exit 2
}

$log = Join-Path $env:TEMP 'warships_analyse.log'
Copy-Item (Join-Path $PSScriptRoot 'analysers.editorconfig') $ec
try {
  Push-Location $root
  # -t:Rebuild so analyser warnings re-emit; an incremental build reports nothing.
  & dotnet build -t:Rebuild --nologo -v q `
      -p:EnforceCodeStyleInBuild=true -p:GenerateDocumentationFile=true `
      -p:NoWarn='CS1591;CS1573;CS1587' *> $log
  Pop-Location
} finally {
  Remove-Item $ec -Force -ErrorAction SilentlyContinue
}

$findings = Get-Content $log -ErrorAction SilentlyContinue |
  Select-String -Pattern 'warning (IDE|CS)[0-9]+' |
  ForEach-Object { $_.Line } |
  Where-Object { $_ -notmatch '_Test\.cs' } |
  ForEach-Object { ($_ -replace '.*[\\/]scripts[\\/]', '') -replace ' \[.*', '' } |
  Sort-Object -Unique

$findings | ForEach-Object { Write-Host $_ }
Write-Host "ANALYSERS: $(@($findings).Count) findings"
