using System.Globalization;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 금전등록기 맨 위의 주황 LED 표시창. 물건을 찍을 때마다 합계가 올라간다.
    /// 매장 그림에 구워진 "0.00" 위에 같은 크기의 그림을 덮어 숫자를 바꿔 보여 준다.
    /// </summary>
    public class RegisterDisplay : MonoBehaviour
    {
        public SpriteRenderer target;
        [Tooltip("표시창 크기(px). 씬을 만들 때 매장 그림에 맞춰 정해진다")]
        public Vector2Int size = new Vector2Int(43, 20);
        [Tooltip("숫자 한 칸(3x5 도트)을 몇 배로 키울지")]
        public int scale = 2;

        [Header("색(씬을 만들 때 매장 그림에서 뽑아 온다)")]
        public Color32 lit = new Color32(255, 138, 61, 255);
        public Color32 background = new Color32(26, 9, 6, 255);
        public Color32 bezel = new Color32(10, 6, 6, 255);

        public int Shown { get; private set; } = int.MinValue;

        PixelTex pt;

        // 3x5 숫자(위에서부터 다섯 줄)
        static readonly string[] Glyphs =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        void Awake() => Show(0);

        /// <summary>금액(원)을 보여 준다. 세 자리마다 쉼표.</summary>
        public void Show(int amount)
        {
            if (target == null) return;
            if (pt == null || pt.W != size.x || pt.H != size.y) pt = new PixelTex(size.x, size.y);

            Color32 dim = PixelTex.Mul(background, 1.35f);
            pt.Clear(background);
            for (int y = 1; y < pt.H - 1; y += 2) pt.Fill(1, y, pt.W - 1, y + 1, dim); // LED 유리의 가는 줄
            pt.Outline(0, 0, pt.W, pt.H, bezel);

            string text = amount.ToString("N0", CultureInfo.InvariantCulture);
            int adv = 3 * scale + 1, comma = scale + 1;
            int width = -1;
            foreach (char ch in text) width += ch == ',' ? comma : adv;
            int x = pt.W - 3 - width;
            int top = (pt.H + 5 * scale) / 2 - 1; // 숫자 맨 윗줄
            Color32 glow = PixelTex.Alpha(lit, 0.28f);
            foreach (char ch in text)
            {
                if (ch == ',')
                {
                    // 쉼표: 숫자 아래쪽에 짧게
                    pt.Fill(x, top - 5 * scale + 1 - scale, x + scale, top - 4 * scale + 1, lit);
                    x += comma;
                    continue;
                }
                int d = ch - '0';
                if (d < 0 || d > 9) { x += adv; continue; }
                string g = Glyphs[d];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if (g[row * 3 + col] != '1') continue;
                        int px = x + col * scale, py = top - (row + 1) * scale + 1;
                        pt.Fill(px - 1, py - 1, px + scale + 1, py + scale + 1, glow); // 은은한 번짐
                    }
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if (g[row * 3 + col] == '1')
                        {
                            int px = x + col * scale, py = top - (row + 1) * scale + 1;
                            pt.Fill(px, py, px + scale, py + scale, lit);
                        }
                x += adv;
            }

            target.sprite = pt.ToSprite(Vector2.zero);
            target.enabled = true;
            Shown = amount;
        }
    }
}
