# THE SHIPS' ART. Two kinds of source, both in art_source\ (kept out of the game by its .gdignore):
#
# FINISHED ART, the owner's 2026-09-24 pack (art_source\pack_2026-09-24\): drawn and shaded already,
# on its own transparency. Each row of $Finished turns one file nose-up (quarter turns only, never
# mirrored: the lettering and the asymmetric hulls would flip), trims it to the drawing with the
# hull's keel as the texture's centre column, and writes it grey on transparent for the game to
# tint (the row's Tint). Nothing is redrawn, resampled, sharpened or shaded: a rerun gives the same
# pixels. It prints every mark and nozzle of the row in world units, for the row in the game.
#
# THE TURRETS (the pack has none), from the one line drawing left, art_source\turret.png:
#
#   turret_main.png      the main turret, barrels up, pivot at the sheet's centre (every class's,
#                        and the heavy raiders')     art_source\turret.png
#   turret_pd.png        the point-defence turret: the main turret's look, round and single-
#                        barrelled -- drawn here, at the main turret's scale
#
# Run: powershell -ExecutionPolicy Bypass -File tools\make_ships.ps1 [-Preview <png>]
param([string]$Preview = '')
$ErrorActionPreference = 'Stop'
$Root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Src = Join-Path $Root 'art_source'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// A drawing being worked: lightness (0 ink .. 1 paper) and coverage per pixel, and the points
// placed on it (continuous pixel coordinates: pixel i spans i..i+1), carried through every step.
public class Sheet
{
    public int W, H;
    public float[] L, A;
    public List<PointF> Marks = new List<PointF>();

    public Sheet(int w, int h, bool clear)
    {
        W = w; H = h; L = new float[w * h]; A = new float[w * h];
        for (int i = 0; i < L.Length; i++) { L[i] = clear ? 0f : 1f; A[i] = clear ? 0f : 1f; }
    }

