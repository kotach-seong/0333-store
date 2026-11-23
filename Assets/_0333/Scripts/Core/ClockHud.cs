using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 화면 오른쪽 위의 DAY와 시각. 고개를 돌려도 늘 같은 자리에 있고, 패널을 열어도 위에 보인다.
    ///  - 시각은 도트로 찍은 굵은 숫자(4x7 글자를 2배, 획 2픽셀, 높이 14px), DAY는 4x7 글자 그대로(높이 7px).
    ///  - 형광등 빛 같은 크림색 + 1픽셀 어두운 외곽선(밝은 천장 위에서도 읽히게).
    ///  - 콜론(:)은 1초마다 깜빡인다.
    ///  - 시각 밑에 하트 3개(목숨 = 흔적), 그 밑에 사람 3개(실종). 하나씩 금 간 회색이 된다.
    /// </summary>
    public class ClockHud : MonoBehaviour
    {
        public NightClock clock;
        public SpriteRenderer target;
        [Tooltip("목숨 하트(흔적이 쌓이면 하나씩 깨진다)")]
        public FateSystem fate;
        public Color32 color = new Color32(237, 230, 200, 255);
        public Color32 outline = new Color32(20, 18, 24, 235);

        public const int W = 52, H = 46;
        const int HeartRow = 19;   // 하트 줄 + 사람 줄 높이(맨 아래)
        const int HeartY = 11, PeopleY = 1;
        const int GlyphH = 7;
        PixelTex pt;
        string shown;
        bool alarmOn;

        // 글자(위에서부터 일곱 줄, 너비는 글자마다 다름)
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['0'] = new[] { ".##.", "#..#", "#..#", "#..#", "#..#", "#..#", ".##." },
            ['1'] = new[] { ".#..", "##..", ".#..", ".#..", ".#..", ".#..", "###." },
            ['2'] = new[] { ".##.", "#..#", "...#", "..#.", ".#..", "#...", "####" },
            ['3'] = new[] { "###.", "...#", "...#", ".##.", "...#", "...#", "###." },
            ['4'] = new[] { "#..#", "#..#", "#..#", "####", "...#", "...#", "...#" },
            ['5'] = new[] { "####", "#...", "###.", "...#", "...#", "#..#", ".##." },
            ['6'] = new[] { ".##.", "#...", "#...", "###.", "#..#", "#..#", ".##." },
            ['7'] = new[] { "####", "...#", "..#.", "..#.", ".#..", ".#..", ".#.." },
            ['8'] = new[] { ".##.", "#..#", "#..#", ".##.", "#..#", "#..#", ".##." },
            ['9'] = new[] { ".##.", "#..#", "#..#", ".###", "...#", "...#", ".##." },
            ['D'] = new[] { "###.", "#..#", "#..#", "#..#", "#..#", "#..#", "###." },
            ['A'] = new[] { ".##.", "#..#", "#..#", "####", "#..#", "#..#", "#..#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            [' '] = new[] { "..", "..", "..", "..", "..", "..", ".." },
        };

        /// <summary>3:33(거꾸로 가는 시간): 시각이 빨갛게 깜빡인다.</summary>
        public static bool Alarm;
        static readonly Color32 AlarmRed = new Color32(236, 52, 48, 255);

        void LateUpdate() => Refresh();

        /// <summary>바뀐 게 있으면 다시 그린다(에디터 미리보기에서도 부른다).</summary>
        public void Refresh()
        {
            if (clock == null || target == null) return;
            bool colon = !Application.isPlaying || Time.unscaledTime % 1f < 0.6f;
            string time = clock.Format();
            int lives = fate != null ? fate.Lives : -1;
            if (lives >= 0 && lastLives >= 0 && lives < lastLives) breakUntil = Time.unscaledTime + 1.6f;   // 방금 깨진 하트는 잠깐 깜빡인다
            lastLives = lives;
            int people = fate != null ? fate.MissingLeft : -1;
            if (people >= 0 && lastPeople >= 0 && people < lastPeople) peopleBreakUntil = Time.unscaledTime + 1.6f;   // 방금 사라진 사람도
            lastPeople = people;
            bool blink = Blinking(breakUntil), blinkP = Blinking(peopleBreakUntil);
            alarmOn = Alarm && (!Application.isPlaying || Time.unscaledTime % 0.4f < 0.24f);
            string key = clock.day + "|" + time + "|" + colon + "|" + lives + "|" + blink + "|" + people + "|" + blinkP + "|" + alarmOn;
            if (key == shown) return;
            shown = key;
            Draw($"DAY {clock.day}", time, colon);
        }

        static int Width(char c) => Glyphs.TryGetValue(c, out var g) ? g[0].Length : 4;

        /// <summary>글자들을 s배로 늘어놓았을 때의 너비(글자 사이 s칸).</summary>
        static int TextWidth(string text, int s, int colonW)
        {
            int w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                w += text[i] == ':' ? colonW : Width(text[i]) * s;
                if (i < text.Length - 1) w += s;
            }
            return w;
        }

        void Draw(string dayText, string time, bool colon)
        {
            if (pt == null) pt = new PixelTex(W, H);
            pt.Clear();
            var ink = new bool[W * H];

            // 시각: 2배 굵기, 오른쪽 맞춤, 아래쪽(아래 1px 띄움)
            const int s = 2, colonW = 2;
            int x = W - 2 - TextWidth(time, s, colonW);
            int top = 1 + HeartRow + GlyphH * s;   // 글자 맨 윗줄 바로 위(아래에 하트 줄)
            foreach (char ch in time)
            {
                if (ch == ':')
                {
                    if (colon)
                    {
                        Block(ink, x, top - 3 * s, colonW, s);   // 위 점(셋째 줄)
                        Block(ink, x, top - 5 * s, colonW, s);   // 아래 점(다섯째 줄)
                    }
                    x += colonW + s;
                    continue;
                }
                Stamp(ink, ch, x, top, s);
                x += Width(ch) * s + s;
            }

            // DAY: 1배, 오른쪽 맞춤, 시각 위(3px 띄움)
            int dx = W - 2 - TextWidth(dayText, 1, 0);
            int dtop = top + 3 + GlyphH;
            foreach (char ch in dayText)
            {
                Stamp(ink, ch, dx, dtop, 1);
                dx += Width(ch) + 1;
            }

            // 외곽선 → 글자
            for (int y = 0; y < H; y++)
                for (int xx = 0; xx < W; xx++)
                {
                    if (ink[y * W + xx]) continue;
                    bool near = false;
                    for (int oy = -1; oy <= 1 && !near; oy++)
                        for (int ox = -1; ox <= 1 && !near; ox++)
                        {
                            int nx = xx + ox, ny = y + oy;
                            if (nx >= 0 && ny >= 0 && nx < W && ny < H && ink[ny * W + nx]) near = true;
                        }
                    if (near) pt.Put(xx, y, outline);
                }
            for (int i = 0; i < ink.Length; i++) if (ink[i]) pt.Px[i] = alarmOn ? AlarmRed : color;
            if (fate != null)
            {
                DrawIcons(Heart, Crack, fate.Lives, HeartY, Blinking(breakUntil), HeartAlive, HeartShine);
                DrawIcons(Person, Slash, fate.MissingLeft, PeopleY, Blinking(peopleBreakUntil), PersonAlive, PersonShine);
            }
            target.sprite = pt.ToSprite(Vector2.zero);
        }

        // ── 목숨 하트(흔적) / 사람(실종) ──
        static readonly string[] Heart = { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." };
        static readonly string[] Crack = { ".......", "...#...", "....#..", "...#...", "...#...", "......." };
        // 머리와 어깨(사이 한 줄은 외곽선이 채워 목처럼 보인다)
        static readonly string[] Person = { "..###..", ".#####.", ".#####.", "..###..", ".......", ".#####.", "#######" };
        static readonly string[] Slash = { ".......", "....#..", "...#...", ".......", "...#...", "..#....", "......." };
        static readonly Color32 HeartAlive = new Color32(224, 64, 74, 255), HeartShine = new Color32(255, 160, 160, 255);
        static readonly Color32 PersonAlive = new Color32(196, 212, 232, 255), PersonShine = new Color32(246, 250, 255, 255);
        static readonly Color32 Lost = new Color32(70, 64, 70, 255), LostMark = new Color32(22, 20, 26, 255);
        int lastLives = -1, lastPeople = -1;
        float breakUntil, peopleBreakUntil;

        static bool Blinking(float until) => Application.isPlaying && Time.unscaledTime < until && Time.unscaledTime % 0.3f < 0.15f;

        /// <summary>
        /// 아이콘 3개(오른쪽 맞춤, 바닥 높이 y0). 남은 만큼은 제 색, 잃은 것은 금 간 회색. 방금 잃은 것은 깜빡인다.
        /// 하트 = 목숨(흔적), 사람 = 실종을 버틸 수 있는 수.
        /// </summary>
        void DrawIcons(string[] shape, string[] mark, int left, int y0, bool blink, Color32 alive, Color32 shine)
        {
            int hw = shape[0].Length, hh = shape.Length;
            const int gap = 2, count = 3;
            int x0 = W - 2 - (count * hw + (count - 1) * gap);
            for (int i = 0; i < count; i++)
            {
                bool on = i < left || (blink && i == left);
                int hx = x0 + i * (hw + gap);
                // 외곽선
                for (int r = 0; r < hh; r++)
                    for (int c = 0; c < hw; c++)
                    {
                        if (shape[r][c] != '#') continue;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int nr = r + oy, nc = c + ox;
                                bool inside = nr >= 0 && nr < hh && nc >= 0 && nc < hw && shape[nr][nc] == '#';
                                if (!inside) pt.Put(hx + nc, y0 + (hh - 1 - nr), outline);
                            }
                    }
                // 채우기
                for (int r = 0; r < hh; r++)
                    for (int c = 0; c < hw; c++)
                    {
                        if (shape[r][c] != '#') continue;
                        Color32 col = on ? alive : Lost;
                        if (on && r == 1 && c == 2 - (shape == Heart ? 1 : 0)) col = shine;   // 반짝임
                        if (!on && mark[r][c] == '#') col = LostMark;                      // 금
                        pt.Put(hx + c, y0 + (hh - 1 - r), col);
                    }
            }
        }

        /// <summary>글자 하나를 s배로 찍는다. top은 글자 맨 윗줄의 바로 위 높이.</summary>
        static void Stamp(bool[] ink, char c, int x, int top, int s)
        {
            if (!Glyphs.TryGetValue(c, out var g)) return;
            for (int row = 0; row < g.Length; row++)
                for (int col = 0; col < g[row].Length; col++)
                    if (g[row][col] == '#') Block(ink, x + col * s, top - (row + 1) * s, s, s);
        }

        static void Block(bool[] ink, int x, int y, int w, int h)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    if (xx >= 0 && yy >= 0 && xx < W && yy < H) ink[yy * W + xx] = true;
        }
    }
}
