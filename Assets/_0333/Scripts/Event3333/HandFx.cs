using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Night0333
{
    /// <summary>
    /// 손이 말을 안 듣는 연출(3:33 이벤트).
    ///  - Tremble(0~1): 손이 떨린다. 시선이 제멋대로 흔들리고, 패널을 열었을 때는 커서도 떨린다.
    ///  - Heavy(0~1): 손이 무겁다. 마우스를 움직여도 시선·커서가 굼뜨게 따라온다.
    /// 시선(MouseLookCamera)은 움직인 양을 여기서 바꾸고, 패널 커서는 CursorFx가 실제 커서를 옮겨서 흉내 낸다.
    /// </summary>
    public static class HandFx
    {
        public static float Tremble;
        public static float Heavy;

        public static bool Active => Tremble > 0.001f || Heavy > 0.001f;

        public static void Reset() { Tremble = 0f; Heavy = 0f; }

        /// <summary>이번 프레임 마우스 이동량(화면 픽셀)을 바꾼다.</summary>
        public static Vector2 Look(Vector2 delta)
        {
            if (Heavy > 0f) delta *= Mathf.Lerp(1f, 0.16f, Heavy);
            if (Tremble > 0f && !GameSettings.NoShake) delta += Jitter(Time.unscaledTime) * (Tremble * 9f);   // 화면 흔들림 끔: 시선이 떨리지 않는다
            return delta;
        }

        /// <summary>부드럽게 떨리는 방향(-1~1). 빠른 떨림과 가끔 크게 튀는 경련을 섞는다.</summary>
        public static Vector2 Jitter(float t)
        {
            float x = Mathf.PerlinNoise(t * 17f, 3.1f) - 0.5f, y = Mathf.PerlinNoise(5.7f, t * 17f) - 0.5f;
            float jerk = Mathf.PerlinNoise(t * 3f, 9.3f) > 0.72f ? 3f : 1f;
            return new Vector2(x, y) * 2f * jerk;
        }
    }

    /// <summary>
    /// 패널(점검표·영수증)이 열려 커서가 보일 때, HandFx에 따라 실제 커서를 떨게 하거나 굼뜨게 따라오게 한다.
    /// 카메라에 붙는다.
    /// </summary>
    public class CursorFx : MonoBehaviour
    {
        Vector2 virt, lastSet;
        bool tracking;

        void LateUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse m = Mouse.current;
            if (m == null || !Application.isPlaying) return;
            if (PauseMenu.Paused) { tracking = false; return; }   // 일시정지 메뉴에서는 커서가 떨리지 않는다
            bool visible = Cursor.lockState != CursorLockMode.Locked;
            if (!visible || !HandFx.Active) { tracking = false; return; }
            Vector2 now = m.position.ReadValue();
            if (!tracking) { virt = lastSet = now; tracking = true; }
            Vector2 moved = now - lastSet;
            virt += moved * Mathf.Lerp(1f, 0.18f, HandFx.Heavy);
            Vector2 shown = virt + (GameSettings.NoShake ? Vector2.zero : HandFx.Jitter(Time.unscaledTime) * (HandFx.Tremble * 14f));
            shown.x = Mathf.Clamp(shown.x, 0f, Screen.width - 1);
            shown.y = Mathf.Clamp(shown.y, 0f, Screen.height - 1);
            virt.x = Mathf.Clamp(virt.x, 0f, Screen.width - 1);
            virt.y = Mathf.Clamp(virt.y, 0f, Screen.height - 1);
            if ((shown - now).sqrMagnitude > 0.25f) m.WarpCursorPosition(shown);
            lastSet = shown;
#endif
        }
    }
}