    static byte[] Bytes(Bitmap b, out int stride)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = d.Stride;
        var px = new byte[d.Stride * b.Height];
        Marshal.Copy(d.Scan0, px, 0, px.Length);
        b.UnlockBits(d);
        return px;
    }

    // a drawing on paper: its lightness over white, all of it covered
    public static Sheet Paper(Bitmap b)
    {
        int st; var px = Bytes(b, out st);
        var s = new Sheet(b.Width, b.Height, false);
        for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int o = y * st + x * 4; float a = px[o + 3] / 255f;
                float l = (0.114f * px[o] + 0.587f * px[o + 1] + 0.299f * px[o + 2]) / 255f;
                s.L[y * s.W + x] = l * a + 1f - a;
            }
        return s;
    }

    // a drawing with its own transparency
    public static Sheet Cut(Bitmap b)
    {
        int st; var px = Bytes(b, out st);
        var s = new Sheet(b.Width, b.Height, true);
        for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int o = y * st + x * 4;
                s.L[y * s.W + x] = (0.114f * px[o] + 0.587f * px[o + 1] + 0.299f * px[o + 2]) / 255f;
                s.A[y * s.W + x] = px[o + 3] / 255f;
            }
        return s;
    }

    // FINISHED ART: a drawing with its own transparency, its lightness kept as drawn. What lies under
    // alpha 0 goes to black: the file's hidden background (the flagship's watermark) must not bleed
    // into the hull's edge through the engine's filtering.
    public static Sheet Finished(Bitmap b)
    {
        var s = Cut(b);
        for (int i = 0; i < s.L.Length; i++) if (s.A[i] <= 0f) s.L[i] = 0f;
        return s;
    }

    // the vertical line (continuous x) the COVER is most nearly mirror-symmetric about: a finished
    // hull's keel, where a row names none
    public float CoverAxis()
    {
        int x0 = W, x1 = -1;
        for (int i = 0; i < A.Length; i++) if (A[i] > 0.02f) { x0 = Math.Min(x0, i % W); x1 = Math.Max(x1, i % W); }
        double best = double.MaxValue; float axis = W / 2f;
        for (int c2 = x0 + x1 + 1 - (x1 - x0) / 5; c2 <= x0 + x1 + 1 + (x1 - x0) / 5; c2++)
        {
            double err = 0;
            for (int y = 0; y < H; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int m = c2 - 1 - x;
                    err += Math.Abs(A[y * W + x] - (m >= 0 && m < W ? A[y * W + m] : 0f));
                }
            if (err < best) { best = err; axis = c2 / 2f; }
        }
        return axis;
    }

    // cropped to the drawing plus `pad`, the keel (continuous x, to the nearest pixel edge) the
    // sheet's centre line: a hull whose sides differ keeps its keel on the node's centre
    public Sheet TrimOn(float keel, int pad)
    {
        int x0 = W, x1 = -1, y0 = H, y1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (A[y * W + x] > 0.02f) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        int k = (int)Math.Round(keel), half = Math.Max(k - x0, x1 + 1 - k) + pad;
        return Crop(k - half, y0 - pad, k + half, y1 + 1 + pad);
    }

    static float C01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }

    public Bitmap ToBitmap()
    {
        var b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var px = new byte[d.Stride * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x, o = y * d.Stride + x * 4;
                byte g = (byte)Math.Round(C01(L[i]) * 255f);
                px[o] = g; px[o + 1] = g; px[o + 2] = g; px[o + 3] = (byte)Math.Round(C01(A[i]) * 255f);
            }
        Marshal.Copy(px, 0, d.Scan0, px.Length);
        b.UnlockBits(d);
        return b;
    }

    public void Save(string path) { using (var b = ToBitmap()) b.Save(path, ImageFormat.Png); }

    public void Mark(float x, float y) { Marks.Add(new PointF(x, y)); }

    // turned a quarter anticlockwise: the right-hand edge becomes the top
    public Sheet TurnLeft()
    {
        var o = new Sheet(H, W, false);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int j = (W - 1 - x) * o.W + y;
                o.L[j] = L[y * W + x]; o.A[j] = A[y * W + x];
            }
        foreach (var p in Marks) o.Marks.Add(new PointF(p.Y, W - p.X));
        return o;
    }

    // painted over, x0..x1 by y0..y1, with the columns of a clean stretch from cleanX on, repeated
    // every `period`: for a band whose lines all run along it (a hull's spine)
    public void PatchColumns(int x0, int y0, int x1, int y1, int cleanX, int period)
    {
        for (int x = x0; x <= x1; x++)
        {
            int sx = cleanX + (x - x0) % period;
            for (int y = y0; y <= y1; y++) L[y * W + x] = L[y * W + sx];
        }
    }

    // the vertical line (continuous x, in half pixels) the ink is most nearly mirror-symmetric
    // about: ink with no ink opposite it -- or off the sheet opposite it -- is the mismatch
    public float FindAxis()
    {
        double best = double.MaxValue; float axis = W / 2f;
        for (int c2 = (int)(W * 0.8); c2 <= (int)(W * 1.2); c2++)
        {
            double err = 0;
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    int m = c2 - 1 - x;
                    float a = 1f - L[row + x], b = m >= 0 && m < W ? 1f - L[row + m] : 0f;
                    err += Math.Abs(a - b);
                }
            }
            if (err < best) { best = err; axis = c2 / 2f; }
        }
        return axis;
    }

    // redrawn at w by h (high-quality bicubic); the drawing must still be on its paper
    public Sheet Resize(int w, int h)
    {
        using (var src = ToBitmap())
        using (var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(dst))
        using (var at = new ImageAttributes())
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            at.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, W, H, GraphicsUnit.Pixel, at);
            var o = Paper(dst);
            float sx = (float)w / W, sy = (float)h / H;
            foreach (var p in Marks) o.Marks.Add(new PointF(p.X * sx, p.Y * sy));
            return o;
        }
    }

    float At(float[] f, float fill, int x, int y) { return x < 0 || x >= W || y < 0 || y >= H ? fill : f[y * W + x]; }
    float Sample(float[] f, float fill, float fx, int y)
    {
        int x0 = (int)Math.Floor(fx); float t = fx - x0;
        return (1 - t) * At(f, fill, x0, y) + t * At(f, fill, x0 + 1, y);
    }

    // exactly symmetrical: the kept half reflected across `axis` (continuous x), on a sheet of even
    // width whose centre line is the axis (what lay beyond the old sheet is transparent)
    public Sheet Mirror(float axis, bool keepLeft)
    {
        int half = (int)Math.Ceiling(Math.Max(axis, W - axis));
        var o = new Sheet(2 * half, H, false);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < o.W; x++)
            {
                float d = Math.Abs(x + 0.5f - half);
                float sx = (keepLeft ? axis - d : axis + d) - 0.5f;
                o.L[y * o.W + x] = Sample(L, 1f, sx, y);
                o.A[y * o.W + x] = Sample(A, 0f, sx, y);
            }
        foreach (var p in Marks) o.Marks.Add(new PointF(p.X - axis + half, p.Y));
        return o;
    }

    static float[] Blur(float[] src, int w, int h, float sigma)
    {
        int r = Math.Max(1, (int)Math.Ceiling(sigma * 3));
        var k = new float[2 * r + 1]; float sum = 0;
        for (int i = -r; i <= r; i++) { k[i + r] = (float)Math.Exp(-i * i / (2 * sigma * sigma)); sum += k[i + r]; }
        for (int i = 0; i < k.Length; i++) k[i] /= sum;
        var t = new float[src.Length]; var o = new float[src.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0;
                for (int i = -r; i <= r; i++) v += k[i + r] * src[y * w + Math.Min(w - 1, Math.Max(0, x + i))];
                t[y * w + x] = v;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0;
                for (int i = -r; i <= r; i++) v += k[i + r] * t[Math.Min(h - 1, Math.Max(0, y + i)) * w + x];
                o[y * w + x] = v;
            }
        return o;
    }

    // an unsharp mask: the blur of the enlargement taken back out of it
    public void Sharpen(float sigma, float amount)
    {
        var b = Blur(L, W, H, sigma);
        for (int i = 0; i < L.Length; i++) L[i] += amount * (L[i] - b[i]);
    }

    // ink below `black` solid, paper above `white` clean, the greys between stretched
    public void Levels(float black, float white)
    {
        for (int i = 0; i < L.Length; i++) L[i] = C01((L[i] - black) / (white - black));
    }

    // The light edge between the transparent and the solid ink, up to `band` px deep, un-mixed from
    // the white it was drawn on: black, at 1 - lightness. The outline then reads clean on any
    // background, and a sliver of paper left inside a cut is gone.
    public void Unmix(int band)
    {
        int n = W * H;
        var dist = new int[n];
        var q = new Queue<int>();
        for (int i = 0; i < n; i++) { dist[i] = A[i] <= 0f ? 0 : -1; if (dist[i] == 0) q.Enqueue(i); }
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % W, y = i / W, d = dist[i];
            if (d >= band) continue;
            int[] nb = { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 };
            foreach (int j in nb)
            {
                if (j < 0 || dist[j] >= 0 || L[j] < 0.5f) continue;
                dist[j] = d + 1; q.Enqueue(j);
            }
        }
        for (int i = 0; i < n; i++)
        {
            if (A[i] <= 0f) { L[i] = 0f; continue; }
            if (dist[i] > 0) { A[i] = Math.Min(A[i], C01(1f - L[i])); L[i] = 0f; }
        }
    }

    // everything outside `shape` transparent (drawn anti-aliased): for art lifted out of a busier
    // drawing, where a flood from the edge would stop at every line of the background
    public void MaskTo(GraphicsPath shape)
    {
        using (var m = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(m))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.FillPath(Brushes.White, shape);
            int st; var px = Bytes(m, out st);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) A[y * W + x] *= px[y * st + x * 4 + 3] / 255f;
        }
    }

    // cropped to the drawing plus `pad`, the centre line kept in the middle
    public Sheet Trim(int pad)
    {
        int x0 = W, x1 = -1, y0 = H, y1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (A[y * W + x] > 0.02f) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        int m = Math.Min(x0, W - 1 - x1);
        return Crop(m - pad, y0 - pad, W - m + pad, y1 + 1 + pad);
    }

    // padded so the continuous point p is the sheet's centre (to the nearest pixel)
    public Sheet CentreOn(PointF p)
    {
        int cx = (int)Math.Round(p.X), cy = (int)Math.Round(p.Y);
        int hw = Math.Max(cx, W - cx), hh = Math.Max(cy, H - cy);
        return Crop(cx - hw, cy - hh, cx + hw, cy + hh);
    }

    // the window x0..x1, y0..y1 (end exclusive; beyond the sheet is transparent), grown to even sides
    public Sheet Crop(int x0, int y0, int x1, int y1)
    {
        if ((x1 - x0) % 2 != 0) x1++;
        if ((y1 - y0) % 2 != 0) y1++;
        var o = new Sheet(x1 - x0, y1 - y0, true);
        for (int y = 0; y < o.H; y++)
            for (int x = 0; x < o.W; x++)
            {
                int sx = x + x0, sy = y + y0;
                if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                o.L[y * o.W + x] = L[sy * W + sx]; o.A[y * o.W + x] = A[sy * W + sx];
            }
        foreach (var p in Marks) o.Marks.Add(new PointF(p.X - x0, p.Y - y0));
        return o;
    }

    // half the size, each pixel the average of four (weighted by their cover, so no fringe)
    public Sheet Half()
    {
        var o = new Sheet(W / 2, H / 2, true);
        for (int y = 0; y < o.H; y++)
            for (int x = 0; x < o.W; x++)
            {
                float sa = 0, sl = 0;
                for (int j = 0; j < 2; j++)
                    for (int i = 0; i < 2; i++) { int k = (2 * y + j) * W + 2 * x + i; sa += A[k]; sl += A[k] * L[k]; }
                o.A[y * o.W + x] = sa / 4f; o.L[y * o.W + x] = sa > 1e-6f ? sl / sa : 0f;
            }
        foreach (var p in Marks) o.Marks.Add(new PointF(p.X / 2f, p.Y / 2f));
        return o;
    }

    // THE POINT-DEFENCE TURRET, drawn at working size: the main turret's look -- white plate, black
    // outline, a grey turntable -- but round, with one barrel. r the housing's radius, reach the
    // pivot to the muzzle, line the outline's width (all working px); the pivot is the centre.
    public static Sheet DrawPd(float r, float reach, float line)
    {
        int half = (int)Math.Ceiling(Math.Max(r, reach) + 2 * line + 2);
        using (var b = new Bitmap(2 * half, 2 * half, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(b))
        using (var ink = new Pen(Color.Black, line))
        using (var fine = new Pen(Color.Black, line * 0.6f))
        using (var plate = new SolidBrush(Color.White))
        using (var table = new SolidBrush(Color.FromArgb(255, 178, 178, 178)))
        using (var black = new SolidBrush(Color.Black))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            ink.LineJoin = LineJoin.Round;
            float c = half;
            // the housing, its turntable and hatch, and six bolts round the rim
            g.FillEllipse(plate, c - r, c - r, 2 * r, 2 * r); g.DrawEllipse(ink, c - r, c - r, 2 * r, 2 * r);
            float rt = r * 0.66f;
            g.FillEllipse(table, c - rt, c - rt, 2 * rt, 2 * rt); g.DrawEllipse(fine, c - rt, c - rt, 2 * rt, 2 * rt);
            float rh = r * 0.27f;
            g.FillEllipse(plate, c - rh, c - rh + r * 0.12f, 2 * rh, 2 * rh); g.DrawEllipse(fine, c - rh, c - rh + r * 0.12f, 2 * rh, 2 * rh);
            for (int k = 0; k < 6; k++)
            {
                double a = (k * 60 + 30) * Math.PI / 180; float bx = c + (float)Math.Cos(a) * r * 0.83f, by = c + (float)Math.Sin(a) * r * 0.83f, br = line * 0.5f;
                g.FillEllipse(black, bx - br, by - br, 2 * br, 2 * br);
            }
            // the barrel, over the housing: a tube from the turntable to a banded muzzle, and the
            // mantlet where it leaves the plate
            float bw = r * 0.34f;
            var tube = new RectangleF(c - bw / 2, c - reach, bw, reach - rt * 0.4f);
            g.FillRectangle(plate, tube); g.DrawRectangle(ink, tube.X, tube.Y, tube.Width, tube.Height);
            var muzzle = new RectangleF(c - bw * 0.72f, c - reach, bw * 1.44f, r * 0.34f);
            g.FillRectangle(plate, muzzle); g.DrawRectangle(ink, muzzle.X, muzzle.Y, muzzle.Width, muzzle.Height);
            var mantlet = new RectangleF(c - bw * 0.95f, c - r * 1.1f, bw * 1.9f, r * 0.5f);
            g.FillRectangle(plate, mantlet); g.DrawRectangle(ink, mantlet.X, mantlet.Y, mantlet.Width, mantlet.Height);
            var s = Cut(b);
            s.Mark(c, c); s.Mark(c, c - reach);
            return s;
        }
    }

    // a copy multiplied by a colour, as the game's Modulate draws it (for the preview)
    public Bitmap Tinted(Color c)
    {
        var b = ToBitmap();
        var d = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[d.Stride * H];
        Marshal.Copy(d.Scan0, px, 0, px.Length);
        for (int i = 0; i < px.Length; i += 4) { px[i] = (byte)(px[i] * c.B / 255); px[i + 1] = (byte)(px[i + 1] * c.G / 255); px[i + 2] = (byte)(px[i + 2] * c.R / 255); }
        Marshal.Copy(px, 0, d.Scan0, px.Length);
        b.UnlockBits(d);
        return b;
    }
}
'@

