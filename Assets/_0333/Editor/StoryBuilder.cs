using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 오프닝·엔딩(StoryView)을 씬에 넣는다. CheckoutBuilder.AddToScene 끝에서 부른다(메인 화면·CCTV·손님이 다 있은 뒤).
    ///  - 카메라 자식 "StoryPanel": 검은 바탕·내 방·CCTV 2번 영상·새 알바·휴대폰·대사창·검은 막(450~475), 글자 캔버스(470)
    ///  - 메인 화면 주차장 CCTV의 오프닝·엔딩 그림(MenuExtrasBuilder.BuildStory)
    ///  - 엔딩: 교대 알바생이 고개를 든 얼굴, CCTV 2번 계산대 뒤의 새 알바
    /// 그림 원본: ArtDrafts/ending/room.png(PixelLab), customers/worker/face_up.png, customers/newhire/south.png
    /// </summary>
    public static class StoryBuilder
    {
        const string Dest = "Assets/_0333/Art/Story";
        const string StoryPath = "Assets/_0333/Data/Story.txt";
        const string TitleFontPath = "Assets/_0333/Fonts/Galmuri11.ttf";
        const string BodyFontPath = "Assets/_0333/Fonts/Galmuri9.ttf";
        public const string PanelName = "StoryPanel";
        const string VoicePath = "Assets/_0333/Audio/phone_voice.wav";
        const string SlowVoicePath = "Assets/_0333/Audio/phone_voice_slow.wav";

        static string Draft(string rel) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", rel));

        public static StoryView Build(CheckoutSystem checkout, GameObject camGo, Material mat, AudioSource src)
        {
            Directory.CreateDirectory(Dest);
            var views = new List<CustomerView>(UnityEngine.Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            views.RemoveAll(v => v == null);
            views.Sort((a, b) => string.CompareOrdinal(a.customerId, b.customerId));
            if (checkout.mainMenu != null) MenuExtrasBuilder.BuildStory(checkout.mainMenu, checkout.mainMenu.sceneRoot, mat, views);

            var panel = new GameObject(PanelName);
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);
            var go = new GameObject("Story View");
            go.transform.SetParent(checkout.transform, false);
            var story = go.AddComponent<StoryView>();
            story.panel = panel;
            story.look = camGo.GetComponent<MouseLookCamera>();
            story.viewCamera = camGo.GetComponent<Camera>();
            story.sfx = src;
            AssetDatabase.ImportAsset(StoryPath);
            story.storyFile = AssetDatabase.LoadAssetAtPath<TextAsset>(StoryPath);

            story.backdrop = Sr("Backdrop", panel.transform, mat, 450);
            story.room = Sr("Room", panel.transform, mat, 451);
            story.feed = Sr("CCTV2 Feed", panel.transform, mat, 452);
            story.newHire = Sr("New Hire", panel.transform, mat, 453);
            story.phone = Sr("Phone", panel.transform, mat, 455);
            story.box = Sr("Dialogue Box", panel.transform, mat, 466);
            story.fade = Sr("Fade", panel.transform, mat, 475);
            story.overlay = Sr("Walk Overlay", panel.transform, mat, 454);

            // 오프닝 1인칭 걸음(RenderWalkInBatch로 미리 그려 둔 장들). 하나라도 빠지면 쓰지 않는다(주차장 CCTV로 대신)
            List<WalkStep> steps = WalkPath();
            var frames = new List<Sprite>();
            for (int i = 0; i < steps.Count; i++)
            {
                Sprite f = AssetDatabase.LoadAssetAtPath<Sprite>(WalkFrame(i));
                if (f == null) { frames.Clear(); Debug.LogWarning($"[0333] 1인칭 걸음 그림이 모자람({i}번): RenderWalkInBatch를 먼저 돌려야 한다"); break; }
                frames.Add(f);
            }
            story.walkFrames = frames.ToArray();
            story.walkDoorFrame = Mathf.Max(0, steps.FindIndex(s => s.gap > 0.01f));
            story.walkInsideFrame = Mathf.Max(0, steps.FindIndex(s => s.inside));
            story.walkFps = WalkFps;
            story.walkStepFrames = StepLength / WalkStride;
            var doorCtl = UnityEngine.Object.FindFirstObjectByType<DoorController>(FindObjectsInactive.Include);
            if (doorCtl != null) story.chime = doorCtl.chime;

            // 내 방(밤, 비)
            string roomSrc = Draft("ending/room.png");
            if (File.Exists(roomSrc))
            {
                string roomAsset = $"{Dest}/room.png";
                File.Copy(roomSrc, roomAsset, true);
                story.room.sprite = CustomerBuilder.ImportSprite(roomAsset, new Vector2(0.5f, 0.5f));
            }
            else Debug.LogWarning("[0333] 오프닝 방 그림이 없음: " + roomSrc);

            // 글자 캔버스
            var canvasGo = new GameObject("StoryText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 470;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            story.textRoot = (RectTransform)canvasGo.transform;
            story.titleFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            story.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);

            // 마지막 장면: CCTV 2번(평소 영상)과 계산대 뒤의 새 알바(내가 서 있던 자리)
            CctvSystem cctv = checkout.cctv;
            if (cctv != null && cctv.panelFeeds != null && cctv.panelFeeds.Length > 1 && cctv.panelFeeds[1] != null)
            {
                story.feedSprite = cctv.panelFeeds[1].sprite;
                story.feedAt = cctv.panelFeeds[1].transform.localPosition;
            }
            string hireSrc = Path.Combine(CustomerBuilder.SourceRoot, "newhire", "south.png");
            if (File.Exists(hireSrc))
            {
                const int ch = 1;
                var feet = new Vector3(0f, 0f, 0.3f);
                Vector2 uvFeet = G.CamUV(ch, feet), uvHead = G.CamUV(ch, feet + Vector3.up * 1.74f);
                RectInt fr = G.PanelFeedRect;
                Texture2D tex = CustomerBuilder.LoadPng(hireSrc);
                Sprite s = CustomerBuilder.MakeCctvSprite(tex, (uvHead.y - uvFeet.y) * fr.height, -1f, $"{Dest}/newhire_cctv2.png");
                UnityEngine.Object.DestroyImmediate(tex);
                var at = new Vector2(Mathf.Round(fr.x + uvFeet.x * fr.width), Mathf.Round(fr.y + uvFeet.y * fr.height));
                story.newHireSprite = CustomerBuilder.MaskBySight(s, at, new Rect(fr.x, fr.y, fr.width, fr.height), ch, feet.z);
                story.newHireAt = at;
            }
            else Debug.LogWarning("[0333] 새 알바 그림이 없음: " + hireSrc);

            // 7일째 아침: 처음으로 고개를 들어 나를 보는 교대 알바생(계산대 앞 그림과 같은 크기·자리)
            CustomerView worker = views.Find(v => v.customerId == "worker");
            string faceSrc = Path.Combine(CustomerBuilder.SourceRoot, "worker", "face_up.png");
            if (worker != null && worker.counterBody != null && worker.counterBody.sprite != null && File.Exists(faceSrc))
            {
                Sprite body = worker.counterBody.sprite;
                var pivot = new Vector2(body.pivot.x / body.rect.width, body.pivot.y / body.rect.height);
                string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(body)).Replace('\\', '/');
                string faceAsset = $"{dir}/face_up.png";
                File.Copy(faceSrc, faceAsset, true);
                story.workerFaceUp = CustomerBuilder.ImportSprite(faceAsset, pivot);
            }

            // 전화 너머 웅얼거리는 목소리(점장 전화·오프닝·엔딩 통화가 같이 쓴다). 3:33 점장 전화만 1배속
            foreach (string vp in new[] { VoicePath, SlowVoicePath })
            {
                AssetDatabase.ImportAsset(vp);
                if (AssetImporter.GetAtPath(vp) is AudioImporter ai)
                {
                    AudioImporterSampleSettings ss = ai.defaultSampleSettings;
                    ss.loadType = AudioClipLoadType.DecompressOnLoad;   // 숨 쉬는 자리를 찾으려고 파형을 읽는다
                    ai.defaultSampleSettings = ss;
                    ai.forceToMono = true;
                    ai.SaveAndReimport();
                }
            }
            var voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoicePath);
            if (voiceClip != null)
            {
                var vgo = new GameObject("Phone Voice");
                vgo.transform.SetParent(checkout.transform, false);
                var pv = vgo.AddComponent<PhoneVoice>();
                pv.clip = voiceClip;
                pv.slowClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SlowVoicePath);
                story.phoneVoice = pv;
                if (checkout.shiftEnd != null) { checkout.shiftEnd.phoneVoice = pv; EditorUtility.SetDirty(checkout.shiftEnd); }
                EditorUtility.SetDirty(pv);
            }
            else Debug.LogWarning("[0333] 전화 목소리가 없음: " + VoicePath);

            if (checkout.shiftEnd != null) { checkout.shiftEnd.fx = camGo.GetComponent<ScreenFx>(); EditorUtility.SetDirty(checkout.shiftEnd); }   // 죽을 때 흔들림·붉은 번쩍임
            checkout.story = story;
            panel.SetActive(false);
            EditorUtility.SetDirty(story);
            EditorUtility.SetDirty(checkout);
            return story;
        }

        // ── 오프닝 1인칭: 비 오는 주차장 → 자동문 → 가게 안 → 계산대 앞 ──
        public const string WalkDir = "Assets/_0333/Art/Story/walk";

        /// <summary>한 장면(한 장): 눈 위치, 보는 방향, 자동문이 벌어진 폭(m), 가게 안인지.</summary>
        public struct WalkStep
        {
            public Vector3 pos, look;
            public float gap;
            public bool inside;
        }

        /// <summary>한 장에 걷는 거리(m)와 재생 빠르기(장/초): 0.1m × 16장 = 초당 1.6m(보통 걸음).</summary>
        public const float WalkStride = 0.1f, WalkFps = 16f;
        const float WalkFrom = 14.2f, WalkTo = 4.35f, StepLength = 0.64f;

        /// <summary>
        /// 걷는 길: 주차장(문에서 6m)에서 가게 문으로 걸어가면 1.4m쯤 앞에서 자동문이 양옆으로 열리고,
        /// 문지방(벽 두께)을 끊김 없이 지나 가게 안에서 계산대 쪽으로 몇 걸음. 걸음마다 머리가 살짝 들썩이고,
        /// 시선은 문지방을 지나며 계산대 쪽으로 천천히 내려간다.
        /// </summary>
        public static List<WalkStep> WalkPath()
        {
            var list = new List<WalkStep>();
            const float eye = 1.6f;
            int n = Mathf.FloorToInt((WalkFrom - WalkTo) / WalkStride) + 1;
            for (int i = 0; i < n; i++)
            {
                float z = WalkFrom - i * WalkStride;
                float phase = i * WalkStride / StepLength * Mathf.PI;   // 0.64m마다 한 걸음
                float drift = Mathf.Lerp(0f, 0.32f, Mathf.InverseLerp(8.6f, WalkFrom, z));   // 주차장에선 살짝 오른쪽에서 문 가운데로
                float x = drift + Mathf.Sin(phase * 0.5f) * 0.012f;
                float gap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((10.6f - z) / 1.0f)) * 1.55f;   // 센서: 1.4m쯤 앞에서 열린다
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8.7f, 7.4f, z));               // 문지방을 지나며 시선이 계산대 쪽으로
                var look = new Vector3(-drift * 0.06f, Mathf.Lerp(-0.07f, -0.12f, k), -1f);
                list.Add(new WalkStep { pos = new Vector3(x, eye + Mathf.Abs(Mathf.Sin(phase)) * 0.03f, z), look = look, gap = gap, inside = z < 8.0f });
            }
            return list;
        }

        static string WalkFrame(int i) => $"{WalkDir}/fp_{i:000}.png";

        /// <summary>
        /// 배치 모드: 1인칭 걸음 그림을 한 장씩 광선 추적으로 그려 WalkDir에 저장한다(한 번만 돌리면 된다, 오래 걸린다).
        /// -frames "0,12,30"이면 그 장만(시험), -previewDir이 있으면 3배로 키운 사본도.
        /// </summary>
        public static void RenderWalkInBatch()
        {
            Directory.CreateDirectory(WalkDir);
            string preview = StoreSceneBuilder.Arg("-previewDir");
            string only = StoreSceneBuilder.Arg("-frames");
            var pick = new HashSet<int>();
            if (!string.IsNullOrEmpty(only)) foreach (string s in only.Split(',')) if (int.TryParse(s.Trim(), out int n)) pick.Add(n);
            List<WalkStep> steps = WalkPath();
            // 전부 다시 그릴 때는 예전 장(간격이 다르던 것)을 지운다
            if (pick.Count == 0)
                foreach (string old in Directory.GetFiles(WalkDir, "fp_*.png"))
                    AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var saved = new List<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                if (pick.Count > 0 && !pick.Contains(i)) continue;
                WalkStep st = steps[i];
                PixelCanvas c = G.RenderEye(st.pos, st.pos + st.look, st.gap);
                string path = WalkFrame(i);
                c.SavePng(path);
                saved.Add(path);
                if (!string.IsNullOrEmpty(preview)) c.SavePng(Path.Combine(preview, $"fp_{i:000}_z{st.pos.z:F2}.png"), 2);
                Debug.Log($"[0333] 1인칭 걸음 {i + 1}/{steps.Count} ({sw.Elapsed.TotalSeconds:F0}초)");
            }
            AssetDatabase.Refresh();
            foreach (string p in saved)
            {
                StoreSceneBuilder.ConfigureSprite(p);
                // 장이 많으니 투명 채널 없이(메모리 3/4)
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = false;
                ti.SaveAndReimport();
            }
            Debug.Log($"[0333] 1인칭 걸음 {saved.Count}장 저장: {WalkDir}");
        }

        static SpriteRenderer Sr(string name, Transform parent, Material mat, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }
    }
}
