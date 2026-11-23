using System.Collections.Generic;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 수첩을 크게 펼친 화면의 그림(종이, 스프링, 버튼). 글자는 그림에 넣지 않고
    /// 게임에서 갈무리 글꼴로 그 위에 쓴다. 좌표는 화면 중심이 (0,0)인 픽셀.
    /// </summary>
    public static class NotebookArt
    {
        public const string PaperName = "note_paper";
        public const string PrevName = "note_btn_prev";
        public const string NextName = "note_btn_next";
        public const string PrevHoverName = "note_btn_prev_hover";
        public const string NextHoverName = "note_btn_next_hover";
        public const string CloseHoverName = "note_btn_close_hover";

        // 펼친 수첩: 왼쪽 쪽과 오른쪽 쪽
        public static readonly RectInt LeftPage = new RectInt(-262, -146, 254, 290);
        public static readonly RectInt RightPage = new RectInt(8, -146, 254, 290);
        /// <summary>줄 간격(px). 본문 한 줄이 줄 하나에 맞게 놓인다.</summary>
        public const int LinePitch = 14;
        /// <summary>제목 아래 첫 줄의 높이. 여기서 LinePitch씩 내려가며 줄이 그어진다.</summary>
        public const int FirstLineY = 100;
        public const int LineCount = 17;
        /// <summary>쪽 안쪽 여백(빨간 여백선 오른쪽부터 글을 쓴다).</summary>
        public const int MarginX = 34;

        public static readonly RectInt CloseRect = new RectInt(-256, 120, 18, 18);
        public static readonly RectInt PrevRect = new RectInt(-270, -174, 40, 20);
        public static readonly RectInt NextRect = new RectInt(230, -174, 40, 20);

        static readonly Color32 Paper = Pal.Hex("#ECE6CC");
        static readonly Color32 Rule = Pal.Hex("#C9BF9F");
        static readonly Color32 Margin = Pal.Hex("#D98E87");
        static readonly Color32 Cover = Pal.Hex("#7A5A40");
        static readonly Color32 CoverDark = Pal.Hex("#4A3628");
        static readonly Color32 Ring = Pal.Hex("#939C91");
        static readonly Color32 RingDark = Pal.Hex("#465254");

        public static List<StoreArtGenerator.Layer> Generate()
        {
            var list = new List<StoreArtGenerator.Layer>();
            int w = StoreArtGenerator.ViewW, h = StoreArtGenerator.ViewH;
            var G = StoreArtGenerator.Group.Ui;

            var paper = new StoreArtGenerator.Layer(PaperName, new RectInt(-w / 2, -h / 2, w, h), 200, G);
            PixelCanvas c = paper.Canvas;
            c.Fill(-w / 2, -h / 2, w / 2, h / 2, new Color32(4, 6, 10, 205));                     // 뒤 어둡게

            // 표지(쪽보다 조금 크게)와 그림자
            c.Fill(LeftPage.xMin - 6, LeftPage.yMin - 10, RightPage.xMax + 10, LeftPage.yMax + 2, new Color32(0, 0, 0, 120));
            for (int y = LeftPage.yMin - 6; y < LeftPage.yMax + 6; y++)
                for (int x = LeftPage.xMin - 6; x < RightPage.xMax + 6; x++)
                    c.Put(x, y, Pal.Add(Cover, Mathf.RoundToInt((Pal.Noise(x, y, 401) - 0.5f) * 8f)));
            c.Outline(LeftPage.xMin - 6, LeftPage.yMin - 6, RightPage.xMax + 6, LeftPage.yMax + 6, CoverDark);

            DrawPage(c, LeftPage, true);
            DrawPage(c, RightPage, false);

            // 가운데 스프링
            c.Fill(-8, LeftPage.yMin, 8, LeftPage.yMax, CoverDark);
            for (int y = LeftPage.yMax - 10; y > LeftPage.yMin + 4; y -= 12)
            {
                c.Fill(-11, y - 2, 11, y + 2, RingDark);
                c.Fill(-10, y - 1, 10, y + 1, Ring);
                c.Put(-6, y, Pal.Hex("#CBCDB8"));
            }

            StoreArtGenerator.DrawCloseButton(c, CloseRect, Pal.Hex("#A8433B"), Pal.Hex("#6B2A2A"), Pal.Hex("#F3F1DF"));
            list.Add(paper);

            list.Add(Button(PrevName, PrevRect, true, false, 201));
            list.Add(Button(NextName, NextRect, false, false, 201));
            list.Add(Button(PrevHoverName, PrevRect, true, true, 202));
            list.Add(Button(NextHoverName, NextRect, false, true, 202));

            var close = new StoreArtGenerator.Layer(CloseHoverName, CloseRect, 202, G);
            StoreArtGenerator.DrawCloseButton(close.Canvas, CloseRect, Pal.Hex("#E5372E"), Pal.Hex("#F3F1DF"), Pal.Hex("#FFFBE8"));
            list.Add(close);
            return list;
        }

        static void DrawPage(PixelCanvas c, RectInt r, bool left)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    // 스프링 쪽으로 갈수록 살짝 어둡게(종이가 휘어 들어가는 느낌)
                    int toSpine = left ? r.xMax - x : x - r.xMin;
                    float shade = toSpine < 14 ? Mathf.Lerp(0.86f, 1f, toSpine / 14f) : 1f;
                    Color32 col = Pal.Mul(Paper, shade);
                    col = Pal.Add(col, Mathf.RoundToInt((Pal.Noise(x, y, 402) - 0.5f) * 5f));
                    c.Put(x, y, col);
                }
            for (int i = 0; i < LineCount; i++)
            {
                int y = FirstLineY - i * LinePitch;
                c.Fill(r.xMin + 6, y, r.xMax - 6, y + 1, Rule);
            }
            int mx = r.xMin + MarginX - 6;
            c.Fill(mx, r.yMin, mx + 1, r.yMax, Margin);
            // 종이 가장자리 그늘
            c.Outline(r.xMin, r.yMin, r.xMax, r.yMax, Pal.Hex("#CFC6AE"));
        }

        static StoreArtGenerator.Layer Button(string name, RectInt r, bool left, bool hover, int order)
        {
            var l = new StoreArtGenerator.Layer(name, r, order, StoreArtGenerator.Group.Ui);
            if (hover) StoreArtGenerator.DrawButton(l.Canvas, r, left, Pal.Hex("#465254"), Pal.Hex("#939C91"), Pal.Hex("#F3F1DF"));
            else StoreArtGenerator.DrawButton(l.Canvas, r, left, Pal.Hex("#333D42"), Pal.Hex("#465254"), Pal.Hex("#B0B6A5"));
            return l;
        }

        /// <summary>쪽 안에서 i번째 줄(0부터)의 아래쪽 y. 글자는 이 줄 위에 앉는다.</summary>
        public static int LineY(int i) => FirstLineY - i * LinePitch;
    }
}
