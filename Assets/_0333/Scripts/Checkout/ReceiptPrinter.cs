using System.Collections;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 금전등록기 영수증 출구에서 종이가 뽑혀 나오는 모습.
    /// 종이는 출구에서 등록기 앞면을 타고 내려와 키 판 위에 눕고, 길면 앞 모서리 아래로 늘어진다.
    /// 매장 그림과 같은 원근으로 매 프레임 그려서, 물건이 많을수록 영수증이 길게 나온다.
    /// 다 나온 영수증을 조준해 클릭하면(핫스팟) 크게 볼 수 있다.
    /// </summary>
    public class ReceiptPrinter : MonoBehaviour
    {
        public SpriteRenderer target;
        [Tooltip("다 나온 영수증을 클릭하는 자리. 영수증이 나와 있을 때만 켜진다")]
        public Hotspot hotspot;
        public StoreProjection projection = StoreProjection.Default;

        [Header("종이가 지나가는 길(미터). 씬을 만들 때 금전등록기 그림에 맞춰 정해진다")]
        public float paperX0 = -1.073f, paperX1 = -0.957f;
        public float slotY = 1.195f, frontZ = 1.562f;
        public float deckY = 1.132f, deckFrontZ = 1.338f;
        public float drawerY = 1.042f, drawerFrontZ = 1.298f;
        public float bottomY = 0.93f;
        [Tooltip("3:33 끊이지 않는 영수증이 늘어지는 끝 높이(m)")]
        public float hangY = 0.35f;

        [Header("길이와 속도")]
        [Tooltip("물건 줄이 없을 때의 영수증 길이(미터)")]
        public float baseLength = 0.211f;
        [Tooltip("물건 한 줄마다 늘어나는 길이(미터)")]
        public float perLine = 0.02f;
        [Tooltip("뽑혀 나오는 빠르기(미터/초)")]
        public float speed = 0.3f;

        [Header("색")]
        public Color32 paper = new Color32(232, 229, 216, 255);
        public Color32 ink = new Color32(118, 116, 124, 255);
        public Color32 outline = new Color32(18, 17, 22, 230);

        [Header("피 묻은 영수증(이상현상)")]
        public bool bloody;
        public int bloodSeed;
        public Color32 blood = new Color32(120, 18, 22, 255);
        static readonly Color32 Stain = new Color32(40, 37, 35, 255);

        public bool Printed { get; private set; }
        public bool Printing { get; private set; }
        public float Length { get; private set; }

        struct Seg { public Vector2 a, b; public float shade, len; }
        Seg[] segs;
        PixelTex pt;
        bool[] mask;
        Vector2Int origin;
        int lineCount;

        void Awake() => Tear();

        // 설정에서 혈흔을 없애거나 되돌리면 이미 뽑힌 영수증도 다시 그린다
        void OnEnable() => GameSettings.Changed += Redraw;
        void OnDisable() => GameSettings.Changed -= Redraw;
        void Redraw() { if (bloody && Length > 0f) Draw(Length); }

        /// <summary>물건 줄 수에 맞는 길이로 영수증을 뽑는다(코루틴).</summary>
        public IEnumerator Print(int lines, AudioSource sfx)
        {
            Prepare();
            lineCount = lines;
            Printed = false;
            Printing = true;
            if (hotspot != null) hotspot.enabled = false;
            float full = ContentLength(lines);
            SoundFx.Play(sfx, SoundFx.Print, 0.8f);
            const float step = 0.012f; // 열전사 프린터처럼 조금씩 끊어서 밀어낸다
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float l = Mathf.Min(full, Mathf.Floor(speed * t / step) * step);
                if (!Mathf.Approximately(l, Length)) Draw(l);
                if (l >= full) break;
                yield return null;
            }
            Draw(full);
            Printing = false;
            Printed = true;
            if (hotspot != null) hotspot.enabled = true;
        }

        /// <summary>
        /// 3:33(끊이지 않는 영수증): 멈추지 않고 계속 뽑혀 나와 계산대 아래로 늘어진다. stop()이 참이 될 때까지.
        /// 조금 나오면 클릭해서 볼 수 있다(Printed).
        /// </summary>
        public IEnumerator PrintEndless(AudioSource sfx, System.Func<bool> stop)
        {
            Prepare();
            lineCount = 60;
            Printed = false;
            Printing = true;
            if (hotspot != null) hotspot.enabled = false;
            float max = 0f;
            foreach (var g in segs) max += g.len;
            float l = 0f, nextSound = 0f;
            while (stop == null || !stop())
            {
                l = Mathf.Min(max, l + speed * 0.7f * Time.deltaTime);
                Draw(Mathf.Floor(l / 0.012f) * 0.012f);
                if (!Printed && l > 0.25f) { Printed = true; if (hotspot != null) hotspot.enabled = true; }
                if (Time.time >= nextSound) { SoundFx.Play(sfx, SoundFx.Print, 0.7f); nextSound = Time.time + 0.95f; }   // 종이가 바닥에 쌓여도 소리는 멈추지 않는다
                yield return null;
            }
            Printing = false;
        }

        /// <summary>미리보기: 끊이지 않는 영수증이 length(m)만큼 나와 늘어진 모습.</summary>
        public void PreviewEndless(float length)
        {
            Prepare();
            lineCount = 60;
            Draw(length);
            Printed = true;
        }

        /// <summary>바로 다 나온 모습으로(에디터 미리보기용).</summary>
        public void SetPrinted(int lines)
        {
            Prepare();
            lineCount = lines;
            Draw(ContentLength(lines));
            Printed = true;
            Printing = false;
            if (hotspot != null) hotspot.enabled = true;
        }

        /// <summary>영수증을 뜯어 낸다(손님이 가져감).</summary>
        public void Tear()
        {
            Length = 0f;
            Printed = Printing = false;
            bloody = false;
            if (target != null) target.enabled = false;
            if (hotspot != null) hotspot.enabled = false;
        }

        public float ContentLength(int lines) => baseLength + perLine * Mathf.Max(1, lines);

        void Prepare()
        {
            if (segs != null) return;
            // 마지막 줄기는 3:33(끊이지 않는 영수증)에서만 닿는다: 계산대 끝에서 바닥 쪽으로 늘어진다
            var p = new[]
            {
                new Vector2(slotY, frontZ), new Vector2(deckY, frontZ), new Vector2(deckY, deckFrontZ),
                new Vector2(drawerY, deckFrontZ), new Vector2(drawerY, drawerFrontZ), new Vector2(bottomY, drawerFrontZ),
                new Vector2(hangY, drawerFrontZ - 0.03f),
            };
            float[] shade = { 0.84f, 1f, 0.78f, 0.93f, 0.72f, 0.62f }; // 서 있는 면은 어둡게, 누운 면은 밝게
            segs = new Seg[p.Length - 1];
            for (int i = 0; i < segs.Length; i++)
                segs[i] = new Seg { a = p[i], b = p[i + 1], shade = shade[i], len = Vector2.Distance(p[i], p[i + 1]) };

            // 종이가 갈 수 있는 끝까지 그렸을 때의 화면 범위 = 그림 크기
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var s in segs)
                foreach (var yz in new[] { s.a, s.b })
                    foreach (float x in new[] { paperX0, paperX1 })
                    {
                        Vector2 q = projection.ToScreen(new Vector3(x, yz.x, yz.y));
                        x0 = Mathf.Min(x0, q.x); x1 = Mathf.Max(x1, q.x);
                        y0 = Mathf.Min(y0, q.y); y1 = Mathf.Max(y1, q.y);
                    }
            origin = new Vector2Int(Mathf.FloorToInt(x0) - 2, Mathf.FloorToInt(y0) - 2);
            pt = new PixelTex(Mathf.CeilToInt(x1) + 3 - origin.x, Mathf.CeilToInt(y1) + 3 - origin.y);
            mask = new bool[pt.W * pt.H];
            if (target != null) target.transform.position = new Vector3(origin.x, origin.y, target.transform.position.z);
        }

        /// <summary>출구에서 s(미터)만큼 나간 곳의 (높이, 거리)와 그 면의 밝기.</summary>
        Vector2 At(float s, out float shade)
        {
            foreach (var g in segs)
            {
                if (s <= g.len) { shade = g.shade; return Vector2.Lerp(g.a, g.b, g.len > 0f ? s / g.len : 0f); }
                s -= g.len;
            }
            Seg last = segs[segs.Length - 1];
            shade = last.shade;
            return last.b;
        }

        void Draw(float length)
        {
            Prepare();
            Length = length;
            if (target == null) return;
            if (length <= 0f) { target.enabled = false; return; }

            pt.Clear();
            System.Array.Clear(mask, 0, mask.Length);
            if (bloody) PrepareBlood();
            const int across = 44;
            for (float s = 0f; s <= length; s += 0.001f)
            {
                Vector2 yz = At(s, out float shade);
                float c = length - s; // 종이 끝(먼저 나온 쪽)에서부터 잰 거리 = 영수증 내용의 위치
                for (int ui = 0; ui <= across; ui++)
                {
                    float u = ui / (float)across;
                    Vector2 q = projection.ToScreen(new Vector3(Mathf.Lerp(paperX0, paperX1, u), yz.x, yz.y));
                    int x = Mathf.FloorToInt(q.x) - origin.x, y = Mathf.FloorToInt(q.y) - origin.y;
                    if (!pt.In(x, y)) continue;
                    Color32 col = Inked(c, u) ? ink : paper;
                    if (bloody && Bloodied(c, u)) col = GameSettings.NoBlood ? Stain : blood;   // 혈흔 없애기: 검은 얼룩
                    pt.Put(x, y, PixelTex.Mul(col, shade));
                    mask[y * pt.W + x] = true;
                }
            }

            // 바깥 테두리
            for (int y = 0; y < pt.H; y++)
                for (int x = 0; x < pt.W; x++)
                {
                    if (mask[y * pt.W + x]) continue;
                    if (Masked(x - 1, y) || Masked(x + 1, y) || Masked(x, y - 1) || Masked(x, y + 1)) pt.Put(x, y, outline);
                }

            target.sprite = pt.ToSprite(Vector2.zero);
            target.enabled = true;

            if (hotspot != null)
            {
                int bx0 = pt.W, by0 = pt.H, bx1 = -1, by1 = -1;
                for (int y = 0; y < pt.H; y++)
                    for (int x = 0; x < pt.W; x++)
                        if (mask[y * pt.W + x]) { bx0 = Mathf.Min(bx0, x); bx1 = Mathf.Max(bx1, x); by0 = Mathf.Min(by0, y); by1 = Mathf.Max(by1, y); }
                if (bx1 >= 0)
                    hotspot.area = Rect.MinMaxRect(origin.x + bx0 - 3, origin.y + by0 - 3, origin.x + bx1 + 4, origin.y + by1 + 4);
            }
        }

        bool Masked(int x, int y) => pt.In(x, y) && mask[y * pt.W + x];

        /// <summary>영수증 내용 위치(c, u)에 핏자국이 있는지. 종이와 함께 움직이도록 내용 좌표로 정한다.</summary>
        Vector3[] blobs = new Vector3[0]; // (내용 위치 c, 가로 u, 반지름)

        void PrepareBlood()
        {
            var rnd = new System.Random(bloodSeed);
            float full = ContentLength(lineCount);
            blobs = new Vector3[2 + rnd.Next(3)];
            for (int i = 0; i < blobs.Length; i++)
                blobs[i] = new Vector3(0.02f + (float)rnd.NextDouble() * (full - 0.04f), 0.15f + (float)rnd.NextDouble() * 0.7f,
                                       0.006f + (float)rnd.NextDouble() * 0.012f);
        }

        bool Bloodied(float c, float u)
        {
            float widthM = paperX1 - paperX0;
            for (int i = 0; i < blobs.Length; i++)
            {
                float bc = blobs[i].x, bu = blobs[i].y, r = blobs[i].z;
                float dc = c - bc, du = (u - bu) * widthM;
                float d = Mathf.Sqrt(dc * dc + du * du);
                // 가장자리를 울퉁불퉁하게
                float edge = r * (0.75f + 0.5f * PixelTex.Noise(Mathf.FloorToInt(c * 400f), Mathf.FloorToInt(u * 40f), i + 3));
                if (d < edge) return true;
                // 아래로 흘러내린 줄기(종이 끝 쪽으로)
                if (i == 0 && Mathf.Abs(du) < 0.004f && dc > 0f && dc < r * 3.5f) return true;
            }
            return false;
        }

        /// <summary>작게 보이는 영수증의 인쇄 무늬(제목, 줄글, 점선, 바코드 자리).</summary>
        bool Inked(float c, float u)
        {
            bool Row(float at, float u0, float u1, float h = 0.0035f) => Mathf.Abs(c - at) < h && u > u0 && u < u1;
            bool Dash(float at) => Mathf.Abs(c - at) < 0.002f && u > 0.06f && u < 0.94f && Mathf.Repeat(u * 16f, 1f) < 0.55f;

            // 머리 부분은 글줄이 좌우로 엇갈리게(가지런히 대칭이면 멀리서 얼굴처럼 보인다)
            if (Row(0.016f, 0.14f, 0.86f, 0.0045f) || Row(0.029f, 0.3f, 0.58f)) return true;         // 상호, 지점
            if (Row(0.043f, 0.07f, 0.52f) || Row(0.043f, 0.8f, 0.93f)) return true;                 // DAY, 시각
            if (Row(0.055f, 0.07f, 0.4f) || Row(0.055f, 0.62f, 0.93f)) return true;                 // POS, 번호
            if (Row(0.067f, 0.07f, 0.47f)) return true;                                               // 방문자
            if (Dash(0.085f)) return true;
            float itemsEnd = 0.097f + perLine * Mathf.Max(1, lineCount);
            for (int i = 0; i < Mathf.Max(1, lineCount); i++)
            {
                float at = 0.104f + perLine * i;
                float n = PixelTex.Noise(i, lineCount, 77);
                if (Row(at, 0.07f, 0.16f + 0.14f * n) || Row(at, 0.52f, 0.58f) || Row(at, 0.72f, 0.93f)) return true;
            }
            float d2 = itemsEnd + 0.004f;
            if (Dash(d2)) return true;
            if (Row(d2 + 0.014f, 0.07f, 0.22f, 0.0045f) || Row(d2 + 0.014f, 0.68f, 0.93f, 0.0045f)) return true; // 합계(굵게)
            if (Row(d2 + 0.028f, 0.07f, 0.27f) || Row(d2 + 0.028f, 0.7f, 0.93f)) return true;                     // 받은 돈
            if (Dash(d2 + 0.042f)) return true;
            if (Row(d2 + 0.054f, 0.12f, 0.88f) || Row(d2 + 0.066f, 0.34f, 0.66f)) return true;                    // 감사 인사
            float bar0 = d2 + 0.08f;
            if (c > bar0 && c < bar0 + 0.026f && u > 0.16f && u < 0.84f)                                          // 바코드
                return PixelTex.Noise(Mathf.FloorToInt(u * 40f), 5, 9) > 0.45f;
            return false;
        }
    }
}
