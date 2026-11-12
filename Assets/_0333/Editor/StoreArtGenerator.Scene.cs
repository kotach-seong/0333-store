using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>매장 배치와 각 물체의 겉모습(재질). 위치·크기·색을 바꾸려면 이 파일을 고친다.</summary>
    public static partial class StoreArtGenerator
    {
        // ── 색 ───────────────────────────────────────────────
        static Vector3 H(string hex) { Color32 c = Pal.Hex(hex); return new Vector3(c.r, c.g, c.b) / 255f; }

        static readonly Vector3 White = new Vector3(0.95f, 0.95f, 0.9f);
        static readonly Vector3 Dark = H("#16191B");
        static readonly Vector3 Metal = H("#9AA09C");
        static readonly Vector3 WallCol = H("#E4E3CF");
        static readonly Vector3 BaseCol = H("#8E8C7C");
        static readonly Vector3 CeilCol = H("#EEEDDD");
        static readonly Vector3 CeilGrid = H("#B4B5A4");
        static readonly Vector3 StainCol = H("#B9A878");
        static readonly Vector3 FloorCol = H("#CFCEBE");
        static readonly Vector3 GroutCol = H("#8A8A7C");
        static readonly Vector3 MatCol = H("#3A3D3E");
        static readonly Vector3 FrameCol = H("#4A5052");
        static readonly Vector3 ShelfMetal = H("#B9BDB4");
        static readonly Vector3 ShelfLip = H("#D6D9CE");
        static readonly Vector3 ShelfKick = H("#6F7572");
        static readonly Vector3 ShelfShadow = H("#474C4B");
        static readonly Vector3 HeaderTeal = H("#2F6A66");
        static readonly Vector3 HeaderRed = H("#A8433B");
        static readonly Vector3 TagYellow = H("#DCC96A");
        static readonly Vector3 StaffDoorCol = H("#9FA69C");
        static readonly Vector3 SafetyYellow = H("#D9B93F");
        static readonly Vector3 CoolerBody = H("#C9CEC8");
        static readonly Vector3 AtmBody = H("#5B7391");
        static readonly Vector3 AtmSign = H("#26426B");
        static readonly Vector3 CounterCol = H("#CFC4A6");
        static readonly Vector3 CounterWorn = H("#DDD3B8");
        static readonly Vector3 CounterEdgeCol = H("#7B7061");
        static readonly Vector3 RegBody = H("#34393A");
        static readonly Vector3 RegBody2 = H("#474D4D");
        static readonly Vector3 KeyLight = H("#B5B8AC");
        static readonly Vector3 KeyDark = H("#687070");
        static readonly Vector3 KeyGreen = H("#55895A");
        static readonly Vector3 KeyRed = H("#A8433B");
        static readonly Vector3 KeyYellow = H("#CDB651");
        static readonly Vector3 LedOrange = new Vector3(1f, 0.45f, 0.15f);
        static readonly Vector3 MonDark = H("#232A30");
        static readonly Vector3 PhoneCol = H("#2C3031");
        static readonly Vector3 Paper = H("#ECE6CC");
        static readonly Vector3 PaperLine = H("#9AA29A");
        static readonly Vector3 PenBlue = H("#26426B");
        static readonly Vector3 BannerRed = H("#A8433B");
        static readonly Vector3 BannerBlue = H("#4470A6");
        static readonly Vector3 UmbBlack = H("#17191C");
        static readonly Vector3 UmbNavy = H("#26426B");
        static readonly Vector3 BayRim = H("#5D6A68");
        static readonly Vector3 TrayCol = H("#3F8C86");
        static readonly Vector3 TrayLight = H("#7CBDB2");
        static readonly Vector3 TrayRim = H("#2B6E69");
        static readonly Vector3 TubeOff = H("#8D958D");
        static readonly Vector3 Housing = H("#B6BAAE");
        static readonly Vector3 TubeLit = new Vector3(1f, 0.985f, 0.91f);
        static readonly Vector3 HousingLit = new Vector3(0.86f, 0.86f, 0.78f);

        static readonly Vector3[] ProductCols =
        {
            H("#A8433B"), H("#C8843A"), H("#CDB651"), H("#55895A"), H("#3F8C86"), H("#4470A6"),
            H("#775C97"), H("#C27C93"), H("#7A5A40"), H("#D9D6C4"), H("#D06A58"), H("#7FA6CF"),
        };

        static Vector3 Product(float seed) => ProductCols[Fl(seed * 997f) % ProductCols.Length];

        // ── 배치 ─────────────────────────────────────────────
        static Box Add(string name, float x0, float y0, float z0, float x1, float y1, float z1, Shader shade,
            bool fg = false, float outline = 0f, bool shadow = true)
        {
            var b = new Box
            {
                Name = name, Min = new Vector3(x0, y0, z0), Max = new Vector3(x1, y1, z1),
                Shade = shade, Fg = fg, Outline = outline, Shadow = shadow,
            };
            boxes.Add(b);
            return b;
        }

        static Shader Flat(Vector3 col) => (ref Surf s) => { s.Albedo = col; };

        static void AddLight(float x, float y, float z, Vector3 col, bool down) =>
            lights.Add(new LightS { P = new Vector3(x, y, z), Col = col, Down = down });

        static void BuildScene()
        {
            boxes.Clear();
            lights.Clear();

            // 매장 껍데기
            Add("floor", -30f, -1f, -6f, 30f, 0f, BackZ + 0.3f, FloorShade, shadow: false);
            Add("ceiling", -30f, CeilH, -6f, 30f, CeilH + 1f, BackZ + 0.3f, CeilShade, shadow: false);
            Add("wallL", -HalfW - 1f, 0f, -6f, -HalfW, CeilH, BackZ + 0.3f, SideWallShade, shadow: false);
            Add("wallR", HalfW, 0f, -6f, HalfW + 1f, CeilH, BackZ + 0.3f, SideWallShade, shadow: false);
            Add("wallBack", -HalfW, 0f, BackZ, HalfW, CeilH, BackZ + 0.3f, BackWallShade, shadow: false);

            // 진열대(짧게), 음료 냉장고, ATM
            Add("shelfL", -3.6f, 0f, 2.6f, -3.0f, 1.9f, 6.8f, ShelfShade, outline: 0.45f);
            Add("shelfR", 3.0f, 0f, 2.6f, 3.6f, 1.9f, 4.8f, ShelfShade, outline: 0.45f);
            Add("cooler", 3.0f, 0f, 5.0f, 3.6f, 2.1f, 6.6f, CoolerShade, outline: 0.45f);
            Add("atm", 2.65f, 0f, 7.45f, 3.25f, 1.55f, 8f, AtmShade, outline: 0.5f);

            // 문 주변 소품
            Add("exit", -0.26f, 2.5f, 7.9f, 0.26f, 2.7f, 8f, ExitShade, outline: 0.5f, shadow: false);
            Add("umbStand", 0.95f, 0f, 7.62f, 1.22f, 0.42f, 7.9f, UmbStandShade, outline: 0.6f);
            Add("umbA", 1.0f, 0.1f, 7.70f, 1.045f, 0.98f, 7.745f, Flat(UmbBlack), outline: 0.6f);
            Add("umbAHandle", 1.0f, 0.98f, 7.70f, 1.09f, 1.025f, 7.745f, Flat(UmbBlack), outline: 0.6f);
            Add("umbB", 1.12f, 0.1f, 7.78f, 1.16f, 0.86f, 7.82f, Flat(UmbNavy), outline: 0.6f);
            Add("wetSign", -1.05f, 0f, 6.3f, -0.73f, 0.62f, 6.34f, WetSignShade, outline: 0.6f);

            // 형광등 6개(가운데 왼쪽 맨 뒤 하나는 낡아서 누렇고 약하다)
            int k = 0;
            foreach (float z in TubeZ)
                foreach (Vector2 xr in TubeX)
                {
                    Box b = Add("tube" + k, xr.x, CeilH - 0.05f, z - 0.11f, xr.y, CeilH, z + 0.11f, TubeShade, outline: 0.35f, shadow: false);
                    b.Tube = k;
                    Vector3 col = (k == 4 ? new Vector3(1f, 0.9f, 0.66f) * 0.7f : LightCol) * TubeI;
                    AddLight(Mathf.Lerp(xr.x, xr.y, 0.25f), CeilH - 0.07f, z, col, true);
                    AddLight(Mathf.Lerp(xr.x, xr.y, 0.75f), CeilH - 0.07f, z, col, true);
                    k++;
                }
            // 계산대 위 조명(화면 밖)
            AddLight(-0.9f, CeilH - 0.07f, 0.2f, LightCol * TubeI, true);
            AddLight(0.9f, CeilH - 0.07f, 0.2f, LightCol * TubeI, true);
            // 스스로 빛나는 물건들
            AddLight(2.85f, 1.2f, 5.8f, new Vector3(0.7f, 0.9f, 1f) * 0.10f, false);    // 냉장고
            AddLight(0f, 2.55f, 7.8f, new Vector3(0.2f, 1f, 0.45f) * 0.035f, false);    // 비상구 표시등
            AddLight(2.95f, 1.2f, 7.3f, new Vector3(0.3f, 0.5f, 1f) * 0.03f, false);    // ATM 화면

            // ── 계산대(전경) ──
            // 왼쪽 끝: 금전등록기 / 가운데: 물품 칸 + 돈바구니 / 오른쪽 끝: CCTV
            Add("counter", -30f, CY - 0.04f, -2f, 30f, CY, CZ, CounterShade, fg: true);
            Add("counterBase", -30f, 0f, CZ - 0.1f, 30f, CY - 0.04f, CZ, Flat(CounterEdgeCol), fg: true);

            // 금전등록기: 서랍 → 키 판 → 몸통 → 표시창이 계단식으로 올라간다
            const float rx0 = -1.12f, rx1 = -0.64f, rz0 = 1.30f, rz1 = 1.80f;
            Add("regDrawer", rx0, CY, rz0, rx1, CY + 0.12f, rz1, RegDrawerShade, fg: true, outline: 1f);
            Add("regDeck", rx0 + 0.02f, CY + 0.12f, rz0 + 0.04f, rx1 - 0.02f, CY + 0.21f, rz1, RegDeckShade, fg: true, outline: 1f);
            Add("regUpper", rx0 + 0.02f, CY + 0.21f, rz0 + 0.28f, rx1 - 0.02f, CY + 0.34f, rz1, RegUpperShade, fg: true, outline: 1f);
            Add("regHead", rx0 + 0.12f, CY + 0.34f, rz0 + 0.34f, rx0 + 0.36f, CY + 0.48f, rz1, RegHeadShade, fg: true, outline: 1f);
            Add("receipt", rx0 + 0.045f, CY + 0.21f, rz0 + 0.265f, rx0 + 0.165f, CY + 0.275f, rz0 + 0.28f, ReceiptShade, fg: true, outline: 0.5f);

            // 물품 칸: 손님이 가져온 물건을 올려놓는 낮은 테두리의 고무 매트
            const float bx0 = -0.44f, bx1 = 0.24f, bz0 = 1.36f, bz1 = 1.80f, rim = 0.02f, rimH = 0.035f;
            Add("bayBase", bx0, CY, bz0, bx1, CY + 0.008f, bz1, BayShade, fg: true);
            Add("bayRimL", bx0 - rim, CY, bz0 - rim, bx0, CY + rimH, bz1 + rim, Flat(BayRim), fg: true, outline: 0.9f);
            Add("bayRimR", bx1, CY, bz0 - rim, bx1 + rim, CY + rimH, bz1 + rim, Flat(BayRim), fg: true, outline: 0.9f);
            Add("bayRimN", bx0, CY, bz0 - rim, bx1, CY + rimH, bz0, Flat(BayRim), fg: true, outline: 0.9f);
            Add("bayRimF", bx0, CY, bz1, bx1, CY + rimH, bz1 + rim, Flat(BayRim), fg: true, outline: 0.9f);
            // 물품 칸 앞(알바생 쪽)의 바코드 스캐너
            Add("scanner", -0.22f, CY, 1.14f, 0.02f, CY + 0.006f, 1.28f, ScannerShade, fg: true, outline: 0.5f);

            // 돈바구니: 손님이 돈을 올려놓는 청록색 접시
            const float tx0 = 0.34f, tx1 = 0.62f, tz0 = 1.52f, tz1 = 1.78f, tr = 0.015f, trH = 0.028f;
            Add("trayBase", tx0, CY, tz0, tx1, CY + 0.008f, tz1, TrayShade, fg: true);
            Add("trayRimL", tx0 - tr, CY, tz0 - tr, tx0, CY + trH, tz1 + tr, Flat(TrayRim), fg: true, outline: 0.9f);
            Add("trayRimR", tx1, CY, tz0 - tr, tx1 + tr, CY + trH, tz1 + tr, Flat(TrayRim), fg: true, outline: 0.9f);
            Add("trayRimN", tx0, CY, tz0 - tr, tx1, CY + trH, tz0, Flat(TrayRim), fg: true, outline: 0.9f);
            Add("trayRimF", tx0, CY, tz1, tx1, CY + trH, tz1 + tr, Flat(TrayRim), fg: true, outline: 0.9f);

            // CCTV(오른쪽 끝): 납작한 녹화기 위에 세운 얇은 평면 모니터
            Add("dvr", 0.80f, CY, 1.44f, 1.20f, CY + 0.07f, 1.80f, DvrShade, fg: true, outline: 1f);
            Add("monBase", 0.88f, CY + 0.07f, 1.56f, 1.12f, CY + 0.085f, 1.74f, Flat(MonDark * 1.3f), fg: true, outline: 1f);
            Add("monNeck", 0.975f, CY + 0.085f, 1.665f, 1.025f, CY + 0.16f, 1.695f, Flat(MonDark * 1.15f), fg: true, outline: 1f);
            Add("monitor", 0.70f, CY + 0.13f, 1.62f, 1.30f, CY + 0.50f, 1.65f, MonitorShade, fg: true, outline: 1f);

            // 카드 단말기, 수첩, 전화
            Add("cardReader", -0.58f, CY, 1.68f, -0.48f, CY + 0.05f, 1.80f, CardReaderShade, fg: true, outline: 1f);
            Add("notebook", 0.06f, CY, 0.99f, 0.32f, CY + 0.014f, 1.16f, NotebookShade, fg: true, outline: 0.8f);
            Add("pen", 0.35f, CY, 1.02f, 0.365f, CY + 0.012f, 1.15f, Flat(PenBlue), fg: true, outline: 0.8f);
            Add("phone", 0.44f, CY, 0.98f, 0.62f, CY + 0.05f, 1.22f, PhoneShade, fg: true, outline: 1f);
            Add("handset", 0.445f, CY + 0.05f, 0.96f, 0.505f, CY + 0.085f, 1.24f, Flat(PhoneCol * 1.25f), fg: true, outline: 1f);

            bN = boxes.Count;
            bb = new float[bN * 6];
            bSh = new bool[bN];
            for (int i = 0; i < bN; i++)
            {
                Box b = boxes[i];
                bb[i * 6] = b.Min.x; bb[i * 6 + 1] = b.Min.y; bb[i * 6 + 2] = b.Min.z;
                bb[i * 6 + 3] = b.Max.x; bb[i * 6 + 4] = b.Max.y; bb[i * 6 + 5] = b.Max.z;
                bSh[i] = b.Shadow;
            }
        }

        // ── 매장 껍데기 ──────────────────────────────────────
        static void FloorShade(ref Surf s)
        {
            float x = s.P.x, z = s.P.z;
            const float tile = 0.5f;
            int tx = Fl(x / tile), tz = Fl(z / tile);
            Vector3 c = FloorCol * (0.96f + 0.08f * Hash(tx, tz, 21));
            if (Hash(tx, tz, 22) < 0.07f) c *= 0.9f; // 낡은 타일
            float g = Mathf.Max(GridLine(x, tile, 0.014f, s.Foot.x), GridLine(z, tile, 0.014f, s.Foot.z));
            c = Vector3.Lerp(c, GroutCol, g);
            c *= Mathf.Lerp(0.86f, 1.03f, Fbm(x * 1.3f, z * 1.3f, 31)); // 얼룩
            float edge = Mathf.Min(3.0f - Mathf.Abs(x), BackZ - z);   // 진열대 밑/벽 가장자리 때
            c *= Mathf.Lerp(0.8f, 1f, Mathf.InverseLerp(0f, 0.3f, edge));
            s.Gloss = 0.16f;

            if (Mathf.Abs(x) < 0.8f && z > 6.95f && z < 7.9f)
            {
                // 입구 매트
                bool border = Mathf.Abs(x) > 0.74f || z < 7.01f || z > 7.84f;
                c = border ? Dark * 1.4f : MatCol * (Mathf.Repeat(x, 0.09f) < 0.045f ? 1f : 0.8f);
                s.Gloss = 0.02f;
            }
            else
            {
                // 비 오는 날 문 앞에 번진 물기
                float wet = 1f - new Vector2(x / 1.25f, (z - 6.75f) / 0.7f).magnitude + (Fbm(x * 4f, z * 4f, 41) - 0.5f) * 0.5f;
                if (wet > 0f) { c *= 0.88f; s.Gloss = 0.42f; }
            }
            s.Albedo = c;
        }

        static void CeilShade(ref Surf s)
        {
            float x = s.P.x, z = s.P.z;
            const float p = 0.6f;
            int px = Fl(x / p), pz = Fl(z / p);
            Vector3 c = CeilCol * (0.96f + 0.05f * Hash(px, pz, 51));
            float stain = Fbm(x * 1.7f + 3f, z * 1.7f, 61);        // 누수 얼룩
            if (stain > 0.73f) c = Vector3.Lerp(c, StainCol, Mathf.Clamp01((stain - 0.73f) * 6f));
            if (px == -5 && pz == 9) { c *= 0.8f; if (Mathf.Repeat(z, p) < 0.06f) c = Dark; } // 살짝 내려앉은 패널
            if (px == 2 && pz == 7) c = Mathf.Repeat(x, 0.075f) < 0.03f ? Dark * 1.5f : Metal; // 환기구
            float g = Mathf.Max(GridLine(x, p, 0.02f, s.Foot.x), GridLine(z, p, 0.02f, s.Foot.z));
            c = Vector3.Lerp(c, CeilGrid, g);
            s.Albedo = c;

            // 형광등 주변은 천장도 환하다
            float best = 99f;
            foreach (float tz in TubeZ)
                foreach (Vector2 xr in TubeX)
                {
                    float dx = Mathf.Max(xr.x - x, 0f, x - xr.y), dz = Mathf.Abs(z - tz);
                    best = Mathf.Min(best, dx * dx + dz * dz);
                }
            s.Emit = CeilCol * (0.16f * Mathf.Exp(-best / 0.4f));
        }

        static void SideWallShade(ref Surf s)
        {
            float y = s.P.y, z = s.P.z;
            bool left = s.P.x < 0f;
            Vector3 c = WallCol * Mathf.Lerp(0.9f, 1.02f, Fbm(z * 0.9f, y * 2.2f, left ? 71 : 72));
            if (y < 0.1f) c = BaseCol;
            // 진열대 위 홍보 띠(색이 바램)
            if (left && z > 3.4f && z < 5.9f && y > 2.12f && y < 2.56f)
                c = y < 2.16f || y > 2.52f || z < 3.46f || z > 5.84f ? White * 0.9f
                    : y > 2.27f && y < 2.41f && Mathf.Repeat((z - 3.4f) * 1.6f, 1f) < 0.72f ? White * 0.92f : BannerRed;
            if (!left && z > 2.9f && z < 4.7f && y > 2.12f && y < 2.56f)
                c = y > 2.28f && y < 2.4f ? White * 0.92f : BannerBlue;
            s.Albedo = c;
        }

        static void BackWallShade(ref Surf s)
        {
            if (s.Face != 4) { s.Albedo = WallCol; return; }
            float x = s.P.x, y = s.P.y, ax = Mathf.Abs(x);

            // 자동문: 고정 문틀은 벽에, 좌우 문짝은 따로(나중에 열 수 있게)
            if (ax < DoorHalf && y < DoorTop)
            {
                if (ax > DoorHalf - 0.06f || y > DoorTop - 0.1f)
                {
                    s.Albedo = y > DoorTop - 0.08f && y < DoorTop - 0.02f && ax < 0.14f ? Dark : FrameCol; // 센서
                    return;
                }
                s.Target = x < 0f ? TDoorL : TDoorR;
                if (ax < 0.045f || ax > DoorHalf - 0.11f || y < 0.14f || y > DoorTop - 0.17f)
                {
                    s.Albedo = ax < 0.012f ? Dark : FrameCol * 1.15f; // 문짝 틀
                    return;
                }
                if (y > 1.02f && y < 1.1f) { s.Albedo = Mathf.Repeat(ax, 0.16f) < 0.08f ? SafetyYellow : Dark; return; } // 안전띠
                GlassShade(ref s);
                return;
            }

            // 창문
            for (int i = 0; i < 2; i++)
            {
                Rect r = i == 0 ? WinL : WinR;
                if (x < r.xMin || x > r.xMax || y < r.yMin || y > r.yMax) continue;
                bool frame = x < r.xMin + 0.06f || x > r.xMax - 0.06f || y < r.yMin + 0.06f || y > r.yMax - 0.06f
                             || Mathf.Abs(x - r.center.x) < 0.028f;
                if (frame) { s.Albedo = FrameCol; return; }
                s.Target = TGlass;
                // 유리에 붙인 전단지(안에서는 뒷면이 보인다)
                bool p1 = i == 0 && x > -2.42f && x < -1.98f && y > 0.82f && y < 1.42f;
                bool p2 = i == 0 && x > -1.62f && x < -1.3f && y > 0.82f && y < 1.1f;
                bool p3 = i == 1 && x > 2.0f && x < 2.42f && y > 1.6f && y < 2.3f;
                if (p1 || p2 || p3)
                {
                    float show = Fbm(x * 9f, y * 9f, 81);
                    s.Albedo = Vector3.Lerp(Paper * 0.92f, p3 ? BannerRed : BannerBlue, show > 0.6f ? 0.22f : 0f);
                    return;
                }
                GlassShade(ref s);
                return;
            }

            Vector3 c = WallCol * Mathf.Lerp(0.9f, 1.02f, Fbm(x * 1.1f, y * 2.5f, 73));
            // 창틀 밑으로 흘러내린 얼룩
            if (y < 0.7f && ax > 1.05f && ax < 2.55f && VNoise(x * 14f, 0.5f, 74) > 0.6f)
                c *= Mathf.Lerp(0.86f, 1f, y / 0.7f);
            if (y > 0.63f && y < 0.7f && ax > 1.0f && ax < 2.6f) c = BaseCol * 1.2f; // 창턱
            if (y < 0.1f) c = BaseCol;

            // 직원용 창고 문
            if (x > Staff.xMin && x < Staff.xMax && y < Staff.yMax)
            {
                float lx = x - Staff.xMin, w = Staff.width;
                if (lx < 0.05f || lx > w - 0.05f || y > Staff.yMax - 0.05f) c = FrameCol * 1.3f;
                else
                {
                    c = StaffDoorCol;
                    if (y < 0.25f) c = Metal * 0.75f;
                    if (lx > 0.2f && lx < 0.5f && y > 1.38f && y < 1.78f)
                        c = Mathf.Repeat(lx, 0.06f) < 0.012f || Mathf.Repeat(y, 0.06f) < 0.012f ? Metal * 0.6f : Dark * 1.3f; // 철망 유리
                    if (lx > 0.16f && lx < 0.54f && y > 1.12f && y < 1.26f) c = y > 1.21f ? BannerRed : White; // STAFF ONLY
                    if (lx > 0.56f && lx < 0.62f && y > 0.95f && y < 1.07f) c = Metal;
                }
            }
            s.Albedo = c;
        }

        /// <summary>유리: 거의 투명하지만 실내 불빛이 비쳐 보이고 빗방울이 맺혀 있다.</summary>
        static void GlassShade(ref Surf s)
        {
            float x = s.P.x, y = s.P.y;
            var col = new Vector3(0.62f, 0.72f, 0.75f);
            float a = 0.06f;
            float band = Mathf.Repeat(x * 0.8f + y * 0.55f, 1.7f);
            if (band < 0.1f) { col = new Vector3(0.85f, 0.9f, 0.88f); a = 0.13f; }
            else if (band < 0.16f) a = 0.09f;

            // 유리에 비친 천장 형광등
            Vector3 d = s.Rd;
            if (d.y > 0f)
            {
                float t = (CeilH - Eye) / d.y;
                if (t > BackZ)
                {
                    float mz = 2f * BackZ - t, mx = d.x * t;
                    bool hit = Mathf.Abs(mx) < 0.8f && Mathf.Abs(mz - 0.2f) < 0.14f;
                    foreach (float tz in TubeZ)
                        foreach (Vector2 xr in TubeX)
                            if (mx > xr.x && mx < xr.y && Mathf.Abs(mz - tz) < 0.13f) hit = true;
                    if (hit) { col = new Vector3(1f, 0.98f, 0.86f); a = 0.32f; }
                }
            }
            if (Hash(s.Px, s.Py, 91) < 0.012f) { col = new Vector3(0.7f, 0.8f, 0.9f); a = 0.3f; } // 빗방울
            s.Albedo = col;
            s.Alpha = a;
        }

        // ── 진열대·냉장고·ATM ────────────────────────────────
        static void ShelfShade(ref Surf s)
        {
            bool left = s.B.Min.x < 0f;
            if (s.Face != (left ? 1 : 0)) { s.Albedo = ShelfMetal; return; }
            float y = s.P.y, z = s.L.z, top = s.B.Max.y;
            if (y < 0.1f) { s.Albedo = ShelfKick; return; }
            if (y > top - 0.12f)
            {
                // 상단 분류 띠
                float u = Mathf.Repeat(z, 0.9f);
                s.Albedo = y > top - 0.02f ? ShelfLip
                    : u > 0.12f && u < 0.62f && y > top - 0.09f && y < top - 0.045f ? White * 0.9f
                    : left ? HeaderTeal : HeaderRed;
                return;
            }

            const float pitch = 0.34f;
            int level = Fl((y - 0.1f) / pitch);
            float ly = y - 0.1f - level * pitch;
            if (ly < 0.03f)
            {
                // 선반 판 + 가격표
                float tag = Mathf.Repeat(z, 0.17f);
                bool isTag = ly < 0.024f && tag > 0.03f && tag < 0.1f && s.Foot.z < 0.02f;
                s.Albedo = isTag ? (Hash(Fl(z / 0.17f), level, 5) < 0.2f ? TagYellow : White * 0.95f) : ShelfLip;
                return;
            }

            float slotW = 0.13f + 0.035f * ((level * 3 + (left ? 0 : 2)) % 4);
            int idx = Fl(z / slotW);
            float lz = z - idx * slotW;
            float seed = Hash(idx, level, left ? 101 : 102);
            float h = (pitch - 0.03f) * Mathf.Lerp(0.5f, 0.93f, Hash(idx, level, 103));
            float gap = Mathf.Max(0.012f, s.Foot.z * 0.8f);
            if (seed < 0.07f || lz < gap || ly - 0.03f > h) { s.Albedo = ShelfShadow; return; } // 빈 칸·틈·상품 위 그늘

            Vector3 c = Product(seed);
            float ty = (ly - 0.03f) / h;
            if (ty > 0.38f && ty < 0.7f) c = Vector3.Lerp(c, White, 0.55f);  // 라벨
            if (ty > 0.9f) c *= 1.15f;                                       // 뚜껑
            if (lz < gap + slotW * 0.18f) c *= 0.78f;                        // 옆면 그늘
            s.Albedo = c;
        }

        static void CoolerShade(ref Surf s)
        {
            if (s.Face != 0) { s.Albedo = CoolerBody; return; }
            float y = s.P.y, z = s.L.z, len = s.S.z;
            var glow = new Vector3(0.78f, 0.92f, 0.95f);
            s.Albedo = CoolerBody;
            if (y > 1.88f)
            {
                // 불 켜진 상단 간판
                float u = Mathf.Repeat(z, len / 3f);
                bool text = y > 1.94f && y < 2.03f && u > 0.1f && u < 0.4f;
                s.Emit = text ? new Vector3(0.9f, 0.98f, 1f) * 0.8f : new Vector3(0.3f, 0.55f, 0.78f) * 0.7f;
                return;
            }
            if (y < 0.2f) { s.Albedo = Mathf.Repeat(y, 0.05f) < 0.025f ? Dark : CoolerBody * 0.6f; return; }

            float doorW = len / 3f;
            int door = Mathf.Min(2, Fl(z / doorW));
            float lz = z - door * doorW;
            float fr = Mathf.Max(0.035f, s.Foot.z);
            if (lz < fr || lz > doorW - fr || y < 0.25f || y > 1.84f) return;                       // 문틀
            if (lz > doorW - fr - 0.06f && lz < doorW - fr - 0.02f && y > 0.8f && y < 1.3f) { s.Albedo = Metal * 0.6f; return; } // 손잡이

            // 안쪽: 차가운 백색 조명 + 음료
            s.Albedo = Dark;
            const float pitch = 0.32f;
            int level = Fl((y - 0.25f) / pitch);
            float ly = y - 0.25f - level * pitch;
            if (ly < 0.025f) { s.Emit = glow * 0.4f; return; }
            const float bw = 0.085f;
            int bi = Fl(z / bw);
            float bz = z - bi * bw;
            float bh = Mathf.Lerp(0.17f, 0.26f, Hash(bi / 3, level, 111));
            if (bz > Mathf.Max(bw * 0.22f, s.Foot.z * 0.7f) && ly - 0.025f < bh)
            {
                Vector3 c = Product(Hash(bi / 3, level, 112));
                float ty = (ly - 0.025f) / bh;
                if (ty > 0.84f) c = White * 0.75f;                                   // 뚜껑
                else if (ty > 0.35f && ty < 0.6f) c = Vector3.Lerp(c, White, 0.5f);  // 라벨
                s.Emit = c * 0.7f;
                return;
            }
            s.Emit = glow * 0.92f;
        }

        static void AtmShade(ref Surf s)
        {
            if (s.Face != 4) { s.Albedo = AtmBody * 0.8f; return; }
            float x = s.L.x, y = s.L.y, w = s.S.x;
            Vector3 c = AtmBody;
            if (y > 1.42f) c = AtmSign;
            else if (y > 1.0f && y < 1.36f && x > 0.1f && x < w - 0.1f)
            {
                bool line = Mathf.Repeat(y, 0.07f) < 0.025f && x > 0.16f && x < w - 0.2f;
                s.Albedo = Dark;
                s.Emit = line ? new Vector3(0.75f, 0.88f, 1f) * 0.9f : new Vector3(0.12f, 0.3f, 0.62f);
                return;
            }
            else if (y > 0.7f && y < 0.92f && x > 0.12f && x < 0.36f)
                c = Mathf.Repeat(x - 0.12f, 0.06f) < 0.042f && Mathf.Repeat(y - 0.7f, 0.055f) < 0.038f ? Metal : Dark;
            else if (y > 0.78f && y < 0.82f && x > 0.4f && x < w - 0.08f) c = Dark;   // 카드 투입구
            else if (y > 0.42f && y < 0.52f && x > 0.1f && x < w - 0.1f) c = Dark;    // 현금 출구
            s.Albedo = c;
        }

        static void ExitShade(ref Surf s)
        {
            if (s.Face != 4) { s.Albedo = Dark * 2f; return; }
            float u = s.L.x / s.S.x, v = s.L.y / s.S.y;
            var green = new Vector3(0.12f, 0.78f, 0.4f);
            var white = new Vector3(0.9f, 1f, 0.92f);
            s.Albedo = Dark;
            if (u < 0.04f || u > 0.96f || v < 0.1f || v > 0.9f) { s.Emit = green * 0.45f; return; }
            bool door = u > 0.66f && u < 0.86f && v > 0.2f && v < 0.8f;
            bool head = new Vector2((u - 0.42f) * s.S.x, (v - 0.72f) * s.S.y).magnitude < 0.022f;
            bool body = u > 0.3f && u < 0.54f && v > 0.24f && v < 0.6f && Mathf.Abs(u - 0.3f - (v - 0.24f) * 0.45f) < 0.07f;
            bool arrow = u > 0.1f && u < 0.26f && Mathf.Abs(v - 0.5f) < 0.08f;
            s.Emit = door || head || body || arrow ? white : green;
        }

        static void UmbStandShade(ref Surf s)
        {
            Vector3 l = s.L;
            if (s.Face == 3 || l.y > s.S.y - 0.035f) { s.Albedo = Metal * 0.8f; return; }
            float u = s.Face < 2 ? l.z : l.x;
            s.Albedo = Mathf.Repeat(u, 0.07f) < Mathf.Max(0.02f, s.Foot.x) ? Metal * 0.6f : Dark * 1.3f;
        }

        static void WetSignShade(ref Surf s)
        {
            float u = s.L.x / s.S.x, v = s.L.y / s.S.y;
            Vector3 c = SafetyYellow;
            if (u < 0.07f || u > 0.93f || v > 0.95f) c *= 0.78f;
            float tri = Mathf.Abs(u - 0.5f) - (0.88f - v) * 0.45f;     // 경고 삼각형
            if (v > 0.5f && v < 0.88f && tri < 0f)
            {
                c = Dark;
                if (tri < -0.09f && v > 0.56f) c = SafetyYellow;
                if (Mathf.Abs(u - 0.5f) < 0.035f && v > 0.6f && v < 0.74f) c = Dark;
            }
            if ((v > 0.3f && v < 0.38f && u > 0.2f && u < 0.8f) || (v > 0.17f && v < 0.24f && u > 0.28f && u < 0.72f)) c = Dark;
            s.Albedo = c;
        }

        static void TubeShade(ref Surf s)
        {
            Vector3 l = s.L, sz = s.S;
            if (s.Face != 2) { s.Albedo = Housing * 0.9f; s.Emit = HousingLit * 0.85f; return; }
            bool end = l.x < 0.05f || l.x > sz.x - 0.05f;
            float fz = l.z / sz.z;
            bool tube = !end && ((fz > 0.14f && fz < 0.42f) || (fz > 0.58f && fz < 0.86f));
            if (s.Foot.z > sz.z * 0.2f) tube = !end; // 멀면 두 줄을 한 줄로
            s.Albedo = tube ? TubeOff : Housing;
            s.Emit = tube ? TubeLit : HousingLit;
        }

        // ── 계산대와 소품 ────────────────────────────────────
        static void CounterShade(ref Surf s)
        {
            if (s.Face != 3) { s.Albedo = CounterEdgeCol; return; }
            float x = s.P.x, z = s.P.z;
            Vector3 c = CounterCol * Mathf.Lerp(0.93f, 1.03f, Fbm(x * 3f, z * 9f, 121)); // 무늬결
            if (Hash(Fl(x * 260f), Fl(z * 260f), 122) < 0.05f) c *= 0.93f;               // 점박이
            float wear = 1f - new Vector2((x + 0.1f) / 0.6f, (z - 1.22f) / 0.3f).magnitude;
            if (wear > 0f) c = Vector3.Lerp(c, CounterWorn, wear * 0.5f);                // 손때로 닳은 자리
            if (z > CZ - 0.035f) c = CounterEdgeCol;
            if (z > CZ - 0.012f) c = CounterEdgeCol * 1.4f;
            s.Albedo = c;
            s.Gloss = 0.07f;
        }

        static void RegDrawerShade(ref Surf s)
        {
            Vector3 l = s.L, sz = s.S;
            Vector3 c = RegBody;
            if (s.Face == 4)
            {
                bool inset = l.x > 0.02f && l.x < sz.x - 0.02f && l.y > 0.015f && l.y < sz.y - 0.02f;
                c = inset ? RegBody * 1.15f : RegBody * 0.8f;
                if (inset && l.y > sz.y - 0.055f && l.y < sz.y - 0.035f && l.x > sz.x * 0.3f && l.x < sz.x * 0.7f) c = Dark; // 손잡이 홈
                if (new Vector2(l.x - (sz.x - 0.07f), l.y - 0.05f).magnitude < 0.014f) c = Metal;                          // 열쇠 구멍
            }
            else if (s.Face == 3) c = RegBody * 1.12f;
            s.Albedo = c;
        }

        static void RegDeckShade(ref Surf s)
        {
            Vector3 l = s.L;
            Vector3 c = RegBody2;
            if (s.Face == 3)
            {
                const float cell = 0.04f;
                float kx = l.x - 0.03f, kz = l.z - 0.02f;
                int col = Fl(kx / cell), row = Fl(kz / cell);
                if (kx >= 0f && kz >= 0f && col < 9 && row < 5 && col != 5)
                {
                    float fx = kx - col * cell, fz = kz - row * cell;
                    float gx = Mathf.Max(0.007f, s.Foot.x * 0.9f), gz = Mathf.Max(0.008f, s.Foot.z * 0.9f);
                    if (fx > gx && fz > gz)
                    {
                        c = col < 5 ? KeyLight : KeyDark;
                        if (col == 8 && row <= 1) c = KeyGreen;
                        if (col == 8 && row == 4) c = KeyRed;
                        if (col == 6 && row == 4) c = KeyYellow;
                    }
                    else c = RegBody * 0.7f;
                }
            }
            else if (s.Face == 4) c = l.y < 0.012f ? RegBody * 0.7f : RegBody;
            s.Albedo = c;
        }

        static void RegUpperShade(ref Surf s)
        {
            Vector3 l = s.L, sz = s.S;
            Vector3 c = RegBody;
            if (s.Face == 4)
            {
                if (l.y > 0.03f && l.y < sz.y - 0.025f)
                {
                    if (l.x > 0.03f && l.x < 0.2f) c = l.y > 0.062f && l.y < 0.082f ? Dark : RegBody2;              // 영수증 출구
                    else if (l.x > 0.24f && l.x < sz.x - 0.03f) c = Mathf.Repeat(l.y, 0.03f) < 0.012f ? RegBody * 0.7f : RegBody2; // 통풍 슬릿
                }
            }
            else if (s.Face == 3) c = RegBody2 * 1.05f;
            s.Albedo = c;
        }

        static void RegHeadShade(ref Surf s)
        {
            Vector3 l = s.L, sz = s.S;
            s.Albedo = RegBody * 0.9f;
            if (s.Face != 4 || l.x < 0.02f || l.x > sz.x - 0.02f || l.y < 0.025f || l.y > sz.y - 0.025f) return;
            // 주황 LED 표시창 "0.00"
            const float cell = 0.0105f;
            s.Albedo = new Vector3(0.03f, 0.02f, 0.02f);
            bool on = PixelFont.Hit("0.00", (l.x - 0.04f) / cell, (sz.y - 0.043f - l.y) / cell);
            s.Emit = on ? LedOrange : new Vector3(0.1f, 0.025f, 0.01f);
        }

        static void ReceiptShade(ref Surf s)
        {
            Vector3 l = s.L;
            bool text = s.Face == 4 && Mathf.Repeat(l.y, 0.016f) < 0.006f && l.x > 0.015f
                        && l.x < s.S.x * (0.5f + 0.4f * Hash(Fl(l.y / 0.016f), 7, 131));
            s.Albedo = text ? PaperLine : Paper * 1.05f;
        }

        static void DvrShade(ref Surf s)
        {
            Vector3 l = s.L;
            s.Albedo = s.Face == 3 ? Dark * 2.1f : Dark * 1.7f;
            if (s.Face != 4) return;
            if (l.y > 0.04f && l.y < 0.062f)
            {
                for (int i = 0; i < 3; i++)
                    if (l.x > 0.03f + i * 0.028f && l.x < 0.044f + i * 0.028f) { s.Albedo = Dark; s.Emit = new Vector3(0.2f, 0.85f, 0.4f); return; }
                if (l.x > 0.13f && l.x < 0.144f) { s.Albedo = Dark; s.Emit = new Vector3(0.9f, 0.15f, 0.1f); return; }
                if (l.x > 0.2f && l.x < 0.36f) s.Albedo = Dark * 0.7f; // 디스크 슬롯
            }
        }

        /// <summary>얇은 평면(LCD) 모니터: 가는 검은 테두리에 CCTV 화면 하나.</summary>
        static void MonitorShade(ref Surf s)
        {
            Vector3 l = s.L, sz = s.S;
            s.Albedo = MonDark;
            if (s.Face != 4) return;
            if (inFeed) { s.Albedo = Dark; s.Emit = new Vector3(0.4f, 0.45f, 0.5f); return; } // CCTV에 찍힌 모니터 자신
            const float side = 0.012f, bottom = 0.026f;
            if (l.x > side && l.x < sz.x - side && l.y > bottom && l.y < sz.y - side)
            {
                CctvScreen(ref s, (l.x - side) / (sz.x - 2f * side), (l.y - bottom) / (sz.y - side - bottom));
                return;
            }
            // 아래 테두리: 전원 표시등과 상표 자리
            if (l.y < bottom && l.y > 0.008f)
            {
                if (l.x > sz.x - 0.03f && l.x < sz.x - 0.018f) { s.Albedo = Dark; s.Emit = new Vector3(0.45f, 0.75f, 1f); return; }
                if (Mathf.Abs(l.x - sz.x * 0.5f) < 0.03f && l.y > 0.011f && l.y < 0.018f) s.Albedo = MonDark * 2.2f;
            }
        }

        // 계산대 뒤 천장에 달린 CCTV 카메라: 알바생 머리 위에서 출입문 쪽을 내려다본다
        static readonly Vector3 CamPos = new Vector3(0f, 2.55f, -0.6f);
        static readonly Vector3 CamFwd = (new Vector3(0f, 0.9f, 4.5f) - CamPos).normalized;
        static bool inFeed;

        /// <summary>
        /// CCTV 화면 한 장. 위의 카메라 위치에서 실제 매장을 다시 그려서 흑백 영상으로 보여준다.
        /// u: 왼→오, v: 아래→위 (0~1).
        /// </summary>
        static void CctvScreen(ref Surf s, float u, float v)
        {
            s.Albedo = new Vector3(0.02f, 0.03f, 0.03f);
            Vector3 up = Vector3.Cross(CamFwd, Vector3.right);
            Vector3 dir = (CamFwd + Vector3.right * ((u - 0.5f) * 1.5f) + up * ((v - 0.5f) * 0.92f)).normalized;

            float g = 0.03f;
            inFeed = true;
            int bi = Trace(CamPos, dir, 60f, false, out float t, out int face);
            if (bi >= 0)
            {
                Surf r = MakeSurf(bi, face, CamPos, dir, t, s.Px, s.Py, false);
                boxes[bi].Shade(ref r);
                Vector3 c = r.Target != 0 && r.Alpha < 1f ? NightCol : Vector3.Scale(r.Albedo, Light(r.P, r.N)) + r.Emit;
                g = c.x * 0.3f + c.y * 0.59f + c.z * 0.11f;
            }
            inFeed = false;

            g = Mathf.Pow(Mathf.Clamp01(g), 1.15f) * 0.9f + (Hash(s.Px, s.Py, 141) - 0.5f) * 0.035f;
            g *= 1f - 0.3f * Mathf.Pow(Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f, 4f); // 가장자리 어두움
            s.Emit = new Vector3(0.74f, 0.82f, 0.86f) * Mathf.Max(0f, g);                              // 흑백 영상

            // 화면 위에 겹쳐 찍히는 글자들: 녹화 표시와 채널 이름(왼쪽 위), 시각(오른쪽 위)
            if (v > 0.9f && v < 0.95f)
            {
                var text = new Vector3(0.9f, 0.94f, 0.94f);
                if (u > 0.03f && u < 0.055f) s.Emit = new Vector3(0.9f, 0.15f, 0.1f);
                else if (u > 0.075f && u < 0.2f && Mathf.Repeat(u, 0.03f) < 0.02f) s.Emit = text;
                else if (u > 0.72f && u < 0.97f && Mathf.Repeat(u, 0.028f) < 0.019f) s.Emit = text;
            }
        }

        /// <summary>물품 칸 바닥: 골이 진 고무 매트.</summary>
        static void BayShade(ref Surf s)
        {
            Vector3 l = s.L;
            Vector3 c = MatCol;
            if (s.Face == 3) c *= Mathf.Repeat(l.x, 0.05f) < Mathf.Max(0.02f, s.Foot.x) ? 0.78f : 1.08f;
            s.Albedo = c;
        }

        /// <summary>상판에 박힌 바코드 스캐너(빨간 레이저 선).</summary>
        static void ScannerShade(ref Surf s)
        {
            Vector3 l = s.L, sz = s.S;
            Vector3 c = MatCol * 0.9f;
            if (s.Face == 3)
            {
                if (l.x < 0.012f || l.x > sz.x - 0.012f || l.z < 0.012f || l.z > sz.z - 0.012f) c = MatCol * 1.3f;
                if (l.x > 0.04f && l.x < sz.x - 0.04f && l.z > 0.03f && l.z < sz.z - 0.03f)
                {
                    s.Albedo = new Vector3(0.05f, 0.06f, 0.07f);
                    if (Mathf.Abs(l.z - sz.z * 0.5f) < Mathf.Max(0.004f, s.Foot.z * 0.6f)) s.Emit = new Vector3(0.9f, 0.12f, 0.1f);
                    return;
                }
            }
            s.Albedo = c;
        }

        /// <summary>돈바구니 바닥: 동전이 잘 집히도록 돌기가 난 접시.</summary>
        static void TrayShade(ref Surf s)
        {
            Vector3 l = s.L;
            Vector3 c = TrayCol;
            if (s.Face == 3 && Mathf.Repeat(l.x, 0.03f) > 0.013f && Mathf.Repeat(l.z, 0.03f) > Mathf.Max(0.015f, s.Foot.z * 0.9f)) c = TrayLight;
            s.Albedo = c;
        }

        static void CardReaderShade(ref Surf s)
        {
            Vector3 l = s.L;
            s.Albedo = PhoneCol;
            if (s.Face != 3) return;
            if (l.z > 0.07f && l.z < 0.11f && l.x > 0.012f && l.x < s.S.x - 0.012f) { s.Albedo = Dark; s.Emit = new Vector3(0.25f, 0.42f, 0.32f); }
            else if (l.z < 0.06f && Mathf.Repeat(l.x, 0.022f) > 0.007f && Mathf.Repeat(l.z, 0.02f) > 0.008f) s.Albedo = KeyDark;
        }

        static void NotebookShade(ref Surf s)
        {
            Vector3 l = s.L;
            if (s.Face != 3) { s.Albedo = Paper * 0.8f; return; }
            Vector3 c = Paper;
            int line = Fl(l.z / 0.024f);
            float fz = l.z - line * 0.024f;
            if (fz < Mathf.Max(0.004f, s.Foot.z * 0.7f)) c = PaperLine;
            else if (fz > 0.009f && fz < 0.02f && Hash(line, 3, 151) < 0.65f && l.x > 0.05f && l.x < 0.05f + 0.19f * Hash(line, 4, 151)
                     && Hash(Fl(l.x * 90f), line, 152) < 0.7f) c = new Vector3(0.2f, 0.22f, 0.32f);     // 손글씨
            if (l.x > 0.035f && l.x < 0.035f + Mathf.Max(0.004f, s.Foot.x)) c = BannerRed;              // 여백선
            if (l.x < 0.02f) c = Mathf.Repeat(l.z, 0.02f) < 0.01f ? Metal * 0.5f : Paper * 0.9f;         // 스프링
            s.Albedo = c;
        }

        static void PhoneShade(ref Surf s)
        {
            Vector3 l = s.L;
            s.Albedo = PhoneCol;
            if (s.Face != 3) return;
            if (l.x > 0.085f && l.x < 0.165f && l.z > 0.03f && l.z < 0.16f)
            {
                if (Mathf.Repeat(l.x - 0.085f, 0.028f) > 0.008f && Mathf.Repeat(l.z - 0.03f, 0.034f) > Mathf.Max(0.01f, s.Foot.z * 0.8f)) s.Albedo = KeyLight * 0.85f;
            }
            else if (l.x > 0.085f && l.x < 0.165f && l.z > 0.18f && l.z < 0.215f) { s.Albedo = Dark; s.Emit = new Vector3(0.3f, 0.45f, 0.3f); } // 액정
            else if (l.x > 0.068f && l.x < 0.08f && l.z > 0.19f && l.z < 0.205f) { s.Albedo = Dark; s.Emit = new Vector3(0.9f, 0.15f, 0.1f); }   // 부재중 표시등
        }

        // ── 손으로 찍는 작은 디테일 ───────────────────────────
        static void Disc(Buf b, float cx, float cy, float r, Vector3 col)
        {
            RectInt w = WorldRect;
            for (int y = Fl(cy - r); y <= Fl(cy + r) + 1; y++)
                for (int x = Fl(cx - r); x <= Fl(cx + r) + 1; x++)
                {
                    if (!w.Contains(new Vector2Int(x, y))) continue;
                    if (new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude > r) continue;
                    int i = (y - w.y) * w.width + (x - w.x);
                    if (b.A[i] > 0f) b.C[i] = col;
                }
        }

        static void PostDetails(Buf b)
        {
            // 오른쪽 구석의 볼록 감시 거울
            Vector2 m = Project(new Vector3(3.3f, 2.45f, 7.9f));
            Disc(b, m.x, m.y, 11.5f, H("#232A30"));
            Disc(b, m.x, m.y, 10f, H("#939C91"));
            Disc(b, m.x - 1f, m.y + 1f, 8f, H("#B0B6A5"));
            Disc(b, m.x - 2f, m.y - 3f, 3.2f, H("#5D6A68"));   // 거울에 비친 매장(어두운 덩어리)
            Disc(b, m.x + 3f, m.y - 2f, 2f, H("#465254"));
            Disc(b, m.x - 4f, m.y + 4f, 2.2f, H("#F3F1DF"));   // 반짝임

            // 문 위 천장의 돔형 CCTV(빨간 불)
            Vector2 d = Project(new Vector3(0f, CeilH, 7.4f));
            Disc(b, d.x, d.y - 2f, 4.5f, H("#161B21"));
            Disc(b, d.x - 1f, d.y - 3f, 1.2f, H("#465254"));
            Disc(b, d.x + 1.5f, d.y - 3.5f, 0.8f, new Vector3(0.9f, 0.22f, 0.18f));
        }

        // ── 창밖(밤) ─────────────────────────────────────────
        static readonly RectInt OutsideRect = new RectInt(-208, -88, 416, 170);
        static readonly Vector3[] Lamps = { new Vector3(-6.7f, 4.2f, 30f), new Vector3(1.6f, 4.2f, 48f), new Vector3(11f, 4.2f, 40f) };
        static readonly Vector3 LampCol = new Vector3(1f, 0.74f, 0.38f);

        static Layer PaintOutside()
        {
            RectInt r = OutsideRect;
            var layer = new Layer("outside_night", r, 0, Group.Outside);
            var col = new Vector3[r.width * r.height];
            var skyLow = new Vector3(0.05f, 0.065f, 0.11f);
            var skyTop = new Vector3(0.012f, 0.016f, 0.03f);
            var asphalt = new Vector3(0.035f, 0.04f, 0.055f);

            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    float sx = x + 0.5f, sy = y + 0.5f;
                    Vector3 c;
                    if (sy >= VPy) c = Vector3.Lerp(skyLow, skyTop, Mathf.Clamp01((sy - VPy) / 60f));
                    else
                    {
                        float gz = F * Eye / (VPy - sy), gx = sx * gz / F;
                        float foot = gz / F;
                        bool puddle = false;
                        if (gz < 9.5f) c = new Vector3(0.1f, 0.1f, 0.095f) * (Mathf.Repeat(gx, 1.2f) < Mathf.Max(0.04f, foot) ? 0.7f : 1f); // 보도
                        else if (gz < 9.72f) c = new Vector3(0.13f, 0.13f, 0.125f);                                                        // 연석
                        else
                        {
                            c = asphalt * (0.75f + 0.5f * Fbm(gx * 2f, gz * 0.6f, 201));
                            if (gz > 11.5f && gz < 17.5f && Mathf.Repeat(gx + 1.3f, 2.6f) < Mathf.Max(0.1f, foot)) c = new Vector3(0.1f, 0.105f, 0.115f); // 주차선
                            else if (gz < 60f && Fbm(gx * 0.9f, gz * 0.35f, 211) > 0.62f) { c = new Vector3(0.05f, 0.065f, 0.1f); puddle = true; }
                        }
                        // 매장 불빛이 번진 자리
                        float spill = Mathf.Exp(-(gz - BackZ) / 2.2f) * Mathf.Clamp01((4.4f - Mathf.Abs(gx)) / 0.8f);
                        c += new Vector3(0.5f, 0.5f, 0.42f) * spill * (puddle ? 1.5f : 1f);
                        // 가로등 불빛(젖은 곳은 더 번들거린다)
                        foreach (Vector3 lamp in Lamps)
                        {
                            float d2 = (gx - lamp.x) * (gx - lamp.x) + (gz - lamp.z) * (gz - lamp.z);
                            c += LampCol * (0.45f / (1.5f + d2 * 0.25f)) * (puddle ? 1.7f : 1f);
                        }
                        if (gz > 150f) c = Vector3.Lerp(c, skyLow, 0.7f);
                    }
                    col[(y - r.y) * r.width + (x - r.x)] = c;
                }

            void Px(int x, int y, Vector3 c, bool add = false)
            {
                if (x < r.xMin || x >= r.xMax || y < r.yMin || y >= r.yMax) return;
                int i = (y - r.y) * r.width + (x - r.x);
                col[i] = add ? col[i] + c : c;
            }

            // 지평선 위 건물·나무 실루엣
            int horizon = Mathf.CeilToInt(VPy);
            var sil = new Vector3(0.022f, 0.027f, 0.045f);
            for (int x = r.xMin; x < r.xMax; x++)
            {
                int block = Fl((x + 1000) / 13f);
                bool tree = Hash(block, 4, 221) < 0.4f;
                int h = tree ? 3 + Fl(Mathf.Sin(x * 0.5f) * 1.5f + Hash(x, 5, 221) * 3f) : 3 + Fl(Hash(block, 3, 221) * 17f);
                for (int y = horizon; y < horizon + h; y++) Px(x, y, sil);
                if (!tree && h > 6 && Hash(x, block, 222) < 0.07f) Px(x, horizon + 2 + Fl(Hash(x, 9, 222) * (h - 4)), new Vector3(0.36f, 0.29f, 0.13f)); // 먼 창문 불빛
            }
            // 먼 철탑의 붉은 경고등
            for (int y = horizon; y < horizon + 27; y++) Px(-27, y, sil);
            Px(-27, horizon + 27, new Vector3(0.85f, 0.14f, 0.1f));

            // 주차된 차 한 대
            Vector2 c0 = Project(new Vector3(4.3f, 0f, 20f)), c1 = Project(new Vector3(6.3f, 1.45f, 20f));
            for (int y = Fl(c0.y); y <= Fl(c1.y); y++)
                for (int x = Fl(c0.x); x <= Fl(c1.x); x++)
                {
                    float fy = (y - c0.y) / (c1.y - c0.y);
                    float inset = fy > 0.55f ? (fy - 0.55f) / 0.45f * 9f + 3f : 0f;
                    if (x < c0.x + inset || x > c1.x - inset) continue;
                    bool glass = fy > 0.6f && fy < 0.9f && x > c0.x + inset + 1 && x < c1.x - inset - 1;
                    Px(x, y, glass ? new Vector3(0.05f, 0.065f, 0.095f) : new Vector3(0.018f, 0.02f, 0.028f));
                    if (fy > 0.93f) Px(x, y, new Vector3(0.2f, 0.15f, 0.08f)); // 지붕에 떨어진 가로등 빛
                }

            // 가로등
            foreach (Vector3 lamp in Lamps)
            {
                Vector2 top = Project(lamp), bottom = Project(new Vector3(lamp.x, 0f, lamp.z));
                int lx = Fl(top.x), ty = Fl(top.y);
                for (int y = Fl(bottom.y); y <= ty; y++) Px(lx, y, new Vector3(0.06f, 0.065f, 0.085f));
                float k = 30f / lamp.z;
                for (int dy = -14; dy <= 14; dy++)
                    for (int dx = -14; dx <= 14; dx++)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) / k;
                        float g = dist < 2.2f ? 0.5f : dist < 4.5f ? 0.2f : dist < 8f ? 0.09f : dist < 12f ? 0.035f : 0f;
                        if (g > 0f) Px(lx + dx, ty + dy, LampCol * g, true);
                        // 불빛 아래로 보이는 빗줄기
                        if (dy < 0 && Mathf.Abs(dx) < -dy * 0.45f + 1f && -dy < 22f * k && Hash(lx + dx, ty + dy, 231) < 0.16f)
                            Px(lx + dx, ty + dy, LampCol * 0.12f, true);
                    }
                Px(lx - 1, ty, new Vector3(1f, 0.92f, 0.62f)); Px(lx, ty, new Vector3(1f, 0.92f, 0.62f)); Px(lx + 1, ty, new Vector3(1f, 0.92f, 0.62f));
            }

            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    layer.Canvas.Put(x, y, PixelPalette.Quantize(col[(y - r.y) * r.width + (x - r.x)], x, y, 0.2f));
            return layer;
        }

        static Layer PaintRain()
        {
            var layer = new Layer("outside_rain", OutsideRect, 1, Group.Outside);
            PixelCanvas c = layer.Canvas;
            c.WrapY = true; // 세로로 이어 붙여도 끊기지 않게
            RectInt r = c.Rect;
            int drops = r.width * r.height / 55;
            for (int i = 0; i < drops; i++)
            {
                int x = r.xMin + Fl(Hash(i, 1, 241) * r.width);
                int y = r.yMin + Fl(Hash(i, 2, 241) * r.height);
                int len = 3 + Fl(Hash(i, 3, 241) * 6f);
                byte a = (byte)(26 + Hash(i, 4, 241) * 50f);
                for (int k = 0; k < len; k++) c.Set(x - k / 3, y - k, new Color32(150, 172, 205, a));
            }
            return layer;
        }
    }
}
