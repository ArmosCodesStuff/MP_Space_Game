# PLAY THE GAME FROM SOURCE -- for anyone who has just cloned this repo.
#
#     powershell -ExecutionPolicy Bypass -File tools\play.ps1
#     ...or double-click PLAY.bat at the root, which is that line and nothing else.
#
# There is no runnable file in this repo and there is not meant to be: no .exe, no .pck, and
# dist\ is gitignored. A release is an OUTPUT (tools\pack.ps1 makes one). What a clone has is the
# project, and this runs it.
#
#     -Editor   open the Godot editor instead of running the game
#     -Godot    an explicit path to the engine, if the search cannot find it
#
# WHAT IT NEEDS, and what it says when it is missing:
#   * Godot 4.7.2 MONO/.NET for Windows. The plain build cannot run this at all -- it is a C#
#     project, and the plain build has no C# in it. Resolved by tools\find-godot.ps1, which also
#     looks at $env:WARSHIPS_GODOT and local.config.ps1, so nobody has to hard-code a path.
#   * the .NET 8 SDK, because Godot builds the C# assembly before the window opens.
# Both are checked here rather than left to fail inside the engine, where the error is a wall of
# unrelated text and the actual cause is one line in the middle of it.
param([switch]$Editor, [string]$Godot)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$repo = (Get-Location).Path

# THE .NET SDK. Godot shells out to `dotnet build`; without it the engine opens and then fails to
# load a single script, which reads like the project is broken rather than the machine.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'play: no .NET SDK on this machine.'
    Write-Host '      This is a C# game and the engine builds it before it runs.'
    Write-Host '      Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0'
    exit 2
}

# THE ENGINE, through the one resolver every other tool here uses. Called in process: it keeps its
# explanation on the host stream so it cannot pollute the path it prints, and a CHILD powershell
# would merge the two and hand back the explanation glued to the front of the path.
$engine = & (Join-Path $PSScriptRoot 'find-godot.ps1') -Godot $Godot
if ($LASTEXITCODE -ne 0 -or -not $engine) {
    Write-Host ''
    Write-Host 'play: no Godot found. Get Godot 4.7.2 for Windows, the .NET / MONO build:'
    Write-Host '      https://godotengine.org/download/archive/4.7.2-stable/'
    Write-Host '      Then either put it somewhere obvious (Desktop, Downloads, C:\Godot), or:'
    Write-Host '        $env:WARSHIPS_GODOT = "C:\path\to\Godot_v4.7.2-stable_mono_win64.exe"'
    Write-Host '      ...or write that path into local.config.ps1 at the repo root, as'
    Write-Host '        $Godot = "C:\path\to\..."'
    Write-Host '      (local.config.ps1 is gitignored, so it is yours and survives a pull.)'
    exit 2
}
$engine = (Resolve-Path $engine).Path

# THE MONO BUILD OR NOTHING. The plain export of Godot has no C# runtime, so it opens this project
# and then cannot load one script in it -- a failure that looks like the project's fault and is not.
if ($engine -notmatch 'mono') {
    Write-Host "play: $engine does not look like the mono/.NET build."
    Write-Host '      A C# project needs it. The filename has "mono" in it.'
    exit 2
}

Write-Host "play: $(Split-Path $engine -Leaf)"
if ($Editor) {
    Write-Host 'play: opening the editor. Press F5 in it to run.'
    & $engine --path $repo -e
} else {
    # First run on a fresh clone imports every asset and builds the assembly before a window
    # appears. Saying so is the difference between waiting and assuming it has hung.
    Write-Host 'play: starting. A FIRST run on a fresh clone takes a minute or two -- every asset'
    Write-Host '      is imported and the C# is built before the window opens. It has not hung.'
    & $engine --path $repo
}
