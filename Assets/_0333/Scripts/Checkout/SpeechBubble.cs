using System;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 손님 머리 위 말풍선. 글자가 한 자씩 찍히고, 다 읽을 시간이 지나면 사라진다.
    ///  - 카메라의 자식이라 패널(CCTV·영수증·점검표)보다 위, 시계(HUD)보다 아래에 그린다.
    ///  - 손님이 화면 밖에 있으면(고개를 돌렸거나 CCTV를 보는 중) 화면 가장자리에 붙어서 보이고, 꼬리는 숨긴다.
    /// </summary>
    [DefaultExecutionOrder(-70)]   // 대답 버튼(-60)이 말풍선 자리를 읽기 전에 자리를 잡는다
    public class SpeechBubble : MonoBehaviour
    {
        [Tooltip("말풍선 그림(카메라의 자식, z=5)")]
        public SpriteRenderer box;
        [Tooltip("말풍선 글자(화면 크기 캔버스 안)")]
        public Text label;
        public int maxTextWidth = 230;
        public float charsPerSecond = 30f;
        public Color32 fill = new Color32(246, 241, 226, 255);
        public Color32 edge = new Color32(26, 22, 30, 255);
        public Color32 shade = new Color32(205, 196, 176, 255);
        public AudioSource sfx;

        const int PadX = 6, PadY = 4, Tail = 7, ScreenW = 640, ScreenH = 360, Margin = 4, ZalgoPad = 7;
        public Color32 zalgoInk = new Color32(40, 32, 44, 255);

        bool glitch;                                       // 들킨 손님의 말: 글자 위아래로 꿈틀거리는 획(잘고)
        float nextMarks;
        readonly System.Collections.Generic.List<Vector3Int> marks = new System.Collections.Generic.List<Vector3Int>();   // x, y, 길이(+위 / -아래)
        int PadTop => glitch ? PadY + ZalgoPad : PadY;

        Func<Vector3> anchor;
        string full = "";
        float typed, hideAt;
        int textW, textH, boxW, boxH;
        PixelTex pt;
        int lastTailX = int.MinValue;
        Transform cam;

        public bool Showing { get; private set; }
        /// <summary>글자가 아직 찍히는 중.</summary>
        public bool Typing => Showing && typed < full.Length;
        public string Current => Showing ? full : null;
        /// <summary>말풍선 몸통(꼬리 제외)의 화면 자리(가운데 기준 px).</summary>
        public RectInt ScreenRect { get; private set; }

        void Awake()
        {
            cam = box != null && box.transform.parent != null ? box.transform.parent : transform;
            HideNow();
        }

        /// <summary>head(월드 좌표, 손님 머리 꼭대기) 위에 line을 띄운다. hold는 다 찍힌 뒤 남아 있는 시간(음수면 길이에 맞춰).</summary>
        public void Say(Func<Vector3> head, string line, float hold = -1f, bool zalgo = false)
        {
            if (box == null || label == null || string.IsNullOrEmpty(line)) return;
            if (cam == null) cam = box.transform.parent != null ? box.transform.parent : transform;
            anchor = head;
            glitch = zalgo;
            full = Loc.T(line);   // 코드에 적힌 말(대사 파일 줄은 이미 그 언어)
            typed = Application.isPlaying ? 0f : full.Length;
            if (hold < 0f) hold = Mathf.Clamp(1.4f + full.Length * 0.07f, 2.2f, 5f);
            hideAt = (Application.isPlaying ? Time.time : 0f) + full.Length / charsPerSecond + hold;
            Measure();
            if (glitch) MakeMarks();
            lastTailX = int.MinValue;
            Showing = true;
            box.enabled = true;
            label.enabled = true;
            label.text = Application.isPlaying ? "" : full;
            Place();
        }

        public void Hide() => HideNow();

        void HideNow()
        {
            Showing = false;
            anchor = null;
            if (box != null) box.enabled = false;
            if (label != null) { label.enabled = false; label.text = ""; }
        }

        void LateUpdate()
        {
            if (!Showing || !Application.isPlaying) return;
            if (Time.time >= hideAt) { HideNow(); return; }
            if (typed < full.Length)
            {
                int before = Mathf.FloorToInt(typed);
                typed += Time.deltaTime * charsPerSecond;
                int now = Mathf.Min(full.Length, Mathf.FloorToInt(typed));
                if (now != before)
                {
                    label.text = full.Substring(0, now);
                    if (now % 3 == 0 && full[now - 1] != ' ') SoundFx.Play(sfx, SoundFx.Blip, 0.25f);
                }
            }
            if (glitch && Time.time >= nextMarks)
            {
                // 잘고 획이 꿈틀거린다(찍힌 글자에만)
                nextMarks = Time.time + 0.09f;
                MakeMarks();
                lastTailX = int.MinValue;   // 다시 그리게
            }
            Place();
        }

        /// <summary>찍힌 글자마다 위로 솟는 획 1~3개, 아래로 처지는 획 0~2개. 첫 줄은 위로 길게, 끝 줄은 아래로 길게.</summary>
        void MakeMarks()
        {
            marks.Clear();
            Font f = label.font;
            if (f == null) return;
            int size = label.fontSize;
            f.RequestCharactersInTexture(full, size);
            var rnd = Application.isPlaying ? new System.Random(UnityEngine.Random.Range(0, int.MaxValue)) : new System.Random(11);
            int shown = Application.isPlaying ? Mathf.FloorToInt(typed) : full.Length;
            string[] rows = full.Split('\n');
            int lineH = Mathf.Max(size, Mathf.RoundToInt(textH / (float)Mathf.Max(1, rows.Length)));
            int idx = 0;
            for (int l = 0; l < rows.Length; l++)
            {
                int x = PadX;
                int top = Tail + PadTop + textH - l * lineH;            // 이 줄 글자의 맨 위
                foreach (char c in rows[l])
                {
                    if (idx >= shown) return;
                    int adv = f.GetCharacterInfo(c, out CharacterInfo ci, size) && ci.advance > 0 ? ci.advance : size;
                    if (!char.IsWhiteSpace(c))
                    {
                        int up = rnd.Next(1, 4), down = rnd.Next(0, 3);
                        for (int k = 0; k < up; k++)
                            marks.Add(new Vector3Int(x + rnd.Next(adv), top, rnd.Next(2, l == 0 ? ZalgoPad + 1 : 3)));
                        for (int k = 0; k < down; k++)
                            marks.Add(new Vector3Int(x + rnd.Next(adv), top - size - 1, -rnd.Next(2, l == rows.Length - 1 ? ZalgoPad : 3)));
                    }
                    x += adv;
                    idx++;
                }
                idx++;   // 줄바꿈
            }
        }

        /// <summary>글자 크기를 재서 말풍선 크기를 정한다(다 찍힌 글자 기준이라 찍히는 동안 크기가 변하지 않는다).</summary>
        void Measure()
        {
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = full;
            float w = label.preferredWidth;
            var rt = label.rectTransform;
            if (w > maxTextWidth)
            {
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                rt.sizeDelta = new Vector2(maxTextWidth, rt.sizeDelta.y);
                w = maxTextWidth;
            }
            else rt.sizeDelta = new Vector2(Mathf.Ceil(w) + 1f, rt.sizeDelta.y);
            float h = label.preferredHeight;
            textW = Mathf.CeilToInt(w);
            textH = Mathf.CeilToInt(h);
            rt.sizeDelta = new Vector2(textW + 1, textH);
            boxW = textW + PadX * 2;
            boxH = textH + PadTop * 2;
        }

        /// <summary>머리 위(화면 밖이면 가장자리)에 놓는다. 픽셀 단위로 맞춘다.</summary>
        void Place()
        {
            Vector3 head = anchor != null ? anchor() : cam.position;
            Vector3 rel = head - cam.position;                 // 1 유닛 = 1 화면 픽셀(가운데 기준)
            float hx = Mathf.Round(rel.x), hy = Mathf.Round(rel.y);
            int left = Mathf.RoundToInt(hx - boxW * 0.5f);
            int bottom = Mathf.RoundToInt(hy + 3f) + Tail;       // 꼬리 끝이 머리 위 3픽셀
            int minX = -ScreenW / 2 + Margin, maxX = ScreenW / 2 - Margin - boxW;
            int minY = -ScreenH / 2 + Margin + Tail, maxY = ScreenH / 2 - Margin - boxH - 30;   // 위쪽은 시계 자리를 피한다
            bool offscreen = hx < -ScreenW / 2 || hx > ScreenW / 2 || hy > ScreenH / 2 || hy < -ScreenH / 2;
            left = Mathf.Clamp(left, minX, Mathf.Max(minX, maxX));
            bottom = Mathf.Clamp(bottom, minY, Mathf.Max(minY, maxY));
            int tailX = offscreen ? -1 : Mathf.Clamp(Mathf.RoundToInt(hx) - left, 5, boxW - 6);
            if (pt == null || pt.W != boxW || pt.H != boxH + Tail || tailX != lastTailX) Draw(tailX);
            box.transform.localPosition = new Vector3(left, bottom - Tail, box.transform.localPosition.z);
            ScreenRect = new RectInt(left, bottom, boxW, boxH);
            var rt = label.rectTransform;
            rt.anchoredPosition = new Vector2(left + PadX, bottom + PadTop);
        }

        void Draw(int tailX)
        {
            lastTailX = tailX;
            int w = boxW, h = boxH + Tail;
            if (pt == null || pt.W != w || pt.H != h) pt = new PixelTex(w, h);
            pt.Clear();
            // 몸통(모서리 1픽셀 깎음) + 아래쪽 그림자 한 줄
            for (int y = Tail; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == Tail || y == h - 1);
                    if (corner) continue;
                    bool border = x == 0 || x == w - 1 || y == Tail || y == h - 1;
                    pt.Put(x, y, border ? edge : y == Tail + 1 ? shade : fill);
                }
            // 꼬리: 몸통에서 머리 쪽으로 뾰족하게(위는 넓고 아래로 갈수록 좁다)
            if (tailX >= 0)
            {
                for (int i = 0; i < Tail; i++)
                {
                    int y = Tail - 1 - i;                       // 몸통 바로 아래부터 끝까지
                    if (i == Tail - 1) { pt.Put(tailX, y, edge); break; }
                    int half = (Tail - 2 - i) * 3 / 4;
                    for (int x = tailX - half - 1; x <= tailX + half + 1; x++)
                        pt.Put(x, y, x == tailX - half - 1 || x == tailX + half + 1 ? edge : fill);
                }
                int open = (Tail - 2) * 3 / 4;                 // 몸통과 꼬리 사이 테두리를 터서 이어 준다
                for (int x = tailX - open; x <= tailX + open; x++) { pt.Put(x, Tail, fill); pt.Put(x, Tail + 1, fill); }
            }
            // 잘고: 획마다 한 픽셀씩 좌우로 비틀리며 솟거나 처진다(몸통 안쪽에만)
            if (glitch)
                foreach (Vector3Int m in marks)
                {
                    int len = Mathf.Abs(m.z), dir = m.z > 0 ? 1 : -1;
                    for (int j = 0; j < len; j++)
                    {
                        int px = m.x + ((m.x * 7 + j * 5) % 3) - 1;
                        int py = m.y + dir * (j + 1);
                        if (px > 0 && px < w - 1 && py > Tail + 1 && py < h - 1) pt.Put(px, py, zalgoInk);
                    }
                }
            box.sprite = pt.ToSprite(Vector2.zero);
        }
    }
}
