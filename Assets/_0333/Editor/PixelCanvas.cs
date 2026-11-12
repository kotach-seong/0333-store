using System.IO;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 코드로 픽셀아트를 그리기 위한 간단한 캔버스.
    /// 좌표는 "월드 픽셀" 기준(카메라 정중앙이 0,0, 위쪽이 +y)이며
    /// 캔버스가 덮는 영역(rect) 밖에 그리면 무시된다.
    /// </summary>
    public class PixelCanvas
    {
        public readonly RectInt Rect;
        public bool WrapY;
        readonly Color32[] px;

        public int Width => Rect.width;
        public int Height => Rect.height;

        public PixelCanvas(RectInt rect)
        {
            Rect = rect;
            px = new Color32[rect.width * rect.height];
        }

        bool Index(int x, int y, out int i)
        {
            int cx = x - Rect.x;
            int cy = y - Rect.y;
            if (WrapY) cy = ((cy % Height) + Height) % Height;
            i = 0;
            if (cx < 0 || cy < 0 || cx >= Width || cy >= Height) return false;
            i = cy * Width + cx;
            return true;
        }

        public Color32 Get(int x, int y) => Index(x, y, out int i) ? px[i] : default;

        /// <summary>알파 블렌딩으로 칠한다.</summary>
        public void Set(int x, int y, Color32 c)
        {
            if (c.a == 0 || !Index(x, y, out int i)) return;
            px[i] = c.a == 255 ? c : Blend(px[i], c);
        }

        /// <summary>블렌딩 없이 덮어쓴다(투명 구멍을 낼 때 사용).</summary>
        public void Put(int x, int y, Color32 c)
        {
            if (Index(x, y, out int i)) px[i] = c;
        }

        public void Fill(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    Set(x, y, c);
        }

        public void Outline(int x0, int y0, int x1, int y1, Color32 c, int t = 1)
        {
            Fill(x0, y0, x1, y0 + t, c);
            Fill(x0, y1 - t, x1, y1, c);
            Fill(x0, y0, x0 + t, y1, c);
            Fill(x1 - t, y0, x1, y1, c);
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>원형 빛 번짐. 가장자리로 갈수록 투명해진다.</summary>
        public void Glow(float cx, float cy, float radius, Color32 c, float maxAlpha)
        {
            int r = Mathf.CeilToInt(radius);
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) / radius;
                    if (d >= 1f) continue;
                    float a = maxAlpha * (1f - d) * (1f - d);
                    Set(x, y, Pal.Alpha(c, a));
                }
        }

        public void FillCircle(float cx, float cy, float radius, Color32 c)
        {
            int r = Mathf.CeilToInt(radius);
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                    if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) <= radius)
                        Set(x, y, c);
        }

        /// <summary>3x5 픽셀 숫자/콜론. (x,y)는 글자의 왼쪽 위.</summary>
        public void Digits(int x, int y, string text, Color32 c)
        {
            foreach (char ch in text)
            {
                string[] g = PixelFont.Glyph(ch);
                if (g != null)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (g[row][col] == '1') Set(x + col, y - row, c);
                x += 4;
            }
        }

        public void SavePng(string path, int scale = 1)
        {
            var tex = new Texture2D(Width * scale, Height * scale, TextureFormat.RGBA32, false);
            if (scale == 1)
            {
                tex.SetPixels32(px);
            }
            else
            {
                var big = new Color32[Width * scale * Height * scale];
                for (int y = 0; y < Height * scale; y++)
                    for (int x = 0; x < Width * scale; x++)
                        big[y * Width * scale + x] = px[(y / scale) * Width + x / scale];
                tex.SetPixels32(big);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>다른 캔버스를 이 캔버스 위에 합성한다(미리보기 이미지용).</summary>
        public void Composite(PixelCanvas src, Vector2Int offset)
        {
            for (int y = src.Rect.yMin; y < src.Rect.yMax; y++)
                for (int x = src.Rect.xMin; x < src.Rect.xMax; x++)
                    Set(x + offset.x, y + offset.y, src.Get(x, y));
        }

        static Color32 Blend(Color32 dst, Color32 src)
        {
            float sa = src.a / 255f;
            float da = dst.a / 255f;
            float oa = sa + da * (1f - sa);
            if (oa <= 0f) return default;
            byte Ch(byte s, byte d) => (byte)Mathf.Clamp(Mathf.RoundToInt((s * sa + d * da * (1f - sa)) / oa), 0, 255);
            return new Color32(Ch(src.r, dst.r), Ch(src.g, dst.g), Ch(src.b, dst.b), (byte)Mathf.RoundToInt(oa * 255f));
        }
    }

    public static class Pal
    {
        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        public static Color32 Alpha(Color32 c, float a) =>
            new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(a), 0, 255));

        public static Color32 Mul(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        public static Color32 Add(Color32 c, int d) =>
            new Color32((byte)Mathf.Clamp(c.r + d, 0, 255), (byte)Mathf.Clamp(c.g + d, 0, 255), (byte)Mathf.Clamp(c.b + d, 0, 255), c.a);

        public static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));

        /// <summary>좌표 기반 결정적 해시(0~1).</summary>
        public static float Noise(int a, int b, int c = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    public static class PixelFont
    {
        static readonly string[][] digits =
        {
            new[] { "111", "101", "101", "101", "111" },
            new[] { "010", "110", "010", "010", "111" },
            new[] { "111", "001", "111", "100", "111" },
            new[] { "111", "001", "111", "001", "111" },
            new[] { "101", "101", "111", "001", "001" },
            new[] { "111", "100", "111", "001", "111" },
            new[] { "111", "100", "111", "101", "111" },
            new[] { "111", "001", "001", "001", "001" },
            new[] { "111", "101", "111", "101", "111" },
            new[] { "111", "101", "111", "001", "111" },
        };
        static readonly string[] colon = { "000", "010", "000", "010", "000" };
        static readonly string[] dot = { "000", "000", "000", "000", "010" };

        public static string[] Glyph(char c)
        {
            if (c >= '0' && c <= '9') return digits[c - '0'];
            if (c == ':') return colon;
            if (c == '.') return dot;
            return null;
        }

        /// <summary>글자 칸 좌표(cu: 오른쪽으로, cv: 아래로)가 켜진 점인지. 한 글자는 가로 4칸(3+간격).</summary>
        public static bool Hit(string text, float cu, float cv)
        {
            if (cu < 0f || cv < 0f || cv >= 5f) return false;
            int ch = (int)(cu / 4f);
            if (ch >= text.Length) return false;
            int col = (int)cu - ch * 4;
            if (col > 2) return false;
            string[] g = Glyph(text[ch]);
            return g != null && g[(int)cv][col] == '1';
        }
    }
}
