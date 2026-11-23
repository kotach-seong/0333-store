using System.Collections.Generic;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 3:33(계산대 위 앞줄 등만 켜진 가게)의 CCTV 화면.
    /// 평소 CCTV와 같은 카메라·같은 장면을 앞줄 등만 켠 빛으로 다시 그린다(야간 카메라라 어두운 곳은 거칠게 끌어올려진다).
    /// </summary>
    public static partial class StoreArtGenerator
    {
        /// <summary>
        /// 채널별 크게 보기 영상과 계산대 모니터 화면(앞줄만 켜짐). 이름은 평소 이름 + "_front".
        /// monitorArea(월드 픽셀)만 훑어서 모니터 화면 자리를 찾는다(가게 전체를 다시 그리지 않는다).
        /// </summary>
        public static List<Layer> GenerateFrontFeeds(RectInt monitorArea)
        {
            mode = LightMode.Front;
            try
            {
                BuildScene();
                var list = new List<Layer>();
                for (int k = 0; k < Cams.Length; k++)
                {
                    var feed = new Layer(FullFeedName(k) + FrontSuffix, FeedRect, 200, Group.Ui);
                    RectInt r = FeedRect;
                    for (int y = r.yMin; y < r.yMax; y++)
                        for (int x = r.xMin; x < r.xMax; x++)
                        {
                            float u = (x - r.x + 0.5f) / r.width, v = (y - r.y + 0.5f) / r.height;
                            Vector3 e = CctvLook(FeedColor(Cams[k], u, v, x, y), u, v, x, y, Cams[k].Gain, Cams[k].Outdoor);
                            feed.Canvas.Put(x, y, PixelPalette.Quantize(e, x, y));
                        }
                    list.Add(feed);
                }

                RectInt w = WorldRect;
                int n = w.width * w.height;
                var monC = new Vector3[Cams.Length][];
                var monA = new float[Cams.Length][];
                for (int k = 0; k < Cams.Length; k++) { monC[k] = new Vector3[n]; monA[k] = new float[n]; }
                int x0 = Mathf.Max(w.xMin, monitorArea.xMin - 2), x1 = Mathf.Min(w.xMax, monitorArea.xMax + 2);
                int y0 = Mathf.Max(w.yMin, monitorArea.yMin - 2), y1 = Mathf.Min(w.yMax, monitorArea.yMax + 2);
                var eye = new Vector3(0f, Eye, 0f);
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        Vector3 d = Dir(x + 0.5f, y + 0.5f);
                        int bi = Trace(eye, d, 1e6f, false, out float t, out int face);
                        if (bi < 0) continue;
                        Surf s = MakeSurf(bi, face, eye, d, t, x, y, true);
                        boxes[bi].Shade(ref s);
                        if (s.Target != 0 || !s.IsScreen) continue;
                        int i = (y - w.y) * w.width + (x - w.x);
                        for (int k = 0; k < Cams.Length; k++)
                        {
                            monC[k][i] = CctvLook(FeedColor(Cams[k], s.ScreenUV.x, s.ScreenUV.y, x, y), s.ScreenUV.x, s.ScreenUV.y, x, y, Cams[k].Gain, Cams[k].Outdoor);
                            monA[k][i] = 1f;
                        }
                    }
                for (int k = 0; k < Cams.Length; k++)
                {
                    Layer l = ToLayer(MonitorLayerName(k) + FrontSuffix, monC[k], monA[k], 51, Group.Foreground);
                    if (l != null) list.Add(l);
                }
                return list;
            }
            finally
            {
                mode = LightMode.All;
                BuildScene();   // 뒤에 쓰는 것(메인 화면 괴물 빛 등)은 평소 빛으로
            }
        }
    }
}
