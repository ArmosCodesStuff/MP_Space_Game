# Shade a flat line-art sprite so it reads as lit metal: a bevel from the hull's edge (light from the
# top-left), shadow gathering along every panel line, and a soft darkening round the silhouette.
param([string]$In, [string]$Out, [double]$Bevel = 26, [double]$LineShade = 0.45, [double]$Light = 0.5, [int]$Scale = 1, [int]$Detail = 0)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Shade
{
    public static void Run(string inPath, string outPath, double bevel, double lineShade, double light, int scale, int detail)
    {
        using (var src = Bigger(inPath, scale))
        {
            int W = src.Width, H = src.Height, n = W * H;
            var d = src.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new byte[d.Stride * H];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            var L = new float[n]; var A = new float[n];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int o = y * d.Stride + x * 4, i = y * W + x;
                L[i] = (0.114f * px[o] + 0.587f * px[o + 1] + 0.299f * px[o + 2]) / 255f;
                A[i] = px[o + 3] / 255f;
            }
            // distance from the outside, in pixels (two-pass chamfer)
            var dist = new float[n];
            for (int i = 0; i < n; i++) dist[i] = A[i] > 0.5f ? 1e9f : 0f;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; float v = dist[i];
                if (x > 0) v = Math.Min(v, dist[i - 1] + 1); if (y > 0) v = Math.Min(v, dist[i - W] + 1);
                if (x > 0 && y > 0) v = Math.Min(v, dist[i - W - 1] + 1.414f);
                if (x < W - 1 && y > 0) v = Math.Min(v, dist[i - W + 1] + 1.414f);
                dist[i] = v;
            }
            for (int y = H - 1; y >= 0; y--) for (int x = W - 1; x >= 0; x--)
            {
                int i = y * W + x; float v = dist[i];
                if (x < W - 1) v = Math.Min(v, dist[i + 1] + 1); if (y < H - 1) v = Math.Min(v, dist[i + W] + 1);
                if (x < W - 1 && y < H - 1) v = Math.Min(v, dist[i + W + 1] + 1.414f);
                if (x > 0 && y < H - 1) v = Math.Min(v, dist[i + W - 1] + 1.414f);
                dist[i] = v;
            }
            if (detail > 0) Detail(L, A, dist, W, H, detail);
            // the ink's own shadow: how much line there is nearby
            var ink = new float[n];
            for (int i = 0; i < n; i++) ink[i] = A[i] > 0.5f && L[i] < 0.5f ? 1f : 0f;
            var near = Blur(ink, W, H, (float)Math.Max(2, bevel * 0.16));
            // the bevel's height, and its normal
            var hgt = new float[n];
            for (int i = 0; i < n; i++) hgt[i] = (float)Math.Min(1.0, dist[i] / bevel);
            var smooth = Blur(hgt, W, H, (float)Math.Max(1, bevel * 0.25));
            float lx = -0.7f, ly = -0.7f;                       // light from the top-left
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (A[i] <= 0.004f) continue;
                float gx = smooth[i + (x < W - 1 ? 1 : 0)] - smooth[i - (x > 0 ? 1 : 0)];
                float gy = smooth[i + (y < H - 1 ? W : 0)] - smooth[i - (y > 0 ? W : 0)];
                float lam = -(gx * lx + gy * ly) * 14f;          // slope toward the light
                float shade = 0.80f + (float)light * Math.Max(-1f, Math.Min(1f, lam));
                shade *= 1f - (float)lineShade * near[i];         // shadow along the lines
                shade *= 0.60f + 0.40f * Math.Min(1f, dist[i] / 9f);   // and round the silhouette
                float v = L[i] * shade;
                byte g = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255)));
                int o = y * d.Stride + x * 4;
                px[o] = g; px[o + 1] = g; px[o + 2] = g;
            }
            Marshal.Copy(px, 0, d.Scan0, px.Length);
            src.UnlockBits(d);
            src.Save(outPath, ImageFormat.Png);
        }
    }
    // the sprite at `scale`x, its transparency carried properly (premultiplied)
    static Bitmap Bigger(string path, int scale)
    {
        var src = new Bitmap(path);
        if (scale <= 1) return src;
        var big = new Bitmap(src.Width * scale, src.Height * scale, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(big))
        using (var at = new ImageAttributes())
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            at.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
            g.DrawImage(src, new Rectangle(0, 0, big.Width, big.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, at);
        }
        src.Dispose();
        return big;
    }

    // DETAIL, drawn into the plate: faint seams across the open metal, rivets along them, and a fine
    // grain over everything -- all low contrast, so it reads as surface rather than as new structure.
    static void Detail(float[] L, float[] A, float[] dist, int w, int h, int step)
    {
        var rnd = new Random(7);
        var grain = new float[L.Length];
        for (int i = 0; i < grain.Length; i++) grain[i] = (float)rnd.NextDouble();
        var soft = Blur(grain, w, h, 1.6f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            if (A[i] < 0.5f || L[i] < 0.55f) continue;             // ink and paper edges keep their own tone
            float open = Math.Min(1f, (dist[i] - 6f) / 10f);        // only well inside the plate
            if (open <= 0) continue;
            float v = L[i] * (0.985f + 0.03f * (soft[i] - 0.5f));   // the grain
            if (y % step == 0) v *= 1f - 0.10f * open;               // a seam across the plate
            if (y % step == 0 && x % (step / 3) == 0) v *= 1f - 0.35f * open;   // rivets along it
            L[i] = v;
        }
    }

    static float[] Blur(float[] s, int w, int h, float sigma)
    {
        int r = Math.Max(1, (int)Math.Ceiling(sigma * 2.5f));
        var k = new float[2 * r + 1]; float sum = 0;
        for (int i = -r; i <= r; i++) { k[i + r] = (float)Math.Exp(-i * i / (2f * sigma * sigma)); sum += k[i + r]; }
        for (int i = 0; i < k.Length; i++) k[i] /= sum;
        var t = new float[s.Length]; var o = new float[s.Length];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        { float v = 0; for (int i = -r; i <= r; i++) v += k[i + r] * s[y * w + Math.Min(w - 1, Math.Max(0, x + i))]; t[y * w + x] = v; }
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        { float v = 0; for (int i = -r; i <= r; i++) v += k[i + r] * t[Math.Min(h - 1, Math.Max(0, y + i)) * w + x]; o[y * w + x] = v; }
        return o;
    }
}
'@
[Shade]::Run($In, $Out, $Bevel, $LineShade, $Light, $Scale, $Detail)
"shaded: $Out"
