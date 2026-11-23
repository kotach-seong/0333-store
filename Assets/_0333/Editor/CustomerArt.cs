using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 손님 픽셀아트를 코드로 그린다(PixelLab 없이). 결과는 PixelLab 손님과 같은 꼴:
    /// 128x128, 앞모습(south.png)과 뒷모습(north.png), 발이 아래쪽, 실제 사람 비율.
    ///
    /// 그리는 방법
    ///  1) 몸·옷·머리 같은 덩어리를 "영역"으로 칠한다(뒤에 그린 것이 앞).
    ///  2) 영역마다 왼쪽 위에서 빛이 오는 원통 명암을 넣고, 바깥은 어두운 외곽선,
    ///     겹친 경계(팔과 몸통, 머리카락과 얼굴 등)는 한 단계 어둡게 그어 준다.
    ///  3) 그 위에 눈·입·단추·무늬 같은 잔디테일을 직접 찍는다.
    /// 손님 목록은 CustomerArt.Roster.cs.
    /// </summary>
    public static partial class CustomerArt
    {
        public const int S = 128;
        /// <summary>1미터 = 68.6픽셀(PixelLab 후드 손님 키 1.72m가 118px).</summary>
        public const float PxPerM = 68.6f;

        public static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c);
            return c;
        }

        public static Color32 Mul(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        public static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));

        public static float N(int a, int b, int c = 0) => Pal.Noise(a, b, c);

        public delegate Color32 Paint(int x, int y);

        public static Paint Solid(Color32 c) => (x, y) => c;

        /// <summary>영역의 명암 종류.</summary>
        public enum Mat { Cloth, Skin, Hair, Shiny, Flat }

        // ── 그림판 ─────────────────────────────────────────────
        public class Fig
        {
            public readonly Color32[] col = new Color32[S * S];
            public readonly int[] reg = new int[S * S];
            readonly List<Mat> mats = new List<Mat> { Mat.Flat };
            public Color32[] img;

            public int Region(Mat m = Mat.Cloth)
            {
                mats.Add(m);
                return mats.Count - 1;
            }

            public static bool In(int x, int y) => x >= 0 && y >= 0 && x < S && y < S;

            public void Set(int r, int x, int y, Color32 c)
            {
                if (!In(x, y) || c.a == 0) return;
                int i = y * S + x;
                reg[i] = r;
                col[i] = c;
            }

            /// <summary>줄마다 [왼쪽, 오른쪽) 범위를 칠한다(픽셀 중심 기준). y는 위쪽이 +.</summary>
            public void Rows(int r, float y0, float y1, Func<float, Vector2> span, Paint p)
            {
                if (y1 < y0) { float t = y0; y0 = y1; y1 = t; }
                for (int y = Mathf.FloorToInt(y0) - 1; y <= Mathf.CeilToInt(y1) + 1; y++)
                {
                    float yc = y + 0.5f;
                    if (yc < y0 || yc > y1) continue;
                    Vector2 s = span(yc);
                    for (int x = Mathf.CeilToInt(s.x - 0.5f); x <= Mathf.FloorToInt(s.y - 0.5f); x++) Set(r, x, y, p(x, y));
                }
            }

            /// <summary>칸마다 [아래, 위] 범위를 칠한다.</summary>
            public void Cols(int r, int x0, int x1, Func<int, Vector2> span, Paint p)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 s = span(x);
                    for (int y = Mathf.CeilToInt(s.x - 0.5f); y <= Mathf.FloorToInt(s.y - 0.5f); y++) Set(r, x, y, p(x, y));
                }
            }

            public void Ellipse(int r, float cx, float cy, float rx, float ry, Paint p)
            {
                if (rx <= 0 || ry <= 0) return;
                for (int y = Mathf.FloorToInt(cy - ry) - 1; y <= Mathf.CeilToInt(cy + ry) + 1; y++)
                    for (int x = Mathf.FloorToInt(cx - rx) - 1; x <= Mathf.CeilToInt(cx + rx) + 1; x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Set(r, x, y, p(x, y));
                    }
            }

            /// <summary>굵기가 변하는 막대(팔·다리·꼬리).</summary>
            public void Capsule(int r, Vector2 a, Vector2 b, float ra, float rb, Paint p)
            {
                float m = Mathf.Max(ra, rb) + 1f;
                Vector2 ab = b - a;
                float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
                for (int y = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - m); y <= Mathf.CeilToInt(Mathf.Max(a.y, b.y) + m); y++)
                    for (int x = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - m); x <= Mathf.CeilToInt(Mathf.Max(a.x, b.x) + m); x++)
                    {
                        var q = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
                        float rad = Mathf.Lerp(ra, rb, t);
                        if ((q - (a + ab * t)).sqrMagnitude <= rad * rad) Set(r, x, y, p(x, y));
                    }
            }

            /// <summary>다각형(짝홀 규칙).</summary>
            public void Poly(int r, Paint p, params Vector2[] pts)
            {
                float y0 = float.MaxValue, y1 = float.MinValue;
                foreach (var v in pts) { y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
                var xs = new List<float>();
                for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                {
                    float yc = y + 0.5f;
                    xs.Clear();
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    {
                        Vector2 a = pts[i], b = pts[j];
                        if ((a.y <= yc && yc < b.y) || (b.y <= yc && yc < a.y))
                            xs.Add(a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                        for (int x = Mathf.CeilToInt(xs[k] - 0.5f); x <= Mathf.FloorToInt(xs[k + 1] - 0.5f); x++) Set(r, x, y, p(x, y));
                }
            }

            // ── 명암과 외곽선 ──
            static float Factor(Mat m, float t, int len)
            {
                if (m == Mat.Flat) return 1f;
                if (len <= 2) return 0.95f;
                switch (m)
                {
                    case Mat.Skin: return t < 0.15f ? 1.05f : t < 0.74f ? 1f : t < 0.9f ? 0.95f : 0.9f;
                    case Mat.Hair: return t < 0.22f ? 1.15f : t < 0.55f ? 1f : t < 0.8f ? 0.87f : 0.79f;
                    case Mat.Shiny: return t < 0.14f ? 1.45f : t < 0.3f ? 1.13f : t < 0.72f ? 0.96f : 0.76f;
                    default: return t < 0.16f ? 1.1f : t < 0.38f ? 1.03f : t < 0.68f ? 0.97f : t < 0.86f ? 0.87f : 0.78f;
                }
            }

            public void Shade()
            {
                img = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    int x = 0;
                    while (x < S)
                    {
                        int r = reg[y * S + x];
                        if (r == 0) { x++; continue; }
                        int x0 = x;
                        while (x < S && reg[y * S + x] == r) x++;
                        int len = x - x0;
                        Mat m = mats[r];
                        for (int px = x0; px < x; px++)
                        {
                            int i = y * S + px;
                            float f = Factor(m, (px - x0 + 0.5f) / len, len);
                            if (m != Mat.Flat)
                            {
                                int up = y + 1 < S ? reg[i + S] : 0, dn = y > 0 ? reg[i - S] : 0;
                                if (up != r) f *= 1.05f;
                                if (m == Mat.Hair && N(px, y / 2, 7) > 0.78f) f *= 0.84f; // 머리카락 결
                                if (dn != r) f *= 0.9f;
                            }
                            img[i] = Mul(col[i], f);
                        }
                    }
                }
                // 외곽선: 바깥은 거의 검게, 겹친 경계는 위에 그린 쪽을 한 단계 어둡게
                var outline = new Color32[S * S];
                Array.Copy(img, outline, img.Length);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int i = y * S + x, r = reg[i];
                        if (r == 0) continue;
                        bool outer = false, inner = false;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0), ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                            int s = In(nx, ny) ? reg[ny * S + nx] : 0;
                            if (s == 0) outer = true;
                            else if (s != r && s < r) inner = true;
                        }
                        if (outer)
                        {
                            Color32 c = col[i];
                            outline[i] = new Color32((byte)(c.r * 0.22f + 14), (byte)(c.g * 0.2f + 11), (byte)(c.b * 0.22f + 16), 255);
                        }
                        else if (inner) outline[i] = Mul(img[i], 0.7f);
                    }
                Array.Copy(outline, img, img.Length);
            }

            // ── 잔디테일(명암 뒤에 직접 찍기) ──
            public void Px(int x, int y, Color32 c)
            {
                if (!In(x, y) || c.a == 0 || img == null) return;
                int i = y * S + x;
                img[i] = c.a == 255 ? c : Color32.Lerp(img[i], new Color32(c.r, c.g, c.b, 255), c.a / 255f);
                if (reg[i] == 0 && c.a == 255) reg[i] = -1;
            }

            /// <summary>몸(칠해진 곳) 위에만 찍는다.</summary>
            public void On(int x, int y, Color32 c)
            {
                if (In(x, y) && reg[y * S + x] != 0) Px(x, y, c);
            }

            public Color32 Get(int x, int y) => In(x, y) && img != null ? img[y * S + x] : default;
            public int RegAt(int x, int y) => In(x, y) ? reg[y * S + x] : 0;

            public void HLine(int x0, int x1, int y, Color32 c) { for (int x = x0; x <= x1; x++) Px(x, y, c); }
            public void VLine(int x, int y0, int y1, Color32 c) { for (int y = y0; y <= y1; y++) Px(x, y, c); }

            public void Line(int x0, int y0, int x1, int y1, Color32 c, bool onBody = false)
            {
                int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
                while (true)
                {
                    if (onBody) On(x0, y0, c); else Px(x0, y0, c);
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }

            public void Save(string path)
            {
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                tex.SetPixels32(img);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        // ── 몸 비율 ──────────────────────────────────────────────
        public enum Build { Normal, Thin, Muscular, Chubby, Huge, Female, Girl, Child }

        public class Body
        {
            public float cx, feet = 3f, H, top;
            public float headCy, headRx, headRy, jaw;
            public float chinY, neckHalf, sh, shHalf, chestHalf, bellyHalf, bellyY, waistY, waistHalf, hipY, hipHalf;
            public float kneeY, ankleY, thighHalf, kneeHalf, ankleHalf;
            public float upperArmR, foreArmR, handRx, handRy;
            public bool child, female, huge;

            public Body(Build bt, float heightM, float cx)
            {
                this.cx = cx;
                H = heightM * PxPerM;
                top = feet + H;
                child = bt == Build.Child;
                female = bt == Build.Female || bt == Build.Girl;
                huge = bt == Build.Huge;
                headRy = H * (child ? 0.125f : 0.095f);
                headRx = headRy * (child ? 0.82f : female ? 0.7f : 0.72f);
                jaw = child ? 0.86f : female ? 0.8f : 0.76f;
                float neckH = child ? 2f : 3f;
                switch (bt)
                {
                    case Build.Thin: Set(18f, 16f, 0f, 13f, 14f, 6.5f, 4.6f, 3.2f, 3f, 3.6f, 3f, 2.7f, 3.7f); break;
                    case Build.Muscular: Set(26f, 24f, 0f, 16f, 17f, 9f, 6f, 4f, 6f, 6.6f, 5.2f, 3.6f, 4.5f); neckH = 2f; break;
                    case Build.Chubby: Set(21.5f, 21f, 23f, 21f, 20f, 9f, 6.2f, 4f, 4.5f, 4.8f, 4f, 3.3f, 4.2f); headRx *= 1.08f; jaw = 0.9f; break;
                    case Build.Huge: Set(26f, 29f, 35f, 32f, 30f, 13.5f, 9f, 5.5f, 6f, 7f, 6f, 4f, 4.8f); headRx *= 1.22f; jaw = 0.98f; neckH = 1f; break;
                    case Build.Female: Set(15.5f, 15f, 0f, 11.5f, 16f, 7f, 4.6f, 3f, 2.8f, 3.3f, 2.7f, 2.4f, 3.4f); break;
                    case Build.Girl: Set(14.5f, 13.5f, 0f, 11f, 14.5f, 6.6f, 4.4f, 2.9f, 2.7f, 3.1f, 2.6f, 2.3f, 3.2f); break;
                    case Build.Child: Set(11f, 11f, 0f, 10.5f, 11f, 5f, 3.6f, 2.6f, 2.4f, 2.8f, 2.4f, 2.2f, 2.8f); break;
                    default: Set(20f, 18.5f, 0f, 15f, 16f, 7.5f, 5.2f, 3.6f, 3.5f, 4.2f, 3.4f, 3f, 4f); break;
                }
                headCy = top - headRy;
                chinY = headCy - headRy;
                sh = chinY - neckH;
                float torso = H * 0.27f;
                waistY = sh - torso;
                hipY = waistY - H * 0.085f;
                ankleY = feet + (child ? 4f : 5f);
                kneeY = (hipY + ankleY) * 0.5f;
                bellyY = sh - torso * 0.62f;
            }

            void Set(float sh, float chest, float belly, float waist, float hip, float thigh, float knee, float ankle, float neck,
                     float upArm, float foreArm, float hRx, float hRy)
            {
                shHalf = sh; chestHalf = chest; bellyHalf = belly; waistHalf = waist; hipHalf = hip;
                thighHalf = thigh; kneeHalf = knee; ankleHalf = ankle; neckHalf = neck;
                upperArmR = upArm; foreArmR = foreArm; handRx = hRx; handRy = hRy;
            }

            public float Torso => sh - waistY;

            /// <summary>머리 너비의 절반(y 높이에서). 턱 쪽은 좁아진다.</summary>
            public float HeadHalf(float y)
            {
                float d = (y - headCy) / headRy;
                if (d >= 1f || d <= -1f) return 0f;
                float w = headRx * Mathf.Sqrt(1f - d * d);
                if (d < 0f) w *= Mathf.Lerp(1f, jaw, -d);
                return w;
            }

            /// <summary>몸통(과 긴 옷) 너비의 절반.</summary>
            public float TorsoHalf(float y)
            {
                var pts = new List<Vector2>
                {
                    new Vector2(sh + 0.5f, shHalf - 3.5f), new Vector2(sh - 1.5f, shHalf - 0.6f), new Vector2(sh - 4f, shHalf),
                    new Vector2(sh - Torso * 0.35f, chestHalf),
                };
                if (bellyHalf > 0f) pts.Add(new Vector2(bellyY, bellyHalf));
                pts.Add(new Vector2(waistY, waistHalf));
                pts.Add(new Vector2(hipY, hipHalf));
                pts.Add(new Vector2(kneeY, hipHalf + 2.5f));
                pts.Add(new Vector2(feet, hipHalf + 6f));
                if (y >= pts[0].x) return pts[0].y;
                for (int i = 1; i < pts.Count; i++)
                    if (y >= pts[i].x)
                        return Mathf.Lerp(pts[i].y, pts[i - 1].y, (y - pts[i].x) / Mathf.Max(0.001f, pts[i - 1].x - pts[i].x));
                return pts[pts.Count - 1].y;
            }

            /// <summary>팔 관절(어깨 J, 팔꿈치 E, 손목 W). side: -1 왼쪽, +1 오른쪽(화면 기준).</summary>
            public void Arm(int side, Pose pose, out Vector2 J, out Vector2 E, out Vector2 W)
            {
                float s = side;
                J = new Vector2(cx + s * (shHalf - upperArmR * 0.9f), sh - upperArmR * 0.9f);
                float sideW = Mathf.Max(chestHalf, Mathf.Max(bellyHalf, waistHalf));
                float lowW = Mathf.Max(Mathf.Max(waistHalf, hipHalf), bellyHalf * 0.95f);
                switch (pose)
                {
                    case Pose.Hold:
                        E = new Vector2(cx + s * (sideW + upperArmR * 0.3f), waistY + 3f);
                        W = new Vector2(cx + s * 5f, (bellyHalf > 0f ? bellyY : waistY) + 8f);
                        break;
                    case Pose.Hips:
                        E = new Vector2(cx + s * (sideW + 7f), waistY + 5f);
                        W = new Vector2(cx + s * (waistHalf + 1.5f), waistY + 0.5f);
                        break;
                    case Pose.Crossed:
                        E = new Vector2(cx + s * (sideW + upperArmR * 0.2f), waistY + 6f);
                        W = new Vector2(cx - s * (chestHalf - 5f), waistY + 11f + (side > 0 ? 1f : 0f));
                        break;
                    case Pose.Pockets:
                        E = new Vector2(cx + s * (sideW + upperArmR * 0.5f), waistY + 2f);
                        W = new Vector2(cx + s * (hipHalf - 1.5f), hipY + 3f);
                        break;
                    case Pose.RaiseRight when side > 0:
                        E = new Vector2(cx + s * (sideW + 5f), sh - 9f);
                        W = new Vector2(cx + s * (shHalf + 1f), sh + 6f);
                        break;
                    default:
                        E = new Vector2(cx + s * (sideW + upperArmR * 0.55f), waistY + 2f);
                        W = new Vector2(cx + s * (lowW + foreArmR + 1.2f), hipY - 1f);
                        break;
                }
            }

            /// <summary>뒤에서 본 머리 너비의 절반: 턱 대신 둥근 뒤통수.</summary>
            public float HeadHalfBack(float y)
            {
                float d = (y - headCy) / headRy;
                if (d >= 1f || d <= -1f) return 0f;
                float w = headRx * Mathf.Sqrt(1f - d * d);
                if (d < 0f) w *= Mathf.Lerp(1f, 0.9f, -d);
                return Mathf.Max(w, d < 0f ? neckHalf + 0.5f : 0f);
            }
        }

        // ── 손님 설정 ────────────────────────────────────────────
        public enum Pose { Down, Hold, Hips, Crossed, Pockets, RaiseRight }
        public enum Hair { None, Short, Messy, Slick, Buzz, Bald, BaldFringe, Long, Bob, Perm, TwinTails, Pigtails, Ponytail, Parted }
        public enum Hat { None, Fedora, Cap, Helmet, PoliceCap, Visor, Bandana, Hood }
        public enum Eyes { Normal, Narrow, Tired, Wide, Closed }
        public enum Brow { Flat, Angry, Worried, Raised }
        public enum Mouth { Flat, Smile, Frown, Open, Grin }
        [Flags] public enum Face { None = 0, Glasses = 1, Foggy = 2, Sunglasses = 4, Stubble = 8, Beard = 16, Mustache = 32, Wrinkles = 64, Blush = 128, Flush = 256, Lipstick = 512, Eyeshadow = 1024, Scar = 2048, Sweat = 4096, Faceless = 8192, HairOverEye = 16384, DoubleChin = 32768 }
        public enum Top { TShirt, Tank, Suit, Coat, Military, Police, Blazer, Hanbok, Gown, Robe, Cardigan, Raincoat, Puffer, Vest, Blouse, Frilly, LoudShirtSuit }
        public enum Bottom { Jeans, Slacks, Shorts, Skirt, Cargo, Covered }
        public enum Shoes { Sneakers, Dress, Boots, Heels, RainBoots, Slippers }

        public class Spec
        {
            public string id, label;
            public float heightM = 1.72f;
            public Build build = Build.Normal;
            public Color32 skin = C("#E3B08F");
            public Hair hair = Hair.Short;
            public Color32 hairCol = C("#2B2420");
            public Hat hat = Hat.None;
            public Color32 hatCol = C("#2A2A2E"), hatCol2 = C("#55555A");
            public Eyes eyes = Eyes.Normal;
            public Brow brow = Brow.Flat;
            public Mouth mouth = Mouth.Flat;
            public Color32 iris = C("#3A2A22");
            public Face face = Face.None;
            public Color32 glassesCol = C("#202024");
            public Top top = Top.TShirt;
            public Color32 topCol = C("#8A8F96"), topCol2 = C("#F0EEE8"), topCol3 = C("#7A2A2A");
            public Paint topPaint;
            public Bottom bottom = Bottom.Jeans;
            public Color32 bottomCol = C("#41587A");
            public Paint bottomPaint;
            public Shoes shoes = Shoes.Sneakers;
            public Color32 shoeCol = C("#E6E6E0");
            public Pose pose = Pose.Down;
            /// <summary>명암 넣기 전에 더 그릴 영역(소품 등). back: 뒷모습인지.</summary>
            public Action<Fig, Body, bool> regions;
            /// <summary>명암 넣은 뒤에 찍을 잔디테일.</summary>
            public Action<Fig, Body, bool> overlay;
            /// <summary>같이 온 사람(커플, 시장과 동행). 앞모습에서 오른쪽에 선다.</summary>
            public Spec partner;
        }

        // ── 한 사람 그리기 ───────────────────────────────────────
        public static Fig Render(Spec s, bool back)
        {
            var f = new Fig();
            if (s.partner == null)
            {
                var b = new Body(s.build, s.heightM, 64f);
                DrawRegions(f, s, b, back);
                f.Shade();
                DrawOverlay(f, s, b, back);
                return f;
            }
            // 둘이 같이 선 그림: 앞모습은 주인공 왼쪽·동행 오른쪽, 뒷모습은 반대
            float lx = 42f, rx = 87f;
            var b1 = new Body(s.build, s.heightM, back ? rx : lx);
            var b2 = new Body(s.partner.build, s.partner.heightM, back ? lx : rx);
            DrawRegions(f, s, b1, back);
            DrawRegions(f, s.partner, b2, back);
            f.Shade();
            DrawOverlay(f, s, b1, back);
            DrawOverlay(f, s.partner, b2, back);
            return f;
        }

        static bool LongGarment(Top t) => t == Top.Coat || t == Top.Gown || t == Top.Robe || t == Top.Hanbok || t == Top.Raincoat;

        static float HemY(Spec s, Body b)
        {
            switch (s.top)
            {
                case Top.Coat: case Top.Raincoat: return b.kneeY - 2f;
                case Top.Gown: case Top.Robe: return b.ankleY + 1f;
                case Top.Hanbok: return b.ankleY + 3f;
                case Top.TShirt: case Top.Tank: case Top.Vest: case Top.Blouse: return b.hipY + 3f;
                case Top.Frilly: return b.waistY - 1f;
                case Top.Suit: case Top.Cardigan: case Top.LoudShirtSuit: return b.hipY - 1f;
                default: return b.hipY;
            }
        }

        enum Sleeve { Long, Short, None }

        static Sleeve SleeveOf(Top t)
        {
            switch (t)
            {
                case Top.TShirt: case Top.Vest: case Top.Frilly: return Sleeve.Short;
                case Top.Tank: case Top.Gown: return Sleeve.None;
                default: return Sleeve.Long;
            }
        }

        static Paint TopPaint(Spec s) => s.topPaint ?? Solid(s.topCol);

        static void DrawRegions(Fig f, Spec s, Body b, bool back)
        {
            // 1) 몸 뒤(긴 머리 뒤쪽, 쌍갈래 꼬리)
            if (!back) HairBehind(f, s, b);
            // 2) 다리·신발
            Legs(f, s, b, back);
            Shoes_(f, s, b, back);
            // 3) 뒷모습은 팔을 몸통보다 먼저(앞으로 모은 손은 몸에 가려진다)
            if (back) Arms(f, s, b, true);
            Neck(f, s, b);
            TorsoTop(f, s, b, back);
            if (!back) Arms(f, s, b, false);
            s.regions?.Invoke(f, b, back);
            // 4) 머리
            Head(f, s, b, back);
            if (back) HairBackView(f, s, b); else HairFront(f, s, b);
            HatRegions(f, s, b, back);
        }

        // ── 다리 ──
        static void Legs(Fig f, Spec s, Body b, bool back)
        {
            Paint pants = s.bottomPaint ?? Solid(s.bottomCol);
            bool covered = s.bottom == Bottom.Covered || LongGarment(s.top);
            float hem = HemY(s, b);
            // 골반(바지 윗부분)
            if (s.bottom != Bottom.Skirt && !covered)
            {
                int pelvis = f.Region();
                f.Rows(pelvis, b.hipY - 3f, b.waistY + 1f, y => Span(b.cx, b.TorsoHalf(y)), pants);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float hipX = b.cx + side * b.hipHalf * 0.5f, kneeX = b.cx + side * b.hipHalf * 0.45f, ankX = b.cx + side * (b.hipHalf * 0.42f + 0.5f);
                var hip = new Vector2(hipX, b.hipY + 2f);
                var knee = new Vector2(kneeX, b.kneeY);
                var ank = new Vector2(ankX, b.ankleY);
                Color32 legSkin = s.skin;
                if (covered)
                {
                    // 긴 옷 아래로 드러난 발목만
                    if (hem > b.ankleY + 1f)
                    {
                        int r = f.Region(Mat.Skin);
                        Vector2 top = Vector2.Lerp(knee, ank, Mathf.Clamp01((b.kneeY - hem) / Mathf.Max(1f, b.kneeY - b.ankleY)));
                        f.Capsule(r, new Vector2(top.x, hem + 1f), ank, b.kneeHalf * 0.9f, b.ankleHalf, s.top == Top.Raincoat ? Solid(s.skin) : Solid(legSkin));
                    }
                    continue;
                }
                switch (s.bottom)
                {
                    case Bottom.Shorts:
                    {
                        int skinR = f.Region(Mat.Skin);
                        f.Capsule(skinR, hip, knee, b.thighHalf * 0.92f, b.kneeHalf, Solid(legSkin));
                        f.Capsule(skinR, knee, ank, b.kneeHalf, b.ankleHalf, Solid(legSkin));
                        int r = f.Region();
                        f.Capsule(r, hip, Vector2.Lerp(hip, knee, 0.55f), b.thighHalf + 1f, b.thighHalf + 0.6f, pants);
                        break;
                    }
                    case Bottom.Skirt:
                    {
                        int r = f.Region(Mat.Skin);
                        f.Capsule(r, hip, knee, b.thighHalf * 0.85f, b.kneeHalf * 0.95f, Solid(legSkin));
                        f.Capsule(r, knee, ank, b.kneeHalf * 0.95f, b.ankleHalf, Solid(legSkin));
                        break;
                    }
                    default:
                    {
                        int r = f.Region();
                        f.Capsule(r, hip, knee, b.thighHalf, b.kneeHalf + 0.4f, pants);
                        f.Capsule(r, knee, new Vector2(ank.x, ank.y - 1f), b.kneeHalf + 0.4f, b.ankleHalf + 0.9f, pants);
                        break;
                    }
                }
            }
            if (s.bottom == Bottom.Skirt && !covered)
            {
                // 무릎 위까지 퍼지는 치마
                int r = f.Region();
                float bottom = b.hipY - (b.hipY - b.kneeY) * 0.55f;
                f.Rows(r, bottom, b.waistY + 1f, y => Span(b.cx, Mathf.Lerp(b.hipHalf + 3f, b.waistHalf + 0.5f, (y - bottom) / Mathf.Max(1f, b.waistY + 1f - bottom))), pants);
            }
        }

        static Vector2 Span(float cx, float half) => new Vector2(cx - half, cx + half);

        // ── 신발 ──
        static void Shoes_(Fig f, Spec s, Body b, bool back)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float ankX = b.cx + side * (b.hipHalf * 0.42f + 0.5f);
                float w = b.ankleHalf + 2.4f;
                switch (s.shoes)
                {
                    case Shoes.Boots:
                    {
                        int r = f.Region();
                        f.Capsule(r, new Vector2(ankX, b.ankleY + 6f), new Vector2(ankX, b.feet + 3f), b.ankleHalf + 1.7f, b.ankleHalf + 2f, Solid(s.shoeCol));
                        f.Ellipse(r, ankX + side * 0.6f, b.feet + 2.5f, w + 0.5f, 2.8f, Solid(s.shoeCol));
                        break;
                    }
                    case Shoes.RainBoots:
                    {
                        int r = f.Region(Mat.Shiny);
                        f.Capsule(r, new Vector2(ankX, (b.kneeY + b.ankleY) * 0.5f + 2f), new Vector2(ankX, b.feet + 3f), b.ankleHalf + 2f, b.ankleHalf + 2.2f, Solid(s.shoeCol));
                        f.Ellipse(r, ankX + side * 0.4f, b.feet + 2.5f, w, 2.8f, Solid(s.shoeCol));
                        break;
                    }
                    case Shoes.Heels:
                    {
                        int r = f.Region(Mat.Shiny);
                        f.Ellipse(r, ankX, b.feet + 2.6f, b.ankleHalf + 1.4f, 2.2f, Solid(s.shoeCol));
                        break;
                    }
                    case Shoes.Slippers:
                    {
                        int foot = f.Region(Mat.Skin);
                        f.Ellipse(foot, ankX + side * 0.5f, b.feet + 2.6f, w - 0.4f, 2.6f, Solid(C("#E9E6DD"))); // 흰 양말
                        int r = f.Region();
                        f.Rows(r, b.feet + 1.5f, b.feet + 4.2f, y => Span(ankX + side * 0.5f, w - 0.6f), Solid(s.shoeCol));
                        break;
                    }
                    default:
                    {
                        int r = f.Region(s.shoes == Shoes.Dress ? Mat.Shiny : Mat.Cloth);
                        f.Ellipse(r, ankX + side * 0.7f, b.feet + 2.6f, w, back ? 2.6f : 3f, Solid(s.shoeCol));
                        break;
                    }
                }
            }
        }

        static void Neck(Fig f, Spec s, Body b)
        {
            int r = f.Region(Mat.Skin);
            f.Rows(r, b.sh - 3f, b.chinY + 2.5f, y => Span(b.cx, b.neckHalf), Solid(Mul(s.skin, 0.93f)));
        }

        // ── 윗옷 ──
        static void TorsoTop(Fig f, Spec s, Body b, bool back)
        {
            float hem = HemY(s, b);
            Paint p = TopPaint(s);
            float t = b.Torso;
            switch (s.top)
            {
                case Top.Tank:
                {
                    int chest = f.Region(Mat.Skin);
                    f.Rows(chest, b.sh - 9f, b.sh + 0.5f, y => Span(b.cx, b.TorsoHalf(y)), Solid(s.skin));
                    int r = f.Region();
                    f.Rows(r, hem, b.sh - 7f, y => Span(b.cx, b.TorsoHalf(y) - (y > b.sh - 12f ? 2.5f : 0f)), p);
                    float strapX = b.shHalf * 0.42f;
                    f.Rows(r, b.sh - 8f, b.sh + 0.5f, y => Span(b.cx - strapX, 1.6f), p);
                    f.Rows(r, b.sh - 8f, b.sh + 0.5f, y => Span(b.cx + strapX, 1.6f), p);
                    if (!back)
                    {
                        int scoop = f.Region(Mat.Skin);
                        f.Ellipse(scoop, b.cx, b.sh - 5f, strapX - 1.6f, 6.5f, Solid(s.skin));
                    }
                    return;
                }
                case Top.Gown:
                {
                    int chest = f.Region(Mat.Skin);
                    f.Rows(chest, b.sh - 6f, b.sh + 0.5f, y => Span(b.cx, b.TorsoHalf(y)), Solid(s.skin));
                    int r = f.Region();
                    f.Rows(r, hem, b.sh - 4.5f, y => Span(b.cx, b.TorsoHalf(y) - (y > b.sh - 8f ? 1.5f : 0f)), p);
                    float strapX = b.shHalf * 0.5f;
                    f.Rows(r, b.sh - 5f, b.sh + 0.5f, y => Span(b.cx - strapX, 0.9f), p);
                    f.Rows(r, b.sh - 5f, b.sh + 0.5f, y => Span(b.cx + strapX, 0.9f), p);
                    return;
                }
            }

            int body = f.Region();
            f.Rows(body, hem, b.sh + 0.5f, y => Span(b.cx, b.TorsoHalf(y) + (LongGarment(s.top) && y < b.hipY ? 0.5f : 0f)), p);
            if (back) return;

            switch (s.top)
            {
                case Top.Suit:
                case Top.LoudShirtSuit:
                {
                    int shirt = f.Region();
                    Color32 shirtCol = s.top == Top.LoudShirtSuit ? s.topCol3 : s.topCol2;
                    float vy = b.sh - t * (s.top == Top.LoudShirtSuit ? 0.55f : 0.5f);
                    f.Poly(shirt, s.top == Top.LoudShirtSuit ? LoudPrint(s.topCol3) : Solid(shirtCol),
                        new Vector2(b.cx - 6f, b.sh + 0.5f), new Vector2(b.cx + 6f, b.sh + 0.5f), new Vector2(b.cx, vy));
                    if (s.top == Top.Suit)
                    {
                        int tie = f.Region();
                        f.Poly(tie, Solid(s.topCol3),
                            new Vector2(b.cx - 1.3f, b.sh - 1f), new Vector2(b.cx + 1.3f, b.sh - 1f), new Vector2(b.cx + 2.3f, b.sh - t * 0.42f),
                            new Vector2(b.cx, b.sh - t * 0.49f), new Vector2(b.cx - 2.3f, b.sh - t * 0.42f));
                        f.Ellipse(tie, b.cx, b.sh - 0.8f, 1.8f, 1.5f, Solid(s.topCol3));
                    }
                    break;
                }
                case Top.Blazer:
                {
                    int shirt = f.Region();
                    f.Poly(shirt, Solid(s.topCol2), new Vector2(b.cx - 5f, b.sh + 0.5f), new Vector2(b.cx + 5f, b.sh + 0.5f), new Vector2(b.cx, b.sh - t * 0.45f));
                    int bow = f.Region();
                    f.Ellipse(bow, b.cx - 2.2f, b.sh - 2f, 2.3f, 1.6f, Solid(s.topCol3));
                    f.Ellipse(bow, b.cx + 2.2f, b.sh - 2f, 2.3f, 1.6f, Solid(s.topCol3));
                    f.Poly(bow, Solid(s.topCol3), new Vector2(b.cx - 0.8f, b.sh - 2.5f), new Vector2(b.cx - 2.4f, b.sh - 8f), new Vector2(b.cx - 0.6f, b.sh - 7.5f));
                    f.Poly(bow, Solid(s.topCol3), new Vector2(b.cx + 0.8f, b.sh - 2.5f), new Vector2(b.cx + 2.4f, b.sh - 8f), new Vector2(b.cx + 0.6f, b.sh - 7.5f));
                    break;
                }
                case Top.Cardigan:
                {
                    // 안에 입은 블라우스가 가운데로 보이고, 양쪽 앞판이 덮는다
                    int inner = f.Region();
                    f.Rows(inner, b.hipY, b.sh + 0.5f, y => Span(b.cx, 4.5f + (b.sh - y) * 0.05f), Solid(s.topCol2));
                    break;
                }
                case Top.TShirt:
                case Top.Vest:
                case Top.Blouse:
                case Top.Frilly:
                {
                    int neck = f.Region(Mat.Skin);
                    f.Ellipse(neck, b.cx, b.sh + 0.6f, b.neckHalf + 1.4f, 3f, Solid(Mul(s.skin, 0.93f)));
                    if (s.top == Top.Vest)
                    {
                        // 조끼 앞판(가운데가 갈라진 패딩)
                        int vest = f.Region();
                        float vt = b.sh - 2f;
                        f.Rows(vest, b.hipY + 1f, vt, y => new Vector2(b.cx - b.TorsoHalf(y) + 1f, b.cx - 1.4f), Solid(s.topCol2));
                        f.Rows(vest, b.hipY + 1f, vt, y => new Vector2(b.cx + 1.4f, b.cx + b.TorsoHalf(y) - 1f), Solid(s.topCol2));
                    }
                    if (s.top == Top.Frilly)
                    {
                        int bow = f.Region();
                        f.Ellipse(bow, b.cx - 3f, b.sh - 6f, 3f, 2.2f, Solid(s.topCol3));
                        f.Ellipse(bow, b.cx + 3f, b.sh - 6f, 3f, 2.2f, Solid(s.topCol3));
                        f.Ellipse(bow, b.cx, b.sh - 6f, 1.3f, 1.5f, Solid(Mul(s.topCol3, 0.85f)));
                        int skirt = f.Region();
                        float sb = b.hipY - 6f;
                        f.Rows(skirt, sb, b.waistY + 0.5f, y => Span(b.cx, Mathf.Lerp(b.hipHalf + 7f, b.waistHalf + 1f, (y - sb) / Mathf.Max(1f, b.waistY + 0.5f - sb))), Solid(s.topCol));
                    }
                    break;
                }
                case Top.Coat:
                {
                    // 털 깃(목을 감싸는 북슬북슬한 깃)
                    int fur = f.Region(Mat.Hair);
                    Paint furP = (x, y) => Mul(s.topCol2, 0.9f + N(x, y, 5) * 0.25f);
                    for (int i = -3; i <= 3; i++)
                    {
                        float a = i / 3f;
                        f.Ellipse(fur, b.cx + a * (b.shHalf * 0.62f), b.sh - 2.5f - (1f - Mathf.Abs(a)) * 1.5f, 3.6f, 3.2f, furP);
                    }
                    break;
                }
                case Top.Hanbok:
                {
                    // 동정(흰 깃)과 옷고름은 잔디테일에서
                    break;
                }
                case Top.Robe:
                {
                    int shawl = f.Region(Mat.Cloth);
                    f.Rows(shawl, b.sh - 9f, b.sh + 1.2f, y => Span(b.cx, b.TorsoHalf(y) + 1.6f), Solid(s.topCol2));
                    int collar = f.Region();
                    f.Rows(collar, b.sh - 1f, b.chinY + 1.5f, y => Span(b.cx, b.neckHalf + 1f), Solid(s.topCol));
                    break;
                }
                case Top.Puffer:
                case Top.Raincoat:
                case Top.Military:
                case Top.Police:
                {
                    int collar = f.Region();
                    f.Rows(collar, b.sh - 2.5f, b.sh + 2.5f, y => Span(b.cx, b.neckHalf + 2.2f), s.top == Top.Military ? p : Solid(Mul(s.topCol, 0.92f)));
                    break;
                }
            }
        }

        static Paint LoudPrint(Color32 baseC) => (x, y) =>
        {
            float n = N(x / 3, y / 3, 41);
            return n > 0.72f ? C("#E8C34A") : n > 0.5f ? Mul(baseC, 0.7f) : baseC;
        };

        // ── 팔 ──
        static void Arms(Fig f, Spec s, Body b, bool back)
        {
            Sleeve sl = SleeveOf(s.top);
            Paint sleeveP = s.top == Top.Cardigan ? Solid(s.topCol) : TopPaint(s);
            if (s.top == Top.Vest || s.top == Top.Frilly) sleeveP = Solid(s.top == Top.Frilly ? s.topCol : s.topCol);
            int[] order = { -1, 1 };
            foreach (int side in order)
            {
                b.Arm(side, s.pose, out Vector2 J, out Vector2 E, out Vector2 W);
                if (s.pose == Pose.Crossed && side == 1 && !back) { } // 오른팔이 위
                int arm = f.Region(sl == Sleeve.Long ? Mat.Cloth : Mat.Skin);
                Paint armP = sl == Sleeve.Long ? sleeveP : Solid(s.skin);
                f.Capsule(arm, J, E, b.upperArmR, b.upperArmR * 0.9f, armP);
                f.Capsule(arm, E, W, b.foreArmR * 1.05f, b.foreArmR * 0.88f, armP);
                if (sl == Sleeve.Short)
                {
                    int sleeve = f.Region();
                    float puff = s.top == Top.Frilly ? 1.8f : 0.9f;
                    f.Capsule(sleeve, J, Vector2.Lerp(J, E, s.top == Top.Frilly ? 0.4f : 0.55f), b.upperArmR + puff, b.upperArmR + puff * 0.7f, sleeveP);
                }
                if (s.pose == Pose.Pockets) continue;
                if (back && (s.pose == Pose.Hold || s.pose == Pose.Crossed)) continue;
                int hand = f.Region(Mat.Skin);
                Vector2 dir = (W - E).normalized;
                Vector2 hc = W + dir * (b.handRy * 0.75f);
                f.Ellipse(hand, hc.x, hc.y, b.handRx, b.handRy, Solid(s.skin));
            }
        }

        // ── 머리 ──
        static void Head(Fig f, Spec s, Body b, bool back)
        {
            int ears = f.Region(Mat.Skin);
            float earY = b.headCy - b.headRy * 0.08f;
            f.Ellipse(ears, b.cx - b.headRx - 0.3f, earY, 1.7f, b.headRy * 0.25f, Solid(Mul(s.skin, 0.95f)));
            f.Ellipse(ears, b.cx + b.headRx + 0.3f, earY, 1.7f, b.headRy * 0.25f, Solid(Mul(s.skin, 0.95f)));
            bool faceless = (s.face & Face.Faceless) != 0 && !back;
            int head = f.Region(faceless ? Mat.Flat : Mat.Skin);
            // 뒷모습은 턱 대신 둥근 뒤통수
            f.Rows(head, b.chinY, b.top, y => Span(b.cx, back ? b.HeadHalfBack(y) : b.HeadHalf(y)), Solid(faceless ? C("#08080A") : s.skin));
            if ((s.face & Face.DoubleChin) != 0 && !back)
            {
                int chin = f.Region(Mat.Skin);
                f.Ellipse(chin, b.cx, b.chinY + 0.5f, b.headRx * 0.62f, 2.4f, Solid(Mul(s.skin, 0.96f)));
            }
            if ((s.face & Face.Beard) != 0 && !back)
            {
                int beard = f.Region(Mat.Hair);
                float mouthY = b.headCy - b.headRy * 0.62f;
                f.Rows(beard, b.chinY - 9f, mouthY + 0.5f, y => Span(b.cx, Mathf.Max(1.5f, Mathf.Min(b.HeadHalf(Mathf.Max(y, b.chinY + 0.2f)) - 0.5f, 6f - (b.chinY - y) * 0.45f))),
                    (x, y) => Mul(s.hairCol, 0.95f + N(x, y, 9) * 0.1f));
            }
        }

        static float HairTopY(Body b) => b.top + 1.4f;

        /// <summary>얼굴 뒤로 내려오는 머리(긴 머리, 단발, 쌍갈래).</summary>
        static void HairBehind(Fig f, Spec s, Body b)
        {
            Paint hp = (x, y) => Mul(s.hairCol, 0.94f + N(x, y / 2, 3) * 0.12f);
            switch (s.hair)
            {
                case Hair.Long:
                {
                    int r = f.Region(Mat.Hair);
                    float bottom = b.sh - 16f;
                    f.Rows(r, bottom, HairTopY(b), y => Span(b.cx, y > b.headCy ? b.HeadHalf(y) + 2.2f : b.headRx + 2.6f + (b.chinY - y) * 0.06f), hp);
                    break;
                }
                case Hair.Bob:
                case Hair.TwinTails:
                {
                    int r = f.Region(Mat.Hair);
                    f.Rows(r, b.chinY + 1f, HairTopY(b), y => Span(b.cx, y > b.headCy ? b.HeadHalf(y) + 2.2f : b.headRx + 2.2f), hp);
                    if (s.hair == Hair.TwinTails)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int tail = f.Region(Mat.Hair);
                            var a = new Vector2(b.cx + side * (b.headRx + 1.5f), b.headCy + b.headRy * 0.45f);
                            var m = new Vector2(b.cx + side * (b.shHalf + 6f), b.sh - 4f);
                            var e = new Vector2(b.cx + side * (b.shHalf + 4f), b.waistY - 4f);
                            f.Capsule(tail, a, m, 4.2f, 4.6f, hp);
                            f.Capsule(tail, m, e, 4.6f, 2.2f, hp);
                        }
                    break;
                }
            }
        }

        static void HairFront(Fig f, Spec s, Body b)
        {
            Paint hp = (x, y) => Mul(s.hairCol, 0.94f + N(x, y / 2, 3) * 0.12f);
            float capBottom = b.headCy + b.headRy * 0.28f;
            int x0 = Mathf.FloorToInt(b.cx - b.headRx - 1.5f), x1 = Mathf.CeilToInt(b.cx + b.headRx + 1.5f);
            // 머리를 덮는 모자 모양(정수리부터 이마까지)
            Func<float, Vector2> cap = y => Span(b.cx, Mathf.Max(b.HeadHalf(Mathf.Min(y, b.top - 0.6f)) + 1.2f, y < b.top + 1f ? 2f : 0f));
            switch (s.hair)
            {
                case Hair.Short:
                case Hair.Parted:
                case Hair.Ponytail:
                case Hair.Messy:
                case Hair.Long:
                case Hair.Bob:
                case Hair.TwinTails:
                case Hair.Pigtails:
                {
                    int r = f.Region(Mat.Hair);
                    bool messy = s.hair == Hair.Messy;
                    f.Cols(r, x0, x1, x =>
                    {
                        float half = b.headRx + 1.2f, dx = Mathf.Abs(x + 0.5f - b.cx);
                        if (dx > half) return new Vector2(1f, 0f);
                        float topY = b.headCy + (b.headRy + 1.4f) * Mathf.Sqrt(Mathf.Max(0f, 1f - (dx / half) * (dx / half)));
                        if (messy) topY += N(x, 3, 17) * 2.6f;
                        float jag = N(x, 7, 13) * (messy ? 3.5f : 2f);
                        float lo = capBottom - jag;
                        if (s.hair == Hair.Parted && x + 0.5f > b.cx + 1f) lo = capBottom + 1.5f - jag * 0.5f; // 가르마 쪽은 이마가 보인다
                        if (s.hair == Hair.Long || s.hair == Hair.Bob || s.hair == Hair.TwinTails) lo = capBottom - 1.2f; // 일자 앞머리
                        if (dx > b.headRx - 1.5f) lo = b.headCy - b.headRy * (s.hair == Hair.Long || s.hair == Hair.Bob ? 0.6f : 0.05f); // 옆머리
                        return new Vector2(lo, topY);
                    }, hp);
                    if (s.hair == Hair.Long)
                    {
                        // 어깨까지 내려오는 옆 머리카락
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float sx = b.cx + side * (b.headRx + 0.5f);
                            f.Rows(r, b.sh - 12f, b.headCy, y => new Vector2(sx - 2.2f + side * (b.headCy - y) * 0.08f, sx + 2.2f + side * (b.headCy - y) * 0.08f), hp);
                        }
                    }
                    if (s.hair == Hair.Pigtails)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int tail = f.Region(Mat.Hair);
                            f.Capsule(tail, new Vector2(b.cx + side * (b.headRx + 0.5f), b.headCy - 1f), new Vector2(b.cx + side * (b.headRx + 4.5f), b.headCy - 9f), 3f, 1.8f, hp);
                        }
                    if ((s.face & Face.HairOverEye) != 0)
                    {
                        // 한쪽 눈을 덮는 긴 앞머리
                        int lock_ = f.Region(Mat.Hair);
                        f.Rows(lock_, b.headCy - b.headRy * 0.55f, capBottom, y => new Vector2(b.cx - b.headRx + 0.5f, b.cx - 0.5f - (capBottom - y) * 0.25f), hp);
                    }
                    break;
                }
                case Hair.Slick:
                {
                    int r = f.Region(Mat.Shiny);
                    f.Rows(r, b.headCy + b.headRy * 0.4f, b.top + 2f, y => Span(b.cx, Mathf.Max(b.HeadHalf(Mathf.Min(y, b.top - 0.5f)) + 1.3f, y < b.top + 1.5f ? 3f : 0f)), Solid(s.hairCol));
                    f.Rows(r, b.headCy - b.headRy * 0.05f, b.headCy + b.headRy * 0.45f, y => new Vector2(b.cx - b.HeadHalf(y) - 1.1f, b.cx - b.HeadHalf(y) + 1.2f), Solid(s.hairCol));
                    f.Rows(r, b.headCy - b.headRy * 0.05f, b.headCy + b.headRy * 0.45f, y => new Vector2(b.cx + b.HeadHalf(y) - 1.2f, b.cx + b.HeadHalf(y) + 1.1f), Solid(s.hairCol));
                    break;
                }
                case Hair.Buzz:
                {
                    int r = f.Region(Mat.Hair);
                    f.Rows(r, b.headCy + b.headRy * 0.42f, b.top + 0.6f, y => cap(y), (x, y) => Mix(s.hairCol, s.skin, N(x, y, 21) * 0.45f));
                    break;
                }
                case Hair.BaldFringe:
                {
                    int r = f.Region(Mat.Hair);
                    f.Ellipse(r, b.cx - b.headRx + 0.2f, b.headCy - 0.2f, 2f, b.headRy * 0.36f, hp);
                    f.Ellipse(r, b.cx + b.headRx - 0.2f, b.headCy - 0.2f, 2f, b.headRy * 0.36f, hp);
                    break;
                }
                case Hair.Perm:
                {
                    int r = f.Region(Mat.Hair);
                    Paint curl = (x, y) => Mul(s.hairCol, N(x, y, 33) > 0.62f ? 0.78f : 1.02f);
                    f.Ellipse(r, b.cx, b.headCy + b.headRy * 0.5f, b.headRx + 2f, b.headRy * 0.62f, curl);
                    for (int i = -4; i <= 4; i++)
                    {
                        float a = i / 4f * 1.9f;
                        f.Ellipse(r, b.cx + Mathf.Sin(a) * (b.headRx + 1.8f), b.headCy + 1f + Mathf.Cos(a) * (b.headRy * 0.8f), 3.8f, 3.4f, curl);
                    }
                    break;
                }
            }
        }

        static void HairBackView(Fig f, Spec s, Body b)
        {
            Paint hp = (x, y) => Mul(s.hairCol, 0.94f + N(x, y / 2, 3) * 0.12f);
            float nape = b.chinY + b.headRy * 0.22f;
            switch (s.hair)
            {
                case Hair.None:
                case Hair.Bald:
                    return;
                case Hair.BaldFringe:
                {
                    int r = f.Region(Mat.Hair);
                    f.Rows(r, b.chinY + 0.5f, b.headCy - b.headRy * 0.1f, y => Span(b.cx, b.HeadHalfBack(y) + 0.6f), hp);
                    return;
                }
                case Hair.Buzz:
                {
                    int r = f.Region(Mat.Hair);
                    f.Rows(r, nape, b.top + 0.6f, y => Span(b.cx, b.HeadHalf(Mathf.Min(y, b.top - 0.5f)) + 0.4f), (x, y) => Mix(s.hairCol, s.skin, N(x, y, 21) * 0.45f));
                    return;
                }
                case Hair.Perm:
                {
                    int r = f.Region(Mat.Hair);
                    Paint curl = (x, y) => Mul(s.hairCol, N(x, y, 33) > 0.62f ? 0.78f : 1.02f);
                    f.Ellipse(r, b.cx, b.headCy + 1f, b.headRx + 3f, b.headRy + 1.5f, curl);
                    return;
                }
                case Hair.Long:
                {
                    int r = f.Region(Mat.Hair);
                    float bottom = b.sh - 18f;
                    f.Rows(r, bottom, HairTopY(b), y => Span(b.cx, y > b.headCy ? b.HeadHalf(y) + 1.6f : b.headRx + 2.4f + (b.chinY - y) * 0.06f), hp);
                    return;
                }
                default:
                {
                    int r = f.Region(s.hair == Hair.Slick ? Mat.Shiny : Mat.Hair);
                    float low = s.hair == Hair.Bob || s.hair == Hair.TwinTails ? b.chinY + 1f : nape;
                    f.Rows(r, low, HairTopY(b), y => Span(b.cx, Mathf.Max(b.HeadHalfBack(Mathf.Min(y, b.top - 0.6f)) + 1.2f, y < b.top + 1f ? 2f : 0f)), s.hair == Hair.Slick ? Solid(s.hairCol) : hp);
                    if (s.hair == Hair.Ponytail)
                    {
                        int tail = f.Region(Mat.Hair);
                        f.Capsule(tail, new Vector2(b.cx, b.headCy + 1f), new Vector2(b.cx + 1f, b.sh - 10f), 3.4f, 1.8f, hp);
                    }
                    if (s.hair == Hair.TwinTails)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int tail = f.Region(Mat.Hair);
                            var a = new Vector2(b.cx + side * (b.headRx + 1.5f), b.headCy + b.headRy * 0.45f);
                            var m = new Vector2(b.cx + side * (b.shHalf + 6f), b.sh - 4f);
                            var e = new Vector2(b.cx + side * (b.shHalf + 4f), b.waistY - 4f);
                            f.Capsule(tail, a, m, 4.2f, 4.6f, hp);
                            f.Capsule(tail, m, e, 4.6f, 2.2f, hp);
                        }
                    if (s.hair == Hair.Pigtails)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int tail = f.Region(Mat.Hair);
                            f.Capsule(tail, new Vector2(b.cx + side * (b.headRx + 0.5f), b.headCy - 1f), new Vector2(b.cx + side * (b.headRx + 4.5f), b.headCy - 9f), 3f, 1.8f, hp);
                        }
                    return;
                }
            }
        }

        // ── 모자 ──
        static void HatRegions(Fig f, Spec s, Body b, bool back)
        {
            switch (s.hat)
            {
                case Hat.Fedora:
                {
                    int crown = f.Region();
                    float baseY = b.top - 2f;
                    f.Rows(crown, baseY, baseY + 9f, y => Span(b.cx, b.headRx + 0.8f - (y - baseY) * 0.18f), Solid(s.hatCol));
                    int brim = f.Region();
                    f.Ellipse(brim, b.cx, baseY - 0.5f, b.headRx + 7f, 2.3f, Solid(Mul(s.hatCol, 1.1f)));
                    break;
                }
                case Hat.Cap:
                {
                    int r = f.Region();
                    f.Rows(r, b.headCy + b.headRy * 0.35f, b.top + 2f, y => Span(b.cx, Mathf.Max(b.HeadHalf(Mathf.Min(y, b.top - 0.6f)) + 1.3f, y < b.top + 1.5f ? 3f : 0f)), Solid(s.hatCol));
                    if (!back)
                    {
                        int bill = f.Region();
                        f.Ellipse(bill, b.cx, b.headCy + b.headRy * 0.32f, b.headRx + 2.5f, 2f, Solid(s.hatCol2));
                    }
                    break;
                }
                case Hat.Helmet:
                {
                    int r = f.Region();
                    Paint camo = s.topPaint ?? Solid(s.hatCol);
                    f.Rows(r, b.headCy + b.headRy * 0.08f, b.top + 3.5f, y =>
                    {
                        float d = (y - (b.headCy + b.headRy * 0.1f)) / (b.headRy * 0.98f + 3.5f);
                        return Span(b.cx, (b.headRx + 3f) * Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)));
                    }, camo);
                    break;
                }
                case Hat.PoliceCap:
                {
                    int crown = f.Region();
                    float baseY = b.headCy + b.headRy * 0.42f;
                    f.Rows(crown, baseY, b.top + 3f, y => Span(b.cx, b.headRx + 1.6f + (y - baseY) * 0.12f), Solid(s.hatCol));
                    int band = f.Region();
                    f.Rows(band, baseY, baseY + 2.2f, y => Span(b.cx, b.headRx + 1.8f), Solid(Mul(s.hatCol, 0.6f)));
                    if (!back)
                    {
                        int visor = f.Region(Mat.Shiny);
                        f.Ellipse(visor, b.cx, baseY - 0.2f, b.headRx + 2.6f, 1.6f, Solid(C("#141418")));
                    }
                    break;
                }
                case Hat.Visor:
                {
                    int band = f.Region();
                    float y0 = b.headCy + b.headRy * 0.38f;
                    f.Rows(band, y0, y0 + 2.2f, y => Span(b.cx, b.HeadHalf(y) + 1.4f), Solid(s.hatCol));
                    if (!back)
                    {
                        int bill = f.Region();
                        f.Ellipse(bill, b.cx, y0 - 0.4f, b.headRx + 3.5f, 2.4f, Solid(s.hatCol2));
                    }
                    break;
                }
                case Hat.Bandana:
                {
                    int r = f.Region();
                    float y0 = b.headCy + b.headRy * 0.36f;
                    f.Rows(r, y0, y0 + 3.2f, y => Span(b.cx, b.HeadHalf(y) + 1.5f), (x, y) => (x + y) % 4 == 0 ? C("#F4EEE6") : s.hatCol);
                    if (back)
                    {
                        f.Capsule(r, new Vector2(b.cx, y0 + 1.5f), new Vector2(b.cx - 3f, y0 - 5f), 1.6f, 1.2f, Solid(s.hatCol));
                        f.Capsule(r, new Vector2(b.cx, y0 + 1.5f), new Vector2(b.cx + 3f, y0 - 6f), 1.6f, 1.2f, Solid(s.hatCol));
                    }
                    break;
                }
                case Hat.Hood:
                {
                    // 뒤집어쓴 우비 모자: 얼굴 둘레를 감싼다
                    int r = f.Region();
                    if (back)
                    {
                        f.Ellipse(r, b.cx, b.headCy + 0.5f, b.headRx + 3.4f, b.headRy + 2.4f, Solid(s.hatCol));
                    }
                    else
                    {
                        f.Rows(r, b.headCy + b.headRy * 0.45f, b.top + 2.5f, y => Span(b.cx, Mathf.Max(b.HeadHalf(Mathf.Min(y, b.top - 0.6f)) + 3f, y < b.top + 2f ? 4f : 0f)), Solid(s.hatCol));
                        f.Rows(r, b.chinY + 1f, b.headCy + b.headRy * 0.45f, y => new Vector2(b.cx - b.HeadHalf(y) - 3.4f, b.cx - b.HeadHalf(y) + 0.6f), Solid(s.hatCol));
                        f.Rows(r, b.chinY + 1f, b.headCy + b.headRy * 0.45f, y => new Vector2(b.cx + b.HeadHalf(y) - 0.6f, b.cx + b.HeadHalf(y) + 3.4f), Solid(s.hatCol));
                    }
                    break;
                }
            }
        }

        // ── 잔디테일 ─────────────────────────────────────────────
        static void DrawOverlay(Fig f, Spec s, Body b, bool back)
        {
            ClothDetails(f, s, b, back);
            if (!back) FaceDetails(f, s, b);
            HatDetails(f, s, b, back);
            s.overlay?.Invoke(f, b, back);
        }

        public static int Xi(float x) => Mathf.FloorToInt(x);

        static void FaceDetails(Fig f, Spec s, Body b)
        {
            if ((s.face & Face.Faceless) != 0) return;
            Color32 skinD = Mul(s.skin, 0.82f), skinDD = Mul(s.skin, 0.68f);
            Color32 lash = C("#241A18"), white = C("#F2EEE6"), irisD = Mul(s.iris, 0.55f);
            Color32 browC = Mix(Mul(s.hairCol, 0.75f), C("#2A201C"), 0.3f);
            if (s.hair == Hair.Bald || s.hair == Hair.BaldFringe) browC = Mul(s.skin, 0.55f);
            int ey = Mathf.RoundToInt(b.headCy - b.headRy * 0.12f);
            float eo = b.headRx * 0.56f;
            int ew = b.child ? 4 : 4;
            int lx = Mathf.RoundToInt(b.cx - eo - ew * 0.5f), rx = Mathf.RoundToInt(b.cx + eo - ew * 0.5f);
            bool covered = (s.face & (Face.Sunglasses)) != 0;
            for (int e = 0; e < 2; e++)
            {
                int x0 = e == 0 ? lx : rx;
                bool hidden = e == 0 && (s.face & Face.HairOverEye) != 0;
                if (hidden || covered) continue;
                switch (s.eyes)
                {
                    case Eyes.Closed:
                        f.HLine(x0, x0 + ew - 1, ey, lash);
                        break;
                    case Eyes.Narrow:
                        f.HLine(x0, x0 + ew - 1, ey + 1, lash);
                        f.Px(x0 + 1, ey, irisD); f.Px(x0 + 2, ey, irisD);
                        break;
                    case Eyes.Tired:
                        f.HLine(x0, x0 + ew - 1, ey + 1, skinDD);
                        f.HLine(x0, x0 + ew - 1, ey, lash);
                        f.Px(x0 + (e == 0 ? 2 : 1), ey, irisD);
                        f.Px(x0 + (e == 0 ? 1 : 2), ey - 1, Mul(s.skin, 0.88f));
                        break;
                    default:
                    {
                        bool wide = s.eyes == Eyes.Wide || b.child;
                        // 4x3 눈: 위 속눈썹, 가운데 흰자·홍채·동공, 아래 홍채 아랫부분
                        f.HLine(x0 - (e == 0 ? 1 : 0), x0 + ew - 1 + (e == 1 ? 1 : 0), ey + 1, lash);
                        f.Px(x0, ey, white); f.Px(x0 + ew - 1, ey, white);
                        f.Px(x0 + 1, ey, e == 0 ? s.iris : irisD);
                        f.Px(x0 + 2, ey, e == 0 ? irisD : s.iris);
                        f.Px(x0 + 1, ey - 1, Mul(s.iris, 0.8f)); f.Px(x0 + 2, ey - 1, Mul(s.iris, 0.8f));
                        f.Px(x0, ey - 1, Mul(s.skin, 0.9f)); f.Px(x0 + ew - 1, ey - 1, Mul(s.skin, 0.9f));
                        if (wide)
                        {
                            f.Px(x0, ey - 1, white); f.Px(x0 + ew - 1, ey - 1, white);
                            f.Px(x0 + 1, ey - 1, s.iris); f.Px(x0 + 2, ey - 1, s.iris);
                            f.Px(x0 + 1, ey, C("#FFFFFF"));
                        }
                        break;
                    }
                }
                // 눈썹
                int by = ey + 3;
                int inner = e == 0 ? x0 + ew - 1 : x0, outer = e == 0 ? x0 : x0 + ew - 1;
                f.HLine(x0, x0 + ew - 1, by, browC);
                if (s.brow == Brow.Angry) { f.Px(inner, by, Mul(s.skin, 1f)); f.Px(inner, by - 1, browC); f.Px(outer, by + 1, browC); }
                if (s.brow == Brow.Worried) { f.Px(outer, by, Mul(s.skin, 1f)); f.Px(outer, by - 1, browC); f.Px(inner, by + 1, browC); }
                if (s.brow == Brow.Raised) { f.HLine(x0, x0 + ew - 1, by, Mul(s.skin, 1f)); f.HLine(x0, x0 + ew - 1, by + 1, browC); }
            }

            // 코
            int cxL = Mathf.FloorToInt(b.cx - 0.5f), cxR = cxL + 1;
            f.Px(cxR, ey - 2, skinD); f.Px(cxR, ey - 3, skinD);
            f.Px(cxL, ey - 4, skinDD); f.Px(cxR + 1, ey - 4, skinD);

            // 입
            int my = Mathf.RoundToInt(b.headCy - b.headRy * 0.62f);
            Color32 lip = (s.face & Face.Lipstick) != 0 ? C("#A8283A") : Mix(skinDD, C("#7A3A34"), 0.4f);
            switch (s.mouth)
            {
                case Mouth.Smile:
                    f.HLine(cxL - 1, cxR + 1, my, lip); f.Px(cxL - 2, my + 1, lip); f.Px(cxR + 2, my + 1, lip);
                    break;
                case Mouth.Frown:
                    f.HLine(cxL - 1, cxR + 1, my, lip); f.Px(cxL - 2, my - 1, lip); f.Px(cxR + 2, my - 1, lip);
                    break;
                case Mouth.Open:
                    f.HLine(cxL - 1, cxR + 1, my, C("#3A1616")); f.HLine(cxL, cxR, my - 1, C("#3A1616")); f.HLine(cxL - 1, cxR + 1, my + 1, lip);
                    break;
                case Mouth.Grin:
                    f.HLine(cxL - 2, cxR + 2, my + 1, lip); f.HLine(cxL - 1, cxR + 1, my, C("#F2EEE6")); f.HLine(cxL - 1, cxR + 1, my - 1, lip);
                    break;
                default:
                    f.HLine(cxL - 1, cxR + 1, my, lip);
                    break;
            }
            if ((s.face & Face.Lipstick) != 0) f.HLine(cxL, cxR, my - 1, Mul(C("#A8283A"), 0.85f));

            if ((s.face & Face.Eyeshadow) != 0)
                for (int e = 0; e < 2; e++) { int x0 = e == 0 ? lx : rx; f.HLine(x0, x0 + ew - 1, ey + 2, C("#5A3A6A")); }
            if ((s.face & Face.Blush) != 0)
            {
                f.Px(lx, ey - 2, C("#E88A8A")); f.Px(lx + 1, ey - 2, C("#E88A8A"));
                f.Px(rx + 2, ey - 2, C("#E88A8A")); f.Px(rx + 3, ey - 2, C("#E88A8A"));
            }
            if ((s.face & Face.Flush) != 0)
            {
                var red = new Color32(214, 70, 60, 120);
                for (int x = lx - 1; x <= rx + ew; x++) { f.On(x, ey - 2, red); f.On(x, ey - 3, red); }
            }
            if ((s.face & Face.Stubble) != 0)
                for (int y = Mathf.FloorToInt(b.chinY); y <= my - 1; y++)
                    for (int x = Xi(b.cx - b.headRx); x <= Xi(b.cx + b.headRx); x++)
                        if (f.RegAt(x, y) != 0 && N(x, y, 61) > 0.55f && Mathf.Abs(x + 0.5f - b.cx) < b.HeadHalf(y + 0.5f) - 0.5f)
                            f.Px(x, y, Mix(s.skin, s.hairCol, 0.45f));
            if ((s.face & Face.Mustache) != 0)
            {
                f.HLine(cxL - 2, cxR + 2, my + 1, Mul(s.hairCol, 0.9f));
                f.HLine(cxL - 1, cxR + 1, my + 2, s.hairCol);
            }
            if ((s.face & Face.Wrinkles) != 0)
            {
                Color32 w = Mul(s.skin, 0.8f);
                f.HLine(cxL - 3, cxL - 1, ey + 6, w); f.HLine(cxR + 1, cxR + 3, ey + 6, w);
                f.Px(lx - 1, ey - 1, w); f.Px(rx + ew, ey - 1, w);
                f.Px(cxL - 3, my + 2, w); f.Px(cxL - 3, my + 1, w); f.Px(cxR + 3, my + 2, w); f.Px(cxR + 3, my + 1, w);
            }
            if ((s.face & Face.Scar) != 0)
                f.Line(rx + 1, ey + 3, rx + 4, ey - 4, C("#C9776E"), true);
            if ((s.face & Face.Sweat) != 0)
            {
                f.Px(rx + 4, ey + 5, C("#BFE3F2")); f.Px(rx + 4, ey + 4, C("#E8F6FC"));
                f.Px(lx - 2, ey + 2, C("#BFE3F2"));
            }
            if ((s.face & (Face.Glasses | Face.Foggy | Face.Sunglasses)) != 0)
            {
                Color32 frame = s.glassesCol;
                for (int e = 0; e < 2; e++)
                {
                    int x0 = (e == 0 ? lx : rx) - 1, x1 = x0 + ew + 1, y0 = ey - 2, y1 = ey + 2;
                    if ((s.face & Face.Foggy) != 0 || (s.face & Face.Sunglasses) != 0)
                        for (int y = y0; y <= y1; y++)
                            for (int x = x0; x <= x1; x++)
                                f.Px(x, y, (s.face & Face.Sunglasses) != 0 ? C("#121216") : new Color32(232, 238, 240, 235));
                    f.HLine(x0, x1, y1, frame); f.HLine(x0, x1, y0, frame);
                    f.VLine(x0, y0, y1, frame); f.VLine(x1, y0, y1, frame);
                    if ((s.face & Face.Foggy) != 0) { f.Px(x0 + 1, y1 - 1, C("#FFFFFF")); f.Px(x0 + 2, y1 - 1, C("#FFFFFF")); }
                    if ((s.face & Face.Sunglasses) != 0) f.Px(x0 + 1, y1 - 1, C("#5A6A7A"));
                }
                f.HLine(lx + ew + 1, rx - 2, ey + 1, frame);
            }
        }

        static void ClothDetails(Fig f, Spec s, Body b, bool back)
        {
            Color32 dark = Mul(s.topCol, 0.62f);
            int cx = Mathf.FloorToInt(b.cx);
            switch (s.top)
            {
                case Top.Suit:
                case Top.LoudShirtSuit:
                case Top.Blazer:
                case Top.Police:
                    if (back) { if (s.top != Top.Police) f.VLine(cx, Xi(b.hipY), Xi(b.waistY - 2f), dark); break; }
                    // 단추
                    f.On(cx, Xi(b.waistY + 3f), Mul(s.topCol, 0.45f));
                    f.On(cx, Xi(b.waistY + 9f), Mul(s.topCol, 0.45f));
                    if (s.top == Top.Police)
                    {
                        f.VLine(cx, Xi(b.hipY), Xi(b.sh - 3f), dark);
                        // 가슴 배지(금색)와 어깨 견장
                        int bx = Xi(b.cx - b.chestHalf * 0.5f), by = Xi(b.sh - 9f);
                        f.Px(bx, by, C("#E2C25A")); f.Px(bx + 1, by, C("#C9A43A")); f.Px(bx, by - 1, C("#C9A43A")); f.Px(bx + 1, by - 1, C("#A8862A"));
                        f.HLine(Xi(b.cx + b.chestHalf * 0.3f), Xi(b.cx + b.chestHalf * 0.3f) + 4, by, C("#E8E6DE"));
                        f.HLine(Xi(b.cx - b.shHalf + 2f), Xi(b.cx - b.shHalf + 7f), Xi(b.sh - 1f), C("#14182A"));
                        f.HLine(Xi(b.cx + b.shHalf - 7f), Xi(b.cx + b.shHalf - 2f), Xi(b.sh - 1f), C("#14182A"));
                    }
                    break;
                case Top.Cardigan:
                    if (back) break;
                    for (int i = 0; i < 4; i++) f.On(cx - 5, Xi(b.sh - 6f - i * 6f), Mul(s.topCol, 0.5f));
                    break;
                case Top.Military:
                    // 허리띠
                    f.HLine(Xi(b.cx - b.waistHalf), Xi(b.cx + b.waistHalf) - 1, Xi(b.waistY), C("#2A2A22"));
                    f.HLine(Xi(b.cx - b.waistHalf), Xi(b.cx + b.waistHalf) - 1, Xi(b.waistY) + 1, C("#3A3A2E"));
                    if (!back)
                    {
                        f.Px(cx - 1, Xi(b.waistY), C("#8A8A70")); f.Px(cx, Xi(b.waistY), C("#8A8A70"));
                        f.VLine(cx, Xi(b.waistY + 2f), Xi(b.sh - 3f), Mul(C("#3E4A33"), 0.8f));
                    }
                    break;
                case Top.Puffer:
                    for (int y = Xi(b.hipY) + 3; y < Xi(b.sh) - 2; y += 6)
                        for (int x = Xi(b.cx - b.TorsoHalf(y)) + 1; x < Xi(b.cx + b.TorsoHalf(y)) - 1; x++) f.On(x, y, Mul(s.topCol, 0.75f));
                    if (!back) f.VLine(cx, Xi(b.hipY), Xi(b.sh - 1f), C("#1A1A1E"));
                    break;
                case Top.Vest:
                    if (back) break;
                    for (int y = Xi(b.hipY) + 4; y < Xi(b.sh) - 3; y += 5)
                        for (int x = Xi(b.cx - b.TorsoHalf(y)) + 1; x < Xi(b.cx + b.TorsoHalf(y)) - 1; x++)
                            if (Mathf.Abs(x + 0.5f - b.cx) > 1.6f) f.On(x, y, Mul(s.topCol2, 0.75f));
                    break;
                case Top.Raincoat:
                    if (back) break;
                    f.VLine(cx, Xi(b.kneeY), Xi(b.sh - 1f), Mul(s.topCol, 0.7f));
                    for (int i = 0; i < 4; i++) f.On(cx + 2, Xi(b.sh - 5f - i * 8f), C("#F4EEE0"));
                    break;
                case Top.Hanbok:
                {
                    if (back) break;
                    // 흰 동정(목깃)이 오른쪽 어깨에서 왼쪽 가슴으로, 옷고름 두 가닥
                    Color32 collar = C("#F4F1E8");
                    for (int i = 0; i < 14; i++)
                    {
                        int x = Xi(b.cx + 4f - i * 0.75f), y = Xi(b.sh - i);
                        f.On(x, y, collar); f.On(x + 1, y, collar);
                    }
                    Color32 ribbon = s.topCol3;
                    f.Line(cx - 4, Xi(b.sh - 14f), cx - 2, Xi(b.sh - 30f), ribbon, true);
                    f.Line(cx - 5, Xi(b.sh - 14f), cx - 6, Xi(b.sh - 27f), ribbon, true);
                    break;
                }
                case Top.Robe:
                    if (back) break;
                    // 초승달 목걸이
                    f.Px(cx - 1, Xi(b.sh - 7f), C("#D8D2B8")); f.Px(cx, Xi(b.sh - 8f), C("#D8D2B8")); f.Px(cx - 1, Xi(b.sh - 9f), C("#D8D2B8"));
                    f.Px(cx - 2, Xi(b.sh - 8f), C("#9A2A3A"));
                    break;
                case Top.Frilly:
                    if (back) break;
                    // 레이스 장식
                    for (int x = Xi(b.cx - b.hipHalf - 6f); x <= Xi(b.cx + b.hipHalf + 6f); x++)
                        if (x % 2 == 0) f.On(x, Xi(b.hipY - 6f) + 1, C("#FFFFFF"));
                    for (int x = Xi(b.cx - b.TorsoHalf(b.sh - 2f)) + 1; x < Xi(b.cx + b.TorsoHalf(b.sh - 2f)) - 1; x++)
                        if (x % 2 == 0) f.On(x, Xi(b.sh - 3f), C("#FFFFFF"));
                    break;
            }

            // 바지 솔기·청바지
            if (s.bottom == Bottom.Jeans && !LongGarment(s.top))
                for (int side = -1; side <= 1; side += 2)
                {
                    int x = Xi(b.cx + side * b.hipHalf * 0.45f) + (side > 0 ? 2 : -2);
                    for (int y = Xi(b.ankleY) + 1; y < Xi(b.hipY); y += 1) if (N(x, y, 71) > 0.5f) f.On(x, y, Mul(s.bottomCol, 1.18f));
                }

            // 운동화 밑창
            if (s.shoes == Shoes.Sneakers)
                for (int side = -1; side <= 1; side += 2)
                {
                    float ankX = b.cx + side * (b.hipHalf * 0.42f + 0.5f) + side * 0.7f;
                    float w = b.ankleHalf + 2.4f;
                    f.HLine(Xi(ankX - w) + 1, Xi(ankX + w) - 1, Xi(b.feet) + 1, C("#B9B9B2"));
                }
            if (s.shoes == Shoes.Slippers && !back)
                for (int side = -1; side <= 1; side += 2)
                {
                    float x0 = b.cx + side * (b.hipHalf * 0.42f + 0.5f) + side * 0.5f;
                    for (int k = -1; k <= 1; k++) f.VLine(Xi(x0) + k * 2, Xi(b.feet) + 2, Xi(b.feet) + 3, C("#F4F4F0"));
                }
        }

        static void HatDetails(Fig f, Spec s, Body b, bool back)
        {
            switch (s.hat)
            {
                case Hat.Fedora:
                    f.HLine(Xi(b.cx - b.headRx), Xi(b.cx + b.headRx) - 1, Xi(b.top - 1f), Mul(s.hatCol2, 1f));
                    break;
                case Hat.PoliceCap:
                    if (back) break;
                    int by = Xi(b.headCy + b.headRy * 0.42f) + 4;
                    f.Px(Xi(b.cx) - 1, by, C("#E2C25A")); f.Px(Xi(b.cx), by, C("#E2C25A")); f.Px(Xi(b.cx) - 1, by + 1, C("#C9A43A")); f.Px(Xi(b.cx), by + 1, C("#C9A43A"));
                    break;
                case Hat.Cap:
                    if (back) break;
                    f.Px(Xi(b.cx) - 1, Xi(b.headCy + b.headRy * 0.6f), Mul(s.hatCol2, 1.3f)); f.Px(Xi(b.cx), Xi(b.headCy + b.headRy * 0.6f), Mul(s.hatCol2, 1.3f));
                    break;
            }
        }

        // ── 게임에 들어갈 손님 목록 ─────────────────────────────
        public struct Entry
        {
            public string id, label;
            public float heightM;
            public int party;
        }

        /// <summary>
        /// 게임에 넣을 손님 전원: id(ArtDrafts/customers 폴더), 영수증의 손님 설명, 키(CCTV 크기), 사람 수.
        /// 그림은 PixelLab으로 만든 것이든 코드로 그린 것이든 같은 꼴(128px, south·north)이라 그대로 바꿔 끼울 수 있다.
        /// </summary>
        public static List<Entry> Catalog()
        {
            var list = new List<Entry>
            {
                new Entry { id = "hoodie", label = "후드 청년", heightM = 1.72f, party = 1 },
                new Entry { id = "faceless_fedora", label = "중절모 정장남", heightM = 1.80f, party = 1 },
                // 06:00 교대 알바생(사용자가 그려 온 그림, ArtDrafts/customers/worker). 손님으로는 오지 않는다(CheckoutSystem.shiftWorkerId)
                new Entry { id = "worker", label = "아침 알바생", heightM = 1.74f, party = 1 },
            };
            foreach (Spec s in Roster())
                list.Add(new Entry
                {
                    id = s.id, label = s.label,
                    heightM = s.partner != null ? Mathf.Max(s.heightM, s.partner.heightM) : s.heightM,
                    party = s.partner != null ? 2 : 1,
                });
            return list;
        }

        // ── 일괄 생성 ───────────────────────────────────────────
        static string DraftRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "customers"));

        /// <summary>배치 모드용: 손님 전원을 그려 ArtDrafts/customers/{id}/south.png·north.png로 저장하고, -previewDir에 한눈에 보는 그림을 만든다.</summary>
        public static void RenderBatch()
        {
            string dir = StoreSceneBuilder.Arg("-previewDir");
            string only = StoreSceneBuilder.Arg("-only");
            var specs = Roster();
            var fronts = new List<Color32[]>();
            var backs = new List<Color32[]>();
            // 비교용: PixelLab으로 만든 기존 두 명
            foreach (string id in new[] { "hoodie", "faceless_fedora" })
            {
                fronts.Add(LoadPng(Path.Combine(DraftRoot, id, "south.png")));
                backs.Add(LoadPng(Path.Combine(DraftRoot, id, "north.png")));
            }
            foreach (Spec s in specs)
            {
                if (!string.IsNullOrEmpty(only) && !only.Contains(s.id)) continue;
                Fig front = Render(s, false), back = Render(s, true);
                // 코드 그림은 *_drawn.png로. 게임이 쓰는 south/north.png는 비어 있을 때만 채운다
                // (사용자가 가져온 그림이나 PixelLab 그림을 덮어쓰지 않게)
                string idDir = Path.Combine(DraftRoot, s.id);
                front.Save(Path.Combine(idDir, "south_drawn.png"));
                back.Save(Path.Combine(idDir, "north_drawn.png"));
                if (!File.Exists(Path.Combine(idDir, "south.png"))) front.Save(Path.Combine(idDir, "south.png"));
                if (!File.Exists(Path.Combine(idDir, "north.png"))) back.Save(Path.Combine(idDir, "north.png"));
                fronts.Add(front.img);
                backs.Add(back.img);
            }
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                SaveSheet(fronts, 6, 2, Path.Combine(dir, "customers_front.png"));
                SaveSheet(backs, 6, 2, Path.Combine(dir, "customers_back.png"));
                SaveSheet(fronts, 6, 3, Path.Combine(dir, "customers_front_x3.png"), upperOnly: true);
            }
            Debug.Log($"[0333] 손님 그림 {specs.Count}명 저장: {DraftRoot}");
        }

        static Color32[] LoadPng(string path)
        {
            if (!File.Exists(path)) return new Color32[S * S];
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(path));
            Color32[] px = t.width == S && t.height == S ? t.GetPixels32() : new Color32[S * S];
            UnityEngine.Object.DestroyImmediate(t);
            return px;
        }

        /// <summary>여러 명을 격자로 늘어놓은 그림. upperOnly면 계산대 위로 보이는 윗부분(머리~허리)만 크게.</summary>
        static void SaveSheet(List<Color32[]> imgs, int cols, int scale, string path, bool upperOnly = false)
        {
            int cellH = upperOnly ? 64 : S, y0 = upperOnly ? S - 64 : 0;
            int rows = (imgs.Count + cols - 1) / cols;
            int w = cols * S * scale, h = rows * cellH * scale;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(62, 66, 70, 255);
            for (int k = 0; k < imgs.Count; k++)
            {
                int cx = k % cols, cy = rows - 1 - k / cols;
                for (int y = 0; y < cellH; y++)
                    for (int x = 0; x < S; x++)
                    {
                        Color32 c = imgs[k][(y + y0) * S + x];
                        Color32 bg = ((x / 8 + y / 8) % 2 == 0) ? new Color32(70, 74, 78, 255) : new Color32(62, 66, 70, 255);
                        Color32 o = c.a > 0 ? Color32.Lerp(bg, new Color32(c.r, c.g, c.b, 255), c.a / 255f) : bg;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                px[((cy * cellH + y) * scale + sy) * w + (cx * S + x) * scale + sx] = o;
                    }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
