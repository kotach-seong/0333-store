using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 손님 한 명의 겉모습. 계산대 앞에 선 앞모습과, CCTV에 찍히는 모습(채널마다 하나씩)을 함께 다룬다.
    ///  - 등장: 뒤에서 자동문이 열리며 차임이 울리고, 손님이 계산대 아래에서 스윽 올라온다.
    ///  - 퇴장: 문이 열리며 차임이 울리고, 손님이 계산대 아래로 가라앉아 사라진다.
    ///  - 가만히 서 있을 때: 숨 쉬듯 아주 살짝 오르내리고, 가끔 몸을 한쪽으로 옮긴다.
    /// 지금은 시험용으로 게임 시작 후 잠시 뒤 들어오고, C 키로 나갔다 들어왔다 할 수 있다.
    /// </summary>
    public class CustomerView : MonoBehaviour
    {
        public string customerId;
        [Tooltip("영수증의 손님 설명(사람 수 빼고). 예: 후드 청년 → 영수증에는 \"후드 청년 1\"")]
        public string receiptLabel = "손님";
        [Tooltip("같이 온 사람 수(커플·일행은 2). 영수증에 \"커플 2\"처럼 찍힌다")]
        public int partySize = 1;
        [Tooltip("키(미터). CCTV에 찍히는 크기를 정할 때 썼다")]
        public float heightM = 1.72f;
        [Tooltip("계산대 앞에 선 앞모습(하반신은 계산대에 가려진다)")]
        public SpriteRenderer counterBody;
        [Tooltip("계산대 앞에 다 올라왔을 때의 자리(씬을 만들 때 저장된다)")]
        public Vector3 bodyHome;
        [Tooltip("이상한 손님을 통과시켰을 때 변하는 모습(앞모습과 같은 크기·자리, 올 때마다 하나를 고른다)")]
        public Sprite[] transforms = new Sprite[0];
        [Tooltip("3:33(다시 온 손님): 입이 찢어지게 웃는 얼굴(앞모습과 같은 크기·자리)")]
        public Sprite grinFace;
        [Tooltip("변신 모습이 움직이면 그 프레임들(transforms와 같은 순서, 비어 있으면 멈춘 그림)")]
        public SpriteLoop[] transformLoops = new SpriteLoop[0];

        /// <summary>반복 재생하는 그림 묶음(촉수가 꿈틀대는 변신 등).</summary>
        [System.Serializable]
        public class SpriteLoop
        {
            public Sprite[] frames = new Sprite[0];
            public float fps = 8f;
        }
        [Tooltip("CCTV 작은 모니터와 크게 보기 화면에 찍히는 모습들")]
        public List<CctvOverlay> cctvImages = new List<CctvOverlay>();
        [Tooltip("손님이 들어오고 나갈 때 열리는 정면 자동문(차임 소리 포함)")]
        public DoorController door;

        [Header("등장·퇴장")]
        [Tooltip("계산대 아래 얼마나 깊은 곳에서 올라오는지(px). 머리까지 다 가려질 만큼")]
        public float riseDistance = 150f;
        public float riseTime = 0.9f, sinkTime = 0.7f;
        [Tooltip("문이 열리기 시작하고 이만큼 뒤에 올라오기 시작")]
        public float riseDelay = 0.25f;

        [Header("가만히 서 있을 때")]
        [Tooltip("숨 쉬는 오르내림(px)")]
        public float breathAmount = 1f;
        public float breathPeriod = 3.4f;
        [Tooltip("몸을 옮기는 정도(px)")]
        public float shiftAmount = 1f;
        public Vector2 shiftEvery = new Vector2(3f, 7f);

        [Header("시험용")]
        [Tooltip("게임 시작 후 이 시간(초)이 지나면 손님이 들어온다. 0보다 작으면 들어오지 않는다")]
        public float enterAfterSeconds = 1.5f;
        public bool toggleWithC = true;

        public bool Visible { get; private set; }
        /// <summary>CCTV에 안 찍히게(이상현상: CCTV에 손님이 없음·괴물로 찍힘). 내 눈에는 그대로 보인다.</summary>
        [System.NonSerialized] public bool cctvHidden;
        public bool Busy => moving != null;

        float rise = 1f;          // 0: 계산대 아래에 숨음, 1: 다 올라옴
        float shift, shiftTarget, nextShift;
        float breathOffset;
        Coroutine moving;

        void Start()
        {
            breathOffset = Random.value * breathPeriod;
            nextShift = Time.time + Random.Range(shiftEvery.x, shiftEvery.y);
            HideAll();
            if (enterAfterSeconds >= 0f) Invoke(nameof(Enter), enterAfterSeconds);
        }

        void Update()
        {
            if (toggleWithC && Application.isEditor && GameInput.CustomerToggleDown && !Busy) { if (Visible) Leave(); else Enter(); }   // 시험용(에디터에서만)

            // 가끔 몸을 한쪽으로 옮겼다 돌아오기
            if (Time.time >= nextShift)
            {
                shiftTarget = Mathf.Approximately(shiftTarget, 0f) ? (Random.value < 0.5f ? -1f : 1f) : 0f;
                nextShift = Time.time + Random.Range(shiftEvery.x, shiftEvery.y);
            }
            shift = Mathf.MoveTowards(shift, shiftTarget, Time.deltaTime * 2f);
            ApplyPose();
        }

        /// <summary>손님 등장: 문이 열리며 차임이 울리고, 계산대 아래에서 올라온다.</summary>
        public void Enter()
        {
            if (Visible || Busy) return;
            if (door != null) door.OpenAndClose();
            moving = StartCoroutine(RiseUp());
        }

        /// <summary>
        /// 손님 퇴장: 문이 열리며 차임이 울리고, 계산대 아래로 가라앉는다.
        /// silent면 문도 열리지 않고 차임도 없이 조용히 가라앉아 사라진다(점검표에서 "아니오"를 골랐을 때).
        /// </summary>
        public void Leave(bool silent = false)
        {
            if (!Visible || Busy) return;
            if (!silent && door != null) door.OpenAndClose();
            moving = StartCoroutine(SinkDown());
        }

        IEnumerator RiseUp()
        {
            Visible = true;
            rise = 0f;
            ApplyPose();
            if (counterBody != null) counterBody.enabled = true;
            yield return new WaitForSeconds(riseDelay);
            for (float t = 0f; t < riseTime; t += Time.deltaTime)
            {
                float k = t / riseTime;
                rise = 1f - (1f - k) * (1f - k) * (1f - k);   // 빠르게 올라오다 천천히 멈춤
                yield return null;
            }
            rise = 1f;
            SetCctv(true);   // 다 올라온 뒤 CCTV에도 찍힌다
            moving = null;
        }

        IEnumerator SinkDown()
        {
            SetCctv(false);
            for (float t = 0f; t < sinkTime; t += Time.deltaTime)
            {
                float k = t / sinkTime;
                rise = 1f - k * k;                               // 천천히 시작해서 쑥 내려감
                yield return null;
            }
            rise = 0f;
            HideAll();
            moving = null;
        }

        void ApplyPose()
        {
            if (counterBody == null) return;
            float breath = Mathf.Round(Mathf.Sin((Time.time + breathOffset) * Mathf.PI * 2f / breathPeriod) * breathAmount);
            float y = -Mathf.Round(riseDistance * (1f - rise)) + (rise >= 1f ? breath : 0f);
            float x = Mathf.Round(shift * shiftAmount);
            counterBody.transform.localPosition = bodyHome + new Vector3(x, y, 0f);
        }

        void HideAll()
        {
            Visible = false;
            if (counterBody != null) counterBody.enabled = false;
            SetCctv(false);
        }

        void SetCctv(bool on)
        {
            foreach (var o in cctvImages)
            {
                if (o == null) continue;
                o.present = on && !cctvHidden;
                o.Refresh(CctvSystem.CurrentChannel);
            }
        }

        /// <summary>바로 보이거나 숨기기(연출 없이). 에디터 미리보기에서도 쓴다.</summary>
        public void SetVisible(bool v)
        {
            Visible = v;
            rise = v ? 1f : 0f;
            if (counterBody != null) { counterBody.enabled = v; counterBody.transform.localPosition = bodyHome; }
            SetCctv(v);
        }

        /// <summary>에디터 미리보기용: 올라오는 도중(0~1)의 모습으로 둔다.</summary>
        public void PreviewRise(float r)
        {
            rise = Mathf.Clamp01(r);
            if (counterBody != null)
            {
                counterBody.enabled = true;
                counterBody.transform.localPosition = bodyHome + new Vector3(0f, -Mathf.Round(riseDistance * (1f - rise)), 0f);
            }
        }
    }
}
