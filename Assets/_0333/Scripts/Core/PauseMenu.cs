using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 일시정지(사용자 결정: 화면에는 아무것도 없고 Esc로만 연다). 열면 게임 시간과 소리가 멈추고
    /// 계속하기 / 설정 / 메인 화면 / 종료 네 개가 뜬다(메인 화면과 같은 글꼴·색·조작).
    ///  - 열 수 있을 때: 근무 중 시선이 자유로울 때만(점검표·영수증·CCTV·수첩이 열려 있으면 Esc는 그 패널을 닫는다,
    ///    대사창·메인 화면·오프닝·엔딩·죽음 장면 중에는 열리지 않는다)
    ///  - 설정: 소리·감도·화면 모드·창 크기·밝기·깜빡임 줄이기·혈흔 없애기·화면 흔들림(메인 화면 설정과 같은 값, GameSettings)
    ///  - 메인 화면·종료: "오늘 밤 진행은 저장되지 않아요" 한 번 묻고, 메인 화면은 가게를 처음부터 다시 불러온다
    ///  - Esc: 닫기(설정·확인에선 뒤로)
    /// 다른 것보다 먼저 Esc를 읽어야 해서(패널이 같은 프레임에 닫히기 전) 실행 순서를 앞에 둔다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PauseMenu : MonoBehaviour
    {
        public MouseLookCamera look;
        public Camera viewCamera;
        public GameObject panel;
        public SpriteRenderer dim, box;
        public Text[] items = new Text[9];
        public Text[] values = new Text[GameSettings.Rows];
        public Text note;
        public Color32 textColor = new Color32(222, 230, 234, 255), hoverColor = new Color32(255, 168, 92, 255);

        /// <summary>일시정지 중(게임 시간·소리가 멈춰 있다).</summary>
        public static bool Paused { get; private set; }
        /// <summary>일시정지 메뉴가 있다(Esc로 커서만 풀던 예전 동작 대신 메뉴를 연다).</summary>
        public static bool Available { get; private set; }

        enum Page { Main, Settings, ConfirmMenu, ConfirmQuit }
        static readonly string[] MainLabels = { "계속하기", "설정", "메인 화면", "종료" };
        static readonly string[] SettingLabels = GameSettings.Labels;
        static readonly string[] ConfirmLabels = { "예", "아니오" };
        const int BoxW = 280, Row = 22;   // 영어 설정 이름이 길어서 조금 넓게

        Page page;
        int hover = -1, selected = -1, openedFrame = -1;
        readonly RectInt[] rects = new RectInt[9];
        float savedScale = 1f;
        bool wasLocked;
        AudioSource click;
        PixelTex dimTex, boxTex;

        void Awake()
        {
            Available = true;
            Paused = false;
            if (panel != null) panel.SetActive(false);
            click = gameObject.AddComponent<AudioSource>();
            click.playOnAwake = false;
            click.ignoreListenerPause = true;   // 멈춘 동안에도 메뉴 소리는 난다
            click.spatialBlend = 0f;
        }

        void OnDestroy()
        {
            if (Paused) Resume();
            Available = false;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (!Paused)
            {
                // 근무 중 시선이 자유로울 때만(패널이 열려 있으면 그 Esc는 패널이 닫는다)
                if (GameInput.EscapeDown && look != null && !look.LookLocked && !look.CursorChangedThisFrame) Open();
                return;
            }
            if (Time.frameCount == openedFrame) return;
            Pointer();
        }

        // ── 열고 닫기 ──
        void Open()
        {
            Paused = true;
            openedFrame = Time.frameCount;
            savedScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            wasLocked = look != null && look.LookLocked;
            if (look != null) look.LookLocked = true;   // 커서가 보이고 시선은 멈춘다
            page = Page.Main;
            hover = -1;
            selected = 0;
            if (panel != null) panel.SetActive(true);
            BuildSprites();
            Layout();
            Tick(0.5f);
        }

        void Resume()
        {
            Paused = false;
            Time.timeScale = savedScale <= 0f ? 1f : savedScale;
            AudioListener.pause = false;
            if (panel != null) panel.SetActive(false);
            if (look != null) look.LookLocked = wasLocked;
        }

        void Tick(float v)
        {
            if (click != null) click.PlayOneShot(SoundFx.Tick, v);
        }

        // ── 그림 ──
        void BuildSprites()
        {
            if (dim != null && dimTex == null)
            {
                dimTex = new PixelTex(1, 1);
                dimTex.Put(0, 0, new Color32(0, 0, 0, 190));
                dim.sprite = dimTex.ToSprite(new Vector2(0.5f, 0.5f));
                dim.transform.localScale = new Vector3(644f, 364f, 1f);
            }
        }

        /// <summary>글자 줄 수에 맞춘 상자(어두운 바탕, 크림색 두 줄 테두리).</summary>
        void DrawBox(int rows, bool withNote)
        {
            if (box == null) return;
            int h = rows * Row + 22 + (withNote ? 30 : 0);
            if (boxTex == null || boxTex.H != h) boxTex = new PixelTex(BoxW, h);
            boxTex.Clear();
            boxTex.Fill(0, 0, BoxW, h, new Color32(10, 12, 16, 250));
            boxTex.Outline(0, 0, BoxW, h, new Color32(222, 230, 234, 255));
            boxTex.Outline(2, 2, BoxW - 2, h - 2, new Color32(60, 66, 72, 255));
            box.sprite = boxTex.ToSprite(new Vector2(0.5f, 0.5f));
        }

        int Rows => page == Page.Main ? 4 : page == Page.Settings ? SettingLabels.Length : 2;
        bool Confirming => page == Page.ConfirmMenu || page == Page.ConfirmQuit;

        void Layout()
        {
            string[] labels = page == Page.Main ? MainLabels : page == Page.Settings ? SettingLabels : ConfirmLabels;
            int rows = Rows;
            DrawBox(rows, Confirming);
            int boxH = rows * Row + 22 + (Confirming ? 30 : 0);
            int top = boxH / 2 - 11 - (Confirming ? 30 : 0);
            int left = -BoxW / 2 + 22;
            // 값 칸은 가장 긴 설정 이름 뒤에(언어마다 길이가 달라서)
            int valueOff = page == Page.Settings && items.Length > 0 && items[0] != null ? Mathf.Max(112, Mathf.CeilToInt(MainMenu.LongestLabel(items[0], SettingLabels)) + 10) : 112;
            if (note != null)
            {
                note.enabled = Confirming;
                note.text = page == Page.ConfirmMenu ? Loc.T("메인 화면으로 갈까요?\n오늘 밤 진행은 저장되지 않아요.")
                          : page == Page.ConfirmQuit ? Loc.T("게임을 끌까요?\n오늘 밤 진행은 저장되지 않아요.") : "";
                note.rectTransform.anchoredPosition = new Vector2(left, boxH / 2 - 34);
            }
            for (int i = 0; i < items.Length; i++)
            {
                Text t = items[i];
                if (t == null) continue;
                bool show = i < rows;
                t.enabled = show;
                if (!show) { rects[i] = new RectInt(0, 0, 0, 0); continue; }
                int y = top - (i + 1) * Row + 4;
                bool on = i == hover || i == selected;
                t.text = (on ? "▶ " : "   ") + Loc.T(labels[i]);
                t.color = on ? hoverColor : textColor;
                t.rectTransform.anchoredPosition = new Vector2(left, y);
                rects[i] = new RectInt(left - 4, y - 2, BoxW - 36, 17);
                if (page == Page.Settings && i < values.Length && values[i] != null)
                {
                    values[i].enabled = true;
                    values[i].text = GameSettings.ValueText(i);
                    values[i].color = GameSettings.Inactive(i) && !on ? new Color32(120, 128, 134, 255) : t.color;   // 전체 화면일 때 창 크기는 회색
                    values[i].rectTransform.anchoredPosition = new Vector2(left + valueOff, y);
                }
            }
            if (page != Page.Settings) foreach (var v in values) if (v != null) v.enabled = false;
        }

        // ── 조작(마우스·키보드) ──
        void Pointer()
        {
            int over = -1;
            Vector2 p = new Vector2(99999f, 99999f);
            if (viewCamera != null) p = (Vector2)viewCamera.ScreenToWorldPoint(GameInput.MousePosition) - (Vector2)viewCamera.transform.position;
            for (int i = 0; i < rects.Length; i++)
                if (rects[i].width > 0 && new Rect(rects[i].x, rects[i].y, rects[i].width, rects[i].height).Contains(p)) over = i;
            if (over != hover)
            {
                hover = over;
                if (over >= 0) { selected = over; Tick(0.4f); }
                Layout();
            }

            if (GameInput.EscapeDown || GameInput.RightClickDown)
            {
                if (page == Page.Main) { Tick(0.6f); Resume(); }
                else { page = Page.Main; hover = -1; selected = 0; Tick(0.6f); Layout(); }
                return;
            }
            if (GameInput.UpDown || GameInput.DownDown)
            {
                int dir = GameInput.UpDown ? -1 : 1;
                selected = ((selected < 0 ? 0 : selected) + dir + Rows) % Rows;
                hover = -1;
                Tick(0.4f);
                Layout();
            }
            if (page == Page.Settings && (GameInput.PrevDown || GameInput.NextDown) && selected >= 0 && selected < GameSettings.Rows)
                Adjust(selected, GameInput.NextDown ? 1 : -1);

            bool clicked = GameInput.LeftClickDown && over >= 0;
            if (clicked || (GameInput.SubmitDown && selected >= 0))
            {
                int i = clicked ? over : selected;
                if (page == Page.Settings && i < GameSettings.Rows)
                {
                    // ◀ 쪽을 누르면 내리고 ▶ 쪽을 누르면 올린다(키보드는 올리기)
                    int dir = 1;
                    if (clicked && values[i] != null)
                    {
                        float mid = values[i].rectTransform.anchoredPosition.x + values[i].preferredWidth * 0.5f;
                        dir = p.x < mid ? -1 : 1;
                    }
                    Adjust(i, dir);
                    return;
                }
                Choose(i);
            }
        }

        void Choose(int i)
        {
            Tick(0.9f);
            switch (page)
            {
                case Page.Main:
                    if (i == 0) Resume();
                    else if (i == 1) Go(Page.Settings);
                    else if (i == 2) Go(Page.ConfirmMenu);
                    else Go(Page.ConfirmQuit);
                    break;
                case Page.Settings:
                    Go(Page.Main);   // 뒤로
                    break;
                case Page.ConfirmMenu:
                    if (i == 0) ToMainMenu(); else Go(Page.Main);
                    break;
                case Page.ConfirmQuit:
                    if (i == 0) Quit(); else Go(Page.Main);
                    break;
            }
        }

        void Go(Page p)
        {
            page = p;
            hover = -1;
            selected = p == Page.ConfirmMenu || p == Page.ConfirmQuit ? 1 : 0;   // 확인은 "아니오"에서 시작
            Layout();
        }

        void Adjust(int row, int dir)
        {
            GameSettings.Adjust(row, dir, look);   // 메인 화면 설정과 같은 값(저장·적용)
            Tick(0.6f);
            Layout();
        }

        /// <summary>메인 화면으로: 멈춘 것을 풀고 가게를 처음부터 다시 불러온다(오늘 밤 진행은 버린다, 세이브는 그날 시작 그대로).</summary>
        void ToMainMenu()
        {
            Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            HandFx.Reset();
            ClockHud.Alarm = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Quit()
        {
#if UNITY_EDITOR
            Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 미리보기(에디터) ──
        /// <summary>미리보기: 0 처음(계속하기에 마우스), 1 설정, 2 메인 화면 확인.</summary>
        public void PreviewShow(int stage, int select = -1)
        {
            if (panel != null) panel.SetActive(true);
            BuildSprites();
            page = stage == 1 ? Page.Settings : stage == 2 ? Page.ConfirmMenu : Page.Main;
            hover = -1;
            selected = select >= 0 ? select : stage == 2 ? 1 : 0;
            Layout();
        }

        public void PreviewHide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
