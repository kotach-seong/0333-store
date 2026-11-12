using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 픽셀아트용 고정 팔레트. 렌더링한 색을 가장 가까운 팔레트 색으로 바꾸고,
    /// 두 색의 경계 부근에서만 격자(Bayer) 디더링을 넣는다.
    /// 밝은 쪽은 미색, 어두운 쪽은 청록 기운이 도는 회색으로 흐르게 만들어
    /// "밝지만 어딘가 차가운" 형광등 매장 색감을 낸다.
    /// </summary>
    public static class PixelPalette
    {
        static readonly string[] Hexes =
        {
            // 중립(벽·천장·바닥): 미색 → 청록빛 어둠
            "#F3F1DF", "#E2E1CC", "#CBCDB8", "#B0B6A5", "#939C91", "#77837D",
            "#5D6A68", "#465254", "#333D42", "#232A30", "#161B21", "#0C0F14",
            // 따뜻한 베이지(계산대 상판, 낡은 플라스틱)
            "#E0D6B8", "#C7BC9C", "#A89C80", "#857A66", "#625A4D", "#403B35",
            // 밤(창밖)
            "#04060A", "#080C14", "#0E1522", "#172134", "#23314A", "#34476A",
            // 가로등
            "#6B5A2E", "#C9A24A", "#F1D98A",
            // 상품·간판 색(어두움/중간/밝음)
            "#6B2A2A", "#A8433B", "#D06A58",
            "#8A5524", "#C8843A", "#E6B062",
            "#9A8434", "#CDB651", "#E8DA8C",
            "#2F5A3C", "#55895A", "#8DB884",
            "#235B5A", "#3F8C86", "#7CBDB2",
            "#26426B", "#4470A6", "#7FA6CF",
            "#4B3A66", "#775C97", "#A893C0",
            "#8E4F68", "#C27C93", "#E3B1BC",
            "#4A3628", "#7A5A40", "#A98866",
            // 빛나는 것들(형광등, LED, 화면)
            "#FFFBE8", "#FF7A33", "#E5372E", "#3FE07A", "#1F9A55", "#8FD4FF", "#2E6FB0",
            "#C8F4E6", "#FFB45A",
        };

        static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static readonly Vector3 W = new Vector3(0.55f, 0.77f, 0.33f); // sqrt(0.30, 0.59, 0.11)

        static Vector3[] cols;
        static Color32[] c32;

        static void Init()
        {
            if (cols != null) return;
            cols = new Vector3[Hexes.Length];
            c32 = new Color32[Hexes.Length];
            for (int i = 0; i < Hexes.Length; i++)
            {
                c32[i] = Pal.Hex(Hexes[i]);
                c32[i].a = 255;
                cols[i] = Vector3.Scale(new Vector3(c32[i].r, c32[i].g, c32[i].b) / 255f, W);
            }
        }

        /// <param name="band">디더링 구간 폭(0이면 디더링 없음, 0.5면 전 구간).</param>
        public static Color32 Quantize(Vector3 c, int x, int y, float band = 0.12f)
        {
            Init();
            c = Vector3.Scale(new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z)), W);

            int b1 = 0, b2 = -1;
            float d1 = float.MaxValue, d2 = float.MaxValue;
            for (int i = 0; i < cols.Length; i++)
            {
                float d = (c - cols[i]).sqrMagnitude;
                if (d < d1) { d2 = d1; b2 = b1; d1 = d; b1 = i; }
                else if (d < d2) { d2 = d; b2 = i; }
            }

            if (band > 0f && b2 >= 0)
            {
                Vector3 ab = cols[b2] - cols[b1];
                float len = ab.sqrMagnitude;
                if (len > 1e-6f)
                {
                    float t = Vector3.Dot(c - cols[b1], ab) / len; // 0(가까운 색) ~ 0.5(중간)
                    float lo = 0.5f - band;
                    if (t > lo)
                    {
                        float p = (t - lo) / (2f * band);
                        float th = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                        if (th < p) return c32[b2];
                    }
                }
            }
            return c32[b1];
        }
    }
}
