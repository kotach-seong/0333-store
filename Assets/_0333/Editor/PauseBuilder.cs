using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 일시정지 메뉴(Esc로만 연다, 평소 화면엔 아무것도 없다). 카메라 자식 "PausePanel": 어두운 막(494)·상자(495)·글자(496).
    /// 메인 화면(400번대)·오프닝·엔딩(450~475)보다 위. CheckoutBuilder.AddToScene 끝에서 부른다.
    /// </summary>
    public static class PauseBuilder
    {
        public const string PanelName = "PausePanel";
        const string TitleFontPath = "Assets/_0333/Fonts/Galmuri11.ttf";
        const string BodyFontPath = "Assets/_0333/Fonts/Galmuri9.ttf";
        const string TintShaderPath = "Assets/_0333/Shaders/ScreenTint.shader";

        public static PauseMenu Build(CheckoutSystem checkout, GameObject camGo, Material mat)
        {
            var panel = new GameObject(PanelName);
            panel.transform.SetParent(camGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 5f);

            var go = new GameObject("Pause Menu");
            go.transform.SetParent(checkout.transform, false);
            var pause = go.AddComponent<PauseMenu>();
            pause.panel = panel;
            pause.look = camGo.GetComponent<MouseLookCamera>();
            pause.viewCamera = camGo.GetComponent<Camera>();
            pause.dim = Sr("Dim", panel.transform, mat, 494);
            pause.box = Sr("Box", panel.transform, mat, 495);

            var canvasGo = new GameObject("PauseText", typeof(RectTransform));
            canvasGo.transform.SetParent(panel.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camGo.GetComponent<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 496;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(G.ViewW, G.ViewH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            Font title = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath), body = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            Text MakeText(string name, Font f, int size, Color32 color)
            {
                var tgo = new GameObject(name, typeof(RectTransform));
                tgo.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)tgo.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = Vector2.zero;
                rt.sizeDelta = new Vector2(200f, 16f);
                var t = tgo.AddComponent<Text>();
                t.font = f; t.fontSize = size; t.color = color; t.alignment = TextAnchor.LowerLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = false; t.raycastTarget = false;
                tgo.AddComponent<Outline>().effectColor = new Color32(6, 8, 10, 220);
                t.enabled = false;
                return t;
            }
            for (int i = 0; i < pause.items.Length; i++) pause.items[i] = MakeText($"Item {i + 1}", title, 12, pause.textColor);
            for (int i = 0; i < pause.values.Length; i++) pause.values[i] = MakeText($"Value {i + 1}", title, 12, pause.textColor);
            pause.note = MakeText("Note", body, 10, new Color32(190, 198, 204, 255));
            pause.note.lineSpacing = 1.2f;

            panel.SetActive(false);

            // 설정의 밝기: 카메라 앞에 화면을 덮는 막(모든 것 위, 500~501). 막은 게임을 켤 때 만들어진다
            var brightness = camGo.GetComponent<ScreenBrightness>();
            if (brightness == null) brightness = camGo.AddComponent<ScreenBrightness>();
            brightness.shader = AssetDatabase.LoadAssetAtPath<Shader>(TintShaderPath);
            if (brightness.shader == null) Debug.LogError("[0333] 밝기 셰이더를 찾지 못함: " + TintShaderPath);
            EditorUtility.SetDirty(brightness);
            EditorUtility.SetDirty(pause);
            return pause;
        }

        static SpriteRenderer Sr(string name, Transform parent, Material mat, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
