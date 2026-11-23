param([string]$Src, [string]$OutDir, [int]$Thresh = 22, [int]$SeedX = 830, [int]$SeedY = 550, [int]$Seed2X = 830, [int]$Seed2Y = 320, [int]$HoleMaxY = 410,
      [int]$X0 = 680, [int]$Y0 = 260, [int]$X1 = 990, [int]$Y1 = 700, [int]$OutW = 640, [int]$OutH = 360, [double]$Cover = 0.5)
# Cut the dark figure out of a repainted screenshot and shrink it to game pixels (640x360 screen).
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class MonX {
    static byte[] Bytes(Bitmap b, out int stride) {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = d.Stride;
        var a = new byte[d.Stride * b.Height]; Marshal.Copy(d.Scan0, a, 0, a.Length); b.UnlockBits(d); return a;
    }
    static Bitmap FromBytes(byte[] a, int w, int h) {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(a, 0, d.Scan0, a.Length); b.UnlockBits(d); return b;
    }
    public static string Run(Bitmap src, string outDir, int thresh, int sx, int sy, int sx2, int sy2, int holeMaxY, int x0, int y0, int x1, int y1, int outW, int outH, double cover) {
        int W = src.Width, H = src.Height, st;
        var px = Bytes(src, out st);
        Func<int, int, int> L = (x, y) => { int i = y * st + x * 4; return (px[i + 2] * 30 + px[i + 1] * 59 + px[i] * 11) / 100; };
        var mask = new bool[W * H];
        // 1) dark region connected to the seed
        var q = new Queue<int>(); q.Enqueue(sy * W + sx); mask[sy * W + sx] = true; q.Enqueue(sy2 * W + sx2); mask[sy2 * W + sx2] = true;
        while (q.Count > 0) {
            int p = q.Dequeue(), x = p % W, y = p / W;
            int[] nx = { x - 1, x + 1, x, x }, ny = { y, y, y - 1, y + 1 };
            for (int k = 0; k < 4; k++) {
                int a = nx[k], b = ny[k];
                if (a < x0 || b < y0 || a >= x1 || b >= y1) continue;
                int j = b * W + a; if (mask[j]) continue;
                if (L(a, b) < thresh) { mask[j] = true; q.Enqueue(j); }
            }
        }
        // 2) fill holes (eyes, teeth): non-mask pixels in the box not reachable from the box border
        var outside = new bool[W * H];
        for (int x = x0; x < x1; x++) { foreach (int y in new[] { y0, y1 - 1 }) { int j = y * W + x; if (!mask[j] && !outside[j]) { outside[j] = true; q.Enqueue(j); } } }
        for (int y = y0; y < y1; y++) { foreach (int x in new[] { x0, x1 - 1 }) { int j = y * W + x; if (!mask[j] && !outside[j]) { outside[j] = true; q.Enqueue(j); } } }
        while (q.Count > 0) {
            int p = q.Dequeue(), x = p % W, y = p / W;
            int[] nx = { x - 1, x + 1, x, x }, ny = { y, y, y - 1, y + 1 };
            for (int k = 0; k < 4; k++) {
                int a = nx[k], b = ny[k];
                if (a < x0 || b < y0 || a >= x1 || b >= y1) continue;
                int j = b * W + a; if (mask[j] || outside[j]) continue;
                outside[j] = true; q.Enqueue(j);
            }
        }
        for (int y = y0; y < Math.Min(y1, holeMaxY); y++) for (int x = x0; x < x1; x++) { int j = y * W + x; if (!outside[j]) mask[j] = true; }
        // 2b) close short vertical gaps in the head band (the grin splits head from neck)
        for (int x = x0; x < x1; x++) {
            int last = -1;
            for (int y = y0; y < Math.Min(y1, holeMaxY + 40); y++) {
                if (!mask[y * W + x]) continue;
                if (last >= 0 && y - last > 1 && y - last - 1 <= 26 && last < holeMaxY)
                    for (int k = last + 1; k < y; k++) mask[k * W + x] = true;
                last = y;
            }
        }
        // 3) shrink to game pixels: coverage + average colour
        double s = (double)W / outW;
        int u0 = (int)Math.Floor(x0 / s), u1 = (int)Math.Ceiling(x1 / s), v0 = (int)Math.Floor(y0 / s), v1 = (int)Math.Ceiling(y1 / s);
        int ow = u1 - u0, oh = v1 - v0;
        var outPx = new byte[ow * oh * 4];
        int minU = int.MaxValue, maxU = -1, minV = int.MaxValue, maxV = -1;
        for (int v = v0; v < v1; v++)
            for (int u = u0; u < u1; u++) {
                int fx0 = (int)Math.Floor(u * s), fx1 = (int)Math.Ceiling((u + 1) * s), fy0 = (int)Math.Floor(v * s), fy1 = (int)Math.Ceiling((v + 1) * s);
                int tot = 0, inn = 0; long r = 0, g = 0, b = 0;
                for (int y = fy0; y < fy1 && y < H; y++)
                    for (int x = fx0; x < fx1 && x < W; x++) {
                        tot++;
                        if (!mask[y * W + x]) continue;
                        inn++; int i = y * st + x * 4; r += px[i + 2]; g += px[i + 1]; b += px[i];
                    }
                if (tot == 0 || inn < tot * cover) continue;
                int o = ((v - v0) * ow + (u - u0)) * 4;
                outPx[o] = (byte)(b / inn); outPx[o + 1] = (byte)(g / inn); outPx[o + 2] = (byte)(r / inn); outPx[o + 3] = 255;
                minU = Math.Min(minU, u); maxU = Math.Max(maxU, u); minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
            }
        var full = FromBytes(outPx, ow, oh);
        var crop = full.Clone(new Rectangle(minU - u0, minV - v0, maxU - minU + 1, maxV - minV + 1), PixelFormat.Format32bppArgb);
        crop.Save(System.IO.Path.Combine(outDir, "monster_640.png"), ImageFormat.Png);
        // preview: mask over the source
        var prev = (byte[])px.Clone();
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (!mask[y * W + x]) { int i = y * st + x * 4; prev[i] = 255; prev[i + 1] = 0; prev[i + 2] = 255; }
        var pb = FromBytes(prev, W, H).Clone(new Rectangle(x0, y0, x1 - x0, y1 - y0), PixelFormat.Format32bppArgb);
        pb.Save(System.IO.Path.Combine(outDir, "monster_mask_preview.png"), ImageFormat.Png);
        return string.Format("screen box u {0}..{1} v {2}..{3} (size {4}x{5})", minU, maxU, minV, maxV, maxU - minU + 1, maxV - minV + 1);
    }
}
"@ -ReferencedAssemblies System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($Src)
$bmp = New-Object System.Drawing.Bitmap $img
[MonX]::Run($bmp, $OutDir, $Thresh, $SeedX, $SeedY, $Seed2X, $Seed2Y, $HoleMaxY, $X0, $Y0, $X1, $Y1, $OutW, $OutH, $Cover)
$img.Dispose(); $bmp.Dispose()
