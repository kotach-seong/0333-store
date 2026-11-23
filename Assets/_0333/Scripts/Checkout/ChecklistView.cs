using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 야간 거래 점검표. 계산대 위(바코드 스캐너와 금전등록기 사이) 클립보드를 클릭하면 크게 펼쳐진다.
    ///  - 그날 밤 손님마다 한 줄씩: 번호, 시각, 판정(정상은 파란 펜, 비정상은 빨간 펜).
    ///  - 아래 "이 거래는 정상입니까?" — 영수증이 나온 뒤에만 예 / 아니오에 체크할 수 있다(몇 번이든 바꿀 수 있음).
    ///  - 맨 밑 "거래 종료하기"를 눌러야 판정이 정해진다. 그러면 잠깐 뒤 저절로 닫히고 손님이 나간다
    ///    (예: 문이 열리며 딸랑 / 아니오: 차임 없이 조용히 사라짐 — CheckoutSystem이 처리).
    /// </summary>
    public class ChecklistView : PaperPanel
    {
        [Header("점검표 모양")]
        public int sheetWidth = 300;
        [Tooltip("보이는 줄 수(하룻밤 손님 수)")]
        public int sheetRows = 10;
        public int rowPitch = 15;
        public Color32 sheetColor = new Color32(230, 233, 219, 255);
        public Color32 ruleColor = new Color32(156, 172, 163, 255);
        public Color32 boardColor = new Color32(112, 82, 56, 255);
        public Color32 penBlue = new Color32(44, 70, 140, 255);
        public Color32 penRed = new Color32(168, 52, 46, 255);
        public Color32 highlight = new Color32(244, 228, 160, 255);
        public Color32 endButton = new Color32(58, 74, 92, 255);
        public Color32 endText = new Color32(240, 236, 220, 255);
        [Tooltip("거래 종료하기를 누른 뒤 판정이 적히는 걸 보여 주고 닫히기까지(초)")]
        public float answerCloseDelay = 0.5f;

        /// <summary>"거래 종료하기"로 확정한 판정. 확정 전에는 None.</summary>
        public Verdict Answer { get; private set; }
        /// <summary>예/아니오 중 지금 체크해 둔 것(아직 확정 아님).</summary>
        public Verdict Selected { get; private set; }
        /// <summary>지금 예/아니오를 고를 수 있는지(영수증이 나온 뒤).</summary>
        public bool CanAnswer { get; private set; }

        IReadOnlyList<ChecklistEntry> entries;
        int day = 1, currentNumber = -1;
        int sheetX, sheetTop, sheetH, tableTop, currentRow = -1;
        RectInt yesBox, noBox, endBox;
        bool hoverYes, hoverNo, hoverEnd;

        /// <summary>"거래 종료하기" 버튼 자리(화면 가운데 기준 px, 열려 있을 때).</summary>
        public RectInt EndButtonRect => endBox;

        bool Choosing => CanAnswer && Answer == Verdict.None;
        bool CanEnd => Choosing && Selected != Verdict.None;

        /// <summary>그날 밤의 기록을 연결한다.</summary>
        public void Bind(IReadOnlyList<ChecklistEntry> list, int dayNumber)
        {
            entries = list;
            day = dayNumber;
            Refresh();
        }

        /// <summary>새 손님 계산이 시작됐을 때(아직 고를 수 없음).</summary>
        public void SetCurrent(int number)
        {
            currentNumber = number;
            CanAnswer = false;
            Answer = Selected = Verdict.None;
            Refresh();
        }

        /// <summary>영수증이 나왔을 때: 이제 예/아니오를 고를 수 있다.</summary>
        public void Ask()
        {
            CanAnswer = true;
            Answer = Selected = Verdict.None;
            Refresh();
        }

        /// <summary>판정이 기록에 들어간 뒤.</summary>
        public void EndQuestion()
        {
            CanAnswer = false;
            currentNumber = -1;
            Answer = Selected = Verdict.None;
            Refresh();
        }

        public void ShowPreview(IReadOnlyList<ChecklistEntry> list, int dayNumber, int number, bool canAnswer, Verdict selected, Verdict answer)
        {
            entries = list;
            day = dayNumber;
            currentNumber = number;
            CanAnswer = canAnswer;
            Selected = selected;
            Answer = answer;
            ShowForPreview();
        }

        void Refresh()
        {
            if (IsOpen) Render();
        }

        protected override void OnOpen() => hoverYes = hoverNo = hoverEnd = false;

        static bool Inside(RectInt r, Vector2 p) => new Rect(r.x, r.y, r.width, r.height).Contains(p);

        protected override bool Pointer(Vector2 p, bool click)
        {
            bool overYes = Choosing && Inside(yesBox, p);
            bool overNo = Choosing && Inside(noBox, p);
            bool overEnd = CanEnd && Inside(endBox, p);
            bool changed = overYes != hoverYes || overNo != hoverNo || overEnd != hoverEnd;
            hoverYes = overYes;
            hoverNo = overNo;
            hoverEnd = overEnd;
            if (!click) return changed;

            if (overYes || overNo)
            {
                // 체크만 해 둔다(바꿀 수 있음). 정해지는 건 "거래 종료하기"를 눌렀을 때
                Selected = overYes ? Verdict.Normal : Verdict.Abnormal;
                SoundFx.Play(sfx, SoundFx.Tick, 0.9f);
                Render();
                return false;
            }
            if (overEnd)
            {
                Answer = Selected;
                hoverYes = hoverNo = hoverEnd = false;
                SoundFx.Play(sfx, SoundFx.Thunk, 0.9f);
                CloseAfter(answerCloseDelay);
                Render();
                return false;
            }
            return changed;
        }

        protected override void Layout()
        {
            sheetX = -sheetWidth / 2;
            sheetH = 24 + 16 + 4 + 13 + sheetRows * rowPitch + 12 + 15 + 8 + 22 + 9 + 22 + 12;
            sheetTop = sheetH / 2 - 6;
            int y = sheetTop - 24;
            closeRect = new RectInt(sheetX - 7 - 26, sheetTop + 9 - 18, 18, 18);

            Add("야간 거래 점검표", sheetX + 14, y - 15, 180, 15, TextAnchor.LowerLeft, inkColor, true);
            Add($"DAY {day}", sheetX + 14, y - 15, sheetWidth - 28, 15, TextAnchor.LowerRight, softInk);
            y -= 16 + 4;

            int cNo = sheetX + 18, cTime = sheetX + 84, cVerdict = sheetX + 152;
            Add("No.", cNo, y - 12, 60, 12, TextAnchor.LowerLeft, softInk);
            Add("시각", cTime, y - 12, 60, 12, TextAnchor.LowerLeft, softInk);
            Add("판정", cVerdict, y - 12, 120, 12, TextAnchor.LowerLeft, softInk);
            y -= 13;
            tableTop = y;

            // 그날 밤 기록: 줄이 넘치면 최근 것만
            currentRow = -1;
            int count = entries != null ? entries.Count : 0;
            int first = Mathf.Max(0, count - sheetRows);
            for (int i = first; i < count; i++)
            {
                ChecklistEntry e = entries[i];
                int row = i - first;
                int bottom = tableTop - (row + 1) * rowPitch + 3;
                bool isCurrent = e.number == currentNumber;
                if (isCurrent) currentRow = row;
                Verdict v = isCurrent ? Answer : e.verdict;
                Add($"{e.number:0000}", cNo, bottom, 60, 12, TextAnchor.LowerLeft, inkColor);
                Add(e.time, cTime, bottom, 60, 12, TextAnchor.LowerLeft, inkColor);
                if (v != Verdict.None)
                    Add(v == Verdict.Normal ? "정상" : "비정상", cVerdict, bottom, 120, 12, TextAnchor.LowerLeft, v == Verdict.Normal ? penBlue : penRed);
                else if (isCurrent)
                    Add(CanAnswer ? "판정 기다리는 중" : "계산 중", cVerdict, bottom, 120, 12, TextAnchor.LowerLeft, softInk);
            }
            y = tableTop - sheetRows * rowPitch - 12;

            bool active = CanAnswer;
            Add("이 거래는 정상입니까?", sheetX, y - 15, sheetWidth, 15, TextAnchor.LowerCenter, active ? inkColor : softInk, true);
            y -= 15 + 8;

            int mid = sheetX + sheetWidth / 2;
            yesBox = new RectInt(mid - 92, y - 22, 80, 22);
            noBox = new RectInt(mid + 12, y - 22, 80, 22);
            Color32 label = active ? inkColor : softInk;
            Add("예", yesBox.x + 24, yesBox.y + 5, 52, 13, TextAnchor.LowerLeft, label);
            Add("아니오", noBox.x + 24, noBox.y + 5, 52, 13, TextAnchor.LowerLeft, label);
            y -= 22 + 9;

            // 맨 밑: 거래 종료하기(예/아니오를 체크해야 눌린다)
            endBox = new RectInt(mid - 70, y - 22, 140, 22);
            Add("거래 종료하기", endBox.x, endBox.y + 4, endBox.width, 15, TextAnchor.LowerCenter, CanEnd ? endText : softInk, true);
        }

        protected override void DrawContent(int ox, int oy)
        {
            int x0 = sheetX + ox, x1 = sheetX + sheetWidth + ox;
            int top = sheetTop + oy, bottom = sheetTop - sheetH + oy;

            // 클립보드 판(그림자 → 판 → 테두리)
            pt.Fill(x0 - 4, bottom - 10, x1 + 10, top + 6, new Color32(0, 0, 0, 110));
            for (int y = bottom - 7; y < top + 9; y++)
                for (int x = x0 - 7; x < x1 + 7; x++)
                    pt.Put(x, y, PixelTex.Mul(boardColor, 0.94f + PixelTex.Noise(x / 3, y, 57) * 0.1f));
            pt.Outline(x0 - 7, bottom - 7, x1 + 7, top + 9, PixelTex.Mul(boardColor, 0.55f));

            // 종이
            for (int y = bottom; y < top; y++)
                for (int x = x0; x < x1; x++)
                {
                    int n = Mathf.RoundToInt((PixelTex.Noise(x, y, 77) - 0.5f) * 5f);
                    pt.Put(x, y, new Color32((byte)(sheetColor.r + n), (byte)(sheetColor.g + n), (byte)(sheetColor.b + n), 255));
                }
            pt.Fill(x0, bottom, x1, bottom + 1, PixelTex.Mul(sheetColor, 0.86f));
            pt.Fill(x1 - 1, bottom, x1, top, PixelTex.Mul(sheetColor, 0.9f));

            // 금속 집게
            int mid = (x0 + x1) / 2;
            Color32 metal = PixelTex.Hex("#A9AFAD"), metalDark = PixelTex.Hex("#5E6564"), metalLight = PixelTex.Hex("#DCE0DC");
            pt.Fill(mid - 34, top - 9, mid + 34, top + 6, metalDark);
            pt.Fill(mid - 33, top - 8, mid + 33, top + 5, metal);
            pt.Fill(mid - 33, top + 4, mid + 33, top + 5, metalLight);
            pt.Fill(mid - 12, top + 5, mid + 12, top + 12, metalDark);
            pt.Fill(mid - 11, top + 6, mid + 11, top + 11, metal);
            pt.Fill(mid - 5, top + 7, mid + 5, top + 10, metalDark);

            // 표
            int tTop = tableTop + oy, tBottom = tTop - sheetRows * rowPitch;
            int lx0 = x0 + 10, lx1 = x1 - 10;
            if (currentRow >= 0)
                pt.Fill(lx0, tTop - (currentRow + 1) * rowPitch + 1, lx1, tTop - currentRow * rowPitch, PixelTex.Alpha(highlight, 0.75f));
            pt.Fill(lx0, tTop, lx1, tTop + 1, PixelTex.Mul(ruleColor, 0.8f));
            for (int r = 1; r <= sheetRows; r++) pt.Fill(lx0, tTop - r * rowPitch, lx1, tTop - r * rowPitch + 1, ruleColor);
            foreach (int cx in new[] { sheetX + 74, sheetX + 142 })
                pt.Fill(cx + ox, tBottom, cx + ox + 1, tTop + 14, ruleColor);

            Verdict mark = Answer != Verdict.None ? Answer : Selected;
            DrawAnswerBox(yesBox, ox, oy, hoverYes, mark == Verdict.Normal, penBlue);
            DrawAnswerBox(noBox, ox, oy, hoverNo, mark == Verdict.Abnormal, penRed);

            // 거래 종료하기 버튼
            var e = new RectInt(endBox.x + ox, endBox.y + oy, endBox.width, endBox.height);
            bool on = CanEnd;
            Color32 fill = on ? (hoverEnd ? PixelTex.Mul(endButton, 1.35f) : endButton) : PixelTex.Mul(sheetColor, 0.9f);
            pt.Fill(e.x + 1, e.y - 2, e.xMax + 1, e.yMax - 2, new Color32(0, 0, 0, on ? (byte)90 : (byte)40)); // 그림자
            pt.Fill(e.x, e.y, e.xMax, e.yMax, fill);
            pt.Outline(e.x, e.y, e.xMax, e.yMax, on ? PixelTex.Mul(endButton, 0.6f) : PixelTex.Mul(ruleColor, 0.95f));
            if (on) pt.Fill(e.x + 1, e.yMax - 2, e.xMax - 1, e.yMax - 1, PixelTex.Mul(endButton, 1.6f));      // 윗면 빛
        }

        void DrawAnswerBox(RectInt r, int ox, int oy, bool hover, bool chosen, Color32 pen)
        {
            var b = new RectInt(r.x + ox, r.y + oy, r.width, r.height);
            bool active = CanAnswer;
            Color32 fill = hover ? highlight : PixelTex.Mul(sheetColor, active ? 1.04f : 0.96f);
            pt.Fill(b.x, b.y, b.xMax, b.yMax, fill);
            pt.Outline(b.x, b.y, b.xMax, b.yMax, hover ? inkColor : PixelTex.Mul(ruleColor, active ? 0.75f : 0.95f));
            // 왼쪽 체크 칸
            int bx = b.x + 7, by = b.y + 6;
            pt.Fill(bx, by, bx + 10, by + 10, PixelTex.Mul(sheetColor, 1.05f));
            pt.Outline(bx, by, bx + 10, by + 10, active ? inkColor : softInk);
            if (chosen) DrawPenCheck(bx, by, pen);
        }
    }
}
