using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 돈바구니. 손님이 합계에 딱 맞게 지폐와 동전을 한 장씩 내려놓는다.
    ///  - 만원(초록), 오천원(주황), 천원(파랑), 500원·100원(은색).
    ///  - 지폐는 바구니 가운데에 겹쳐 쌓고, 동전은 그 옆에 흩어 놓는다.
    /// 그림은 게임 중에 코드로 그린다(바닥에 납작하게 누운 모습).
    /// </summary>
    public class MoneyTray : MonoBehaviour
    {
        public Material material;
        [Tooltip("계산대(50)와 물건(52~)보다 위")]
        public int baseSortingOrder = 60;
        public StoreProjection projection = StoreProjection.Default;

        [Header("바구니 안쪽(미터)")]
        public Vector2 xRange = new Vector2(0.37f, 0.59f);
        public Vector2 zRange = new Vector2(1.56f, 1.74f);
        public float surfaceHeight = 0.93f;

        [Header("내려놓기")]
        public float dropInterval = 0.16f;
        public float dropTime = 0.09f;

        public static readonly int[] Units = { 10000, 5000, 1000, 500, 100 };

        readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        static readonly Dictionary<int, Sprite[]> sprites = new Dictionary<int, Sprite[]>();

        /// <summary>금액을 큰 돈부터 나눈다(예: 7,400 → 5000, 1000, 1000, 100 x4).</summary>
        public static List<int> Breakdown(int amount)
        {
            var list = new List<int>();
            foreach (int u in Units)
                while (amount >= u) { list.Add(u); amount -= u; }
            return list;
        }

        /// <summary>손님이 돈을 한 장씩 내려놓는다(코루틴).</summary>
        public IEnumerator Pay(int amount, int seed, AudioSource sfx)
        {
            Clear();
            var rnd = new System.Random(seed);
            foreach (var spot in Plan(amount, rnd))
            {
                SpriteRenderer sr = Spawn(spot);
                Vector3 home = sr.transform.position;
                for (float t = 0f; t < dropTime; t += Time.deltaTime)
                {
                    float k = t / dropTime;
                    sr.transform.position = home + new Vector3(0f, Mathf.Round(7f * (1f - k * k)), 0f);
                    yield return null;
                }
                sr.transform.position = home;
                SoundFx.Play(sfx, spot.unit >= 1000 ? SoundFx.Bill : SoundFx.Coin, 0.9f);
                yield return new WaitForSeconds(dropInterval);
            }
        }

        /// <summary>바로 다 놓인 모습으로(에디터 미리보기용).</summary>
        public void ShowInstant(int amount, int seed)
        {
            Clear();
            foreach (var spot in Plan(amount, new System.Random(seed))) Spawn(spot);
        }

        // ── 이상현상: 돈 대신 신체 부위 ─────────────────────────
        public static readonly string[] BodyParts = { "finger", "eye", "brain", "heart", "kidney" };

        /// <summary>이상현상: 손님이 돈 대신 손가락·눈·뇌·장기를 하나씩 내려놓는다(코루틴).</summary>
        public IEnumerator PayBodyParts(int seed, AudioSource sfx)
        {
            Clear();
            foreach (var spot in PlanBodyParts(new System.Random(seed ^ 0x77)))
            {
                SpriteRenderer sr = Spawn(spot);
                Vector3 home = sr.transform.position;
                for (float t = 0f; t < dropTime; t += Time.deltaTime)
                {
                    float k = t / dropTime;
                    sr.transform.position = home + new Vector3(0f, Mathf.Round(7f * (1f - k * k)), 0f);
                    yield return null;
                }
                sr.transform.position = home;
                SoundFx.Play(sfx, SoundFx.Squish, 1f);
                yield return new WaitForSeconds(dropInterval + 0.08f);
            }
        }

        public void ShowBodyPartsInstant(int seed)
        {
            Clear();
            foreach (var spot in PlanBodyParts(new System.Random(seed ^ 0x77))) Spawn(spot);
        }

        int extraOrder;

        /// <summary>3:33(계산대 밑의 손): 손가락이나 눈알 하나를 바구니에 툭(비우지 않고 쌓인다).</summary>
        public IEnumerator DropOnePart(int seed, AudioSource sfx)
        {
            var rnd = new System.Random(seed);
            string k = rnd.Next(3) == 0 ? "eye" : "finger";
            float x = Mathf.Lerp(xRange.x, xRange.y, 0.15f + (float)rnd.NextDouble() * 0.7f);
            float z = Mathf.Lerp(zRange.x, zRange.y, 0.15f + (float)rnd.NextDouble() * 0.7f);
            Vector2 s = projection.ToScreen(new Vector3(x, surfaceHeight, z));
            SpriteRenderer sr = Spawn(new Spot { kind = k, screen = new Vector3(Mathf.Round(s.x), Mathf.Round(s.y), 0f), variant = rnd.Next(3), order = 10 + (extraOrder++ % 20) });
            if (sr == null) yield break;
            Vector3 home = sr.transform.position;
            for (float t = 0f; t < dropTime; t += Time.deltaTime)
            {
                float kk = t / dropTime;
                sr.transform.position = home + new Vector3(0f, Mathf.Round(7f * (1f - kk * kk)), 0f);
                yield return null;
            }
            sr.transform.position = home;
            SoundFx.Play(sfx, SoundFx.Squish, 1f);
        }

        /// <summary>2~3개. 뇌·심장은 크니까 하나씩만. 바구니 안에 흩어 놓는다.</summary>
        List<Spot> PlanBodyParts(System.Random rnd)
        {
            var spots = new List<Spot>();
            int count = rnd.Next(2, 4);
            var kinds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string k;
                int guard = 0;
                do k = BodyParts[rnd.Next(BodyParts.Length)];
                while ((k == "brain" || k == "heart") && kinds.Contains(k) && guard++ < 20);
                kinds.Add(k);
            }
            kinds.Sort((a, b) => Size(b).CompareTo(Size(a))); // 큰 것부터 깔고 작은 것을 위에
            int order = 0;
            foreach (string k in kinds)
            {
                float x = Mathf.Lerp(xRange.x, xRange.y, 0.15f + (float)rnd.NextDouble() * 0.7f);
                float z = Mathf.Lerp(zRange.x, zRange.y, 0.15f + (float)rnd.NextDouble() * 0.7f);
                Vector2 s = projection.ToScreen(new Vector3(x, surfaceHeight, z));
                spots.Add(new Spot { kind = k, screen = new Vector3(Mathf.Round(s.x), Mathf.Round(s.y), 0f), variant = rnd.Next(3), order = order++ });
            }
            return spots;
        }

        static int Size(string kind) => kind == "brain" ? 3 : kind == "heart" ? 2 : kind == "kidney" ? 1 : 0;

        /// <summary>알바생이 돈을 가져간다(바구니를 비운다).</summary>
        public void Clear()
        {
            foreach (var p in pieces)
                if (p != null)
                {
                    if (Application.isPlaying) Destroy(p.gameObject);
                    else DestroyImmediate(p.gameObject);
                }
            pieces.Clear();
        }

        /// <summary>바구니에 놓일 것 하나: 돈(unit) 또는 신체 부위(kind).</summary>
        struct Spot { public int unit; public string kind; public Vector3 screen; public int variant; public int order; }

        List<Spot> Plan(int amount, System.Random rnd)
        {
            var spots = new List<Spot>();
            List<int> money = Breakdown(amount);
            // 지폐 더미의 가운데(왼쪽 앞), 동전은 오른쪽에 흩어 놓는다
            float billX = Mathf.Lerp(xRange.x, xRange.y, 0.38f), billZ = Mathf.Lerp(zRange.x, zRange.y, 0.5f);
            int order = 0;
            foreach (int u in money)
            {
                float x, z;
                if (u >= 1000)
                {
                    x = billX + (float)(rnd.NextDouble() - 0.5) * 0.035f;
                    z = billZ + (float)(rnd.NextDouble() - 0.5) * 0.06f;
                }
                else
                {
                    x = Mathf.Lerp(xRange.x, xRange.y, 0.72f + (float)rnd.NextDouble() * 0.26f);
                    z = Mathf.Lerp(zRange.x, zRange.y, 0.1f + (float)rnd.NextDouble() * 0.8f);
                }
                Vector2 s = projection.ToScreen(new Vector3(x, surfaceHeight, z));
                spots.Add(new Spot { unit = u, screen = new Vector3(Mathf.Round(s.x), Mathf.Round(s.y), 0f), variant = rnd.Next(3), order = order++ });
            }
            return spots;
        }

        SpriteRenderer Spawn(Spot s)
        {
            string name = !string.IsNullOrEmpty(s.kind) ? s.kind : s.unit >= 1000 ? $"{s.unit}원 지폐" : $"{s.unit}원 동전";
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = s.screen;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = !string.IsNullOrEmpty(s.kind) ? BodyPartSprite(s.kind) : SpriteFor(s.unit, s.variant);
            sr.flipX = !string.IsNullOrEmpty(s.kind) && s.variant == 1;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = baseSortingOrder + s.order;
            pieces.Add(sr);
            return sr;
        }

        // ── 그림 ─────────────────────────────────────────────
        static Sprite SpriteFor(int unit, int variant)
        {
            if (!sprites.TryGetValue(unit, out Sprite[] set) || set == null || set[0] == null)
            {
                set = new Sprite[3];
                for (int v = 0; v < 3; v++) set[v] = unit >= 1000 ? DrawBill(unit, v) : DrawCoin(unit);
                sprites[unit] = set;
            }
            return set[variant % set.Length];
        }

        /// <summary>바닥에 누운 지폐: 납작한 평행사변형. variant마다 기울기가 조금씩 다르다.</summary>
        static Sprite DrawBill(int unit, int variant)
        {
            Color32 body, dark, light;
            switch (unit)
            {
                case 10000: body = PixelTex.Hex("#86A56E"); dark = PixelTex.Hex("#4C6A43"); light = PixelTex.Hex("#C3D3A2"); break;
                case 5000: body = PixelTex.Hex("#CC9A5E"); dark = PixelTex.Hex("#7E5531"); light = PixelTex.Hex("#EBCB97"); break;
                default: body = PixelTex.Hex("#7F9DC0"); dark = PixelTex.Hex("#475E80"); light = PixelTex.Hex("#BBCDE0"); break;
            }
            Color32 edge = PixelTex.Hex("#15181A");
            const int bw = 28, bh = 7;
            int skew = variant - 1; // -1, 0, 1 → 위로 갈수록 이만큼(×2) 옆으로
            var pt = new PixelTex(bw + 6, bh + 2);
            for (int y = 0; y < bh; y++)
            {
                int off = 3 + Mathf.RoundToInt(skew * 2f * y / (bh - 1));
                for (int x = 0; x < bw; x++)
                {
                    Color32 c = body;
                    if (y == bh - 1) c = light;                                   // 먼 쪽 가장자리(빛을 받음)
                    else if (y == 0) c = dark;                                    // 가까운 쪽 두께
                    else if (x >= bw / 2 - 5 && x <= bw / 2 + 1 && y >= 2 && y <= 4) c = light;   // 가운데 무늬
                    else if (x >= bw - 6 && x <= bw - 4 && y >= 2) c = dark;     // 오른쪽 숫자 자리
                    else if (x <= 2 && y >= 2) c = dark;
                    pt.Put(off + x, y + 1, c);
                }
            }
            Outline(pt, edge);
            return pt.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>바닥에 누운 동전: 납작한 타원 + 아래쪽 두께.</summary>
        static Sprite DrawCoin(int unit)
        {
            bool big = unit >= 500;
            string[] rows = big
                ? new[] { ".ddddd.", "dmmmmmd", "mlllllm", ".mlllm." }   // 아래 → 위
                : new[] { ".dddd.", "dmmmmd", ".mllm." };
            Color32 l = PixelTex.Hex(big ? "#E4E6E2" : "#D2D5D2"), m = PixelTex.Hex(big ? "#B5B9B6" : "#A4A9A7"), d = PixelTex.Hex("#5F6462");
            var pt = new PixelTex(rows[0].Length + 2, rows.Length + 2);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '.') continue;
                    pt.Put(x + 1, y + 1, ch == 'l' ? l : ch == 'm' ? m : d);
                }
            Outline(pt, PixelTex.Hex("#15181A"));
            return pt.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ── 신체 부위 그림(직접 찍는 도트, 위쪽 줄부터) ─────────────
        // p, P = 밑에 고인 피(테두리 없이 빈 곳에만)
        static readonly Dictionary<string, string[]> PartPatterns = new Dictionary<string, string[]>
        {
            // 손가락: 왼쪽 끝 손톱, 마디 주름, 오른쪽 끝은 잘린 단면(살과 뼈)
            ["finger"] = new[]
            {
                "...lllllllllllll....",
                ".NnnnllllldlllllllRr",
                "NnnnnsssssdssssssRbR",
                "NnnnNsssssssssdssRbb",
                ".dsssssssssssssssRbR",
                "..dddddddddddddddrRr",
                "....dddddddddddd.rr.",
                "....ppppppppppppPPP.",
            },
            // 눈알: 위를 보는 홍채와 동공, 흰자의 핏줄, 뒤로 끌려 나온 시신경
            ["eye"] = new[]
            {
                "...wwwww....",
                "..wwwewwww..",
                ".wwwiiiiwww.",
                ".weiIIIIiww.",
                "wwiIkkxIiwew",
                "wwiIkkkIiwww",
                ".wwiIIIIiwwr",
                ".gwwiiiiwwrR",
                "..gwwwwwgrR.",
                "...ggggg.Rr.",
                "....ppppPp..",
            },
            // 심장: 위로 굵은 혈관(대동맥·정맥), 표면의 핏줄
            ["heart"] = new[]
            {
                ".....aaa..vvv...",
                "....aAAa.vVVv...",
                "....aAa..vVv....",
                "..rrRRRRRrrrr...",
                ".rRRhhRRRRRrrr..",
                "rRRhhhRRRRRRrrr.",
                "rRRhRRRRRRRRRrrr",
                "rRRRRRRRRvRRRrrr",
                ".rRRRRRRRvRRrrr.",
                "..rRRRRRRRvRrr..",
                "...rRRRRRRRrr...",
                ".....rRRRRrr....",
                ".......rrrr.....",
                "....pppppPPpp...",
            },
            // 콩팥: 강낭콩 모양, 오목한 곳에 혈관
            ["kidney"] = new[]
            {
                "...rrrrrrr.....",
                ".rrRRhhRRRrr...",
                "rRRhhRRRRRRr...",
                "rRhRRRRRRRr.vv.",
                "rRRRRRRRRr.aaav",
                "rRRRRRRRRRr.vv.",
                ".rRRRRRRRRRr...",
                "..rrRRRRRrrr...",
                "....rrrrrr.....",
                "...ppppppPP....",
            },
        };

        static readonly Dictionary<char, Color32> PartColors = new Dictionary<char, Color32>
        {
            ['l'] = PixelTex.Hex("#EDC3A6"), ['s'] = PixelTex.Hex("#D29C80"), ['d'] = PixelTex.Hex("#9E6550"),
            ['n'] = PixelTex.Hex("#E8C4BE"), ['N'] = PixelTex.Hex("#B88A86"), ['b'] = PixelTex.Hex("#E8E0C8"),
            ['r'] = PixelTex.Hex("#6E1418"), ['R'] = PixelTex.Hex("#A3282C"), ['h'] = PixelTex.Hex("#D0555A"),
            ['a'] = PixelTex.Hex("#D7AFA6"), ['A'] = PixelTex.Hex("#A9837C"), ['v'] = PixelTex.Hex("#4D3466"), ['V'] = PixelTex.Hex("#6E5590"),
            ['w'] = PixelTex.Hex("#ECEBE2"), ['g'] = PixelTex.Hex("#B8B4A6"), ['e'] = PixelTex.Hex("#B5302C"),
            ['i'] = PixelTex.Hex("#5B7E9C"), ['I'] = PixelTex.Hex("#3A5670"), ['k'] = PixelTex.Hex("#0C0C10"), ['x'] = PixelTex.Hex("#FFFFFF"),
            ['p'] = new Color32(90, 12, 16, 220), ['P'] = new Color32(122, 20, 24, 200),
        };

        static readonly Dictionary<string, Sprite> partSprites = new Dictionary<string, Sprite>();
        static readonly Color32 PartEdge = PixelTex.Hex("#1A0A0C");

        static Sprite BodyPartSprite(string kind)
        {
            string key = GameSettings.NoBlood ? kind + "#noblood" : kind;   // 혈흔 없애기: 고인 피 대신 어두운 그림자
            if (partSprites.TryGetValue(key, out Sprite s) && s != null) return s;
            s = kind == "brain" ? DrawBrain() : DrawPattern(PartPatterns[kind]);
            partSprites[key] = s;
            return s;
        }

        static Sprite DrawPattern(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var pt = new PixelTex(w + 2, h + 2);
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[r][x];
                    if (ch == '.' || ch == 'p' || ch == 'P') continue;
                    pt.Put(x + 1, h - r, PartColors[ch]);
                }
            Outline(pt, PartEdge);
            // 고인 피는 테두리 없이, 빈 곳에만
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[r][x];
                    if ((ch == 'p' || ch == 'P') && pt.Get(x + 1, h - r).a == 0) pt.Put(x + 1, h - r, Pool(ch));
                }
            return pt.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>뇌: 분홍빛 타원에 구불구불한 주름, 가운데 갈라진 골, 밑에 고인 피.</summary>
        static Sprite DrawBrain()
        {
            const int w = 24, h = 12;
            var pt = new PixelTex(w + 2, h + 4);
            Color32 baseC = PixelTex.Hex("#CF9AA0"), fold = PixelTex.Hex("#9C6370"), light = PixelTex.Hex("#EBC2C6"),
                    groove = PixelTex.Hex("#6E3A48"), under = PixelTex.Hex("#A87580");
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ex = (x + 0.5f - w * 0.5f) / (w * 0.5f), ey = (y + 0.5f - h * 0.5f) / (h * 0.5f); // ey: 위가 +
                    if (ex * ex + ey * ey > 1f) continue;
                    Color32 c = baseC;
                    bool isFold = Mathf.Sin(x * 1.25f + Mathf.Sin(y * 1.9f) * 1.6f) + Mathf.Sin(y * 2.3f + x * 0.45f) > 0.9f;
                    if (isFold) c = fold;
                    else if (ey > 0.45f) c = light;          // 위쪽(먼 쪽) 빛 받는 면
                    if (ey < -0.55f) c = under;               // 아래쪽 그늘
                    if (Mathf.Abs(x + 0.5f - w * 0.5f) < 0.6f && ey > -0.7f) c = groove;   // 좌우 반구 사이 골
                    pt.Put(x + 1, y + 3, c);
                }
            Outline(pt, PartEdge);
            for (int x = 3; x < w - 1; x++)
                for (int y = 0; y < 3; y++)
                    if (pt.Get(x, y).a == 0 && (y > 0 || (x > 5 && x < w - 4)))
                        pt.Put(x, y, Pool(y == 2 ? 'P' : 'p'));
            return pt.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>신체 부위 밑에 고인 피(혈흔 없애기면 어두운 그림자).</summary>
        static Color32 Pool(char ch) => !GameSettings.NoBlood ? PartColors[ch] : ch == 'P' ? new Color32(34, 30, 30, 170) : new Color32(20, 18, 18, 190);

        static void Outline(PixelTex pt, Color32 c)
        {
            var filled = new bool[pt.W * pt.H];
            for (int i = 0; i < filled.Length; i++) filled[i] = pt.Px[i].a > 0;
            bool F(int x, int y) => x >= 0 && y >= 0 && x < pt.W && y < pt.H && filled[y * pt.W + x];
            for (int y = 0; y < pt.H; y++)
                for (int x = 0; x < pt.W; x++)
                    if (!filled[y * pt.W + x] && (F(x - 1, y) || F(x + 1, y) || F(x, y - 1) || F(x, y + 1))) pt.Put(x, y, c);
        }
    }
}
