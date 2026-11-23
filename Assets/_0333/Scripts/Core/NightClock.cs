using System.Collections;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 밤 근무 시계(00:00 ~ 06:00). 시간은 손님 진행에 묶여 흐른다.
    ///  - 하룻밤 손님 10명. 손님마다 도착 시각이 정해져 있다(기본 시간표 + 날마다 몇 분씩 흔들림).
    ///  - 03:00 ~ 03:33에는 손님이 오지 않는다(03:33 메인 이벤트 자리).
    ///  - 손님이 없을 때는 시간이 빨리 흐르고, 다음 손님 도착 시각이 되면 멈춰서 손님을 들인다.
    ///  - 손님을 계산하는 동안에는 보통 빠르기로 흐르되, 다음 손님 도착 시각 1분 전에서 멈춘다
    ///    (계산이 늦어도 "1명 받았는데 05:59" 같은 일이 생기지 않는다).
    ///  - 10명을 다 받아야 06:00이 되고 근무가 끝난다.
    /// </summary>
    public class NightClock : MonoBehaviour
    {
        [Header("하룻밤")]
        public int day = 1;
        public int startMinute = 0;     // 00:00
        public int endMinute = 360;     // 06:00
        [Tooltip("게임 1분이 현실 몇 초인지(손님을 계산하는 동안)")]
        public float secondsPerMinute = 2f;
        [Tooltip("손님이 없을 때 이만큼 빨리 흐른다")]
        public float idleSpeed = 7f;   // 5 → 7(사용자 요청: 손님이 간 뒤 조금 더 빨리)

        [Header("손님 도착 시각(분, 00:00부터)")]
        public int[] baseArrivals = { 15, 45, 75, 110, 140, 170, 230, 270, 310, 340 };
        [Tooltip("날마다 도착 시각을 이 범위(±분)에서 흔든다")]
        public int jitter = 6;
        [Tooltip("손님이 오지 않는 시간대(03:00 ~ 03:33)")]
        public Vector2Int quietRange = new Vector2Int(180, 213);

        [Header("03:33 이벤트")]
        [Tooltip("이 시각(분)이 되면 멈추고 3:33 이벤트를 연다(손님이 오지 않는 03:00~03:33 끝)")]
        public int eventMinute = 213;

        public float Minute { get; private set; }
        /// <summary>오늘 밤 3:33 이벤트가 아직 남아 있다(시계가 03:33에서 멈춘다).</summary>
        public bool EventPending { get; set; }
        /// <summary>3:33 이벤트를 열 때가 됐다.</summary>
        public bool EventDue => Running && EventPending && !Serving && Minute >= eventMinute;
        /// <summary>이벤트가 시계를 직접 움직이는 동안(멈춤·거꾸로) 저절로 흐르지 않게.</summary>
        public bool Held { get; set; }
        /// <summary>이벤트가 시계를 직접 정한다(거꾸로 가는 시간 등).</summary>
        public void SetMinute(float m) => Minute = m;

        /// <summary>시험용: 그 시각으로 건너뛴다(그 전에 올 손님들은 건너뛴다).</summary>
        public void DebugSkipTo(int m)
        {
            Minute = m;
            while (NextIndex < Arrivals.Length && Arrivals[NextIndex] < m) NextIndex++;
        }
        public int[] Arrivals { get; private set; } = new int[0];
        public int CustomerCount => Arrivals.Length;
        /// <summary>다음에 올 손님 순번(0부터). 다 왔으면 CustomerCount.</summary>
        public int NextIndex { get; private set; }
        public bool Serving { get; private set; }
        public bool Running { get; set; }
        public bool NightOver => Running && Minute >= endMinute;
        /// <summary>다음 손님이 도착할 시각이 되어 들어올 차례인지.</summary>
        public bool CustomerDue => Running && !Serving && NextIndex < Arrivals.Length && Minute >= Arrivals[NextIndex];

        /// <summary>새 밤을 시작한다. 같은 날·같은 seed면 같은 시간표(세이브용).</summary>
        public void BeginNight(int dayNumber, int seed)
        {
            day = dayNumber;
            var rnd = new System.Random(seed * 31 + dayNumber);
            Arrivals = new int[baseArrivals.Length];
            int prev = startMinute;
            for (int i = 0; i < baseArrivals.Length; i++)
            {
                int m = baseArrivals[i] + rnd.Next(-jitter, jitter + 1);
                // 조용한 시간대(03:00~03:33)를 피하고, 앞 손님보다 최소 10분 뒤
                if (m >= quietRange.x && m <= quietRange.y) m = baseArrivals[i] < quietRange.x ? quietRange.x - 5 : quietRange.y + 10;
                m = Mathf.Max(m, prev + 10);
                Arrivals[i] = Mathf.Clamp(m, startMinute + 5, endMinute - 15);
                prev = Arrivals[i];
            }
            Minute = startMinute;
            NextIndex = 0;
            Serving = false;
            Running = true;
            EventPending = true;
            Held = false;
        }

        public void StartServing()
        {
            Serving = true;
            NextIndex++;
        }

        public void FinishServing() => Serving = false;

        bool rushing;

        /// <summary>
        /// 남은 밤을 건너뛴다: 시계가 미친 듯이 빨라져 06:00이 된다(손님은 더 오지 않는다).
        /// 처음엔 빨라지기 시작해 점점 가속한다. onMinute는 분이 바뀔 때마다 불린다(째깍 소리용).
        /// </summary>
        public IEnumerator RushToEnd(float seconds, System.Action<int> onMinute = null)
        {
            rushing = true;
            Running = true;
            Serving = false;
            EventPending = false;
            Held = false;
            NextIndex = Arrivals.Length;
            float from = Minute;
            int last = Mathf.FloorToInt(Minute);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                Minute = Mathf.Lerp(from, endMinute, k * k);
                int m = Mathf.FloorToInt(Minute);
                if (m != last) { last = m; onMinute?.Invoke(m); }
                yield return null;
            }
            Minute = endMinute;
            rushing = false;
        }

        void Update()
        {
            if (!Running || rushing || Held || Minute >= endMinute) return;
            float limit = endMinute;
            if (NextIndex < Arrivals.Length) limit = Serving ? Arrivals[NextIndex] - 1 : Arrivals[NextIndex];
            else if (Serving) limit = endMinute - 1;
            if (EventPending && Minute < eventMinute) limit = Mathf.Min(limit, eventMinute);   // 03:33에서 멈춘다
            float speed = Serving ? 1f : idleSpeed;
            Minute = Mathf.Min(limit, Minute + Time.deltaTime * speed / Mathf.Max(0.01f, secondsPerMinute));
        }

        public string Format() => Format(Mathf.FloorToInt(Minute));

        public static string Format(int minute)
        {
            minute = Mathf.Max(0, minute);
            return $"{minute / 60 % 24:00}:{minute % 60:00}";
        }

        /// <summary>에디터 미리보기용: 시각을 바로 정한다.</summary>
        public void SetPreview(int dayNumber, int minute)
        {
            day = dayNumber;
            Minute = minute;
        }
    }
}
