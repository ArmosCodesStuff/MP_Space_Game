# The destroyer's hull: the battleship's, 75% as wide and 90% as long, on a 270 px square
# canvas (so 0.7467 u/px at 201.6 u, the battleship's own scale), with missile pods painted
# where the battleship's two inner turrets stood and along the sponsons.
# Run: powershell -ExecutionPolicy Bypass -File tools\make_destroyer.ps1 [-Preview <png>]
param([string]$Out = '', [string]$Preview = '')
$Root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Out) { $Out = Join-Path $Root 'destroyer_hull.png' }
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::FromFile((Join-Path $Root 'battleship_hull.png'))
$N = 270
$bmp = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$W = 300 * 0.75; $H = 300 * 0.9
$dst = New-Object System.Drawing.RectangleF ((($N - $W) / 2), 0, $W, $H)
$attr = New-Object System.Drawing.Imaging.ImageAttributes
$attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)   # no dark fringe at the edges
$g.DrawImage($src, [System.Drawing.Rectangle]::Round($dst), 0, 0, 300, 300, [System.Drawing.GraphicsUnit]::Pixel, $attr)

function C([string]$hex, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, [Convert]::ToInt32($hex.Substring(0,2),16), [Convert]::ToInt32($hex.Substring(2,2),16), [Convert]::ToInt32($hex.Substring(4,2),16)) }
$rim = C '2a3b57'; $wall = C '4e668b'; $lid = C '6f89b0'; $hi = C '9fb6d8'; $hatch = C '1c2739'

# one pod: a dark rim, a raised lid with a lit edge, the hatch in the middle
function Pod([float]$cx, [float]$cy, [float]$r) {
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $rim), $cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $ri = $r * 0.78
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $wall), $cx - $ri, $cy - $ri, 2 * $ri, 2 * $ri)
    $rl = $r * 0.62
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $lid), $cx - $rl, $cy - $rl, 2 * $rl, 2 * $rl)
    $pen = New-Object System.Drawing.Pen $hi, ([Math]::Max(0.8, $r * 0.18))
    $g.DrawArc($pen, $cx - $rl, $cy - $rl, 2 * $rl, 2 * $rl, 190, 110)
    $rh = $r * 0.26
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $hatch), $cx - $rh, $cy - $rh, 2 * $rh, 2 * $rh)
}
# a bank of pods on a dark plate
function Bank([float]$cx, [float]$cy, [int]$cols, [int]$rows, [float]$r, [float]$gap) {
    $w = $cols * 2 * $r + ($cols - 1) * $gap; $h = $rows * 2 * $r + ($rows - 1) * $gap
    $plate = New-Object System.Drawing.SolidBrush (C '33476a')
    $g.FillRectangle($plate, $cx - $w / 2 - 2, $cy - $h / 2 - 2, $w + 4, $h + 4)
    for ($i = 0; $i -lt $cols; $i++) { for ($j = 0; $j -lt $rows; $j++) {
        Pod ($cx - $w / 2 + $r + $i * (2 * $r + $gap)) ($cy - $h / 2 + $r + $j * (2 * $r + $gap)) $r
    } }
}
$cx = $N / 2
Bank $cx (100.7 * 0.9) 2 2 5.2 2.0     # where the battleship's second turret stood
Bank $cx (215.3 * 0.9) 2 2 5.2 2.0     # and its third
Bank $cx (100.7 * 0.9 + 22) 2 1 4.2 2.0
Bank $cx (215.3 * 0.9 - 22) 2 1 4.2 2.0
foreach ($side in -1, 1) {             # the sponsons: a pod fore and aft of each point-defence turret
    Pod ($cx + $side * 36) (150.6 * 0.9 - 22) 4.0
    Pod ($cx + $side * 36) (150.6 * 0.9 + 22) 4.0
}
$g.Dispose(); $src.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
if ($Preview) {
    # 3x, on a dark field, tinted like the default pilot, with the turrets overlaid, to judge it
    $p = New-Object System.Drawing.Bitmap ($N * 3), ($N * 3)
    $pg = [System.Drawing.Graphics]::FromImage($p)
    $pg.Clear((C '0b0f18'))
    $pg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $pg.DrawImage($bmp, 0, 0, $N * 3, $N * 3)
    $tm = [System.Drawing.Bitmap]::FromFile((Join-Path $Root 'turret_bs_main.png'))
    $tp = [System.Drawing.Bitmap]::FromFile((Join-Path $Root 'turret_bs_pd.png'))
    $k = 0.9
    foreach ($m in @(@(0.41, -65.41, $tm), @(0.38, 76.23, $tm), @(-33.25, 0.45, $tp), @(34.97, 0.45, $tp))) {
        $x = $m[0] * 0.75 / 0.7467; $y = $m[1] * 0.9 / 0.7467; $t = $m[2]
        if ($t -eq $tp) { $x = $m[0] * 0.75 / 0.7467 }
        $w = $t.Width * $k * 3; $h = $t.Height * $k * 3
        $pg.DrawImage($t, [float](($cx + $x) * 3 - $w / 2), [float](($N / 2 + $y) * 3 - $h / 2), [float]$w, [float]$h)
    }
    $pg.Dispose(); $p.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png); $p.Dispose(); $tm.Dispose(); $tp.Dispose()
}
$bmp.Dispose()
"wrote $Out"
