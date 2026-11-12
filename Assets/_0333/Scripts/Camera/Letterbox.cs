using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 창 크기와 상관없이 게임 화면을 16:9로 유지한다(남는 부분은 검은 띠).
    /// 검은 띠는 깊이가 더 낮은 배경 카메라가 지운다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class Letterbox : MonoBehaviour
    {
        public float targetAspect = 16f / 9f;

        Camera cam;
        int lastW, lastH;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            lastW = lastH = 0;
        }

        void Update()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width;
            lastH = Screen.height;
            if (lastW <= 0 || lastH <= 0) return;

            float screenAspect = (float)lastW / lastH;
            float scale = screenAspect / targetAspect;
            cam.rect = scale < 1f
                ? new Rect(0f, (1f - scale) * 0.5f, 1f, scale)          // 위아래 띠
                : new Rect((1f - 1f / scale) * 0.5f, 0f, 1f / scale, 1f); // 좌우 띠
        }
    }
}
