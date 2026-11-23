using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// CCTV에서만 보이는 것들의 원본 그림(코드로 그림). CustomerBuilder.MakeCctvSprite가 키에 맞게 줄이고 야간 카메라처럼 흑백으로 바꾼다.
    ///  - 손님 뒤에 붙은 귀신: 흰 옷, 얼굴을 덮은 긴 검은 머리(앞모습/뒷모습)
    ///  - 괴물: 길쭉한 검은 몸, 긴 팔, 앞모습은 하얀 눈과 들쭉날쭉한 이빨
    ///  - 밖에서 보는 귀신들: 검은 실루엣, 앞모습은 하얀 두 눈
    /// 발이 그림 맨 아래에 닿는다(가로 가운데).
    /// </summary>
    public static class CctvOddArt
    {
        static Texture2D New(int w, int h, out Color32[] px)
        {
            px = new Color32[w * h];
            return new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        static Texture2D Done(Texture2D t, Color32[] px)
        {
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static void Put(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c;
        }

        static void Ellipse(Color32[] px, int w, int h, float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Put(px, w, h, x, y, c);
                }
        }

        static void Bar(Color32[] px, int w, int h, float x0, float x1, int y0, int y1, Color32 c)
        {
            for (int y = y0; y < y1; y++)
                for (int x = Mathf.RoundToInt(x0); x < Mathf.RoundToInt(x1); x++) Put(px, w, h, x, y, c);
        }

        // ── 손님 뒤에 붙은 귀신 ──
        public static Texture2D Ghost(bool front)
        {
            const int w = 64, h = 160;
            var t = New(w, h, out var px);
            var dress = new Color32(232, 232, 226, 255);
            var dressShade = new Color32(196, 198, 194, 255);
            var skin = new Color32(214, 212, 206, 255);
            var hair = new Color32(14, 14, 16, 255);
            float cx = w * 0.5f;
            // 흰 옷: 어깨(위 22% 아래)부터 발목까지 조금씩 넓어진다
            for (int y = 4; y < 122; y++)
            {
                float k = y / 122f;
                float half = Mathf.Lerp(15f, 11f, k) + Mathf.Sin(y * 0.4f) * 0.6f;
                for (int x = Mathf.RoundToInt(cx - half); x < Mathf.RoundToInt(cx + half); x++)
                    Put(px, w, h, x, y, (x + y) % 7 == 0 ? dressShade : dress);
            }
            // 축 늘어뜨린 팔과 창백한 손
            Bar(px, w, h, cx - 17f, cx - 13f, 52, 118, dressShade);
            Bar(px, w, h, cx + 13f, cx + 17f, 52, 118, dressShade);
            Bar(px, w, h, cx - 17f, cx - 13f, 44, 52, skin);
            Bar(px, w, h, cx + 13f, cx + 17f, 44, 52, skin);
            // 맨발
            Bar(px, w, h, cx - 8f, cx - 3f, 0, 4, skin);
            Bar(px, w, h, cx + 3f, cx + 8f, 0, 4, skin);
            // 머리와 허리까지 내려온 검은 머리카락
            Ellipse(px, w, h, cx, 140f, 11f, 13f, hair);
            for (int y = 70; y < 140; y++)
            {
                float half = Mathf.Lerp(13f, 11f, (y - 70) / 70f);
                for (int x = Mathf.RoundToInt(cx - half); x < Mathf.RoundToInt(cx + half); x++)
                    if (!front || Mathf.Abs(x + 0.5f - cx) > 5f || y > 118 || ((x * 3 + y) % 5 == 0)) Put(px, w, h, x, y, hair);
            }
            if (front)
            {
                // 머리카락 틈으로 보이는 창백한 얼굴과 검은 눈
                for (int y = 118; y < 140; y++)
                    for (int x = Mathf.RoundToInt(cx - 5f); x < Mathf.RoundToInt(cx + 5f); x++)
                        if ((x * 3 + y) % 5 != 0) Put(px, w, h, x, y, skin);
                Put(px, w, h, Mathf.RoundToInt(cx - 3f), 132, hair); Put(px, w, h, Mathf.RoundToInt(cx + 2f), 132, hair);
                Put(px, w, h, Mathf.RoundToInt(cx - 3f), 131, hair); Put(px, w, h, Mathf.RoundToInt(cx + 2f), 131, hair);
            }
            return Done(t, px);
        }

        // ── 손님 대신 찍히는 괴물 ──
        public static Texture2D Monster(bool front)
        {
            const int w = 80, h = 190;
            var t = New(w, h, out var px);
            var body = new Color32(10, 10, 12, 255);
            var rim = new Color32(46, 48, 52, 255);
            var white = new Color32(250, 250, 244, 255);
            float cx = w * 0.5f;
            // 다리(가늘고 길게)
            Bar(px, w, h, cx - 10f, cx - 4f, 0, 78, body);
            Bar(px, w, h, cx + 4f, cx + 10f, 0, 78, body);
            // 몸통: 좁은 허리에서 넓은 어깨로
            for (int y = 72; y < 150; y++)
            {
                float half = Mathf.Lerp(11f, 21f, (y - 72) / 78f);
                for (int x = Mathf.RoundToInt(cx - half); x < Mathf.RoundToInt(cx + half); x++) Put(px, w, h, x, y, body);
            }
            // 무릎까지 내려오는 긴 팔과 갈고리 손가락
            for (int side = -1; side <= 1; side += 2)
            {
                float ax = cx + side * 24f;
                for (int y = 46; y < 148; y++)
                {
                    float x = ax + side * Mathf.Sin((148 - y) / 102f * 0.6f) * 3f;
                    Bar(px, w, h, x - 2.5f, x + 2.5f, y, y + 1, body);
                }
                for (int f = 0; f < 3; f++)
                    Bar(px, w, h, ax + side * (f - 1) * 2.2f - 0.6f, ax + side * (f - 1) * 2.2f + 0.6f, 36, 46, body);
            }
            // 길쭉한 머리
            Ellipse(px, w, h, cx, 168f, 11f, 18f, body);
            // 가장자리를 살짝 밝게(어둠 속 실루엣이 보이게)
            var copy = (Color32[])px.Clone();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                    if (copy[y * w + x].a > 0 && (copy[y * w + x - 1].a == 0 || copy[y * w + x + 1].a == 0 || copy[(y + 1) * w + x].a == 0))
                        px[y * w + x] = rim;
            if (front)
            {
                // 하얀 눈(위로 찢어짐)과 귀까지 찢어진 이빨
                Ellipse(px, w, h, cx - 5f, 172f, 2.6f, 3.6f, white);
                Ellipse(px, w, h, cx + 5f, 172f, 2.6f, 3.6f, white);
                for (int x = Mathf.RoundToInt(cx - 9f); x <= Mathf.RoundToInt(cx + 9f); x++)
                {
                    float u = (x - cx) / 9f;
                    int y0 = 158 + Mathf.RoundToInt(u * u * 4f);
                    Put(px, w, h, x, y0, x % 2 == 0 ? white : body);
                    Put(px, w, h, x, y0 + 1, white);
                    Put(px, w, h, x, y0 - 1, x % 2 == 1 ? white : body);
                }
            }
            return Done(t, px);
        }

        // ── 밖에서 가게 안을 보는 귀신 ──
        public static Texture2D Watcher(bool eyes, int variant)
        {
            const int w = 50, h = 150;
            var t = New(w, h, out var px);
            var body = new Color32(8, 8, 10, 255);
            var white = new Color32(250, 250, 246, 255);
            float cx = w * 0.5f;
            float tilt = (variant % 3 - 1) * 2f;   // 고개를 살짝 기울인 정도
            Bar(px, w, h, cx - 8f, cx - 2f, 0, 60, body);
            Bar(px, w, h, cx + 2f, cx + 8f, 0, 60, body);
            for (int y = 56; y < 120; y++)
            {
                float half = Mathf.Lerp(10f, 15f, (y - 56) / 64f);
                for (int x = Mathf.RoundToInt(cx - half); x < Mathf.RoundToInt(cx + half); x++) Put(px, w, h, x, y, body);
            }
            Bar(px, w, h, cx - 18f, cx - 14f, 60, 118, body);
            Bar(px, w, h, cx + 14f, cx + 18f, 60, 118, body);
            Ellipse(px, w, h, cx + tilt, 132f, 9f, 12f, body);
            if (eyes)
            {
                Ellipse(px, w, h, cx + tilt - 3.5f, 134f, 1.6f, 1.6f, white);
                Ellipse(px, w, h, cx + tilt + 3.5f, 134f, 1.6f, 1.6f, white);
            }
            return Done(t, px);
        }
    }
}
