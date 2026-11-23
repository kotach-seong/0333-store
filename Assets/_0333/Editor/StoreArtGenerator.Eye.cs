using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 1인칭(내 눈높이) 컬러 그림: 오프닝에서 비 오는 주차장을 걸어 가게 안으로 들어가는 장면.
    /// CCTV와 같은 장면(가게 안 + 바깥 3D 배경)을 같은 빛으로 다시 그리되, 흑백이 아니라 정면 화면과 같은 색 단계로.
    /// 시야는 정면 화면과 같은 초점거리(F)라서 들어와 계산대 쪽을 볼 때 게임 화면과 같은 크기감이다.
    /// </summary>
    public static partial class StoreArtGenerator
    {
        /// <summary>pos에서 target을 보는 640x360 그림. doorGap(m)만큼 자동문이 열려 있다.</summary>
        public static PixelCanvas RenderEye(Vector3 pos, Vector3 target, float doorGap)
        {
            float oldGap = MenuDoorGap;
            MenuDoorGap = doorGap;
            if (boxes.Count == 0) BuildScene();
            var canvas = new PixelCanvas(new RectInt(-ViewW / 2, -ViewH / 2, ViewW, ViewH));
            Vector3 fwd = (target - pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            bool was = inFeed;
            inFeed = true;   // 바깥 3D 배경(보도·주차장·간판·가로등)도 그린다
            try
            {
                for (int y = -ViewH / 2; y < ViewH / 2; y++)
                    for (int x = -ViewW / 2; x < ViewW / 2; x++)
                    {
                        Vector3 d = (fwd + right * ((x + 0.5f) / F) + up * ((y + 0.5f - VPy) / F)).normalized;
                        Vector3 c = FeedTrace(pos, d, x, y, 0);
                        canvas.Put(x, y, PixelPalette.Quantize(c, x, y));
                    }
            }
            finally
            {
                inFeed = was;
                MenuDoorGap = oldGap;
            }
            return canvas;
        }
    }
}
