using System.Collections.Generic;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 정면 편의점 임시 픽셀아트 생성기 (v2).
    ///
    /// 매장을 상자(Box)들로 이루어진 간단한 3D 장면으로 만들고, 계산대 안쪽 눈높이에서
    /// 광선을 쏴서 조명·그림자·바닥 반사까지 계산한 뒤, 고정 팔레트로 줄여 픽셀아트로 만든다.
    ///  - 1 픽셀 = 1 유닛, 한 화면은 480x270, 둘러볼 수 있는 전체 그림은 864x573.
    ///  - 평상시(NORMAL): 실내는 밝은 형광등, 창밖은 어두운 밤.
    ///  - 형광등·문짝·유리·계산대는 따로 레이어로 뽑아 나중에 끄거나 움직일 수 있다.
    /// </summary>
    public static partial class StoreArtGenerator
    {
        // ── 화면/월드(픽셀) ───────────────────────────────────
        public const int ViewW = 480, ViewH = 270;
        public static readonly RectInt WorldRect = new RectInt(-432, -330, 864, 573);
        /// <summary>마우스가 화면 중앙일 때의 시선(살짝 아래를 본다).</summary>
        public static readonly Vector2 Home = new Vector2(0f, -40f);

        // ── 가상 카메라와 매장(미터) ──────────────────────────
        const float F = 450f;      // 초점거리(px)
        const float VPy = 8f;      // 소실점 높이(px)
        const float Eye = 1.6f;    // 눈높이
        const float HalfW = 3.6f;  // 매장 폭의 절반
        const float CeilH = 2.8f;  // 천장 높이
        const float BackZ = 8f;    // 정면 유리벽까지 거리
        const float CY = 0.92f;    // 계산대 상판 높이
        const float CZ = 1.85f;    // 계산대 끝(손님 쪽)

        static readonly Rect WinL = Rect.MinMaxRect(-2.55f, 0.7f, -1.05f, 2.5f);
        static readonly Rect WinR = Rect.MinMaxRect(1.05f, 0.7f, 2.55f, 2.5f);
        const float DoorHalf = 0.85f, DoorTop = 2.4f;
        static readonly Rect Staff = Rect.MinMaxRect(-3.45f, 0f, -2.75f, 2.05f);

        static readonly float[] TubeZ = { 3f, 5f, 7f };
        static readonly Vector2[] TubeX = { new Vector2(-2.2f, -0.9f), new Vector2(0.9f, 2.2f) };

        // ── 조명 세기 ────────────────────────────────────────
        const float TubeI = 1.2f;
        const float AmbUp = 0.42f, AmbSide = 0.63f, AmbDown = 0.85f;
        const float AoDist = 0.45f;
        static readonly Vector3 LightCol = new Vector3(0.97f, 1f, 0.92f);   // 살짝 푸르스름한 형광등
        static readonly Vector3 AmbientCol = new Vector3(0.94f, 0.99f, 0.95f);
        static readonly Vector3 NightCol = new Vector3(0.03f, 0.04f, 0.065f);

        public enum Group { Outside, Interior, Lights, Foreground }

        public class Layer
        {
            public string Name;
            public PixelCanvas Canvas;
            public int Order;
            public Group Group;
            public Layer(string name, RectInt rect, int order, Group group)
            {
                Name = name; Canvas = new PixelCanvas(rect); Order = order; Group = group;
            }
        }

        // ── 장면 ─────────────────────────────────────────────
        delegate void Shader(ref Surf s);

        class Box
        {
            public string Name;
            public Vector3 Min, Max;
            public Shader Shade;
            public bool Fg;             // 계산대 전경 레이어에 속함
            public float Outline;       // 외곽선 세기(0이면 없음)
            public bool Shadow = true;  // 그림자를 드리우는지
            public int Tube = -1;       // 형광등 번호
        }

        const int TGlass = 1, TDoorL = 2, TDoorR = 3;

        struct Surf
        {
            // 입력
            public Box B;
            public int Face;        // 0:-X 1:+X 2:-Y 3:+Y 4:-Z 5:+Z
            public Vector3 P, N, Rd;
            public Vector3 Foot;    // 픽셀 하나가 덮는 월드 길이(축별) → 먼 곳의 선을 뭉개지 않게 하는 용도
            public int Px, Py;
            // 출력
            public Vector3 Albedo, Emit;
            public float Alpha, Gloss;
            public int Target;      // 0: 자기 레이어, TGlass/TDoorL/TDoorR: 유리·문짝 레이어

            public Vector3 L => P - B.Min;      // 상자 기준 좌표
            public Vector3 S => B.Max - B.Min;  // 상자 크기
        }

        struct LightS { public Vector3 P, Col; public bool Down; }

        static readonly List<Box> boxes = new List<Box>();
        static readonly List<LightS> lights = new List<LightS>();
        static float[] bb;
        static bool[] bSh;
        static int bN;

        class Buf
        {
            public readonly Vector3[] C;
            public readonly float[] A, Z;
            public readonly int[] Id;
            public readonly byte[] Fc;
            public Buf(int n)
            {
                C = new Vector3[n]; A = new float[n]; Z = new float[n]; Id = new int[n]; Fc = new byte[n];
                for (int i = 0; i < n; i++) Id[i] = -1;
            }
        }

        // ── 진입점 ────────────────────────────────────────────
        public static List<Layer> Generate()
        {
            BuildScene();

            RectInt w = WorldRect;
            int n = w.width * w.height;
            var interior = new Buf(n);
            var fg = new Buf(n);
            var glassC = new Vector3[n]; var glassA = new float[n];
            var doorLC = new Vector3[n]; var doorLA = new float[n];
            var doorRC = new Vector3[n]; var doorRA = new float[n];
            var tubeAt = new int[n]; var tubeEmit = new Vector3[n];
            for (int i = 0; i < n; i++) tubeAt[i] = -1;

            var eye = new Vector3(0f, Eye, 0f);
            for (int y = w.yMin; y < w.yMax; y++)
                for (int x = w.xMin; x < w.xMax; x++)
                {
                    int i = (y - w.y) * w.width + (x - w.x);
                    Vector3 d = Dir(x + 0.5f, y + 0.5f);
                    int bi = Trace(eye, d, 1e6f, false, out float t, out int face);
                    if (bi < 0) continue;
                    Box b = boxes[bi];
                    Surf s = MakeSurf(bi, face, eye, d, t, x, y, true);
                    b.Shade(ref s);

                    if (s.Target != 0)
                    {
                        // 불투명한 부분(문틀, 포스터)은 조명을 받고, 유리는 얇은 덧칠로만 남긴다
                        Vector3 oc = s.Alpha >= 1f ? Vector3.Scale(s.Albedo, Light(s.P, s.N)) + s.Emit : s.Albedo;
                        if (s.Target == TGlass) { glassC[i] = oc; glassA[i] = s.Alpha; }
                        else if (s.Target == TDoorL) { doorLC[i] = oc; doorLA[i] = s.Alpha; }
                        else { doorRC[i] = oc; doorRA[i] = s.Alpha; }
                        continue;
                    }

                    Vector3 c = Vector3.Scale(s.Albedo, Light(s.P, s.N));
                    if (b.Tube >= 0) { tubeAt[i] = b.Tube; tubeEmit[i] = s.Emit; }   // 실내 레이어에는 꺼진 모습
                    else c += s.Emit;
                    if (s.Gloss > 0f) c = Reflect(ref s, c);

                    Buf buf = b.Fg ? fg : interior;
                    buf.C[i] = c; buf.A[i] = 1f; buf.Id[i] = bi; buf.Fc[i] = (byte)face; buf.Z[i] = t;
                }

            Outline(interior, false);
            Outline(fg, true);
            PostDetails(interior);

            var layers = new List<Layer> { PaintOutside(), PaintRain() };
            void AddLayer(Layer l) { if (l != null) layers.Add(l); }
            AddLayer(ToLayer("door_leaf_l", doorLC, doorLA, 5, Group.Interior));
            AddLayer(ToLayer("door_leaf_r", doorRC, doorRA, 5, Group.Interior));
            AddLayer(ToLayer("interior", interior.C, interior.A, 10, Group.Interior));
            AddLayer(ToLayer("window_glass", glassC, glassA, 11, Group.Interior));
            for (int k = 0; k < TubeZ.Length * TubeX.Length; k++) AddLayer(TubeLayer(k, tubeAt, tubeEmit));
            AddLayer(ToLayer("counter", fg.C, fg.A, 50, Group.Foreground));
            return layers;
        }

        /// <summary>모든 레이어를 합친 미리보기(검증용).</summary>
        public static PixelCanvas Composite(List<Layer> layers)
        {
            var all = new PixelCanvas(WorldRect);
            all.Fill(WorldRect.xMin, WorldRect.yMin, WorldRect.xMax, WorldRect.yMax, new Color32(0, 0, 0, 255));
            var sorted = new List<Layer>(layers);
            sorted.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var l in sorted) all.Composite(l.Canvas, Vector2Int.zero);
            return all;
        }

        // ── 광선 ─────────────────────────────────────────────
        static Vector3 Dir(float sx, float sy) => new Vector3(sx / F, (sy - VPy) / F, 1f);

        static Vector2 Project(Vector3 p) => new Vector2(F * p.x / p.z, VPy + F * (p.y - Eye) / p.z);

        /// <summary>가장 먼저 닿는 상자. 없으면 -1.</summary>
        static int Trace(Vector3 o, Vector3 d, float tMax, bool shadowOnly, out float tHit, out int face)
        {
            float ox = o.x, oy = o.y, oz = o.z, dx = d.x, dy = d.y, dz = d.z;
            bool px = Mathf.Abs(dx) < 1e-8f, py = Mathf.Abs(dy) < 1e-8f, pz = Mathf.Abs(dz) < 1e-8f;
            float ix = px ? 0f : 1f / dx, iy = py ? 0f : 1f / dy, iz = pz ? 0f : 1f / dz;
            int best = -1, bestAx = 0;
            float bestT = tMax;
            float[] b = bb;
            for (int i = 0; i < bN; i++)
            {
                if (shadowOnly && !bSh[i]) continue;
                int k = i * 6;
                float tmin = 1e-5f, tmax = bestT, t1, t2;
                int ax = -1;
                if (px) { if (ox < b[k] || ox > b[k + 3]) continue; }
                else
                {
                    t1 = (b[k] - ox) * ix; t2 = (b[k + 3] - ox) * ix;
                    if (t1 > t2) { float tt = t1; t1 = t2; t2 = tt; }
                    if (t1 > tmin) { tmin = t1; ax = 0; }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) continue;
                }
                if (py) { if (oy < b[k + 1] || oy > b[k + 4]) continue; }
                else
                {
                    t1 = (b[k + 1] - oy) * iy; t2 = (b[k + 4] - oy) * iy;
                    if (t1 > t2) { float tt = t1; t1 = t2; t2 = tt; }
                    if (t1 > tmin) { tmin = t1; ax = 1; }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) continue;
                }
                if (pz) { if (oz < b[k + 2] || oz > b[k + 5]) continue; }
                else
                {
                    t1 = (b[k + 2] - oz) * iz; t2 = (b[k + 5] - oz) * iz;
                    if (t1 > t2) { float tt = t1; t1 = t2; t2 = tt; }
                    if (t1 > tmin) { tmin = t1; ax = 2; }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) continue;
                }
                if (ax < 0) continue; // 출발점이 상자 안
                bestT = tmin; best = i; bestAx = ax;
            }
            tHit = bestT;
            float da = bestAx == 0 ? dx : bestAx == 1 ? dy : dz;
            face = bestAx * 2 + (da > 0f ? 0 : 1);
            return best;
        }

        static Surf MakeSurf(int bi, int face, Vector3 o, Vector3 d, float t, int px, int py, bool primary)
        {
            Box b = boxes[bi];
            int ax = face >> 1;
            bool pos = (face & 1) == 1;
            float plane = pos ? b.Max[ax] : b.Min[ax];
            Vector3 n = Vector3.zero; n[ax] = pos ? 1f : -1f;
            Vector3 p = o + d * t; p[ax] = plane;

            var s = new Surf { B = b, Face = face, P = p, N = n, Rd = d, Px = px, Py = py, Alpha = 1f };
            if (primary)
            {
                Vector3 d1 = Dir(px + 1.5f, py + 0.5f), d2 = Dir(px + 0.5f, py + 1.5f);
                Vector3 p1 = o + d1 * ((plane - o[ax]) / d1[ax]);
                Vector3 p2 = o + d2 * ((plane - o[ax]) / d2[ax]);
                Vector3 f = Abs(p1 - p) + Abs(p2 - p);
                s.Foot = new Vector3(Cap(f.x), Cap(f.y), Cap(f.z));
            }
            else s.Foot = new Vector3(0.03f, 0.03f, 0.03f);
            return s;
        }

        static float Cap(float v) => v < 1f ? v : 1f; // NaN/무한대도 1로
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ── 조명 ─────────────────────────────────────────────
        static readonly Vector2[] AoDir =
        {
            new Vector2(0.7f, 0f), new Vector2(0.35f, 0.606f), new Vector2(-0.35f, 0.606f),
            new Vector2(-0.7f, 0f), new Vector2(-0.35f, -0.606f), new Vector2(0.35f, -0.606f),
        };

        /// <summary>한 점이 받는 빛: 주변광(구석은 어둡게) + 형광등 직사광(그림자 포함).</summary>
        static Vector3 Light(Vector3 p, Vector3 n)
        {
            Vector3 o = p + n * 0.004f;
            Vector3 u = Mathf.Abs(n.x) > 0.5f ? Vector3.up : Vector3.right;
            Vector3 v = Vector3.Cross(n, u);

            float occ = 0f;
            for (int i = 0; i < AoDir.Length; i++)
            {
                Vector3 dir = (n * 0.7f + u * AoDir[i].x + v * AoDir[i].y).normalized;
                if (Trace(o, dir, AoDist, false, out float t, out _) >= 0) occ += 1f - t / AoDist;
            }
            float ao = 1f - 0.7f * occ / AoDir.Length;
            float amb = n.y > 0.5f ? AmbUp : n.y < -0.5f ? AmbDown : AmbSide;
            Vector3 sum = AmbientCol * (amb * ao);

            for (int i = 0; i < lights.Count; i++)
            {
                LightS l = lights[i];
                Vector3 to = l.P - o;
                float d2 = to.sqrMagnitude;
                if (d2 < 1e-4f) continue;
                float dist = Mathf.Sqrt(d2);
                Vector3 dir = to / dist;
                float ndl = Vector3.Dot(n, dir);
                if (ndl <= 0f) continue;
                float e = ndl / d2;
                if (l.Down) { if (dir.y <= 0f) continue; e *= dir.y; }
                float strength = Mathf.Max(l.Col.x, Mathf.Max(l.Col.y, l.Col.z)) * e;
                if (strength < 0.004f) continue;
                if (Trace(o, dir, dist - 0.03f, true, out _, out _) >= 0) continue; // 그림자
                sum += l.Col * e;
            }
            return sum;
        }

        /// <summary>광택 있는 면(바닥·상판)에 비친 상을 섞는다. 비스듬히 볼수록 강하다.</summary>
        static Vector3 Reflect(ref Surf s, Vector3 c)
        {
            Vector3 dn = s.Rd.normalized;
            float cos = Mathf.Abs(Vector3.Dot(dn, s.N));
            float k = Mathf.Min(0.6f, s.Gloss * (0.4f + 2.5f * Mathf.Pow(1f - cos, 3f)));
            Vector3 rd = Vector3.Reflect(dn, s.N);
            Vector3 o = s.P + s.N * 0.004f;
            Vector3 rc = NightCol;
            int bi = Trace(o, rd, 60f, false, out float t, out int face);
            if (bi >= 0)
            {
                Surf r = MakeSurf(bi, face, o, rd, t, s.Px, s.Py, false);
                boxes[bi].Shade(ref r);
                if (r.Target == 0) rc = r.Albedo * 0.62f + r.Emit;
                else if (r.Alpha >= 1f) rc = Vector3.Lerp(NightCol, r.Albedo * 0.62f, 0.35f); // 문틀·안전띠는 흐릿하게만
            }
            return c * (1f - k * 0.6f) + rc * k;
        }

        // ── 외곽선 ───────────────────────────────────────────
        /// <summary>물체의 윤곽에 어두운 선, 윗면의 앞 모서리에 밝은 선을 넣는다.</summary>
        static void Outline(Buf b, bool edgeOnEmpty)
        {
            int w = WorldRect.width, h = WorldRect.height;
            var mul = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    mul[i] = 1f;
                    int id = b.Id[i];
                    if (id < 0) continue;
                    float ol = boxes[id].Outline;
                    if (ol <= 0f) continue;
                    bool edge = false, crease = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        int nid = b.Id[j];
                        if (nid < 0) { if (edgeOnEmpty) edge = true; continue; }
                        if (nid != id) { if (b.Z[i] < b.Z[j] - 0.01f || boxes[nid].Outline <= 0f) edge = true; }
                        else if (k == 3 && b.Fc[i] == 3 && b.Fc[j] != 3) crease = true;
                    }
                    if (edge) mul[i] = 1f - 0.62f * ol;
                    else if (crease) mul[i] = 1f + 0.18f * ol;
                }
            for (int i = 0; i < mul.Length; i++) b.C[i] *= mul[i];
        }

        // ── 레이어 만들기 ─────────────────────────────────────
        static Layer ToLayer(string name, Vector3[] c, float[] a, int order, Group g)
        {
            RectInt w = WorldRect;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int y = 0; y < w.height; y++)
                for (int x = 0; x < w.width; x++)
                    if (a[y * w.width + x] > 0f)
                    {
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
            if (x1 < x0) return null;

            var layer = new Layer(name, new RectInt(w.x + x0, w.y + y0, x1 - x0 + 1, y1 - y0 + 1), order, g);
            RectInt r = layer.Canvas.Rect;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int i = (y - w.y) * w.width + (x - w.x);
                    if (a[i] <= 0f) continue;
                    if (a[i] >= 0.999f) layer.Canvas.Put(x, y, PixelPalette.Quantize(c[i], x, y));
                    else layer.Canvas.Put(x, y, Raw(c[i], a[i]));
                }
            return layer;
        }

        static Color32 Raw(Vector3 c, float a) => new Color32(
            (byte)(Mathf.Clamp01(c.x) * 255f), (byte)(Mathf.Clamp01(c.y) * 255f),
            (byte)(Mathf.Clamp01(c.z) * 255f), (byte)(Mathf.Clamp01(a) * 255f));

        /// <summary>켜진 형광등 하나(빛 번짐 포함). 숨기면 실내 레이어의 꺼진 등이 보인다.</summary>
        static Layer TubeLayer(int k, int[] tubeAt, Vector3[] tubeEmit)
        {
            RectInt w = WorldRect;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int y = 0; y < w.height; y++)
                for (int x = 0; x < w.width; x++)
                    if (tubeAt[y * w.width + x] == k)
                    {
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
            if (x1 < x0) return null;

            int rad = Mathf.Clamp(Mathf.RoundToInt((y1 - y0 + 1) * 0.7f), 3, 9);
            x0 = Mathf.Max(0, x0 - rad); y0 = Mathf.Max(0, y0 - rad);
            x1 = Mathf.Min(w.width - 1, x1 + rad); y1 = Mathf.Min(w.height - 1, y1 + rad);
            var layer = new Layer($"light_tube_{k}", new RectInt(w.x + x0, w.y + y0, x1 - x0 + 1, y1 - y0 + 1), 30, Group.Lights);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * w.width + x;
                    if (tubeAt[i] == k)
                    {
                        layer.Canvas.Put(w.x + x, w.y + y, PixelPalette.Quantize(tubeEmit[i], x, y, 0f));
                        continue;
                    }
                    // 가장 가까운 등 픽셀까지의 거리 → 계단식 빛 번짐
                    float best = float.MaxValue;
                    for (int yy = Mathf.Max(y0, y - rad); yy <= Mathf.Min(y1, y + rad); yy++)
                        for (int xx = Mathf.Max(x0, x - rad); xx <= Mathf.Min(x1, x + rad); xx++)
                            if (tubeAt[yy * w.width + xx] == k)
                            {
                                float dd = (xx - x) * (xx - x) + (yy - y) * (yy - y);
                                if (dd < best) best = dd;
                            }
                    float dist = Mathf.Sqrt(best) / rad;
                    if (dist > 1f) continue;
                    byte alpha = dist < 0.34f ? (byte)78 : dist < 0.67f ? (byte)40 : (byte)18;
                    layer.Canvas.Put(w.x + x, w.y + y, new Color32(255, 251, 226, alpha));
                }
            return layer;
        }

        // ── 노이즈/무늬 도우미 ────────────────────────────────
        static int Fl(float v) => Mathf.FloorToInt(v);
        static float Hash(int a, int b, int c = 0) => Pal.Noise(a, b, c);

        static float VNoise(float x, float y, int seed)
        {
            int xi = Fl(x), yi = Fl(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), fx);
            float b = Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float x, float y, int seed) =>
            VNoise(x, y, seed) * 0.6f + VNoise(x * 2.1f, y * 2.1f, seed + 1) * 0.3f + VNoise(x * 4.3f, y * 4.3f, seed + 2) * 0.1f;

        /// <summary>일정 간격의 선에 걸리는 정도(0~1). 멀어서 선이 1픽셀보다 가늘어지면 굵게 그리되 옅게 한다.</summary>
        static float GridLine(float coord, float period, float width, float foot)
        {
            float w = Mathf.Max(width, foot * 0.85f);
            if (w > period * 0.6f) return 0.25f;
            float f = Mathf.Repeat(coord + w * 0.5f, period);
            return f < w ? Mathf.Clamp01(width / w + 0.35f) : 0f;
        }
    }
}
