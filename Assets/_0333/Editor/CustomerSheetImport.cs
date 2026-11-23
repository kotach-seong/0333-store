using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 사용자가 다시 그려 온 손님 시트(6칸 x 4줄)를 잘라 게임용 손님 그림(128px, south·north)으로 바꾼다.
    ///  - 칸 순서는 CustomerArt 미리보기 시트와 같다(후드, 중절모, 운동광 ... 고등학생).
    ///  - 칸마다 배경(단색)을 지우고, 손님 키에 맞게(1m = 68.6px) 줄인 뒤 색 수를 줄이고 바깥 외곽선을 어둡게 다듬는다.
    ///  - 원래 그림은 *_drawn.png(코드 그림) / *_pixellab.png(PixelLab 그림)로 남겨 언제든 되돌릴 수 있다.
    ///  - 22번 코스프레의 전신 앞모습은 미쿠풍이지만, 사용자가 판매하지 않는 개인 작품이라 그대로 쓰기로 했다.
    ///    판매·공개 배포용으로는 윗몸 시트의 분홍 창작 의상으로 만든 south_pink.png를 대신 쓴다.
    /// 배치: -front 전신앞.png -back 뒷모습.png -upper 윗몸.png [-previewDir 폴더]
    /// </summary>
    public static class CustomerSheetImport
    {
        static readonly string[] Order =
        {
            "hoodie", "faceless_fedora", "gym", "yakuza", "office", "bald_office",
            "drunk", "occult", "cult", "child", "foggy", "couple",
            "leopard", "danso", "jinsang_m", "jinsang_w", "gentle_elder", "otaku",
            "coins", "soldier", "police", "mayor", "cosplay", "student",
        };

        const int Cols = 6, Rows = 4, S = CustomerArt.S, FeetY = 3;

        static string DraftRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "customers"));

        public static void ImportBatch()
        {
            string front = StoreSceneBuilder.Arg("-front"), back = StoreSceneBuilder.Arg("-back"), upper = StoreSceneBuilder.Arg("-upper");
            var F = new Sheet(front, Cols, Rows);
            var B = new Sheet(back, Cols, Rows);
            var U = new Sheet(upper, Cols, Rows);
            var heights = new Dictionary<string, float>();
            foreach (var e in CustomerArt.Catalog()) heights[e.id] = e.heightM;

            for (int i = 0; i < Order.Length; i++)
            {
                string id = Order[i];
                int col = i % Cols, row = i / Cols;
                float h = heights.TryGetValue(id, out float hh) ? hh : 1.72f;
                bool pair = id == "couple" || id == "mayor";
                string dir = Path.Combine(DraftRoot, id);
                Directory.CreateDirectory(dir);
                Backup(dir, id);
                // 코스프레: 사용자가 비상업 개인 작품이라 전신 그림(미쿠풍)을 그대로 쓰기로 함.
                // 판매·공개 배포할 때 바꿔 끼울 수 있게 분홍 창작 버전을 south_pink.png로 남긴다.
                if (id == "cosplay")
                {
                    string pink = Path.Combine(dir, "south_pink.png");
                    if (!File.Exists(pink)) Save(Finish(CosplayFront(U, col, row, dir)), pink);
                }

                Color32[] south = F.Extract(col, row, h, pair);
                Color32[] north = B.Extract(col, row, h, pair);
                Save(Finish(south), Path.Combine(dir, "south.png"));
                Save(Finish(north), Path.Combine(dir, "north.png"));
            }
            Debug.Log($"[0333] 다시 그려 온 손님 {Order.Length}명을 가져옴: {DraftRoot}");
            CheckoutBuilder.RebuildBatch();
        }

        /// <summary>
        /// 배치 모드용: 두 사람이 나란히 선 앞모습(-front)·뒷모습(-back) 그림 한 장씩에서
        /// 왼쪽 사람은 -left id로, 오른쪽 사람은 -right id로 가져온다(키는 -leftH, -rightH 미터).
        /// 예: 주인공(player)과 06:00 교대 알바생(worker).
        /// </summary>
        public static void ImportPairBatch()
        {
            string front = StoreSceneBuilder.Arg("-front"), back = StoreSceneBuilder.Arg("-back");
            string[] ids = { StoreSceneBuilder.Arg("-left"), StoreSceneBuilder.Arg("-right") };
            float[] heights =
            {
                float.TryParse(StoreSceneBuilder.Arg("-leftH"), out float lh) ? lh : 1.74f,
                float.TryParse(StoreSceneBuilder.Arg("-rightH"), out float rh) ? rh : 1.74f,
            };
            var F = new Sheet(front, 2, 1);
            var B = new Sheet(back, 2, 1);
            for (int col = 0; col < 2; col++)
            {
                if (string.IsNullOrEmpty(ids[col])) continue;
                string dir = Path.Combine(DraftRoot, ids[col]);
                Directory.CreateDirectory(dir);
                Save(Finish(F.Extract(col, 0, heights[col], false)), Path.Combine(dir, "south.png"));
                Save(Finish(B.Extract(col, 0, heights[col], false)), Path.Combine(dir, "north.png"));
            }
            Debug.Log($"[0333] 두 사람 그림을 가져옴: {ids[0]}, {ids[1]} → {DraftRoot}");
            CheckoutBuilder.RebuildBatch();
        }

        /// <summary>바꾸기 전 그림을 남겨 둔다(이미 남겨 둔 게 있으면 그대로).</summary>
        static void Backup(string dir, string id)
        {
            bool pixellab = id == "hoodie" || id == "faceless_fedora";
            string tag = pixellab ? "_pixellab" : "_drawn";
            foreach (string side in new[] { "south", "north" })
            {
                string cur = Path.Combine(dir, side + ".png"), keep = Path.Combine(dir, side + tag + ".png");
                if (File.Exists(cur) && !File.Exists(keep)) File.Copy(cur, keep);
            }
        }

        // ── 시트 한 장 ──
        class Sheet
        {
            readonly Color32[] px;
            readonly int w, h, cols, rows;

            public Sheet(string path, int cols, int rows)
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(path));
                px = t.GetPixels32();
                w = t.width; h = t.height;
                this.cols = cols; this.rows = rows;
                Object.DestroyImmediate(t);
            }

            /// <summary>칸 하나(위에서 row번째 줄)의 범위. 텍스처 좌표(아래가 0).</summary>
            public RectInt Cell(int col, int row)
            {
                int x0 = Mathf.RoundToInt(col * w / (float)cols), x1 = Mathf.RoundToInt((col + 1) * w / (float)cols);
                int yTop = Mathf.RoundToInt(row * h / (float)rows), yBot = Mathf.RoundToInt((row + 1) * h / (float)rows);
                return new RectInt(x0, h - yBot, x1 - x0, yBot - yTop);
            }

            public Color32 At(int x, int y) => px[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)];

            /// <summary>칸의 전경 표시(배경 지우기). 테두리에서 이어진 배경색은 넉넉히, 안쪽 구멍은 좁게 지운다.</summary>
            public bool[] Mask(RectInt r, out Color32 bg)
            {
                // 배경색: 네 귀퉁이 근처의 중앙값
                var samples = new List<Color32>();
                for (int k = 0; k < 4; k++)
                    for (int dy = 0; dy < 6; dy++)
                        for (int dx = 0; dx < 6; dx++)
                            samples.Add(At(k % 2 == 0 ? r.x + dx : r.xMax - 1 - dx, k < 2 ? r.y + dy : r.yMax - 1 - dy));
                samples.Sort((a, b) => (a.r + a.g + a.b).CompareTo(b.r + b.g + b.b));
                bg = samples[samples.Count / 2];
                Color32 bgc = bg;
                float Dist(Color32 c) => Mathf.Sqrt((c.r - bgc.r) * (c.r - bgc.r) + (c.g - bgc.g) * (c.g - bgc.g) + (c.b - bgc.b) * (c.b - bgc.b));

                int W = r.width, H = r.height;
                var fg = new bool[W * H];
                for (int i = 0; i < fg.Length; i++) fg[i] = true;
                // 테두리에서 번지는 배경
                var q = new Queue<int>();
                void Seed(int x, int y) { int i = y * W + x; if (fg[i] && Dist(At(r.x + x, r.y + y)) < 16f) { fg[i] = false; q.Enqueue(i); } }
                for (int x = 0; x < W; x++) { Seed(x, 0); Seed(x, H - 1); }
                for (int y = 0; y < H; y++) { Seed(0, y); Seed(W - 1, y); }
                while (q.Count > 0)
                {
                    int i = q.Dequeue(), x = i % W, y = i / W;
                    if (x > 0) Seed(x - 1, y);
                    if (x < W - 1) Seed(x + 1, y);
                    if (y > 0) Seed(x, y - 1);
                    if (y < H - 1) Seed(x, y + 1);
                }
                var outside = new bool[W * H];               // 테두리와 이어진 진짜 배경
                for (int i = 0; i < fg.Length; i++) outside[i] = !fg[i];

                // 갇힌 배경 구멍(팔과 몸 사이 등): 배경색에 아주 가까운 픽셀이 넓게 뭉친 곳만 지운다.
                // (검은 정장의 밝은 결처럼 옷 안에 흩어진 배경 비슷한 픽셀은 남긴다)
                var seen = new bool[W * H];
                var comp = new List<int>();
                for (int start = 0; start < fg.Length; start++)
                {
                    if (seen[start] || !fg[start] || Dist(At(r.x + start % W, r.y + start / W)) >= 9f) continue;
                    comp.Clear();
                    var cq = new Queue<int>();
                    cq.Enqueue(start); seen[start] = true;
                    while (cq.Count > 0)
                    {
                        int i = cq.Dequeue(); comp.Add(i);
                        int x = i % W, y = i / W;
                        foreach (int n in new[] { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 })
                            if (n >= 0 && !seen[n] && fg[n] && Dist(At(r.x + n % W, r.y + n / W)) < 9f) { seen[n] = true; cq.Enqueue(n); }
                    }
                    if (comp.Count >= 80) foreach (int i in comp) fg[i] = false;
                }

                // 가장자리 배경 번짐 정리(두 번): 바깥 배경과 맞닿은 곳만
                for (int pass = 0; pass < 2; pass++)
                {
                    var kill = new List<int>();
                    for (int y = 1; y < H - 1; y++)
                        for (int x = 1; x < W - 1; x++)
                        {
                            int i = y * W + x;
                            if (!fg[i]) continue;
                            bool edge = outside[i - 1] || outside[i + 1] || outside[i - W] || outside[i + W];
                            if (edge && Dist(At(r.x + x, r.y + y)) < 24f) kill.Add(i);
                        }
                    foreach (int i in kill) { fg[i] = false; outside[i] = true; }
                }
                return fg;
            }

            /// <summary>
            /// 칸 하나를 128px 그림으로: 키(heightM)에 맞게 줄이고, 발을 아래(3px)에, 머리 중심(둘이면 전체 가운데)을 가로 가운데에 둔다.
            /// </summary>
            public Color32[] Extract(int col, int row, float heightM, bool pair)
            {
                RectInt r = Cell(col, row);
                bool[] fg = Mask(r, out _);
                int W = r.width, H = r.height;
                // 덩어리 범위(작은 티끌은 무시: 한 줄에 3px 이상인 줄만)
                int minY = H, maxY = -1, minX = W, maxX = -1;
                for (int y = 0; y < H; y++)
                {
                    int n = 0;
                    for (int x = 0; x < W; x++) if (fg[y * W + x]) n++;
                    if (n < 3) continue;
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                for (int y = minY; y <= maxY; y++)
                    for (int x = 0; x < W; x++)
                        if (fg[y * W + x]) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
                if (maxY < 0) return new Color32[S * S];

                float scale = heightM * CustomerArt.PxPerM / (maxY - minY + 1);   // 원본 1px → 결과 px
                // 가로 기준: 한 명이면 머리(위 18%) 가운데, 둘이면 전체 가운데
                float cxSrc;
                if (pair) cxSrc = (minX + maxX + 1) * 0.5f;
                else
                {
                    float sum = 0f; int cnt = 0;
                    int headLow = maxY - Mathf.RoundToInt((maxY - minY) * 0.18f);
                    for (int y = headLow; y <= maxY; y++)
                        for (int x = minX; x <= maxX; x++)
                            if (fg[y * W + x]) { sum += x + 0.5f; cnt++; }
                    cxSrc = cnt > 0 ? sum / cnt : (minX + maxX + 1) * 0.5f;
                }

                var outPx = new Color32[S * S];
                const int sub = 3;
                for (int ty = 0; ty < S; ty++)
                    for (int tx = 0; tx < S; tx++)
                    {
                        int n = 0; float cr = 0, cg = 0, cb = 0;
                        for (int j = 0; j < sub; j++)
                            for (int k = 0; k < sub; k++)
                            {
                                float sx = cxSrc + (tx + (k + 0.5f) / sub - S * 0.5f) / scale;
                                float sy = minY + (ty + (j + 0.5f) / sub - FeetY) / scale;
                                int ix = Mathf.FloorToInt(sx), iy = Mathf.FloorToInt(sy);
                                if (ix < 0 || iy < 0 || ix >= W || iy >= H || !fg[iy * W + ix]) continue;
                                Color32 c = At(r.x + ix, r.y + iy);
                                cr += c.r; cg += c.g; cb += c.b; n++;
                            }
                        if (n * 2 < sub * sub) continue;
                        outPx[ty * S + tx] = new Color32((byte)(cr / n), (byte)(cg / n), (byte)(cb / n), 255);
                    }
                return outPx;
            }

            /// <summary>칸을 정해진 배율로 그대로 옮긴다(윗몸 시트용). 칸 아래끝 = 결과의 baseY, 칸 너비 = 결과 128px.</summary>
            public Color32[] Place(int col, int row, int baseY, int outH)
            {
                RectInt r = Cell(col, row);
                bool[] fg = Mask(r, out _);
                float scale = S / (float)r.width;   // 원본 1px → 결과 px
                var outPx = new Color32[S * S];
                const int sub = 3;
                for (int ty = baseY; ty < Mathf.Min(S, baseY + outH); ty++)
                    for (int tx = 0; tx < S; tx++)
                    {
                        int n = 0; float cr = 0, cg = 0, cb = 0;
                        for (int j = 0; j < sub; j++)
                            for (int k = 0; k < sub; k++)
                            {
                                int ix = Mathf.FloorToInt((tx + (k + 0.5f) / sub) / scale);
                                int iy = Mathf.FloorToInt((ty - baseY + (j + 0.5f) / sub) / scale);
                                if (ix < 0 || iy < 0 || ix >= r.width || iy >= r.height || !fg[iy * r.width + ix]) continue;
                                Color32 c = At(r.x + ix, r.y + iy);
                                cr += c.r; cg += c.g; cb += c.b; n++;
                            }
                        if (n * 2 < sub * sub) continue;
                        outPx[ty * S + tx] = new Color32((byte)(cr / n), (byte)(cg / n), (byte)(cb / n), 255);
                    }
                return outPx;
            }
        }

        /// <summary>코스프레 앞모습: 윗몸 시트(분홍 창작 의상)를 캔버스 윗절반에, 아랫절반은 코드로 그린 그림에서.</summary>
        static Color32[] CosplayFront(Sheet upper, int col, int row, string dir)
        {
            Color32[] top = upper.Place(col, row, S / 2, S / 2);
            string drawn = Path.Combine(dir, "south_drawn.png");
            var result = new Color32[S * S];
            if (File.Exists(drawn))
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(drawn));
                if (t.width == S && t.height == S)
                {
                    Color32[] d = t.GetPixels32();
                    for (int y = 0; y < S / 2; y++) for (int x = 0; x < S; x++) result[y * S + x] = d[y * S + x];
                }
                Object.DestroyImmediate(t);
            }
            for (int y = S / 2; y < S; y++) for (int x = 0; x < S; x++) result[y * S + x] = top[y * S + x];
            return result;
        }

        /// <summary>마무리: 색 수 줄이기(흐린 가장자리를 또렷하게) + 바깥 외곽선을 어둡게.</summary>
        static Color32[] Finish(Color32[] img)
        {
            Quantize(img, 40);
            var outImg = (Color32[])img.Clone();
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Color32 c = img[y * S + x];
                    if (c.a == 0) continue;
                    bool edge = x == 0 || y == 0 || x == S - 1 || y == S - 1
                        || img[y * S + x - 1].a == 0 || img[y * S + x + 1].a == 0 || img[(y - 1) * S + x].a == 0 || img[(y + 1) * S + x].a == 0;
                    if (!edge) continue;
                    float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
                    if (lum > 45f) outImg[y * S + x] = new Color32((byte)(c.r * 0.35f + 10), (byte)(c.g * 0.33f + 9), (byte)(c.b * 0.35f + 12), 255);
                }
            return outImg;
        }

        /// <summary>k-평균으로 색을 k개로 줄인다.</summary>
        static void Quantize(Color32[] img, int k)
        {
            var pts = new List<Vector3>();
            foreach (var c in img) if (c.a > 0) pts.Add(new Vector3(c.r, c.g, c.b));
            if (pts.Count <= k) return;
            var sorted = new List<Vector3>(pts);
            sorted.Sort((a, b) => (a.x + a.y + a.z).CompareTo(b.x + b.y + b.z));
            var cent = new Vector3[k];
            for (int i = 0; i < k; i++) cent[i] = sorted[(int)((i + 0.5f) / k * sorted.Count)];
            var assign = new int[pts.Count];
            for (int it = 0; it < 10; it++)
            {
                var sum = new Vector3[k];
                var cnt = new int[k];
                for (int p = 0; p < pts.Count; p++)
                {
                    int best = 0; float bd = float.MaxValue;
                    for (int c = 0; c < k; c++) { float d = (pts[p] - cent[c]).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
                    assign[p] = best; sum[best] += pts[p]; cnt[best]++;
                }
                for (int c = 0; c < k; c++) if (cnt[c] > 0) cent[c] = sum[c] / cnt[c];
            }
            int pi = 0;
            for (int i = 0; i < img.Length; i++)
            {
                if (img[i].a == 0) continue;
                Vector3 c = cent[assign[pi++]];
                img[i] = new Color32((byte)c.x, (byte)c.y, (byte)c.z, 255);
            }
        }

        static void Save(Color32[] img, string path)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels32(img);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }
    }
}
