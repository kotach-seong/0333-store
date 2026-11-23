using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 영수증 크게 보기. 금전등록기에서 나온 영수증을 클릭하면 화면 가운데에 펼쳐진다.
    ///  - 도트로 그린 감열지(위아래 톱니 모양, 점선, 바코드), 글자는 갈무리11(제목)·갈무리9(본문).
    ///  - 합계와 받은 돈만 적는다(거스름돈 없음).
    ///  - 손님이 정상인지는 영수증이 아니라 계산대 위 점검표(ChecklistView)에서 고른다.
    /// </summary>
    public class ReceiptView : PaperPanel
    {
        [Header("영수증 모양")]
        public int paperWidth = 200;
        public Color32 paperColor = new Color32(238, 235, 225, 255);
        [Tooltip("글자 위에 덮어 그리는 그림(피 얼룩). 글자보다 위 순서(206)")]
        public SpriteRenderer overlayRenderer;
        public Color32 bloodDark = new Color32(64, 6, 10, 250);
        public Color32 bloodMid = new Color32(104, 14, 18, 245);
        public Color32 bloodFresh = new Color32(146, 24, 26, 240);

        public ReceiptData Current { get; private set; }
        /// <summary>이번 영수증을 한 번이라도 열어 봤는지.</summary>
        public bool Viewed { get; private set; }

        const int Pad = 12;   // 종이 안쪽 좌우 여백
        readonly List<int> dashes = new List<int>();   // 점선의 높이(화면 중심 기준 y)
        int receiptLeft, receiptTop, receiptH, barcodeY;

        protected override bool CanOpen => Current != null;

        /// <summary>0보다 크면 상품 줄을 맨 끝 이만큼만 보여 준다(3:33 끊이지 않는 영수증).</summary>
        [System.NonSerialized] public int tailRows;

        /// <summary>열려 있으면 내용이 바뀐 대로 다시 그린다.</summary>
        public void Rerender()
        {
            if (IsOpen) Render();
        }

        /// <summary>새 영수증이 나왔을 때.</summary>
        public void Prepare(ReceiptData data)
        {
            Current = data;
            Viewed = false;
        }

        protected override void OnOpen() => Viewed = true;

        public void ShowPreview(ReceiptData data)
        {
            Current = data;
            Viewed = true;
            ShowForPreview();
        }

        static string Won(int v) => v.ToString("N0", CultureInfo.InvariantCulture);

        protected override void Layout()
        {
            ReceiptData d = Current;
            dashes.Clear();
            receiptLeft = -paperWidth / 2;
            int mL = receiptLeft + Pad, mR = receiptLeft + paperWidth - Pad, inner = mR - mL;
            int colPrice = mL + 92, colQty = mL + 122; // 단가·수량 칸의 오른쪽 끝

            // kind 0: 좌우, 1: 가운데, 2: 상품 칸(|로 나눔), 3: 점선, 4: 바코드, 5: 빈칸
            var rows = new List<(string a, string b, int h, bool title, bool soft, int kind)>();
            void Row(string a, string b, int h, bool title = false, bool soft = false, int kind = 0) => rows.Add((a, b, h, title, soft, kind));

            Row(d.storeName, null, 16, title: true, kind: 1);
            Row(d.branch, null, 12, soft: true, kind: 1);
            Row(null, null, 5, kind: 5);
            Row($"DAY {d.day}", d.time, 12);
            Row("POS 01", d.NumberLabel, 12);
            Row(d.visitorText, null, 12);
            Row(null, null, 2, kind: 5);
            Row(null, null, 9, kind: 3);
            Row("상품명|단가|수량|금액", null, 12, soft: true, kind: 2);
            Row(null, null, 9, kind: 3);
            // 3:33(끊이지 않는 영수증): 맨 끝 몇 줄만 보인다(새 줄이 생길 때마다 위로 밀려 올라간다)
            int firstLine = tailRows > 0 ? Mathf.Max(0, d.lines.Count - tailRows) : 0;
            for (int i = firstLine; i < d.lines.Count; i++)
            {
                ReceiptLine l = d.lines[i];
                Row($"{l.name}|{Won(l.unitPrice)}|{l.qty}|{Won(l.Amount)}", null, 12, kind: 2);
            }
            Row(null, null, 9, kind: 3);
            Row("합계", Won(d.total), 15, title: true);
            Row("받은 돈", string.IsNullOrEmpty(d.paidText) ? Won(d.paid) : d.paidText, 12);
            Row(null, null, 2, kind: 5);
            Row(null, null, 9, kind: 3);
            Row("저희 편의점을 이용해 주셔서", null, 13, kind: 1);
            Row("감사합니다", null, 12, kind: 1);
            Row(null, null, 7, kind: 5);
            Row(null, null, 18, kind: 4);
            Row(d.BarcodeLabel, null, 12, soft: true, kind: 1);

            receiptH = 14 + 10;
            foreach (var r in rows) receiptH += r.h;
            receiptTop = Mathf.Min(ScreenH / 2 - 6, receiptH / 2 + 4);

            int y = receiptTop - 14; // 지금 줄의 윗변
            foreach (var r in rows)
            {
                int bottom = y - r.h + 1; // 글자가 앉는 칸의 아래 끝
                Color32 col = r.soft ? softInk : inkColor;
                switch (r.kind)
                {
                    case 0:
                        Add(r.a, mL, bottom, inner, r.h - 1, TextAnchor.LowerLeft, col, r.title);
                        if (!string.IsNullOrEmpty(r.b)) Add(r.b, mL, bottom, inner, r.h - 1, TextAnchor.LowerRight, col, r.title);
                        break;
                    case 1: Add(r.a, mL, bottom, inner, r.h - 1, TextAnchor.LowerCenter, col, r.title); break;
                    case 2:
                        string[] c = r.a.Split('|');
                        Add(c[0], mL, bottom, 60, r.h - 1, TextAnchor.LowerLeft, col);
                        Add(c[1], colPrice - 40, bottom, 40, r.h - 1, TextAnchor.LowerRight, col);
                        Add(c[2], colQty - 24, bottom, 24, r.h - 1, TextAnchor.LowerRight, col);
                        Add(c[3], mR - 50, bottom, 50, r.h - 1, TextAnchor.LowerRight, col);
                        break;
                    case 3: dashes.Add(y - 4); break;
                    case 4: barcodeY = y; break;
                }
                y -= r.h;
            }

            closeRect = new RectInt(receiptLeft - 26, receiptTop - 18, 18, 18);
        }

        protected override void DrawContent(int ox, int oy)
        {
            int left = receiptLeft, right = receiptLeft + paperWidth;
            int top = receiptTop, bottom = receiptTop - receiptH;

            // 그림자, 종이(위아래 톱니)
            PaperShape(left + 3 + ox, right + 3 + ox, bottom - 3 + oy, top - 3 + oy, (x, y) => new Color32(0, 0, 0, 120));
            PaperShape(left + ox, right + ox, bottom + oy, top + oy, (x, y) =>
            {
                int n = Mathf.RoundToInt((PixelTex.Noise(x, y, 31) - 0.5f) * 6f);
                Color32 c = paperColor;
                if (x == left + ox) c = PixelTex.Mul(c, 0.94f);
                else if (x == right + ox - 1) c = PixelTex.Mul(c, 0.9f);
                return new Color32((byte)(c.r + n), (byte)(c.g + n), (byte)(c.b + n), 255);
            });

            // 점선
            Color32 dash = new Color32(150, 147, 154, 255);
            foreach (int d in dashes)
                for (int x = left + Pad; x < right - Pad; x++)
                    if ((x - left) % 5 < 3) pt.Put(x + ox, d + oy, dash);

            // 바코드
            var rnd = new System.Random(Current != null ? Current.number * 7919 + Current.day : 1);
            int mid = left + paperWidth / 2;
            for (int bx = mid - 64; bx < mid + 64;)
            {
                int w = rnd.Next(1, 3), gap = rnd.Next(1, 3);
                pt.Fill(bx + ox, barcodeY - 18 + oy, bx + w + ox, barcodeY + oy, inkColor);
                bx += w + gap;
            }
        }

        // ── 피 묻은 영수증(이상현상) ─────────────────────────────
        PixelTex stains;

        /// <summary>피 얼룩은 글자 위에 덮이도록 따로 그린다(글자가 피에 가려 반쯤 보인다).</summary>
        protected override void AfterRender()
        {
            if (overlayRenderer == null) return;
            if (Current == null || !Current.bloody) { overlayRenderer.enabled = false; return; }
            if (stains == null) stains = new PixelTex(ScreenW, ScreenH);
            stains.Clear();
            // 혈흔 없애기: 같은 모양의 검은 얼룩(판정에 쓰는 단서라 없애지는 않는다)
            Color32 d = bloodDark, m = bloodMid, f = bloodFresh;
            if (GameSettings.NoBlood)
            {
                bloodDark = new Color32(22, 20, 20, 250);
                bloodMid = new Color32(40, 37, 35, 245);
                bloodFresh = new Color32(58, 54, 50, 240);
            }
            DrawBlood(Current.bloodSeed, ScreenW / 2, ScreenH / 2);
            bloodDark = d; bloodMid = m; bloodFresh = f;
            overlayRenderer.sprite = stains.ToSprite(new Vector2(0.5f, 0.5f));
            overlayRenderer.enabled = true;
        }

        /// <summary>큰 핏자국과 흘러내림, 튄 방울, 손가락 자국, 문지른 자국. 종이 밖으로는 나가지 않는다.</summary>
        void DrawBlood(int seed, int ox, int oy)
        {
            var rnd = new System.Random(seed);
            int left = receiptLeft + 1, right = receiptLeft + paperWidth - 1;
            int top = receiptTop - 4, bottom = receiptTop - receiptH + 4;
            void Dot(int x, int y, Color32 c)
            {
                if (x >= left && x < right && y >= bottom && y < top) stains.Set(x + ox, y + oy, c);
            }
            void Disc(float cx, float cy, float r, Color32 c)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                    for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                        if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) Dot(x, y, c);
            }

            // 큰 핏자국: 가장자리가 울퉁불퉁하고, 테두리가 더 진하게 마른 모양
            int splats = rnd.Next(1, 3);
            for (int i = 0; i < splats; i++)
            {
                int cx = rnd.Next(left + 25, right - 25), cy = rnd.Next(bottom + 45, top - 30);
                float r = rnd.Next(10, 18);
                int salt = rnd.Next(1000);
                int span = Mathf.CeilToInt(r * 1.4f);
                for (int y = -span; y <= span; y++)
                    for (int x = -span; x <= span; x++)
                    {
                        float ang = (Mathf.Atan2(y, x) + Mathf.PI) / (2f * Mathf.PI) * 12f;
                        int b = Mathf.FloorToInt(ang);
                        float edgeK = Mathf.Lerp(PixelTex.Noise(b % 12, salt), PixelTex.Noise((b + 1) % 12, salt), ang - b);
                        float edge = r * (0.75f + 0.5f * edgeK);
                        float d = Mathf.Sqrt(x * x + y * y);
                        if (d >= edge) continue;
                        Color32 c = d > edge - 1.6f ? bloodDark
                            : d < edge * 0.5f && PixelTex.Noise(x + cx, y + cy, salt) > 0.55f ? bloodFresh : bloodMid;
                        Dot(cx + x, cy + y, c);
                    }
                // 주변에 튄 방울
                int sat = rnd.Next(3, 8);
                for (int k = 0; k < sat; k++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = r * (1.2f + (float)rnd.NextDouble() * 1.2f);
                    Disc(cx + Mathf.Cos(a) * dist, cy + Mathf.Sin(a) * dist, 0.6f + (float)rnd.NextDouble() * 1.4f, bloodMid);
                }
                // 아래로 흘러내린 줄기
                int drips = rnd.Next(1, 4);
                for (int k = 0; k < drips; k++)
                {
                    int dx = cx + rnd.Next(-(int)r + 2, (int)r - 1), len = rnd.Next(12, 40), w = rnd.Next(1, 4);
                    int startY = cy - Mathf.RoundToInt(r * 0.6f);
                    for (int y = 0; y < len; y++)
                        for (int x = 0; x < w + (y < 3 ? 1 : 0); x++) Dot(dx + x, startY - y, bloodMid);
                    Disc(dx + w * 0.5f, startY - len, w * 0.5f + 1.1f, bloodDark);
                }
            }

            // 종이 여기저기 튄 작은 방울
            int drops = rnd.Next(5, 14);
            for (int k = 0; k < drops; k++)
                Disc(rnd.Next(left, right), rnd.Next(bottom, top), 0.5f + (float)rnd.NextDouble() * 1.3f, rnd.Next(2) == 0 ? bloodMid : bloodDark);

            // 피 묻은 손가락 자국(둥근 지문 무늬)
            if (rnd.NextDouble() < 0.6)
            {
                int fx = rnd.Next(left + 12, right - 12), fy = rnd.Next(bottom + 15, top - 15);
                float rx = 5f, ry = 7f;
                for (int y = -8; y <= 8; y++)
                    for (int x = -6; x <= 6; x++)
                    {
                        float e = Mathf.Sqrt((x / rx) * (x / rx) + (y / ry) * (y / ry));
                        if (e > 1f || PixelTex.Noise(x + fx, y + fy, 7) < e * 0.5f) continue;
                        if (Mathf.FloorToInt(e * 6f + x * 0.12f) % 2 != 0) continue; // 지문 골
                        Dot(fx + x, fy + y, PixelTex.Alpha(bloodMid, 0.7f));
                    }
            }

            // 손으로 문지른 자국
            if (rnd.NextDouble() < 0.4)
            {
                int sx = rnd.Next(left + 5, left + 40), sy = rnd.Next(bottom + 30, top - 30);
                int len = rnd.Next(35, 70), thick = rnd.Next(4, 7);
                float slope = ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                for (int t = 0; t < len; t++)
                    for (int o = 0; o < thick; o++)
                    {
                        if (PixelTex.Noise(t / 3, o, 13) < 0.35f) continue; // 결이 생기게 군데군데 비운다
                        float fade = 1f - t / (float)len;
                        Dot(sx + t, sy + Mathf.RoundToInt(t * slope) + o, PixelTex.Alpha(bloodMid, 0.25f + 0.65f * fade));
                    }
            }
        }

        /// <summary>톱니 모양 위아래 가장자리를 가진 종이 모양으로 칠한다.</summary>
        void PaperShape(int x0, int x1, int y0, int y1, System.Func<int, int, Color32> color)
        {
            for (int x = x0; x < x1; x++)
            {
                int tooth = Mathf.Abs((x - x0) % 6 - 3);   // 0~3
                for (int y = y0 + tooth; y < y1 - tooth; y++) pt.Put(x, y, color(x, y));
            }
        }
    }
}
