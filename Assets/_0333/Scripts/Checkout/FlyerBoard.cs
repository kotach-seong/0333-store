using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 왼쪽 유리창에 붙는 실종자 전단. 경찰이 다녀갈 때마다 한 장씩 늘어난다.
    /// 전단에는 내가 "아니오"로 돌려보내 사라진 그 손님의 얼굴(복사기로 뽑은 듯한 흑백)이 찍혀 있다.
    /// </summary>
    public class FlyerBoard : MonoBehaviour
    {
        [Tooltip("전단을 붙일 창문(화면 좌표). 씬을 만들 때 정해진다")]
        public Rect window = new Rect(-106f, -31f, 62f, 74f);
        public Material material;
        [Tooltip("실내(10)·유리(11) 위, 손님(40) 아래")]
        public int sortingOrder = 12;

        const int W = 16, H = 21;
        readonly List<SpriteRenderer> shown = new List<SpriteRenderer>();

        public void Show(IList<CustomerView> faces)
        {
            foreach (var s in shown)
                if (s != null) { if (Application.isPlaying) Destroy(s.gameObject); else DestroyImmediate(s.gameObject); }
            shown.Clear();
            for (int i = 0; i < faces.Count && i < 3; i++)
            {
                var go = new GameObject($"실종 전단 {i + 1}");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false);
                float x = window.xMin + 5f + i * 19f, y = window.center.y - 6f + (i % 2 == 0 ? 0f : 4f);
                go.transform.position = new Vector3(Mathf.Round(x), Mathf.Round(y), 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Draw(faces[i], i);
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = sortingOrder;
                shown.Add(sr);
            }
        }

        static Sprite Draw(CustomerView face, int seed)
        {
            var pt = new PixelTex(W, H);
            Color32 paper = new Color32(226, 220, 200, 255), ink = new Color32(60, 58, 62, 255), red = new Color32(176, 44, 40, 255);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int n = Mathf.RoundToInt((PixelTex.Noise(x, y, 51 + seed) - 0.5f) * 10f);
                    pt.Put(x, y, new Color32((byte)(paper.r + n), (byte)(paper.g + n), (byte)(paper.b + n), 255));
                }
            // 위: 빨간 "실종" 띠, 아래: 글줄
            pt.Fill(1, H - 4, W - 1, H - 1, red);
            for (int x = 3; x < W - 3; x += 2) pt.Put(x, H - 3, new Color32(246, 236, 226, 255));
            for (int r = 0; r < 2; r++)
                for (int x = 2; x < W - 2; x++)
                    if (PixelTex.Noise(x, r, 77 + seed) > 0.3f) pt.Put(x, 2 + r * 2, ink);
            // 가운데: 사라진 손님 얼굴(12x10, 흑백 복사본)
            DrawFace(pt, face, 2, 6, 12, 10);
            pt.Outline(0, 0, W, H, new Color32(120, 112, 98, 255));
            // 테이프
            pt.Put(0, H - 1, new Color32(220, 220, 210, 160)); pt.Put(W - 1, H - 1, new Color32(220, 220, 210, 160));
            return pt.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>손님 앞모습 그림의 얼굴 부분(24x20)을 반으로 줄여 흑백으로 찍는다.</summary>
        static void DrawFace(PixelTex pt, CustomerView view, int x0, int y0, int w, int h)
        {
            pt.Fill(x0, y0, x0 + w, y0 + h, new Color32(150, 146, 136, 255));
            Sprite sp = view != null && view.counterBody != null ? view.counterBody.sprite : null;
            Texture2D tex = sp != null ? sp.texture : null;
            if (tex == null || !tex.isReadable) return;
            Rect tr = sp.textureRect;
            int tw = (int)tr.width, th = (int)tr.height, tx0 = (int)tr.x, ty0 = (int)tr.y;
            Color32[] px = tex.GetPixels32();
            int top = -1;
            for (int y = th - 1; y >= 0 && top < 0; y--)
                for (int x = 0; x < tw; x++)
                    if (px[(ty0 + y) * tex.width + tx0 + x].a > 20) { top = y; break; }
            if (top < 0) return;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f; int n = 0;
                    for (int j = 0; j < 2; j++)
                        for (int i = 0; i < 2; i++)
                        {
                            int sx = tw / 2 - w + x * 2 + i, sy = top - h * 2 + 1 + y * 2 + j;
                            if (sx < 0 || sy < 0 || sx >= tw || sy >= th) continue;
                            Color32 c = px[(ty0 + sy) * tex.width + tx0 + sx];
                            if (c.a < 20) continue;
                            sum += 0.3f * c.r + 0.59f * c.g + 0.11f * c.b; n++;
                        }
                    if (n == 0) continue;
                    byte l = (byte)Mathf.Clamp(sum / n * 0.85f, 0, 255);
                    pt.Put(x0 + x, y0 + y, new Color32(l, l, (byte)Mathf.Min(255, l + 4), 255));
                }
        }
    }
}
