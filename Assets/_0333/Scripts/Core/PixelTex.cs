using System;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 게임 중에 코드로 그리는 작은 픽셀 그림(금전등록기 숫자, 영수증, 돈 등).
    /// 좌표는 그림의 왼쪽 아래가 (0,0), 1칸 = 화면 1픽셀.
    /// </summary>
    public class PixelTex
    {
        public readonly int W, H;
        public readonly Color32[] Px;
        Texture2D tex;
        Sprite sprite;
        Vector2 spritePivot = new Vector2(-1f, -1f);

        public PixelTex(int w, int h)
        {
            W = Mathf.Max(1, w);
            H = Mathf.Max(1, h);
            Px = new Color32[W * H];
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Color32 Get(int x, int y) => In(x, y) ? Px[y * W + x] : default;

        public void Clear(Color32 c = default)
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = c;
        }

        /// <summary>그대로 덮어쓴다.</summary>
        public void Put(int x, int y, Color32 c)
        {
            if (In(x, y)) Px[y * W + x] = c;
        }

        /// <summary>반투명이면 아래 색과 섞는다.</summary>
        public void Set(int x, int y, Color32 c)
        {
            if (c.a == 0 || !In(x, y)) return;
            int i = y * W + x;
            Px[i] = c.a == 255 ? c : Blend(Px[i], c);
        }

        /// <summary>(x0,y0)부터 (x1,y1) 바로 앞까지 칠한다.</summary>
        public void Fill(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) Set(x, y, c);
        }

        public void Outline(int x0, int y0, int x1, int y1, Color32 c)
        {
            Fill(x0, y0, x1, y0 + 1, c);
            Fill(x0, y1 - 1, x1, y1, c);
            Fill(x0, y0, x0 + 1, y1, c);
            Fill(x1 - 1, y0, x1, y1, c);
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

        public Texture2D Apply()
        {
            if (tex == null)
            {
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
            }
            tex.SetPixels32(Px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>그림을 올리고 스프라이트로 돌려준다. pivot은 0~1(왼쪽 아래가 0,0).</summary>
        public Sprite ToSprite(Vector2 pivot)
        {
            Apply();
            if (sprite == null || spritePivot != pivot)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, W, H), pivot, 1f, 0, SpriteMeshType.FullRect);
                sprite.hideFlags = HideFlags.DontSave;
                spritePivot = pivot;
            }
            return sprite;
        }

        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c);
            return c;
        }

        public static Color32 Mul(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        public static Color32 Alpha(Color32 c, float a) => new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(a * 255f, 0, 255));

        /// <summary>좌표마다 늘 같은 값이 나오는 0~1 난수(종이 결 같은 잔무늬용).</summary>
        public static float Noise(int a, int b, int c = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + c * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static Color32 Blend(Color32 dst, Color32 src)
        {
            float sa = src.a / 255f, da = dst.a / 255f;
            float oa = sa + da * (1f - sa);
            if (oa <= 0f) return default;
            byte Ch(byte s, byte d) => (byte)Mathf.Clamp(Mathf.RoundToInt((s * sa + d * da * (1f - sa)) / oa), 0, 255);
            return new Color32(Ch(src.r, dst.r), Ch(src.g, dst.g), Ch(src.b, dst.b), (byte)Mathf.RoundToInt(oa * 255f));
        }
    }

    /// <summary>매장 그림과 같은 원근 계산. 매장 속 한 점(미터)을 화면(월드 픽셀) 위치로 바꾼다.</summary>
    [Serializable]
    public struct StoreProjection
    {
        public float focal, vanishY, eyeHeight;

        public static StoreProjection Default => new StoreProjection { focal = 333f, vanishY = 6f, eyeHeight = 1.6f };

        public Vector2 ToScreen(Vector3 p) => new Vector2(focal * p.x / p.z, vanishY + focal * (p.y - eyeHeight) / p.z);
    }
}