function Load([string]$name) { $b = New-Object System.Drawing.Bitmap (Join-Path $Src $name); try { [Sheet]::Paper($b) } finally { $b.Dispose() } }
# a mark's offset from the sheet's centre in world units, for a sheet `length` u tall
function U($s, [int]$i, [double]$length) {
    $k = $length / $s.H; $p = $s.Marks[$i]
    '({0:0.00}, {1:0.00})' -f (($p.X - $s.W / 2) * $k), (($p.Y - $s.H / 2) * $k)
}
function Out-Sheet($s, [string]$name) { $s.Save((Join-Path $Root $name)); "{0,-24} {1} x {2}" -f $name, $s.W, $s.H }

# ── FINISHED ART: one row per game texture ──
# Src the pack's file; Nose where its nose points in the file (Up, Right, Down, Left); Out the game's
# file (today's names, so every res:// path stays); Length the row's length in the game (world u).
# Every mark is in the NOSE-UP frame: px of the turned file, before the trim (x across, y down, the
# bow at the top). Keel the hull's centre line in x (left out: the line the cover is most nearly
# mirror-symmetric about). Marks, name = @(x, y): printed as the offset from the texture's centre in
# world units and as a share of the length. Nozzles, @(x, y, bell): the centre of each bell's aft rim
# and the bell's width, px -- printed in world units as the row's `new(x, y, bell)`.
$Pack = Join-Path $Src 'pack_2026-09-24'
$Finished = @(
    # the raiders (Enemies.All), their painted guns left on (too small to see under a turret)
    @{ Src = 'fighter_swept.png'; Nose = 'Up'; Out = 'enemy_light_fighter.png'; Length = 34
       Nozzles = @(@(102, 237, 18), @(168, 237, 18)) }
    @{ Src = 'frigate_b.png'; Nose = 'Left'; Out = 'enemy_heavy_hull.png'; Length = 136
       Marks = [ordered]@{ turret = @(140, 388); housing = @(162, 388) }
       Nozzles = @(@(82, 588, 63), @(193, 588, 63)) }
    @{ Src = 'fighter_tri_a.png'; Nose = 'Up'; Out = 'enemy_talon_hull.png'; Length = 40
       Nozzles = @(@(61.5, 264, 23), @(147.5, 264, 23)) }
    # drone_sensor.png is ONE game file on TWO rows (D18): the Pod (Enemies.All) below and the siege's
    # shield pylon (Emplacements.All), at each row's own Length
    @{ Src = 'drone_sensor.png'; Nose = 'Up'; Out = 'drone_sensor.png'; Length = 52
       Nozzles = @(,@(130, 241, 37)) }
    @{ Src = 'gunship_h.png'; Nose = 'Left'; Out = 'enemy_cross_hull.png'; Length = 120
       Marks = [ordered]@{ turret = @(161, 445); housing = @(191, 445) }
       Nozzles = @(@(108, 602, 62), @(210, 602, 62)) }
    @{ Src = 'frigate_d.png'; Nose = 'Left'; Out = 'enemy_lancerkin_hull.png'; Length = 150
       Marks = [ordered]@{ turret = @(93, 492); housing = @(118.5, 492) }
       Nozzles = @(@(42.5, 652, 59), @(142, 652, 59)) }
    # the title screen's Web (MenuFoe), at the webifier's length
    @{ Src = 'crescent_a.png'; Nose = 'Down'; Out = 'enemy_light_tier_2.png'; Length = 34
       Nozzles = @(@(218.5, 320, 51), @(351.5, 320, 51)) }
    # the bosses (Missions.Bosses): no moving turrets, so their painted guns stay; their bells are
    # listed (a boss draws no flame). Measured at these lengths; the game draws them Missions.BossSize
    # times as large (its rows scale Length, HalfWidth and these bells by it)
    @{ Src = 'frigate_a.png'; Nose = 'Right'; Out = 'boss_raider.png'; Length = 360
       Nozzles = @(@(115, 610, 66), @(218.5, 610, 65)) }
    @{ Src = 'flagship.png'; Nose = 'Right'; Out = 'boss_drake.png'; Length = 420
       Nozzles = @(@(184.5, 1358, 67), @(266, 1356, 66), @(346.5, 1300, 51), @(422, 1355, 64), @(520, 1355, 72)) }
    # the base's fleet. The hauler (Hauler.Art): its six cargo frames are the pods (pod0-2 the port
    # bays inside their rails, bow to stern; podcorner the first bay's fore-port corner), its point
    # defence on the bow dome (pd), its span at the frames' outer rails (side)
    @{ Src = 'cargo_4.png'; Nose = 'Left'; Out = 'hauler.png'; Length = 200
       Marks = [ordered]@{ pod0 = @(61, 245.5); pod1 = @(61, 340.5); pod2 = @(61, 434.5); podcorner = @(29, 210); pd = @(153.5, 51); side = @(22, 245.5) }
       Nozzles = @(@(89, 643, 50), @(216.5, 644, 51)) }
    # the gatherers (Gathering.All, every row: one drone, tinted per row): the beam and the load leave
    # from the emitter, in the claws' mouth; side is the span the raiders hold off
    @{ Src = 'drone_salvager.png'; Nose = 'Right'; Out = 'gatherer.png'; Length = 40
       Marks = [ordered]@{ emitter = @(165, 100); side = @(7, 120) }
       Nozzles = @(@(111.5, 557, 50), @(166, 570, 62), @(222, 558, 52)) }
    # the lanes' couriers (Lanes.All, all four rows)
    @{ Src = 'drone_economy.png'; Nose = 'Left'; Out = 'courier.png'; Length = 30
       Nozzles = @(@(92.5, 682, 46), @(140, 685, 48), @(187.5, 682, 46)) }
    # the carrier's wing (Wings.All): the bomber's torpedoes leave from its wingtip rails' front
    # (launch, the port rail; the starboard is its mirror)
    @{ Src = 'fighter_delta.png'; Nose = 'Up'; Out = 'wing_fighter.png'; Length = 17
       Nozzles = @(@(102, 243, 20), @(176, 243, 20)) }
    @{ Src = 'fighter_g.png'; Nose = 'Right'; Out = 'wing_bomber.png'; Length = 28.125
       Marks = [ordered]@{ launch = @(16.5, 232) }
       Nozzles = @(@(105.5, 446, 33), @(204.5, 446, 33)) }
    # the siege (Emplacements.All): the pirate base, the owner's pick ("the crescent hull is the
    # pirate base"). It carries no guns of its own and draws no flame, so no marks or nozzles;
    # Out keeps the game's existing res:// path, so nothing else moves
    @{ Src = 'crescent_b.png'; Nose = 'Up'; Out = 'pirate_base.png'; Length = 483 }
    # the 12 player classes (Classes.All), hull art only -- hit sizes (HalfWidth) unchanged (Q1).
    # The battleship's 6 painted twin housings are patched clean of their barrels (Q4: painted guns
    # under a moving turret are painted out on player hulls); 4 of the 6 -- the two forward rows,
    # both flanks -- carry the real Mains (Q3's default); the aft flanking pair and the 3 centre
    # (keel) turrets stay painted, unarmed. Every other class keeps today's mount literals except
    # where noted (POST): the new art's own landmark is a clear match for the old one.
    @{ Src = 'battleship_bb05.png'; Nose = 'Left'; Out = 'battleship_hull.png'; Length = 378
       Marks = [ordered]@{ main1 = @(118.9, 231.2); main2 = @(118.9, 305.8); pd = @(50.2, 517.8) }
       Patches = @(
           @(105,183,133,239, 94,6), @(199,183,227,239, 188,6),
           @(105,258,133,314, 94,6), @(199,258,227,314, 188,6),
           @(75,325,120,410, 124,6), @(212,325,257,410, 205,6)
       ) }
    @{ Src = 'carrier_a.png'; Nose = 'Right'; Out = 'carrier_player.png'; Length = 283.5
       Marks = [ordered]@{ pdflank = @(92.5, 338.5) } }
    @{ Src = 'destroyer_dd22.png'; Nose = 'Right'; Out = 'destroyer_hull.png'; Length = 212.625
       Marks = [ordered]@{ main1 = @(132.5, 235.6); main2 = @(132.5, 315.4); pd = @(55.2, 285.5) } }
    @{ Src = 'cargo_2.png'; Nose = 'Left'; Out = 'freight_hauler_hull.png'; Length = 230 }
    @{ Src = 'cargo_3.png'; Nose = 'Left'; Out = 'freight_tender_hull.png'; Length = 230 }
    @{ Src = 'frigate_c.png'; Nose = 'Right'; Out = 'freight_bastion_hull.png'; Length = 230
       Marks = [ordered]@{ main = @(168, 425) } }
    @{ Src = 'fighter_unit_c.png'; Nose = 'Right'; Out = 'heavy_sniper_hull.png'; Length = 120 }
    @{ Src = 'fighter_unit_b.png'; Nose = 'Left'; Out = 'heavy_warrior_hull.png'; Length = 120 }
    @{ Src = 'fighter_e.png'; Nose = 'Left'; Out = 'heavy_warden_hull.png'; Length = 120 }
    @{ Src = 'fighter_unit_d.png'; Nose = 'Left'; Out = 'light_dart_hull.png'; Length = 70 }
    @{ Src = 'fighter_f.png'; Nose = 'Left'; Out = 'light_echo_hull.png'; Length = 70
       Marks = [ordered]@{ main = @(168, 75) } }
    @{ Src = 'fighter_unit_a.png'; Nose = 'Right'; Out = 'light_wraith_hull.png'; Length = 70 }
)
$Turns = @{ Up = 0; Right = 1; Down = 2; Left = 3 }
$Made = @()
foreach ($row in $Finished) {
    $bm = New-Object System.Drawing.Bitmap (Join-Path $Pack $row.Src)
    try { $s = [Sheet]::Finished($bm) } finally { $bm.Dispose() }
    for ($i = 0; $i -lt $Turns[$row.Nose]; $i++) { $s = $s.TurnLeft() }
    # PAINTED GUNS UNDER A MOVING TURRET, painted out (Q4): each Patches entry is
    # (x0,y0,x1,y1,cleanX,period), NOSE-UP frame px, run before the marks so a mark can still
    # land inside the patched area
    if ($row.Patches) { foreach ($p in $row.Patches) { $s.PatchColumns($p[0], $p[1], $p[2], $p[3], $p[4], $p[5]) } }
    $keel = if ($row.Keel) { [float]$row.Keel } else { $s.CoverAxis() }
    $names = @()
    if ($row.Marks) { foreach ($m in $row.Marks.GetEnumerator()) { $s.Mark($m.Value[0], $m.Value[1]); $names += $m.Key } }
    if ($row.Nozzles) { foreach ($n in $row.Nozzles) { $s.Mark($n[0], $n[1]); $names += 'nozzle' } }
    $s.Mark($keel, 0)
    $s = $s.TrimOn($keel, 1)
    Out-Sheet $s $row.Out
    $k = $row.Length / $s.H
    "  keel {0:0.0} px: {1:0.00} u off the texture's centre" -f $keel, (($s.Marks[$s.Marks.Count - 1].X - $s.W / 2) * $k)
    for ($i = 0; $i -lt $names.Count; $i++) {
        $p = $s.Marks[$i]; $x = ($p.X - $s.W / 2) * $k; $y = ($p.Y - $s.H / 2) * $k
        if ($names[$i] -eq 'nozzle') { $n = $row.Nozzles[$i - ($names.Count - $row.Nozzles.Count)]; "  nozzle  new({0:0.00}f, {1:0.00}f, {2:0.00}f)" -f $x, $y, ($n[2] * $k) }
        else { "  {0,-7} new({1:0.00}f, {2:0.00}f)  ({3:0.0000} of the length aft of centre)" -f $names[$i], $x, $y, ($y / $row.Length) }
    }
    $Made += ,@($s, $row)
}

