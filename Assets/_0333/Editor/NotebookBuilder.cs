using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 수첩(크게 보기 화면 + 계산대 위 수첩 핫스팟)을 씬에 넣는다.
    /// 매장 그림을 다시 그리지 않으므로 빠르다. 메뉴 [0333 > 수첩만 다시 만들기] 또는 전체 씬 생성 때 함께 실행된다.
    /// 내용(Notebook.txt)은 절대 덮어쓰지 않는다.
    /// </summary>
    public static class NotebookBuilder
    {
        const string ArtDir = "Assets/_0333/Art/Notebook";
        const string FontDir = "Assets/_0333/Fonts";
        const string TitleFontPath = FontDir + "/Galmuri11.ttf";
        const string BodyFontPath = FontDir + "/Galmuri9.ttf";
        const string ContentPath = "Assets/_0333/Data/Notebook.txt";

        // 갈무리 글꼴이 가장 또렷한 크기(제작자 권장): 갈무리11 = 12px, 갈무리9 = 10px
        const int TitleSize = 12, BodySize = 10;
        static readonly Color InkBlue = new Color32(0x2C, 0x3A, 0x5C, 0xFF);
        static readonly Color InkBrown = new Color32(0x3A, 0x32, 0x26, 0xFF);
        static readonly Color PencilGray = new Color32(0x9A, 0x92, 0x82, 0xFF);

        [MenuItem("0333/수첩만 다시 만들기 (Rebuild Notebook)")]
        static void Menu()
        {
            var scene = EditorSceneManager.OpenScene(StoreSceneBuilder.ScenePath);
            AddToScene(FindCamera(), StoreSceneBuilder.GetOrCreateMaterial());
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[0333] 수첩 다시 만들기 완료");
        }

        /// <summary>배치 모드용: 씬의 수첩만 다시 만들고, -previewDir가 있으면 펼친 수첩 화면을 PNG로 찍는다.</summary>
        public static void RebuildBatch()
        {
            Menu();
            string dir = StoreSceneBuilder.Arg("-previewDir");
            if (!string.IsNullOrEmpty(dir)) RenderPreview(dir);
        }

        static GameObject FindCamera()
        {
            var look = UnityEngine.Object.FindFirstObjectByType<MouseLookCamera>();
            if (look == null) throw new Exception("씬에 Main Camera(MouseLookCamera)가 없음");
            return look.gameObject;
        }

        public static void AddToScene(GameObject camGo, Material mat)
        {
            // 예전 수첩 지우기
            var old = UnityEngine.Object.FindFirstObjectByType<NotebookSystem>(FindObjectsInactive.Include);
            if (old != null)
            {
                if (old.panel != null) UnityEngine.Object.DestroyImmediate(old.panel);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            Font titleFont = ConfigureFont(TitleFontPath);
            Font bodyFont = ConfigureFont(BodyFontPath);
            TextAsset content = EnsureContent();

            // 그림
            Directory.CreateDirectory(ArtDir);
            var layers = NotebookArt.Generate();
            foreach (var l in layers) l.Canvas.SavePng($"{ArtDir}/{l.Name}.png");
            AssetDatabase.Refresh();
            foreach (var l in layers) StoreSceneBuilder.ConfigureSprite($"{ArtDir}/{l.Name}.png");

            var panel = new GameObject("NotebookPanel");
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);

            var note = new GameObject("Notebook").AddComponent<NotebookSystem>();
            foreach (var l in layers)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{l.Name}.png");
                if (sprite == null) throw new Exception($"스프라이트를 불러오지 못함: {l.Name}");
                RectInt r = l.Canvas.Rect;
                var sr = StoreSceneBuilder.CreateSprite(l.Name, panel.transform, new Vector3(r.x + r.width * 0.5f, r.y + r.height * 0.5f, 0f), sprite, mat, l.Order);
                var area = new Rect(r.x, r.y, r.width, r.height);
                switch (l.Name)
                {
                    case NotebookArt.PrevName: note.prevButton = sr; note.prevArea = area; break;
                    case NotebookArt.NextName: note.nextButton = sr; note.nextArea = area; break;
                    case NotebookArt.PrevHoverName: note.prevHover = sr; sr.enabled = false; break;
                    case NotebookArt.NextHoverName: note.nextHover = sr; sr.enabled = false; break;
                    case NotebookArt.CloseHoverName: note.closeHover = sr; note.closeArea = area; sr.enabled = false; break;
                }
            }

            // 글자: 카메라 화면에 붙는 캔버스. 기준 해상도 640x360이라 1 단위 = 그림 1픽셀
            var canvasGo = new GameObject("NotebookText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 205;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StoreArtGenerator.ViewW, StoreArtGenerator.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            RectInt lp = NotebookArt.LeftPage, rp = NotebookArt.RightPage;
            int textW = lp.width - NotebookArt.MarginX - 10;
            note.lineWidth = textW;
            note.leftTitle = MakeText(canvasGo, "Left Title", titleFont, TitleSize, InkBrown, lp.x + NotebookArt.MarginX, NotebookArt.FirstLineY + 8, textW, 18, TextAnchor.LowerLeft);
            note.rightTitle = MakeText(canvasGo, "Right Title", titleFont, TitleSize, InkBrown, rp.x + NotebookArt.MarginX, NotebookArt.FirstLineY + 8, textW, 18, TextAnchor.LowerLeft);
            note.leftLines = new Text[NotebookArt.LineCount - 1];
            note.rightLines = new Text[NotebookArt.LineCount - 1];
            for (int i = 0; i < NotebookArt.LineCount - 1; i++)
            {
                // i번째 줄의 글은 (i+1)번째 줄 선 위에 앉는다
                int y = NotebookArt.LineY(i + 1) + 2;
                note.leftLines[i] = MakeText(canvasGo, $"Left Line {i + 1}", bodyFont, BodySize, InkBlue, lp.x + NotebookArt.MarginX, y, textW, NotebookArt.LinePitch - 2, TextAnchor.LowerLeft);
                note.rightLines[i] = MakeText(canvasGo, $"Right Line {i + 1}", bodyFont, BodySize, InkBlue, rp.x + NotebookArt.MarginX, y, textW, NotebookArt.LinePitch - 2, TextAnchor.LowerLeft);
            }
            note.leftPageNo = MakeText(canvasGo, "Left Page No", bodyFont, BodySize, PencilGray, lp.x, lp.y + 3, lp.width, 12, TextAnchor.LowerCenter);
            note.rightPageNo = MakeText(canvasGo, "Right Page No", bodyFont, BodySize, PencilGray, rp.x, rp.y + 3, rp.width, 12, TextAnchor.LowerCenter);

            var look = camGo.GetComponent<MouseLookCamera>();
            note.look = look;
            note.viewCamera = camGo.GetComponent<Camera>();
            note.panel = panel;
            note.content = content;
            note.spread = 0;
            panel.SetActive(false);

            // 계산대 위 수첩 핫스팟
            var hotspotGo = new GameObject("Hotspot Notebook");
            hotspotGo.transform.SetParent(note.transform, false);
            var hotspot = hotspotGo.AddComponent<Hotspot>();
            Rect a = StoreArtGenerator.NotebookScreenArea();
            hotspot.area = new Rect(a.x - 3f, a.y - 3f, a.width + 6f, a.height + 6f);
            hotspot.label = "수첩 보기";
            UnityEventTools.AddPersistentListener(hotspot.onInteract, note.Open);

            // 조준했을 때 뜨는 이름표도 갈무리9로
            var interactor = camGo.GetComponent<HotspotInteractor>();
            if (interactor == null) interactor = camGo.AddComponent<HotspotInteractor>();
            interactor.look = look;
            interactor.labelFont = bodyFont;
            interactor.labelSize = BodySize;
        }

        static Text MakeText(GameObject canvas, string name, Font font, int size, Color color, float x, float y, float w, float h, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }

        /// <summary>픽셀 글꼴이 번지지 않게: 안티에일리어싱 없이(Hinted Raster) 필요한 글자만 그때그때 굽는다.</summary>
        static Font ConfigureFont(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (ti == null) throw new Exception($"글꼴이 없음: {path}");
            if (ti.fontRenderingMode != FontRenderingMode.HintedRaster || ti.fontTextureCase != FontTextureCase.Dynamic)
            {
                ti.fontRenderingMode = FontRenderingMode.HintedRaster;
                ti.fontTextureCase = FontTextureCase.Dynamic;
                ti.includeFontData = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        static TextAsset EnsureContent()
        {
            if (!File.Exists(ContentPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ContentPath));
                File.WriteAllText(ContentPath, "# 수첩\n(내용을 여기에 적는다)\n");
                AssetDatabase.ImportAsset(ContentPath);
            }
            return AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath);
        }

        /// <summary>검증용: 실제 카메라로 수첩을 펼친 화면을 렌더링해 PNG로 저장한다(1920x1080).</summary>
        static void RenderPreview(string dir)
        {
            Directory.CreateDirectory(dir);
            var note = UnityEngine.Object.FindFirstObjectByType<NotebookSystem>(FindObjectsInactive.Include);
            var cam = note.viewCamera;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            note.panel.SetActive(true);
            note.Show(0);

            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, "notebook_open.png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            note.panel.SetActive(false);
            Debug.Log("[0333] 수첩 미리보기 저장: " + Path.Combine(dir, "notebook_open.png"));
        }
    }
}
