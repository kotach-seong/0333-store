using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 계산 기능을 씬에 넣는다: 금전등록기 숫자, 스캐너 빛, 영수증 프린터, 돈바구니, 영수증 크게 보기,
    /// 물건 끌기(카메라), 영수증 핫스팟. 자리는 매장 그림과 같은 값(StoreArtGenerator)으로 계산한다.
    /// 메뉴 [0333 > 계산대 다시 만들기] 또는 전체 씬 생성 때 함께 실행된다.
    /// </summary>
    public static class CheckoutBuilder
    {
        const string CounterArt = "Assets/_0333/Art/Generated/counter.png";
        // 사용자가 덧그린 그림에서 떼어낸 피범벅·괴물(ArtDrafts/fate에 원본과 변환 스크립트)
        const string BloodPaintArt = "Assets/_0333/Art/Fate/blood_paint.png";
        const string BloodGapArt = "Assets/_0333/Art/Fate/blood_gap.png";
        // PixelLab으로 가게 정면을 피범벅으로 다시 그려 떼어낸 피(2026-11-18): 바닥·벽(blood_paint), 계산대 위(blood_counter)
        const string BloodCounterArt = "Assets/_0333/Art/Fate/blood_counter.png";
        // 흔적 1: 왼쪽 창유리에 달라붙은 손들(PixelLab, 2026-11-18)
        const string WindowHandsArt = "Assets/_0333/Art/Fate/window_hands.png";
        // 가게 전체(1140x640)를 15조각으로 피범벅을 그려 떼어낸 피(2026-11-18): 화면 밖까지 같은 그림체
        const string BloodWorldArt = "Assets/_0333/Art/Fate/blood_world.png";
        const string BloodCounterWorldArt = "Assets/_0333/Art/Fate/blood_counter_world.png";
        const string MonsterArt = "Assets/_0333/Art/Fate/monster.png";
        // 괴물 그림의 왼쪽 위가 놓이는 화면 픽셀(640x360, 위에서부터) — 덧그린 그림에서 서 있던 자리 그대로
        static readonly Vector2Int MonsterScreenTopLeft = new Vector2Int(274, 114);
        const string TitleFontPath = "Assets/_0333/Fonts/Galmuri11.ttf";
        const string BodyFontPath = "Assets/_0333/Fonts/Galmuri9.ttf";
        const string CreditsPath = "Assets/_0333/Data/Credits.txt";
        const string DialoguePath = "Assets/_0333/Data/Dialogue.txt";
        const string PhoneCallsPath = "Assets/_0333/Data/PhoneCalls.txt";

        static readonly (string id, int won)[] Prices =
        {
            ("water", 1000), ("gimbap", 1500), ("ramen", 1300), ("snack", 1700),
            ("bread", 2000), ("drink", 1200), ("soju", 1900), ("pudding", 2200),
        };

        [MenuItem("0333/계산대 다시 만들기 (Rebuild Checkout)")]
        static void Menu()
        {
            var scene = EditorSceneManager.OpenScene(StoreSceneBuilder.ScenePath);
            Material mat = StoreSceneBuilder.GetOrCreateMaterial();
            ItemBayBuilder.AddToScene(mat); // 물건 그림을 집을 수 있게(투명한 곳 판정) 다시 가져온다
            CustomerBuilder.AddToScene(mat); // 영수증의 손님 설명(후드 청년 등)을 새로 넣는다
            AddToScene(FindCamera(), mat);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[0333] 계산대 다시 만들기 완료");
        }

        /// <summary>배치 모드용: 계산 기능만 다시 넣고, -previewDir가 있으면 계산 단계별 화면을 찍는다.</summary>
        public static void RebuildBatch()
        {
            Menu();
            string dir = StoreSceneBuilder.Arg("-previewDir");
            if (!string.IsNullOrEmpty(dir)) RenderPreviews(dir);
        }

        static GameObject FindCamera()
        {
            var look = UnityEngine.Object.FindFirstObjectByType<MouseLookCamera>();
            if (look == null) throw new Exception("씬에 Main Camera(MouseLookCamera)가 없음");
            return look.gameObject;
        }

        public static void AddToScene(GameObject camGo, Material mat)
        {
            // 예전 것 지우기
            foreach (var old in UnityEngine.Object.FindObjectsByType<CheckoutSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (old.receiptView != null && old.receiptView.panel != null) UnityEngine.Object.DestroyImmediate(old.receiptView.panel);
                if (old.checklistView != null && old.checklistView.panel != null) UnityEngine.Object.DestroyImmediate(old.checklistView.panel);
                if (old.shiftEnd != null && old.shiftEnd.panel != null) UnityEngine.Object.DestroyImmediate(old.shiftEnd.panel);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            foreach (string panelName in new[] { "ReceiptPanel", "ChecklistPanel", "Clock HUD", "ShiftEndPanel", "SpeechPanel", "ChoicePanel", "MainMenuPanel", StoryBuilder.PanelName, PauseBuilder.PanelName })
            {
                Transform leftover = camGo.transform.Find(panelName);
                if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover.gameObject);
            }
            foreach (var d in camGo.GetComponents<ItemDragger>()) UnityEngine.Object.DestroyImmediate(d);

            var proj = new StoreProjection { focal = G.Focal, vanishY = G.VanishY, eyeHeight = G.EyeHeight };
            float top = G.CounterTopY;

            var root = new GameObject("Checkout");
            var checkout = root.AddComponent<CheckoutSystem>();
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            checkout.sfx = src;
            checkout.bay = UnityEngine.Object.FindFirstObjectByType<ItemBay>(FindObjectsInactive.Include);
            var allCustomers = new List<CustomerView>(UnityEngine.Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            allCustomers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            // 아침 교대 알바생(지금은 코스프레 그림)은 손님 목록에서 빼고 따로
            checkout.shiftWorker = allCustomers.Find(c => c.customerId == checkout.shiftWorkerId);
            allCustomers.RemoveAll(c => c.customerId == checkout.shiftWorkerId);
            checkout.customers = allCustomers.ToArray();
            checkout.customer = allCustomers.Find(c => c.customerId == "hoodie") ?? (allCustomers.Count > 0 ? allCustomers[0] : null);
            if (checkout.bay == null) throw new Exception("씬에 물품 칸(ItemBay)이 없음");
            var prices = new List<CheckoutSystem.Price>();
            foreach (var p in Prices) prices.Add(new CheckoutSystem.Price { id = p.id, won = p.won });
            checkout.prices = prices.ToArray();

            // 밤 근무 시계(손님 10명 시간표)와 화면 오른쪽 위 DAY·시각
            var clock = root.AddComponent<NightClock>();
            checkout.clock = clock;
            var hudGo = new GameObject("Clock HUD");
            hudGo.transform.SetParent(camGo.transform, false);
            hudGo.transform.localPosition = new Vector3(G.ViewW * 0.5f - 4f - ClockHud.W, G.ViewH * 0.5f - 4f - ClockHud.H, 5f);
            var hudSr = hudGo.AddComponent<SpriteRenderer>();
            hudSr.sharedMaterial = mat;
            hudSr.sortingOrder = 220; // CCTV·수첩·영수증 패널(199~206)보다 위
            var hud = hudGo.AddComponent<ClockHud>();
            hud.clock = clock;
            hud.target = hudSr;
            EditorUtility.SetDirty(clock);
            EditorUtility.SetDirty(hud);

            // 1) 금전등록기 표시창: 구워진 "0.00" 위를 같은 크기로 덮는다
            Rect led = G.RegisterLedArea();
            var ledRect = new RectInt(Mathf.FloorToInt(led.xMin) - 1, Mathf.FloorToInt(led.yMin) - 1, 0, 0);
            ledRect.width = Mathf.CeilToInt(led.xMax) + 1 - ledRect.x;
            ledRect.height = Mathf.CeilToInt(led.yMax) + 1 - ledRect.y;
            SpriteRenderer ledSr = NewRenderer("Register Display", root.transform, new Vector3(ledRect.x, ledRect.y, 0f), mat, 51);
            var display = ledSr.gameObject.AddComponent<RegisterDisplay>();
            display.target = ledSr;
            display.size = new Vector2Int(ledRect.width, ledRect.height);
            SampleLedColors(display, ledRect);
            checkout.display = display;

            // 2) 스캐너 유리의 빨간 빛(구워진 빨간 줄과 같은 자리)
            float midZ = (G.ScanZ0 + G.ScanZ1) * 0.5f, scanY = top + 0.006f;
            Vector2 sa = G.ToScreen(new Vector3(G.ScanX0 + 0.04f, scanY, midZ));
            Vector2 sb = G.ToScreen(new Vector3(G.ScanX1 - 0.04f, scanY, midZ));
            SpriteRenderer scanSr = NewRenderer("Scanner Light", root.transform, new Vector3(Mathf.Round(sa.x) - 3f, Mathf.Floor(sa.y) + 0.5f, 0f), mat, 55);
            scanSr.enabled = false;
            var scanner = scanSr.gameObject.AddComponent<ScannerLight>();
            scanner.target = scanSr;
            scanner.width = Mathf.RoundToInt(sb.x - sa.x);
            checkout.scanner = scanner;

            // 3) 영수증 프린터(금전등록기 출구 → 앞면 → 키 판 위 → 앞 모서리 아래)
            SpriteRenderer stripSr = NewRenderer("Receipt Strip", root.transform, Vector3.zero, mat, 58);
            stripSr.enabled = false;
            var printer = stripSr.gameObject.AddComponent<ReceiptPrinter>();
            printer.target = stripSr;
            printer.projection = proj;
            printer.paperX0 = G.RegX0 + 0.047f;
            printer.paperX1 = G.RegX0 + 0.163f;
            printer.slotY = top + 0.275f;
            printer.frontZ = G.RegZ0 + 0.262f;
            printer.deckY = top + 0.212f;
            printer.deckFrontZ = G.RegZ0 + 0.038f;
            printer.drawerY = top + 0.122f;
            printer.drawerFrontZ = G.RegZ0 - 0.002f;
            printer.bottomY = top + 0.01f;
            checkout.printer = printer;

            var hsGo = new GameObject("Hotspot Receipt");
            hsGo.transform.SetParent(root.transform, false);
            var hotspot = hsGo.AddComponent<Hotspot>();
            hotspot.label = "영수증 보기";
            hotspot.enabled = false; // 영수증이 나와 있을 때만
            UnityEventTools.AddPersistentListener(hotspot.onInteract, checkout.OpenReceipt);
            printer.hotspot = hotspot;

            // 4) 돈바구니
            var trayGo = new GameObject("Money Tray");
            trayGo.transform.SetParent(root.transform, false);
            var tray = trayGo.AddComponent<MoneyTray>();
            tray.material = mat;
            tray.projection = proj;
            tray.xRange = new Vector2(G.TrayX0 + 0.03f, G.TrayX1 - 0.03f);
            tray.zRange = new Vector2(G.TrayZ0 + 0.04f, G.TrayZ1 - 0.04f);
            tray.surfaceHeight = top + 0.01f;
            checkout.tray = tray;

            // 5) 영수증 크게 보기, 점검표 크게 보기(카메라의 자식이라 시선을 따라다닌다)
            checkout.receiptView = BuildPanel<ReceiptView>("Receipt", camGo, mat, root.transform, src);
            // 피 얼룩은 글자(캔버스 205) 위에 덮이도록 206
            SpriteRenderer stains = NewRenderer("Receipt Stains", checkout.receiptView.panel.transform, Vector3.zero, mat, 206);
            stains.enabled = false;
            checkout.receiptView.overlayRenderer = stains;
            checkout.checklistView = BuildPanel<ChecklistView>("Checklist", camGo, mat, root.transform, src);
            checkout.shiftEnd = BuildShiftEnd(camGo, mat, root.transform, src);
            // 손님 말풍선(패널보다 위, 시계보다 아래)과 손님별 대사
            checkout.bubble = BuildSpeechBubble(camGo, mat, src);
            checkout.choices = BuildChoiceView(camGo, mat, src);
            // 게임을 켜면 먼저 뜨는 메인 화면
            checkout.mainMenu = BuildMainMenu(camGo, mat, src);
            // 이상한 손님을 통과시키면 눈과 입이 찢어지며 웃는다
            checkout.grin = checkout.gameObject.AddComponent<CreepyGrin>();
            checkout.grin.sfx = src;
            checkout.dialogueFile = AssetDatabase.LoadAssetAtPath<TextAsset>(DialoguePath);
            checkout.cctv = UnityEngine.Object.FindFirstObjectByType<CctvSystem>(FindObjectsInactive.Include);
            checkout.cctvAnomalies = UnityEngine.Object.FindFirstObjectByType<CctvAnomalies>(FindObjectsInactive.Include);

            // 판정 결과: 흔적(창밖 실루엣·피범벅·괴물)과 실종(유리창 전단·경찰)
            var fateGo = new GameObject("Fate");
            fateGo.transform.SetParent(root.transform, false);
            var fate = fateGo.AddComponent<FateSystem>();
            var effects = fateGo.AddComponent<TraceEffects>();
            Rect winR = G.WindowScreenArea(true), winL = G.WindowScreenArea(false);
            // 흔적 1: 오른쪽 창 너머 형체들(빗줄기 뒤: 비와 같은 층(1)이지만 조금 멀리) — 창틀은 실내(10)가 가린다
            SpriteRenderer ghost = NewRenderer("Window Crowd", fateGo.transform, new Vector3(Mathf.Round(winR.xMin), Mathf.Round(winR.yMin), 0.5f), mat, 1);
            ghost.enabled = false;
            // 흔적 1: 왼쪽 창유리 바깥 면에 달라붙은 손들(빗줄기 앞(3), 실내(10) 아래)
            SpriteRenderer handsSr = NewRenderer("Window Hands", fateGo.transform, new Vector3(Mathf.Round(winL.xMin), Mathf.Round(winL.yMin), 0f), mat, 3);
            handsSr.sprite = ImportBottomLeft(WindowHandsArt);
            handsSr.enabled = false;
            effects.hands = handsSr;
            SpriteRenderer bloodSr = NewRenderer("Blood", fateGo.transform, Vector3.zero, mat, 12);   // 실내(10)·유리(11) 위, 손님(40) 아래
            bloodSr.enabled = false;
            effects.ghost = ghost;
            effects.blood = bloodSr;
            effects.ghostWindow = winR;
            effects.projection = proj;
            effects.world = G.WorldRect;
            effects.bloodPaint = LoadReadable(BloodPaintArt);
            // 새 피 그림은 손님 없는 빈 가게를 그린 것이라 메울 빈자리가 없고, 내장도 그림에 들어 있다
            effects.bloodGap = null;
            effects.gutsOverPaint = false;
            effects.bloodOnCounter = File.Exists(BloodCounterArt) ? LoadReadable(BloodCounterArt) : null;
            SpriteRenderer bloodCounterSr = NewRenderer("Blood On Counter", fateGo.transform, new Vector3(0f, 0f, -0.05f), mat, 50);   // 계산대(50)와 같은 층, 조금 앞
            bloodCounterSr.enabled = false;
            effects.bloodCounter = bloodCounterSr;
            effects.surfaceMask = LoadReadable(StoreSceneBuilder.BloodSurfacePath);
            // 그림은 처음 바라보는 화면(카메라 집 위치 중심) 그대로
            effects.paintRect = new RectInt(Mathf.RoundToInt(G.Home.x) - G.ViewW / 2, Mathf.RoundToInt(G.Home.y) - G.ViewH / 2, G.ViewW, G.ViewH);
            // 가게 전체(둘러볼 수 있는 곳 모두)를 PixelLab으로 피범벅으로 그린 것이 있으면 그것을 쓴다(코드 피는 그리지 않는다)
            if (File.Exists(BloodWorldArt) && File.Exists(BloodCounterWorldArt))
            {
                effects.bloodPaint = LoadReadable(BloodWorldArt);
                effects.bloodOnCounter = LoadReadable(BloodCounterWorldArt);
                effects.paintRect = G.WorldRect;
            }
            var flyerGo = new GameObject("Missing Flyers");
            flyerGo.transform.SetParent(fateGo.transform, false);
            var flyers = flyerGo.AddComponent<FlyerBoard>();
            flyers.window = winL;
            flyers.material = mat;
            fate.effects = effects;
            fate.flyers = flyers;
            checkout.fate = fate;
            hud.fate = fate;

            // 흔적 3의 괴물: 사람 크기로 계산대 앞에 선다(덧그린 그림의 자리 그대로). 그림이 없으면 임시 그림을 2.4배로
            Sprite monsterArt = null;
            if (File.Exists(MonsterArt))
            {
                AssetDatabase.ImportAsset(MonsterArt);
                StoreSceneBuilder.ConfigureSprite(MonsterArt);
                monsterArt = AssetDatabase.LoadAssetAtPath<Sprite>(MonsterArt);
            }
            Vector3 standAt;
            if (monsterArt != null)
            {
                Vector2 size = monsterArt.rect.size;   // 가운데 기준 → 왼쪽 위가 화면 픽셀에 맞도록
                standAt = new Vector3(G.Home.x + MonsterScreenTopLeft.x - G.ViewW / 2f + size.x / 2f, G.Home.y + G.ViewH / 2f - MonsterScreenTopLeft.y - size.y / 2f, 0f);
            }
            else
                standAt = checkout.customer != null && checkout.customer.counterBody != null
                    ? checkout.customer.counterBody.transform.position : new Vector3(-7f, -235f, 0f);
            SpriteRenderer monster = NewRenderer("Monster", root.transform, standAt, mat, 45);   // 손님(40) 위, 계산대(50) 아래
            monster.sprite = monsterArt;
            float monsterScale = monsterArt != null ? 1f : 2.4f;
            monster.transform.localScale = new Vector3(monsterScale, monsterScale, 1f);
            monster.enabled = false;
            checkout.shiftEnd.monster = monster;
            checkout.shiftEnd.monsterHome = standAt;
            // 죽음: 깜빡일 때마다 다가오는 괴물(전신 걷기), 계산대를 넘어 덮치는 모습, 바닥에서 올려다본 화면
            DeathBuilder.Build(checkout.shiftEnd, root.transform, mat);

            // 가게 형광등(정면 편의점 씬 생성 때 만들어진다): 흔적 3에서 하나씩 나간다. 피·전단·물건·돈바구니도 같이 어두워진다
            var storeLights = UnityEngine.Object.FindFirstObjectByType<StoreLights>(FindObjectsInactive.Include);
            checkout.shiftEnd.lights = storeLights;
            checkout.grin.lights = storeLights;   // 손님이 괴물로 변할 때도 형광등이 깜빡인다
            if (storeLights != null)
            {
                // 피·전단은 가게 전체 밝기를, 계산대 위 물건·돈바구니는 바로 위 앞줄 등을 따른다(3:33엔 앞줄만 켜진다)
                storeLights.shadedRoots = new[] { fateGo.transform };
                var counterRoots = new List<Transform> { trayGo.transform };
                if (checkout.bay != null) counterRoots.Add(checkout.bay.transform);
                storeLights.counterRoots = counterRoots.ToArray();
                storeLights.sfx = src;
                EditorUtility.SetDirty(storeLights);
            }
            else Debug.LogWarning("[0333] StoreLights가 없음: 정면 편의점 씬을 다시 생성해야 형광등이 하나씩 꺼진다");
            foreach (var o in new UnityEngine.Object[] { fate, effects, flyers, checkout.shiftEnd, hud }) EditorUtility.SetDirty(o);

            // 계산대 위 점검표 클립보드(스캐너와 금전등록기 사이)를 클릭하면 열린다
            var listGo = new GameObject("Hotspot Checklist");
            listGo.transform.SetParent(root.transform, false);
            var listSpot = listGo.AddComponent<Hotspot>();
            Rect la = G.ChecklistScreenArea();
            listSpot.area = new Rect(la.x - 3f, la.y - 3f, la.width + 6f, la.height + 6f);
            listSpot.label = "점검표 보기";
            UnityEventTools.AddPersistentListener(listSpot.onInteract, checkout.checklistView.Open);

            // 근무 시작 전 점장 전화: 계산대 오른쪽 전화기가 울리고(표시등·액정이 깜빡), 조준해서 클릭하면 받는다
            var phoneGo = new GameObject("Phone");
            phoneGo.transform.SetParent(root.transform, false);
            var phone = phoneGo.AddComponent<PhoneCall>();
            var phoneSrc = phoneGo.AddComponent<AudioSource>();
            phoneSrc.playOnAwake = false;
            phoneSrc.spatialBlend = 0f;
            phone.source = phoneSrc;
            phone.dialog = checkout.shiftEnd;
            AssetDatabase.ImportAsset(PhoneCallsPath);
            phone.callsFile = AssetDatabase.LoadAssetAtPath<TextAsset>(PhoneCallsPath);
            phone.ledArea = G.PhoneLedArea();
            phone.lcdArea = G.PhoneLcdArea();
            // 울릴 때 흔들리도록 계산대 그림에서 전화기만 떼어 낸다(떼어 낸 자리는 둘레 색으로 메운 덮개)
            BuildPhoneCutout(phone, phoneGo.transform, mat);
            phone.led = NewRenderer("Phone LED", phone.body != null ? phone.body.transform : phoneGo.transform, Vector3.zero, mat, 52);
            phone.led.enabled = false;
            var phoneSpotGo = new GameObject("Hotspot Phone");
            phoneSpotGo.transform.SetParent(root.transform, false);
            var phoneSpot = phoneSpotGo.AddComponent<Hotspot>();
            Rect pa = G.PhoneScreenArea();
            phoneSpot.area = new Rect(pa.x - 4f, pa.y - 3f, pa.width + 8f, pa.height + 8f);
            phoneSpot.label = "전화 받기";
            phoneSpot.enabled = false;   // 울릴 때만
            UnityEventTools.AddPersistentListener(phoneSpot.onInteract, phone.Answer);
            phone.hotspot = phoneSpot;
            checkout.phone = phone;
            checkout.notebook = UnityEngine.Object.FindFirstObjectByType<NotebookSystem>(FindObjectsInactive.Include);
            if (checkout.notebook == null) Debug.LogWarning("[0333] 수첩(NotebookSystem)이 없음: 전화로 들은 수칙이 수첩에 적히지 않는다");
            EditorUtility.SetDirty(phone);
            EditorUtility.SetDirty(phoneSpot);

            // 6) 물건 끌어서 스캔(카메라)
            var dragger = camGo.AddComponent<ItemDragger>();
            dragger.look = camGo.GetComponent<MouseLookCamera>();
            dragger.interactor = camGo.GetComponent<HotspotInteractor>();
            dragger.checkout = checkout;
            // 물건 바닥이 스캐너 유리 먼 쪽 모서리를 넘어 내려오면 찍힌다(물품 칸 테두리에서는 안 찍힘)
            float scanFar = G.ToScreen(new Vector3(0f, top, G.ScanZ1)).y;
            dragger.scanLineY = Mathf.Round(scanFar) - 1f;
            dragger.scanXRange = new Vector2(
                Mathf.Round(G.ToScreen(new Vector3(G.BayX0 - 0.06f, top, G.BayZ0)).x),
                Mathf.Round(G.ToScreen(new Vector3(G.BayX1 + 0.06f, top, G.BayZ0)).x));

            foreach (var o in new UnityEngine.Object[] { checkout, display, scanner, printer, hotspot, tray, checkout.receiptView, checkout.checklistView, listSpot, dragger })
                EditorUtility.SetDirty(o);

            // 03:33 이벤트(20가지): 화면 연출·줄 선 사람들·발자국·손·CCTV 덧그림 등
            Event3333Builder.Build(checkout, camGo, mat, src);
            // 오프닝(내 방 → 통화 → 가게로)·엔딩(7일째 아침 → 새벽 주차장 → 역할이 바뀐 통화)
            StoryBuilder.Build(checkout, camGo, mat, src);
            // 일시정지(Esc로만 연다): 계속하기 / 설정 / 메인 화면 / 종료
            PauseBuilder.Build(checkout, camGo, mat);
            // 언어(한국어 / English / 简体中文): 글자표·언어별 대사 파일·중국어 글꼴
            LocBuilder.Build(checkout);
        }

        /// <summary>
        /// 계산대 그림(counter)에서 전화기 픽셀만 떼어 낸다(정면 시선으로 처음 닿는 물체가 "phone"인 픽셀).
        ///  - 몸통: 평소 모습 / 불 켜진 모습(액정이 밝은 초록에 번호 점, 표시등이 빨갛게)
        ///  - 덮개: 전화기가 있던 자리를 같은 줄의 가장 가까운 계산대 색으로 메운 것(흔들릴 때 가장자리에 원래 그림이 비치지 않게)
        /// 평소에는 둘 다 꺼 두고(구워진 전화기가 보인다), 울릴 때만 켠다.
        /// </summary>
        /// <summary>PNG를 왼쪽 아래 기준 스프라이트로 가져온다(없으면 null).</summary>
        static Sprite ImportBottomLeft(string path)
        {
            if (!File.Exists(path)) return null;
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
            s.spritePivot = Vector2.zero;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void BuildPhoneCutout(PhoneCall phone, Transform parent, Material mat)
        {
            var counterGo = GameObject.Find("counter");
            var counterSr = counterGo != null ? counterGo.GetComponent<SpriteRenderer>() : null;
            if (counterSr == null || counterSr.sprite == null) { Debug.LogWarning("[0333] 계산대 그림이 없어 전화기를 떼어 내지 못함"); return; }
            Texture2D tex = CustomerBuilder.LoadPng(AssetDatabase.GetAssetPath(counterSr.sprite));
            Color32[] all = tex.GetPixels32();
            Bounds cb = counterSr.bounds;
            int left = Mathf.RoundToInt(cb.min.x), bottom = Mathf.RoundToInt(cb.min.y);
            Rect pa = G.PhoneScreenArea();
            int x0 = Mathf.FloorToInt(pa.xMin) - 6, y0 = Mathf.FloorToInt(pa.yMin) - 6;
            int w = Mathf.CeilToInt(pa.xMax) + 6 - x0, h = Mathf.CeilToInt(pa.yMax) + 8 - y0;
            var src = new Color32[w * h];
            var isPhone = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int tx = x0 + x - left, ty = y0 + y - bottom;
                    if (tx < 0 || ty < 0 || tx >= tex.width || ty >= tex.height) continue;
                    Color32 c = all[ty * tex.width + tx];
                    src[y * w + x] = c;
                    string hit = c.a > 0 ? G.HitName(x0 + x, y0 + y) : null;
                    isPhone[y * w + x] = hit == "phone" || hit == "handset";   // 몸통과 그 위에 놓인 수화기
                }
            UnityEngine.Object.DestroyImmediate(tex);

            // 액정·표시등은 그려진 색으로 찾는다(초록 액정, 빨간 표시등). 액정 칸의 가운데 줄에 번호 같은 점을 찍는다
            bool IsLcd(Color32 c) => c.g > c.r + 12 && c.g > c.b + 8 && c.g > 60;
            bool IsLed(Color32 c) => c.r > 140 && c.g < 90 && c.b < 90;
            int ly0 = int.MaxValue, ly1 = -1, lx0 = int.MaxValue, lx1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (isPhone[y * w + x] && IsLcd(src[y * w + x])) { ly0 = Mathf.Min(ly0, y); ly1 = Mathf.Max(ly1, y); lx0 = Mathf.Min(lx0, x); lx1 = Mathf.Max(lx1, x); }
            int digitRow = ly1 >= 0 ? (ly0 + ly1) / 2 : -1;
            var cut = new Color32[w * h];
            var lit = new Color32[w * h];
            var patch = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!isPhone[i]) continue;
                    cut[i] = src[i];
                    lit[i] = src[i];
                    if (IsLcd(src[i]))
                    {
                        // 켜진 액정: 밝은 초록 바탕에 걸려 온 번호처럼 보이는 점
                        bool digit = y == digitRow && x > lx0 + 1 && x < lx1 - 1 && (x - lx0) % 3 != 0;
                        lit[i] = digit ? new Color32(40, 86, 50, 255) : new Color32(170, 238, 170, 255);
                    }
                    else if (IsLed(src[i])) lit[i] = new Color32(255, 84, 62, 255);
                    // 덮개: 같은 줄에서 가장 가까운 전화기가 아닌 계산대 색
                    Color32 fill = default;
                    for (int d = 1; d < w && fill.a == 0; d++)
                    {
                        if (x - d >= 0 && !isPhone[i - d] && src[i - d].a > 0) fill = src[i - d];
                        else if (x + d < w && !isPhone[i + d] && src[i + d].a > 0) fill = src[i + d];
                    }
                    patch[i] = fill;
                }

            Sprite Save(Color32[] px, string name)
            {
                string path = $"{StoreSceneBuilder.ArtDir}/{name}.png";
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.SetPixels32(px);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
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
                s.spritePivot = Vector2.zero;
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            phone.patch = NewRenderer("Phone Patch", parent, Vector3.zero, mat, 50);
            phone.patch.transform.position = new Vector3(x0, y0, -0.06f);
            phone.patch.sprite = Save(patch, "phone_patch");
            phone.patch.enabled = false;
            phone.body = NewRenderer("Phone Body", parent, Vector3.zero, mat, 51);
            phone.body.transform.position = new Vector3(x0, y0, -0.07f);
            phone.bodyNormal = Save(cut, "phone_cut");
            phone.bodyLit = Save(lit, "phone_cut_lit");
            phone.body.sprite = phone.bodyNormal;
            phone.body.enabled = false;
        }

        /// <summary>종이를 크게 펼쳐 보여 주는 창(영수증·점검표): 카메라 자식 패널 + 종이 그림 + 글자 캔버스.</summary>
        static T BuildPanel<T>(string name, GameObject camGo, Material mat, Transform root, AudioSource sfx) where T : PaperPanel
        {
            var panel = new GameObject(name + "Panel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);

            SpriteRenderer paper = NewRenderer(name + " Paper", panel.transform, Vector3.zero, mat, 200);

            // 글자: 카메라 화면에 붙는 캔버스. 기준 해상도 640x360이라 1 단위 = 그림 1픽셀(수첩과 같은 방식)
            var canvasGo = new GameObject(name + "Text", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 205;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var viewGo = new GameObject(name + " View");
            viewGo.transform.SetParent(root, false);
            var view = viewGo.AddComponent<T>();
            view.look = camGo.GetComponent<MouseLookCamera>();
            view.viewCamera = camGo.GetComponent<Camera>();
            view.panel = panel;
            view.paperRenderer = paper;
            view.textRoot = (RectTransform)canvasGo.transform;
            view.sfx = sfx;
            view.titleFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            view.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            if (view.titleFont == null || view.bodyFont == null) throw new Exception("갈무리 글꼴을 찾지 못함: " + TitleFontPath);
            panel.SetActive(false);
            return view;
        }

        /// <summary>게임 중에 픽셀을 읽어 쓰는 그림(피범벅 그림 등): 읽기 가능, 압축·밉맵 없음. 파일이 없으면 null.</summary>
        static Texture2D LoadReadable(string path)
        {
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return null;
            ti.textureType = TextureImporterType.Default;
            ti.isReadable = true;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, Vector3 pos, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>매장 그림의 표시창에서 불 켜진 주황색과 바탕색을 뽑아 와 똑같은 색으로 숫자를 그린다.</summary>
        static void SampleLedColors(RegisterDisplay display, RectInt r)
        {
            GameObject counterGo = GameObject.Find("counter");
            var counter = counterGo != null ? counterGo.GetComponent<SpriteRenderer>() : null;
            if (counter == null || !File.Exists(CounterArt)) { Debug.LogWarning("[0333] 계산대 그림을 찾지 못해 기본 LED 색을 씀"); return; }
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(CounterArt));
            int ox = Mathf.RoundToInt(counter.bounds.min.x), oy = Mathf.RoundToInt(counter.bounds.min.y);
            Color32 lit = default;
            float bestLum = -1f;
            var dark = new Dictionary<int, int>();
            for (int y = r.yMin + 1; y < r.yMax - 1; y++)
                for (int x = r.xMin + 1; x < r.xMax - 1; x++)
                {
                    Color32 c = tex.GetPixel(x - ox, y - oy);
                    float lum = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                    if (lum > bestLum && c.r > c.b) { bestLum = lum; lit = c; }
                    if (lum < 0.14f)
                    {
                        int key = (c.r << 16) | (c.g << 8) | c.b;
                        dark.TryGetValue(key, out int n);
                        dark[key] = n + 1;
                    }
                }
            UnityEngine.Object.DestroyImmediate(tex);
            if (bestLum > 0.3f) display.lit = new Color32(lit.r, lit.g, lit.b, 255);
            int bestKey = -1, bestN = 0;
            foreach (var kv in dark) if (kv.Value > bestN) { bestN = kv.Value; bestKey = kv.Key; }
            if (bestKey >= 0)
            {
                var bg = new Color32((byte)(bestKey >> 16), (byte)(bestKey >> 8), (byte)bestKey, 255);
                display.background = bg;
                display.bezel = new Color32((byte)(bg.r / 2), (byte)(bg.g / 2), (byte)(bg.b / 2), 255);
            }
            Debug.Log($"[0333] LED 색: 켜짐 {display.lit}, 바탕 {display.background}");
        }

        /// <summary>검증용: 계산 단계별로 실제 카메라 화면을 찍는다.</summary>
        static void RenderPreviews(string dir)
        {
            Directory.CreateDirectory(dir);
            var checkout = UnityEngine.Object.FindFirstObjectByType<CheckoutSystem>();
            var cam = FindCamera().GetComponent<Camera>();
            GameSettings.PreviewSet();   // 미리보기는 기본 설정으로(에디터에 저장된 설정과 상관없이)
            GameSettings.PreviewLanguage(Lang.Ko);
            // 미리보기 시각: DAY 1, 02:47
            var previewClock = checkout.clock;
            if (previewClock != null) { previewClock.SetPreview(1, 167); var h = cam.GetComponentInChildren<ClockHud>(true); if (h != null) h.Refresh(); }
            var dragger = cam.GetComponent<ItemDragger>();
            var bag = new List<(string id, bool anomaly)>
            {
                ("water", false), ("gimbap", false), ("ramen", false), ("snack", true),
                ("pudding", false), ("water", false), ("soju", false),
            };
            var drag = new Vector2(-24f, dragger.scanLineY - 3f); // 스캐너 유리 위로 끌어온 순간
            string[] names = { "checkout_1_scan", "checkout_2_paid", "checkout_3_receipt", "checkout_4_checklist", "checkout_5_end" };
            for (int stage = 0; stage < names.Length; stage++)
            {
                checkout.PreviewStage(stage, bag, 7, drag);
                PreviewRenderer.Render(cam, Path.Combine(dir, names[stage] + ".png"));
            }
            checkout.PreviewReset();

            // 03:33 이벤트 20가지: 이벤트마다 대표 장면 한 장
            if (checkout.event333 != null)
            {
                var hud333 = cam.GetComponentInChildren<ClockHud>(true);
                for (int n = 1; n <= 20; n++)
                {
                    checkout.event333.PreviewState((Ev333)n);
                    if (hud333 != null) hud333.Refresh();
                    PreviewRenderer.Render(cam, Path.Combine(dir, $"e333_{n:00}_{(Ev333)n}.png"));
                    if ((Ev333)n == Ev333.Mirror && checkout.checklistView != null)
                    {
                        // 거울 가게에서도 점검표가 보이고 판정할 수 있는지(글씨는 거꾸로)
                        checkout.BeginSpecial("03:33", 333, "방문자 1");
                        checkout.checklistView.ShowPreview(checkout.Entries, 1, 333, true, Verdict.Abnormal, Verdict.None);
                        PreviewRenderer.Render(cam, Path.Combine(dir, $"e333_{n:00}b_MirrorChecklist.png"));
                    }
                }
                checkout.event333.PreviewReset();
                if (previewClock != null) { previewClock.SetPreview(1, 167); if (hud333 != null) hud333.Refresh(); }
            }

            // 죽는 장면: 깜빡이는 사이로 다가오는 괴물 3단계 / 덮쳐 오는 중 / 바닥에서 올려다본 화면
            if (checkout.shiftEnd != null && checkout.shiftEnd.stalker != null)
            {
                ShiftEndView se = checkout.shiftEnd;
                Sprite standing = se.monster != null ? se.monster.sprite : null;
                int standingOrder = se.monster != null ? se.monster.sortingOrder : 45;
                var hudD = cam.GetComponentInChildren<ClockHud>(true);
                if (previewClock != null) { previewClock.SetPreview(3, 360); if (hudD != null) hudD.Refresh(); }
                string[] deathNames = { "death_1_door", "death_2_mid", "death_3_near", "death_4_lunge", "death_5_floor" };
                for (int s = 0; s < deathNames.Length; s++)
                {
                    se.PreviewDeath(s);
                    PreviewRenderer.Render(cam, Path.Combine(dir, deathNames[s] + ".png"));
                }
                se.PreviewDeathReset(standing, standingOrder);
                if (previewClock != null) { previewClock.SetPreview(1, 167); if (hudD != null) hudD.Refresh(); }
            }

            // 일시정지 메뉴: 처음 / 설정 / 메인 화면 확인
            var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause != null)
            {
                string[] pauseNames = { "pause_1_main", "pause_2_settings", "pause_3_confirm" };
                for (int s = 0; s < pauseNames.Length; s++)
                {
                    pause.PreviewShow(s);
                    PreviewRenderer.Render(cam, Path.Combine(dir, pauseNames[s] + ".png"));
                }
                pause.PreviewHide();
            }

            // 오프닝·엔딩
            if (checkout.story != null)
            {
                StoryView story = checkout.story;
                MainMenu menu = checkout.mainMenu;
                story.PreviewOpening(0);
                PreviewRenderer.Render(cam, Path.Combine(dir, "story_01_room_ad.png"));
                story.PreviewOpening(1);
                PreviewRenderer.Render(cam, Path.Combine(dir, "story_02_room_call.png"));
                story.PreviewHide();
                if (story.walkFrames.Length > 0)
                {
                    // 1인칭: 비 오는 주차장(우산 아래) / 자동문이 열린 문 앞 / 가게 안
                    story.PreviewWalk(12);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_03_walk_lot.png"));
                    story.PreviewWalk(story.walkInsideFrame - 5);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_04_walk_door.png"));
                    story.PreviewWalk(story.walkInsideFrame + 12);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_04b_walk_inside.png"));
                    story.PreviewHide();
                }
                else if (menu != null)
                {
                    menu.PreviewArrival(5);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_03_arrival_walk.png"));
                    menu.PreviewArrival(-1);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_04_arrival_door.png"));
                    menu.PreviewStoryHide();
                }
                // 7일째 06:00: 처음으로 고개를 든 교대 알바생
                if (previewClock != null) { previewClock.SetPreview(7, 360); var h = cam.GetComponentInChildren<ClockHud>(true); if (h != null) h.Refresh(); }
                checkout.PreviewShiftEnd(0);
                CustomerView worker = null;
                foreach (var v in UnityEngine.Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (v.customerId == "worker") worker = v;
                Sprite workerSaved = worker != null && worker.counterBody != null ? worker.counterBody.sprite : null;
                if (workerSaved != null && story.workerFaceUp != null) worker.counterBody.sprite = story.workerFaceUp;
                if (checkout.shiftEnd != null) checkout.shiftEnd.PreviewDialogue("아침 알바생", "…버텼네.");
                PreviewRenderer.Render(cam, Path.Combine(dir, "story_05_worker_face_up.png"));
                if (workerSaved != null) worker.counterBody.sprite = workerSaved;
                checkout.PreviewReset();
                if (menu != null)
                {
                    var missing = new List<string> { "office", "student" };
                    var letOut = new List<string> { "drunk", "yakuza" };
                    menu.PreviewDawn(missing, letOut);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_06_dawn.png"));
                    menu.PreviewDawn(new List<string>(), new List<string>());
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_06b_dawn_clean.png"));
                    menu.PreviewDawn(missing, letOut);
                    story.PreviewEnding(0);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_07_dawn_phone.png"));
                    story.PreviewEnding(1);
                    PreviewRenderer.Render(cam, Path.Combine(dir, "story_08_dawn_call.png"));
                    story.PreviewHide();
                    menu.PreviewStoryHide();
                }
                story.PreviewEnding(2);
                PreviewRenderer.Render(cam, Path.Combine(dir, "story_09_final_cctv2.png"));
                story.PreviewEnding(3);
                PreviewRenderer.Render(cam, Path.Combine(dir, "story_10_end.png"));
                story.PreviewHide();
                if (previewClock != null) { previewClock.SetPreview(1, 167); var h = cam.GetComponentInChildren<ClockHud>(true); if (h != null) h.Refresh(); }
            }

            // 영수증 이상현상: 계산대 화면(작은 영수증·돈바구니)과 크게 본 영수증
            var cases = new (string name, ReceiptAnomaly flags, int seed)[]
            {
                ("anomaly_a_person_time_number", ReceiptAnomaly.Visitor | ReceiptAnomaly.Time | ReceiptAnomaly.Number, 11),
                ("anomaly_b_fake_total_blood", ReceiptAnomaly.FakeItem | ReceiptAnomaly.Total | ReceiptAnomaly.Blood, 23),
                ("anomaly_c_body_payment", ReceiptAnomaly.BodyPayment, 37),
                ("anomaly_d_person_garble", ReceiptAnomaly.Visitor | ReceiptAnomaly.Number, 4),
                ("anomaly_e_body_payment", ReceiptAnomaly.BodyPayment, 52),
                ("anomaly_f_body_payment", ReceiptAnomaly.BodyPayment, 61),
            };
            foreach (var c in cases)
            {
                checkout.PreviewAnomalies(bag, c.seed, c.flags);
                PreviewRenderer.Render(cam, Path.Combine(dir, c.name + "_counter.png"));
                checkout.receiptView.ShowPreview(checkout.Current);
                PreviewRenderer.Render(cam, Path.Combine(dir, c.name + "_receipt.png"));
                checkout.receiptView.panel.SetActive(false);
                Debug.Log($"[0333] {c.name}: 손님 '{checkout.Current.visitorText}', 시각 {checkout.Current.time}, {checkout.Current.NumberLabel}, 합계 {checkout.Current.total}, 받은 돈 '{checkout.Current.paidText}'");
            }
            // 설정의 혈흔 없애기: 피 묻은 영수증은 같은 모양의 검은 얼룩(판정 단서라 남긴다)
            GameSettings.PreviewSet(blood: true);
            checkout.PreviewAnomalies(bag, 23, ReceiptAnomaly.Blood);
            PreviewRenderer.Render(cam, Path.Combine(dir, "settings_noblood_counter.png"));
            checkout.receiptView.ShowPreview(checkout.Current);
            PreviewRenderer.Render(cam, Path.Combine(dir, "settings_noblood_receipt.png"));
            checkout.receiptView.panel.SetActive(false);
            GameSettings.PreviewSet();
            checkout.PreviewReset();

            // 근무 끝: 교대 알바생 대사 / 결과 화면 / 다음 날 제목
            if (previewClock != null) { previewClock.SetPreview(1, 360); var h = cam.GetComponentInChildren<ClockHud>(true); if (h != null) h.Refresh(); }
            string[] endNames = { "shiftend_1_dialogue", "shiftend_2_results", "shiftend_3_day2" };
            for (int s = 0; s < endNames.Length; s++)
            {
                checkout.PreviewShiftEnd(s);
                PreviewRenderer.Render(cam, Path.Combine(dir, endNames[s] + ".png"));
            }
            checkout.PreviewReset();

            // 판정 결과: 창밖 실루엣(하트 2) / 피범벅(하트 1) / 경찰과 전단 / 괴물 / 죽음 / 체포
            var hudView = cam.GetComponentInChildren<ClockHud>(true);
            if (previewClock != null) previewClock.SetPreview(3, 140);
            // 빈 가게(손님·흔적 없음): 덧그린 그림에서 피·괴물을 떼어낼 때 비교용
            checkout.PreviewFateReset();
            if (hudView != null) hudView.Refresh();
            PreviewRenderer.Render(cam, Path.Combine(dir, "base_empty.png"));
            // 설정의 밝기: 60% / 150%
            GameSettings.PreviewSet(brightness: -4); PreviewRenderer.Render(cam, Path.Combine(dir, "settings_bright_60.png"));
            GameSettings.PreviewSet(brightness: 5); PreviewRenderer.Render(cam, Path.Combine(dir, "settings_bright_150.png"));
            GameSettings.PreviewSet();
            string[] fateNames = { "fate_1_ghost", "fate_2_blood", "fate_3_police", "fate_4_monster", "fate_5_dead", "fate_6_arrest" };
            for (int s = 0; s < fateNames.Length; s++)
            {
                checkout.PreviewFate(s);
                if (hudView != null) hudView.Refresh();
                PreviewRenderer.Render(cam, Path.Combine(dir, fateNames[s] + ".png"));
            }
            // 피범벅인데 계산대가 빈 때(그림에서 손님에게 가려 있던 자리를 코드 피로 메운 모습)
            checkout.PreviewFateReset();
            checkout.PreviewFate(1);
            checkout.customer.SetVisible(false);
            if (hudView != null) hudView.Refresh();
            PreviewRenderer.Render(cam, Path.Combine(dir, "fate_2b_blood_empty.png"));
            // 설정의 혈흔 없애기: 같은 장면(피범벅)에서 바닥·벽·계산대·유리의 피가 안 보인다
            GameSettings.PreviewSet(blood: true);
            checkout.PreviewFate(1);
            if (hudView != null) hudView.Refresh();
            PreviewRenderer.Render(cam, Path.Combine(dir, "settings_noblood_fate2.png"));
            GameSettings.PreviewSet();
            checkout.PreviewFate(1);
            checkout.customer.SetVisible(false);
            // 피범벅을 고개 돌려 본 모습(왼쪽·오른쪽·아래): 정면 밖에도 같은 그림체로 묻어 있는지
            {
                Vector3 home = cam.transform.position;
                var looks = new (Vector3 off, string file)[]
                {
                    (new Vector3(-250f, 10f, 0f), "fate_2c_blood_left"), (new Vector3(250f, 10f, 0f), "fate_2d_blood_right"),
                    (new Vector3(0f, -140f, 0f), "fate_2e_blood_down"), (new Vector3(-200f, 60f, 0f), "fate_2f_blood_upleft"),
                };
                foreach (var l in looks)
                {
                    cam.transform.position = home + l.off;
                    if (hudView != null) hudView.Refresh();
                    PreviewRenderer.Render(cam, Path.Combine(dir, l.file + ".png"));
                }
                cam.transform.position = home;
            }
            // 형광등이 다 나간 가게(괴물이 나오기 직전): 빛나는 것만 남는다
            checkout.shiftEnd.PreviewDark();
            if (hudView != null) hudView.Refresh();
            PreviewRenderer.Render(cam, Path.Combine(dir, "fate_4a_dark.png"));
            GameSettings.PreviewSet(brightness: 5); PreviewRenderer.Render(cam, Path.Combine(dir, "settings_bright_150_dark.png"));
            GameSettings.PreviewSet();
            checkout.PreviewFateReset();
            if (hudView != null) hudView.Refresh();

            // 손님 말풍선: 물건을 올려놓고 한마디(후드 청년) / CCTV만 볼 때(커플) / 계산이 늦을 때(민폐 아줌마)
            var speech = new (string id, string kind, bool cctv, string file)[]
            {
                ("hoodie", CustomerLines.Greet, false, "speech_1_greet"),
                ("couple", CustomerLines.Cctv, true, "speech_2_cctv"),
                ("jinsang_w", CustomerLines.Slow2, false, "speech_3_slow"),
            };
            foreach (var s in speech)
            {
                checkout.PreviewSpeech(s.id, s.kind, s.cctv);
                PreviewRenderer.Render(cam, Path.Combine(dir, s.file + ".png"));
                checkout.PreviewSpeechReset(s.id);
            }
            // 거래 종료 뒤 나가면서: 비정상→정상(찢어진 웃음) / 비정상→비정상(깨진 글자) / 정상→비정상(화냄) / 정상→정상(인사)
            var farewell = new (string id, bool strange, Verdict verdict, string file)[]
            {
                ("hoodie", true, Verdict.Normal, "speech_5_grin"),
                ("couple", true, Verdict.Normal, "speech_5_grin_couple"),
                ("office", true, Verdict.Abnormal, "speech_6_garbled"),
                ("drunk", false, Verdict.Abnormal, "speech_7_angry"),
                ("drunk", false, Verdict.Normal, "speech_8_bye"),
            };
            foreach (var f in farewell)
            {
                checkout.PreviewFarewell(f.id, f.strange, f.verdict);
                PreviewRenderer.Render(cam, Path.Combine(dir, f.file + ".png"));
                checkout.PreviewSpeechReset(f.id);
            }
            RenderGrinLineup(checkout, cam, dir);

            // 인사 + 내가 고를 대답 2개(커플·단소 할아버지)
            foreach (string id in new[] { "couple", "danso" })
            {
                checkout.PreviewChoice(id);
                PreviewRenderer.Render(cam, Path.Combine(dir, $"speech_0_choice_{id}.png"));
                checkout.PreviewSpeechReset(id);
            }

            RenderPhoneAndCctvOdd(checkout, cam, dir);

            // 메인 화면: 처음(저장 없음) / 이어하기(DAY 3) / 설정 / 가끔 일어나는 일(깨진 글자·창밖 형체)
            if (checkout.mainMenu != null)
            {
                checkout.mainMenu.PreviewShow(0); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_1_new.png"));
                checkout.mainMenu.PreviewShow(3); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_2_continue.png"));
                checkout.mainMenu.PreviewShow(3, showSettings: true); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_3_settings.png"));
                checkout.mainMenu.PreviewShow(3, showSettings: true, select: 6); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_3b_settings_blood.png"));
                checkout.mainMenu.PreviewShow(3, glitch: true); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_4_glitch.png"));
                // 진행에 따라: 흔적 2(형체+유리의 피)·전단 2장 / 가만히 두면 모인 시체 6구 / 얼굴 들이밀기 / 죽은 뒤 괴물 / 시작 확대
                var mm = checkout.mainMenu;
                mm.PreviewProgress(2, 2, 0, 0, false, false); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_5_progress.png"));
                mm.PreviewProgress(0, 0, 0, 99, false, false); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_6_watchers.png"));
                mm.PreviewProgress(0, 0, 0, 99, false, false, 1f, true); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_6b_turned.png"));
                mm.PreviewProgress(0, 0, 0, 7, false, false); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_6c_door_shut.png"));
                mm.PreviewProgress(0, 0, 0, 0, true, false); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_7_closeup.png"));
                mm.PreviewProgress(2, 1, 1, 0, false, true); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_8_monster.png"));
                mm.PreviewProgress(0, 0, 0, 0, false, false, 2.6f); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_9_zoom.png"));
                mm.PreviewCredits(0.45f); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_10_credits.png"));
                mm.PreviewCredits(1f); PreviewRenderer.Render(cam, Path.Combine(dir, "menu_11_credits_end.png"));
                checkout.mainMenu.PreviewHide();
            }

            CustomerBuilder.RenderLineup(dir);
            RenderLanguagePreviews(checkout, cam, dir, bag, drag);
            GameSettings.PreviewReset();
            Debug.Log("[0333] 계산 미리보기 저장: " + dir);
        }

        /// <summary>언어 미리보기: 영어·중국어로 주요 화면(메뉴·설정·크레딧·일시정지·스캔·점검표·영수증·말풍선·교대·결과·오프닝·전화·수첩·죽음·3:33 속삭임).</summary>
        static void RenderLanguagePreviews(CheckoutSystem checkout, Camera cam, string dir, List<(string id, bool anomaly)> bag, Vector2 drag)
        {
            var hud = cam.GetComponentInChildren<ClockHud>(true);
            foreach (var (lang, tag) in new[] { (Lang.En, "en"), (Lang.Zh, "zh") })
            {
                GameSettings.PreviewLanguage(lang);
                string P(string n) => Path.Combine(dir, $"lang_{tag}_{n}.png");
                MainMenu mm = checkout.mainMenu;
                if (mm != null)
                {
                    mm.PreviewShow(3); PreviewRenderer.Render(cam, P("01_menu"));
                    mm.PreviewShow(3, showSettings: true, select: GameSettings.LanguageRow); PreviewRenderer.Render(cam, P("02_settings"));
                    mm.PreviewCredits(1f); PreviewRenderer.Render(cam, P("03_credits"));
                    mm.PreviewHide();
                }
                var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
                if (pause != null)
                {
                    pause.PreviewShow(1, GameSettings.LanguageRow); PreviewRenderer.Render(cam, P("04_pause_settings"));
                    pause.PreviewShow(2); PreviewRenderer.Render(cam, P("04b_pause_confirm"));
                    pause.PreviewHide();
                }
                checkout.PreviewStage(0, bag, 7, drag); PreviewRenderer.Render(cam, P("05_scan"));
                checkout.PreviewStage(3, bag, 7, drag); PreviewRenderer.Render(cam, P("06_checklist"));
                checkout.PreviewReset();
                checkout.PreviewAnomalies(bag, 23, ReceiptAnomaly.FakeItem | ReceiptAnomaly.Total | ReceiptAnomaly.Visitor);
                checkout.receiptView.ShowPreview(checkout.Current);
                PreviewRenderer.Render(cam, P("07_receipt"));
                checkout.receiptView.panel.SetActive(false);
                checkout.PreviewReset();
                checkout.PreviewSpeech("hoodie", CustomerLines.Greet, false); PreviewRenderer.Render(cam, P("08_speech"));
                checkout.PreviewSpeechReset("hoodie");
                checkout.PreviewShiftEnd(0); PreviewRenderer.Render(cam, P("09_shiftend"));
                checkout.PreviewShiftEnd(1); PreviewRenderer.Render(cam, P("10_results"));
                checkout.PreviewReset();
                if (checkout.story != null)
                {
                    checkout.story.PreviewOpening(0); PreviewRenderer.Render(cam, P("11_ad"));
                    checkout.story.PreviewOpening(1); PreviewRenderer.Render(cam, P("12_call"));
                    checkout.story.PreviewEnding(1); PreviewRenderer.Render(cam, P("12b_ending_call"));
                    checkout.story.PreviewHide();
                }
                PhoneCall phone = checkout.phone;
                if (phone != null)
                {
                    if (checkout.clock != null) { checkout.clock.SetPreview(1, 0); if (hud != null) hud.Refresh(); }
                    phone.PreviewLine(1, 4); PreviewRenderer.Render(cam, P("13_phone"));
                    checkout.shiftEnd.PreviewHide();
                    phone.PreviewRinging(false);
                    if (checkout.clock != null) { checkout.clock.SetPreview(1, 167); if (hud != null) hud.Refresh(); }
                }
                NotebookSystem note = checkout.notebook;
                if (note != null && phone != null)
                {
                    note.panel.SetActive(true);
                    note.SetRules(phone.RulesUpTo(3));
                    note.SetToday(3, true, 4, checkout.customersPerNight);
                    note.Show(0); PreviewRenderer.Render(cam, P("14_notebook"));
                    note.panel.SetActive(false);
                }
                checkout.PreviewFateReset();
                checkout.PreviewFate(4); PreviewRenderer.Render(cam, P("15_death"));
                checkout.PreviewFateReset();
                if (checkout.event333 != null)
                {
                    checkout.event333.PreviewState(Ev333.Whisper); PreviewRenderer.Render(cam, P("16_e333_whisper"));
                    checkout.event333.PreviewState(Ev333.Returner); PreviewRenderer.Render(cam, P("17_e333_returner"));
                    checkout.event333.PreviewReset();
                }
                if (hud != null) hud.Refresh();
            }
            GameSettings.PreviewLanguage(Lang.Ko);
        }

        /// <summary>
        /// 검증용: 점장 전화(00:00 벨·대사), 수첩(전화 전·DAY 3·DAY 6 수칙과 오늘의 할 일),
        /// CCTV 이상현상(손님 뒤 귀신·괴물·안 찍힘·밖 귀신들, 크게 본 화면과 계산대 위 모니터).
        /// </summary>
        static void RenderPhoneAndCctvOdd(CheckoutSystem checkout, Camera cam, string dir)
        {
            var hud = cam.GetComponentInChildren<ClockHud>(true);
            void Clock(int day, int minute) { if (checkout.clock != null) checkout.clock.SetPreview(day, minute); if (hud != null) hud.Refresh(); }

            PhoneCall phone = checkout.phone;
            if (phone != null)
            {
                Clock(1, 0);
                phone.PreviewRinging(true);
                PreviewRenderer.Render(cam, Path.Combine(dir, "phone_1_ringing.png"));
                phone.PreviewRinging(true, shaken: true);
                PreviewRenderer.Render(cam, Path.Combine(dir, "phone_1b_ringing_shake.png"));
                phone.PreviewRinging(false);
                PreviewRenderer.Render(cam, Path.Combine(dir, "phone_0_idle.png"));
                phone.PreviewLine(1, 4);
                PreviewRenderer.Render(cam, Path.Combine(dir, "phone_2_call_day1.png"));
                Clock(6, 0);
                phone.PreviewLine(6, 1);
                PreviewRenderer.Render(cam, Path.Combine(dir, "phone_3_call_day6.png"));
                checkout.shiftEnd.PreviewHide();
            }

            NotebookSystem note = checkout.notebook;
            if (note != null && phone != null)
            {
                note.panel.SetActive(true);
                var pages = new (int rulesDay, int day, bool called, int served, int spread, string file)[]
                {
                    (0, 1, false, 0, 0, "notebook_1_day1_before_call"),
                    (1, 1, true, 3, 0, "notebook_2_day1_after_call"),
                    (3, 3, true, 4, 0, "notebook_3_day3"),
                    (6, 6, true, 7, 0, "notebook_4_day6"),
                    (6, 6, true, 7, 1, "notebook_5_day6_more"),
                };
                foreach (var p in pages)
                {
                    note.SetRules(phone.RulesUpTo(p.rulesDay));
                    note.SetToday(p.day, p.called, p.served, checkout.customersPerNight);
                    note.Show(p.spread);
                    PreviewRenderer.Render(cam, Path.Combine(dir, p.file + ".png"));
                }
                note.panel.SetActive(false);
            }

            CctvSystem cctv = checkout.cctv;
            CctvAnomalies odd = checkout.cctvAnomalies;
            CustomerView who = checkout.customer;
            if (cctv != null && odd != null && who != null)
            {
                Clock(2, 140);
                var shots = new (CctvOdd kind, int ch, bool panel, string file)[]
                {
                    (CctvOdd.None, 0, true, "cctv_odd_0_normal_ch1"),
                    (CctvOdd.Ghost, 0, true, "cctv_odd_1_ghost_ch1"),
                    (CctvOdd.Ghost, 1, true, "cctv_odd_2_ghost_ch2"),
                    (CctvOdd.Monster, 0, true, "cctv_odd_3_monster_ch1"),
                    (CctvOdd.Monster, 1, true, "cctv_odd_4_monster_ch2"),
                    (CctvOdd.Missing, 0, true, "cctv_odd_5_missing_ch1"),
                    (CctvOdd.Missing, 1, true, "cctv_odd_6_missing_ch2"),
                    (CctvOdd.Watchers, 0, true, "cctv_odd_7_watchers_ch1"),
                    (CctvOdd.Watchers, 2, true, "cctv_odd_8_watchers_ch3"),
                    (CctvOdd.Monster, 0, false, "cctv_odd_9_monster_monitor"),
                    (CctvOdd.Ghost, 1, false, "cctv_odd_10_ghost_monitor"),
                };
                foreach (var s in shots)
                {
                    who.cctvHidden = s.kind == CctvOdd.Missing || s.kind == CctvOdd.Monster;
                    who.SetVisible(true);
                    cctv.panel.SetActive(s.panel);
                    cctv.SetChannel(s.ch);
                    odd.Show(s.kind);
                    PreviewRenderer.Render(cam, Path.Combine(dir, s.file + ".png"));
                }
                odd.Clear();
                who.cctvHidden = false;
                who.SetVisible(false);
                cctv.SetChannel(0);
                cctv.panel.SetActive(false);
            }
        }

        /// <summary>손님 말풍선: 그림(211) + 글자 캔버스(212). 카메라 자식이라 패널 위에 뜨고, 손님이 화면 밖이면 가장자리에 붙는다.</summary>
        static SpeechBubble BuildSpeechBubble(GameObject camGo, Material mat, AudioSource sfx)
        {
            var panel = new GameObject("SpeechPanel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);
            SpriteRenderer box = NewRenderer("Bubble", panel.transform, Vector3.zero, mat, 211);   // 패널(199~206) 위, 시계(220) 아래
            box.enabled = false;

            var canvasGo = new GameObject("SpeechText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 212;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var textGo = new GameObject("Line", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(170f, 14f);
            var text = textGo.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            text.fontSize = 12;
            text.color = new Color32(30, 26, 34, 255);
            text.alignment = TextAnchor.UpperLeft;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.enabled = false;

            var bubble = panel.AddComponent<SpeechBubble>();
            bubble.box = box;
            bubble.label = text;
            bubble.sfx = sfx;
            return bubble;
        }

        /// <summary>
        /// 메인 화면: CCTV 3번 배경(400) · 매장 안의 나와 창밖 형체(401) · 노이즈·주사선(402) · 03:33(403) · 글자(404) · 검은 막(406).
        /// 카메라 자식이라 시계(220)와 패널 위를 덮는다.
        /// </summary>
        static MainMenu BuildMainMenu(GameObject camGo, Material mat, AudioSource sfx)
        {
            var panel = new GameObject("MainMenuPanel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);
            var menu = panel.AddComponent<MainMenu>();
            menu.look = camGo.GetComponent<MouseLookCamera>();
            menu.viewCamera = camGo.GetComponent<Camera>();
            menu.sfx = sfx;
            Vector2 Px(Vector2 uv) => new Vector2(Mathf.Round(uv.x * G.ViewW - G.ViewW / 2f), Mathf.Round(uv.y * G.ViewH - G.ViewH / 2f));

            string bgPath = $"{StoreSceneBuilder.ArtDir}/{G.MenuBackgroundName}.png";
            menu.background = NewRenderer("Menu CCTV", panel.transform, Vector3.zero, mat, 400);
            menu.background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
            if (menu.background.sprite == null) Debug.LogWarning("[0333] 메인 화면 배경이 없음: 정면 편의점 씬을 다시 생성해야 한다 " + bgPath);

            // 매장 안 계산대 뒤에 선 나: 메인 화면 카메라에서 창 너머로 보이는 만큼만(창틀·벽·계산대에 가리는 곳은 지움)
            const int ch = G.MenuCam;
            string playerSrc = Path.Combine(CustomerBuilder.SourceRoot, "player", "south.png");
            menu.player = NewRenderer("Menu Player", panel.transform, Vector3.zero, mat, 401);
            if (File.Exists(playerSrc))
            {
                Vector2 feet = G.CamUV(ch, new Vector3(0f, 0f, 0.3f)), head = G.CamUV(ch, new Vector3(0f, 1.74f, 0.3f));
                Texture2D front = CustomerBuilder.LoadPng(playerSrc);
                Directory.CreateDirectory("Assets/_0333/Art/Customers/player");
                Sprite s = CustomerBuilder.MakeCctvSprite(front, (head.y - feet.y) * G.ViewH, -1f, "Assets/_0333/Art/Customers/player/menu_cctv3.png");
                UnityEngine.Object.DestroyImmediate(front);
                Vector2 at = Px(feet);
                // 창틀·벽·계산대에 가리는 부분은 지우고, 유리 너머로 보이는 부분만 남긴다
                menu.player.sprite = CustomerBuilder.MaskBySight(s, at, new Rect(-G.ViewW / 2f, -G.ViewH / 2f, G.ViewW, G.ViewH), ch, 0.3f);
                menu.player.transform.localPosition = new Vector3(at.x, at.y, 0f);
            }
            // 창밖에 잠깐 비치는 형체: 내가 보이는 창과 반대쪽 창 앞
            Vector2 gFeet = G.CamUV(ch, new Vector3(-1.8f, 0f, 8.7f)), gHead = G.CamUV(ch, new Vector3(-1.8f, 1.9f, 8.7f));
            Vector2 gAt = Px(gFeet);
            menu.ghost = NewRenderer("Menu Ghost", panel.transform, new Vector3(gAt.x, gAt.y, 0f), mat, 401);
            menu.ghost.enabled = false;
            menu.ghostHeight = Mathf.Max(16, Mathf.RoundToInt((gHead.y - gFeet.y) * G.ViewH));

            // CCTV 화면 속 그림들은 한 묶음(시작할 때 문 쪽으로 확대된다). 주사선·노이즈·글자는 화면에 붙어 있다
            var sceneRoot = new GameObject("Menu Scene").transform;
            sceneRoot.SetParent(panel.transform, false);
            foreach (var sr in new[] { menu.background, menu.player, menu.ghost }) sr.transform.SetParent(sceneRoot, true);
            menu.sceneRoot = sceneRoot;
            // 진행에 따라 변하는 것(형체·유리의 피·전단·괴물), 가만히 두면 모여드는 시체들
            MenuExtrasBuilder.Build(menu, sceneRoot, mat);
            var doorCtl = UnityEngine.Object.FindFirstObjectByType<DoorController>(FindObjectsInactive.Include);
            if (doorCtl != null) menu.chime = doorCtl.chime;

            menu.scan = NewRenderer("Menu Scanlines", panel.transform, new Vector3(-G.ViewW / 2f, -G.ViewH / 2f, 0f), mat, 402);
            menu.fx = NewRenderer("Menu Noise", panel.transform, new Vector3(-G.ViewW / 2f, -G.ViewH / 2f, 0f), mat, 402);
            menu.title = NewRenderer("Menu Title", panel.transform, new Vector3(-297f, 44f, 0f), mat, 403);
            menu.fade = NewRenderer("Menu Fade", panel.transform, new Vector3(-G.ViewW / 2f, -G.ViewH / 2f, 0f), mat, 406);
            menu.creditsDim = NewRenderer("Menu Credits Dim", panel.transform, new Vector3(-G.ViewW / 2f, -G.ViewH / 2f, 0f), mat, 403);   // CCTV 화면 위, 글자(404) 아래
            menu.creditsDim.enabled = false;
            menu.fade.enabled = false;

            var canvasGo = new GameObject("MenuText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 404;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            Font title = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath), body = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            Text MakeText(string name, Vector2 pos, Font f, int size, Color32 color, TextAnchor align = TextAnchor.LowerLeft)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = align == TextAnchor.LowerRight ? new Vector2(1f, 0f) : Vector2.zero;
                rt.sizeDelta = new Vector2(240f, 16f);
                rt.anchoredPosition = pos;
                var t = go.AddComponent<Text>();
                t.font = f; t.fontSize = size; t.color = color; t.alignment = align;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = false; t.raycastTarget = false;
                go.AddComponent<Outline>().effectColor = new Color32(6, 8, 10, 220);
                return t;
            }
            for (int i = 0; i < menu.items.Length; i++) menu.items[i] = MakeText($"Item {i + 1}", new Vector2(-292f, 4f - i * 22f), title, 12, menu.textColor);
            for (int i = 0; i < menu.values.Length; i++) menu.values[i] = MakeText($"Value {i + 1}", new Vector2(-170f, 4f - i * 22f), title, 12, menu.textColor);
            menu.rec = MakeText("REC", new Vector2(-304f, 152f), title, 12, new Color32(236, 240, 240, 255));
            menu.rec.supportRichText = true;
            menu.camLabel = MakeText("CAM", new Vector2(304f, 152f), body, 10, new Color32(220, 228, 232, 255), TextAnchor.LowerRight);
            menu.camLabel.text = "CAM 03  주차장";
            menu.stamp = MakeText("Stamp", new Vector2(304f, -170f), body, 10, new Color32(220, 228, 232, 255), TextAnchor.LowerRight);
            menu.hint = MakeText("Hint", new Vector2(-292f, -96f), body, 10, new Color32(170, 180, 186, 255));
            // 크레딧(메인 화면 → 크레딧): 가운데 정렬, 위에서부터 흘러내리는 여러 줄(글자 크기·색은 줄마다 다르다)
            menu.creditsText = MakeText("Credits", Vector2.zero, title, 11, menu.textColor, TextAnchor.UpperCenter);
            menu.creditsText.rectTransform.pivot = new Vector2(0.5f, 1f);
            menu.creditsText.rectTransform.sizeDelta = new Vector2(600f, 10f);
            menu.creditsText.supportRichText = true;
            menu.creditsText.lineSpacing = 1.15f;
            menu.creditsText.enabled = false;
            AssetDatabase.ImportAsset(CreditsPath);
            menu.creditsFile = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
            menu.PreviewHide();   // 평소(에디터·다른 미리보기)에는 숨겨 둔다. 게임을 켜면 CheckoutSystem이 띄운다
            return menu;
        }

        /// <summary>모든 손님의 찢어진 웃음(얼굴 자리를 제대로 찾았는지 확인용): 얼굴 부분만 잘라 한 장에.</summary>
        static void RenderGrinLineup(CheckoutSystem checkout, Camera cam, string dir)
        {
            if (checkout.grin == null) return;
            var views = new List<CustomerView>(UnityEngine.Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            views.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            const int cw = 200, ch = 150, cols = 6;
            int rows = (views.Count + cols - 1) / cols;
            // 변신 1, 변신 2를 한 장씩(변신 그림이 없는 손님은 둘 다 찢어진 웃음 덧그림)
            for (int variant = 0; variant < 2; variant++)
            {
                var sheet = new Texture2D(cols * cw, rows * ch, TextureFormat.RGB24, false);
                for (int i = 0; i < views.Count; i++)
                {
                    foreach (var o in views) o.SetVisible(false);
                    views[i].SetVisible(true);
                    checkout.grin.Show(views[i], 1f, variant);
                    Texture2D shot = PreviewRenderer.Capture(cam, G.ViewW, G.ViewH);
                    // 손님 머리 쪽(화면 가운데 위)만 잘라 낸다
                    int sx = G.ViewW / 2 - cw / 2, sy = G.ViewH / 2 - 95;
                    int cx = i % cols, cy = rows - 1 - i / cols;
                    sheet.SetPixels(cx * cw, cy * ch, cw, ch, shot.GetPixels(sx, sy, cw, ch));
                    UnityEngine.Object.DestroyImmediate(shot);
                    checkout.grin.Hide();
                }
                foreach (var o in views) o.SetVisible(false);
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"customers_turn{variant + 1}.png"), sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);
            }

            // 형광등과 함께 변하는 순서(정장 직장인): 깜빡 꺼짐(평소) / 번쩍(변신) / 어둠 속에서 변한 채 대사 / 나간 뒤 흔적이 생기고 불이 켜짐
            StoreLights lights = checkout.grin.lights;
            CustomerView office = views.Find(v => v.customerId == "office");
            if (lights != null && office != null && office.transforms != null && office.transforms.Length > 0)
            {
                foreach (var o in views) o.SetVisible(false);
                office.SetVisible(true);
                SpriteRenderer body = office.counterBody;
                Sprite normal = body.sprite;
                // 1) 2) 깜빡이는 사이
                lights.SetAll(0.15f);
                body.color = Color.Lerp(lights.darkTint, Color.white, lights.Brightness);
                PreviewRenderer.Render(cam, Path.Combine(dir, "turn_lights_1_flicker.png"));
                lights.SetAll(0.7f);
                body.sprite = office.transforms[0];
                body.color = Color.Lerp(lights.darkTint, Color.white, lights.Brightness);
                PreviewRenderer.Render(cam, Path.Combine(dir, "turn_lights_2_flash.png"));
                // 3) 꺼진 채로 변한 손님이 희미하게 보이며 말한다
                checkout.PreviewFarewell("office", true, Verdict.Normal);
                lights.SetAll(0f);
                body.color = Color.Lerp(lights.darkTint, Color.white, checkout.grin.darkGlow);
                PreviewRenderer.Render(cam, Path.Combine(dir, "turn_lights_3_dark_talk.png"));
                checkout.PreviewSpeechReset("office");
                checkout.grin.Hide();
                body.sprite = normal;
                body.color = Color.white;
                office.SetVisible(false);
                // 4) 아무도 없는 가게: 흔적(피범벅)이 생기고 불이 다시 켜진 뒤
                lights.ResetAll();
                checkout.PreviewFate(1);
                checkout.customer.SetVisible(false);
                PreviewRenderer.Render(cam, Path.Combine(dir, "turn_lights_4_after.png"));
                checkout.PreviewFateReset();
            }

            // 움직이는 변신: 손님 한 줄에 프레임 8칸(띄엄띄엄)
            var animated = views.FindAll(v => v.transformLoops != null && v.transformLoops.Length > 0 && v.transformLoops[0] != null
                                              && v.transformLoops[0].frames != null && v.transformLoops[0].frames.Length > 1);
            if (animated.Count == 0) return;
            const int acols = 8;
            var anim = new Texture2D(acols * cw, animated.Count * ch, TextureFormat.RGB24, false);
            for (int r = 0; r < animated.Count; r++)
            {
                CustomerView v = animated[r];
                int n = v.transformLoops[0].frames.Length;
                for (int c = 0; c < acols; c++)
                {
                    foreach (var o in views) o.SetVisible(false);
                    v.SetVisible(true);
                    checkout.grin.ShowLoopFrame(v, 0, c * n / acols);
                    Texture2D shot = PreviewRenderer.Capture(cam, G.ViewW, G.ViewH);
                    int sx = G.ViewW / 2 - cw / 2, sy = G.ViewH / 2 - 95;
                    anim.SetPixels(c * cw, (animated.Count - 1 - r) * ch, cw, ch, shot.GetPixels(sx, sy, cw, ch));
                    UnityEngine.Object.DestroyImmediate(shot);
                    checkout.grin.Hide();
                }
            }
            foreach (var o in views) o.SetVisible(false);
            anim.Apply();
            File.WriteAllBytes(Path.Combine(dir, "customers_turn_anim.png"), anim.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(anim);
        }

        /// <summary>인사에 대한 내 대답 2개: 버튼 그림(213) + 글자 캔버스(214). 카메라 자식.</summary>
        static ChoiceView BuildChoiceView(GameObject camGo, Material mat, AudioSource sfx)
        {
            var panel = new GameObject("ChoicePanel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);
            var view = panel.AddComponent<ChoiceView>();
            view.look = camGo.GetComponent<MouseLookCamera>();
            view.viewCamera = camGo.GetComponent<Camera>();
            view.sfx = sfx;
            view.interactor = camGo.GetComponent<HotspotInteractor>();   // 조준점으로 대답을 겨누면 괄호를 그려 준다

            var canvasGo = new GameObject("ChoiceText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 214;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer box = NewRenderer($"Choice {i + 1}", panel.transform, Vector3.zero, mat, 213);
                box.enabled = false;
                var textGo = new GameObject($"Choice Text {i + 1}", typeof(RectTransform));
                textGo.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)textGo.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = Vector2.zero;
                var text = textGo.AddComponent<Text>();
                text.font = font;
                text.fontSize = 12;
                text.alignment = TextAnchor.LowerLeft;
                text.supportRichText = false;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.enabled = false;
                view.boxes[i] = box;
                view.labels[i] = text;
            }
            return view;
        }

        /// <summary>근무 끝 장면(검은 막 + 대사창·결과 그림 + 글자 캔버스). 카메라 자식이라 화면에 붙는다.</summary>
        static ShiftEndView BuildShiftEnd(GameObject camGo, Material mat, Transform root, AudioSource sfx)
        {
            var panel = new GameObject("ShiftEndPanel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);
            SpriteRenderer fade = NewRenderer("Fade", panel.transform, Vector3.zero, mat, 300);   // 시계(220)·패널보다 위
            SpriteRenderer board = NewRenderer("Board", panel.transform, Vector3.zero, mat, 305);

            var canvasGo = new GameObject("ShiftEndText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 310;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var viewGo = new GameObject("Shift End View");
            viewGo.transform.SetParent(root, false);
            var view = viewGo.AddComponent<ShiftEndView>();
            view.look = camGo.GetComponent<MouseLookCamera>();
            view.panel = panel;
            view.fade = fade;
            view.board = board;
            view.textRoot = (RectTransform)canvasGo.transform;
            view.sfx = sfx;
            view.titleFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            view.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            panel.SetActive(false);
            EditorUtility.SetDirty(view);
            return view;
        }
    }
}
