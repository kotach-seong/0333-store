param([string]$Src, [string]$Dst, [int]$Extend = 24)
# Tidy the shrunk monster: 3 body shades, faint rim light, crisp glowing eyes and grin, body extended below the counter line.
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class MonF {
    public static void Run(Bitmap src, string dst, int extend) {
        int w = src.Width, h0 = src.Height, h = h0 + extend;
        var c = new Color[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c[x, y] = src.GetPixel(x, Math.Min(y, h0 - 1));
        Func<int, int, bool> In = (x, y) => x >= 0 && y >= 0 && x < w && y < h && c[x, y].A > 0;
        // smooth the outline: drop 1-pixel spurs, fill 1-pixel notches
        var c2 = (Color[,])c.Clone();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            int n = (In(x - 1, y) ? 1 : 0) + (In(x + 1, y) ? 1 : 0) + (In(x, y - 1) ? 1 : 0) + (In(x, y + 1) ? 1 : 0);
            if (In(x, y) && n <= 1) c2[x, y] = Color.Transparent;
            else if (!In(x, y) && n >= 3) c2[x, y] = Color.FromArgb(255, 14, 14, 18);
        }
        c = c2;
        var o = new Color[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            if (!In(x, y)) { o[x, y] = Color.Transparent; continue; }
            // 3x3 average brightness so fabric folds read as soft shading, not specks
            int sum = 0, cnt = 0;
            for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) {
                if (!In(x + ox, y + oy)) continue; var q = c[x + ox, y + oy]; sum += (q.R * 30 + q.G * 59 + q.B * 11) / 100; cnt++;
            }
            int L = sum / cnt;
            Color col = L < 13 ? Color.FromArgb(8, 8, 11) : L < 19 ? Color.FromArgb(14, 14, 18) : Color.FromArgb(22, 23, 28);
            bool edge = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1);
            if (edge) col = y < 60 ? Color.FromArgb(46, 48, 55) : Color.FromArgb(30, 31, 37);
            o[x, y] = col;
        }
        // eyes: white-hot core, orange ring, dark red glow
        foreach (int ex in new[] { 37, 51 }) {
            Color glow = Color.FromArgb(96, 26, 16), ring = Color.FromArgb(232, 122, 52), deep = Color.FromArgb(150, 52, 26), core = Color.FromArgb(255, 242, 206);
            o[ex - 1, 17] = glow; o[ex + 3, 17] = glow; o[ex + 1, 16] = glow; o[ex - 1, 18] = glow; o[ex + 3, 18] = glow;
            o[ex, 17] = ring; o[ex + 1, 17] = core; o[ex + 2, 17] = ring;
            o[ex, 18] = deep; o[ex + 1, 18] = ring; o[ex + 2, 18] = deep;
        }
        // grin: lip line, upper teeth (2 rows), gap, lower teeth, lip line
        int gx0 = 33, gx1 = 55;
        for (int x = gx0; x <= gx1; x++) {
            bool corner = x == gx0 || x == gx1;
            Color lip = Color.FromArgb(44, 14, 14), gum = Color.FromArgb(26, 8, 8);
            if (!corner) o[x, 26] = lip;
            bool upSep = (x - gx0) % 3 == 0, lowSep = (x - gx0) % 3 == 1;
            o[x, 27] = upSep ? Color.FromArgb(70, 50, 40) : Color.FromArgb(232, 222, 196);
            o[x, 28] = upSep ? Color.FromArgb(60, 40, 32) : Color.FromArgb(204, 190, 160);
            o[x, 29] = gum;
            o[x, 30] = lowSep ? Color.FromArgb(56, 38, 30) : Color.FromArgb(190, 176, 146);
            if (!corner) o[x, 31] = lip;
        }
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) b.SetPixel(x, y, o[x, y]);
        b.Save(dst, ImageFormat.Png);
        Console.WriteLine(w + "x" + h);
    }
}
"@ -ReferencedAssemblies System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($Src)
$bmp = New-Object System.Drawing.Bitmap $img
[MonF]::Run($bmp, $Dst, $Extend)
$img.Dispose(); $bmp.Dispose()
