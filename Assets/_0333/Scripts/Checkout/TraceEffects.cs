using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 흔적이 눈에 보이는 모습.
    ///  1단계: 오른쪽 창문 밖에 눈만 빛나는 검은 실루엣이 서 있다(가끔 눈을 깜빡이고 아주 조금 흔들린다).
    ///         바깥 그림 층에 있어서 CCTV(따로 구운 영상)에는 찍히지 않는다.
    ///  2단계: 가게 바닥·벽·진열대·유리가 피범벅, 바닥에 내장 더미. 손님들은 아무렇지 않게 계산한다.
    ///         역시 CCTV 영상에는 없다(나만 보인다).
    /// 실루엣은 코드로 그리고, 피범벅은 덧그린 그림에서 떼어낸 피(정면) + 코드로 그린 피(그 밖)를 합친다.
    /// </summary>
    public class TraceEffects : MonoBehaviour
    {
        [Header("1단계: 오른쪽 창 너머 형체들(안광), 왼쪽 창유리에 달라붙은 손들")]
        public SpriteRenderer ghost;
        [Tooltip("형체들이 모여 선 창문(화면 좌표, 오른쪽 창). 씬을 만들 때 정해진다")]
        public Rect ghostWindow = new Rect(44f, -31f, 62f, 74f);
        [Tooltip("왼쪽 창유리에 달라붙은 손들(PixelLab 그림, 왼쪽 아래 기준으로 창에 맞춰 놓는다)")]
        public SpriteRenderer hands;

        [Header("2단계: 피범벅")]
        public SpriteRenderer blood;
        public RectInt world = new RectInt(-570, -380, 1140, 640);
        public StoreProjection projection = StoreProjection.Default;
        public int bloodSeed = 333;
        [Tooltip("덧그린 그림에서 떼어낸 피(정면 화면 640x360, 읽기 가능). 비어 있으면 전부 코드로 그린다")]
        public Texture2D bloodPaint;
        [Tooltip("그림에서 손님에게 가려 피가 없던 자리(불투명한 곳). 여기는 코드로 그린 피로 메운다")]
        public Texture2D bloodGap;
        [Tooltip("그림이 덮는 월드 영역(처음 바라보는 정면 화면)")]
        public RectInt paintRect = new RectInt(-320, -214, 640, 360);
        [Tooltip("그림 피 위에도 코드로 그린 내장 더미 3개를 얹는다")]
        public bool gutsOverPaint = true;
        [Tooltip("코드로 그린 피가 묻을 수 있는 면(월드 크기, R 바닥·G 벽·B 진열대, 읽기 가능). 없으면 어디든 그린다")]
        public Texture2D surfaceMask;
        [Tooltip("계산대 위에 묻은 피(정면 화면 640x360, 읽기 가능). 계산대 그림 위·물건 아래에 그린다")]
        public Texture2D bloodOnCounter;
        public SpriteRenderer bloodCounter;

        const int OnFloor = 1, OnWall = 2, OnShelf = 4;
        Color32[] surfPx;
        int allow;   // 지금 그리는 핏자국이 묻을 수 있는 면(0이면 가리지 않음)

        public int Stage { get; private set; }

        Sprite ghostOpen, ghostClosed;
        Vector3 ghostHome;
        bool ghostHomeSet;
        float nextBlink, blinkUntil;
        bool bloodBuilt;

        static readonly Color32 BloodDark = new Color32(58, 6, 10, 242);
        static readonly Color32 BloodMid = new Color32(104, 14, 18, 238);
        static readonly Color32 BloodFresh = new Color32(146, 22, 26, 228);
        static readonly Color32 BloodDry = new Color32(72, 24, 16, 215);

        void Awake()
        {
            if (Stage == 0)
            {
                if (ghost != null) ghost.enabled = false;
                if (hands != null) hands.enabled = false;
                if (blood != null) blood.enabled = false;
                if (bloodCounter != null) bloodCounter.enabled = false;
            }
        }

        /// <summary>0: 아무것도 없음, 1: 창밖 실루엣, 2: 실루엣 + 피범벅.</summary>
        public void SetStage(int stage)
        {
            Stage = Mathf.Clamp(stage, 0, 2);
            if (ghost != null)
            {
                if (Stage >= 1 && ghostOpen == null) BuildGhost();
                ghost.enabled = Stage >= 1;
                if (ghost.enabled) ghost.sprite = ghostOpen;
            }
            if (hands != null) hands.enabled = Stage >= 1 && hands.sprite != null;
            ShowBlood();
        }

        /// <summary>피범벅(흔적 2): 설정에서 혈흔을 없애면 안 보인다.</summary>
        void ShowBlood()
        {
            bool on = Stage >= 2 && !GameSettings.NoBlood;
            if (blood != null)
            {
                if (on && !bloodBuilt) BuildBlood();
                blood.enabled = on;
            }
            if (bloodCounter != null) bloodCounter.enabled = on && bloodCounter.sprite != null;
        }

        void OnEnable() => GameSettings.Changed += ShowBlood;
        void OnDisable() => GameSettings.Changed -= ShowBlood;

        float nextTwitch, twitchUntil;
        int blinkVariant;

        void Update()
        {
            // 왼쪽 창의 손들: 가끔 유리를 긁듯 한 칸 미끄러졌다 돌아온다
            if (hands != null && hands.enabled)
            {
                if (!handsHomeSet) { handsHome = hands.transform.localPosition; handsHomeSet = true; }
                if (Time.time >= nextTwitch) { twitchUntil = Time.time + 0.25f; nextTwitch = Time.time + Random.Range(2.5f, 6f); }
                hands.transform.localPosition = handsHome + (Time.time < twitchUntil ? new Vector3(0f, -1f, 0f) : Vector3.zero);
            }
            if (ghost == null || !ghost.enabled) return;
            // 오른쪽 창의 형체들: 가끔 몇몇이 눈을 감았다 뜬다(빛나는 눈이 꺼졌다 켜진다)
            if (Time.time >= nextBlink)
            {
                blinkUntil = Time.time + Random.Range(0.12f, 0.3f);
                nextBlink = Time.time + Random.Range(1.8f, 5f);
                blinkVariant = Random.Range(0, ghostBlinks.Length);
            }
            ghost.sprite = Time.time < blinkUntil && ghostBlinks.Length > 0 ? ghostBlinks[blinkVariant] : ghostOpen;
        }

        // ── 창밖 형체들 ──
        Sprite[] ghostBlinks = new Sprite[0];
        Vector3 handsHome;
        bool handsHomeSet;

        void BuildGhost()
        {
            int w = Mathf.Max(24, Mathf.RoundToInt(ghostWindow.width)), h = Mathf.Max(24, Mathf.RoundToInt(ghostWindow.height));
            ghostOpen = DrawCrowd(w, h, 0);
            ghostBlinks = new[] { DrawCrowd(w, h, (1 << 1) | (1 << 3)), DrawCrowd(w, h, 1 << 0), DrawCrowd(w, h, (1 << 2) | (1 << 4)) };
            ghost.transform.position = new Vector3(Mathf.Round(ghostWindow.x), Mathf.Round(ghostWindow.y), ghost.transform.position.z);
            if (!ghostHomeSet) { ghostHome = ghost.transform.localPosition; ghostHomeSet = true; }
        }

        /// <summary>
        /// 오른쪽 창 너머에 다닥다닥 모여 선 사람들의 검은 실루엣(창 크기 그대로, 왼쪽 아래 기준).
        /// 키가 다른 다섯(위로 잘린 큰 사람, 가운데 둘, 오른쪽 아래 아이, 뒤쪽 멀리 하나), 눈만 희게 빛난다.
        /// closedMask의 비트가 켜진 사람은 눈을 감고 있다.
        /// </summary>
        static Sprite DrawCrowd(int w, int h, int closedMask)
        {
            var pt = new PixelTex(w, h);
            // 뒤에서 비치는 희뿌연 빛(빗속 가로등): 이게 있어야 검은 형체들이 밤하늘에 묻히지 않고 또렷이 보인다
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.62f) / (w * 0.62f), dy = (y + 0.5f - h * 0.55f) / (h * 0.62f);
                    float k = 1f - Mathf.Sqrt(dx * dx + dy * dy);
                    if (k <= 0f) continue;
                    float n = (PixelTex.Noise(x, y, 41) - 0.5f) * 0.08f;   // 아주 약한 잡티(덩어리 무늬가 안 생기게)
                    pt.Put(x, y, new Color32(158, 174, 196, (byte)Mathf.Clamp(Mathf.RoundToInt(250f * (Mathf.Sqrt(k) * 0.85f + n)), 0, 245)));
                }
            // (머리 x, 머리 y, 머리 반지름, 어깨 반폭, 진하기 0~1) — 비율, 뒤에 있는 것부터
            var figs = new (float hx, float hy, float hr, float sh, float dark)[]
            {
                (0.16f, 0.52f, 0.075f, 0.13f, 0.55f),   // 4: 뒤쪽 멀리
                (0.62f, 0.60f, 0.105f, 0.19f, 0.85f),   // 2: 가운데 오른쪽(머리가 긴 여자)
                (0.40f, 0.64f, 0.115f, 0.21f, 0.95f),   // 1: 가운데 왼쪽
                (0.80f, 0.88f, 0.135f, 0.25f, 1f),      // 0: 오른쪽 위로 잘린 큰 사람
                (0.86f, 0.27f, 0.09f, 0.15f, 1f),       // 3: 오른쪽 아래 아이
            };
            int[] ids = { 4, 2, 1, 0, 3 };
            for (int f = 0; f < figs.Length; f++)
            {
                var g = figs[f];
                float hx = g.hx * w, hy = g.hy * h, hr = g.hr * w, sh = g.sh * w;
                var body = new Color32((byte)Mathf.Lerp(70, 6, g.dark), (byte)Mathf.Lerp(78, 8, g.dark), (byte)Mathf.Lerp(92, 12, g.dark), (byte)Mathf.Lerp(200, 252, g.dark));
                var rim = new Color32((byte)Mathf.Lerp(86, 28, g.dark), (byte)Mathf.Lerp(94, 32, g.dark), (byte)Mathf.Lerp(108, 40, g.dark), body.a);
                bool longHair = ids[f] == 2;
                bool Shape(float x, float y)
                {
                    float dx = (x - hx) / hr, dy = (y - hy) / (hr * 1.18f);
                    if (dx * dx + dy * dy <= 1f) return true;                                  // 머리
                    if (longHair && Mathf.Abs(x - hx) <= hr * 1.05f && y < hy && y > hy - hr * 2.6f) return true;   // 늘어진 머리카락
                    if (Mathf.Abs(x - hx) <= hr * 0.45f && y < hy && y > hy - hr * 1.6f) return true;           // 목
                    float top = hy - hr * 1.35f;
                    if (y <= top)
                    {
                        float k = Mathf.Clamp01((top - y) / (hr * 1.2f));                         // 어깨가 둥글게 넓어진다
                        float half = Mathf.Lerp(hr * 0.9f, sh, Mathf.Sqrt(k));
                        return Mathf.Abs(x - hx) <= half;
                    }
                    return false;
                }
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        if (!Shape(px, py)) continue;
                        bool edge = !Shape(px - 1f, py) || !Shape(px + 1f, py) || !Shape(px, py + 1f);
                        pt.Put(x, y, edge ? rim : body);
                    }
                if ((closedMask & (1 << ids[f])) != 0) continue;
                // 안광: 머리 가운데 약간 위, 두 점(흰빛 심 + 둘레에 붉은 기가 도는 번짐)
                int ey = Mathf.RoundToInt(hy + hr * 0.05f);
                float gap = Mathf.Max(1.5f, hr * 0.42f);
                foreach (float ex in new[] { hx - gap, hx + gap })
                {
                    int cx = Mathf.RoundToInt(ex);
                    for (int gy = ey - 2; gy <= ey + 2; gy++)
                        for (int gx = cx - 2; gx <= cx + 2; gx++)
                        {
                            float d = Mathf.Sqrt((gx - cx) * (gx - cx) + (gy - ey) * (gy - ey) * 1.6f);
                            if (d < 2.4f && d > 0.6f) pt.Set(gx, gy, new Color32(235, 120, 80, (byte)(90f * (1f - d / 2.4f) * g.dark + 20)));
                        }
                    pt.Put(cx, ey, new Color32(255, 246, 222, 255));
                    if (hr > 5f) pt.Put(cx + (ex < hx ? -1 : 1), ey, new Color32(250, 220, 190, 230));
                }
            }
            return pt.ToSprite(Vector2.zero);
        }

        // ── 피범벅 ──
        PixelTex bt;

        // 덧그린 그림에서 떼어낸 피(정면 화면)와 그림에서 손님에게 가려 비어 있던 자리
        Color32[] paintPx, gapPx;

        /// <summary>
        /// 정면 화면은 사용자가 덧그린 그림의 피를 쓰고, 그림이 없는 곳(고개를 돌려야 보이는 곳)과
        /// 그림에서 손님에게 가려 비어 있던 자리는 코드로 그린 피로 채운다. 내장 더미는 그 위에 얹는다.
        /// 그림이 없으면 예전처럼 전부 코드로 그린다.
        /// </summary>
        void BuildBlood()
        {
            bloodBuilt = true;
            bt = new PixelTex(world.width, world.height);
            var rnd = new System.Random(bloodSeed);
            paintPx = Readable(bloodPaint) ? bloodPaint.GetPixels32() : null;
            gapPx = paintPx != null && Readable(bloodGap) ? bloodGap.GetPixels32() : null;
            surfPx = surfaceMask != null && surfaceMask.isReadable && surfaceMask.width == world.width && surfaceMask.height == world.height ? surfaceMask.GetPixels32() : null;

            // 바닥(계산대 너머로 보이는 곳: 4.5~7.8m)의 핏자국
            allow = OnFloor;
            for (int i = 0; i < 14; i++)
            {
                float x = Rf(rnd, -2.4f, 2.4f), z = Rf(rnd, 4.6f, 7.8f);
                Vector2 c = projection.ToScreen(new Vector3(x, 0f, z));
                float r = Rf(rnd, 0.18f, 0.55f);
                float rx = projection.focal * r / z;
                if (CodeHere(c.x, c.y)) Splat(c.x, c.y, rx, rx * 0.3f, rnd);
            }
            // 문에서 계산대 쪽으로 이어진 핏방울 자국
            for (float z = 7.8f; z > 4.6f; z -= 0.22f)
            {
                float x = Mathf.Lerp(0.15f, 0.7f, (7.8f - z) / 3.2f) + Rf(rnd, -0.06f, 0.06f);
                Vector2 c = projection.ToScreen(new Vector3(x, 0f, z));
                float rx = 1.2f + rnd.Next(2);
                if (CodeHere(c.x, c.y)) Disc(c.x, c.y, rx, 0.7f, BloodMid);
            }
            // 바닥의 내장 더미(자리만 정해 두고 맨 마지막에 그린다)
            var guts = new Vector2[3];
            for (int i = 0; i < guts.Length; i++)
            {
                float x = (i - 1) * Rf(rnd, 1.1f, 1.8f), z = Rf(rnd, 4.8f, 6.2f);
                guts[i] = projection.ToScreen(new Vector3(x, 0f, z));
            }
            // 안쪽 벽(8m, 유리·문에는 안 묻는다): 흘러내린 피와 손바닥 자국
            allow = OnWall;
            for (int i = 0; i < 7; i++)
            {
                float x = Rf(rnd, -3.4f, 3.4f), y = Rf(rnd, 0.9f, 2.2f);
                Vector2 c = projection.ToScreen(new Vector3(x, y, 7.95f));
                bool draw = CodeHere(c.x, c.y);
                if (draw) Splat(c.x, c.y, Rf(rnd, 3f, 6f), Rf(rnd, 3f, 5f), rnd);
                int drips = rnd.Next(1, 4);
                for (int k = 0; k < drips; k++)
                {
                    float dx = c.x + Rf(rnd, -4f, 4f); int len = rnd.Next(8, 26), dw = rnd.Next(1, 3);
                    if (draw) Drip(dx, c.y - 2f, len, dw);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float x = Rf(rnd, -2.6f, 2.6f), y = Rf(rnd, 0.8f, 1.6f);
                Vector2 c = projection.ToScreen(new Vector3(x, y, 7.95f));
                bool left = rnd.Next(2) == 0;
                if (CodeHere(c.x, c.y)) Hand(c.x, c.y, left);
            }
            // 양옆 진열대에 튄 피(그림이 있으면 고개를 돌려야 보이는 바깥쪽까지 더 뿌린다)
            allow = OnShelf | OnWall;
            int shelf = paintPx != null ? 24 : 12;
            for (int i = 0; i < shelf; i++)
            {
                float far = i < 12 ? 420f : 560f;
                float sx = (rnd.Next(2) == 0 ? -1f : 1f) * Rf(rnd, i < 12 ? 175f : 330f, far), sy = Rf(rnd, -150f, 40f);
                float rx = Rf(rnd, 3f, 9f), ry = Rf(rnd, 2f, 6f);
                bool drip = rnd.Next(2) == 0; int len = rnd.Next(10, 40), dw = rnd.Next(1, 3);
                if (!CodeHere(sx, sy)) continue;
                Splat(sx, sy, rx, ry, rnd);
                if (drip) Drip(sx, sy - 2f, len, dw);
            }
            // 그림에서 손님에게 가려 비어 있던 자리: 바닥 핏자국과 문·벽에 흐른 피로 메운다
            allow = OnFloor;
            if (gapPx != null) FillGap(new System.Random(bloodSeed + 7));
            allow = 0;
            // 그림의 피를 그 위에 덮는다
            if (paintPx != null)
            {
                int ox = paintRect.x - world.x, oy = paintRect.y - world.y;
                for (int y = 0; y < paintRect.height; y++)
                    for (int x = 0; x < paintRect.width; x++)
                    {
                        Color32 c = paintPx[y * paintRect.width + x];
                        if (c.a > 0) bt.Put(x + ox, y + oy, c);
                    }
            }
            // 내장 더미는 맨 위(바닥에만)
            allow = OnFloor;
            foreach (var g in guts)
                if (gutsOverPaint || CodeHere(g.x, g.y)) Guts(g.x, g.y, rnd);
            allow = 0;

            blood.sprite = bt.ToSprite(Vector2.zero);
            blood.transform.localPosition = new Vector3(world.x, world.y, blood.transform.localPosition.z);

            // 계산대 위의 피: 계산대 그림(50) 위, 금전등록기 숫자·물건(51~) 아래
            if (bloodCounter != null && Readable(bloodOnCounter))
            {
                var ct = new PixelTex(paintRect.width, paintRect.height);
                Color32[] cp = bloodOnCounter.GetPixels32();
                for (int i = 0; i < cp.Length; i++) if (cp[i].a > 0) ct.Px[i] = cp[i];
                bloodCounter.sprite = ct.ToSprite(Vector2.zero);
                bloodCounter.transform.localPosition = new Vector3(paintRect.x, paintRect.y, bloodCounter.transform.localPosition.z);
            }
        }

        bool Readable(Texture2D t) => t != null && t.isReadable && t.width == paintRect.width && t.height == paintRect.height;

        bool InPaint(float sx, float sy) =>
            paintPx != null && sx >= paintRect.xMin && sx < paintRect.xMax && sy >= paintRect.yMin && sy < paintRect.yMax;

        bool InGap(float sx, float sy)
        {
            if (gapPx == null || !InPaint(sx, sy)) return false;
            int x = Mathf.FloorToInt(sx) - paintRect.x, y = Mathf.FloorToInt(sy) - paintRect.y;
            return gapPx[y * paintRect.width + x].a > 0;
        }

        /// <summary>코드로 그린 피를 여기에 둘까: 그림이 없는 곳이거나, 그림에서 손님에게 가려 비어 있던 자리.</summary>
        bool CodeHere(float sx, float sy) => !InPaint(sx, sy) || InGap(sx, sy);

        /// <summary>손님 자리(그림에서 비어 있던 곳) 안쪽 바닥에 핏자국 몇 개.</summary>
        void FillGap(System.Random rnd)
        {
            // 비어 있던 자리의 범위
            int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
            for (int y = 0; y < paintRect.height; y++)
                for (int x = 0; x < paintRect.width; x++)
                    if (gapPx[y * paintRect.width + x].a > 0) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            if (x1 < x0) return;
            float floorLine = projection.ToScreen(new Vector3(0f, 0f, 7.95f)).y;   // 안쪽 벽과 바닥이 만나는 높이
            // 손님 뒤는 거의 문 유리라 벽 핏자국은 두지 않고, 바닥에만 핏자국을 둔다
            var placed = new System.Collections.Generic.List<Vector2>();
            int floorSplats = 0;
            for (int tries = 0; tries < 400 && floorSplats < 6; tries++)
            {
                float sx = paintRect.x + x0 + (float)rnd.NextDouble() * (x1 - x0);
                float sy = paintRect.y + y0 + (float)rnd.NextDouble() * (y1 - y0);
                if (!InGap(sx, sy) || sy >= floorLine - 2f) continue;
                bool near = false;
                foreach (var p in placed) if ((p - new Vector2(sx, sy)).sqrMagnitude < 14f * 14f) near = true;
                if (near) continue;
                placed.Add(new Vector2(sx, sy));
                // 멀수록(화면 위쪽일수록) 납작하고 작게
                float k = Mathf.InverseLerp(floorLine, paintRect.y + y0, sy);
                float rx = Mathf.Lerp(5f, 13f, k) * Rf(rnd, 0.8f, 1.2f);
                Splat(sx, sy, rx, rx * 0.32f, rnd);
                floorSplats++;
            }
        }

        static float Rf(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        void Dot(float wx, float wy, Color32 c)
        {
            int x = Mathf.FloorToInt(wx) - world.x, y = Mathf.FloorToInt(wy) - world.y;
            if (surfPx != null && allow != 0)
            {
                // 이 자리에 보이는 면이 바닥·벽·진열대 중 허락된 것일 때만(입간판·우산꽂이 앞, 유리 위에는 안 묻는다)
                if (x < 0 || y < 0 || x >= world.width || y >= world.height) return;
                Color32 m = surfPx[y * world.width + x];
                // 투명한 칸은 아무 면도 아니다(유니티가 투명한 칸에 옆 칸 색을 번져 넣으므로 색만 보면 안 된다)
                int s = m.a < 128 ? 0 : (m.r > 127 ? OnFloor : 0) | (m.g > 127 ? OnWall : 0) | (m.b > 127 ? OnShelf : 0);
                if ((s & allow) == 0) return;
            }
            bt.Set(x, y, c);
        }

        void Disc(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry) - 1; y <= Mathf.CeilToInt(cy + ry) + 1; y++)
                for (int x = Mathf.FloorToInt(cx - rx) - 1; x <= Mathf.CeilToInt(cx + rx) + 1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / Mathf.Max(0.5f, ry);
                    if (dx * dx + dy * dy <= 1f) Dot(x, y, c);
                }
        }

        /// <summary>가장자리가 울퉁불퉁하고 테두리가 진한 핏자국 + 주변에 튄 방울.</summary>
        void Splat(float cx, float cy, float rx, float ry, System.Random rnd)
        {
            int salt = rnd.Next(1000);
            ry = Mathf.Max(1.2f, ry);
            for (int y = Mathf.FloorToInt(cy - ry * 1.5f); y <= Mathf.CeilToInt(cy + ry * 1.5f); y++)
                for (int x = Mathf.FloorToInt(cx - rx * 1.5f); x <= Mathf.CeilToInt(cx + rx * 1.5f); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float ang = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * 10f;
                    int b = Mathf.FloorToInt(ang);
                    float edge = 0.75f + 0.5f * Mathf.Lerp(PixelTex.Noise(b % 10, salt), PixelTex.Noise((b + 1) % 10, salt), ang - b);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= edge) continue;
                    Color32 c = d > edge - 0.18f ? BloodDark : d < edge * 0.45f && PixelTex.Noise(x, y, salt) > 0.6f ? BloodFresh : BloodMid;
                    Dot(x, y, c);
                }
            int sat = rnd.Next(2, 7);
            for (int k = 0; k < sat; k++)
            {
                float a = Rf(rnd, 0f, Mathf.PI * 2f), dist = Rf(rnd, 1.2f, 2.3f);
                Disc(cx + Mathf.Cos(a) * rx * dist, cy + Mathf.Sin(a) * ry * dist, Rf(rnd, 0.6f, 1.6f), Rf(rnd, 0.6f, 1.4f), BloodMid);
            }
        }

        void Drip(float x, float y, int len, int w)
        {
            for (int i = 0; i < len; i++)
                for (int k = 0; k < w; k++) Dot(x + k, y - i, i < len - 2 ? BloodMid : BloodDark);
            Disc(x + w * 0.5f, y - len, w * 0.5f + 0.9f, w * 0.5f + 0.9f, BloodDark);
        }

        void Hand(float cx, float cy, bool left)
        {
            Disc(cx, cy, 3.4f, 4f, BloodDry);
            float s = left ? -1f : 1f;
            for (int f = 0; f < 4; f++)
            {
                float fx = cx - 2.4f + f * 1.6f;
                for (int i = 0; i < 4 + (f == 1 || f == 2 ? 1 : 0); i++) Dot(fx, cy + 4f + i, BloodDry);
            }
            for (int i = 0; i < 3; i++) Dot(cx + s * (3.5f + i), cy + 1f + i, BloodDry);   // 엄지
        }

        /// <summary>바닥에 널린 내장 더미: 피 웅덩이 위로 구불구불한 창자.</summary>
        void Guts(float cx, float cy, System.Random rnd)
        {
            Disc(cx, cy, Rf(rnd, 10f, 15f), Rf(rnd, 3.5f, 5f), BloodDark);
            Color32 gut = new Color32(176, 78, 84, 255), gutD = new Color32(118, 38, 44, 255), gutL = new Color32(222, 128, 132, 255);
            float x = cx - 8f, y = cy;
            float a = Rf(rnd, -0.5f, 0.5f);
            for (int i = 0; i < 26; i++)
            {
                a += Rf(rnd, -0.9f, 0.9f);
                x += Mathf.Cos(a) * 1.3f;
                y += Mathf.Sin(a) * 0.55f;
                x = Mathf.Clamp(x, cx - 11f, cx + 11f);
                y = Mathf.Clamp(y, cy - 3f, cy + 3f);
                Disc(x, y + 1f, 2f, 1.5f, gutD);
                Disc(x, y + 1.4f, 1.6f, 1.2f, gut);
                Dot(x - 0.5f, y + 2f, gutL);
            }
        }
    }
}
