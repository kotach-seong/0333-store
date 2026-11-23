using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// CCTV. 계산대 위 모니터를 클릭하면 화면이 크게 열리고, 채널 3개를 넘겨 볼 수 있다.
    ///  - 모니터 왼쪽 아래 버튼 = 이전 채널, 오른쪽 아래 버튼 = 다음 채널. 1번과 3번은 서로 이어진다.
    ///  - 키보드로는 ←/A = 이전, →/D = 다음.
    ///  - 화면 왼쪽 위의 빨간 X 버튼, Esc, 마우스 오른쪽 클릭으로 닫는다.
    ///  - 계산대 위 작은 모니터에는 마지막으로 본 채널이 계속 나온다.
    /// </summary>
    public class CctvSystem : MonoBehaviour
    {
        [Header("연결")]
        public MouseLookCamera look;
        public Camera viewCamera;
        [Tooltip("크게 보기 화면 전체(카메라의 자식)")]
        public GameObject panel;
        [Tooltip("계산대 위 모니터에 겹쳐 놓은 채널별 화면")]
        public SpriteRenderer[] monitorScreens;
        [Tooltip("크게 보기 화면의 채널별 영상")]
        public SpriteRenderer[] panelFeeds;
        public SpriteRenderer prevHover, nextHover, closeHover;
        public Transform channelDot;
        [Tooltip("3:33(앞줄 등만 켜진 가게)의 모니터 화면·크게 보기 영상. 비어 있으면 평소 화면을 쓴다")]
        public SpriteRenderer[] monitorScreensNight = new SpriteRenderer[0];
        public SpriteRenderer[] panelFeedsNight = new SpriteRenderer[0];

        bool night;
        /// <summary>가게 불이 앞줄만 켜져 있다(3:33): CCTV도 어두운 가게를 비춘다.</summary>
        public bool Night
        {
            get => night;
            set { if (night == value) return; night = value; Apply(); }
        }

        [Header("배치(화면 중심 기준 픽셀)")]
        public Rect prevButton;
        public Rect nextButton;
        public Rect closeButton;
        public float dotStep = 11f;

        [Header("상태")]
        [Tooltip("지금 보고 있는 채널(0부터)")]
        public int channel;

        public bool IsOpen => panel != null && panel.activeSelf;

        [Tooltip("1번 채널일 때 초록 점의 자리(씬을 만들 때 저장된다)")]
        public Vector3 dotHome;
        int openedFrame = -1, closedFrame = -1;

        void Awake()
        {
            if (channelDot != null && dotHome == Vector3.zero) dotHome = channelDot.localPosition;
            if (panel != null) panel.SetActive(false);
            Apply();
        }

        /// <summary>모니터를 클릭했을 때: 크게 보기를 연다.</summary>
        public void Open()
        {
            if (IsOpen || panel == null) return;
            if (Time.frameCount == closedFrame) return; // X를 누른 클릭으로 닫자마자 다시 열리지 않게
            panel.SetActive(true);
            openedFrame = Time.frameCount;
            if (look != null) look.LookLocked = true; // 시선 고정, 커서 표시
            Apply();
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            closedFrame = Time.frameCount;
            if (look != null) look.LookLocked = false;
        }

        public void Next() => SetChannel(channel + 1);
        public void Prev() => SetChannel(channel - 1);

        public void SetChannel(int index)
        {
            int n = Mathf.Max(1, monitorScreens != null ? monitorScreens.Length : 1);
            channel = ((index % n) + n) % n; // 끝에서 처음으로, 처음에서 끝으로 이어진다
            Apply();
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == openedFrame) return; // 여는 데 쓴 클릭을 버튼 클릭으로 잘못 읽지 않게

            if (GameInput.EscapeDown || GameInput.RightClickDown) { Close(); return; }
            if (GameInput.PrevDown) Prev();
            if (GameInput.NextDown) Next();

            // 마우스가 가리키는 곳을 화면 중심 기준 픽셀 좌표로
            Vector2 p = Vector2.zero;
            bool overPrev = false, overNext = false, overClose = false;
            if (viewCamera != null)
            {
                Vector3 w = viewCamera.ScreenToWorldPoint(GameInput.MousePosition);
                p = (Vector2)w - (Vector2)viewCamera.transform.position;
                overPrev = prevButton.Contains(p);
                overNext = nextButton.Contains(p);
                overClose = closeButton.Contains(p);
            }
            if (prevHover != null) prevHover.enabled = overPrev;
            if (nextHover != null) nextHover.enabled = overNext;
            if (closeHover != null) closeHover.enabled = overClose;

            if (GameInput.LeftClickDown)
            {
                if (overClose) Close();
                else if (overPrev) Prev();
                else if (overNext) Next();
            }
        }

        /// <summary>현재 채널에 맞춰 작은 모니터, 큰 화면, 채널 표시 점을 바꾼다.</summary>
        void Apply()
        {
            ShowFeeds(monitorScreens, monitorScreensNight);
            ShowFeeds(panelFeeds, panelFeedsNight);
            if (channelDot != null) channelDot.localPosition = dotHome + new Vector3(channel * dotStep, 0f, 0f);

            // CCTV에 찍히는 손님 등 덧그림도 지금 채널 것만 보이게
            CurrentChannel = channel;
            foreach (var o in FindObjectsByType<CctvOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                o.Refresh(channel);
        }

        /// <summary>지금 채널의 화면만 켠다. 밤(3:33)이고 그 채널의 어두운 화면이 있으면 그쪽을.</summary>
        void ShowFeeds(SpriteRenderer[] day, SpriteRenderer[] dark)
        {
            int n = Mathf.Max(day != null ? day.Length : 0, dark != null ? dark.Length : 0);
            for (int i = 0; i < n; i++)
            {
                SpriteRenderer d = day != null && i < day.Length ? day[i] : null;
                SpriteRenderer k = dark != null && i < dark.Length ? dark[i] : null;
                bool here = i == channel, useDark = night && k != null;
                if (d != null) d.enabled = here && !useDark;
                if (k != null) k.enabled = here && useDark;
            }
        }

        /// <summary>지금 보고 있는 채널(덧그림이 따라 쓴다).</summary>
        public static int CurrentChannel { get; private set; }
    }
}
