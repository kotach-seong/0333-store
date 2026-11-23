using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// CCTV 화면. 카메라 위치에서 실제 매장(안과 밖)을 다시 그려 흑백 영상으로 만든다.
    ///  1번: 알바생 머리 위에서 출입문 쪽 → 내 뒷모습, 손님 앞모습
    ///  2번: 출입문 쪽 천장에서 계산대 쪽 → 손님 뒷모습, 내 앞모습
    ///  3번: 주차장 쪽에서 매장 입구 → 들어오는 손님, 밖에서 쳐다보기만 하는 사람(뒷모습)
    /// 매장 밖(보도·주차장·간판·가로등)은 CCTV에만 쓰이는 3D 배경이다.
    /// 알바생이 창문으로 직접 보는 바깥은 PaintOutside가 따로 그린다.
    /// </summary>
    public static partial class StoreArtGenerator
    {
        public struct Cam
        {
            public Vector3 Pos, Target;
            public float TanW, TanH;   // 시야의 절반(탄젠트)
            public float Gain;         // 밝기 증폭(야간 카메라)
            public bool Outdoor;       // 빗줄기 표시
        }

        static readonly Cam Cam1 = new Cam { Pos = new Vector3(0f, 2.55f, -0.6f), Target = new Vector3(0f, 0.9f, 4.5f), TanW = 0.75f, TanH = 0.46f, Gain = 1f };
        static readonly Cam Cam2 = new Cam { Pos = new Vector3(0f, 2.6f, 7.6f), Target = new Vector3(0f, 1.0f, 2.2f), TanW = 0.75f, TanH = 0.46f, Gain = 1f };
        // 3번: 주차장 가로등 기둥에 달려 매장 정면을 본다(밖에 선 사람은 뒷모습). 사용자가 고른 구도.
        static readonly Cam Cam3 = new Cam { Pos = new Vector3(4.2f, 3.6f, 15.5f), Target = new Vector3(0.2f, 1.1f, 8.6f), TanW = 0.62f, TanH = 0.35f, Gain = 3f, Outdoor = true };

        static readonly Vector3 SkyNight = new Vector3(0.02f, 0.025f, 0.04f);
        static readonly Vector3 FacadeCol = new Vector3(0.3f, 0.31f, 0.33f);
        static bool inFeed;

        // ── 매장 밖 ──────────────────────────────────────────
        static void BuildExterior()
        {
            float z0 = BackZ + 0.3f;
            Add("outGround", -60f, -1f, z0, 60f, 0f, 90f, OutGroundShade, shadow: false);
            Add("facadeL", -30f, 0f, BackZ, -HalfW - 1f, CeilH, z0, FacadeSideShade);
            Add("facadeR", HalfW + 1f, 0f, BackZ, 30f, CeilH, z0, FacadeSideShade);
            Add("fascia", -30f, CeilH, z0, 30f, CeilH + 1.1f, z0 + 0.2f, FasciaShade);   // 간판이 달린 처마
            Add("bin", -1.95f, 0f, z0 + 0.1f, -1.55f, 0.8f, z0 + 0.5f, Flat(new Vector3(0.16f, 0.24f, 0.2f)));
            AddLight(0f, CeilH - 0.2f, z0 + 0.7f, new Vector3(1f, 1f, 0.92f) * 2.5f, false, true); // 처마 밑 등

            foreach (Vector3 lamp in Lamps)
            {
                Add("lampPole", lamp.x - 0.07f, 0f, lamp.z - 0.07f, lamp.x + 0.07f, lamp.y, lamp.z + 0.07f, Flat(Dark * 1.6f));
                Add("lampHead", lamp.x - 0.35f, lamp.y, lamp.z - 0.15f, lamp.x + 0.35f, lamp.y + 0.12f, lamp.z + 0.15f, LampHeadShade, shadow: false);
                if (!MenuDawn) AddLight(lamp.x, lamp.y - 0.15f, lamp.z, new Vector3(1f, 0.8f, 0.5f) * 14f, false, true);   // 새벽엔 가로등이 꺼졌다
            }

            // 주차된 차
            Add("carBody", 4.3f, 0.25f, 18f, 6.3f, 0.85f, 22.2f, Flat(new Vector3(0.07f, 0.075f, 0.09f)));
            Add("carCabin", 4.45f, 0.85f, 19f, 6.15f, 1.42f, 21.3f, Flat(new Vector3(0.1f, 0.12f, 0.15f)));

            // 계산대 뒤 벽(담배 진열장). 알바생 등 뒤라 직접 보이지 않고 2번 CCTV에만 찍힌다.
            Add("clerkWall", -HalfW - 1f, 0f, -1.3f, HalfW + 1f, CeilH, -1.0f, ClerkWallShade);
        }

        // 계산대 뒤 벽의 담배 진열장(작게)과, 통로 뒤의 그림 문
        const float CabHalfW = 0.9f, CabY0 = 1.3f, CabY1 = 2.1f;
        static readonly Rect BackDoor = Rect.MinMaxRect(-3.3f, 0f, -2.6f, 2.05f);

        static void ClerkWallShade(ref Surf s)
        {
            float x = s.P.x, y = s.P.y;
            Vector3 c = WallCol * 0.95f;
            if (s.Face != 5) { s.Albedo = c; return; }

            if (Mathf.Abs(x) < CabHalfW && y > CabY0 && y < CabY1)
            {
                // 담배 진열장: 칸칸이 꽂힌 작은 갑
                c = ShelfShadow;
                float fx = Mathf.Repeat(x, 0.09f), fy = Mathf.Repeat(y - CabY0, 0.2f);
                if (fy < 0.03f) c = ShelfLip;
                else if (fx > 0.015f && fy < 0.17f) c = Product(Hash(Fl(x / 0.09f) / 3, Fl((y - CabY0) / 0.2f), 321));
            }
            else if (Mathf.Abs(x) < CabHalfW && y >= CabY1 && y < CabY1 + 0.12f) c = HeaderRed; // 진열장 위 띠
            else if (x > BackDoor.xMin && x < BackDoor.xMax && y < BackDoor.yMax)
            {
                // 그림으로만 된 문(열리지 않는다): 직원용 창고 문과 같은 모양
                float lx = x - BackDoor.xMin, w = BackDoor.width;
                if (lx < 0.05f || lx > w - 0.05f || y > BackDoor.yMax - 0.05f) c = FrameCol * 1.3f;
                else
                {
                    c = StaffDoorCol;
                    if (y < 0.25f) c = Metal * 0.75f;
                    if (lx > 0.2f && lx < 0.5f && y > 1.38f && y < 1.78f)
                        c = Mathf.Repeat(lx, 0.06f) < 0.012f || Mathf.Repeat(y, 0.06f) < 0.012f ? Metal * 0.6f : Dark * 1.3f; // 철망 유리
                    if (lx > 0.16f && lx < 0.54f && y > 1.12f && y < 1.26f) c = y > 1.21f ? BannerRed : White;        // STAFF ONLY
                    if (lx > 0.56f && lx < 0.62f && y > 0.95f && y < 1.07f) c = Metal;                                 // 손잡이
                }
            }
            else if (y < 0.1f) c = BaseCol;
            s.Albedo = c;
        }

        static void OutGroundShade(ref Surf s)
        {
            float x = s.P.x, z = s.P.z;
            Vector3 c;
            if (z < 9.8f)
            {
                // 보도블록
                c = new Vector3(0.44f, 0.44f, 0.42f) * (0.92f + 0.1f * Hash(Fl(x / 1.2f), Fl(z / 0.75f), 301));
                float g = Mathf.Max(GridLine(x, 1.2f, 0.02f, s.Foot.x), GridLine(z, 0.75f, 0.02f, s.Foot.z));
                c = Vector3.Lerp(c, c * 0.6f, g);
            }
            else if (z < 10.02f) c = new Vector3(0.5f, 0.5f, 0.48f); // 연석
            else
            {
                // 젖은 아스팔트 + 주차선
                c = new Vector3(0.2f, 0.205f, 0.22f) * (0.8f + 0.4f * Fbm(x * 2f, z * 0.6f, 201));
                if (z > 11.5f && z < 17.5f && Mathf.Repeat(x + 1.3f, 2.6f) < Mathf.Max(0.1f, s.Foot.x)) c = new Vector3(0.4f, 0.41f, 0.42f);
                else if (Fbm(x * 0.9f, z * 0.35f, 211) > 0.62f) c *= 0.6f; // 물웅덩이
            }
            s.Albedo = c;
        }

        static void FacadeSideShade(ref Surf s)
        {
            float x = s.P.x, y = s.P.y;
            s.Albedo = y < 0.15f ? FacadeCol * 0.6f
                : FacadeCol * (Mathf.Repeat(x, 0.9f) < 0.03f || Mathf.Repeat(y, 0.9f) < 0.03f ? 0.75f : 1f);
        }

        static void FasciaShade(ref Surf s)
        {
            s.Albedo = FacadeCol * 0.8f;
            if (s.Face != 5) return;
            float u = (s.P.x + 3.2f) / 6.4f, v = (s.P.y - CeilH) / 1.1f;
            if (u < 0f || u > 1f || v < 0.18f || v > 0.82f) return;
            // 불 켜진 간판: 위아래 초록 띠, 가운데 흰 바탕에 글자 자리
            s.Albedo = Dark;
            bool stripe = v > 0.64f || v < 0.34f;
            s.Emit = stripe ? new Vector3(0.2f, 0.75f, 0.45f) : new Vector3(0.95f, 0.97f, 0.9f);
            if (!stripe && u > 0.22f && u < 0.78f && Mathf.Repeat(u * 9f, 1f) < 0.68f) s.Emit = new Vector3(0.15f, 0.3f, 0.6f);
        }

        static void LampHeadShade(ref Surf s)
        {
            s.Albedo = Dark * 2f;
            if (s.Face == 2 && !MenuDawn) s.Emit = new Vector3(1f, 0.85f, 0.55f);
        }

        // ── 엔딩: 비 그친 새벽 ──
        /// <summary>메인 화면 카메라 그림을 비 그친 새벽으로(가로등 꺼짐, 하늘이 밝아 오고 바깥에 푸른 새벽빛, 빗줄기 없음).</summary>
        public static bool MenuDawn { get; private set; }
        static readonly Vector3 DawnSky = new Vector3(0.36f, 0.4f, 0.5f);
        static readonly Vector3 DawnAmbient = new Vector3(1.55f, 1.65f, 1.85f);

        /// <summary>새벽 빛으로 장면을 다시 세운다(끄면 평소 밤으로).</summary>
        public static void SetDawn(bool on)
        {
            MenuDawn = on;
            BuildScene();
        }

        // ── 카메라로 장면 다시 그리기 ─────────────────────────
        /// <summary>카메라 화면의 (u,v) 지점에 보이는 색. u: 왼→오, v: 아래→위 (0~1).</summary>
        static Vector3 FeedColor(in Cam cam, float u, float v, int px, int py)
        {
            Vector3 fwd = (cam.Target - cam.Pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            Vector3 dir = (fwd + right * ((u - 0.5f) * 2f * cam.TanW) + up * ((v - 0.5f) * 2f * cam.TanH)).normalized;

            bool was = inFeed;
            inFeed = true;
            Vector3 c = FeedTrace(cam.Pos, dir, px, py, 0);
            inFeed = was;
            return c;
        }

        static Vector3 FeedTrace(Vector3 o, Vector3 dir, int px, int py, int depth)
        {
            int bi = Trace(o, dir, 300f, false, out float t, out int face);
            if (bi < 0) return MenuDawn ? DawnSky * Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(dir.y * 2f)) : SkyNight;
            Surf r = MakeSurf(bi, face, o, dir, t, px, py, false);
            boxes[bi].Shade(ref r);

            if (r.Target != 0 && r.Alpha < 1f)
            {
                // 유리: 벽 두께만큼 지나가서 건너편(안에서는 바깥, 밖에서는 매장 안)을 본다
                if (depth >= 2) return NightCol;
                Vector3 beyond = r.P + dir * (0.3f / Mathf.Max(0.05f, Mathf.Abs(dir.z)) + 0.01f);
                return Vector3.Lerp(FeedTrace(beyond, dir, px, py, depth + 1), r.Albedo, r.Alpha);
            }
            return Vector3.Scale(r.Albedo, Light(r.P, r.N)) + r.Emit;
        }

        /// <summary>색을 CCTV 영상처럼 바꾼다: 흑백, 잡음, 가장자리 어두움. (화면 위에 겹치는 표시나 글자는 없다)</summary>
        static Vector3 CctvLook(Vector3 c, float u, float v, int px, int py, float gain, bool rain)
        {
            // 야간 카메라: 어두운 곳은 밝게 끌어올리되 밝은 곳(간판, 매장 안)은 하얗게 날아가지 않게 누른다
            float lum = Mathf.Clamp01(c.x * 0.3f + c.y * 0.59f + c.z * 0.11f);
            float g = lum * gain / (1f + (gain - 1f) * lum);
            g = Mathf.Pow(g, 1.15f) * 0.9f + (Hash(px, py, 141) - 0.5f) * 0.035f;
            if (rain && Hash(px + py / 3, py / 6, 311) < 0.03f) g += 0.1f;                             // 렌즈 앞 빗줄기
            g *= 1f - 0.3f * Mathf.Pow(Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f, 4f);
            return new Vector3(0.74f, 0.82f, 0.86f) * Mathf.Max(0f, g);
        }

        // ── 게임에서 쓰는 CCTV 그림 ───────────────────────────
        static readonly Cam[] Cams = { Cam1, Cam2, Cam3 };

        public const string MenuBackgroundName = "menu_cctv3";

        /// <summary>
        /// 메인 화면용 카메라: CCTV 3번과 같은 쪽(주차장)에서 보되 조금 낮게 달아서,
        /// 문 정면 쪽 낮은 자리라서 문 유리 너머로 계산대 뒤에 선 사람의 머리부터 계산대 위까지 보인다
        /// (3번은 높고 비스듬해서 창 위 벽이나 창에 붙은 전단에 가린다).
        /// </summary>
        static readonly Cam CamMenu = new Cam { Pos = new Vector3(1.0f, 2.3f, 15.5f), Target = new Vector3(0.1f, 1.3f, 8.4f), TanW = 0.62f, TanH = 0.35f, Gain = 3f, Outdoor = true };
        /// <summary>CamUV·SeesThrough에서 메인 화면 카메라를 가리키는 번호(채널 0~2 다음).</summary>
        public const int MenuCam = 3;
        static Cam AnyCam(int k) => k == MenuCam ? CamMenu : Cams[k];

        /// <summary>
        /// 채널 k 영상의 (u,v)에 비친 시선이 매장 안 깊이 targetZ까지 막힘 없이 닿는지.
        /// 유리(창·문 유리)는 비쳐 보이고, 벽·문틀·계산대처럼 불투명한 것에 먼저 닿으면 가려진다.
        /// 나중에 덧그리는 사람(CCTV 속 주인공)을 장면에 맞게 가릴 때 쓴다.
        /// </summary>
        public static bool SeesThrough(int k, float u, float v, float targetZ)
        {
            if (boxes.Count == 0) BuildScene();
            Cam cam = AnyCam(k);
            Vector3 fwd = (cam.Target - cam.Pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            Vector3 dir = (fwd + right * ((u - 0.5f) * 2f * cam.TanW) + up * ((v - 0.5f) * 2f * cam.TanH)).normalized;
            if (Mathf.Abs(dir.z) < 1e-4f) return false;
            float t = (targetZ - cam.Pos.z) / dir.z;
            return t > 0f && ClearTo(cam.Pos, dir, t, 0);
        }

        static bool ClearTo(Vector3 o, Vector3 dir, float tTarget, int depth)
        {
            if (tTarget <= 0f) return true;
            int bi = Trace(o, dir, tTarget, false, out float t, out int face);
            if (bi < 0) return true;
            if (depth >= 3) return false;
            Surf r = MakeSurf(bi, face, o, dir, t, 0, 0, false);
            boxes[bi].Shade(ref r);
            if (r.Target != 0 && r.Alpha < 1f)
            {
                // 유리: 벽 두께만큼 지나가서 계속 본다
                float step = 0.3f / Mathf.Max(0.05f, Mathf.Abs(dir.z)) + 0.01f;
                return ClearTo(o + dir * (t + step), dir, tTarget - t - step, depth + 1);
            }
            return false;
        }

        /// <summary>
        /// 메인 화면 배경: CCTV 3번(주차장 가로등 기둥에서 비 오는 매장 정면)을 화면 크기(640x360) 녹화 영상으로.
        /// 매장 안에 선 주인공·창밖 형체·노이즈·빗줄기·REC 표시는 게임에서 위에 겹친다.
        /// </summary>
        public static PixelCanvas RenderMenuBackground()
        {
            BuildScene();
            var canvas = new PixelCanvas(new RectInt(-ViewW / 2, -ViewH / 2, ViewW, ViewH));
            Cam cam = CamMenu;
            for (int y = -ViewH / 2; y < ViewH / 2; y++)
                for (int x = -ViewW / 2; x < ViewW / 2; x++)
                {
                    float u = (x + ViewW / 2 + 0.5f) / ViewW, v = (y + ViewH / 2 + 0.5f) / ViewH;
                    Vector3 e = CctvLook(FeedColor(cam, u, v, x, y), u, v, x, y, cam.Gain, cam.Outdoor && !MenuDawn);
                    canvas.Put(x, y, PixelPalette.Quantize(e, x, y));
                }
            return canvas;
        }

        /// <summary>
        /// 매장 안의 한 점이 채널 k 영상의 어디에 찍히는지(0~1, u는 왼→오, v는 아래→위).
        /// 손님처럼 나중에 덧붙이는 그림을 CCTV 화면 위에 정확히 올릴 때 쓴다.
        /// </summary>
        public static Vector2 CamUV(int k, Vector3 p)
        {
            Cam cam = AnyCam(k);
            Vector3 fwd = (cam.Target - cam.Pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            Vector3 d = p - cam.Pos;
            float a = Vector3.Dot(d, fwd);
            return new Vector2(0.5f + Vector3.Dot(d, right) / a / (2f * cam.TanW),
                               0.5f + Vector3.Dot(d, up) / a / (2f * cam.TanH));
        }

        // ── 메인 화면에 덧그리는 것(창밖 괴물)을 배경과 같은 빛·같은 영상 색으로 ──
        /// <summary>메인 화면 카메라 위치와 화면 (u,v)로 나가는 시선 방향.</summary>
        public static Ray MenuRay(float u, float v)
        {
            Cam cam = CamMenu;
            Vector3 fwd = (cam.Target - cam.Pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            Vector3 dir = (fwd + right * ((u - 0.5f) * 2f * cam.TanW) + up * ((v - 0.5f) * 2f * cam.TanH)).normalized;
            return new Ray(cam.Pos, dir);
        }

        /// <summary>메인 화면 카메라의 오른쪽·위·앞 방향(화면에서 본 면 방향을 장면 방향으로 바꿀 때).</summary>
        public static void MenuBasis(out Vector3 right, out Vector3 up, out Vector3 fwd)
        {
            Cam cam = CamMenu;
            fwd = (cam.Target - cam.Pos).normalized;
            right = Vector3.Cross(Vector3.up, fwd).normalized;
            up = Vector3.Cross(fwd, right);
        }

        /// <summary>장면의 한 점(p, 면 방향 n)이 받는 빛. 배경을 그릴 때와 같은 계산(주변광·구석 어둠·등·그림자).</summary>
        public static Vector3 SceneLight(Vector3 p, Vector3 n)
        {
            if (boxes.Count == 0) BuildScene();
            bool was = inFeed;
            inFeed = true;
            Vector3 c = Light(p, n);
            inFeed = was;
            return c;
        }

        /// <summary>장면 색을 메인 화면 픽셀(px,py: 화면 가운데가 0) 색으로: 배경과 같은 밝기 곡선·잡음·색 단계.</summary>
        public static Color32 MenuLook(Vector3 c, int px, int py)
        {
            float u = (px + ViewW / 2 + 0.5f) / ViewW, v = (py + ViewH / 2 + 0.5f) / ViewH;
            return PixelPalette.Quantize(CctvLook(c, u, v, px, py, CamMenu.Gain, CamMenu.Outdoor && !MenuDawn), px, py);
        }

        /// <summary>매장 밖을 비추는 등(처마 밑 등·가로등): 위치와 세기. 덧그린 것의 그림자를 바닥·벽에 드리울 때.</summary>
        public static List<(Vector3 pos, float power)> OutdoorLights()
        {
            if (boxes.Count == 0) BuildScene();
            var list = new List<(Vector3, float)>();
            foreach (var l in lights)
                if (l.OutdoorOnly) list.Add((l.P, Mathf.Max(l.Col.x, Mathf.Max(l.Col.y, l.Col.z))));
            return list;
        }

        /// <summary>장면 상자들에 시선이 막히는지(그림자 판정용): o에서 dir로 dist 안에 닿는 것이 있나.</summary>
        public static bool Blocked(Vector3 o, Vector3 dir, float dist)
        {
            if (boxes.Count == 0) BuildScene();
            return Trace(o, dir, dist, true, out _, out _) >= 0;
        }

        /// <summary>CCTV 크게 보기 화면에서 영상이 놓이는 자리(화면 중심 기준 픽셀).</summary>
        public static RectInt PanelFeedRect => FeedRect;

        /// <summary>계산대 위 작은 모니터에 채널 k의 영상을 띄우는 레이어 이름.</summary>
        public static string MonitorLayerName(int k) => $"monitor_cctv{k + 1}";
        /// <summary>CCTV를 크게 볼 때 쓰는 채널 k의 영상 이름.</summary>
        public static string FullFeedName(int k) => $"cctv_full{k + 1}";
        public const string FrameName = "cctv_frame";
        public const string PrevHoverName = "cctv_btn_prev_hover";
        public const string NextHoverName = "cctv_btn_next_hover";
        public const string DotOnName = "cctv_dot_on";
        public const string CloseHoverName = "cctv_btn_close_hover";
        public const int ChannelCount = 3;
        /// <summary>채널 표시 점 사이 간격(px).</summary>
        public const int DotStep = 11;

        // 크게 보기 화면의 배치(화면 중심이 0,0). 영상 544x306, 그 아래 띠에 이전/다음 버튼.
        static readonly RectInt FeedRect = new RectInt(-272, -141, 544, 306);
        static readonly RectInt PrevRect = new RectInt(-276, -166, 40, 20);
        static readonly RectInt NextRect = new RectInt(236, -166, 40, 20);
        static readonly RectInt DotRect = new RectInt(-14, -159, 6, 6);
        // 닫기(X) 버튼: 영상 왼쪽 위, 예전에 빨간 녹화 표시가 있던 자리. 빨간 네모에 흰 X라 화면 표시처럼 보인다.
        static readonly RectInt CloseRect = new RectInt(-263, 136, 18, 18);

        /// <summary>CCTV 크게 보기에 필요한 그림들: 채널별 영상, 모니터 테두리, 버튼 강조, 채널 표시 점.</summary>
        static List<Layer> GenerateCctvUi()
        {
            var list = new List<Layer>();

            for (int k = 0; k < Cams.Length; k++)
            {
                var feed = new Layer(FullFeedName(k), FeedRect, 200, Group.Ui);
                RectInt r = FeedRect;
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        float u = (x - r.x + 0.5f) / r.width, v = (y - r.y + 0.5f) / r.height;
                        Vector3 e = CctvLook(FeedColor(Cams[k], u, v, x, y), u, v, x, y, Cams[k].Gain, Cams[k].Outdoor);
                        feed.Canvas.Put(x, y, PixelPalette.Quantize(e, x, y));
                    }
                list.Add(feed);
            }

            // 모니터 테두리(화면 전체 크기). 영상 자리는 뚫려 있고, 바깥은 반투명하게 어둡다.
            var frame = new Layer(FrameName, new RectInt(-ViewW / 2, -ViewH / 2, ViewW, ViewH), 201, Group.Ui);
            PixelCanvas c = frame.Canvas;
            Color32 bezel = Pal.Hex("#232A30"), bezelHi = Pal.Hex("#333D42"), line = Pal.Hex("#0C0F14");
            c.Fill(-ViewW / 2, -ViewH / 2, ViewW / 2, ViewH / 2, new Color32(4, 6, 10, 205));
            c.Fill(-30, -ViewH / 2, 30, -170, Pal.Hex("#161B21"));                  // 받침대 목
            for (int y = -170; y < 172; y++)
                for (int x = -282; x < 282; x++) c.Put(x, y, bezel);
            c.Fill(-282, 171, 282, 172, bezelHi);
            c.Fill(-282, -170, 282, -169, line);
            c.Outline(FeedRect.xMin - 1, FeedRect.yMin - 1, FeedRect.xMax + 1, FeedRect.yMax + 1, line);
            for (int y = FeedRect.yMin; y < FeedRect.yMax; y++)
                for (int x = FeedRect.xMin; x < FeedRect.xMax; x++) c.Put(x, y, default); // 영상 자리
            DrawButton(c, PrevRect, true, bezelHi, Pal.Hex("#465254"), Pal.Hex("#B0B6A5"));
            DrawButton(c, NextRect, false, bezelHi, Pal.Hex("#465254"), Pal.Hex("#B0B6A5"));
            for (int k = 0; k < ChannelCount; k++)                                  // 채널 표시 점(꺼진 상태)
                c.Fill(DotRect.x + k * DotStep, DotRect.y, DotRect.x + k * DotStep + DotRect.width, DotRect.yMax, Pal.Hex("#465254"));
            c.Fill(218, -158, 222, -154, Pal.Hex("#8FD4FF"));                       // 전원 표시등
            DrawCloseButton(c, CloseRect, Pal.Hex("#A8433B"), Pal.Hex("#6B2A2A"), Pal.Hex("#F3F1DF"));
            list.Add(frame);

            // 닫기 버튼에 마우스를 올렸을 때: 더 밝은 빨강
            var close = new Layer(CloseHoverName, CloseRect, 202, Group.Ui);
            DrawCloseButton(close.Canvas, CloseRect, Pal.Hex("#E5372E"), Pal.Hex("#F3F1DF"), Pal.Hex("#FFFBE8"));
            list.Add(close);

            // 버튼에 마우스를 올렸을 때 덮어 그리는 밝은 버튼
            var prev = new Layer(PrevHoverName, PrevRect, 202, Group.Ui);
            DrawButton(prev.Canvas, PrevRect, true, Pal.Hex("#465254"), Pal.Hex("#939C91"), Pal.Hex("#F3F1DF"));
            list.Add(prev);
            var next = new Layer(NextHoverName, NextRect, 202, Group.Ui);
            DrawButton(next.Canvas, NextRect, false, Pal.Hex("#465254"), Pal.Hex("#939C91"), Pal.Hex("#F3F1DF"));
            list.Add(next);

            // 지금 보고 있는 채널을 가리키는 초록 점(게임에서 옆으로 옮긴다)
            var dot = new Layer(DotOnName, DotRect, 202, Group.Ui);
            dot.Canvas.Fill(DotRect.xMin, DotRect.yMin, DotRect.xMax, DotRect.yMax, Pal.Hex("#3FE07A"));
            list.Add(dot);
            return list;
        }

        /// <summary>세모 화살표가 그려진 네모 버튼. left면 ◀(이전), 아니면 ▶(다음).</summary>
        internal static void DrawButton(PixelCanvas c, RectInt r, bool left, Color32 fill, Color32 edge, Color32 arrow)
        {
            c.Fill(r.xMin, r.yMin, r.xMax, r.yMax, fill);
            c.Outline(r.xMin, r.yMin, r.xMax, r.yMax, edge);
            int cx = r.x + r.width / 2, cy = r.y + r.height / 2;
            for (int i = 0; i < 6; i++)
            {
                int x = left ? cx - 3 + i : cx + 3 - i;
                for (int dy = -i; dy <= i; dy++) c.Set(x, cy + dy, arrow);
            }
        }

        /// <summary>X 표시가 그려진 네모 버튼(닫기).</summary>
        internal static void DrawCloseButton(PixelCanvas c, RectInt r, Color32 fill, Color32 edge, Color32 mark)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++) c.Put(x, y, fill); // 반투명 배경 위에 그리므로 덮어쓴다
            c.Outline(r.xMin, r.yMin, r.xMax, r.yMax, edge);
            int cx = r.x + r.width / 2, cy = r.y + r.height / 2;
            for (int i = -4; i <= 3; i++)
            {
                c.Put(cx + i, cy + i, mark); c.Put(cx + i + 1, cy + i, mark);
                c.Put(cx + i, cy - 1 - i, mark); c.Put(cx + i + 1, cy - 1 - i, mark);
            }
        }

        // ── 채널 화면 미리보기 ────────────────────────────────
        /// <summary>CCTV 1·2·3번 채널의 빈 배경 화면(사람 없음)을 PNG로 저장한다.</summary>
        public static void RenderCctvChannels(string dir)
        {
            Directory.CreateDirectory(dir);
            BuildScene();
            SaveFeed(Cam1, Path.Combine(dir, "cctv1.png"));
            SaveFeed(Cam2, Path.Combine(dir, "cctv2.png"));
            SaveFeed(Cam3, Path.Combine(dir, "cctv3.png"));
        }

        static void SaveFeed(in Cam cam, string path)
        {
            const int w = 384, h = 216;
            var canvas = new PixelCanvas(new RectInt(0, 0, w, h));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Vector3 e = CctvLook(FeedColor(cam, u, v, x, y), u, v, x, y, cam.Gain, cam.Outdoor);
                    canvas.Put(x, y, PixelPalette.Quantize(e, x, y));
                }
            canvas.SavePng(path, 3);
        }
    }
}
