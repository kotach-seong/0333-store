using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 내가 할 대답 2개.
    ///  - 평소(<see cref="ShowBeside"/>): 손님 말풍선 오른쪽 위에 붙어 뜬다. 화면은 계속 움직이고 계산도 계속할 수 있다.
    ///    조준점을 버튼에 맞추고 클릭하거나 1·2 키로 고른다. 안 고르면 잠시 뒤 그냥 사라진다.
    ///  - 스토리(<see cref="ShowForced"/>): 중요한 장면·무서운 장면. 화면 아래 가운데에 뜨고, 고를 때까지 시선이 멈춘다(커서로 고른다).
    /// </summary>
    [DefaultExecutionOrder(-60)]   // 물건 끌기(-50)보다 먼저: 버튼을 겨눈 클릭이 물건을 집지 않게
    public class ChoiceView : MonoBehaviour
    {
        public SpriteRenderer[] boxes = new SpriteRenderer[2];
        public Text[] labels = new Text[2];
        public MouseLookCamera look;
        public Camera viewCamera;
        public HotspotInteractor interactor;
        public AudioSource sfx;
        [Tooltip("스토리 선택지: 첫 번째 버튼의 아래쪽 높이(화면 가운데 기준 px)")]
        public int forcedTopY = -92;
        public int buttonH = 17, gap = 3, padX = 6;
        public Color32 fill = new Color32(22, 20, 28, 236), fillHover = new Color32(64, 52, 34, 245);
        public Color32 edge = new Color32(206, 196, 168, 255), edgeHover = new Color32(255, 212, 120, 255);
        public Color32 textColor = new Color32(232, 224, 200, 255), textHover = new Color32(255, 222, 148, 255);

        public bool Showing { get; private set; }
        public bool Forced { get; private set; }
        /// <summary>고른 대답(0 또는 1). 아직이면 -1.</summary>
        public int Answer { get; private set; } = -1;
        /// <summary>이번 프레임에 조준점이 버튼 위에 있다(물건 끌기·핫스팟은 쉰다).</summary>
        public static bool Aimed { get; private set; }

        readonly RectInt[] rects = new RectInt[2];
        readonly PixelTex[] pts = new PixelTex[2];
        int hover = -1, shownFrame = -1, buttonW, textW;
        bool locked;
        SpeechBubble beside;
        const int ScreenW = 640, ScreenH = 360;

        void Awake() => Hide();

        /// <summary>말풍선 오른쪽 위에 붙여 띄운다(화면은 그대로 움직인다).</summary>
        public void ShowBeside(SpeechBubble bubble, string first, string second)
        {
            Begin(first, second, false);
            beside = bubble;
            LayoutBeside();
        }

        /// <summary>정해진 자리(화면 가운데 기준, 왼쪽 x·위 top)에 띄운다. 시선은 멈추지 않는다.</summary>
        public void ShowAt(int x, int top, string first, string second)
        {
            Begin(first, second, false);
            beside = null;
            x = Mathf.Clamp(x, -ScreenW / 2 + 4, ScreenW / 2 - 4 - buttonW);
            for (int i = 0; i < 2; i++) Place(i, x, top - buttonH - i * (buttonH + gap));
        }

        /// <summary>
        /// 가게 안 한 곳(월드 픽셀, 버튼 묶음의 오른쪽 위)에 붙여 띄운다(3:33 내가 하는 말).
        /// 화면이 움직이면 같이 움직여서, 그쪽을 보면 조준점으로 고를 수 있다(1·2 키도 된다).
        /// </summary>
        public void ShowAtWorld(Vector2 topRight, string first, string second)
        {
            Begin(first, second, false);
            beside = null;
            worldAnchor = topRight;
            hasWorldAnchor = true;
            LayoutWorld();
        }

        Vector2 worldAnchor;
        bool hasWorldAnchor;

        void LayoutWorld()
        {
            if (!hasWorldAnchor || viewCamera == null) return;
            Vector2 c = viewCamera.transform.position;
            int right = Mathf.RoundToInt(worldAnchor.x - c.x), top = Mathf.RoundToInt(worldAnchor.y - c.y);
            for (int i = 0; i < 2; i++) Place(i, right - buttonW, top - buttonH - i * (buttonH + gap));
        }

        /// <summary>스토리용: 화면 아래 가운데, 고를 때까지 시선이 멈춘다.</summary>
        public void ShowForced(string first, string second)
        {
            Begin(first, second, true);
            beside = null;
            for (int i = 0; i < 2; i++) Place(i, -buttonW / 2, forcedTopY - i * (buttonH + gap));
            if (look != null && !look.LookLocked) { look.LookLocked = true; locked = true; }
        }

        void Begin(string first, string second, bool forced)
        {
            Answer = -1;
            hover = -1;
            Forced = forced;
            hasWorldAnchor = false;
            string[] texts = { "1  " + first, "2  " + second };
            textW = 0;
            for (int i = 0; i < 2; i++)
            {
                labels[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                labels[i].verticalOverflow = VerticalWrapMode.Overflow;
                labels[i].text = Loc.T(texts[i]);
                textW = Mathf.Max(textW, Mathf.CeilToInt(labels[i].preferredWidth));
            }
            buttonW = textW + padX * 2;
            for (int i = 0; i < 2; i++)
            {
                labels[i].rectTransform.sizeDelta = new Vector2(textW + 2, 14f);
                labels[i].enabled = true;
                boxes[i].enabled = true;
                pts[i] = null;
            }
            Showing = true;
            shownFrame = Application.isPlaying ? Time.frameCount : -1;
        }

        public void Hide()
        {
            Showing = false;
            Aimed = false;
            beside = null;
            hasWorldAnchor = false;
            for (int i = 0; i < 2; i++)
            {
                if (boxes != null && i < boxes.Length && boxes[i] != null) boxes[i].enabled = false;
                if (labels != null && i < labels.Length && labels[i] != null) labels[i].enabled = false;
            }
            if (locked && look != null) look.LookLocked = false;
            locked = false;
        }

        /// <summary>말풍선 오른쪽 위(오른쪽에 자리가 없으면 왼쪽 위). 버튼 두 개를 위아래로.</summary>
        void LayoutBeside()
        {
            if (beside == null) return;
            RectInt b = beside.ScreenRect;
            int stackH = buttonH * 2 + gap;
            int x = b.xMax + 3;
            if (x + buttonW > ScreenW / 2 - 4) x = b.xMin - 3 - buttonW;
            int top = b.yMax + 8;                                       // 말풍선 윗변보다 조금 위에서 시작
            top = Mathf.Clamp(top, -ScreenH / 2 + 4 + stackH, ScreenH / 2 - 34);
            x = Mathf.Clamp(x, -ScreenW / 2 + 4, ScreenW / 2 - 4 - buttonW);
            for (int i = 0; i < 2; i++) Place(i, x, top - buttonH - i * (buttonH + gap));
        }

        void Place(int i, int x, int y)
        {
            bool moved = rects[i].x != x || rects[i].y != y || rects[i].width != buttonW;
            rects[i] = new RectInt(x, y, buttonW, buttonH);
            labels[i].rectTransform.anchoredPosition = new Vector2(x + padX, y + 1);
            boxes[i].transform.localPosition = new Vector3(x, y, boxes[i].transform.localPosition.z);
            if (moved || pts[i] == null) Draw(i);
        }

        void Update()
        {
            Aimed = false;
            if (!Showing || !Application.isPlaying || Time.frameCount == shownFrame || PauseMenu.Paused) return;
            int pick = GameInput.Choice1Down ? 0 : GameInput.Choice2Down ? 1 : -1;

            // 겨누는 곳: 시선이 자유로우면 화면 한가운데(조준점), 커서가 보이면 커서
            int over = -1;
            bool cursor = look != null && look.LookLocked;
            Vector2 p = Vector2.zero;
            if (cursor && viewCamera != null)
                p = (Vector2)viewCamera.ScreenToWorldPoint(GameInput.MousePosition) - (Vector2)viewCamera.transform.position;
            if (cursor || Cursor.lockState == CursorLockMode.Locked)
                for (int i = 0; i < 2; i++)
                    if (new Rect(rects[i].x, rects[i].y, rects[i].width, rects[i].height).Contains(p)) over = i;
            if (over != hover)
            {
                int old = hover;
                hover = over;
                if (old >= 0) Draw(old);
                if (hover >= 0) Draw(hover);
            }
            if (over >= 0)
            {
                Aimed = true;
                if (!cursor && interactor != null && viewCamera != null)
                {
                    Vector2 c = viewCamera.transform.position;
                    interactor.Highlight(new Rect(c.x + rects[over].x, c.y + rects[over].y, rects[over].width, rects[over].height), "대답하기");
                }
                if (pick < 0 && GameInput.LeftClickDown) pick = over;
            }
            if (pick >= 0)
            {
                Answer = pick;
                SoundFx.Play(sfx, SoundFx.Tick, 0.8f);
            }
        }

        void LateUpdate()
        {
            if (!Showing || Forced || !Application.isPlaying) return;
            if (hasWorldAnchor) LayoutWorld();   // 가게 안 그 자리에 붙어 있다
            else LayoutBeside();                 // 말풍선을 따라 움직인다
        }

        void Draw(int i)
        {
            RectInt r = rects[i];
            if (r.width <= 0) return;
            if (pts[i] == null || pts[i].W != r.width || pts[i].H != r.height) pts[i] = new PixelTex(r.width, r.height);
            PixelTex pt = pts[i];
            bool on = i == hover;
            pt.Clear();
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                {
                    bool corner = (x == 0 || x == r.width - 1) && (y == 0 || y == r.height - 1);
                    if (corner) continue;
                    bool border = x == 0 || x == r.width - 1 || y == 0 || y == r.height - 1;
                    pt.Put(x, y, border ? (on ? edgeHover : edge) : (on ? fillHover : fill));
                }
            boxes[i].sprite = pt.ToSprite(Vector2.zero);
            labels[i].color = on ? textHover : textColor;
        }
    }
}
