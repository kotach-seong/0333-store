using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 화면 가운데 조준점으로 핫스팟을 겨누고 클릭하는 기능. 카메라에 붙인다.
    ///  - 조준점이 핫스팟 위에 있으면 네 귀퉁이 괄호와 이름을 보여 준다.
    ///  - 왼쪽 클릭하면 그 핫스팟의 동작을 실행한다.
    ///  - 패널이 열려 시선이 고정된 동안, 또는 커서가 풀려 있는 동안에는 동작하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HotspotInteractor : MonoBehaviour
    {
        public MouseLookCamera look;
        [Tooltip("조준했을 때 뜨는 이름의 글꼴(갈무리9)")]
        public Font labelFont;
        [Tooltip("이름 글자 크기(화면 픽셀 기준, 갈무리9는 10이 가장 또렷하다)")]
        public int labelSize = 10;

        Camera cam;
        Hotspot[] hotspots;
        Hotspot current;
        GUIStyle labelStyle;

        // 핫스팟이 아닌 것(물품 칸의 물건 등)을 조준했을 때 다른 스크립트가 띄우는 테두리
        Rect externalArea;
        string externalLabel;
        int externalFrame = -1;
        bool HasExternal => externalFrame == Time.frameCount;

        /// <summary>
        /// 이번 프레임에 이 영역을 조준 중인 것으로 보여 준다(핫스팟보다 먼저). 그 사이 핫스팟 클릭은 막힌다.
        /// 영역 크기가 0이면 아무것도 그리지 않고 핫스팟만 막는다(물건을 끄는 중일 때).
        /// 이 프레임의 Update보다 먼저 불러야 한다.
        /// </summary>
        public void Highlight(Rect area, string label)
        {
            externalArea = area;
            externalLabel = label;
            externalFrame = Time.frameCount;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (look == null) look = GetComponent<MouseLookCamera>();
            hotspots = FindObjectsByType<Hotspot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        void Update()
        {
            current = null;
            if (look == null || look.LookLocked || Cursor.lockState != CursorLockMode.Locked) return;
            // 패널을 막 닫은 프레임에는 그 클릭이 아직 "눌림"으로 남아 있다. 여기서 다시 읽으면 닫자마자 또 열린다.
            if (look.CursorChangedThisFrame) return;
            if (HasExternal) return;

            Vector2 aim = transform.position; // 조준점 = 화면 한가운데 = 카메라 위치
            foreach (Hotspot h in hotspots)
                if (h != null && h.Contains(aim)) { current = h; break; }

            if (current != null && GameInput.LeftClickDown) current.onInteract.Invoke();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (current != null) DrawAim(current.area, current.label);
            else if (HasExternal && externalArea.width > 0f) DrawAim(externalArea, externalLabel);
        }

        void DrawAim(Rect area, string label)
        {
            float px = Mathf.Max(1f, Mathf.Round(cam.pixelRect.height / (2f * cam.orthographicSize))); // 도트 하나의 화면 크기
            Vector3 a = cam.WorldToScreenPoint(new Vector3(area.xMin, area.yMin, 0f));
            Vector3 b = cam.WorldToScreenPoint(new Vector3(area.xMax, area.yMax, 0f));
            var r = Rect.MinMaxRect(a.x, Screen.height - b.y, b.x, Screen.height - a.y);

            // 네 귀퉁이 괄호
            float len = 5f * px;
            GUI.color = new Color(0.95f, 0.93f, 0.75f, 0.95f);
            Corner(r.xMin, r.yMin, len, px, 1, 1);
            Corner(r.xMax, r.yMin, len, px, -1, 1);
            Corner(r.xMin, r.yMax, len, px, 1, -1);
            Corner(r.xMax, r.yMax, len, px, -1, -1);

            // 조준점 아래 이름
            label = Loc.T(label);   // "영수증 보기" 같은 이름은 지금 언어로
            if (!string.IsNullOrEmpty(label))
            {
                if (labelStyle == null)
                {
                    labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
                    if (labelFont != null) labelStyle.font = labelFont;   // (언어를 바꾸면 아래에서 다시 맞춘다)
                    else labelStyle.fontStyle = FontStyle.Bold;
                }
                if (labelFont != null && labelStyle.font != labelFont) labelStyle.font = labelFont;
                labelStyle.fontSize = Mathf.RoundToInt(labelSize * px);
                Rect view = cam.pixelRect;
                var box = new Rect(view.x, Screen.height - (view.y + view.height * 0.5f) + 6f * px, view.width, 14f * px);
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(box.x + px, box.y + px, box.width, box.height), label, labelStyle);
                GUI.color = new Color(0.95f, 0.93f, 0.75f, 1f);
                GUI.Label(box, label, labelStyle);
            }
            GUI.color = Color.white;
        }

        /// <summary>(x,y)에서 시작해 가로·세로로 뻗는 ㄱ자 괄호.</summary>
        static void Corner(float x, float y, float len, float t, int dx, int dy)
        {
            GUI.DrawTexture(new Rect(dx > 0 ? x : x - len, dy > 0 ? y : y - t, len, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(dx > 0 ? x : x - t, dy > 0 ? y : y - len, t, len), Texture2D.whiteTexture);
        }
    }
}