# ── the main turret: lifted out of its drawing (a flood would stop at the hull lines behind it),
#    barrels up, the housing's centre the pivot. 12 u across the housing at the battleship's
#    turret scale (5.5 px/u): the housing is 133 px of the drawing, worked at 132. ──
$t = (Load 'turret.png').TurnLeft()
$t.Mark(81, 147.5); $t.Mark(81, 12)                                 # 0 the pivot, 1 the muzzles
$axis = $t.FindAxis()
$k = 132.0 / 133.0
$t = $t.Resize([int][Math]::Round($t.W * $k), [int][Math]::Round($t.H * $k))
$shape = New-Object System.Drawing.Drawing2D.GraphicsPath
function S([double]$v) { [float]($v * $k) }
# the housing, its corners cut; the two barrels
$shape.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF (S 31), (S 79)), (New-Object System.Drawing.PointF (S 131), (S 79)),
    (New-Object System.Drawing.PointF (S 150), (S 98)), (New-Object System.Drawing.PointF (S 150), (S 198)),
    (New-Object System.Drawing.PointF (S 131), (S 217)), (New-Object System.Drawing.PointF (S 31), (S 217)),
    (New-Object System.Drawing.PointF (S 12), (S 198)), (New-Object System.Drawing.PointF (S 12), (S 98))))
