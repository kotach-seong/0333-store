using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 03:33 이벤트(20가지)에 필요한 것들을 씬에 넣는다.
    ///  - 화면 연출(카메라): 흔들림·거울·붉은 막·속삭임 글자(ScreenFx), 떨리고 무거운 커서(CursorFx)
    ///  - 4 줄 선 사람들(정면 + CCTV), 5 젖은 발자국, 6 에러·검정 상품, 10 계산대 밑의 손,
    ///    12 나를 보는 CCTV, 14·16 CCTV 2번의 다른 것·전임자, 15 잡음 화면, 18 창밖 실종자, 19 붉은 막 너머 형체
    /// CheckoutBuilder.AddToScene 끝에서 부른다(손님·CCTV·계산대가 다 있은 뒤).
    /// </summary>
    public static class Event3333Builder
    {
        const string Dest = "Assets/_0333/Art/Event333";
        const string Prefix = "E333 ";
        const string RootName = "Event 3333";
        const string LinesPath = "Assets/_0333/Data/Event3333.txt";
        const string BodyFontPath = "Assets/_0333/Fonts/Galmuri9.ttf";
        const float StandX = -0.05f, StandZ = 2.21f;    // 손님이 서는 자리(CustomerBuilder와 같다)
        const float PlayerZ = 0.3f;

        static string Draft(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", path));

        public static Event3333 Build(CheckoutSystem checkout, GameObject camGo, Material mat, AudioSource src)
        {
            // 예전 것 지우기
            var oldRoot = GameObject.Find(RootName);
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
            CctvSystem cctv = checkout.cctv;
            if (cctv != null && cctv.panel != null)
            {
                var stale = new List<GameObject>();
                foreach (Transform t in cctv.panel.transform) if (t.name.StartsWith(Prefix)) stale.Add(t.gameObject);
                foreach (var g in stale) UnityEngine.Object.DestroyImmediate(g);
            }
            foreach (string n in new[] { "Event Veil", "Whisper Canvas" })
            {
                Transform t = camGo.transform.Find(n);
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            foreach (var c in camGo.GetComponents<ScreenFx>()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in camGo.GetComponents<CursorFx>()) UnityEngine.Object.DestroyImmediate(c);
            Directory.CreateDirectory(Dest);

            var root = new GameObject(RootName);
            var ev = root.AddComponent<Event3333>();
            ev.checkout = checkout;
            ev.clock = checkout.clock;
            ev.lights = UnityEngine.Object.FindFirstObjectByType<StoreLights>(FindObjectsInactive.Include);
            ev.door = UnityEngine.Object.FindFirstObjectByType<DoorController>(FindObjectsInactive.Include);
            ev.phone = checkout.phone;
            ev.cctv = cctv;
            ev.bubble = checkout.bubble;
            ev.choices = checkout.choices;
            ev.look = camGo.GetComponent<MouseLookCamera>();
            ev.fate = checkout.fate;
            ev.sfx = src;
            AssetDatabase.ImportAsset(LinesPath);
            ev.linesFile = AssetDatabase.LoadAssetAtPath<TextAsset>(LinesPath);
            checkout.event333 = ev;

            ev.fx = BuildScreenFx(camGo, mat);
            camGo.AddComponent<CursorFx>();

            AddSpecialItems(checkout.bay);
            var views = new List<CustomerView>(checkout.customers);
            views.RemoveAll(v => v == null);
            views.Sort((a, b) => string.CompareOrdinal(a.customerId, b.customerId));

            // 말풍선 자리: 보이지 않는 손님의 머리(보통 손님 그림의 머리 꼭대기와 같은 높이), 문 위
            ev.counterHead = (Vector3)G.ToScreen(new Vector3(StandX, 1.72f, StandZ));
            CustomerView typical = views.Find(v => v.customerId == "hoodie") ?? (views.Count > 0 ? views[0] : null);
            if (typical != null && typical.counterBody != null && typical.counterBody.sprite != null)
            {
                SpriteRenderer body = typical.counterBody;
                Sprite s = body.sprite;
                Texture2D t = CustomerBuilder.LoadPng(AssetDatabase.GetAssetPath(s));
                int top = -1;
                Color32[] px = t.GetPixels32();
                for (int y = t.height - 1; y >= 0 && top < 0; y--)
                    for (int x = 0; x < t.width; x++) if (px[y * t.width + x].a > 40) { top = y; break; }
                UnityEngine.Object.DestroyImmediate(t);
                float scale = body.transform.lossyScale.y;
                ev.counterHead = new Vector3(body.transform.position.x, body.transform.position.y + (top + 1 - s.pivot.y) * scale, 0f);
            }
            ev.doorTop = G.ToScreen(new Vector3(0f, 2.35f, 8f));

            BuildQueue(ev, root.transform, cctv, mat, views);
            BuildFootprints(ev, root.transform, mat);
            BuildHand(ev, root.transform, mat);
            if (cctv != null)
            {
                BuildNightCctv(root.transform, cctv, mat);
                BuildStare(ev, root, cctv, mat);
                BuildStaffOnCctv(ev, root, cctv, mat);
                BuildStatic(ev, root.transform, cctv, mat);
                BuildMissing(ev, root, cctv, mat, views);
            }
            BuildRedShape(ev, root.transform, checkout);

            EditorUtility.SetDirty(ev);
            EditorUtility.SetDirty(checkout);
            return ev;
        }

        // ── 3:33의 CCTV: 앞줄 등만 켜진 가게를 비춘다 ──
        static void BuildNightCctv(Transform root, CctvSystem cctv, Material mat)
        {
            int n = cctv.monitorScreens != null ? cctv.monitorScreens.Length : 0;
            cctv.monitorScreensNight = new SpriteRenderer[n];
            cctv.panelFeedsNight = new SpriteRenderer[cctv.panelFeeds != null ? cctv.panelFeeds.Length : 0];
            // 계산대 모니터 화면 자리(여기만 다시 훑는다)
            bool any = false;
            Rect area = default;
            foreach (var sr in cctv.monitorScreens)
            {
                if (sr == null || sr.sprite == null) continue;
                Bounds b = sr.bounds;
                var r = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                area = any ? Rect.MinMaxRect(Mathf.Min(area.xMin, r.xMin), Mathf.Min(area.yMin, r.yMin), Mathf.Max(area.xMax, r.xMax), Mathf.Max(area.yMax, r.yMax)) : r;
                any = true;
            }
            var areaInt = new RectInt(Mathf.FloorToInt(area.xMin), Mathf.FloorToInt(area.yMin), Mathf.CeilToInt(area.width) + 1, Mathf.CeilToInt(area.height) + 1);
            List<StoreArtGenerator.Layer> layers = StoreArtGenerator.GenerateFrontFeeds(any ? areaInt : new RectInt(0, 0, 0, 0));
            var monitorGroup = new GameObject("Night CCTV Monitor").transform;
            monitorGroup.SetParent(root, false);
            foreach (var l in layers)
            {
                string path = $"{Dest}/{l.Name}.png";
                l.Canvas.SavePng(path);
                Sprite s = Import(path, new Vector2(0.5f, 0.5f));
                RectInt r = l.Canvas.Rect;
                var center = new Vector3(r.x + r.width * 0.5f, r.y + r.height * 0.5f, 0f);
                for (int k = 0; k < cctv.panelFeedsNight.Length; k++)
                {
                    if (l.Name != StoreArtGenerator.FullFeedName(k) + StoreArtGenerator.FrontSuffix) continue;
                    SpriteRenderer day = cctv.panelFeeds[k];
                    if (day == null) continue;
                    // 크게 보기 영상: 평소 영상과 같은 자리·같은 층(패널의 자식)
                    var sr = NewSr($"{Prefix}night feed {k + 1}", day.transform.parent, Vector3.zero, mat, day.sortingOrder);
                    sr.transform.localPosition = day.transform.localPosition;
                    sr.transform.localRotation = day.transform.localRotation;
                    sr.transform.localScale = day.transform.localScale;
                    sr.sprite = s;
                    sr.enabled = false;
                    cctv.panelFeedsNight[k] = sr;
                }
                for (int k = 0; k < n; k++)
                {
                    if (l.Name != StoreArtGenerator.MonitorLayerName(k) + StoreArtGenerator.FrontSuffix) continue;
                    SpriteRenderer day = cctv.monitorScreens[k];
                    if (day == null) continue;
                    var sr = NewSr($"Night monitor {k + 1}", monitorGroup, new Vector3(center.x, center.y, day.transform.position.z), mat, day.sortingOrder);
                    sr.sprite = s;
                    sr.enabled = false;
                    cctv.monitorScreensNight[k] = sr;
                }
            }
            EditorUtility.SetDirty(cctv);
        }

        // ── 화면 연출 ──
        static ScreenFx BuildScreenFx(GameObject camGo, Material mat)
        {
            var fx = camGo.AddComponent<ScreenFx>();
            var veilGo = new GameObject("Event Veil");
            veilGo.transform.SetParent(camGo.transform, false);
            veilGo.transform.localPosition = new Vector3(0f, 0f, 5f);
            var veil = veilGo.AddComponent<SpriteRenderer>();
            veil.sharedMaterial = mat;
            veil.sortingOrder = 207;   // 장면·패널(199~206) 위, 말풍선(211)·시계(220) 아래
            veil.enabled = false;
            fx.veil = veil;

            var canvasGo = new GameObject("Whisper Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(camGo.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 213;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            Font font = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            var texts = new List<Text>();
            for (int i = 0; i < 28; i++)
            {
                var go = new GameObject($"Whisper {i}", typeof(RectTransform));
                go.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = Vector2.zero;
                rt.sizeDelta = new Vector2(120f, 16f);
                var t = go.AddComponent<Text>();
                t.font = font;
                t.fontSize = 10;
                t.color = new Color32(214, 206, 196, 235);
                t.alignment = TextAnchor.LowerLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.supportRichText = false;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1f, -1f);
                t.enabled = false;
                texts.Add(t);
            }
            fx.whispers = texts.ToArray();
            EditorUtility.SetDirty(fx);
            return fx;
        }

        // ── 6 에러 상품·검정 상품 ──
        static void AddSpecialItems(ItemBay bay)
        {
            if (bay == null) return;
            var list = new List<ItemBay.ItemDef>(bay.items);
            list.RemoveAll(d => d.id == ItemBay.ErrorId || d.id == ItemBay.BlackId);
            list.Add(new ItemBay.ItemDef { id = ItemBay.ErrorId, displayName = "ERR0R", normal = SaveProduct(DrawErrorProduct(), "item_error"), anomalies = new ItemBay.AnomalyLook[0] });
            list.Add(new ItemBay.ItemDef { id = ItemBay.BlackId, displayName = "■■■■", normal = SaveProduct(DrawBlackProduct(), "item_black"), anomalies = new ItemBay.AnomalyLook[0], tall = true });
            bay.items = list.ToArray();
            EditorUtility.SetDirty(bay);
        }

        /// <summary>깨진 그림처럼 보이는 상품: 보라·검정 체크 무늬 상자에 "ERR" 글자, 줄줄이 어긋난 줄.</summary>
        static Texture2D DrawErrorProduct()
        {
            var t = Blank(40, 40);
            var rnd = new System.Random(404);
            for (int y = 2; y < 30; y++)
                for (int x = 6; x < 34; x++)
                {
                    bool chk = ((x - 6) / 4 + (y - 2) / 4) % 2 == 0;
                    Color32 c = chk ? new Color32(236, 30, 220, 255) : new Color32(16, 10, 18, 255);
                    int shift = (y % 7 == 3) ? rnd.Next(-3, 4) : 0;   // 어긋난 줄
                    Put(t, x + shift, y, c);
                }
            OutlineRect(t, 6, 2, 34, 30, new Color32(8, 4, 10, 255));
            // ERR 글자(흰 바탕 띠)
            for (int y = 14; y < 21; y++) for (int x = 8; x < 32; x++) Put(t, x, y, new Color32(238, 238, 238, 255));
            string[] E = { "###", "#..", "##.", "#..", "###" }, R = { "##.", "#.#", "##.", "#.#", "#.#" };
            Stamp(t, E, 10, 15, new Color32(20, 12, 20, 255));
            Stamp(t, R, 15, 15, new Color32(20, 12, 20, 255));
            Stamp(t, R, 20, 15, new Color32(20, 12, 20, 255));
            Stamp(t, new[] { ".#.", "#.#", "#.#", "#.#", ".#." }, 25, 15, new Color32(20, 12, 20, 255));
            t.Apply();
            return t;
        }

        /// <summary>새까만 상품: 빛을 하나도 안 받는 검은 상자, 작은 흰 바코드와 "0333".</summary>
        static Texture2D DrawBlackProduct()
        {
            var t = Blank(40, 40);
            for (int y = 2; y < 36; y++)
                for (int x = 10; x < 30; x++) Put(t, x, y, new Color32(4, 4, 6, 255));
            OutlineRect(t, 10, 2, 30, 36, new Color32(30, 28, 34, 255));
            for (int x = 13; x < 27; x++) if (x % 3 != 2) for (int y = 6; y < 12; y++) Put(t, x, y, new Color32(214, 214, 214, 255));
            string[] d0 = { "###", "#.#", "#.#", "#.#", "###" }, d3 = { "###", "..#", ".##", "..#", "###" };
            Stamp(t, d0, 12, 14, new Color32(160, 160, 160, 255));
            Stamp(t, d3, 16, 14, new Color32(160, 160, 160, 255));
            Stamp(t, d3, 20, 14, new Color32(160, 160, 160, 255));
            Stamp(t, d3, 24, 14, new Color32(160, 160, 160, 255));
            t.Apply();
            return t;
        }

        static Texture2D Blank(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(new Color32[w * h]);
            return t;
        }

        static void Put(Texture2D t, int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c);
        }

        static void OutlineRect(Texture2D t, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int x = x0; x < x1; x++) { Put(t, x, y0, c); Put(t, x, y1 - 1, c); }
            for (int y = y0; y < y1; y++) { Put(t, x0, y, c); Put(t, x1 - 1, y, c); }
        }

        /// <summary>위에서부터 적은 작은 글자 무늬를 (x, 아래쪽 y)에 찍는다.</summary>
        static void Stamp(Texture2D t, string[] rows, int x, int y, Color32 c)
        {
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                    if (rows[r][i] == '#') Put(t, x + i, y + rows.Length - 1 - r, c);
        }

        static Sprite SaveProduct(Texture2D t, string name)
        {
            string path = $"{Dest}/{name}.png";
            int bottom = 0;
            for (int y = 0; y < t.height && bottom == 0; y++)
                for (int x = 0; x < t.width; x++) if (t.GetPixel(x, y).a > 0.2f) { bottom = Mathf.Max(1, y); break; }
            File.WriteAllBytes(path, t.EncodeToPNG());
            float pivotY = (float)bottom / t.height;
            UnityEngine.Object.DestroyImmediate(t);
            return Import(path, new Vector2(0.5f, pivotY), readable: true);
        }

        static Sprite Import(string path, Vector2 pivot, bool readable = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = readable;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── 4 줄 선 사람들 ──
        /// <summary>
        /// k번째 사람이 서는 자리: 계산대 앞부터 문까지(가게 안에서는 통로를 따라 한쪽으로 휘어 서로 가리지 않게),
        /// 문턱은 건너뛰고 문 밖 주차장까지.
        /// </summary>
        static Vector3 QueueSpot(int k)
        {
            if (k <= 10)
                return new Vector3(StandX - 0.16f * k, 0f, StandZ + 0.5f * k);   // 계산대에서 왼쪽 뒤로 비스듬히 한 줄
            return new Vector3(StandX + ((k % 2 == 0) ? -0.12f : 0.12f), 0f, 8.8f + 0.55f * (k - 11));
        }

        static void BuildQueue(Event3333 ev, Transform root, CctvSystem cctv, Material mat, List<CustomerView> views)
        {
            var group = new GameObject("Queue").transform;
            group.SetParent(root, false);
            var front = new List<SpriteRenderer>();
            var depth = new List<float>();
            var overlays = new List<CctvOverlay>();
            var owners = new List<int>();
            GameObject cctvRoot = new GameObject("Queue On CCTV");
            cctvRoot.transform.SetParent(root, false);
            for (int k = 0; k < views.Count; k++)
            {
                CustomerView v = views[k];
                Vector3 spot = QueueSpot(k);
                bool outside = spot.z > 8.3f;
                // 정면: 손님 앞모습을 그 깊이만큼 줄여서(계산대 앞 손님이 2배이니 2 × 2.21 / 깊이)
                Sprite body = v.counterBody != null ? v.counterBody.sprite : null;
                if (body != null)
                {
                    Vector2 feet = G.ToScreen(spot);
                    var sr = NewSr($"Queue {k} {v.customerId}", group, new Vector3(Mathf.Round(feet.x), Mathf.Round(feet.y), outside ? 0.6f + spot.z * 0.01f : spot.z * 0.01f), mat,
                                   outside ? 1 : 39 - k / 3);
                    float s = 2f * StandZ / spot.z;
                    sr.transform.localScale = new Vector3(s, s, 1f);
                    sr.sprite = body;
                    sr.enabled = false;
                    front.Add(sr);
                    depth.Add(spot.z);
                }
                if (cctv == null) continue;
                string src = Path.Combine(CustomerBuilder.SourceRoot, v.customerId);
                Texture2D south = LoadOr(Path.Combine(src, "south.png")), north = LoadOr(Path.Combine(src, "north.png"));
                // CCTV 1번(내 머리 위 → 얼굴), 2번(입구 위 → 뒷모습, 카메라 바로 밑은 빼고), 3번(주차장 → 문 밖 사람들의 뒷모습)
                if (south != null) AddPair(cctv, cctvRoot, mat, south, $"queue{k}", 0, spot, v.heightM, 200, 52, overlays, owners, k);
                if (north != null && spot.z < 6.6f) AddPair(cctv, cctvRoot, mat, north, $"queue{k}", 1, spot, v.heightM, 200, 52, overlays, owners, k);
                if (north != null && outside && spot.z < 13.5f) AddPair(cctv, cctvRoot, mat, north, $"queue{k}", 2, spot, v.heightM, 200, 52, overlays, owners, k);
                if (south != null) UnityEngine.Object.DestroyImmediate(south);
                if (north != null) UnityEngine.Object.DestroyImmediate(north);
            }
            ev.queue = front.ToArray();
            ev.queueDepth = depth.ToArray();
            ev.queueCctv = overlays.ToArray();
            ev.queueCctvOwner = owners.ToArray();
        }

        static Texture2D LoadOr(string path) => File.Exists(path) ? CustomerBuilder.LoadPng(path) : null;

        /// <summary>한 채널에 큰 화면·모니터 두 장(발이 feet, 키 heightM). 벽·계산대에 가리는 곳은 지운다.</summary>
        static void AddPair(CctvSystem cctv, GameObject root, Material mat, Texture2D src, string name, int ch, Vector3 feet, float heightM,
                            int panelOrder, int monitorOrder, List<CctvOverlay> into, List<int> owners = null, int owner = 0)
        {
            Vector2 uvFeet = G.CamUV(ch, feet), uvHead = G.CamUV(ch, feet + Vector3.up * heightM);
            if (uvHead.y < 0f || uvFeet.x < -0.3f || uvFeet.x > 1.3f) return;   // 화면 밖
            RectInt fr = G.PanelFeedRect;
            Sprite ps = CustomerBuilder.MakeCctvSprite(src, (uvHead.y - uvFeet.y) * fr.height, -1f, $"{Dest}/{name}_cctv{ch + 1}_panel.png", true);
            var pGo = new GameObject($"{Prefix}{name} cctv{ch + 1} (panel)");
            pGo.transform.SetParent(cctv.panel.transform, false);
            pGo.transform.localPosition = new Vector3(Mathf.Round(fr.x + uvFeet.x * fr.width), Mathf.Round(fr.y + uvFeet.y * fr.height), 0f);
            ps = CustomerBuilder.MaskBySight(ps, pGo.transform.localPosition, new Rect(fr.x, fr.y, fr.width, fr.height), ch, feet.z);
            into.Add(CustomerBuilder.AddOverlay(pGo, ps, mat, panelOrder, ch));
            owners?.Add(owner);

            Bounds b = cctv.monitorScreens[ch].bounds;
            Sprite ms = CustomerBuilder.MakeCctvSprite(src, (uvHead.y - uvFeet.y) * b.size.y, -1f, $"{Dest}/{name}_cctv{ch + 1}_monitor.png", true);
            var mGo = new GameObject($"{Prefix}{name} cctv{ch + 1} (monitor)");
            mGo.transform.SetParent(root.transform, false);
            mGo.transform.position = new Vector3(Mathf.Round(b.min.x + uvFeet.x * b.size.x), Mathf.Round(b.min.y + uvFeet.y * b.size.y), 0f);
            ms = CustomerBuilder.MaskBySight(ms, mGo.transform.position, new Rect(b.min.x, b.min.y, b.size.x, b.size.y), ch, feet.z);
            into.Add(CustomerBuilder.AddOverlay(mGo, ms, mat, monitorOrder, ch));
            owners?.Add(owner);
        }

        // ── 5 젖은 발자국 ──
        static void BuildFootprints(Event3333 ev, Transform root, Material mat)
        {
            var group = new GameObject("Footprints").transform;
            group.SetParent(root, false);
            var list = new List<SpriteRenderer>();
            var depth = new List<float>();
            // 계산대에 가리기 전까지 보이는 바닥(문 앞 7.7m ~ 4.3m)에 일곱 걸음
            const int steps = 7;
            for (int i = 0; i < steps; i++)
            {
                float z = Mathf.Lerp(7.7f, 4.3f, i / (float)(steps - 1));
                float x = StandX + (i % 2 == 0 ? -0.13f : 0.13f);
                list.Add(Footprint(group, mat, x, z, i));
                depth.Add(z);
            }
            ev.footprints = list.ToArray();
            ev.footprintDepth = depth.ToArray();
        }

        /// <summary>바닥에 찍힌 젖은 발자국 하나(원근에 맞게 그린다): 어두운 물기 + 번들거리는 점.</summary>
        static SpriteRenderer Footprint(Transform parent, Material mat, float cx, float cz, int i)
        {
            const float len = 0.34f, wid = 0.15f;
            Vector2 a = G.ToScreen(new Vector3(cx - wid, 0f, cz - len * 0.5f)), b = G.ToScreen(new Vector3(cx + wid, 0f, cz + len * 0.5f));
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - 1, x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + 1;
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - 1, y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + 1;
            int w = Mathf.Max(2, x1 - x0), h = Mathf.Max(2, y1 - y0);
            var t = Blank(w, h);
            var rnd = new System.Random(i * 31 + 7);
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    // 화면 픽셀 → 바닥 위치(눈높이 1.6, 초점 333)
                    float sx = x0 + px + 0.5f, sy = y0 + py + 0.5f;
                    float zz = G.Focal * (0f - G.EyeHeight) / (sy - G.VanishY);
                    if (zz <= 0f) continue;
                    float xx = sx * zz / G.Focal;
                    float u = (xx - cx) / (wid * 0.5f), v = (zz - cz) / (len * 0.5f);   // v: -1 뒤꿈치(가까이) ~ 1 발끝(멀리)
                    bool heel = u * u + (v + 0.55f) * (v + 0.55f) / 0.2f < 0.75f;
                    bool ball = u * u * 0.8f + (v - 0.3f) * (v - 0.3f) / 0.35f < 0.9f;
                    bool toe = v > 0.72f && v < 1f && Mathf.Abs(u) < 0.9f && ((int)((u + 1f) * 2.5f)) % 2 == 0;
                    if (!(heel || ball || toe)) continue;
                    bool shine = rnd.NextDouble() < 0.12;
                    Put(t, px, py, shine ? new Color32(170, 186, 196, 230) : new Color32(22, 28, 36, 225));
                }
            t.Apply();
            string path = $"{Dest}/footprint_{i}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            var sr = NewSr($"Footprint {i}", parent, new Vector3(x0, y0, 0f), mat, 12);   // 바닥(실내 10) 위, 손님(40)·계산대(50) 아래
            sr.sprite = Import(path, Vector2.zero);
            sr.enabled = false;
            return sr;
        }

        // ── 10 계산대 밑의 손 ──
        static void BuildHand(Event3333 ev, Transform root, Material mat)
        {
            string src = Draft("event333/hand.png");
            if (!File.Exists(src)) { Debug.LogWarning("[0333] 3:33 손 그림이 없음: " + src); return; }
            string path = $"{Dest}/hand.png";
            File.Copy(src, path, true);
            Sprite s = Import(path, new Vector2(0.5f, 0f), readable: true);
            // 돈바구니 바로 뒤(손님 쪽) 계산대 끝에서 올라와 바구니 위로. 진짜 팔 길이(0.5m)에 맞게 줄인다
            float x = (G.TrayX0 + G.TrayX1) * 0.5f, z = G.TrayZ1 + 0.08f, top = G.CounterTopY;
            Vector2 up = G.ToScreen(new Vector3(x, top - 0.12f, z));
            float scale = G.Focal * 0.55f / z / Mathf.Max(1f, s.rect.height);
            var sr = NewSr("Counter Hand", root, new Vector3(Mathf.Round(up.x), Mathf.Round(up.y), -0.02f), mat, 59);   // 계산대(50) 위, 돈바구니 물건(60~) 아래
            sr.sprite = s;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.enabled = false;
            ev.hand = sr;
            ev.handUp = sr.transform.position;
            ev.handRise = Mathf.Round(s.rect.height * scale * 0.85f);
            ev.handClipY = G.ToScreen(new Vector3(x, top, z)).y;

            var spotGo = new GameObject("Hotspot Hand");
            spotGo.transform.SetParent(root, false);
            var spot = spotGo.AddComponent<Hotspot>();
            spot.label = "...";
            spot.enabled = false;
            UnityEventTools.AddPersistentListener(spot.onInteract, ev.OnHandClicked);
            ev.handHotspot = spot;
        }

        // ── 12 나를 보는 CCTV ──
        static void BuildStare(Event3333 ev, GameObject root, CctvSystem cctv, Material mat)
        {
            string ghost = Draft("cctv_odd/ghost_front.png");
            if (!File.Exists(ghost)) return;
            Texture2D src = CustomerBuilder.LoadPng(ghost);
            var holder = new GameObject("Stare");
            holder.transform.SetParent(root.transform, false);
            var overlays = new List<CctvOverlay>();
            var stages = new List<int>();
            // 채널마다 세 단계(멀리 → 가까이). 카메라를 정면으로 본다
            var spots = new (int ch, Vector3 p)[]
            {
                (0, new Vector3(0.35f, 0f, 7.3f)), (0, new Vector3(0.25f, 0f, 5.2f)), (0, new Vector3(0.15f, 0f, 3.4f)),
                (1, new Vector3(-0.4f, 0f, 3.0f)), (1, new Vector3(-0.25f, 0f, 4.6f)), (1, new Vector3(-0.1f, 0f, 6.0f)),
                (2, new Vector3(1.4f, 0f, 10.6f)), (2, new Vector3(2.2f, 0f, 12.4f)), (2, new Vector3(3.0f, 0f, 13.9f)),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                int before = overlays.Count;
                AddPair(cctv, holder, mat, src, $"stare{i}", spots[i].ch, spots[i].p, 1.78f, 201, 53, overlays);
                for (int k = before; k < overlays.Count; k++) stages.Add(i % 3);
            }
            ev.stare = overlays.ToArray();
            ev.stareStage = stages.ToArray();

            // 마지막: 얼굴(머리 부분만 잘라) 큰 화면·모니터를 꽉 채운다
            Texture2D face = CropTop(src, 0.42f);
            var faces = new List<SpriteRenderer>();
            RectInt fr = G.PanelFeedRect;
            Sprite fp = CustomerBuilder.MakeCctvSprite(face, fr.height * 1.15f, -1f, $"{Dest}/stare_face_panel.png", true);
            var pGo = new GameObject($"{Prefix}stare face (panel)");
            pGo.transform.SetParent(cctv.panel.transform, false);
            pGo.transform.localPosition = new Vector3(fr.x + fr.width / 2f, fr.y - fr.height * 0.12f, 0f);
            var psr = pGo.AddComponent<SpriteRenderer>();
            psr.sprite = fp; psr.sharedMaterial = mat; psr.sortingOrder = 203; psr.enabled = false;
            faces.Add(psr);
            foreach (SpriteRenderer screen in cctv.monitorScreens)
            {
                if (screen == null) continue;
                Bounds b = screen.bounds;
                Sprite ms = CustomerBuilder.MakeCctvSprite(face, b.size.y * 1.15f, -1f, $"{Dest}/stare_face_monitor.png", true);
                var mGo = new GameObject("Stare Face (monitor)");
                mGo.transform.SetParent(holder.transform, false);
                mGo.transform.position = new Vector3(Mathf.Round(b.center.x), Mathf.Round(b.min.y - b.size.y * 0.12f), -0.01f);
                var msr = mGo.AddComponent<SpriteRenderer>();
                msr.sprite = ms; msr.sharedMaterial = mat; msr.sortingOrder = 55; msr.enabled = false;
                faces.Add(msr);
                break;   // 계산대 모니터는 하나(채널마다 같은 자리)
            }
            ev.stareFaces = faces.ToArray();
            UnityEngine.Object.DestroyImmediate(face);
            UnityEngine.Object.DestroyImmediate(src);
        }

        /// <summary>그림의 위쪽 frac만큼(머리)만 남긴 새 그림.</summary>
        static Texture2D CropTop(Texture2D src, float frac)
        {
            int top = -1, bottom = -1;
            Color32[] px = src.GetPixels32();
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                    if (px[y * src.width + x].a > 40) { if (bottom < 0) bottom = y; top = y; break; }
            int cut = Mathf.RoundToInt(top - (top - bottom) * frac);
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            var o = new Color32[px.Length];
            for (int y = cut; y < src.height; y++)
                for (int x = 0; x < src.width; x++) o[y * src.width + x] = px[y * src.width + x];
            t.SetPixels32(o);
            t.Apply();
            return t;
        }

        // ── 14·16 CCTV 2번: 내 자리의 다른 것, 내 뒤의 전임자 ──
        static void BuildStaffOnCctv(Event3333 ev, GameObject root, CctvSystem cctv, Material mat)
        {
            // 평소 CCTV 2번에 서 있는 나(CustomerBuilder가 만든다)
            var mine = new List<CctvOverlay>();
            Transform p = cctv.panel.transform.Find("CCTV2 player (panel)");
            if (p != null && p.GetComponent<CctvOverlay>() != null) mine.Add(p.GetComponent<CctvOverlay>());
            var pr = GameObject.Find("Player On CCTV");
            if (pr != null) foreach (var o in pr.GetComponentsInChildren<CctvOverlay>(true)) mine.Add(o);
            ev.playerCctv = mine.ToArray();

            string workerSrc = Path.Combine(CustomerBuilder.SourceRoot, "worker", "south.png");
            if (!File.Exists(workerSrc)) return;
            Texture2D worker = CustomerBuilder.LoadPng(workerSrc);
            var holder = new GameObject("Staff On CCTV");
            holder.transform.SetParent(root.transform, false);
            var notMe = new List<CctvOverlay>();
            AddPair(cctv, holder, mat, worker, "notme", 1, new Vector3(0f, 0f, PlayerZ), 1.74f, 200, 52, notMe);
            ev.notMe = notMe.ToArray();
            // 내 바로 뒤(조금 옆): 나보다 먼저 그려져 내 몸 뒤로 보인다
            var pred = new List<CctvOverlay>();
            AddPair(cctv, holder, mat, worker, "predecessor", 1, new Vector3(0.42f, 0f, PlayerZ - 0.12f), 1.72f, 200, 52, pred);
            foreach (var o in pred) o.transform.position += new Vector3(0f, 0f, 0.02f);   // 같은 층의 나보다 뒤에 그려진다
            ev.predecessor = pred.ToArray();
            UnityEngine.Object.DestroyImmediate(worker);
        }

        // ── 15 잡음 화면 ──
        static void BuildStatic(Event3333 ev, Transform root, CctvSystem cctv, Material mat)
        {
            var screens = new List<SpriteRenderer>();
            var sizes = new List<Vector2>();
            RectInt fr = G.PanelFeedRect;
            var pGo = new GameObject($"{Prefix}static (panel)");
            pGo.transform.SetParent(cctv.panel.transform, false);
            pGo.transform.localPosition = new Vector3(fr.x + fr.width / 2f, fr.y + fr.height / 2f, 0f);
            var psr = pGo.AddComponent<SpriteRenderer>();
            psr.sharedMaterial = mat; psr.sortingOrder = 204; psr.enabled = false;
            screens.Add(psr); sizes.Add(new Vector2(fr.width, fr.height));
            SpriteRenderer mon = cctv.monitorScreens.Length > 0 ? cctv.monitorScreens[0] : null;
            if (mon != null)
            {
                Bounds b = mon.bounds;
                var mGo = new GameObject("Static (monitor)");
                mGo.transform.SetParent(root, false);
                mGo.transform.position = new Vector3(b.center.x, b.center.y, -0.01f);
                var msr = mGo.AddComponent<SpriteRenderer>();
                msr.sharedMaterial = mat; msr.sortingOrder = 56; msr.enabled = false;
                screens.Add(msr); sizes.Add(new Vector2(b.size.x, b.size.y));
            }
            ev.staticScreens = screens.ToArray();
            ev.staticSizes = sizes.ToArray();

            // 잡음이 뭉치는 얼굴(밝기만 쓴다): 귀신 앞모습 머리 부분
            string ghost = Draft("cctv_odd/ghost_front.png");
            if (File.Exists(ghost))
            {
                Texture2D g = CustomerBuilder.LoadPng(ghost);
                Texture2D face = CropTop(g, 0.4f);
                string path = $"{Dest}/static_face.png";
                File.WriteAllBytes(path, face.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(g);
                UnityEngine.Object.DestroyImmediate(face);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Default;
                ti.isReadable = true;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                ev.staticFace = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        // ── 18 창밖 실종자 ──
        static readonly Vector3[] MissingSpots =
        {
            new Vector3(-1.75f, 0f, 8.55f), new Vector3(1.85f, 0f, 8.55f), new Vector3(-1.3f, 0f, 8.7f),
        };

        static void BuildMissing(Event3333 ev, GameObject root, CctvSystem cctv, Material mat, List<CustomerView> views)
        {
            var holder = new GameObject("Missing At Windows");
            holder.transform.SetParent(root.transform, false);
            // 정면: 창밖 자리(밖이라 실내 그림 구멍으로만 보인다, 빗줄기 뒤). 그림은 그 손님 앞모습을 회색으로
            var fronts = new List<SpriteRenderer>();
            for (int k = 0; k < MissingSpots.Length; k++)
            {
                Vector2 feet = G.ToScreen(MissingSpots[k]);
                var sr = NewSr($"Missing Slot {k}", holder.transform, new Vector3(Mathf.Round(feet.x), Mathf.Round(feet.y), 0.55f), mat, 1);
                float s = 2f * StandZ / MissingSpots[k].z;
                sr.transform.localScale = new Vector3(s, s, 1f);
                sr.color = new Color(0.42f, 0.44f, 0.47f, 1f);
                sr.enabled = false;
                fronts.Add(sr);
            }
            ev.missingFront = fronts.ToArray();

            // CCTV 1번(내 머리 위): 자리마다 큰 화면·모니터. 그림은 손님마다 미리 만들어 두고 실종자 것을 끼운다
            var panels = new List<CctvOverlay>();
            var monitors = new List<CctvOverlay>();
            var arts = new List<Event3333.MissingArt>();
            foreach (var v in views)
            {
                string src = Path.Combine(CustomerBuilder.SourceRoot, v.customerId, "south.png");
                var art = new Event3333.MissingArt { id = v.customerId, front = v.counterBody != null ? v.counterBody.sprite : null };
                if (File.Exists(src))
                {
                    Texture2D t = CustomerBuilder.LoadPng(src);
                    var pl = new List<Sprite>(); var ml = new List<Sprite>();
                    for (int k = 0; k < MissingSpots.Length; k++)
                    {
                        var tmp = new List<CctvOverlay>();
                        AddPair(cctv, holder, mat, t, $"missing_{v.customerId}_{k}", 0, MissingSpots[k], v.heightM, 200, 52, tmp);
                        if (tmp.Count < 2) { pl.Add(null); ml.Add(null); continue; }
                        pl.Add(tmp[0].GetComponent<SpriteRenderer>().sprite);
                        ml.Add(tmp[1].GetComponent<SpriteRenderer>().sprite);
                        // 그림만 남기고, 자리마다 한 쌍만 씬에 둔다(첫 손님 것을 자리 칸으로 쓴다)
                        if (panels.Count <= k) { panels.Add(tmp[0]); monitors.Add(tmp[1]); tmp[0].name = $"{Prefix}missing slot {k} (panel)"; tmp[1].name = $"Missing Slot {k} (monitor)"; }
                        else { UnityEngine.Object.DestroyImmediate(tmp[0].gameObject); UnityEngine.Object.DestroyImmediate(tmp[1].gameObject); }
                    }
                    art.panel = pl.ToArray();
                    art.monitor = ml.ToArray();
                    UnityEngine.Object.DestroyImmediate(t);
                }
                arts.Add(art);
            }
            ev.missingPanel = panels.ToArray();
            ev.missingMonitor = monitors.ToArray();
            ev.missingArt = arts.ToArray();
        }

        // ── 19 붉은 막 너머 형체 ──
        static void BuildRedShape(Event3333 ev, Transform root, CheckoutSystem checkout)
        {
            SpriteRenderer m = checkout.shiftEnd != null ? checkout.shiftEnd.monster : null;
            if (m == null || m.sprite == null) return;
            var sr = NewSr("Red Shape", root, m.transform.position, m.sharedMaterial, 46);
            sr.sprite = m.sprite;
            sr.transform.localScale = m.transform.localScale;
            sr.color = new Color(0.12f, 0.01f, 0.02f, 0f);
            sr.enabled = false;
            ev.redShape = sr;
        }

        static SpriteRenderer NewSr(string name, Transform parent, Vector3 pos, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
