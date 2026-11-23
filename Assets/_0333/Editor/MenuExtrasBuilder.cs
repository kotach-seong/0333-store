using System.IO;
using UnityEditor;
using UnityEngine;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 메인 화면(주차장 CCTV)에 덧붙이는 것들. 모두 메인 화면 카메라(G.MenuCam)로 본 자리에 맞춰 놓는다.
    ///  - 진행: 창밖 검은 형체(흔적 1+), 안쪽 유리의 피(흔적 2+), 실종 전단(경찰이 다녀간 수), 계산대 앞 괴물(죽은 적이 있으면)
    ///  - 가만히 두면: 주차장에 잿빛 시체 6구가 하나씩, 마지막엔 카메라 바로 앞 얼굴
    ///  - 시작할 때 확대될 문 자리
    /// 그림은 ArtDrafts/cctv_odd(밖 귀신들), Art/Fate/monster.png를 CCTV처럼 흑백으로 줄여 쓴다.
    /// </summary>
    public static partial class MenuExtrasBuilder
    {
        const string Dest = "Assets/_0333/Art/Menu";
        const int Cam = G.MenuCam;
        const int W = 640, H = 360;

        static Vector2 Px(Vector3 p)
        {
            Vector2 uv = G.CamUV(Cam, p);
            return new Vector2(Mathf.Round(uv.x * W - W / 2f), Mathf.Round(uv.y * H - H / 2f));
        }

        static float HeightPx(Vector3 feet, float meters) => (G.CamUV(Cam, feet + Vector3.up * meters).y - G.CamUV(Cam, feet).y) * H;

        static string OddPath(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "cctv_odd", name + ".png"));

        public static void Build(MainMenu menu, Transform scene, Material mat)
        {
            Directory.CreateDirectory(Dest);
            menu.doorPoint = Px(new Vector3(0f, 1.15f, 8f));

            // 흔적 1+: 오른쪽 창 앞에 등지고 선 검은 형체
            menu.traceFigure = Figure("Menu Trace Figure", scene, mat, OddPath("watcher1_back"),new Vector3(1.75f, 0f, 9.0f), 1.82f, 401, -0.03f, silhouette: true, file: "trace_figure");

            // 흔적 2+: 안쪽 유리(창 두 개와 문)에 묻은 피
            menu.bloodGlass = NewSr("Menu Blood Glass", scene, new Vector3(-W / 2f, -H / 2f, -0.01f), mat, 401);
            menu.bloodGlass.sprite = Save(GlassBlood(), "blood_glass", Vector2.zero);
            menu.bloodGlass.enabled = false;

            // 실종 전단: 왼쪽 창 바깥 면에 붙는다(최대 3장)
            menu.flyers = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = new Vector3(-2.2f + i * 0.42f, 1.45f - (i % 2) * 0.12f, 8.32f);
                menu.flyers[i] = NewSr($"Menu Flyer {i + 1}", scene, Px(c), mat, 401, -0.02f);
                menu.flyers[i].sprite = Save(Flyer(c, i), $"flyer_{i + 1}", new Vector2(0.5f, 0.5f));
                menu.flyers[i].enabled = false;
            }

            // 죽은 적이 있으면: 계산대 앞(문 유리 너머)에 그 괴물
            string monsterPath = Path.GetFullPath("Assets/_0333/Art/Fate/monster.png");
            if (File.Exists(monsterPath))
            {
                var feet = new Vector3(0f, 0f, 2.2f);
                Texture2D src = CustomerBuilder.LoadPng(monsterPath);
                Sprite s = CustomerBuilder.MakeCctvSprite(src, HeightPx(feet, 2.0f), -1f, $"{Dest}/monster.png", true);
                Object.DestroyImmediate(src);
                Vector2 at = Px(feet);
                s = CustomerBuilder.MaskBySight(s, at, new Rect(-W / 2f, -H / 2f, W, H), Cam, feet.z);
                menu.monster = NewSr("Menu Monster", scene, at, mat, 401);
                menu.monster.sprite = s;
                menu.monster.enabled = false;
            }

            // 가만히 두면 모여드는 것들: 카메라(나)가 아니라 가게 창문을 본다. 어두운 실루엣이라 아주 약간만 보인다.
            // 창에 서서 붙은 것, 무릎 꿇고 몸을 붙인 것, 거꾸로 붙은 것, 기어서 들어가려는 것.
            // 이 카메라는 가게 쪽을 보므로 월드 +x가 화면 왼쪽(창 x 1.11~2.49가 화면 왼쪽 창, 문 x -0.74~0.74).
            // 머리가 아래인 그림(거꾸로)은 feet 자리가 머리 끝이다.
            // grounded: 바닥에 닿아 있다(접지 그림자·젖은 바닥 반사). fog: 몸 높이(0 아래~1 위) 중 유리에 닿은 부분(김서림), 음수면 없음.
            var spots = new (string art, Vector3 feet, float meters, bool flip, bool grounded, float fogLo, float fogHi)[]
            {
                ("kneel_a",    new Vector3(2.12f, 0f, 8.42f),     1.32f, false, true,  0.55f, 1f),    // 화면 왼쪽 창: 무릎 꿇고 붙음
                ("stand_b",    new Vector3(1.42f, 0f, 8.42f),     1.74f, false, true,  0.6f,  1f),    // 화면 왼쪽 창: 서서 붙음
                ("kneel_b",    new Vector3(-1.42f, 0f, 8.42f),    1.32f, true,  true,  0.55f, 1f),    // 화면 오른쪽 창: 무릎 꿇고 붙음(쓰레기통에 조금 가린다)
                ("spider",     new Vector3(-2.05f, 0.95f, 8.4f),  1.30f, false, false, 0f,    1f),    // 화면 오른쪽 창 유리에 거꾸로(머리가 아래)
                ("stand_a",    new Vector3(0.5f, 0f, 8.45f),      1.70f, false, true,  0.6f,  1f),    // 문 유리(화면 왼쪽 짝)에 서서 붙음
                ("hang",       new Vector3(-0.38f, 1.3f, 8.52f),  1.62f, false, false, 0f,    0.32f), // 문 위 처마에 거꾸로 매달림(머리가 아래)
                ("crawl",      new Vector3(0.85f, 0f, 10.6f),     0.95f, false, true,  -1f,   -1f),   // 주차장에서 문 쪽으로 기어감
                ("crawl_door", new Vector3(0f, 0f, 8.36f),        0.78f, false, true,  -1f,   -1f),   // 벌어진 문틈으로 반쯤 들어감
            };

            // 문틈으로 들어가는 것이 나타날 때 바꿔 끼우는 배경: 자동문이 살짝 벌어진 그림
            string doorBg = $"{Dest}/menu_door_open.png";
            G.MenuDoorGap = 0.42f;
            try { G.RenderMenuBackground().SavePng(doorBg); }
            finally { G.MenuDoorGap = 0f; }
            AssetDatabase.ImportAsset(doorBg, ImportAssetOptions.ForceUpdate);
            StoreSceneBuilder.ConfigureSprite(doorBg);
            menu.backgroundDoorOpen = AssetDatabase.LoadAssetAtPath<Sprite>(doorBg);
            Color32[] bgShut = LoadPixels($"{StoreSceneBuilder.ArtDir}/{G.MenuBackgroundName}.png"), bgOpen = LoadPixels(doorBg);

            // 몸은 배경과 같은 빛으로 칠하고(아주 어둡게), 그림자·반사·김서림은 몸 바로 뒤에 한 장 더
            var feed = new Rect(-W / 2f, -H / 2f, W, H);
            menu.watchers = new SpriteRenderer[spots.Length];
            menu.watcherTurn = new Sprite[spots.Length];
            menu.watcherShade = new SpriteRenderer[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var sp = spots[i];
                Color32[] bg = sp.art == "crawl_door" ? bgOpen : bgShut;
                float z = -0.04f - (sp.feet.z - 8f) * 0.004f - i * 0.0005f;   // 카메라에 가까울수록 앞에
                Vector2 at = Px(sp.feet);

                Placed face = Place(MonsterPath(sp.art), sp.feet, sp.meters, sp.flip, false);
                Placed turn = Place(MonsterPath(sp.art + "_turn"), sp.feet, sp.meters, sp.flip, true);
                var sr = NewSr($"Menu Watcher {i + 1} {sp.art}", scene, at, mat, 401, z);
                sr.enabled = false;
                // 쓰레기통처럼 앞에 있는 것에 가리는 곳은 지운다
                sr.sprite = CustomerBuilder.MaskBySight(LitSprite(face, sp.feet, bg, $"watcher_{sp.art}"), at, feed, Cam, sp.feet.z);
                menu.watchers[i] = sr;
                // 마지막에 다 같이 고개를 홱 돌려 카메라를 본다: 눈만 하얗게 빛난다
                menu.watcherTurn[i] = CustomerBuilder.MaskBySight(LitSprite(turn, sp.feet, bg, $"watcher_{sp.art}_turn"), at, feed, Cam, sp.feet.z);

                Sprite shade = ShadeSprite(face, sp.feet, sp.grounded, sp.fogLo, sp.fogHi, $"watcher_{sp.art}_shade", out Vector2 shadeAt);
                if (shade != null)
                {
                    var ssr = NewSr($"Menu Watcher {i + 1} {sp.art} Shade", scene, shadeAt, mat, 401, z + 0.0002f);
                    ssr.sprite = shade;
                    ssr.enabled = false;
                    menu.watcherShade[i] = ssr;
                }
            }
            menu.doorWatcher = System.Array.FindIndex(spots, s => s.art == "crawl_door");
            menu.idleTurn = 6f;
            menu.idleCloseUp = 2.4f;

            // 카메라 바로 앞에 들이민 얼굴(화면을 거의 가린다)
            // 얼굴이 화면 가운데 오도록: 카메라 쪽으로 다가와 선 키 1.85m(발은 화면 아래로 잘린다)
            menu.closeUp = Figure("Menu Close Up", scene, mat, OddPath("watcher2_front"), new Vector3(0.7f, 0.35f, 14.0f), 1.85f, 401, -0.09f,
                                  silhouette: false, file: "close_up");
        }

        static string MonsterPath(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "menu_monsters", name + ".png"));

        /// <summary>
        /// 밖 귀신 그림 하나를 CCTV처럼 줄여 발 자리에 세운다. silhouette이면 새까맣게.
        /// dark가 1보다 작으면 그만큼 어둡게, glow면 새하얀 눈은 어둡게 하지 않고 남긴다.
        /// </summary>
        static SpriteRenderer Figure(string name, Transform parent, Material mat, string path, Vector3 feet, float meters, int order, float z,
                                     bool silhouette, string file, bool flip = false, bool glow = true, float dark = 1f)
        {
            var sr = NewSr(name, parent, Px(feet), mat, order, z);
            sr.enabled = false;
            sr.flipX = flip;
            if (!File.Exists(path)) { Debug.LogWarning("[0333] 메인 화면 그림이 없음: " + path); return sr; }
            Texture2D src = CustomerBuilder.LoadPng(path);
            if (silhouette)
            {
                Color32[] px = src.GetPixels32();
                for (int i = 0; i < px.Length; i++) if (px[i].a > 40) px[i] = new Color32(6, 6, 8, 255);
                src.SetPixels32(px);
                src.Apply();
            }
            sr.sprite = CustomerBuilder.MakeCctvSprite(src, HeightPx(feet, meters), -1f, $"{Dest}/{file}.png", glow, dark);
            Object.DestroyImmediate(src);
            return sr;
        }

        // ── 유리의 피 ──
        /// <summary>창 두 개와 문 유리 안쪽 면에 튄 피·흘러내림·미끄러진 손자국(메인 화면 640x360 한 장).</summary>
        static Texture2D GlassBlood()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            var rnd = new System.Random(3303);
            // 유리 사각형(가게 정면 안쪽 면, z = 8)
            var panes = new[]
            {
                (x0: -2.49f, x1: -1.83f, y0: 0.76f, y1: 2.44f), (x0: -1.77f, x1: -1.11f, y0: 0.76f, y1: 2.44f),
                (x0: 1.11f, x1: 1.77f, y0: 0.76f, y1: 2.44f), (x0: 1.83f, x1: 2.49f, y0: 0.76f, y1: 2.44f),
                (x0: -0.74f, x1: -0.06f, y0: 0.16f, y1: 2.22f), (x0: 0.06f, x1: 0.74f, y0: 0.16f, y1: 2.22f),
            };
            foreach (var p in panes)
            {
                Vector2 a = Px(new Vector3(p.x0, p.y0, 8f)) + new Vector2(W / 2f, H / 2f);
                Vector2 b = Px(new Vector3(p.x1, p.y1, 8f)) + new Vector2(W / 2f, H / 2f);
                var r = new RectInt(Mathf.RoundToInt(Mathf.Min(a.x, b.x)), Mathf.RoundToInt(Mathf.Min(a.y, b.y)),
                                    Mathf.RoundToInt(Mathf.Abs(b.x - a.x)), Mathf.RoundToInt(Mathf.Abs(b.y - a.y)));
                if (r.width < 3 || r.height < 3) continue;
                int splats = 3 + rnd.Next(4);
                for (int i = 0; i < splats; i++)
                {
                    float cx = r.x + (float)rnd.NextDouble() * r.width, cy = r.y + r.height * (0.35f + 0.6f * (float)rnd.NextDouble());
                    float rad = 1.5f + (float)rnd.NextDouble() * Mathf.Min(5f, r.width * 0.25f);
                    Splat(px, r, cx, cy, rad, rnd);
                    int drips = 1 + rnd.Next(3);
                    for (int d = 0; d < drips; d++) Drip(px, r, cx + (float)(rnd.NextDouble() * 2 - 1) * rad, cy - rad * 0.6f, 5 + rnd.Next(Mathf.Max(6, r.height / 2)), rnd);
                }
                if (rnd.NextDouble() < 0.8) Hand(px, r, r.x + r.width * (0.25f + 0.5f * (float)rnd.NextDouble()), r.y + r.height * (0.45f + 0.35f * (float)rnd.NextDouble()), rnd);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Color32 BloodColor(System.Random rnd)
        {
            switch (rnd.Next(4))
            {
                case 0: return new Color32(58, 10, 12, 235);
                case 1: return new Color32(86, 16, 18, 230);
                case 2: return new Color32(112, 22, 24, 220);
                default: return new Color32(40, 8, 10, 240);
            }
        }

        static void Put(Color32[] px, RectInt r, int x, int y, Color32 c)
        {
            if (x < r.xMin || x >= r.xMax || y < r.yMin || y >= r.yMax || x < 0 || y < 0 || x >= W || y >= H) return;
            px[y * W + x] = c;
        }

        static void Splat(Color32[] px, RectInt r, float cx, float cy, float rad, System.Random rnd)
        {
            Color32 c = BloodColor(rnd);
            for (int y = Mathf.FloorToInt(cy - rad - 2); y <= cy + rad + 2; y++)
                for (int x = Mathf.FloorToInt(cx - rad - 2); x <= cx + rad + 2; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy) * 1.3f);
                    float edge = rad * (0.75f + 0.35f * Mathf.PerlinNoise(x * 0.7f, y * 0.7f));
                    if (d <= edge) Put(px, r, x, y, (x + y) % 5 == 0 ? BloodColor(rnd) : c);
                }
            // 튄 방울
            int n = 3 + rnd.Next(6);
            for (int i = 0; i < n; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = rad * (1.3f + (float)rnd.NextDouble() * 1.4f);
                Put(px, r, Mathf.RoundToInt(cx + Mathf.Cos(ang) * dist), Mathf.RoundToInt(cy + Mathf.Sin(ang) * dist), c);
            }
        }

        static void Drip(Color32[] px, RectInt r, float x, float y, int len, System.Random rnd)
        {
            Color32 c = BloodColor(rnd);
            int xi = Mathf.RoundToInt(x);
            for (int i = 0; i < len; i++)
            {
                Put(px, r, xi, Mathf.RoundToInt(y) - i, c);
                if (rnd.NextDouble() < 0.06) xi += rnd.Next(2) == 0 ? -1 : 1;
            }
            Put(px, r, xi, Mathf.RoundToInt(y) - len, c);
            Put(px, r, xi + 1, Mathf.RoundToInt(y) - len, c);
        }

        /// <summary>유리에 대고 미끄러져 내려간 손바닥 자국(손바닥·손가락 4~5개 + 아래로 끌린 자국).</summary>
        static void Hand(Color32[] px, RectInt r, float cx, float cy, System.Random rnd)
        {
            Color32 c = BloodColor(rnd);
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    if (x * x + y * y <= 5) Put(px, r, Mathf.RoundToInt(cx) + x, Mathf.RoundToInt(cy) + y, c);
            for (int f = 0; f < 4; f++)
            {
                int fx = Mathf.RoundToInt(cx) - 2 + f + (f > 1 ? 1 : 0);
                for (int k = 3; k < 6 + (f == 1 || f == 2 ? 1 : 0); k++) Put(px, r, fx, Mathf.RoundToInt(cy) + k, c);
            }
            for (int k = 1; k < 4; k++) Put(px, r, Mathf.RoundToInt(cx) + 3 + k / 2, Mathf.RoundToInt(cy) + k - 1, c);   // 엄지
            int slide = 6 + rnd.Next(10);
            for (int k = 0; k < slide; k++)
                for (int x = -2; x <= 2; x++)
                    if (rnd.NextDouble() < 0.6) Put(px, r, Mathf.RoundToInt(cx) + x, Mathf.RoundToInt(cy) - 3 - k, c);
        }

        // ── 실종 전단 ──
        static Texture2D Flyer(Vector3 center, int seed)
        {
            Vector2 a = Px(center + new Vector3(-0.15f, -0.21f, 0f)), b = Px(center + new Vector3(0.15f, 0.21f, 0f));
            int w = Mathf.Max(5, Mathf.RoundToInt(Mathf.Abs(b.x - a.x))), h = Mathf.Max(7, Mathf.RoundToInt(Mathf.Abs(b.y - a.y)));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var paper = new Color32(206, 210, 204, 255);
            var ink = new Color32(40, 44, 46, 255);
            var photo = new Color32(116, 120, 120, 255);
            for (int i = 0; i < px.Length; i++) px[i] = paper;
            // 맨 위 "실종" 굵은 줄, 가운데 얼굴 사진, 아래 글줄
            for (int x = 1; x < w - 1; x++) px[(h - 2) * w + x] = ink;
            int ph = Mathf.Max(2, h / 3), pw = Mathf.Max(2, w / 2), px0 = (w - pw) / 2, py0 = h - 3 - ph;
            for (int y = py0; y < py0 + ph; y++) for (int x = px0; x < px0 + pw; x++) if (y >= 0) px[y * w + x] = photo;
            for (int y = 1; y < py0 - 1; y += 2) for (int x = 1; x < w - 1 - (y + seed) % 2; x++) px[y * w + x] = ink;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ── 공용 ──
        static Sprite Save(Texture2D tex, string name, Vector2 pivot)
        {
            string path = $"{Dest}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static SpriteRenderer NewSr(string name, Transform parent, Vector3 pos, Material mat, int order) => NewSr(name, parent, (Vector2)pos, mat, order, pos.z);

        static SpriteRenderer NewSr(string name, Transform parent, Vector2 pos, Material mat, int order, float z = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