$shape.AddRectangle((New-Object System.Drawing.RectangleF (S 46.5), (S 10), (S 19), (S 72)))
$shape.AddRectangle((New-Object System.Drawing.RectangleF (S 94.5), (S 10), (S 19), (S 72)))
$t.MaskTo($shape)
$t = $t.Mirror($axis * $k, $true)
$t.Sharpen(0.6, 0.6); $t.Levels(0.16, 0.9); $t.Unmix(3)
$t = $t.CentreOn($t.Marks[0]).Half()
Out-Sheet $t 'turret_main.png'
$tk = 1 / 5.5
"  main turret: housing 12 u at {0:0.0000} u/px; pivot to muzzle {1:0.00} u" -f $tk, (($t.Marks[0].Y - $t.Marks[1].Y) * $tk)

# ── the point-defence turret, at the same scale: a 6 u housing, the muzzle 5.5 u out ──
$p = [Sheet]::DrawPd(3.0 * 11, 5.5 * 11, 3.6).Half()
Out-Sheet $p 'turret_pd.png'
"  pd turret: ring {0:0.00} u; pivot to muzzle {1:0.00} u" -f 3.0, (($p.Marks[0].Y - $p.Marks[1].Y) * $tk)

if ($Preview) {
    # every hull at 2 px/u on a dark field, in the default hull colour, the turrets in the accent
    # on the marks, a 15 u grid to measure by
    $ppu = 2.0
    $main = [System.Drawing.Color]::FromArgb(255, 153, 153, 153); $acc = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
    $bg = [System.Drawing.Color]::FromArgb(255, 11, 15, 24)
    $ships = @()
    foreach ($m in $Made) { $ships += ,@($m[0], [double]$m[1].Length, $m[1]) }
    $Wp = 60; foreach ($s in $ships) { $Wp += [int]($s[0].W * $s[1] / $s[0].H * $ppu) + 60 }
    $Hp = [int](($ships | ForEach-Object { $_[1] } | Measure-Object -Maximum).Maximum * $ppu) + 80
    $out = New-Object System.Drawing.Bitmap $Wp, $Hp
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.Clear($bg); $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $grid = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 120, 160, 255)), 1
    for ($x = 0; $x -lt $Wp; $x += 30) { $g.DrawLine($grid, $x, 0, $x, $Hp) }
    for ($y = 0; $y -lt $Hp; $y += 30) { $g.DrawLine($grid, 0, $y, $Wp, $y) }
    $tm = $t.Tinted($acc); $tp = $p.Tinted($acc)
    function Place($bmp, [double]$cx, [double]$cy, [double]$w, [double]$h, [double]$deg) {
        $st = $g.Save(); $g.TranslateTransform([float]$cx, [float]$cy); $g.RotateTransform([float]$deg)
        $g.DrawImage($bmp, [float](-$w / 2), [float](-$h / 2), [float]$w, [float]$h); $g.Restore($st)
    }
    $x0 = 60; $cy = $Hp / 2
    foreach ($s in $ships) {
        $sh = $s[0]; $len = $s[1]; $w = $sh.W * $len / $sh.H * $ppu; $hh = $len * $ppu; $cx = $x0 + $w / 2
        $hb = $sh.Tinted($main); Place $hb $cx $cy $w $hh 0; $hb.Dispose()
        $u = $len / $sh.H * $ppu
        function At($i) { @(($cx + ($sh.Marks[$i].X - $sh.W / 2) * $u), ($cy + ($sh.Marks[$i].Y - $sh.H / 2) * $u)) }
        if ($s.Count -gt 2) {
            # a finished row: a main turret on each 'turret' mark, as wide as its 'housing' mark says;
            # every other mark a cyan cross; each nozzle a red bar the bell's width on its aft rim
            $row = $s[2]; $names = @(); if ($row.Marks) { $names = @($row.Marks.Keys) }
            $cyan = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 220, 255)), 1
            for ($i = 0; $i -lt $names.Count; $i++) {
                $q = At $i
                if ($names[$i] -eq 'housing') { continue }
                if ($names[$i] -ne 'turret') {
                    $g.DrawLine($cyan, [float]($q[0] - 3), [float]$q[1], [float]($q[0] + 3), [float]$q[1])
                    $g.DrawLine($cyan, [float]$q[0], [float]($q[1] - 3), [float]$q[0], [float]($q[1] + 3)); continue
                }
                $hw = 2 * [Math]::Abs($sh.Marks[$names.IndexOf('housing')].X - $sh.Marks[$i].X) * $u
                Place $tm $q[0] $q[1] $hw ($hw * $t.H / $t.W) 0
            }
            $cyan.Dispose()
            $red = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 70, 50)), 2
            for ($j = 0; $j -lt $row.Nozzles.Count; $j++) {
                $q = At ($names.Count + $j); $bw = $row.Nozzles[$j][2] * $u / 2
                $g.DrawLine($red, [float]($q[0] - $bw), [float]$q[1], [float]($q[0] + $bw), [float]$q[1])
            }
            $red.Dispose()
        }
        $x0 += $w + 60
    }
    $tm.Dispose(); $tp.Dispose(); $g.Dispose()
    $out.Save($Preview); $out.Dispose()
    "preview: $Preview"
}
