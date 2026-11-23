using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 메인 화면. 게임을 켜면 가장 먼저 뜬다.
    ///  - 배경: CCTV 3번(주차장 가로등 기둥에서 비 오는 매장 정면) 녹화 영상. 매장 안 계산대 뒤에 내가 작게 서 있다.
    ///    노이즈·빗줄기·주사선이 계속 움직이고, 왼쪽 위 "● REC"가 깜빡이며, 오른쪽 아래에 지금 날짜와 시각이 흐른다.
    ///  - 제목: "03:33" 주황 LED 숫자(콜론이 깜빡). 실제 시계가 3시 33분이면 빨갛게.
    ///  - 메뉴: 새로 시작 / 이어하기(저장이 있을 때만, DAY n) / 설정(소리 크기·마우스 감도) / 종료.
    ///  - 소리: 빗소리 + 형광등 웅웅.
    ///  - 가만히 두면 가끔: 메뉴 글자가 한 순간 깨지거나, CCTV 창밖에 검은 형체가 잠깐 비친다.
    /// </summary>
    public partial class MainMenu : MonoBehaviour
    {
        public enum Pick { None, NewGame, Continue }

        [Header("그림(카메라의 자식, 시계·패널보다 위)")]
        public SpriteRenderer background;
        public SpriteRenderer player;
        public SpriteRenderer ghost;
        public SpriteRenderer fx;
        public SpriteRenderer scan;
        public SpriteRenderer title;
        public SpriteRenderer fade;
        [Tooltip("창밖 형체의 키(px, CCTV 3번 화면 안에서)")]
        public int ghostHeight = 40;

        [Header("글자")]
        [Tooltip("메뉴 줄(메인 5줄, 설정 9줄)")]
        public Text[] items = new Text[9];
        [Tooltip("설정 값(◀ ▶) 8줄")]
        public Text[] values = new Text[GameSettings.Rows];
        public Text rec, camLabel, stamp, hint;

        [Header("연결")]
        public MouseLookCamera look;
        public Camera viewCamera;
        public AudioSource sfx;

        public Color32 ledOrange = new Color32(255, 122, 51, 255), ledRed = new Color32(240, 36, 36, 255);
        public Color32 textColor = new Color32(222, 230, 234, 255), hoverColor = new Color32(255, 168, 92, 255);

        [Header("진행에 따라 변하는 것(세이브를 읽는다)")]
        [Tooltip("배경과 그 위 그림들. 시작할 때 문 쪽으로 확대된다")]
        public Transform sceneRoot;
        [Tooltip("확대 중심(문, 패널 좌표)")]
        public Vector2 doorPoint;
        [Tooltip("흔적 1 이상: 창밖에 선 검은 형체")]
        public SpriteRenderer traceFigure;
        [Tooltip("흔적 2 이상: 가게 안쪽 유리에 묻은 피")]
        public SpriteRenderer bloodGlass;
        [Tooltip("실종 전단(경찰이 다녀간 수만큼)")]
        public SpriteRenderer[] flyers = new SpriteRenderer[0];
        [Tooltip("죽은 적이 있으면: 계산대 뒤의 나는 없고, 가끔 그 괴물이 서 있다")]
        public SpriteRenderer monster;

        [Header("가만히 두면 모여드는 것들(창문을 보고 붙어 있다)")]
        public SpriteRenderer[] watchers = new SpriteRenderer[0];
        [Tooltip("watchers와 같은 순서: 고개를 홱 돌려 카메라를 보는 그림")]
        public Sprite[] watcherTurn = new Sprite[0];
        [Tooltip("watchers와 같은 순서: 그 몸이 드리운 그림자·젖은 바닥 반사·유리 김서림(함께 켜고 끈다)")]
        public SpriteRenderer[] watcherShade = new SpriteRenderer[0];
        [Tooltip("문틈으로 반쯤 들어가는 것(나타나면 배경을 문이 벌어진 그림으로 바꾼다). 없으면 -1")]
        public int doorWatcher = -1;
        public Sprite backgroundDoorOpen;
        [Tooltip("다 모이고 고개를 돌린 뒤 카메라 바로 앞에 한 번 들이민다")]
        public SpriteRenderer closeUp;
        [Tooltip("첫 등장, 하나씩 늘어나는 간격, 다 모인 뒤 고개 돌리기까지, 고개 돌린 뒤 들이밀기까지(초)")]
        public float idleFirst = 10f, idleStep = 6f, idleTurn = 6f, idleCloseUp = 2.4f;

        [Header("멀리서 나는 소리")]
        public AudioClip chime;

        public bool Showing { get; private set; }
        public Pick Choice { get; private set; } = Pick.None;
        /// <summary>시작을 고른 뒤 화면이 검게 덮여 있는 중(CheckoutSystem이 전화벨을 울린 뒤 Reveal로 걷는다).</summary>
        public bool Covering { get; private set; }

        int traces, policeSeen, deaths;
        float idle, closeUpUntil, monsterUntil, nextSound;
        int watchersShown;
        bool closeUpDone, turned;
        int[] watcherOrder;
        Sprite[] watcherFace;            // 평소(창문을 보는) 그림
        Vector3[] watcherHome;           // 꿈틀거리기 전 자리
        float[] twitchAt, twitchEnd;
        Sprite backgroundShut;
        Vector2 lastMouse;
        AudioSource phoneSrc;
        float phoneStopAt = -1f;

        const int W = 640, H = 360;
        static readonly string[] MainLabels = { "새로 시작", "이어하기", "설정", "크레딧", "종료" };
        static readonly string[] SettingLabels = GameSettings.Labels;
        // 설정 화면: 제목을 숨기고 위에서부터 촘촘히(9줄)
        const int SettingsTop = 104, SettingsRow = 20;

        bool settings, hasSave, busy;
        int saveDay, selected = -1, hover = -1;
        readonly RectInt[] rects = new RectInt[9];
        readonly string[] shownText = new string[9];
        PixelTex fxTex, scanTex, titleTex, ghostTex, fadeTex;
        AudioSource rainSrc, humSrc;
        float nextEvent, glitchUntil, ghostUntil, burstUntil, nextFx;
        int glitchItem = -1;
        bool lastRed, lastColon;
        System.Random rnd = new System.Random(333);
        readonly Vector4[] drops = new Vector4[110];   // x, y, 길이, 속도

        void Awake()
        {
            GameSettings.Apply(look);
            GameSettings.ApplyDisplayAtStart();
            if (!Showing) SetVisible(false);
        }

        // ── 보이기·숨기기 ──
        /// <summary>메인 화면을 띄운다(게임을 켜자마자). 고를 때까지 Choice는 None.</summary>
        public void Show()
        {
            Choice = Pick.None;
            Showing = true;
            settings = false;
            busy = false;
            SaveData save = SaveSystem.Load();
            hasSave = File.Exists(SaveSystem.FilePath) && save.nextDay > 1;
            saveDay = save.nextDay;
            traces = save.traces;
            policeSeen = save.policeSeen;
            deaths = save.deaths;
            idle = 0f;
            watchersShown = 0;
            closeUpDone = false;
            watcherOrder = Shuffled(watchers.Length);
            lastMouse = GameInput.MousePosition;
            nextSound = Time.unscaledTime + Range(14f, 26f);
            Covering = false;
            Zoom(1f);
            SetVisible(true);
            if (fade != null) fade.enabled = false;
            BuildSprites();
            Layout();
            if (look != null) look.LookLocked = true;
            nextEvent = Time.unscaledTime + Range(9f, 16f);
            StartLoops();
        }

        void SetVisible(bool v)
        {
            foreach (var sr in new[] { background, player, fx, scan, title }) if (sr != null) sr.enabled = v;
            if (ghost != null) ghost.enabled = false;
            // 진행에 따라: 형체(흔적 1+), 유리의 피(흔적 2+), 전단(경찰이 다녀간 수), 죽은 적이 있으면 나는 없다
            if (traceFigure != null) traceFigure.enabled = v && traces >= 1;
            if (bloodGlass != null) bloodGlass.enabled = v && traces >= 2 && !GameSettings.NoBlood;
            for (int i = 0; i < flyers.Length; i++) if (flyers[i] != null) flyers[i].enabled = v && i < policeSeen;
            if (player != null && deaths > 0) player.enabled = false;
            if (monster != null) monster.enabled = false;
            ResetWatchers();
            if (closeUp != null) closeUp.enabled = false;
            foreach (var t in items) if (t != null) t.enabled = v;
            foreach (var t in values) if (t != null) t.enabled = false;
            foreach (var t in new[] { rec, camLabel, stamp, hint }) if (t != null) t.enabled = v;
            HideCredits();
        }

        // ── 그림 ──
        void BuildSprites()
        {
            if (scanTex == null && scan != null)
            {
                // 주사선: 세 줄마다 어두운 줄
                scanTex = new PixelTex(W, H);
                for (int y = 0; y < H; y += 3)
                    for (int x = 0; x < W; x++) scanTex.Put(x, y, new Color32(0, 0, 0, 34));
                scan.sprite = scanTex.ToSprite(Vector2.zero);
            }
            if (fxTex == null && fx != null)
            {
                fxTex = new PixelTex(W, H);
                for (int i = 0; i < drops.Length; i++) drops[i] = NewDrop(true);
                DrawFx();
            }
            if (ghostTex == null && ghost != null) ghost.sprite = DrawGhost();
            if (fade != null && fadeTex == null)
            {
                fadeTex = new PixelTex(1, 1);
                fadeTex.Put(0, 0, new Color32(0, 0, 0, 255));
                fade.sprite = fadeTex.ToSprite(Vector2.zero);
                fade.transform.localScale = new Vector3(W, H, 1f);
            }
            DrawTitle(IsThreeThirtyThree(), true);
        }

        Vector4 NewDrop(bool anywhere) => new Vector4(rnd.Next(W + 60), anywhere ? rnd.Next(H) : H + rnd.Next(30), 4 + rnd.Next(7), 220f + rnd.Next(160));

        /// <summary>움직이는 겹그림: 빗줄기(비스듬히) + 지지직 점 + 가끔 가로로 찢어지는 띠.</summary>
        void DrawFx()
        {
            Color32[] px = fxTex.Px;
            Array.Clear(px, 0, px.Length);
            bool burst = Time.unscaledTime < burstUntil && !GameSettings.ReduceFlicker;   // 깜빡임 줄이기: 지지직 폭발 없음
            // 빗줄기(엔딩의 새벽은 비가 그쳤다)
            var rainC = new Color32(210, 222, 230, 70);
            foreach (Vector4 d in drops)
                for (int j = 0; j < (int)d.z && !noRain; j++)
                {
                    int x = (int)d.x - j / 3, y = (int)d.y + j;
                    if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = rainC;
                }
            // 지지직 점
            int dots = burst ? 9000 : 1400;
            for (int i = 0; i < dots; i++)
            {
                int x = rnd.Next(W), y = rnd.Next(H);
                byte g = (byte)rnd.Next(120, 255);
                px[y * W + x] = new Color32(g, g, g, (byte)rnd.Next(30, burst ? 150 : 90));
            }
            // 가로로 찢어지는 띠
            int bands = burst ? 4 : (rnd.NextDouble() < 0.04 ? 1 : 0);
            for (int b = 0; b < bands; b++)
            {
                int y0 = rnd.Next(H), hgt = 1 + rnd.Next(burst ? 6 : 3);
                byte g = (byte)rnd.Next(150, 230);
                for (int y = y0; y < Mathf.Min(H, y0 + hgt); y++)
                    for (int x = 0; x < W; x++)
                        if (rnd.NextDouble() < 0.55) px[y * W + x] = new Color32(g, g, g, 60);
            }
            fxTex.Apply();
            if (fx.sprite == null) fx.sprite = fxTex.ToSprite(Vector2.zero);
        }

        /// <summary>창밖에 선 검은 형체: 머리·어깨 실루엣과 희미하게 빛나는 두 점.</summary>
        Sprite DrawGhost()
        {
            int h = Mathf.Max(16, ghostHeight), w = Mathf.Max(8, h * 2 / 5);
            ghostTex = new PixelTex(w, h);
            var body = new Color32(8, 9, 11, 235);
            float headR = w * 0.28f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w * 0.5f, cy = y + 0.5f;
                    bool head = cx * cx + (cy - (h - headR - 1)) * (cy - (h - headR - 1)) <= headR * headR;
                    float half = Mathf.Lerp(w * 0.5f, w * 0.3f, cy / (h * 0.78f));
                    bool torso = cy < h * 0.78f && Mathf.Abs(cx) <= half;
                    if (head || torso) ghostTex.Put(x, y, body);
                }
            int ey = Mathf.RoundToInt(h - headR - 1);
            ghostTex.Put(Mathf.RoundToInt(w * 0.5f - headR * 0.45f), ey, new Color32(235, 240, 240, 255));
            ghostTex.Put(Mathf.RoundToInt(w * 0.5f + headR * 0.45f) - 1, ey, new Color32(235, 240, 240, 255));
            return ghostTex.ToSprite(new Vector2(0.5f, 0f));
        }

        // ── 03:33 LED 숫자 ──
        static readonly bool[][] Seg =
        {
            // a, b, c, d, e, f, g
            new[] { true, true, true, true, true, true, false },   // 0
            new[] { true, true, true, true, false, false, true },  // 3
        };
        const int DigitW = 22, DigitH = 40, T = 4, Gap = 6, ColonW = 4;

        public static int TitleWidth => DigitW * 4 + Gap * 4 + ColonW;

        void DrawTitle(bool red, bool colon)
        {
            if (title == null) return;
            lastRed = red; lastColon = colon;
            int w = TitleWidth + 6, h = DigitH + 6;
            if (titleTex == null) titleTex = new PixelTex(w, h);
            titleTex.Clear();
            Color32 on = red ? ledRed : ledOrange;
            var glow = new Color32(on.r, on.g, on.b, 70);
            int x = 3;
            int[] digits = { 0, 1, -1, 1, 1 };   // 0 3 : 3 3 (Seg 인덱스: 0→"0", 1→"3")
            foreach (int d in digits)
            {
                if (d < 0)
                {
                    if (colon)
                        foreach (int cy in new[] { 3 + DigitH / 3, 3 + DigitH * 2 / 3 })
                        {
                            Box(x - 1, cy - 3, ColonW + 2, ColonW + 2, glow);
                            Box(x, cy - 2, ColonW, ColonW, on);
                        }
                    x += ColonW + Gap;
                    continue;
                }
                for (int s = 0; s < 7; s++) if (Seg[d][s]) DrawSegment(x, 3, s, on, glow);   // 꺼진 조각은 안 그린다(8처럼 보이지 않게)
                x += DigitW + Gap;
            }
            titleTex.Apply();
            title.sprite = titleTex.ToSprite(Vector2.zero);
        }

        void DrawSegment(int ox, int oy, int s, Color32 c, Color32 glow)
        {
            int half = DigitH / 2;
            RectInt r;
            switch (s)
            {
                case 0: r = new RectInt(ox + 1, oy + DigitH - T, DigitW - 2, T); break;                 // a 위
                case 1: r = new RectInt(ox + DigitW - T, oy + half + 1, T, half - 2); break;            // b 오른쪽 위
                case 2: r = new RectInt(ox + DigitW - T, oy + 1, T, half - 2); break;                   // c 오른쪽 아래
                case 3: r = new RectInt(ox + 1, oy, DigitW - 2, T); break;                              // d 아래
                case 4: r = new RectInt(ox, oy + 1, T, half - 2); break;                                // e 왼쪽 아래
                case 5: r = new RectInt(ox, oy + half + 1, T, half - 2); break;                         // f 왼쪽 위
                default: r = new RectInt(ox + 1, oy + half - T / 2, DigitW - 2, T); break;              // g 가운데
            }
            if (glow.a > 0) Box(r.x - 1, r.y - 1, r.width + 2, r.height + 2, glow);
            // 모서리를 깎아 LED 조각처럼
            for (int y = r.y; y < r.yMax; y++)
                for (int x = r.x; x < r.xMax; x++)
                {
                    bool corner = (x == r.x || x == r.xMax - 1) && (y == r.y || y == r.yMax - 1);
                    if (!corner) titleTex.Put(x, y, c);
                }
        }

        void Box(int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) titleTex.Set(x, y, c);
        }

        static bool IsThreeThirtyThree()
        {
            DateTime now = DateTime.Now;
            return now.Hour % 12 == 3 && now.Minute == 33;
        }

        // ── 메뉴 글자 ──
        void Layout()
        {
            string[] labels = settings ? SettingLabels : MainLabels;
            int y = settings ? SettingsTop : 4;
            // 값 칸(◀ ▶)은 가장 긴 이름 뒤에(영어 "Mouse Sensitivity"처럼 길어도 겹치지 않게)
            int valueX = settings && items.Length > 0 && items[0] != null ? Mathf.Max(-170, -292 + Mathf.CeilToInt(LongestLabel(items[0], labels)) + 10) : -170;
            for (int i = 0; i < items.Length; i++)
            {
                Text t = items[i];
                if (t == null) continue;
                bool show = i < labels.Length && (settings || i != 1 || hasSave);
                t.enabled = show;
                if (!show) { rects[i] = new RectInt(0, 0, 0, 0); continue; }
                string label = Loc.T(labels[i]);
                if (!settings && i == 1) label += $"    DAY {saveDay}";
                shownText[i] = label;
                t.text = (i == hover || i == selected ? "▶ " : "   ") + label;
                t.color = i == hover || i == selected ? hoverColor : textColor;
                t.rectTransform.anchoredPosition = new Vector2(-292, y);
                int w = Mathf.CeilToInt(t.preferredWidth) + 8;
                rects[i] = new RectInt(-294, y - 2, Mathf.Max(w, 120), 17);
                if (settings && i < values.Length && values[i] != null)
                {
                    values[i].enabled = true;
                    values[i].text = GameSettings.ValueText(i);
                    // 지금은 효과가 없는 값(전체 화면일 때 창 크기)은 회색
                    values[i].color = GameSettings.Inactive(i) && !(i == hover || i == selected) ? InactiveColor : t.color;
                    values[i].rectTransform.anchoredPosition = new Vector2(valueX, y);
                    rects[i] = new RectInt(-294, y - 2, valueX + 294 + Mathf.CeilToInt(values[i].preferredWidth) + 4, 17);
                }
                y -= settings ? SettingsRow : 22;
            }
            if (!settings) foreach (var v in values) if (v != null) v.enabled = false;
            if (hint != null)
                hint.text = !settings ? ""
                          : selected == GameSettings.WindowRow && GameSettings.Inactive(selected) ? Loc.T("창 크기는 창 모드일 때 적용돼요")
                          : Loc.T("◀ ▶ 를 눌러 바꾸기");
            if (camLabel != null) camLabel.text = Loc.T("CAM 03  주차장");
        }

        static readonly Color32 InactiveColor = new Color32(120, 128, 134, 255);

        /// <summary>설정 이름들 중 가장 긴 것의 폭("▶ " 붙인 채, 지금 언어·글꼴로). 값 칸 자리를 정한다.</summary>
        internal static float LongestLabel(Text t, string[] labels)
        {
            float max = 0f;
            var gen = t.cachedTextGeneratorForLayout;
            var set = t.GetGenerationSettings(Vector2.zero);
            for (int i = 0; i < labels.Length && i < GameSettings.Rows; i++)
                max = Mathf.Max(max, gen.GetPreferredWidth("▶ " + Loc.T(labels[i]), set) / Mathf.Max(0.0001f, t.pixelsPerUnit));
            return max;
        }

        /// <summary>설정 화면을 열고 닫는다(설정 중에는 제목을 숨겨 9줄이 들어갈 자리를 만든다).</summary>
        void SetSettings(bool on)
        {
            settings = on;
            selected = hover = -1;
            if (title != null) title.enabled = !on;
            Layout();
        }

        // ── 매 프레임 ──
        void Update()
        {
            if ((!Showing && !storyScene) || !Application.isPlaying) return;
            float now = Time.unscaledTime;

            // 움직이는 겹그림(30번/초)
            if (now >= nextFx && fxTex != null)
            {
                nextFx = now + 1f / 30f;
                float dt = 1f / 30f;
                for (int i = 0; i < drops.Length; i++)
                {
                    Vector4 d = drops[i];
                    d.y -= d.w * dt; d.x += d.w * dt / 3f;
                    drops[i] = d.y + d.z < 0 ? NewDrop(false) : d;
                }
                DrawFx();
            }

            // REC 깜빡임, 지금 시각, 제목(콜론·3시 33분)
            if (rec != null) rec.text = (now % 1f < 0.55f ? "<color=#ff3a30>●</color>" : "  ") + " REC";
            if (storyScene)
            {
                // 오프닝·엔딩: 메뉴 없이 CCTV 화면만. 시각은 그 장면의 시각(밤 11시 58분, 새벽 6시 12분)
                if (stamp != null) stamp.text = StoryStamp();
                return;
            }
            if (stamp != null) stamp.text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
            bool red = IsThreeThirtyThree(), colon = now % 1f < 0.6f;
            if (red != lastRed || colon != lastColon) DrawTitle(red, colon);

            // 가끔 일어나는 일(죽은 적이 있으면 계산대 앞에 그 괴물이 비치기도 한다)
            if (!busy && !credits && now >= nextEvent)
            {
                nextEvent = now + Range(12f, 24f);
                double roll = rnd.NextDouble();
                if (deaths > 0 && monster != null && roll < 0.4)
                {
                    monster.enabled = true; monsterUntil = now + Range(0.45f, 0.8f); burstUntil = now + 0.3f;
                    SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.5f);
                }
                else if (roll < 0.7) StartGlitch();
                else { ghostUntil = now + Range(0.5f, 0.9f); burstUntil = now + 0.25f; if (ghost != null) ghost.enabled = true; SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.35f); }
            }
            if (ghost != null && ghost.enabled && now >= ghostUntil) { ghost.enabled = false; burstUntil = now + 0.15f; }
            if (monster != null && monster.enabled && now >= monsterUntil) { monster.enabled = false; burstUntil = now + 0.2f; }
            if (!busy && !credits) { Idle(now); FarSounds(now); }
            if (glitchItem >= 0)
            {
                if (now >= glitchUntil) EndGlitch();
                else items[glitchItem].rectTransform.anchoredPosition = new Vector2(-292 + rnd.Next(-2, 3), items[glitchItem].rectTransform.anchoredPosition.y);
            }

            if (busy) return;
            if (credits) { UpdateCredits(); return; }
            Pointer();
        }

        // ── 가만히 두면 모여드는 것들 ──
        /// <summary>
        /// 아무것도 안 하고 있으면 비 오는 주차장에 잿빛 시체가 하나씩 늘어난다(지지직 하며 나타남).
        /// 다 모이고 조금 지나면 하나가 카메라 바로 앞에 얼굴을 들이밀었다 사라진다. 마우스를 움직이거나 누르면 흩어진다.
        /// </summary>
        void Idle(float now)
        {
            Vector2 m = GameInput.MousePosition;
            bool input = GameInput.AnyDown || (m - lastMouse).sqrMagnitude > 9f;
            lastMouse = m;
            if (input)
            {
                if (watchersShown > 0 || (closeUp != null && closeUp.enabled))
                {
                    HideIdle();
                    burstUntil = now + 0.22f;
                    SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.3f);
                }
                idle = 0f;
                closeUpDone = false;
                return;
            }
            idle += Time.unscaledDeltaTime;
            if (closeUp != null && closeUp.enabled)
            {
                if (now >= closeUpUntil) { HideIdle(); burstUntil = now + 0.35f; idle = 0f; }
                return;
            }
            int want = idle < idleFirst ? 0 : Mathf.Min(watchers.Length, 1 + Mathf.FloorToInt((idle - idleFirst) / idleStep));
            while (watchersShown < want)
            {
                int i = watcherOrder[watchersShown++];
                ShowWatcher(i);
                if (watchers[i] != null) twitchAt[i] = now + Range(1.5f, 4f);
                burstUntil = now + 0.18f;
                SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.22f);
            }
            Twitch(now);

            // 다 모이고 조금 지나면: 모두 동시에 고개를 홱 돌려 카메라를 본다(눈만 하얗게). 그다음 하나가 바로 앞에 들이민다
            float turnAt = idleFirst + idleStep * Mathf.Max(0, watchers.Length - 1) + idleTurn;
            if (!turned && watchers.Length > 0 && idle >= turnAt)
            {
                Turn(true);
                burstUntil = now + 0.12f;
                SoundFx.Play(sfx, SoundFx.Tear, 0.5f);
                SoundFx.Play(sfx, SoundFx.TubePop, 0.5f);
            }
            float closeAt = turnAt + idleCloseUp;
            if (!closeUpDone && closeUp != null && watchers.Length > 0 && idle >= closeAt)
            {
                closeUpDone = true;
                foreach (var w in watchers) if (w != null) w.enabled = false;
                foreach (var w in watcherShade) if (w != null) w.enabled = false;
                closeUp.enabled = true;
                closeUpUntil = now + 0.6f;
                burstUntil = now + 0.6f;
                SoundFx.Play(sfx, SoundFx.TubePop, 0.9f);
                SoundFx.Play(sfx, SoundFx.TubeBuzz, 1f);
            }
        }

        void HideIdle()
        {
            ResetWatchers();
            if (closeUp != null) closeUp.enabled = false;
            watcherOrder = Shuffled(watchers.Length);
        }

        /// <summary>모두 숨기고 처음 모습(창문을 보는 그림, 제자리, 닫힌 문)으로 되돌린다.</summary>
        void ResetWatchers()
        {
            if (watcherFace == null || watcherFace.Length != watchers.Length)
            {
                watcherFace = new Sprite[watchers.Length];
                watcherHome = new Vector3[watchers.Length];
                twitchAt = new float[watchers.Length];
                twitchEnd = new float[watchers.Length];
                for (int i = 0; i < watchers.Length; i++)
                    if (watchers[i] != null) { watcherFace[i] = watchers[i].sprite; watcherHome[i] = watchers[i].transform.localPosition; }
            }
            if (backgroundShut == null && background != null) backgroundShut = background.sprite;
            for (int i = 0; i < watchers.Length; i++)
            {
                if (i < watcherShade.Length && watcherShade[i] != null) watcherShade[i].enabled = false;
                if (watchers[i] == null) continue;
                watchers[i].enabled = false;
                watchers[i].sprite = watcherFace[i];
                watchers[i].transform.localPosition = watcherHome[i];
                twitchEnd[i] = 0f;
            }
            if (background != null && backgroundShut != null) background.sprite = backgroundShut;
            watchersShown = 0;
            turned = false;
        }

        void ShowWatcher(int i)
        {
            if (watchers[i] == null) return;
            watchers[i].enabled = true;
            if (i < watcherShade.Length && watcherShade[i] != null) watcherShade[i].enabled = true;
            // 문틈으로 들어가는 것: 그 순간 자동문이 살짝 벌어져 있다
            if (i == doorWatcher && background != null && backgroundDoorOpen != null) background.sprite = backgroundDoorOpen;
        }

        void Turn(bool on)
        {
            turned = on;
            for (int i = 0; i < watchers.Length; i++)
            {
                if (watchers[i] == null) continue;
                Sprite s = on && i < watcherTurn.Length && watcherTurn[i] != null ? watcherTurn[i] : watcherFace[i];
                watchers[i].sprite = s;
                watchers[i].transform.localPosition = watcherHome[i];
            }
        }

        /// <summary>창에 붙은 것들이 가끔 1픽셀씩 꿈틀한다(고개를 돌린 뒤에는 꼼짝 않고 본다).</summary>
        void Twitch(float now)
        {
            if (turned) return;
            for (int i = 0; i < watchers.Length; i++)
            {
                SpriteRenderer w = watchers[i];
                if (w == null || !w.enabled) continue;
                if (twitchEnd[i] > 0f && now >= twitchEnd[i]) { w.transform.localPosition = watcherHome[i]; twitchEnd[i] = 0f; }
                if (now < twitchAt[i]) continue;
                int dx = rnd.Next(3) - 1, dy = rnd.Next(2) == 0 ? -1 : 0;
                if (dx == 0 && dy == 0) dy = -1;
                w.transform.localPosition = watcherHome[i] + new Vector3(dx, dy, 0f);
                twitchEnd[i] = now + Range(0.12f, 0.3f);
                twitchAt[i] = now + Range(1.5f, 5f);
            }
        }

        int[] Shuffled(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = rnd.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); }
            return a;
        }

        // ── 멀리서 나는 소리 ──
        /// <summary>가끔 아주 멀리서 전화벨이 울리다 뚝 끊기거나, 아무도 없는데 문 차임이 울린다.</summary>
        void FarSounds(float now)
        {
            if (phoneSrc != null && phoneStopAt > 0f && now >= phoneStopAt) { phoneSrc.Stop(); phoneStopAt = -1f; }
            if (now < nextSound || !Application.isPlaying) return;
            nextSound = now + Range(22f, 40f);
            if (rnd.NextDouble() < 0.6)
            {
                if (phoneSrc == null)
                {
                    phoneSrc = gameObject.AddComponent<AudioSource>();
                    phoneSrc.playOnAwake = false;
                    phoneSrc.spatialBlend = 0f;
                    phoneSrc.clip = SoundFx.PhoneRing;
                }
                phoneSrc.volume = Range(0.05f, 0.09f);
                phoneSrc.pitch = Range(0.94f, 1f);
                phoneSrc.Play();
                phoneStopAt = now + Range(0.35f, 1.05f);   // 따르르… 하다 뚝
            }
            else if (chime != null && sfx != null) sfx.PlayOneShot(chime, 0.14f);
        }

        void StartGlitch()
        {
            var shown = new System.Collections.Generic.List<int>();
            for (int i = 0; i < items.Length; i++) if (items[i] != null && items[i].enabled) shown.Add(i);
            if (shown.Count == 0) return;
            glitchItem = shown[rnd.Next(shown.Count)];
            items[glitchItem].text = "   " + CustomerLines.Corrupt(shownText[glitchItem] ?? items[glitchItem].text, rnd, 0.6f);
            glitchUntil = Time.unscaledTime + 0.28f;
            burstUntil = Time.unscaledTime + 0.28f;
            SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.3f);
        }

        void EndGlitch()
        {
            glitchItem = -1;
            Layout();
        }

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
                if (over >= 0) { selected = over; SoundFx.Play(sfx, SoundFx.Tick, 0.4f); }
                if (glitchItem < 0) Layout();
            }

            // 키보드
            if (GameInput.UpDown || GameInput.DownDown)
            {
                int dir = GameInput.UpDown ? -1 : 1;
                for (int k = 0; k < items.Length; k++)
                {
                    selected = ((selected < 0 ? (dir > 0 ? -1 : 0) : selected) + dir + items.Length) % items.Length;
                    if (rects[selected].width > 0) break;
                }
                hover = -1;
                SoundFx.Play(sfx, SoundFx.Tick, 0.4f);
                if (glitchItem < 0) Layout();
            }
            if (settings && (GameInput.PrevDown || GameInput.NextDown) && selected >= 0 && selected < GameSettings.Rows)
                Adjust(selected, GameInput.NextDown ? 1 : -1);
            if (settings && (GameInput.EscapeDown || GameInput.RightClickDown)) { SetSettings(false); return; }

            bool click = GameInput.LeftClickDown && over >= 0;
            if (click || (GameInput.SubmitDown && selected >= 0))
            {
                int i = click ? over : selected;
                if (settings && i < GameSettings.Rows)
                {
                    // ◀ 쪽을 누르면 내리고 ▶ 쪽을 누르면 올린다(키보드는 올리기)
                    int dir = 1;
                    if (click && values[i] != null)
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

        void Adjust(int row, int dir)
        {
            GameSettings.Adjust(row, dir, look);
            // 혈흔 없애기는 뒤에 보이는 주차장 CCTV(유리의 피)에도 바로
            if (bloodGlass != null) bloodGlass.enabled = Showing && traces >= 2 && !GameSettings.NoBlood;
            SoundFx.Play(sfx, SoundFx.Tick, 0.6f);
            Layout();
        }

        void Choose(int i)
        {
            SoundFx.Play(sfx, SoundFx.Tick, 0.9f);
            if (settings) { SetSettings(false); return; }   // 뒤로
            switch (i)
            {
                case 0: StartCoroutine(Begin(Pick.NewGame)); break;
                case 1: if (hasSave) StartCoroutine(Begin(Pick.Continue)); break;
                case 2: SetSettings(true); break;
                case 3: OpenCredits(); break;
                default:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        /// <summary>
        /// 시작: 글자가 지지직 사라지고 CCTV가 가게 문 쪽으로 빨려 들어가듯 확대되다가 암전.
        /// 검은 화면은 그대로 두고(Covering) 고른 것을 알린다. CheckoutSystem이 어둠 속에서 전화벨을 울린 뒤 Reveal로 걷는다.
        /// </summary>
        IEnumerator Begin(Pick pick)
        {
            busy = true;
            foreach (var t in items) if (t != null) t.enabled = false;
            foreach (var t in values) if (t != null) t.enabled = false;
            if (title != null) title.enabled = false;
            if (hint != null) hint.enabled = false;
            HideIdle();
            if (monster != null) monster.enabled = false;
            if (ghost != null) ghost.enabled = false;
            if (phoneSrc != null) phoneSrc.Stop();
            SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.6f);
            if (fade != null) fade.enabled = true;
            SetFade(0f);
            // 새로 시작은 오프닝(내 방 → 가게로 걸어 들어감)이 먼저: 여기서는 그냥 어두워지고, 문 쪽 확대는 가게에 들어갈 때
            bool zoom = !(pick == Pick.NewGame && walkIn.Length > 0);
            const float zoomTime = 1.4f;
            for (float t = 0f; t < zoomTime; t += Time.unscaledDeltaTime)
            {
                float k = t / zoomTime, e = k * k * k;   // 천천히 다가가다 빨려 들어간다
                if (zoom)
                {
                    Zoom(1f + 3.4f * e);
                    if (k > 0.55f) burstUntil = Time.unscaledTime + 0.05f;
                }
                SetFade(zoom ? Mathf.Clamp01((k - 0.6f) / 0.4f) : k);
                SetLoopVolume(1f - k);
                yield return null;
            }
            SetFade(1f);
            StopLoops();
            SetVisible(false);
            Zoom(1f);
            Showing = false;
            Covering = true;
            if (look != null) look.LookLocked = false;
            Choice = pick;
            busy = false;
        }

        /// <summary>검게 덮인 화면을 걷는다(전화벨이 울린 뒤 가게가 밝아진다).</summary>
        public IEnumerator Reveal(float seconds = 1.3f)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFade(1f - t / seconds);
                yield return null;
            }
            SetFade(0f);
            if (fade != null) fade.enabled = false;
            Covering = false;
        }

        void Zoom(float s)
        {
            if (sceneRoot == null) return;
            sceneRoot.localScale = new Vector3(s, s, 1f);
            sceneRoot.localPosition = new Vector3(-doorPoint.x * (s - 1f), -doorPoint.y * (s - 1f), sceneRoot.localPosition.z);
        }

        void SetFade(float a)
        {
            if (fade != null) fade.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        // ── 빗소리·웅웅 ──
        void StartLoops()
        {
            if (!Application.isPlaying) return;
            if (rainSrc == null) rainSrc = Loop(SoundFx.Rain);
            if (humSrc == null) humSrc = Loop(SoundFx.TubeHum);
            SetLoopVolume(1f);
            if (!rainSrc.isPlaying) rainSrc.Play();
            if (!humSrc.isPlaying) humSrc.Play();
        }

        AudioSource Loop(AudioClip clip)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        void SetLoopVolume(float k)
        {
            if (rainSrc != null) rainSrc.volume = 0.45f * k;
            if (humSrc != null) humSrc.volume = 0.12f * k;
        }

        void StopLoops()
        {
            if (rainSrc != null) rainSrc.Stop();
            if (humSrc != null) humSrc.Stop();
        }

        float Range(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        // ── 미리보기(에디터) ──
        public void PreviewShow(int continueDay, bool showSettings = false, bool glitch = false, int select = 0)
        {
            Showing = true;
            hasSave = continueDay > 1;
            saveDay = continueDay;
            settings = showSettings;
            SetVisible(true);
            if (fade != null) fade.enabled = false;
            BuildSprites();
            DrawTitle(false, true);
            selected = select; hover = -1;
            Layout();
            if (showSettings && title != null) title.enabled = false;
            if (rec != null) rec.text = "<color=#ff3a30>●</color> REC";
            if (stamp != null) stamp.text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
            if (glitch)
            {
                burstUntil = float.MaxValue;
                DrawFx();
                burstUntil = 0f;
                if (ghost != null) ghost.enabled = true;
                items[0].text = "   " + CustomerLines.Corrupt(MainLabels[0], new System.Random(5), 0.6f);
            }
            Showing = false;
        }

        /// <summary>미리보기: 진행 상태(흔적·전단·죽음), 모여든 시체 수, 얼굴 들이밀기, 괴물, 시작 확대 정도.</summary>
        public void PreviewProgress(int previewTraces, int previewPolice, int previewDeaths, int shownWatchers, bool showCloseUp, bool showMonster, float zoom = 1f,
                                    bool turnWatchers = false)
        {
            traces = previewTraces; policeSeen = previewPolice; deaths = previewDeaths;
            PreviewShow(3);
            for (int i = 0; i < watchers.Length && i < shownWatchers; i++) ShowWatcher(i);
            if (turnWatchers) Turn(true);
            if (closeUp != null) closeUp.enabled = showCloseUp;
            if (monster != null) monster.enabled = showMonster;
            Zoom(zoom);
            if (zoom > 1.01f) foreach (var t in items) if (t != null) t.enabled = false;
            if (zoom > 1.01f && title != null) title.enabled = false;
        }

        public void PreviewHide()
        {
            traces = policeSeen = deaths = 0;
            Zoom(1f);
            SetVisible(false);
            if (fade != null) fade.enabled = false;
        }
    }
}
