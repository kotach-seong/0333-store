using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 오프닝·엔딩에 쓰는 주차장 CCTV 그림(메인 화면 카메라).
    ///  - 오프닝: 비 오는 밤, 우산 쓴 내가 보도를 따라 와서 가게 문으로(뒷모습, 걸음마다 한 장)
    ///  - 엔딩: 비 그친 새벽 배경(가로등 꺼짐), 문에서 걸어 나와 카메라 쪽을 보고 서는 나(앞모습, 얼굴 없음),
    ///    손님마다 실종자(창백한 얼굴)·내보낸 것(찢어진 웃음) 그림 — 게임에서 그날까지의 기록대로 골라 세운다.
    /// 사람은 괴물보다 밝게(실루엣이 아니게) 배경과 같은 빛으로 칠한다.
    /// </summary>
    public static partial class MenuExtrasBuilder
    {
        const string StoryDest = "Assets/_0333/Art/Story";
        const float PersonAlbedo = 0.45f, GhostAlbedo = 0.8f;
        const float PlayerMeters = 1.75f;

        static string Draft(string rel) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", rel));

        public static void BuildStory(MainMenu menu, Transform scene, Material mat, IList<CustomerView> views)
        {
            if (menu == null || scene == null) return;
            Directory.CreateDirectory(StoryDest);
            var feed = new Rect(-W / 2f, -H / 2f, W, H);
            Color32[] bgShut = LoadPixels($"{StoreSceneBuilder.ArtDir}/{G.MenuBackgroundName}.png");
            Color32[] bgOpen = menu.backgroundDoorOpen != null ? LoadPixels(AssetDatabase.GetAssetPath(menu.backgroundDoorOpen)) : bgShut;

            menu.walker = NewSr("Menu Story Walker", scene, Vector2.zero, mat, 401, -0.06f);
            menu.walker.enabled = false;

            // ── 오프닝: 보도를 따라 와서(화면 왼쪽에서) 가게 문으로. 뒷모습 + 우산 ──
            string back = UmbrellaArt(Draft("customers/player/north.png"), $"{StoryDest}/walker_back_src.png", out float backMeters);
            var path = new[]
            {
                new Vector3(3.3f, 0f, 9.75f), new Vector3(2.95f, 0f, 9.7f), new Vector3(2.6f, 0f, 9.65f), new Vector3(2.25f, 0f, 9.6f),
                new Vector3(1.9f, 0f, 9.52f), new Vector3(1.55f, 0f, 9.42f), new Vector3(1.22f, 0f, 9.3f), new Vector3(0.92f, 0f, 9.16f),
                new Vector3(0.64f, 0f, 9.0f), new Vector3(0.4f, 0f, 8.84f), new Vector3(0.2f, 0f, 8.7f), new Vector3(0.05f, 0f, 8.58f),
            };
            var inSprites = new List<Sprite>();
            var inAt = new List<Vector2>();
            if (back != null)
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 feet = path[i];
                    Placed p = Place(back, feet, backMeters, false, false);
                    Color32[] bg = i >= path.Length - 3 ? bgOpen : bgShut;
                    Vector2 at = Px(feet) + new Vector2(0f, i % 2);   // 걸음마다 1픽셀 들썩
                    Sprite s = LitSprite(p, feet, bg, $"walk_in_{i:00}", PersonAlbedo, StoryDest);
                    inSprites.Add(CustomerBuilder.MaskBySight(s, at, feed, Cam, feet.z));
                    inAt.Add(at);
                }
            menu.walkIn = inSprites.ToArray();
            menu.walkInAt = inAt.ToArray();

            // ── 엔딩: 비 그친 새벽 ──
            G.SetDawn(true);
            try
            {
                string dawnPath = $"{StoryDest}/menu_dawn.png", dawnOpenPath = $"{StoryDest}/menu_dawn_open.png";
                G.RenderMenuBackground().SavePng(dawnPath);
                G.MenuDoorGap = 0.42f;
                try { G.RenderMenuBackground().SavePng(dawnOpenPath); }
                finally { G.MenuDoorGap = 0f; }
                foreach (string pth in new[] { dawnPath, dawnOpenPath })
                {
                    AssetDatabase.ImportAsset(pth, ImportAssetOptions.ForceUpdate);
                    StoreSceneBuilder.ConfigureSprite(pth);
                }
                menu.backgroundDawn = AssetDatabase.LoadAssetAtPath<Sprite>(dawnPath);
                menu.backgroundDawnOpen = AssetDatabase.LoadAssetAtPath<Sprite>(dawnOpenPath);
                Color32[] dawn = LoadPixels(dawnPath), dawnOpen = LoadPixels(dawnOpenPath);

                // 문에서 걸어 나와 카메라 쪽을 보고 선다(앞모습, 얼굴 없는 나)
                string front = Draft("customers/player/south.png");
                var outPath = new[]
                {
                    new Vector3(0.05f, 0f, 8.58f), new Vector3(0.12f, 0f, 8.78f), new Vector3(0.2f, 0f, 8.98f), new Vector3(0.28f, 0f, 9.18f),
                    new Vector3(0.36f, 0f, 9.38f), new Vector3(0.42f, 0f, 9.56f), new Vector3(0.46f, 0f, 9.72f), new Vector3(0.48f, 0f, 9.82f),
                };
                var outSprites = new List<Sprite>();
                var outAt = new List<Vector2>();
                if (File.Exists(front))
                    for (int i = 0; i < outPath.Length; i++)
                    {
                        Vector3 feet = outPath[i];
                        Placed p = Place(front, feet, PlayerMeters, false, false);
                        Vector2 at = Px(feet) + new Vector2(0f, i % 2 == 1 && i < outPath.Length - 1 ? 1f : 0f);
                        Sprite s = LitSprite(p, feet, i < 2 ? dawnOpen : dawn, $"walk_out_{i:00}", PersonAlbedo, StoryDest);
                        outSprites.Add(CustomerBuilder.MaskBySight(s, at, feed, Cam, feet.z));
                        outAt.Add(at);
                    }
                menu.walkOut = outSprites.ToArray();
                menu.walkOutAt = outAt.ToArray();

                // 주차장·보도의 자리: 0·1 실종자(가까이, 양옆), 2·3 내보낸 것(가게 앞 보도)
                var slots = new[] { new Vector3(2.25f, 0f, 10.25f), new Vector3(-1.35f, 0f, 10.4f), new Vector3(2.85f, 0f, 9.05f), new Vector3(-2.55f, 0f, 9.15f) };
                menu.dawnSlots = new SpriteRenderer[slots.Length];
                for (int k = 0; k < slots.Length; k++)
                {
                    menu.dawnSlots[k] = NewSr($"Menu Story Dawn Slot {k}", scene, Px(slots[k]), mat, 401, -0.05f + (slots[k].z - 9f) * -0.004f);
                    menu.dawnSlots[k].enabled = false;
                }
                // 손님마다(경찰 빼고): 실종자는 앞모습을 창백하게, 내보낸 것은 찢어진 웃음
                var arts = new List<MainMenu.DawnArt>();
                foreach (CustomerView v in views)
                {
                    if (v == null || string.IsNullOrEmpty(v.customerId) || v.customerId == "police") continue;
                    string dir = Path.Combine(CustomerBuilder.SourceRoot, v.customerId);
                    string south = Path.Combine(dir, "south.png"), grin = Path.Combine(dir, "grin.png");
                    if (!File.Exists(south)) continue;
                    float meters = v.heightM > 0.5f ? v.heightM : 1.7f;
                    var art = new MainMenu.DawnArt { id = v.customerId };
                    Placed pm = Place(south, slots[0], meters, false, false);
                    art.missing = LitSprite(pm, slots[0], dawn, $"dawn_missing_{v.customerId}", GhostAlbedo, StoryDest);
                    Placed pt = Place(File.Exists(grin) ? grin : south, slots[2], meters, false, true);
                    art.trace = LitSprite(pt, slots[2], dawn, $"dawn_trace_{v.customerId}", PersonAlbedo, StoryDest);
                    arts.Add(art);
                }
                menu.dawnArt = arts.ToArray();

                // 계산대 뒤: 나 대신 교대한 사람(아침 알바생). 문 유리 너머로
                string workerSrc = Path.Combine(CustomerBuilder.SourceRoot, "worker", "south.png");
                if (File.Exists(workerSrc) && menu.player != null)
                {
                    Vector2 fUv = G.CamUV(Cam, new Vector3(0f, 0f, 0.3f)), hUv = G.CamUV(Cam, new Vector3(0f, 1.74f, 0.3f));
                    Texture2D wt = CustomerBuilder.LoadPng(workerSrc);
                    Sprite ws = CustomerBuilder.MakeCctvSprite(wt, (hUv.y - fUv.y) * H, -1f, $"{StoryDest}/dawn_clerk.png");
                    Object.DestroyImmediate(wt);
                    menu.dawnClerk = CustomerBuilder.MaskBySight(ws, menu.player.transform.localPosition, feed, Cam, 0.3f);
                }
            }
            finally
            {
                G.SetDawn(false);
            }
            EditorUtility.SetDirty(menu);
        }

        /// <summary>
        /// 뒷모습 위에 검은 우산을 씌운 그림(캔버스를 위로 늘려 우산 자리를 만든다). 키(우산 끝까지)도 돌려준다.
        /// </summary>
        static string UmbrellaArt(string src, string outAsset, out float meters)
        {
            meters = PlayerMeters;
            if (!File.Exists(src)) { Debug.LogWarning("[0333] 오프닝 걷는 그림이 없음: " + src); return null; }
            Texture2D t = CustomerBuilder.LoadPng(src);
            int w = t.width, h = t.height, extra = 34;
            Color32[] sp = t.GetPixels32();
            Object.DestroyImmediate(t);
            int top = -1, bottom = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (sp[y * w + x].a > 40) { if (bottom < 0) bottom = y; top = y; break; }
            // 머리 가운데
            int hx0 = w, hx1 = -1;
            for (int x = 0; x < w; x++) if (sp[(top - 3) * w + x].a > 40) { hx0 = Mathf.Min(hx0, x); hx1 = Mathf.Max(hx1, x); }
            int cx = hx1 >= hx0 ? (hx0 + hx1) / 2 : w / 2;
            var dst = new Color32[w * (h + extra)];
            for (int y = 0; y < h; y++) System.Array.Copy(sp, y * w, dst, y * w, w);
            int H2 = h + extra;
            void Put(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < H2) dst[y * w + x] = c; }
            // 우산 천: 머리 위로 반원, 아랫단은 물결. 살은 조금 밝게, 끝에 꼭지
            int baseY = top + 6, rx = 34, ry = 20;
            var cloth = new Color32(26, 30, 40, 255);
            var rib = new Color32(54, 60, 76, 255);
            var shine = new Color32(78, 86, 104, 255);
            for (int y = baseY - 3; y <= baseY + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (x - cx) / (float)rx, dy = (y - baseY) / (float)ry;
                    bool dome = dy >= 0f && dx * dx + dy * dy <= 1f;
                    int scallop = (int)(Mathf.Abs(Mathf.Sin((x - cx) * Mathf.PI / 11f)) * 3f);
                    bool hem = dy < 0f && y >= baseY - scallop && Mathf.Abs(dx) <= 1f;
                    if (!dome && !hem) continue;
                    bool isRib = Mathf.Abs(x - cx) % 17 == 0 && dy > 0.05f;
                    bool hi = dome && dy > 0.55f && dx < -0.1f && dx > -0.6f;
                    Put(x, y, isRib ? rib : hi ? shine : cloth);
                }
            for (int y = baseY + ry; y < baseY + ry + 4; y++) Put(cx, y, rib);   // 꼭지
            var tex = new Texture2D(w, H2, TextureFormat.RGBA32, false);
            tex.SetPixels32(dst);
            tex.Apply();
            File.WriteAllBytes(outAsset, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            int newTop = baseY + ry + 3;
            meters = PlayerMeters * (newTop - bottom + 1f) / (top - bottom + 1f);
            return Path.GetFullPath(outAsset);
        }

        static Sprite SaveTo(Texture2D tex, string dir, string name, Vector2 pivot)
        {
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.png";
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
    }
}
