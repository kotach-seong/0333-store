using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Group = Night0333.EditorTools.StoreArtGenerator.Group;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 메뉴 [0333 > 정면 편의점 씬 생성] 한 번으로
    /// 픽셀아트 생성 → 스프라이트 임포트 설정 → Store 씬 구성 → 빌드 목록 등록까지 처리한다.
    /// 다시 실행하면 그림과 씬을 새로 만든다(Store.unity를 덮어씀).
    /// </summary>
    public static class StoreSceneBuilder
    {
        const string Root = "Assets/_0333";
        internal const string ArtDir = Root + "/Art/Generated";
        const string MatPath = Root + "/Materials/PixelSpriteUnlit.mat";
        public const string ScenePath = Root + "/Scenes/Store.unity";
        public const string BloodSurfacePath = Root + "/Art/Fate/blood_surface.png";

        [MenuItem("0333/정면 편의점 씬 생성 (Build Store Scene)")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(null);
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>배치 모드용: Unity.exe -batchmode -executeMethod ...BuildBatch -previewDir 경로</summary>
        public static void BuildBatch() => Build(Arg("-previewDir"));

        /// <summary>배치 모드용: CCTV 1·2·3번 채널의 배경 화면만 그려서 -previewDir에 저장한다. 씬은 건드리지 않는다.</summary>
        public static void PreviewCctv() => StoreArtGenerator.RenderCctvChannels(Arg("-previewDir"));

        static void Build(string previewDir)
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Scenes");

            List<StoreArtGenerator.Layer> layers = StoreArtGenerator.Generate();
            // 코드로 그리는 피가 묻을 수 있는 면(바닥·벽·진열대) 지도: 흔적 2의 피가 물건 앞이나 유리 위에 뜨지 않게
            Directory.CreateDirectory(Root + "/Art/Fate");
            if (StoreArtGenerator.SurfaceMask != null) StoreArtGenerator.SurfaceMask.SavePng(BloodSurfacePath);
            // 형광등이 하나씩 나가는 장면용: 앞줄만 켜진 가게, 다 꺼진 가게
            var variants = new List<StoreArtGenerator.Layer>();
            variants.AddRange(StoreArtGenerator.GenerateFront());
            variants.AddRange(StoreArtGenerator.GenerateDark());
            foreach (var l in layers) l.Canvas.SavePng($"{ArtDir}/{l.Name}.png");
            foreach (var l in variants) l.Canvas.SavePng($"{ArtDir}/{l.Name}.png");
            // 메인 화면 배경(CCTV 3번을 화면 크기로)
            StoreArtGenerator.RenderMenuBackground().SavePng($"{ArtDir}/{StoreArtGenerator.MenuBackgroundName}.png");

            // 이번에 만들지 않은 예전 그림은 지운다
            var keep = new HashSet<string>();
            foreach (var l in layers) keep.Add(l.Name);
            foreach (var l in variants) keep.Add(l.Name);
            keep.Add(StoreArtGenerator.MenuBackgroundName);
            foreach (string file in Directory.GetFiles(ArtDir, "*.png"))
                if (!keep.Contains(Path.GetFileNameWithoutExtension(file)))
                    AssetDatabase.DeleteAsset($"{ArtDir}/{Path.GetFileName(file)}");
            AssetDatabase.Refresh();
            foreach (var l in layers) ConfigureSprite($"{ArtDir}/{l.Name}.png");
            foreach (var l in variants) ConfigureSprite($"{ArtDir}/{l.Name}.png");
            ConfigureSprite($"{ArtDir}/{StoreArtGenerator.MenuBackgroundName}.png");
            var created = new Dictionary<string, SpriteRenderer>();

            Material mat = GetOrCreateMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = CreateCameras();

            var store = new GameObject("Store").transform;
            var groups = new Dictionary<Group, Transform>();
            foreach (Group g in Enum.GetValues(typeof(Group)))
            {
                if (g == Group.Ui) continue;
                var t = new GameObject(g.ToString()).transform;
                t.SetParent(store, false);
                groups[g] = t;
            }

            // CCTV 크게 보기 화면: 카메라의 자식이라 시선을 따라다닌다
            var panel = new GameObject("CctvPanel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);
            var cctv = new GameObject("CCTV").AddComponent<CctvSystem>();
            cctv.monitorScreens = new SpriteRenderer[StoreArtGenerator.ChannelCount];
            cctv.panelFeeds = new SpriteRenderer[StoreArtGenerator.ChannelCount];
            var monitorArea = new Rect();

            foreach (var l in layers)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{l.Name}.png");
                if (sprite == null) throw new Exception($"스프라이트를 불러오지 못함: {l.Name}");
                RectInt r = l.Canvas.Rect;
                var center = new Vector3(r.x + r.width * 0.5f, r.y + r.height * 0.5f, 0f);
                var area = new Rect(r.x, r.y, r.width, r.height);

                if (l.Group == Group.Ui)
                {
                    SpriteRenderer sr = CreateSprite(l.Name, panel.transform, center, sprite, mat, l.Order);
                    if (l.Name == StoreArtGenerator.PrevHoverName) { cctv.prevHover = sr; cctv.prevButton = area; sr.enabled = false; }
                    else if (l.Name == StoreArtGenerator.NextHoverName) { cctv.nextHover = sr; cctv.nextButton = area; sr.enabled = false; }
                    else if (l.Name == StoreArtGenerator.CloseHoverName) { cctv.closeHover = sr; cctv.closeButton = area; sr.enabled = false; }
                    else if (l.Name == StoreArtGenerator.DotOnName) cctv.channelDot = sr.transform;
                    else
                        for (int k = 0; k < StoreArtGenerator.ChannelCount; k++)
                            if (l.Name == StoreArtGenerator.FullFeedName(k)) cctv.panelFeeds[k] = sr;
                    continue;
                }

                int channel = -1;
                for (int k = 0; k < StoreArtGenerator.ChannelCount; k++)
                    if (l.Name == StoreArtGenerator.MonitorLayerName(k)) channel = k;
                if (channel >= 0)
                {
                    cctv.monitorScreens[channel] = CreateSprite(l.Name, groups[l.Group], center, sprite, mat, l.Order);
                    monitorArea = area;
                    continue;
                }

                if (l.Name == "outside_rain")
                {
                    var rain = new GameObject("Rain");
                    rain.transform.SetParent(groups[l.Group], false);
                    rain.transform.localPosition = center;
                    var scroll = rain.AddComponent<ScrollingRain>();
                    scroll.tileHeight = r.height;
                    scroll.speed = 130f;
                    CreateSprite("Rain A", rain.transform, Vector3.zero, sprite, mat, l.Order);
                    CreateSprite("Rain B", rain.transform, new Vector3(0f, r.height, 0f), sprite, mat, l.Order);
                }
                else
                {
                    created[l.Name] = CreateSprite(l.Name, groups[l.Group], center, sprite, mat, l.Order);
                }
            }

            // 형광등: 켜진 레이어 밑(같은 자리)에 불 꺼진 레이어를 깔고, 등 6개를 하나씩 켜고 끈다
            AddStoreLights(store, created, variants, mat);

            // CCTV 연결: 모니터 화면을 조준하고 클릭하면 크게 보기가 열린다
            var look = camGo.GetComponent<MouseLookCamera>();
            cctv.look = look;
            cctv.viewCamera = camGo.GetComponent<Camera>();
            cctv.panel = panel;
            cctv.dotStep = StoreArtGenerator.DotStep;
            cctv.channel = 0;
            for (int k = 0; k < StoreArtGenerator.ChannelCount; k++)
            {
                if (cctv.monitorScreens[k] == null || cctv.panelFeeds[k] == null) throw new Exception($"CCTV {k + 1}번 채널 그림이 없음");
                cctv.monitorScreens[k].enabled = k == 0; // 처음에는 1번 채널
                cctv.panelFeeds[k].enabled = k == 0;
            }
            panel.SetActive(false);

            var hotspotGo = new GameObject("Hotspot CCTV");
            hotspotGo.transform.SetParent(cctv.transform, false);
            var hotspot = hotspotGo.AddComponent<Hotspot>();
            hotspot.area = new Rect(monitorArea.x - 4f, monitorArea.y - 4f, monitorArea.width + 8f, monitorArea.height + 8f);
            hotspot.label = "CCTV 보기";
            UnityEventTools.AddPersistentListener(hotspot.onInteract, cctv.Open);
            camGo.AddComponent<HotspotInteractor>().look = look;

            NotebookBuilder.AddToScene(camGo, mat);
            ItemBayBuilder.AddToScene(mat);
            CustomerBuilder.AddToScene(mat);
            CheckoutBuilder.AddToScene(camGo, mat);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previewDir)) WritePreviews(layers, previewDir);
            Debug.Log($"[0333] Store 씬 생성 완료: {ScenePath} (레이어 {layers.Count}개)");
        }

        /// <summary>
        /// 앞줄만 켜진 레이어·다 꺼진 레이어를 켜진 레이어의 자식으로 붙이고(문짝이 열리면 같이 움직인다) StoreLights를 만든다.
        /// 실내·계산대처럼 불투명한 것은 켜짐(order) 아래에 앞줄만(order-1), 다 꺼짐(order-2) 순으로 깔아 투명도로 섞고,
        /// 유리·문짝처럼 반투명한 것은 켜짐/꺼짐을 같은 칸에서 서로 바꿔 끼운다.
        /// </summary>
        static void AddStoreLights(Transform store, Dictionary<string, SpriteRenderer> created, List<StoreArtGenerator.Layer> variants, Material mat)
        {
            var go = new GameObject("Store Lights");
            go.transform.SetParent(store, false);
            var lights = go.AddComponent<StoreLights>();

            var tubes = new List<SpriteRenderer>();
            for (int k = 0; created.TryGetValue($"light_tube_{k}", out SpriteRenderer t); k++) tubes.Add(t);
            lights.tubes = tubes.ToArray();

            SpriteRenderer Attach(StoreArtGenerator.Layer l, SpriteRenderer litSr, int order)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{l.Name}.png");
                if (sprite == null) throw new Exception($"스프라이트를 불러오지 못함: {l.Name}");
                RectInt r = l.Canvas.Rect;
                Vector3 center = new Vector3(r.x + r.width * 0.5f, r.y + r.height * 0.5f, 0f);
                SpriteRenderer sr = CreateSprite(l.Name, litSr.transform, center - litSr.transform.position, sprite, mat, order);
                sr.enabled = false;   // 평소에는 안 보인다
                return sr;
            }

            var lit = new List<SpriteRenderer>();
            var front = new List<SpriteRenderer>();
            var dark = new List<SpriteRenderer>();
            var cross = new List<bool>();
            foreach (var l in variants)
            {
                if (!l.Name.EndsWith(StoreArtGenerator.DarkSuffix)) continue;
                string litName = l.Name.Substring(0, l.Name.Length - StoreArtGenerator.DarkSuffix.Length);
                if (!created.TryGetValue(litName, out SpriteRenderer litSr)) continue;
                bool translucent = litName.StartsWith("window_glass") || litName.StartsWith("door_leaf");
                StoreArtGenerator.Layer fl = variants.Find(v => v.Name == litName + StoreArtGenerator.FrontSuffix);
                front.Add(fl != null && !translucent ? Attach(fl, litSr, l.Order - 1) : null);
                dark.Add(Attach(l, litSr, translucent ? l.Order : l.Order - 2));
                lit.Add(litSr);
                cross.Add(translucent);
            }
            lights.litLayers = lit.ToArray();
            lights.frontLayers = front.ToArray();
            lights.darkLayers = dark.ToArray();
            lights.crossfade = cross.ToArray();
        }

        static GameObject CreateCameras()
        {
            var bgGo = new GameObject("Letterbox Background Camera");
            var bg = bgGo.AddComponent<Camera>();
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = Color.black;
            bg.cullingMask = 0;
            bg.depth = -10;
            bg.orthographic = true;

            Vector2 home = StoreArtGenerator.Home;
            RectInt world = StoreArtGenerator.WorldRect;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(home.x, home.y, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = StoreArtGenerator.ViewH * 0.5f; // 화면 높이 360px, 1 유닛 = 1 픽셀
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.depth = 0;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Letterbox>();
            var look = camGo.AddComponent<MouseLookCamera>();
            look.worldBounds = new Rect(world.x, world.y, world.width, world.height);
            look.home = home;
            return camGo;
        }

        internal static SpriteRenderer CreateSprite(string name, Transform parent, Vector3 localPos, Sprite sprite, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }

        internal static void ConfigureSprite(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }

        internal static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader == null) throw new Exception("스프라이트용 Unlit 셰이더를 찾지 못함");
            mat = new Material(shader) { name = "PixelSpriteUnlit" };
            AssetDatabase.CreateAsset(mat, MatPath);
            return mat;
        }

        /// <summary>검증용: 레이어를 CPU로 합성해 전체 그림과 시선 방향별 화면(640x360)을 PNG로 저장.</summary>
        static void WritePreviews(List<StoreArtGenerator.Layer> layers, string dir)
        {
            Directory.CreateDirectory(dir);
            PixelCanvas all = StoreArtGenerator.Composite(layers);
            all.SavePng(Path.Combine(dir, "world_full.png"), 2);

            RectInt w = StoreArtGenerator.WorldRect;
            int vw = StoreArtGenerator.ViewW, vh = StoreArtGenerator.ViewH;
            int hx = (int)StoreArtGenerator.Home.x, hy = (int)StoreArtGenerator.Home.y;
            int left = w.xMin + vw / 2, right = w.xMax - vw / 2, down = w.yMin + vh / 2, up = w.yMax - vh / 2;
            var views = new (string name, int cx, int cy)[]
            {
                ("view_home", hx, hy), ("view_up", hx, up), ("view_down", hx, down),
                ("view_left", left, hy), ("view_right", right, hy),
                ("view_down_left", left, down), ("view_down_right", right, down),
            };
            foreach (var v in views)
            {
                var view = new PixelCanvas(new RectInt(v.cx - vw / 2, v.cy - vh / 2, vw, vh));
                view.Composite(all, Vector2Int.zero);
                view.SavePng(Path.Combine(dir, v.name + ".png"), 3);
            }

            // CCTV 크게 보기 화면(채널별)
            for (int k = 0; k < StoreArtGenerator.ChannelCount; k++)
                StoreArtGenerator.CompositePanel(layers, k).SavePng(Path.Combine(dir, $"panel_cctv{k + 1}.png"), 3);
        }

        internal static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
