# Rewrite a PCM WAV at a new level.
#
#     powershell -ExecutionPolicy Bypass -File tools\gain.ps1 -In sfx\x.wav -Out sfx\y.wav -Factor 0.5
#
# WHY A FILE AND NOT A NUMBER IN THE CODE: a sound's level belongs to the sound. Scattered
# volume_db offsets at call sites mean the same asset is four different loudnesses depending on
# who plays it, and "make the fighters quieter" turns into a hunt. Every sfx file here is stored
# at the level it is meant to be heard at, and Sfx plays it at 0 dB.
#
# Factor is LINEAR amplitude, not decibels: 0.65 is "35% quieter" as a person means it. Only
# reductions are expected, so no clipping check is needed -- but samples are clamped anyway,
# because a factor above 1 that wrapped round would sound like destruction rather than like a
# mistake.
param([Parameter(Mandatory)][string]$In, [Parameter(Mandatory)][string]$Out, [Parameter(Mandatory)][double]$Factor)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$b = [System.IO.File]::ReadAllBytes($In)
if ([System.Text.Encoding]::ASCII.GetString($b, 0, 4) -ne 'RIFF' -or
    [System.Text.Encoding]::ASCII.GetString($b, 8, 4) -ne 'WAVE') { throw "$In is not a RIFF/WAVE file" }

# Walk the chunks rather than assuming the data starts at 44: a file with a LIST or fact chunk
# would otherwise have its header scaled as if it were audio.
$fmt = -1; $bits = 0; $dataAt = -1; $dataLen = 0; $p = 12
while ($p + 8 -le $b.Length) {
    $id = [System.Text.Encoding]::ASCII.GetString($b, $p, 4)
    $len = [BitConverter]::ToUInt32($b, $p + 4)
    if ($id -eq 'fmt ') { $fmt = [BitConverter]::ToUInt16($b, $p + 8); $bits = [BitConverter]::ToUInt16($b, $p + 22) }
    elseif ($id -eq 'data') { $dataAt = $p + 8; $dataLen = [int][Math]::Min($len, $b.Length - $dataAt) }
    $p += 8 + $len + ($len % 2)      # chunks are word-aligned
}
if ($fmt -ne 1 -or $bits -ne 16) { throw "$In is not 16-bit PCM (format $fmt, $bits bits)" }
if ($dataAt -lt 0) { throw "$In has no data chunk" }

for ($i = $dataAt; $i -lt $dataAt + $dataLen - 1; $i += 2) {
    $v = [int][BitConverter]::ToInt16($b, $i)
    $v = [int][Math]::Round($v * $Factor)
    if ($v -gt 32767) { $v = 32767 } elseif ($v -lt -32768) { $v = -32768 }
    $s = [BitConverter]::GetBytes([int16]$v)
    $b[$i] = $s[0]; $b[$i + 1] = $s[1]
}
[System.IO.File]::WriteAllBytes((Join-Path (Get-Location) $Out), $b)
Write-Host ("{0} -> {1}  x{2:0.0000}  ({3} samples)" -f $In, $Out, $Factor, [int]($dataLen / 2))
