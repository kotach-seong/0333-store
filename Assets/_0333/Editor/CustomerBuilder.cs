using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 손님(PixelLab으로 만든 앞·뒷모습)을 씬에 넣는다.
    ///  - 계산대 앞: 앞모습을 2배로 키워 세운다. 하반신은 계산대 그림이 가린다.
    ///  - CCTV 1번(내 머리 위): 앞모습, 계산대에 가려지는 아래쪽은 잘라 낸다.
    ///  - CCTV 2번(입구 쪽): 뒷모습 전신.
    ///  CCTV용 그림은 야간 카메라처럼 흑백으로 바꾸고, 화면 크기에 딱 맞게 미리 줄여 둔다.
    /// 원본: 프로젝트 폴더 ArtDrafts/customers/{id}/south.png(앞), north.png(뒤)
    /// 메뉴 [0333 > 손님 다시 넣기] 또는 전체 씬 생성 때 함께 실행된다.
    /// </summary>
    public static class CustomerBuilder
    {
        const string DestRoot = "Assets/_0333/Art/Customers";
        const float CounterScale = 2f;
        const float CounterTopY = 0.92f, CounterFarZ = 1.85f, CounterNearZ = 0.62f;   // 상판 높이, 손님 쪽 모서리, 알바생 쪽 모서리
        const float Focal = 333f, VanishY = 6f, EyeHeight = 1.6f;
        /// <summary>손님이 계산대 앞에 서는 자리(x 좌우, z 거리, 미터). 모두 같은 자리.</summary>
        const float StandX = -0.05f, StandZ = 2.21f;

        /// <summary>넣을 손님들: CustomerArt.Catalog()(id, 영수증 설명, 키, 사람 수). 그림 폴더가 없는 손님은 건너뛴다.</summary>
        static IEnumerable<(string id, string label, float height, int party, float x, float z)> Customers()
        {
            foreach (var e in CustomerArt.Catalog()) yield return (e.id, e.label, e.heightM, e.party, StandX, StandZ);
        }

        internal static string SourceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "customers"));

        [MenuItem("0333/손님 다시 넣기 (Rebuild Customers)")]
        static void Menu()
        {
            var scene = EditorSceneManager.OpenScene(StoreSceneBuilder.ScenePath);
            AddToScene(StoreSceneBuilder.GetOrCreateMaterial());
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[0333] 손님 다시 넣기 완료");
        }

        /// <summary>배치 모드용: 손님을 다시 넣고, -previewDir가 있으면 기본 화면과 CCTV 1·2번 화면을 찍는다.</summary>
        public static void RebuildBatch()
        {
            Menu();
            string dir = StoreSceneBuilder.Arg("-previewDir");
            if (string.IsNullOrEmpty(dir)) return;

            RenderLineup(dir);
        }

        /// <summary>
        /// 검증용: 손님을 한 명씩 계산대 앞에 세운 화면과 CCTV 2번(뒷모습) 화면을 찍어 격자 한 장씩으로 모은다.
        /// customers_counter.png, customers_cctv2.png
        /// </summary>
        public static void RenderLineup(string dir)
        {
            Directory.CreateDirectory(dir);
            var cam = UnityEngine.Object.FindFirstObjectByType<MouseLookCamera>().GetComponent<Camera>();
            var cctv = UnityEngine.Object.FindFirstObjectByType<CctvSystem>();
            var views = new List<CustomerView>(UnityEngine.Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            views.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            const int w = 640, h = 360, cols = 6;
            var shots = new List<Texture2D>[2] { new List<Texture2D>(), new List<Texture2D>() };
            foreach (var v in views)
            {
                foreach (var o in views) o.SetVisible(false);
                v.SetVisible(true);
                shots[0].Add(PreviewRenderer.Capture(cam, w, h));
                cctv.panel.SetActive(true);
                cctv.SetChannel(1);
                shots[1].Add(PreviewRenderer.Capture(cam, w, h));
                cctv.SetChannel(0);
                cctv.panel.SetActive(false);
            }
            foreach (var o in views) o.SetVisible(false);
            for (int k = 0; k < 2; k++)
            {
                int rows = (shots[k].Count + cols - 1) / cols;
                var sheet = new Texture2D(cols * w, rows * h, TextureFormat.RGB24, false);
                for (int i = 0; i < shots[k].Count; i++)
                {
                    int cx = i % cols, cy = rows - 1 - i / cols;
                    sheet.SetPixels(cx * w, cy * h, w, h, shots[k][i].GetPixels());
                    UnityEngine.Object.DestroyImmediate(shots[k][i]);
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(dir, k == 0 ? "customers_counter.png" : "customers_cctv2.png"), sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);
            }
            Debug.Log($"[0333] 손님 {views.Count}명 미리보기 저장: {dir}");
        }

        public static void AddToScene(Material mat)
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var o in old.cctvImages) if (o != null) UnityEngine.Object.DestroyImmediate(o.gameObject);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            DoorController door = DoorBuilder.AddToScene();

            var cctv = UnityEngine.Object.FindFirstObjectByType<CctvSystem>(FindObjectsInactive.Include);
            if (cctv == null) throw new Exception("씬에 CCTV가 없음");
            // 크게 보기 영상(200)보다 덧그림이 위에 오도록 영상을 한 칸 아래로
            foreach (var f in cctv.panelFeeds) if (f != null) f.sortingOrder = 199;
            // 채널 표시 점의 원래 자리를 저장(에디터에서 채널을 바꿔도 점이 엉뚱한 곳으로 가지 않게)
            if (cctv.channelDot != null && cctv.dotHome == Vector3.zero) cctv.dotHome = cctv.channelDot.localPosition - new Vector3(cctv.channel * cctv.dotStep, 0f, 0f);
            EditorUtility.SetDirty(cctv);

            foreach (var c in Customers())
            {
                string src = Path.Combine(SourceRoot, c.id);
                if (!File.Exists(Path.Combine(src, "south.png"))) { Debug.LogWarning($"[0333] 손님 그림 없음: {src}"); continue; }
                string dest = $"{DestRoot}/{c.id}";
                Directory.CreateDirectory(dest);

                Texture2D front = LoadPng(Path.Combine(src, "south.png"));
                Texture2D back = LoadPng(Path.Combine(src, "north.png"));

                var root = new GameObject($"Customer {c.id}");
                var view = root.AddComponent<CustomerView>();
                view.customerId = c.id;
                view.receiptLabel = c.label;
                view.partySize = c.party;
                view.heightM = c.height;
                view.door = door;

                // 1) 계산대 앞: 발 위치에 맞춰 세우고 2배로
                Sprite body = SaveSprite(front, $"{dest}/front.png");
                var bodyGo = new GameObject("Counter Body");
                bodyGo.transform.SetParent(root.transform, false);
                float sx = Focal * c.x / c.z, feetY = VanishY + Focal * (0f - EyeHeight) / c.z;
                bodyGo.transform.position = new Vector3(Mathf.Round(sx), Mathf.Round(feetY), 0f);
                bodyGo.transform.localScale = new Vector3(CounterScale, CounterScale, 1f);
                var sr = bodyGo.AddComponent<SpriteRenderer>();
                sr.sprite = body; sr.sharedMaterial = mat; sr.sortingOrder = 40; // 실내(10~30)보다 앞, 계산대(50)보다 뒤
                sr.enabled = false; // 손님은 들어오기 전까지 숨어 있다(씬에 24명이 겹쳐 보이지 않게)
                view.counterBody = sr;
                view.bodyHome = bodyGo.transform.localPosition;

                // 변신 그림(이상한 손님을 "예"로 통과시켰을 때): 앞모습과 같은 크기·자리라 같은 기준점으로 가져온다
                // 움직이는 변신은 turn_{n}_f1.png, _f2.png ... (반복 재생)
                var turns = new List<Sprite>();
                var loops = new List<CustomerView.SpriteLoop>();
                Vector2 bodyPivot = new Vector2(body.pivot.x / body.rect.width, body.pivot.y / body.rect.height);
                for (int n = 1; n <= 2; n++)
                {
                    string turnSrc = Path.Combine(src, $"turn_{n}.png");
                    if (!File.Exists(turnSrc)) continue;
                    string turnAsset = $"{dest}/turn_{n}.png";
                    File.Copy(turnSrc, turnAsset, true);
                    turns.Add(ImportSprite(turnAsset, bodyPivot));
                    var frames = new List<Sprite>();
                    for (int k = 1; File.Exists(Path.Combine(src, $"turn_{n}_f{k}.png")); k++)
                    {
                        string fa = $"{dest}/turn_{n}_f{k}.png";
                        File.Copy(Path.Combine(src, $"turn_{n}_f{k}.png"), fa, true);
                        frames.Add(ImportSprite(fa, bodyPivot));
                    }
                    loops.Add(new CustomerView.SpriteLoop { frames = frames.ToArray(), fps = 7f });
                }
                view.transforms = turns.ToArray();
                view.transformLoops = loops.ToArray();

                // 3:33(다시 온 손님): 입이 찢어지게 웃는 얼굴(앞모습과 같은 크기·자리)
                string grinSrc = Path.Combine(src, "grin.png");
                if (File.Exists(grinSrc))
                {
                    string grinAsset = $"{dest}/grin.png";
                    File.Copy(grinSrc, grinAsset, true);
                    view.grinFace = ImportSprite(grinAsset, bodyPivot);
                }

                // 2) CCTV 1번(앞모습, 계산대 아래 잘림), 2번(뒷모습 전신)
                var feet = new Vector3(c.x, 0f, c.z);
                var head = new Vector3(c.x, c.height, c.z);
                var edge = new Vector3(c.x, CounterTopY, CounterFarZ);
                for (int ch = 0; ch < 2; ch++)
                {
                    Texture2D srcTex = ch == 0 ? front : back;
                    Vector2 uvFeet = StoreArtGenerator.CamUV(ch, feet), uvHead = StoreArtGenerator.CamUV(ch, head);
                    float? cutV = ch == 0 ? StoreArtGenerator.CamUV(ch, edge).y : (float?)null;

                    // 크게 보기 화면
                    RectInt fr = StoreArtGenerator.PanelFeedRect;
                    Sprite panelSprite = MakeCctvSprite(srcTex, (uvHead.y - uvFeet.y) * fr.height, cutV.HasValue ? (cutV.Value - uvFeet.y) * fr.height : -1f, $"{dest}/cctv{ch + 1}_panel.png");
                    var pGo = new GameObject($"CCTV{ch + 1} {c.id} (panel)");
                    pGo.transform.SetParent(cctv.panel.transform, false);
                    pGo.transform.localPosition = new Vector3(Mathf.Round(fr.x + uvFeet.x * fr.width), Mathf.Round(fr.y + uvFeet.y * fr.height), 0f);
                    view.cctvImages.Add(AddOverlay(pGo, panelSprite, mat, 201, ch));   // 계산대 뒤 주인공(200)보다 앞

                    // 계산대 위 작은 모니터
                    SpriteRenderer screen = cctv.monitorScreens[ch];
                    Bounds b = screen.bounds;
                    Sprite monSprite = MakeCctvSprite(srcTex, (uvHead.y - uvFeet.y) * b.size.y, cutV.HasValue ? (cutV.Value - uvFeet.y) * b.size.y : -1f, $"{dest}/cctv{ch + 1}_monitor.png");
                    var mGo = new GameObject($"CCTV{ch + 1} {c.id} (monitor)");
                    mGo.transform.SetParent(root.transform, false);
                    mGo.transform.position = new Vector3(Mathf.Round(b.min.x + uvFeet.x * b.size.x), Mathf.Round(b.min.y + uvFeet.y * b.size.y), 0f);
                    view.cctvImages.Add(AddOverlay(mGo, monSprite, mat, 53, ch));   // 계산대 뒤 주인공(52)보다 앞
                }

                UnityEngine.Object.DestroyImmediate(front);
                UnityEngine.Object.DestroyImmediate(back);
                EditorUtility.SetDirty(view);
            }

            // 주인공(나): 문 쪽 CCTV(2번)에 계산대 뒤에 선 내가 늘 찍혀 있다
            AddPlayerToCctv(cctv, mat);
            // CCTV에서만 보이는 것들(손님 뒤 귀신, 괴물, 밖에서 보는 귀신들)
            AddCctvAnomalies(cctv, mat);
        }

        const string OddRootName = "CCTV Anomalies", OddPrefix = "CCTV odd ";
        const string OddDest = "Assets/_0333/Art/CctvOdd";
        /// <summary>손님 뒤에 붙은 귀신이 서는 자리(손님 오른쪽 어깨 뒤, 살짝 떠 있음)와 키.</summary>
        const float GhostDX = 0.28f, GhostDZ = 0.42f, GhostFloat = 0.12f, GhostHeight = 1.84f;
        const float MonsterHeight = 2.12f;
        /// <summary>밖에서 가게 안을 보는 귀신들(x, z, 키): 왼쪽 창, 문 유리, 오른쪽 창.</summary>
        static readonly Vector3[] Watchers = { new Vector3(-1.75f, 9.0f, 1.72f), new Vector3(0.38f, 10.6f, 1.8f), new Vector3(1.85f, 9.35f, 1.66f) };

        /// <summary>
        /// CCTV에서만 보이는 그림을 큰 화면·작은 모니터에 미리 만들어 두고 CctvAnomalies에 모은다(평소엔 꺼져 있음).
        ///  - 귀신: 1번(앞모습, 손님보다 뒤), 2번(뒷모습, 손님보다 앞)
        ///  - 괴물: 손님 자리에 1번(앞모습)·2번(뒷모습), 손님 자신은 CCTV에서 지운다
        ///  - 밖 귀신들: 1번(창 너머, 하얀 눈), 3번(주차장에서 본 뒷모습)
        /// 벽·계산대·문틀에 가리는 곳은 시선을 쏴서 지운다.
        /// </summary>
        static void AddCctvAnomalies(CctvSystem cctv, Material mat)
        {
            var oldRoot = GameObject.Find(OddRootName);
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
            var stale = new List<GameObject>();
            foreach (Transform t in cctv.panel.transform) if (t.name.StartsWith(OddPrefix)) stale.Add(t.gameObject);
            foreach (var g in stale) UnityEngine.Object.DestroyImmediate(g);
            Directory.CreateDirectory(OddDest);

            var root = new GameObject(OddRootName);
            var odd = root.AddComponent<CctvAnomalies>();

            var ghostFront = OddArt("ghost_front", () => CctvOddArt.Ghost(true));
            var ghostBack = OddArt("ghost_back", () => CctvOddArt.Ghost(false));
            var gFeet = new Vector3(StandX + GhostDX, GhostFloat, StandZ + GhostDZ);
            odd.ghost.AddRange(AddOdd(cctv, root, mat, ghostFront, "ghost", 0, gFeet, GhostHeight, 200, 52));
            odd.ghost.AddRange(AddOdd(cctv, root, mat, ghostBack, "ghost", 1, gFeet, GhostHeight, 202, 54));

            var monFront = OddArt("monster_front", () => CctvOddArt.Monster(true));
            var monBack = OddArt("monster_back", () => CctvOddArt.Monster(false));
            var mFeet = new Vector3(StandX, 0f, StandZ);
            odd.monster.AddRange(AddOdd(cctv, root, mat, monFront, "monster", 0, mFeet, MonsterHeight, 201, 53));
            odd.monster.AddRange(AddOdd(cctv, root, mat, monBack, "monster", 1, mFeet, MonsterHeight, 201, 53));

            for (int i = 0; i < Watchers.Length; i++)
            {
                var w = Watchers[i];
                var feet = new Vector3(w.x, 0f, w.y);
                int wi = i;
                var face = OddArt($"watcher{i + 1}_front", () => CctvOddArt.Watcher(true, wi));
                var back = OddArt($"watcher{i + 1}_back", () => CctvOddArt.Watcher(false, wi));
                odd.watchers.AddRange(AddOdd(cctv, root, mat, face, $"watcher{i + 1}", 0, feet, w.z, 200, 52));
                odd.watchers.AddRange(AddOdd(cctv, root, mat, back, $"watcher{i + 1}", 2, feet, w.z, 200, 52));
                UnityEngine.Object.DestroyImmediate(face);
                UnityEngine.Object.DestroyImmediate(back);
            }
            foreach (var t in new[] { ghostFront, ghostBack, monFront, monBack }) UnityEngine.Object.DestroyImmediate(t);
            odd.Clear();
            EditorUtility.SetDirty(odd);
        }

        /// <summary>
        /// CCTV 이상현상 그림: 프로젝트 폴더 ArtDrafts/cctv_odd/{name}.png(PixelLab으로 그린 것)가 있으면 그것,
        /// 없으면 코드로 그린 임시 그림.
        /// </summary>
        static Texture2D OddArt(string name, Func<Texture2D> fallback)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "cctv_odd", name + ".png"));
            return File.Exists(path) ? LoadPng(path) : fallback();
        }

        /// <summary>한 채널에 그림 하나를 큰 화면용·모니터용 두 장으로 올린다(발이 feet에 닿게, 키 heightM).</summary>
        static IEnumerable<CctvOverlay> AddOdd(CctvSystem cctv, GameObject root, Material mat, Texture2D src, string name,
            int ch, Vector3 feet, float heightM, int panelOrder, int monitorOrder)
        {
            Vector2 uvFeet = StoreArtGenerator.CamUV(ch, feet);
            Vector2 uvHead = StoreArtGenerator.CamUV(ch, feet + Vector3.up * heightM);

            RectInt fr = StoreArtGenerator.PanelFeedRect;
            Sprite panelSprite = MakeCctvSprite(src, (uvHead.y - uvFeet.y) * fr.height, -1f, $"{OddDest}/{name}_cctv{ch + 1}_panel.png", true);
            var pGo = new GameObject($"{OddPrefix}{name} cctv{ch + 1} (panel)");
            pGo.transform.SetParent(cctv.panel.transform, false);
            pGo.transform.localPosition = new Vector3(Mathf.Round(fr.x + uvFeet.x * fr.width), Mathf.Round(fr.y + uvFeet.y * fr.height), 0f);
            panelSprite = MaskBySight(panelSprite, pGo.transform.localPosition, new Rect(fr.x, fr.y, fr.width, fr.height), ch, feet.z);
            CctvOverlay po = AddOverlay(pGo, panelSprite, mat, panelOrder, ch);

            Bounds b = cctv.monitorScreens[ch].bounds;
            Sprite monSprite = MakeCctvSprite(src, (uvHead.y - uvFeet.y) * b.size.y, -1f, $"{OddDest}/{name}_cctv{ch + 1}_monitor.png", true);
            var mGo = new GameObject($"{OddPrefix}{name} cctv{ch + 1} (monitor)");
            mGo.transform.SetParent(root.transform, false);
            mGo.transform.position = new Vector3(Mathf.Round(b.min.x + uvFeet.x * b.size.x), Mathf.Round(b.min.y + uvFeet.y * b.size.y), 0f);
            monSprite = MaskBySight(monSprite, mGo.transform.position, new Rect(b.min.x, b.min.y, b.size.x, b.size.y), ch, feet.z);
            CctvOverlay mo = AddOverlay(mGo, monSprite, mat, monitorOrder, ch);

            EditorUtility.SetDirty(po);
            EditorUtility.SetDirty(mo);
            return new[] { po, mo };
        }

        const string PlayerId = "player";
        /// <summary>주인공이 서 있는 자리(계산대 안쪽, 눈높이 1.6m의 바로 아래)와 키.</summary>
        const float PlayerX = 0f, PlayerZ = 0.3f, PlayerHeight = 1.74f;
        const string PlayerRootName = "Player On CCTV", PlayerPanelName = "CCTV2 player (panel)";

        /// <summary>
        /// CCTV 2번(문 위에서 계산대를 보는 카메라)에 주인공 앞모습. 계산대에 가려 그 아래는 지운다.
        /// 1번 카메라는 주인공 바로 뒤 위에 있어서 주인공이 화면에 들어오지 않는다.
        /// </summary>
        static void AddPlayerToCctv(CctvSystem cctv, Material mat)
        {
            var oldRoot = GameObject.Find(PlayerRootName);
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
            Transform oldPanel = cctv.panel.transform.Find(PlayerPanelName);
            if (oldPanel != null) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);

            string src = Path.Combine(SourceRoot, PlayerId);
            if (!File.Exists(Path.Combine(src, "south.png"))) return;
            string dest = $"{DestRoot}/{PlayerId}";
            Directory.CreateDirectory(dest);
            Texture2D front = LoadPng(Path.Combine(src, "south.png"));

            const int ch = 1;
            var feet = new Vector3(PlayerX, 0f, PlayerZ);
            var head = new Vector3(PlayerX, PlayerHeight, PlayerZ);
            Vector2 uvFeet = StoreArtGenerator.CamUV(ch, feet), uvHead = StoreArtGenerator.CamUV(ch, head);

            var root = new GameObject(PlayerRootName);
            RectInt fr = StoreArtGenerator.PanelFeedRect;
            Sprite panelSprite = MakeCctvSprite(front, (uvHead.y - uvFeet.y) * fr.height, -1f, $"{dest}/cctv{ch + 1}_panel.png");
            var pGo = new GameObject(PlayerPanelName);
            pGo.transform.SetParent(cctv.panel.transform, false);
            pGo.transform.localPosition = new Vector3(Mathf.Round(fr.x + uvFeet.x * fr.width), Mathf.Round(fr.y + uvFeet.y * fr.height), 0f);
            // 계산대·계산대 위 물건에 가리는 부분은 지운다(시선을 실제로 쏴서)
            panelSprite = MaskBySight(panelSprite, pGo.transform.localPosition, new Rect(fr.x, fr.y, fr.width, fr.height), ch, PlayerZ);
            CctvOverlay po = AddOverlay(pGo, panelSprite, mat, 200, ch);

            SpriteRenderer screen = cctv.monitorScreens[ch];
            Bounds b = screen.bounds;
            Sprite monSprite = MakeCctvSprite(front, (uvHead.y - uvFeet.y) * b.size.y, -1f, $"{dest}/cctv{ch + 1}_monitor.png");
            var mGo = new GameObject("CCTV2 player (monitor)");
            mGo.transform.SetParent(root.transform, false);
            mGo.transform.position = new Vector3(Mathf.Round(b.min.x + uvFeet.x * b.size.x), Mathf.Round(b.min.y + uvFeet.y * b.size.y), 0f);
            monSprite = MaskBySight(monSprite, mGo.transform.position, new Rect(b.min.x, b.min.y, b.size.x, b.size.y), ch, PlayerZ);
            CctvOverlay mo = AddOverlay(mGo, monSprite, mat, 52, ch);

            foreach (CctvOverlay o in new[] { po, mo }) { o.present = true; o.Refresh(cctv.channel); EditorUtility.SetDirty(o); }
            UnityEngine.Object.DestroyImmediate(front);
        }

        internal static CctvOverlay AddOverlay(GameObject go, Sprite sprite, Material mat, int order, int channel)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sharedMaterial = mat; sr.sortingOrder = order;
            var o = go.AddComponent<CctvOverlay>();
            o.channel = channel;
            o.present = false; // 손님이 들어오기 전에는 CCTV에도 없다
            o.Refresh(0);
            return o;
        }

        /// <summary>
        /// 덧그린 사람 그림에서 카메라 시선이 벽·문틀·계산대에 막히는 픽셀을 지운다(유리 너머는 남긴다).
        /// placedAt: 그림의 기준점(발)이 놓인 자리, feed: 영상 0~1이 차지하는 같은 좌표계의 사각형, targetZ: 그 사람이 선 깊이.
        /// </summary>
        internal static Sprite MaskBySight(Sprite sprite, Vector2 placedAt, Rect feed, int channel, float targetZ)
        {
            if (sprite == null) return null;
            string path = AssetDatabase.GetAssetPath(sprite);
            Texture2D tex = LoadPng(path);
            Color32[] px = tex.GetPixels32();
            float ppu = sprite.pixelsPerUnit;
            float left = placedAt.x - sprite.pivot.x / ppu, bottom = placedAt.y - sprite.pivot.y / ppu;
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    int i = y * tex.width + x;
                    if (px[i].a == 0) continue;
                    float u = (left + (x + 0.5f) / ppu - feed.x) / feed.width, v = (bottom + (y + 0.5f) / ppu - feed.y) / feed.height;
                    if (!StoreArtGenerator.SeesThrough(channel, u, v, targetZ)) px[i] = new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static Texture2D LoadPng(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        static (int top, int bottom) OpaqueRows(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            int top = -1, bottom = -1; // 텍스처 y(아래가 0)
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                    if (px[y * t.width + x].a > 40) { if (bottom < 0) bottom = y; top = y; break; }
            return (top, bottom);
        }

        /// <summary>
        /// CCTV용 그림: 사람 키가 heightPx가 되도록 줄이고(구역 평균), 야간 카메라처럼 흑백으로 밝게.
        /// cutPx가 0 이상이면 발에서 그만큼 위까지는 지운다(계산대에 가려지는 부분).
        /// dark가 1보다 작으면 그만큼 어둡게(실루엣처럼). 남긴 하얀 눈은 어둡게 하지 않는다.
        /// </summary>
        internal static Sprite MakeCctvSprite(Texture2D src, float heightPx, float cutPx, string assetPath, bool keepGlow = false, float dark = 1f)
        {
            var (top, bottom) = OpaqueRows(src);
            float s = Mathf.Max(0.05f, heightPx / (top - bottom + 1));
            int w = Mathf.Max(1, Mathf.CeilToInt(src.width * s)), h = Mathf.Max(1, Mathf.CeilToInt((top + 1) * s));
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color32[] sp = src.GetPixels32();
            var dp = new Color32[w * h];
            int feetRow = Mathf.FloorToInt(bottom * s);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 대상 픽셀 하나가 덮는 원본 구역을 평균
                    int x0 = Mathf.FloorToInt(x / s), x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt((x + 1) / s));
                    int y0 = Mathf.FloorToInt(y / s), y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt((y + 1) / s));
                    float lum = 0f; int n = 0, cover = 0, glow = 0;
                    for (int yy = y0; yy < y1 && yy < src.height; yy++)
                        for (int xx = x0; xx < x1 && xx < src.width; xx++)
                        {
                            Color32 c = sp[yy * src.width + xx];
                            n++;
                            if (c.a <= 40) continue;
                            cover++;
                            lum += (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                            if (c.r > 240 && c.g > 240 && c.b > 230) glow++;
                        }
                    bool shine = keepGlow && glow > 0;   // 작게 줄여도 하얀 눈은 남긴다
                    if (n == 0 || (cover * 2 < n && !shine)) continue;
                    if (cutPx >= 0f && y - feetRow < cutPx) continue;
                    float l = shine ? 1f : lum / cover;
                    l = l * 2.6f / (1f + 1.6f * l);                       // 야간 카메라 밝기
                    l = Mathf.Clamp01(l * 0.85f + 0.06f + ((x * 7 + y * 13) % 5 - 2) * 0.012f);
                    if (!shine) l *= dark;
                    dp[y * w + x] = new Color(0.74f * l, 0.82f * l, 0.86f * l, 1f);
                }
            dst.SetPixels32(dp);
            dst.Apply();
            File.WriteAllBytes(assetPath, dst.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(dst);
            return ImportSprite(assetPath, new Vector2(0.5f, (float)feetRow / h));
        }

        static Sprite SaveSprite(Texture2D t, string assetPath)
        {
            File.WriteAllBytes(assetPath, t.EncodeToPNG());
            var (_, bottom) = OpaqueRows(t);
            return ImportSprite(assetPath, new Vector2(0.5f, (float)bottom / t.height));
        }

        internal static Sprite ImportSprite(string assetPath, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = true; // 근무 끝 결과 화면에서 얼굴을 잘라 초상화로 쓴다
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
