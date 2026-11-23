param([string]$Redraw, [string]$Clean, [string]$WithCustomer, [string]$OutDir,
      [int]$RedMin = 16, [int]$MinR = 26, [int]$CleanMax = 16, [int]$MinArea = 4, [int]$MinAvgRed = 26, [int]$MaxHole = 40)
# Blood layer from a repainted screenshot: red pixels that the clean game render does not have,
# cleaned (small/edge specks removed, pool holes kept as glossy highlights) and snapped to a blood palette.
# Also writes the customer silhouette (where the repaint has no information) as a mask.
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Blood2 {
    public static Bitmap Resize(Bitmap src, int w, int h, bool nearest) {
        var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst)) {
            g.InterpolationMode = nearest ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor : System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(src, new Rectangle(0, 0, w, h));
        }
        return dst;
    }
    static byte[] Bytes(Bitmap b) {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var a = new byte[d.Stride * b.Height]; Marshal.Copy(d.Scan0, a, 0, a.Length); b.UnlockBits(d); return a;
    }
    static Bitmap FromBytes(byte[] a, int w, int h) {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(a, 0, d.Scan0, a.Length); b.UnlockBits(d); return b;
    }
    static readonly int[][] Pal = {
        new[]{34,5,7}, new[]{54,8,10}, new[]{76,12,14}, new[]{100,16,18}, new[]{128,22,24}, new[]{156,32,32}, new[]{184,52,46},
        new[]{92,36,28}, new[]{124,60,48},                // dried brown
        new[]{176,108,100}, new[]{214,168,156}            // glossy highlight
    };
    static void Snap(byte[] a, int i) {
        int best = 0, bd = int.MaxValue;
        for (int k = 0; k < Pal.Length; k++) {
            int dr = a[i + 2] - Pal[k][0], dg = a[i + 1] - Pal[k][1], db = a[i] - Pal[k][2];
            int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
            if (d < bd) { bd = d; best = k; }
        }
        a[i + 2] = (byte)Pal[best][0]; a[i + 1] = (byte)Pal[best][1]; a[i] = (byte)Pal[best][2];
    }
    public static string Run(Bitmap redraw, Bitmap clean, Bitmap withCust, string outDir,
                             int redMin, int minR, int cleanMax, int minArea, int minAvgRed, int maxHole) {
        int w = redraw.Width, h = redraw.Height;
        var r = Bytes(redraw); var c = Bytes(clean); var wc = Bytes(withCust);
        var m = new bool[w * h];
        Func<byte[], int, int> Red = (a, i) => a[i + 2] - Math.Max(a[i + 1], a[i]);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                if (y >= 262) continue;                 // counter and below
                if (x >= 560 && y < 48) continue;       // clock / hearts
                int i = (y * w + x) * 4;
                if (Red(r, i) < redMin || r[i + 2] < minR) continue;
                bool cleanRed = false;   // the clean render is red nearby: a shifted edge of a red product, not blood
                for (int oy = -2; oy <= 2 && !cleanRed; oy++) for (int ox = -2; ox <= 2 && !cleanRed; ox++) {
                    int nx = x + ox, ny = y + oy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (Red(c, (ny * w + nx) * 4) > cleanMax) cleanRed = true;
                }
                if (!cleanRed) m[y * w + x] = true;
            }
        // components (8-connected): drop tiny or weakly red ones
        var comp = new int[w * h]; for (int k = 0; k < comp.Length; k++) comp[k] = -1;
        var q = new Queue<int>(); var members = new List<int>(); int kept = 0, dropped = 0;
        for (int s = 0; s < m.Length; s++) {
            if (!m[s] || comp[s] >= 0) continue;
            members.Clear(); q.Enqueue(s); comp[s] = s; long red = 0;
            while (q.Count > 0) {
                int p = q.Dequeue(); members.Add(p); red += Red(r, p * 4);
                int px = p % w, py = p / w;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) {
                    int nx = px + ox, ny = py + oy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx; if (!m[j] || comp[j] >= 0) continue; comp[j] = s; q.Enqueue(j);
                }
            }
            if (members.Count < minArea || red / members.Count < minAvgRed) { foreach (int p in members) m[p] = false; dropped++; } else kept++;
        }
        // holes (4-connected non-blood pockets fully surrounded by blood): keep the repaint colour (glossy highlights)
        var seen = new bool[w * h]; int holes = 0;
        for (int s = 0; s < m.Length; s++) {
            if (m[s] || seen[s]) continue;
            members.Clear(); q.Enqueue(s); seen[s] = true; bool edge = false;
            while (q.Count > 0) {
                int p = q.Dequeue(); members.Add(p);
                int px = p % w, py = p / w;
                if (px == 0 || py == 0 || px == w - 1 || py == h - 1) edge = true;
                if (members.Count > maxHole) edge = true;
                int[] nx = { px - 1, px + 1, px, px }, ny = { py, py, py - 1, py + 1 };
                for (int k = 0; k < 4; k++) {
                    int a = nx[k], b = ny[k]; if (a < 0 || b < 0 || a >= w || b >= h) continue;
                    int j = b * w + a; if (m[j] || seen[j]) continue; seen[j] = true; q.Enqueue(j);
                }
            }
            if (!edge) { foreach (int p in members) m[p] = true; holes++; }
        }
        // output
        var ov = new byte[r.Length]; var compo = (byte[])c.Clone(); int n = 0;
        for (int p = 0; p < m.Length; p++) {
            if (!m[p]) continue; int i = p * 4;
            ov[i] = r[i]; ov[i + 1] = r[i + 1]; ov[i + 2] = r[i + 2]; ov[i + 3] = 255; Snap(ov, i);
            compo[i] = ov[i]; compo[i + 1] = ov[i + 1]; compo[i + 2] = ov[i + 2]; n++;
        }
        // customer silhouette = where the render with the customer differs from the empty one (dilated 2px)
        var gap = new bool[w * h];
        for (int y = 110; y < 266; y++) for (int x = 230; x < 368; x++) {
            int i = (y * w + x) * 4;
            int d = Math.Abs(wc[i] - c[i]) + Math.Abs(wc[i + 1] - c[i + 1]) + Math.Abs(wc[i + 2] - c[i + 2]);
            if (d > 30) gap[y * w + x] = true;
        }
        var gapOut = new byte[r.Length]; int gn = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            bool on = false;
            for (int oy = -2; oy <= 2 && !on; oy++) for (int ox = -2; ox <= 2 && !on; ox++) {
                int nx = x + ox, ny = y + oy; if (nx >= 0 && ny >= 0 && nx < w && ny < h && gap[ny * w + nx]) on = true;
            }
            if (!on) continue; int i = (y * w + x) * 4; gapOut[i] = gapOut[i + 1] = gapOut[i + 2] = gapOut[i + 3] = 255; gn++;
            if (ov[i + 3] > 0) n--;
            ov[i] = ov[i + 1] = ov[i + 2] = ov[i + 3] = 0;   // the repaint shows the customer here (skin is reddish), not blood
            compo[i] = (byte)(c[i] / 2); compo[i + 1] = (byte)(c[i + 1] / 2 + 100); compo[i + 2] = (byte)(c[i + 2] / 2);   // tint green in the preview
        }
        FromBytes(ov, w, h).Save(System.IO.Path.Combine(outDir, "blood_paint.png"), ImageFormat.Png);
        FromBytes(gapOut, w, h).Save(System.IO.Path.Combine(outDir, "blood_gap.png"), ImageFormat.Png);
        Resize(FromBytes(compo, w, h), w * 3, h * 3, true).Save(System.IO.Path.Combine(outDir, "blood2_composite_3x.png"), ImageFormat.Png);
        return string.Format("blood px {0}, components kept {1} dropped {2}, holes {3}, gap px {4}", n, kept, dropped, holes, gn);
    }
}
"@ -ReferencedAssemblies System.Drawing
$red = [System.Drawing.Bitmap]::FromFile($Redraw)
$cln = [System.Drawing.Bitmap]::FromFile($Clean)
$wcu = [System.Drawing.Bitmap]::FromFile($WithCustomer)
$r640 = [Blood2]::Resize($red, 640, 360, $false)
$c640 = [Blood2]::Resize($cln, 640, 360, $true)
$w640 = [Blood2]::Resize($wcu, 640, 360, $true)
[Blood2]::Run($r640, $c640, $w640, $OutDir, $RedMin, $MinR, $CleanMax, $MinArea, $MinAvgRed, $MaxHole)
$red.Dispose(); $cln.Dispose(); $wcu.Dispose()
