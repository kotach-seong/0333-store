using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 물품 칸의 물건을 끌어서 스캔하는 기능. 카메라에 붙인다.
    ///  - 조준점을 물건에 대면 테두리와 "아래로 끌어 스캔"이 뜬다.
    ///  - 왼쪽 버튼을 누른 채 마우스를 움직이면 시선은 멈추고 물건이 따라온다.
    ///  - 물건을 물품 칸 앞(스캐너 쪽)으로 끌어내리면 "삑" 하고 찍히면서 사라진다.
    ///  - 그 전에 버튼을 놓으면 물건은 원래 자리로 돌아간다.
    /// </summary>
    [DefaultExecutionOrder(-50)] // 핫스팟보다 먼저 조준을 가져간다
    [RequireComponent(typeof(Camera))]
    public class ItemDragger : MonoBehaviour
    {
        public MouseLookCamera look;
        public HotspotInteractor interactor;
        public CheckoutSystem checkout;

        [Header("스캔되는 곳(월드 픽셀)")]
        [Tooltip("물건 바닥이 이 높이보다 내려가면 찍힌다(스캐너 유리의 먼 쪽 모서리)")]
        public float scanLineY = -169f;
        [Tooltip("이 좌우 범위 안에서 내려야 찍힌다")]
        public Vector2 scanXRange = new Vector2(-125f, 75f);

        [Header("손맛")]
        [Tooltip("집었을 때 살짝 들리는 높이(px)")]
        public float liftPx = 3f;
        [Tooltip("끄는 동안 물건이 그려지는 순서(다른 물건과 영수증보다 위)")]
        public int dragSortingOrder = 90;
        public float returnTime = 0.12f;
        public string aimLabel = "아래로 끌어 스캔";

        Camera cam;
        SpriteRenderer held;
        Vector3 heldHome;
        int heldOrder;
        Vector2 pos;
        // 놓친 물건이 제자리로 돌아가는 중
        SpriteRenderer returning;
        Vector3 returnFrom, returnTo;
        float returnT;

        public bool Holding => held != null;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (look == null) look = GetComponent<MouseLookCamera>();
            if (interactor == null) interactor = GetComponent<HotspotInteractor>();
            if (checkout == null) checkout = FindFirstObjectByType<CheckoutSystem>();
        }

        void Update()
        {
            AnimateReturn();
            ItemBay bay = checkout != null ? checkout.bay : null;
            bool canUse = bay != null && checkout.CanScan && look != null && !look.LookLocked
                          && Cursor.lockState == CursorLockMode.Locked;
            if (!canUse)
            {
                if (held != null) Drop();
                return;
            }

            if (held == null)
            {
                if (look.CursorChangedThisFrame || ChoiceView.Aimed) return;   // 대답 버튼을 겨누고 있으면 물건을 집지 않는다
                Vector2 aim = transform.position; // 조준점 = 화면 한가운데
                int i = bay.HitTest(aim);
                if (i < 0) return;
                SpriteRenderer sr = bay.Current[i].renderer;
                if (interactor != null) interactor.Highlight(AimRect(sr), aimLabel);
                if (GameInput.LeftClickDown) Grab(sr);
                return;
            }

            if (interactor != null) interactor.Highlight(Rect.zero, null); // 끄는 동안 핫스팟은 쉰다
            if (held == null || !GameInput.LeftHeld) { Drop(); return; }

            pos += GameInput.MouseDelta * (look.UnitsPerScreenPixel() * look.sensitivity);
            // 화면 밖으로 나가지 않게
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 c = transform.position;
            Bounds b = held.bounds;
            float w = b.extents.x;
            pos.x = Mathf.Clamp(pos.x, c.x - halfW + w, c.x + halfW - w);
            pos.y = Mathf.Clamp(pos.y, c.y - halfH + 2f, c.y + halfH - b.size.y);
            held.transform.position = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), heldHome.z);

            if (pos.y < scanLineY && pos.x > scanXRange.x && pos.x < scanXRange.y)
            {
                SpriteRenderer sr = held;
                Release();
                checkout.Scan(sr);
            }
        }

        static Rect AimRect(SpriteRenderer sr)
        {
            Bounds b = sr.bounds;
            return Rect.MinMaxRect(Mathf.Floor(b.min.x) - 1f, Mathf.Floor(b.min.y) - 1f, Mathf.Ceil(b.max.x) + 1f, Mathf.Ceil(b.max.y) + 1f);
        }

        void Grab(SpriteRenderer sr)
        {
            FinishReturn();
            held = sr;
            heldHome = sr.transform.position;
            heldOrder = sr.sortingOrder;
            sr.sortingOrder = dragSortingOrder;
            pos = (Vector2)heldHome + new Vector2(0f, liftPx);
            sr.transform.position = new Vector3(pos.x, pos.y, heldHome.z);
            look.FreezeLook = true;
            look.showReticle = false;
            checkout.PlayPickUp();
        }

        /// <summary>손을 뗀다(물건은 그 자리에 그대로).</summary>
        void Release()
        {
            held = null;
            if (look != null)
            {
                look.FreezeLook = false;
                look.showReticle = true;
            }
        }

        /// <summary>스캔하지 못하고 놓쳤을 때: 원래 자리로 돌려보낸다.</summary>
        void Drop()
        {
            SpriteRenderer sr = held;
            Release();
            if (sr == null) return;
            returning = sr;
            returnFrom = sr.transform.position;
            returnTo = heldHome;
            returnT = 0f;
            returning.sortingOrder = heldOrder;
        }

        void AnimateReturn()
        {
            if (returning == null) return;
            returnT += Time.deltaTime / Mathf.Max(0.01f, returnTime);
            float k = 1f - (1f - Mathf.Clamp01(returnT)) * (1f - Mathf.Clamp01(returnT));
            Vector3 p = Vector3.Lerp(returnFrom, returnTo, k);
            returning.transform.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), p.z);
            if (returnT >= 1f) FinishReturn();
        }

        void FinishReturn()
        {
            if (returning != null) returning.transform.position = returnTo;
            returning = null;
        }

        void OnDisable()
        {
            if (held != null) Drop();
            FinishReturn();
        }
    }
}
