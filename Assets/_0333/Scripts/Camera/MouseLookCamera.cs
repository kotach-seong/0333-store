using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Night0333
{
    /// <summary>
    /// 계산대에 서 있는 알바생의 시선. 캐릭터는 움직이지 않고 고개만 돌린다.
    ///
    /// 일반적인 1인칭 게임처럼 커서를 화면 가운데에 숨겨 두고, 마우스를 움직인 만큼 시선이 움직인다.
    ///  - 마우스를 멈추면 시선도 그 자리에 멈춘다(저절로 정면으로 돌아오지 않는다).
    ///  - 매장 그림의 끝(양옆 진열대, 천장, 계산대)에서 멈추므로 뒤는 돌아볼 수 없다.
    ///  - 확대/축소는 없다. 화면 크기는 항상 같다.
    ///  - 에디터에서는 Game 창을 한 번 클릭해야 커서가 잠긴다. Esc를 누르면 커서가 풀리고, 다시 클릭하면 잠긴다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MouseLookCamera : MonoBehaviour
    {
        [Tooltip("카메라가 볼 수 있는 매장 전체 영역(월드 픽셀)")]
        public Rect worldBounds = new Rect(-570f, -380f, 1140f, 640f);
        [Tooltip("게임을 시작했을 때 보는 곳(정면)")]
        public Vector2 home = new Vector2(0f, -34f);

        [Header("감도")]
        [Tooltip("1이면 마우스를 움직인 거리만큼 화면이 움직인다. 클수록 빠르다")]
        [Range(0.2f, 4f)] public float sensitivity = 1.2f;
        [Tooltip("마우스를 위로 올리면 아래를 보게 한다")]
        public bool invertY;
        [Tooltip("시선이 따라오는 데 걸리는 시간(초). 0이면 즉시")]
        [Range(0f, 0.3f)] public float smoothTime = 0.04f;

        [Header("화면 가운데 조준점")]
        public bool showReticle = true;

        /// <summary>POS/CCTV/수첩 패널이 열려 있는 동안 true로 두면 시선이 고정되고 커서가 나타난다.</summary>
        public bool LookLocked
        {
            get => lookLocked;
            set { lookLocked = value; SetCursorLocked(!value); }
        }

        /// <summary>
        /// 물건을 끄는 동안 true. 커서는 잠긴 채로 시선만 멈추고, 마우스 움직임은 물건이 가져간다.
        /// </summary>
        [System.NonSerialized] public bool FreezeLook;

        Camera cam;
        Vector2 current, target, velocity;
        /// <summary>
        /// 이번 프레임에 커서 잠금 상태가 바뀌었는지. 패널을 닫은 클릭이 같은 프레임에
        /// "핫스팟 클릭"으로 한 번 더 읽히는 것을 막을 때 쓴다.
        /// </summary>
        public bool CursorChangedThisFrame => Time.frameCount == cursorChangedFrame;

        bool lookLocked, skipDelta;
        int cursorChangedFrame = -1;

        void Awake()
        {
            cam = GetComponent<Camera>();
            current = target = home;
        }

        void OnEnable() => SetCursorLocked(!lookLocked);
        void OnDisable() => SetCursorLocked(false);

        void OnApplicationFocus(bool focus)
        {
            if (focus && !lookLocked) SetCursorLocked(true);
        }

        void LateUpdate()
        {
            HandleCursorKeys();

            Rect lim = Limits();
            if (!lookLocked && !FreezeLook && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = ReadDelta();
                if (skipDelta) { delta = Vector2.zero; skipDelta = false; } // 커서를 잠근 직후의 튀는 값은 버린다
                if (invertY) delta.y = -delta.y;
                delta = HandFx.Look(delta);   // 3:33: 손이 떨리거나 무겁다
                target += delta * (UnitsPerScreenPixel() * sensitivity);
            }
            target = Clamp(target, lim);

            current = smoothTime > 0f
                ? Vector2.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime)
                : target;

            // 화면 픽셀 단위로 맞춰서 도트가 번지거나 떨리지 않게 한다
            float unit = UnitsPerScreenPixel();
            Vector2 p = Clamp(current, lim);
            p = new Vector2(Mathf.Round(p.x / unit) * unit, Mathf.Round(p.y / unit) * unit);
            p += ScreenFx.Offset;   // 3:33: 화면 흔들림
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        /// <summary>시선을 정면(문 쪽)으로 즉시 되돌린다.</summary>
        public void Recenter()
        {
            current = target = home;
            velocity = Vector2.zero;
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (locked) skipDelta = true;
            cursorChangedFrame = Time.frameCount;
        }

        /// <summary>화면 클릭: 커서 다시 잠그기. Esc는 일시정지 메뉴가 있으면 그쪽(PauseMenu), 없으면 커서 풀기.</summary>
        void HandleCursorKeys()
        {
            if (lookLocked) return;
            // 패널을 닫은 Esc가 같은 프레임에 커서까지 풀어 버리지 않게, 커서 상태가 막 바뀐 프레임은 건너뛴다
            if (Time.frameCount == cursorChangedFrame) return;
            bool esc, click;
#if ENABLE_INPUT_SYSTEM
            esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            esc = Input.GetKeyDown(KeyCode.Escape);
            click = Input.GetMouseButtonDown(0);
#endif
            if (esc && !PauseMenu.Available) SetCursorLocked(false);   // 일시정지 메뉴가 있으면 Esc는 메뉴가 연다
            else if (click && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
        }

        static Vector2 ReadDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
        }

        /// <summary>화면 픽셀 하나가 월드에서 차지하는 길이.</summary>
        public float UnitsPerScreenPixel() => 2f * cam.orthographicSize / Mathf.Max(1, cam.pixelHeight);

        /// <summary>카메라 중심이 움직일 수 있는 범위(화면이 매장 그림 밖으로 나가지 않게).</summary>
        Rect Limits()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float x0 = worldBounds.xMin + halfW, x1 = worldBounds.xMax - halfW;
            float y0 = worldBounds.yMin + halfH, y1 = worldBounds.yMax - halfH;
            if (x0 > x1) x0 = x1 = worldBounds.center.x;
            if (y0 > y1) y0 = y1 = worldBounds.center.y;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static Vector2 Clamp(Vector2 p, Rect lim) =>
            new Vector2(Mathf.Clamp(p.x, lim.xMin, lim.xMax), Mathf.Clamp(p.y, lim.yMin, lim.yMax));

        /// <summary>커서가 숨겨져 있는 동안 화면 한가운데에 찍는 작은 조준점.</summary>
        void OnGUI()
        {
            if (!showReticle || Cursor.lockState != CursorLockMode.Locked) return;
            if (Event.current.type != EventType.Repaint) return;

            Rect r = cam.pixelRect;
            float px = Mathf.Max(1f, Mathf.Round(r.height / (2f * cam.orthographicSize))); // 도트 하나의 화면 크기
            var c = new Vector2(r.x + r.width * 0.5f, Screen.height - (r.y + r.height * 0.5f));

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(c.x - 1.5f * px, c.y - 1.5f * px, 3f * px, 3f * px), Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.95f, 0.88f, 0.9f);
            GUI.DrawTexture(new Rect(c.x - 0.5f * px, c.y - 0.5f * px, px, px), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
