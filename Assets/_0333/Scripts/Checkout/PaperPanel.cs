using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 종이를 화면 가운데에 크게 펼쳐 보여 주는 창의 공통 부분(영수증, 점검표).
    ///  - 뒤 배경을 어둡게 덮고, 그 위에 코드로 그린 도트 종이와 갈무리 글자를 올린다.
    ///  - 열려 있는 동안 시선이 멈추고 커서가 나타난다.
    ///  - 빨간 X, Esc, 마우스 오른쪽 클릭으로 닫는다.
    /// </summary>
    public abstract class PaperPanel : MonoBehaviour
    {
        [Header("연결")]
        public MouseLookCamera look;
        public Camera viewCamera;
        public GameObject panel;
        [Tooltip("배경을 어둡게 덮고 종이를 그리는 화면 크기 그림")]
        public SpriteRenderer paperRenderer;
        [Tooltip("글자를 올리는 캔버스")]
        public RectTransform textRoot;
        public AudioSource sfx;

        [Header("글꼴(수첩과 같은 갈무리)")]
        public Font titleFont;
        public Font bodyFont;
        public int titleSize = 12, bodySize = 10;

        [Header("글자 색")]
        public Color32 inkColor = new Color32(47, 46, 53, 255);
        public Color32 softInk = new Color32(104, 101, 112, 255);

        public bool IsOpen => panel != null && panel.activeSelf;

        protected const int ScreenW = 640, ScreenH = 360;

        protected struct Cell { public string text; public int x, y, w, h; public TextAnchor align; public bool title; public Color32 color; }

        protected readonly List<Cell> cells = new List<Cell>();
        readonly List<Text> pool = new List<Text>();
        protected PixelTex pt;
        /// <summary>닫기 버튼 자리(화면 중심 기준 px). Layout에서 정한다.</summary>
        protected RectInt closeRect;
        bool hoverClose;
        int openedFrame = -1, closedFrame = -1;
        float closeAt = -1f;

        protected virtual void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>지금 열 수 있는지(예: 영수증은 나온 뒤에만).</summary>
        protected virtual bool CanOpen => true;

        public void Open()
        {
            if (IsOpen || panel == null || !CanOpen) return;
            if (Time.frameCount == closedFrame) return; // X를 누른 클릭으로 닫자마자 다시 열리지 않게
            panel.SetActive(true);
            openedFrame = Time.frameCount;
            closeAt = -1f;
            if (look != null) look.LookLocked = true;
            hoverClose = false;
            OnOpen();
            Render();
        }

        protected virtual void OnOpen() { }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            closedFrame = Time.frameCount;
            closeAt = -1f;
            if (look != null) look.LookLocked = false;
        }

        /// <summary>잠깐 뒤에 저절로 닫는다(펜 자국을 보여 준 뒤 등).</summary>
        protected void CloseAfter(float seconds) => closeAt = Time.time + seconds;

        /// <summary>에디터 미리보기용: 시선·커서는 건드리지 않고 펼친 모습만 그린다.</summary>
        protected void ShowForPreview()
        {
            panel.SetActive(true);
            Render();
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            if (closeAt >= 0f && Time.time >= closeAt) { Close(); return; }
            if (GameInput.EscapeDown || GameInput.RightClickDown) { Close(); return; }

            Vector2 p = new Vector2(99999f, 99999f);
            if (viewCamera != null)
                p = (Vector2)viewCamera.ScreenToWorldPoint(GameInput.MousePosition) - (Vector2)viewCamera.transform.position;
            bool overClose = new Rect(closeRect.x, closeRect.y, closeRect.width, closeRect.height).Contains(p);
            bool click = GameInput.LeftClickDown;
            if (click && overClose) { Close(); return; }
            bool redraw = overClose != hoverClose;
            hoverClose = overClose;
            if (Pointer(p, click)) redraw = true;
            if (redraw && IsOpen) Redraw();
        }

        /// <summary>마우스 위치(화면 중심 기준 px)와 클릭을 처리한다. 그림을 다시 그려야 하면 true.</summary>
        protected virtual bool Pointer(Vector2 p, bool click) => false;

        /// <summary>배치부터 그림·글자까지 전부 다시.</summary>
        protected void Render()
        {
            cells.Clear();
            Layout();
            Redraw();
            DrawText();
            AfterRender();
        }

        /// <summary>글자까지 다 그린 뒤(글자 위에 덮는 그림 등).</summary>
        protected virtual void AfterRender() { }

        /// <summary>그림만 다시(마우스를 올렸을 때 등).</summary>
        protected void Redraw()
        {
            if (pt == null) pt = new PixelTex(ScreenW, ScreenH);
            int ox = ScreenW / 2, oy = ScreenH / 2;   // 화면 중심 → 그림 좌표
            pt.Clear(new Color32(4, 6, 10, 218));       // 뒤를 어둡게
            DrawContent(ox, oy);
            DrawClose(new RectInt(closeRect.x + ox, closeRect.y + oy, closeRect.width, closeRect.height), hoverClose);
            paperRenderer.sprite = pt.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>글자 칸과 버튼 자리를 정한다(cells에 글자를 넣고 closeRect를 정한다).</summary>
        protected abstract void Layout();

        /// <summary>종이 그림을 그린다. (ox, oy)를 더하면 그림 좌표.</summary>
        protected abstract void DrawContent(int ox, int oy);

        /// <summary>글자 한 칸. (x, y)는 칸의 왼쪽 아래(화면 중심 기준 px).</summary>
        protected void Add(string text, int x, int y, int w, int h, TextAnchor align, Color32 color, bool title = false) =>   // 글자는 지금 언어로(표에 없으면 그대로)
            cells.Add(new Cell { text = Loc.T(text), x = x, y = y, w = w, h = h, align = align, color = color, title = title });

        /// <summary>펜으로 쓱 그은 체크(칸을 살짝 넘어간다). (x, y)는 10x10 칸의 왼쪽 아래.</summary>
        protected void DrawPenCheck(int x, int y, Color32 pen)
        {
            void Thick(int ax, int ay, int bx, int by) { pt.Line(ax, ay, bx, by, pen); pt.Line(ax + 1, ay, bx + 1, by, pen); }
            Thick(x + 1, y + 5, x + 4, y + 1);
            Thick(x + 4, y + 1, x + 11, y + 12);
        }

        void DrawClose(RectInt r, bool hover)
        {
            Color32 fill = hover ? PixelTex.Hex("#E5372E") : PixelTex.Hex("#A8433B");
            Color32 edge = hover ? PixelTex.Hex("#F3F1DF") : PixelTex.Hex("#6B2A2A");
            Color32 mark = hover ? PixelTex.Hex("#FFFBE8") : PixelTex.Hex("#F3F1DF");
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++) pt.Put(x, y, fill);
            pt.Outline(r.xMin, r.yMin, r.xMax, r.yMax, edge);
            int cx = r.x + r.width / 2, cy = r.y + r.height / 2;
            for (int i = -4; i <= 3; i++)
            {
                pt.Put(cx + i, cy + i, mark); pt.Put(cx + i + 1, cy + i, mark);
                pt.Put(cx + i, cy - 1 - i, mark); pt.Put(cx + i + 1, cy - 1 - i, mark);
            }
        }

        void DrawText()
        {
            for (int i = 0; i < cells.Count; i++)
            {
                Cell c = cells[i];
                Text t = GetText(i);
                t.gameObject.SetActive(true);
                t.font = c.title ? titleFont : bodyFont;
                t.fontSize = c.title ? titleSize : bodySize;
                t.color = c.color;
                t.alignment = c.align;
                t.text = c.text;
                var rt = (RectTransform)t.transform;
                rt.anchoredPosition = new Vector2(c.x, c.y);
                rt.sizeDelta = new Vector2(c.w, c.h);
            }
            for (int i = cells.Count; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        Text GetText(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject($"Text {pool.Count}", typeof(RectTransform));
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(textRoot, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = Vector2.zero;
                var t = go.AddComponent<Text>();
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = false;
                t.raycastTarget = false;
                pool.Add(t);
            }
            if (pool[i] == null) { pool.RemoveAt(i); return GetText(i); }
            return pool[i];
        }
    }
}
