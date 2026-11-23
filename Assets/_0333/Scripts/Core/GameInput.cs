using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Night0333
{
    /// <summary>마우스·키 입력을 한곳에서 읽는다(새 Input System과 예전 입력 방식 모두 지원).</summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        public static bool LeftClickDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        /// <summary>왼쪽 버튼을 누르고 있는 동안(물건 끌기).</summary>
        public static bool LeftHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        /// <summary>이번 프레임에 마우스가 움직인 양(화면 픽셀).</summary>
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static bool RightClickDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        /// <summary>Space를 누르고 있는 동안(오프닝 건너뛰기: 길게 누르기).</summary>
        public static bool SkipHeld => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        public static bool EscapeDown => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        /// <summary>대사·결과 화면 넘기기(스페이스, 엔터).</summary>
        public static bool SubmitDown => Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
        public static bool PrevDown => Keyboard.current != null &&
            (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame);
        public static bool NextDown => Keyboard.current != null &&
            (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame);
        /// <summary>시험용: 물품 칸 다시 섞기(R).</summary>
        public static bool RerollDown => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        /// <summary>시험용: 손님 나타났다 사라지기(C).</summary>
        public static bool CustomerToggleDown => Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        /// <summary>메뉴 위·아래(화살표, W·S).</summary>
        public static bool UpDown => Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame);
        public static bool DownDown => Keyboard.current != null && (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame);
        /// <summary>대답 고르기(1, 2).</summary>
        public static bool Choice1Down => Keyboard.current != null && (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame);
        public static bool Choice2Down => Keyboard.current != null && (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame);
        /// <summary>아무 키나 마우스 버튼을 눌렀는지(메인 화면에서 가만히 있는지 볼 때).</summary>
        public static bool AnyDown => (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || LeftClickDown || RightClickDown;
        /// <summary>시험용(에디터): 시계를 03:32로(F3).</summary>
        public static bool DebugJumpDown => Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#else
        public static bool DebugJumpDown => Input.GetKeyDown(KeyCode.F3);
        public static bool LeftClickDown => Input.GetMouseButtonDown(0);
        public static bool LeftHeld => Input.GetMouseButton(0);
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        public static bool RightClickDown => Input.GetMouseButtonDown(1);
        public static Vector2 MousePosition => Input.mousePosition;
        public static bool SkipHeld => Input.GetKey(KeyCode.Space);
        public static bool EscapeDown => Input.GetKeyDown(KeyCode.Escape);
        public static bool SubmitDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        public static bool PrevDown => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        public static bool NextDown => Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        public static bool RerollDown => Input.GetKeyDown(KeyCode.R);
        public static bool CustomerToggleDown => Input.GetKeyDown(KeyCode.C);
        public static bool UpDown => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        public static bool DownDown => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        public static bool Choice1Down => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
        public static bool Choice2Down => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
        public static bool AnyDown => Input.anyKeyDown;
#endif
    }
}
