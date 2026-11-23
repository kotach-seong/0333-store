using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 수첩. 계산대 위 수첩을 클릭하면 크게 펼쳐지고, 내용은 Notebook.txt에서 읽는다.
    ///  - 한 번에 두 쪽(왼쪽·오른쪽)을 보여 준다. 쪽이 더 있으면 아래 ◀ ▶ 버튼으로 넘긴다.
    ///  - 왼쪽 위 빨간 X, Esc, 마우스 오른쪽 클릭으로 닫는다.
    ///
    /// Notebook.txt 형식:
    ///   # 제목          ← 갈무리11로 크게
    ///   본문 한 줄       ← 갈무리9로, 종이의 줄 하나에 한 줄씩(길면 자동 줄바꿈)
    ///   ===             ← 다음 쪽
    /// </summary>
    public class NotebookSystem : MonoBehaviour
    {
        [Header("연결")]
        public MouseLookCamera look;
        public Camera viewCamera;
        public GameObject panel;
        public TextAsset content;

        [Header("글자 자리")]
        public Text leftTitle, rightTitle, leftPageNo, rightPageNo;
        public Text[] leftLines, rightLines;
        [Tooltip("본문 한 줄에 쓸 수 있는 폭(px)")]
        public float lineWidth = 210f;

        [Header("버튼")]
        public SpriteRenderer prevButton, nextButton, prevHover, nextHover, closeHover;
        public Rect prevArea, nextArea, closeArea;

        [Header("상태")]
        [Tooltip("지금 펼친 곳(0이면 1·2쪽)")]
        public int spread;

        public bool IsOpen => panel != null && panel.activeSelf;

        struct Page { public string Title; public List<string> Lines; }

        readonly List<Page> pages = new List<Page>();
        int openedFrame = -1, closedFrame = -1;

        // 근무 중에 바뀌는 내용. 비어 있으면(null) Notebook.txt 그대로 보여 준다
        List<string> rules;                       // 점장 전화로 늘어나는 "야간 근무 수칙"(번호 붙은 줄)
        bool todaySet, todayCalled;               // "DAY n 오늘의 할 일"
        int todayDay, todayServed, todayTotal;

        /// <summary>
        /// "야간 근무 수칙" 쪽을 이 줄들로 바꾼다(점장 전화를 받을 때마다 늘어난다. 첫 전화 전에는 빈 쪽).
        /// 한 쪽에 다 안 들어가면 맨 뒤에 "(계속)" 쪽이 생긴다.
        /// </summary>
        public void SetRules(IList<string> lines)
        {
            rules = new List<string>(lines);
            if (IsOpen) Show(spread);
        }

        /// <summary>"DAY n 오늘의 할 일" 쪽: 날짜, 점장 전화를 받았는지, 손님 계산 몇 명째인지.</summary>
        public void SetToday(int day, bool called, int served, int total)
        {
            todaySet = true;
            todayDay = day;
            todayCalled = called;
            todayServed = served;
            todayTotal = total;
            if (IsOpen) Show(spread);
        }

        int SpreadCount => Mathf.Max(1, (pages.Count + 1) / 2);

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen || panel == null) return;
            if (Time.frameCount == closedFrame) return; // X를 누른 클릭으로 닫자마자 다시 열리지 않게
            panel.SetActive(true);
            openedFrame = Time.frameCount;
            if (look != null) look.LookLocked = true;
            Show(spread);
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            closedFrame = Time.frameCount;
            if (look != null) look.LookLocked = false;
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;

            if (GameInput.EscapeDown || GameInput.RightClickDown) { Close(); return; }
            if (GameInput.PrevDown) Show(spread - 1);
            if (GameInput.NextDown) Show(spread + 1);

            bool overPrev = false, overNext = false, overClose = false;
            if (viewCamera != null)
            {
                Vector2 p = (Vector2)viewCamera.ScreenToWorldPoint(GameInput.MousePosition) - (Vector2)viewCamera.transform.position;
                overPrev = prevButton != null && prevButton.enabled && prevArea.Contains(p);
                overNext = nextButton != null && nextButton.enabled && nextArea.Contains(p);
                overClose = closeArea.Contains(p);
            }
            if (prevHover != null) prevHover.enabled = overPrev;
            if (nextHover != null) nextHover.enabled = overNext;
            if (closeHover != null) closeHover.enabled = overClose;

            if (GameInput.LeftClickDown)
            {
                if (overClose) Close();
                else if (overPrev) Show(spread - 1);
                else if (overNext) Show(spread + 1);
            }
        }

        /// <summary>내용 파일을 다시 읽고 index번째 펼침을 보여 준다(에디터 미리보기에서도 쓴다).</summary>
        public void Show(int index)
        {
            Load();
            spread = Mathf.Clamp(index, 0, SpreadCount - 1);
            int l = spread * 2, r = l + 1;
            Fill(l, leftTitle, leftLines, leftPageNo);
            Fill(r, rightTitle, rightLines, rightPageNo);
            // 더 넘길 쪽이 없으면 그 방향 버튼은 숨긴다
            if (prevButton != null) prevButton.enabled = spread > 0;
            if (nextButton != null) nextButton.enabled = spread < SpreadCount - 1;
            if (prevHover != null) prevHover.enabled = false;
            if (nextHover != null) nextHover.enabled = false;
        }

        void Load()
        {
            pages.Clear();
            if (content == null) return;
            Page cur = default;
            bool has = false;
            foreach (string raw in Loc.Text(content).Replace("\r", "").Split('\n'))   // 지금 언어 수첩(Notebook.en.txt 등, 원본은 그대로)
            {
                string line = raw.TrimEnd();
                if (line.Trim() == "===")
                {
                    if (has) pages.Add(cur);
                    has = false;
                    continue;
                }
                if (!has) { cur = new Page { Title = "", Lines = new List<string>() }; has = true; }
                if (line.StartsWith("#") && cur.Title == "" && cur.Lines.Count == 0) cur.Title = line.TrimStart('#').Trim();
                else if (line.Length > 0 || cur.Lines.Count > 0) cur.Lines.Add(line);
            }
            if (has) pages.Add(cur);
            ApplyLive();
        }

        /// <summary>파일 내용 위에 근무 중 바뀐 것(전화로 받은 수칙, 오늘의 할 일)을 덮어쓴다.</summary>
        void ApplyLive()
        {
            var extra = new List<Page>();
            for (int i = 0; i < pages.Count; i++)
            {
                Page p = pages[i];
                if (rules != null && p.Title.Contains(Loc.T("수칙")))
                {
                    // 미리 줄을 나눠서 한 쪽(줄 칸 수)에 안 들어가는 만큼은 뒤쪽 "(계속)"으로
                    var wrapped = new List<string>();
                    Text slot = leftLines != null && leftLines.Length > 0 ? leftLines[0] : null;
                    foreach (string r in rules)
                    {
                        if (slot != null && slot.font != null) Wrap(r, slot.font, slot.fontSize, wrapped);
                        else wrapped.Add(r);
                    }
                    int per = Mathf.Max(1, leftLines != null ? leftLines.Length : 16);
                    p.Lines = wrapped.GetRange(0, Mathf.Min(per, wrapped.Count));
                    for (int at = per; at < wrapped.Count; at += per)
                        extra.Add(new Page { Title = p.Title + " " + Loc.T("(계속)"), Lines = wrapped.GetRange(at, Mathf.Min(per, wrapped.Count - at)) });
                }
                if (todaySet && p.Title.StartsWith("DAY"))
                {
                    p.Title = Regex.Replace(p.Title, @"^DAY\s*\d+", "DAY " + todayDay);
                    var lines = new List<string>();
                    foreach (string l in p.Lines)
                    {
                        string s = l;
                        string done = Loc.T("(완료)"), served = Loc.T("손님 계산");
                        if (s.Contains(Loc.T("점장 전화")) && todayCalled && !s.Contains(done)) s += " " + done;
                        s = Regex.Replace(s, Regex.Escape(served) + @"\s*\d+\s*/\s*\d+", $"{served} {todayServed}/{todayTotal}");
                        lines.Add(s);
                    }
                    p.Lines = lines;
                }
                pages[i] = p;
            }
            pages.AddRange(extra);
        }

        void Fill(int pageIndex, Text title, Text[] slots, Text pageNo)
        {
            bool exists = pageIndex < pages.Count;
            if (title != null) title.text = exists ? pages[pageIndex].Title : "";
            if (pageNo != null) pageNo.text = exists ? (pageIndex + 1).ToString() : "";
            if (slots == null) return;

            var wrapped = new List<string>();
            if (exists && slots.Length > 0 && slots[0] != null)
                foreach (string line in pages[pageIndex].Lines)
                    Wrap(line, slots[0].font, slots[0].fontSize, wrapped);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].text = i < wrapped.Count ? wrapped[i] : "";
        }

        /// <summary>
        /// 폭(lineWidth)을 넘으면 띄어쓰기 자리에서, 없으면 글자 단위로 줄을 나눈다.
        /// "1. ", "- " 로 시작하는 줄은 넘어간 줄을 그 글머리 폭만큼 들여 쓴다.
        /// </summary>
        void Wrap(string line, Font font, int size, List<string> outLines)
        {
            if (font == null || line.Length == 0) { outLines.Add(line); return; }
            font.RequestCharactersInTexture(line + " ", size, FontStyle.Normal);

            int first = outLines.Count;
            string indent = HangingIndent(line, font, size);
            WrapInto(line, font, size, outLines);
            if (indent.Length == 0) return;
            for (int i = first + 1; i < outLines.Count; i++) outLines[i] = indent + outLines[i];
        }

        /// <summary>글머리("1. ", "12. ", "- ")와 같은 폭의 띄어쓰기. 글머리가 없으면 빈 문자열.</summary>
        string HangingIndent(string line, Font font, int size)
        {
            int n = 0;
            while (n < line.Length && char.IsDigit(line[n])) n++;
            if (n > 0 && n < line.Length && line[n] == '.') n++;
            else if (line.StartsWith("- ")) n = 1;
            else return "";
            while (n < line.Length && line[n] == ' ') n++;

            float prefix = 0f;
            for (int i = 0; i < n; i++) prefix += Advance(font, line[i], size);
            float space = Mathf.Max(1f, Advance(font, ' ', size));
            return new string(' ', Mathf.RoundToInt(prefix / space));
        }

        static float Advance(Font font, char ch, int size) =>
            font.GetCharacterInfo(ch, out CharacterInfo info, size, FontStyle.Normal) ? info.advance : size;

        void WrapInto(string line, Font font, int size, List<string> outLines)
        {
            float limit = lineWidth - Advance(font, ' ', size) * HangingIndent(line, font, size).Length;

            var sb = new StringBuilder();
            float width = 0f;
            int lastSpace = -1;
            float widthAtSpace = 0f;
            bool firstLine = true;
            // 중국어는 띄어쓰기가 없어서 글자 단위로 나눈다(띄어쓰기 자리에서 나누면 "4."만 한 줄에 남는다)
            bool byChar = Loc.Current == Lang.Zh;
            foreach (char ch in line)
            {
                float adv = Advance(font, ch, size);
                // 첫 줄은 전체 폭, 들여 쓰는 다음 줄부터는 들여쓴 만큼 좁게
                if (width + adv > (firstLine ? lineWidth : limit) && sb.Length > 0)
                {
                    // 마침표·쉼표 같은 닫는 글자는 줄 머리에 오지 않게 이 줄 끝에 붙인다(조금 넘쳐도)
                    if (byChar && "。，、！？；：）」』…".IndexOf(ch) >= 0) { sb.Append(ch); width += adv; continue; }
                    firstLine = false;
                    if (lastSpace > 0 && !byChar)
                    {
                        outLines.Add(sb.ToString(0, lastSpace));
                        string rest = sb.ToString(lastSpace + 1, sb.Length - lastSpace - 1);
                        sb.Clear().Append(rest);
                        width -= widthAtSpace;
                    }
                    else
                    {
                        outLines.Add(sb.ToString());
                        sb.Clear();
                        width = 0f;
                    }
                    lastSpace = -1;
                }
                if (ch == ' ') { lastSpace = sb.Length; widthAtSpace = width + adv; }
                sb.Append(ch);
                width += adv;
            }
            outLines.Add(sb.ToString());
        }
    }
}
