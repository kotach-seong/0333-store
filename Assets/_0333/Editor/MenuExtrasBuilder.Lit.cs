using System.Collections.Generic;
using UnityEngine;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 메인 화면 괴물을 배경과 같은 빛으로 칠한다(오려 붙인 티가 나지 않게).
    ///  - 몸: 배경을 그린 광선 추적과 같은 등(처마 밑 등·가로등·가게 불빛)으로 명암을 계산하고 같은 CCTV 색 단계로.
    ///    피부·옷은 아주 어둡게(사용자 결정: 실루엣처럼 아주 약간만 보이게).
    ///  - 뒤에서 새는 빛: 밝은 창 앞에 겹친 가장자리에 창 빛이 번진다.
    ///  - 빗속 안개: 멀리(가게 앞) 있을수록 뒤 배경에 살짝 묻힌다.
    ///  - 그림자 그림(따로): 바닥에 닿은 자리의 접지 그림자, 바깥 등이 바닥·벽에 드리우는 그림자,
    ///    젖은 바닥에 비친 어둠, 유리에 손·얼굴을 댄 자리의 김서림.
    /// </summary>
    public static partial class MenuExtrasBuilder
    {
        const float FacadeZ = 8.3f;          // 매장 정면 바깥 면
        const float Albedo = 0.12f;          // 몸 밝기(원본 회색에 곱한다)
        const float ShadowK = 1.5f;          // 등 세기 → 그림자 진하기
        const float MaxLight = 1.1f;         // 몸이 받는 빛의 상한
        const float HazeMax = 0.07f;         // 가장 먼 것이 배경에 묻히는 정도
        const float FogAlpha = 0.34f;        // 유리 김서림 진하기
        static readonly Color32 FogCol = new Color32(196, 206, 212, 255);

        /// <summary>그림 한 장을 화면에 놓은 결과: 픽셀마다 원본 밝기, 하얀 눈, 장면 위치.</summary>
        class Placed
        {
            public int x0, y0, w, h, feetRow;       // 화면 픽셀(가운데 0) 기준 왼쪽 아래
            public bool[] solid, glow;
            public float[] lum;
            public Vector3[] world;
            public Vector2 Pivot => new Vector2((float)(w / 2) / w, (float)feetRow / h);
        }

        static Color32[] LoadPixels(string path)
        {
            Texture2D t = CustomerBuilder.LoadPng(path);
            Color32[] px = t.GetPixels32();
            Object.DestroyImmediate(t);
            return px;
        }

        static Color32 Bg(Color32[] bg, int px, int py)
        {
            px = Mathf.Clamp(px, -W / 2, W / 2 - 1);
            py = Mathf.Clamp(py, -H / 2, H / 2 - 1);
            return bg[(py + H / 2) * W + px + W / 2];
        }

        /// <summary>화면 픽셀 (px,py) 안의 (fx,fy) 자리로 나가는 시선이 깊이 z 평면에 닿는 곳.</summary>
        static Vector3 OnPlane(float px, float py, float z)
        {
            Ray r = G.MenuRay((px + W / 2f) / W, (py + H / 2f) / H);
            float t = (z - r.origin.z) / r.direction.z;
            return r.origin + r.direction * t;
        }

        /// <summary>그림을 CCTV 크기로 줄여 발 자리에 놓는다(flip이면 좌우를 뒤집어 굽는다).</summary>
        static Placed Place(string path, Vector3 feet, float meters, bool flip, bool glowEyes)
        {
            Texture2D src = CustomerBuilder.LoadPng(path);
            int sw = src.width, sh = src.height;
            Color32[] sp = src.GetPixels32();
            Object.DestroyImmediate(src);
            if (flip)
                for (int y = 0; y < sh; y++)
                    System.Array.Reverse(sp, y * sw, sw);
            int top = -1, bottom = -1;
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                    if (sp[y * sw + x].a > 40) { if (bottom < 0) bottom = y; top = y; break; }

            float s = Mathf.Max(0.05f, HeightPx(feet, meters) / (top - bottom + 1));
            var p = new Placed { w = Mathf.Max(1, Mathf.CeilToInt(sw * s)), h = Mathf.Max(1, Mathf.CeilToInt((top + 1) * s)), feetRow = Mathf.FloorToInt(bottom * s) };
            int n = p.w * p.h;
            p.solid = new bool[n]; p.glow = new bool[n]; p.lum = new float[n]; p.world = new Vector3[n];
            Vector2 at = Px(feet);
            p.x0 = (int)at.x - p.w / 2;
            p.y0 = (int)at.y - p.feetRow;
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                {
                    int x0 = Mathf.FloorToInt(x / s), x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt((x + 1) / s));
                    int y0 = Mathf.FloorToInt(y / s), y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt((y + 1) / s));
                    float lum = 0f; int cnt = 0, cover = 0, glow = 0;
                    for (int yy = y0; yy < y1 && yy < sh; yy++)
                        for (int xx = x0; xx < x1 && xx < sw; xx++)
                        {
                            Color32 c = sp[yy * sw + xx];
                            cnt++;
                            if (c.a <= 40) continue;
                            cover++;
                            lum += (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                            if (c.r > 240 && c.g > 240 && c.b > 230) glow++;
                        }
                    bool shine = glowEyes && glow > 0;
                    if (cnt == 0 || (cover * 2 < cnt && !shine)) continue;
                    int i = y * p.w + x;
                    p.solid[i] = true;
                    p.glow[i] = shine;
                    p.lum[i] = cover > 0 ? lum / cover : 1f;
                    p.world[i] = OnPlane(p.x0 + x + 0.5f, p.y0 + y + 0.5f, feet.z);
                }
            return p;
        }

        static bool Solid(Placed p, int x, int y) => x >= 0 && y >= 0 && x < p.w && y < p.h && p.solid[y * p.w + x];

        /// <summary>가장자리까지 거리(1: 바로 옆이 빔, 2: 두 칸 안에 빔, 3: 안쪽).</summary>
        static int EdgeDist(Placed p, int x, int y)
        {
            for (int r = 1; r <= 2; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) <= r && !Solid(p, x + dx, y + dy)) return r;
            return 3;
        }

        /// <summary>
        /// 몸을 장면의 빛으로 칠한 그림. 면 방향은 실루엣 모양에서 어림한다(안쪽은 카메라 쪽, 가장자리는 바깥으로 기운다).
        /// </summary>
        static Sprite LitSprite(Placed p, Vector3 feet, Color32[] bg, string file, float albedo = Albedo, string dir = null)
        {
            G.MenuBasis(out Vector3 right, out Vector3 up, out Vector3 fwd);
            // 실루엣을 부드럽게 흐린 값의 기울기 → 가장자리 면 방향
            float[] blur = new float[p.w * p.h];
            int[] k = { 1, 4, 6, 4, 1 };
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                {
                    float sum = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            if (Solid(p, x + dx, y + dy)) sum += k[dx + 2] * k[dy + 2];
                    blur[y * p.w + x] = sum / 256f;
                }
            float B(int x, int y) => x < 0 || y < 0 || x >= p.w || y >= p.h ? 0f : blur[y * p.w + x];

            Vector3 cam = G.MenuRay(0.5f, 0.5f).origin;
            float dist = Vector3.Distance(cam, feet + Vector3.up * 0.8f);
            float haze = HazeMax * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.5f, 8f, dist));

            var tex = new Texture2D(p.w, p.h, TextureFormat.RGBA32, false);
            var dp = new Color32[p.w * p.h];
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                {
                    int i = y * p.w + x;
                    if (!p.solid[i]) continue;
                    int px = p.x0 + x, py = p.y0 + y;
                    if (p.glow[i]) { dp[i] = G.MenuLook(new Vector3(1.6f, 1.6f, 1.6f), px, py); continue; }

                    float gx = (B(x + 1, y) - B(x - 1, y)) * 0.5f, gy = (B(x, y + 1) - B(x, y - 1)) * 0.5f;
                    float nx = -gx * 2.2f, ny = -gy * 2.2f;
                    float nz = Mathf.Sqrt(Mathf.Max(0.15f, 1f - nx * nx - ny * ny));
                    Vector3 n = (right * nx + up * ny - fwd * nz).normalized;
                    Vector3 lit = G.SceneLight(p.world[i], n);
                    // 등 바로 밑(처마에 매달린 것)이 하얗게 뜨지 않게 받는 빛의 상한을 둔다
                    float peak = Mathf.Max(lit.x, Mathf.Max(lit.y, lit.z));
                    if (peak > MaxLight) lit *= MaxLight / peak;
                    Color32 c = G.MenuLook(lit * (p.lum[i] * albedo), px, py);

                    Color32 behind = Bg(bg, px, py);
                    c = Color32.Lerp(c, behind, haze);

                    // 뒤에서 새는 빛: 밝은 창 앞 가장자리는 창 빛이 몸을 감싼다
                    int d = EdgeDist(p, x, y);
                    if (d <= 2)
                    {
                        float len = Mathf.Sqrt(nx * nx + ny * ny);
                        int ox = len > 0.05f ? Mathf.RoundToInt(nx / len * 2f) : 0, oy = len > 0.05f ? Mathf.RoundToInt(ny / len * 2f) : 2;
                        Color32 o = Bg(bg, px + ox, py + oy);
                        float bl = (0.3f * o.r + 0.59f * o.g + 0.11f * o.b) / 255f;
                        float wrap = (d == 1 ? 0.5f : 0.2f) * bl * bl;
                        c = Color32.Lerp(c, o, wrap);
                    }
                    c.a = 255;
                    dp[i] = c;
                }
            tex.SetPixels32(dp);
            tex.Apply();
            return dir != null ? SaveTo(tex, dir, file, p.Pivot) : Save(tex, file, p.Pivot);
        }

        static bool OnGlass(Vector3 q)
        {
            float ax = Mathf.Abs(q.x);
            if (ax > 1.11f && ax < 2.49f && q.y > 0.76f && q.y < 2.44f) return true;   // 창
            return ax < 0.74f && q.y < 2.22f;                                            // 문
        }

        static void Splat(float[] buf, Vector3 q, float a)
        {
            Vector2 uv = G.CamUV(Cam, q);
            int x = Mathf.FloorToInt(uv.x * W), y = Mathf.FloorToInt(uv.y * H);
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            if (a > buf[i]) buf[i] = a;
        }

        static float[] Blur(float[] src, int rx, int ry)
        {
            var dst = new float[src.Length];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float s = 0f; int n = 0;
                    for (int dy = -ry; dy <= ry; dy++)
                        for (int dx = -rx; dx <= rx; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            s += src[yy * W + xx]; n++;
                        }
                    dst[y * W + x] = s / n;
                }
            return dst;
        }

        /// <summary>
        /// 몸 밖에 생기는 것들(따로 한 장, 몸 바로 뒤에 그린다): 접지 그림자, 바깥 등이 드리우는 그림자(바닥·벽),
        /// 젖은 바닥에 비친 어둠, 유리에 댄 손·얼굴 둘레의 김서림(fogLo~fogHi: 몸 높이 중 유리에 닿은 부분, 음수면 없음).
        /// </summary>
        static Sprite ShadeSprite(Placed p, Vector3 feet, bool grounded, float fogLo, float fogHi, string file, out Vector2 pos)
        {
            float[] dark = new float[W * H], refl = new float[W * H], fog = new float[W * H];
            List<(Vector3 pos, float power)> lamps = G.OutdoorLights();
            int rowLo = int.MaxValue, rowHi = -1;
            for (int i = 0; i < p.solid.Length; i++) if (p.solid[i]) { int r = i / p.w; rowLo = Mathf.Min(rowLo, r); rowHi = Mathf.Max(rowHi, r); }

            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                {
                    if (!p.solid[y * p.w + x]) continue;
                    int px = p.x0 + x, py = p.y0 + y;
                    float hf = rowHi > rowLo ? (y - rowLo) / (float)(rowHi - rowLo) : 0f;
                    if (fogLo >= 0f && hf >= fogLo && hf <= fogHi)
                    {
                        int fi = (py + H / 2) * W + px + W / 2;
                        if (fi >= 0 && fi < fog.Length) fog[fi] = 1f;
                    }
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            Vector3 q = OnPlane(px + 0.25f + sx * 0.5f, py + 0.25f + sy * 0.5f, feet.z);
                            // 바닥에 닿은 자리: 발·무릎·손 아래가 진하게 어둡다
                            if (grounded && q.y < 0.35f)
                            {
                                float near = Mathf.Exp(-Mathf.Max(0f, q.y) / 0.08f);
                                for (float dz = -0.1f; dz <= 0.3f; dz += 0.04f)
                                    Splat(dark, new Vector3(q.x, 0f, q.z + dz), 0.55f * near * Mathf.Exp(-(dz - 0.06f) * (dz - 0.06f) / 0.015f));
                            }
                            // 젖은 바닥에 비친 어둠(위로 갈수록 흐려진다)
                            if (grounded && q.y < 1.1f && q.y >= 0f)
                                Splat(refl, new Vector3(q.x, -q.y, q.z), 0.26f * (1f - q.y / 1.1f));
                            // 바깥 등이 드리우는 그림자: 등에서 몸을 지나 바닥이나 정면 벽에 닿는 곳
                            foreach (var (lp, power) in lamps)
                            {
                                Vector3 d = q - lp;
                                if (d.y > -1e-3f) continue;
                                float tg = -lp.y / d.y;
                                float tw = d.z < -1e-4f ? (FacadeZ - lp.z) / d.z : float.MaxValue;
                                Vector3 hit, sn;
                                if (tw > 1f && tw < tg) { hit = lp + d * tw; sn = Vector3.forward; if (OnGlass(hit)) continue; }
                                else if (tg > 1f) { hit = lp + d * tg; sn = Vector3.up; if (hit.z < FacadeZ) continue; }
                                else continue;
                                Vector3 to = lp - hit;
                                float dist = to.magnitude;
                                Vector3 dir = to / dist;
                                float ndl = Vector3.Dot(sn, dir);
                                if (ndl <= 0f) continue;
                                float a = Mathf.Min(0.62f, power * ndl / (dist * dist) * ShadowK);
                                if (a < 0.03f) continue;
                                if (G.Blocked(hit + sn * 0.01f, dir, dist - 0.05f)) continue;   // 이미 다른 것의 그림자 안
                                Splat(dark, hit, a);
                            }
                        }
                }

            dark = Blur(Blur(dark, 1, 1), 1, 1);
            refl = Blur(refl, 1, 3);
            fog = Blur(Blur(fog, 4, 4), 2, 2);

            // 김서림은 유리 위에만
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (fog[i] <= 0f) continue;
                    if (!OnGlass(OnPlane(x - W / 2 + 0.5f, y - H / 2 + 0.5f, FacadeZ))) fog[i] = 0f;
                }

            int bx0 = W, by0 = H, bx1 = -1, by1 = -1;
            var col = new Color32[W * H];
            for (int i = 0; i < col.Length; i++)
            {
                float a = Mathf.Max(dark[i], refl[i]);
                float f = Mathf.Min(1f, fog[i] * 1.8f) * FogAlpha;
                Color32 c = f > a ? new Color32(FogCol.r, FogCol.g, FogCol.b, (byte)(f * 255f)) : new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
                if (c.a < 3) continue;
                col[i] = c;
                int x = i % W, y = i / W;
                bx0 = Mathf.Min(bx0, x); by0 = Mathf.Min(by0, y); bx1 = Mathf.Max(bx1, x); by1 = Mathf.Max(by1, y);
            }
            pos = Vector2.zero;
            if (bx1 < 0) return null;
            int cw = bx1 - bx0 + 1, ch = by1 - by0 + 1;
            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            var cp = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    cp[y * cw + x] = col[(y + by0) * W + x + bx0];
            tex.SetPixels32(cp);
            tex.Apply();
            pos = new Vector2(bx0 - W / 2, by0 - H / 2);
            return Save(tex, file, Vector2.zero);
        }
    }
}
