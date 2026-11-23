using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 계산 한 번의 흐름을 이끈다.
    ///  1) 손님이 계산대 아래에서 올라온다(문 차임).
    ///  2) 손님이 물품 칸에 물건을 하나씩 내려놓는다.
    ///  3) 알바생이 물건을 아래(스캐너)로 끌어 "삑" 찍는다. 찍을 때마다 금전등록기 숫자가 올라간다.
    ///  4) 다 찍으면 영수증이 뽑혀 나오고, 동시에 손님이 합계에 딱 맞게 돈바구니에 돈을 놓는다.
    ///  5) 영수증을 클릭해 크게 볼 수 있다(CCTV와 비교하는 증거).
    ///  6) 계산대 위 점검표(스캐너와 금전등록기 사이)에서 "이 거래는 정상입니까?"에 예/아니오를 체크하고
    ///     "거래 종료하기"를 누르면 판정이 정해진다. 손님은 영수증을 받아 나가고 알바생은 돈을 챙긴다.
    ///     예: 문이 열리며 딸랑 / 아니오: 문도 차임도 없이 조용히 사라진다.
    /// 지금은 시험용으로 같은 손님이 계속 다시 들어온다(손님 일정·시간 기능이 생기면 그쪽이 부른다).
    /// </summary>
    public class CheckoutSystem : MonoBehaviour
    {
        public enum Phase { Idle, Arriving, Placing, Scanning, Paying, AwaitReceipt, Leaving }

        [Serializable]
        public class Price
        {
            public string id;
            public int won;
        }

        [Header("연결")]
        public ItemBay bay;
        [Tooltip("올 수 있는 손님들(씬을 만들 때 채워진다). 손님마다 이 중에서 랜덤으로 온다")]
        public CustomerView[] customers = new CustomerView[0];
        [Tooltip("지금 계산 중인 손님")]
        public CustomerView customer;
        public RegisterDisplay display;
        public ScannerLight scanner;
        public ReceiptPrinter printer;
        public ReceiptView receiptView;
        public ChecklistView checklistView;
        public MoneyTray tray;
        public AudioSource sfx;
        [Tooltip("밤 근무 시계(00:00~06:00, 손님 10명 시간표)")]
        public NightClock clock;
        [Tooltip("06:00 근무 끝 장면(교대 알바생·결과 화면·다음 날)")]
        public ShiftEndView shiftEnd;
        [Tooltip("아침 교대 알바생(사용자가 그려 온 창백한 알바생 그림). 손님으로는 오지 않는다")]
        public CustomerView shiftWorker;
        public string shiftWorkerId = "worker";
        [Tooltip("판정 결과: 흔적(죽음)·실종(체포), 7일 합계")]
        public FateSystem fate;
        [Tooltip("실종을 알리러 오는 경찰(손님 그림 중 하나)")]
        public string policeId = "police";

        [Header("손님 대사(말풍선)")]
        [Tooltip("손님별 대사(Data/Dialogue.txt)")]
        public TextAsset dialogueFile;
        public SpeechBubble bubble;
        [Tooltip("인사에 내가 할 대답 2개")]
        public ChoiceView choices;
        [Tooltip("이상한 손님을 통과시키면 눈과 입이 찢어지며 웃는다")]
        public CreepyGrin grin;
        [Tooltip("손님을 두고 CCTV만 보는지 확인한다")]
        public CctvSystem cctv;
        [Tooltip("CCTV에서만 보이는 것들(손님 뒤 귀신, 괴물, 밖에서 보는 귀신들)")]
        public CctvAnomalies cctvAnomalies;
        [Tooltip("근무 시작 전 점장 전화(받을 때까지 시계가 멈춘다)")]
        public PhoneCall phone;
        [Tooltip("수첩: 전화로 들은 수칙과 오늘의 할 일이 적힌다")]
        public NotebookSystem notebook;
        [Tooltip("03:33 이벤트(밤마다 20가지 중 하나)")]
        public Event3333 event333;
        [Tooltip("오프닝(새로 시작)·엔딩(7일차 06:00까지 버팀)")]
        public StoryView story;
        [Tooltip("손님을 두고 CCTV를 이만큼 계속 보면 화를 낸다(초)")]
        public float cctvAngrySeconds = 10f;
        [Tooltip("물건을 올려놓고 이만큼 지나도 계산이 안 끝나면 화를 낸다(초): 1단계, 2단계")]
        public float slowFirst = 20f, slowSecond = 40f;
        [Tooltip("인사 말풍선과 대답 버튼이 떠 있는 시간(초). 안 고르면 그냥 사라진다")]
        public float choiceSeconds = 12f;
        CustomerLines lines;
        Lang linesLang;
        // 손님 대사: 지금 언어 파일(언어를 바꾸면 다시 읽는다)
        CustomerLines Lines
        {
            get
            {
                if (lines == null || linesLang != Loc.Current) { lines = CustomerLines.Parse(Loc.Text(dialogueFile)); linesLang = Loc.Current; }
                return lines;
            }
        }

        [Header("근무 일정")]
        public int customersPerNight = 10;
        public int lastDay = 7;
        [Tooltip("켜면 세이브에서 이어 한다(꺼 두면 시험할 때 항상 DAY 1부터)")]
        public bool continueFromSave = false;
        [Tooltip("게임을 켜면 먼저 뜨는 메인 화면(있으면 여기서 새로 시작·이어하기를 고른다)")]
        public MainMenu mainMenu;

        [Header("가격(원)")]
        public Price[] prices = new Price[0];

        [Header("시험용 손님 순환")]
        public bool autoLoop = true;
        public float firstDelay = 1.5f, nextDelay = 3f;
        [Range(0f, 1f)] public float anomalyChance = 0.3f;

        [Header("영수증 이상현상(시험용: 손님·날짜 규칙이 생기면 그쪽이 정한다)")]
        [Tooltip("손님마다 영수증이 이상해질 확률")]
        [Range(0f, 1f)] public float receiptAnomalyChance = 0.5f;
        [Tooltip("이상해질 때 두 가지가 겹칠 확률")]
        [Range(0f, 1f)] public float doubleAnomalyChance = 0.3f;
        [Tooltip("골라 두면 매번 이 이상현상만 나온다(시험용). Nothing이면 랜덤")]
        public ReceiptAnomaly forceAnomalies = ReceiptAnomaly.None;

        [Header("임시 시계(시간 기능이 생기면 바뀐다)")]
        public int day = 1;
        [Tooltip("근무 시작 시각(분). 0이면 00:00")]
        public int startMinute = 0;
        public int minutesPerCustomer = 17;

        public Phase CurrentPhase { get; private set; }
        public bool CanScan => CurrentPhase == Phase.Scanning;
        public ReceiptData Current { get; private set; }
        /// <summary>그날 밤 점검표 기록(손님마다 한 줄).</summary>
        public IReadOnlyList<ChecklistEntry> Entries => entries;

        readonly List<ChecklistEntry> entries = new List<ChecklistEntry>();
        ChecklistEntry currentEntry;
        int served;

        // ── 3:33 이벤트가 쓰는 것들 ──
        /// <summary>찍을 때 "삑" 대신 나는 소리(없는 상품: 웃음소리).</summary>
        [NonSerialized] public AudioClip scanSoundOverride;
        /// <summary>0 이상이면 찍은 물건 값을 이걸로.</summary>
        [NonSerialized] public int scanPriceOverride = -1;
        ChecklistEntry specialEntry;

        /// <summary>올 수 있는 손님들(교대 알바생 제외).</summary>
        public IReadOnlyList<CustomerView> Pool => customers;

        public void SetPhase(Phase p) => CurrentPhase = p;

        /// <summary>
        /// 3:33 거래의 영수증을 연다(그날 손님 수에는 들어가지 않는다). 점검표에는 잠깐 "0333 03:33" 줄이 생긴다.
        /// </summary>
        public void BeginSpecial(string time, int number, string visitor)
        {
            Current = new ReceiptData { day = day, number = number, time = time, visitorText = visitor };
            specialEntry = new ChecklistEntry { number = number, time = time, verdict = Verdict.None, label = visitor };
            entries.Add(specialEntry);
            currentEntry = null;
            if (checklistView != null) checklistView.SetCurrent(number);
            if (display != null) display.Show(0);
        }

        /// <summary>3:33 거래를 닫는다(점검표의 임시 줄도 지운다).</summary>
        public void EndSpecial()
        {
            if (specialEntry != null) entries.Remove(specialEntry);
            specialEntry = null;
            Current = null;
            if (checklistView != null) checklistView.EndQuestion();
            if (receiptView != null) receiptView.Prepare(null);
        }

        public IEnumerator DropItemsRoutine() => DropItems();

        /// <summary>손님 머리 위 말풍선.</summary>
        public void SayBy(CustomerView who, string line, float hold = -1f, bool zalgo = false) => Say(who, line, hold, zalgo);

        void Awake()
        {
            if (bay == null) bay = FindFirstObjectByType<ItemBay>();
            // 손님 목록이 비었거나 다시 만들어져 끊겼으면 씬에서 다시 찾는다
            bool broken = customers == null || customers.Length == 0;
            if (!broken) foreach (var c in customers) if (c == null) { broken = true; break; }
            if (broken) customers = FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            // 교대 알바생은 손님 목록에서 뺀다
            var list = new List<CustomerView>();
            foreach (var c in customers)
            {
                if (c == null) continue;
                if (!string.IsNullOrEmpty(shiftWorkerId) && c.customerId == shiftWorkerId) { if (shiftWorker == null) shiftWorker = c; continue; }
                list.Add(c);
            }
            customers = list.ToArray();
            // 시험용 자동 등장·R 섞기·C 토글은 끈다: 이제 계산 흐름이 손님과 물건을 부른다
            if (bay != null) { bay.fillOnStart = false; bay.rerollWithR = false; }
            foreach (var c in customers) { c.enterAfterSeconds = -1f; c.toggleWithC = false; }
            if (shiftWorker != null) { shiftWorker.enterAfterSeconds = -1f; shiftWorker.toggleWithC = false; }
            if (customer == null && customers.Length > 0) customer = customers[0];
        }

        /// <summary>다음 손님을 랜덤으로 고른다(방금 왔던 손님은 바로 다시 오지 않는다).</summary>
        CustomerView PickCustomer(System.Random rnd)
        {
            if (customers.Length == 0) return customer;
            if (customers.Length == 1) return customers[0];
            CustomerView next;
            do next = customers[rnd.Next(customers.Length)]; while (next == customer);
            return next;
        }

#if UNITY_EDITOR
        void Update()
        {
            // 시험용: F3을 누르면 시계가 03:32로(계산 중이 아닐 때). 그 사이 올 손님들은 건너뛴다
            if (GameInput.DebugJumpDown && clock != null && clock.Running && clock.EventPending && !clock.Serving && clock.Minute < clock.eventMinute - 1)
                clock.DebugSkipTo(clock.eventMinute - 1);
        }
#endif

        void Start()
        {
            if (display != null) display.Show(0);
            if (checklistView != null) checklistView.Bind(entries, day);
            if (autoLoop) StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            if (clock == null)
            {
                // 시계가 없으면 예전처럼 시험용으로 계속 손님을 받는다
                yield return new WaitForSeconds(firstDelay);
                while (true)
                {
                    yield return ServeOne(Environment.TickCount);
                    yield return new WaitForSeconds(nextDelay);
                }
            }

            // 메인 화면: 새로 시작 / 이어하기를 고를 때까지 기다린다
            bool resume = continueFromSave;
            if (mainMenu != null)
            {
                mainMenu.Show();
                while (mainMenu.Choice == MainMenu.Pick.None) yield return null;
                resume = mainMenu.Choice == MainMenu.Pick.Continue;
            }
            // 새로 시작: 오프닝(내 방에서 구인 공고 → 점장과 통화 → 비 오는 밤 가게로 걸어 들어감). 끝나면 검은 화면
            if (!resume && story != null) yield return story.PlayOpening(mainMenu);

            // 날마다: 계획(손님 10명·이상한 손님) → 시간표대로 받기 → 06:00 교대·퇴근·결과 → 저장 → 다음 날
            SaveData save = resume ? SaveSystem.Load() : new SaveData();
            day = Mathf.Clamp(save.nextDay, 1, lastDay);
            if (fate != null) fate.SetLookup(FindCustomerView);
            SaveData prevStart = null;   // 전날 시작 상태(근무 시작 때 체포되면 실종이 생긴 전날을 다시 한다)
            int prevDay = day;
            if (!string.IsNullOrEmpty(save.prevStartJson))
            {
                prevStart = JsonUtility.FromJson<SaveData>(save.prevStartJson);
                prevDay = Mathf.Max(1, day - 1);
            }
            while (true)
            {
                SaveData dayStart = save.Clone();   // 죽거나 잡히면 이 상태로 그날을 다시 한다
                if (fate != null) fate.LoadFrom(save);
                int seed = Environment.TickCount ^ (day * 7919);
                List<Visit> plan = NightPlanner.Plan(day, seed, customers, customersPerNight);
                entries.Clear();
                served = 0;
                currentEntry = null;
                if (checklistView != null) checklistView.Bind(entries, day);
                clock.BeginNight(day, seed);
                // 오늘 밤 03:33에 일어날 일(20가지 중 하나, 아직 안 나온 것)
                if (save.events333 == null) save.events333 = new List<int>();
                Ev333 tonight = event333 != null ? event333.Pick(day, save.events333, seed) : Ev333.None;
                if (event333 == null) clock.EventPending = false;
                bool eventDeath = false;
                // 근무 시작 전 점장 전화: 받을 때까지 00:00에 멈춰 있다. 수첩 수칙은 전날까지 들은 것만
                bool call = phone != null && phone.HasCall(day);
                if (call) clock.Running = false;
                if (notebook != null)
                {
                    if (phone != null) notebook.SetRules(phone.RulesUpTo(day - 1));
                    notebook.SetToday(day, !call, 0, customersPerNight);
                }
                // 어둠 속에서 먼저 전화벨이 울리고, 화면이 밝아진다(메인 화면에서 시작할 때도, 다음 날이 될 때도)
                Coroutine ring = call ? StartCoroutine(phone.Ring(day)) : null;
                bool dark = (mainMenu != null && mainMenu.Covering) || (shiftEnd != null && shiftEnd.Playing);
                if (ring != null && dark) yield return new WaitForSeconds(1.6f);
                if (mainMenu != null && mainMenu.Covering) yield return mainMenu.Reveal();
                if (shiftEnd != null && shiftEnd.Playing) yield return shiftEnd.FadeIn();   // 다음 날: 화면이 밝아지며 00:00
                if (call)
                {
                    yield return ring;
                    // 끊으면 오늘 들은 수칙이 수첩에 적힌다
                    if (notebook != null) { notebook.SetRules(phone.RulesUpTo(day)); notebook.SetToday(day, true, 0, customersPerNight); }
                    clock.Running = true;
                }

                // 실종이 있었다면 첫 손님보다 먼저 경찰이 온다(3명째면 체포 → 그날 다시)
                bool arrested = false;
                if (fate != null && fate.PoliceDue)
                {
                    clock.Running = false;
                    int replayDay = prevStart != null ? prevDay : day;
                    yield return PoliceVisit(replayDay, r => arrested = r);
                    clock.Running = true;
                }
                if (arrested)
                {
                    // 체포도 처음부터(사용자 결정): 기록을 DAY 1로 지우고 메인 화면으로
                    RestartFromDayOne(dayStart.deaths);
                    yield break;
                }

                while (!clock.NightOver)
                {
                    // 03:33: 시계가 멈추고 그날의 이벤트. "정상"으로 마감하거나 시간 안에 못 끝내면 게임 오버
                    if (clock.EventDue)
                    {
                        bool survived = true;
                        yield return event333.Run(tonight, seed ^ 0x333, r => survived = r);
                        if (!survived) { eventDeath = true; break; }
                    }
                    if (clock.CustomerDue)
                    {
                        int i = Mathf.Min(clock.NextIndex, plan.Count - 1);
                        clock.StartServing();
                        yield return ServeVisit(plan[i]);
                        clock.FinishServing();
                        if (notebook != null) notebook.SetToday(day, true, served, customersPerNight);
                    }
                    // 목숨이 다 깎였거나(흔적 3) 실종이 3명이 되면 그날 남은 손님은 오지 않는다: 시계가 미친 듯이 돌아 06:00.
                    // 그 뒤 목숨은 바로 괴물(죽음), 실종은 결과 화면 → 다음 근무 시작에 경찰이 와서 체포
                    if (fate != null && (fate.DeathDue || fate.ArrestDue))
                    {
                        yield return RushToMorning();
                        break;
                    }
                    yield return null;
                }
                Debug.Log($"[0333] DAY {day} 근무 끝(06:00), 손님 {served}명");

                // 흔적 3, 또는 03:33 거래를 잘못 끝냄: 괴물이 와서 나를 죽인다 → 메인 화면(DAY 1부터)
                if (eventDeath || (fate != null && fate.DeathDue))
                {
                    // 3:33에서 죽으면 이벤트 모습(손님·줄 등)이 남은 채 형광등이 나가고, 캄캄할 때 치운다
                    System.Action onDark = eventDeath && event333 != null ? event333.ClearInDark : (System.Action)null;
                    if (shiftEnd != null) yield return shiftEnd.PlayDeath(day, onDark, fromCurrentLights: eventDeath);
                    else onDark?.Invoke();
                    if (event333 != null) event333.ResetAll();
                    // 죽으면 처음부터(사용자 결정): 기록을 DAY 1로 지우고 메인 화면으로. 죽은 횟수는 하나 늘려 남긴다
                    RestartFromDayOne(dayStart.deaths + 1);
                    yield break;
                }

                // 교대 알바생 → 퇴근 → 결과 화면 → 다음 날 제목
                bool hasNext = day < lastDay;
                bool finalArrest = !hasNext && fate != null && fate.ArrestDue;
                string summary = fate != null ? Loc.F("흔적 {0} · 실종 {1}", fate.Traces, fate.Missing) : "";
                // 7일차 06:00까지 버텼다: 엔딩(교대 알바생이 처음으로 고개를 든다 → 결과 → 새벽 주차장 → 역할이 바뀐 통화)
                bool ending = !hasNext && !finalArrest && story != null;
                if (shiftEnd != null)
                    yield return shiftEnd.PlayEnd(day, entries, shiftWorker, hasNext, summary, endTitle: !finalArrest && !ending,
                                                  farewell: ending ? story.WorkerFarewell(shiftEnd, shiftWorker) : null);
                if (finalArrest)
                {
                    // 마지막 날이라 다음 근무가 없다: 결과 화면 뒤에 바로 경찰이 와서 잡아간다
                    CustomerView cop = FindCustomerView(policeId);
                    if (cop != null) cop.SetVisible(true);
                    if (shiftEnd != null) yield return shiftEnd.PlayArrest(day, cop);
                    RestartFromDayOne(dayStart.deaths);   // 체포도 처음부터
                    yield break;
                }

                // 자동 저장
                var rec = new DayRecord { day = day, seed = seed, entries = new List<ChecklistEntry>(entries) };
                foreach (var e in entries) if (e.Correct) rec.correct++;
                save.days.RemoveAll(d => d.day == day);
                save.days.Add(rec);
                save.nextDay = hasNext ? day + 1 : day;
                if (tonight != Ev333.None && !save.events333.Contains((int)tonight)) save.events333.Add((int)tonight);
                if (fate != null) fate.StoreTo(save);
                var startCopy = dayStart.Clone();
                startCopy.prevStartJson = "";
                save.prevStartJson = JsonUtility.ToJson(startCopy);
                SaveSystem.Save(save);

                if (!hasNext)
                {
                    if (ending)
                    {
                        yield return story.PlayEnding(mainMenu, MissingOf(save), LetOutOf(save));
                        // 엔딩을 봤다: 이어할 것은 없다(처음부터). 메인 화면으로 돌아간다
                        PlayerPrefs.SetInt(EndingSeenKey, 1);
                        PlayerPrefs.Save();
                        SaveSystem.Save(new SaveData());
                        StartCoroutine(Loop());
                    }
                    yield break;
                }
                prevStart = dayStart;
                prevDay = day;
                day++;
            }
        }

        /// <summary>
        /// 죽거나 체포되면 처음부터(사용자 결정): 세이브를 DAY 1로 지우고(죽은 횟수만 남겨 메인 화면 CCTV에 괴물이 비치게)
        /// 가게를 처음부터 다시 불러 메인 화면으로 돌아간다.
        /// </summary>
        void RestartFromDayOne(int deaths)
        {
            SaveSystem.Save(new SaveData { deaths = deaths });
            HandFx.Reset();
            ClockHud.Alarm = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>엔딩을 본 적이 있는지(PlayerPrefs).</summary>
        public const string EndingSeenKey = "0333.endingSeen";

        /// <summary>엔딩 새벽 주차장에 서 있을 실종자들(내가 "아니오"로 보낸 평범한 손님, 최대 2).</summary>
        static List<string> MissingOf(SaveData save)
        {
            var ids = new List<string>();
            if (save.missingIds != null) foreach (string id in save.missingIds) if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
            if (ids.Count > 2) ids.RemoveRange(2, ids.Count - 2);
            return ids;
        }

        /// <summary>엔딩 새벽 보도에 서 있을, 내가 "예"로 내보낸 이상한 손님들(흔적 수만큼, 최대 2).</summary>
        static List<string> LetOutOf(SaveData save)
        {
            var ids = new List<string>();
            int want = Mathf.Min(2, save.traces);
            if (save.days != null)
                for (int d = save.days.Count - 1; d >= 0 && ids.Count < want; d--)
                    foreach (var e in save.days[d].entries)
                        if (e.strange && e.verdict == Verdict.Normal && !string.IsNullOrEmpty(e.customerId) && !ids.Contains(e.customerId) && ids.Count < want)
                            ids.Add(e.customerId);
            return ids;
        }

        /// <summary>
        /// 실종을 알리러 경찰이 온다(근무 시작, 첫 손님보다 먼저). 실종 1: 사람이 사라졌다는 말,
        /// 2 이상: 화내며 "당신과 엮였다… 지켜보겠다", 3 이상: "당신 때문이야" → 체포. 다녀가면 전단이 붙는다.
        /// </summary>
        IEnumerator PoliceVisit(int replayDay, Action<bool> arrestedResult)
        {
            CustomerView cop = FindCustomerView(policeId);
            if (cop == null || shiftEnd == null)
            {
                fate.PoliceReported();
                arrestedResult(fate.ArrestDue);
                yield break;
            }
            cop.Enter();
            while (!cop.Visible || cop.Busy) yield return null;
            yield return new WaitForSeconds(0.4f);

            if (fate.ArrestDue)
            {
                yield return shiftEnd.PlayArrest(replayDay, cop);
                arrestedResult(true);
                yield break;
            }
            if (fate.Missing <= 1)
                yield return shiftEnd.Say("경찰", "오늘 밤 이 근처에서 사람이 사라졌습니다.", "혹시 이상한 걸 보시면 바로 연락 주세요.");
            else
                yield return shiftEnd.Say("경찰", "이 근처에서 실종된 사람들... 전부 이 편의점에 들렀더군요.", "당신이랑 엮여 있어요. ...당신을 지켜보겠습니다.");
            cop.Leave();
            while (cop.Busy) yield return null;
            fate.PoliceReported();   // 문 옆 유리창에 실종자 전단이 붙는다
            SoundFx.Play(sfx, SoundFx.Bill, 0.6f);
            arrestedResult(false);
        }

        /// <summary>남은 밤을 건너뛴다: 잠깐 정적 → 형광등이 웅 하고 시계가 점점 빨라지며 째깍째깍 06:00까지(약 3초).</summary>
        IEnumerator RushToMorning()
        {
            yield return new WaitForSeconds(0.8f);
            SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.6f);
            float lastTick = -1f;
            yield return clock.RushToEnd(3.2f, m =>
            {
                if (Time.time - lastTick < 0.05f) return;   // 째깍 소리는 너무 겹치지 않게
                lastTick = Time.time;
                SoundFx.Play(sfx, SoundFx.Tick, 0.35f);
            });
            SoundFx.Play(sfx, SoundFx.TubePop, 0.5f);
            yield return new WaitForSeconds(0.7f);
        }

        CustomerView FindCustomerView(string id)
        {
            foreach (var c in customers) if (c != null && c.customerId == id) return c;
            if (shiftWorker != null && shiftWorker.customerId == id) return shiftWorker;
            return null;
        }

        /// <summary>시험용(시계 없음): 손님 한 명을 랜덤 이상현상으로 계산한다.</summary>
        public IEnumerator ServeOne(int seed)
        {
            var rnd = new System.Random(seed);
            var v = new Visit { customer = PickCustomer(new System.Random(seed ^ 0x1234)), seed = seed };
            v.receipt = ChooseAnomalies(rnd);
            v.strange = v.receipt != ReceiptAnomaly.None;
            yield return ServeVisit(v, randomItems: true);
        }

        /// <summary>계획대로 손님 한 명을 계산한다(들어와서 나갈 때까지).</summary>
        public IEnumerator ServeVisit(Visit v, bool randomItems = false)
        {
            customer = v.customer;
            if (bay == null || customer == null) yield break;
            int seed = v.seed;
            if (event333 != null) event333.EndLinger();   // 3:33 목소리 뒤 남아 있던 이명·흔들림이 풀린다

            CurrentPhase = Phase.Arriving;
            // CCTV 이상현상: 안 찍힘·괴물이면 손님 자신은 CCTV에서 지운다(내 눈에는 그대로 보인다)
            customer.cctvHidden = v.cctv == CctvOdd.Missing || v.cctv == CctvOdd.Monster;
            customer.Enter();
            while (!customer.Visible || customer.Busy) yield return null;
            if (cctvAnomalies != null) cctvAnomalies.Show(v.cctv);   // 손님이 CCTV에 찍히는 순간 같이 켜진다
            yield return new WaitForSeconds(0.35f);

            // 물건 내려놓기: 이상한 물건은 계획한 개수만큼만(평범한 손님은 0개)
            CurrentPhase = Phase.Placing;
            var itemRnd = new System.Random(seed);
            List<(string id, bool anomaly)> bag = bay.PickRandom(itemRnd, 1, 8, randomItems ? anomalyChance : 0f);
            for (int k = 0; k < v.strangeItems && bag.Count > 0; k++)
            {
                int start = itemRnd.Next(bag.Count);
                for (int n = 0; n < bag.Count; n++)
                {
                    int idx = (start + n) % bag.Count;
                    if (bag[idx].anomaly) continue;
                    bag[idx] = (bag[idx].id, true);
                    break;
                }
            }
            var strangeNames = new List<string>();
            foreach (var b in bag)
                if (b.anomaly)
                {
                    ItemBay.ItemDef def = bay.Find(b.id);
                    string nm = def != null ? def.displayName : b.id;
                    if (!strangeNames.Contains(nm)) strangeNames.Add(nm);
                }
            bay.Place(bag, itemRnd);
            yield return DropItems();
            // 물건을 다 올려놓고 한마디. 말풍선 옆에 내 대답 2개가 뜨지만 계산은 바로 할 수 있다(화면도 그대로 움직인다)
            Coroutine converse = StartCoroutine(Converse(customer, seed));
            // 그 뒤로는 CCTV만 보거나 계산이 늦으면 손님마다 다르게 화를 낸다
            Coroutine talk = StartCoroutine(CustomerTalk(customer, seed));

            // 스캔
            Begin();
            CurrentPhase = Phase.Scanning;
            if (scanner != null) scanner.Ready = true;
            while (bay.Current.Count > 0) yield return null;
            if (scanner != null) scanner.Ready = false;

            // 영수증 + 돈
            CurrentPhase = Phase.Paying;
            yield return new WaitForSeconds(0.35f);
            int ledTotal = Current.total; // 금전등록기 숫자(이상현상이 바꾸지 않는 진짜 합계)
            ReceiptAnomaly flags = FinishReceipt(seed, forceAnomalies != ReceiptAnomaly.None ? forceAnomalies : v.receipt);
            // 결과 화면에 적을 이유(이상현상 1~2개)와 정답
            var fakeNames = new List<string>();
            foreach (var l in Current.lines) if (l.fake && !fakeNames.Contains(l.name)) fakeNames.Add(l.name);
            List<string> reasons = NightPlanner.Reasons(flags, string.Join(", ", fakeNames), string.Join(", ", strangeNames), v.cctv);
            if (currentEntry != null)
            {
                currentEntry.reasons = reasons.ToArray();
                currentEntry.strange = reasons.Count > 0;
            }
            if (receiptView != null) receiptView.Prepare(Current);
            if (tray != null)
                StartCoroutine((flags & ReceiptAnomaly.BodyPayment) != 0 ? tray.PayBodyParts(seed, sfx) : tray.Pay(ledTotal, seed, sfx));
            if (printer != null) yield return printer.Print(Current.lines.Count, sfx);

            // 점검표에서 "거래 종료하기"를 누를 때까지 기다린다
            CurrentPhase = Phase.AwaitReceipt;
            if (checklistView != null) checklistView.Ask();
            while (checklistView != null && (checklistView.Answer == Verdict.None || checklistView.IsOpen)) yield return null;
            while (receiptView != null && receiptView.IsOpen) yield return null;
            Verdict verdict = checklistView != null ? checklistView.Answer : Verdict.Normal;
            if (currentEntry != null) currentEntry.verdict = verdict;
            // 하트·사람 표시는 바로 줄어든다. 흔적은 손님이 나간 뒤 형광등이 깜빡일 때 생기고, 실종은 다음 근무 때 경찰이 알린다
            bool newTrace = fate != null && fate.OnVerdict(currentEntry);
            if (checklistView != null) checklistView.EndQuestion();

            // 손님은 영수증을 받아 나가고, 돈은 알바생이 챙긴다
            CurrentPhase = Phase.Leaving;
            if (talk != null) StopCoroutine(talk);
            if (converse != null) StopCoroutine(converse);
            if (choices != null) choices.Hide();
            if (bubble != null) bubble.Hide();
            // 나가면서 한마디(판정에 따라 다르다)
            //  정상→정상: 손님답게 인사 / 정상→비정상: 화내며 나간다
            //  비정상→정상: 눈과 입이 찢어지며 웃고 "감사합니다" / 비정상→비정상: 글자가 깨진 말
            bool strange = currentEntry != null && currentEntry.strange;
            var byeRnd = new System.Random(seed * 31 + 13);
            string bye = Lines.Pick(customer.customerId, CustomerLines.Farewell(strange, verdict), byeRnd);
            if (strange && verdict == Verdict.Normal && grin != null) yield return grin.Play(customer);
            bool caught = strange && verdict == Verdict.Abnormal;   // 들킨 손님: 글자가 네모·L자로 깨지고 위아래로 지지직한 획(잘고)
            if (caught && bye != null) bye = CustomerLines.Corrupt(bye, byeRnd);
            if (bye != null)
            {
                Say(customer, bye, 2f, caught);
                while (bubble != null && bubble.Typing) yield return null;
                yield return new WaitForSeconds(0.7f);
            }
            yield return new WaitForSeconds(0.25f);
            SoundFx.Play(sfx, SoundFx.Tear, 0.9f);
            if (printer != null) printer.Tear();
            yield return new WaitForSeconds(0.2f);
            SoundFx.Play(sfx, SoundFx.Bill, 0.7f);
            if (tray != null) tray.Clear();
            if (bubble != null) bubble.Hide();
            // 예(정상): 문이 열리며 딸랑 / 아니오(비정상): 문도 차임도 없이 조용히
            if (cctvAnomalies != null) cctvAnomalies.Clear();
            customer.Leave(silent: verdict == Verdict.Abnormal);
            while (customer.Busy) yield return null;
            customer.cctvHidden = false;
            // 변신하면서 불이 꺼진 채였다(어둠 속에서 말하고 나갔다)
            bool leftDark = grin != null && grin.LeftDark && grin.lights != null;
            if (grin != null) { grin.Hide(); grin.LeftDark = false; }
            if (display != null) display.Show(0);
            bay.Clear();
            if (leftDark)
            {
                // 아무도 없는 어둠 속에서 흔적(창밖 실루엣·피)이 생기고, 형광등이 틱틱 다시 켜진다
                yield return new WaitForSeconds(0.6f);
                if (newTrace && fate != null) fate.ShowTraces();
                yield return new WaitForSeconds(0.7f);
                yield return grin.lights.Relight();
            }
            // 이상한 손님을 통과시켰다: 형광등이 지지직 깜빡이다 잠깐 꺼진 사이에 흔적(창밖 실루엣·피)이 생긴다
            else if (newTrace) yield return TraceReveal();
            CurrentPhase = Phase.Idle;
        }

        // ── 손님 대사 ──
        /// <summary>
        /// 물건을 다 올려놓은 손님의 말: 먼저 인사 한마디. 그 뒤 손님이 나갈 때까지
        /// CCTV를 cctvAngrySeconds 넘게 계속 보면 "CCTV" 대사(계속 보면 또), 계산이 늦으면 "늦음1"→"늦음2".
        /// </summary>
        IEnumerator CustomerTalk(CustomerView who, int seed)
        {
            var rnd = new System.Random(seed * 31 + 11);
            string id = who.customerId;
            float start = Time.time, watchSince = -1f;
            int slow = 0;
            string lastCctv = null;
            while (CurrentPhase != Phase.Leaving && CurrentPhase != Phase.Idle)
            {
                bool watching = cctv != null && cctv.IsOpen;
                if (!watching) watchSince = -1f;
                else if (watchSince < 0f) watchSince = Time.time;

                if (watching && Time.time - watchSince >= cctvAngrySeconds)
                {
                    lastCctv = Lines.Pick(id, CustomerLines.Cctv, rnd, lastCctv);
                    Say(who, lastCctv);
                    watchSince = Time.time;   // 그래도 계속 보고 있으면 또 한마디
                }
                else if (bubble == null || !bubble.Showing)
                {
                    float waited = Time.time - start;
                    if (slow == 0 && waited >= slowFirst) { slow = 1; Say(who, Lines.Pick(id, CustomerLines.Slow1, rnd)); }
                    else if (slow == 1 && waited >= slowSecond) { slow = 2; Say(who, Lines.Pick(id, CustomerLines.Slow2, rnd)); }
                }
                yield return null;
            }
        }

        /// <summary>
        /// 인사 한마디. 그 인사에 선택지가 있으면 말풍선을 띄워 둔 채 대답 2개를 보여 주고,
        /// 고르면 손님이 그에 맞게 답한다(답변 말풍선은 계산하는 동안 잠깐 남는다).
        /// </summary>
        IEnumerator Converse(CustomerView who, int seed)
        {
            var rnd = new System.Random(seed * 31 + 7);
            CustomerLines.Greeting g = Lines.PickGreeting(who.customerId, rnd);
            if (g == null) yield break;
            bool ask = choices != null && bubble != null && g.choices.Count >= 2;
            Say(who, g.text, ask ? choiceSeconds : -1f);
            if (!ask) yield break;
            while (bubble.Typing) yield return null;
            choices.ShowBeside(bubble, g.choices[0].reply, g.choices[1].reply);
            // 고르거나, 말풍선이 사라지거나(안 고르고 넘어감), 다른 말(CCTV·늦음)로 바뀔 때까지
            while (choices.Showing && choices.Answer < 0 && bubble.Showing && bubble.Current == g.text) yield return null;
            int pick = choices.Answer;
            choices.Hide();
            if (pick >= 0) Say(who, g.choices[pick].answer);
        }

        /// <summary>스토리 장면용: 대사창처럼 강제로 멈추고 대답을 고르게 한다(고른 번호를 돌려준다).</summary>
        public IEnumerator AskForced(string first, string second, System.Action<int> picked)
        {
            if (choices == null) { picked?.Invoke(0); yield break; }
            choices.ShowForced(first, second);
            while (choices.Answer < 0) yield return null;
            int pick = choices.Answer;
            choices.Hide();
            picked?.Invoke(pick);
        }

        void Say(CustomerView who, string line, float hold = -1f, bool zalgo = false)
        {
            if (bubble == null || who == null || string.IsNullOrEmpty(line)) return;
            bubble.Say(() => Head(who), line, hold, zalgo);
        }

        /// <summary>손님 머리 꼭대기(월드 좌표). 그림 위쪽의 빈 칸은 빼고 실제로 그려진 맨 윗줄을 쓴다.</summary>
        static Vector3 Head(CustomerView who)
        {
            SpriteRenderer sr = who.counterBody;
            if (sr == null || sr.sprite == null) return who.transform.position;
            Sprite s = sr.sprite;
            Bounds b = s.bounds;
            float top = b.max.y;
            if (!headTop.TryGetValue(s, out int row)) headTop[s] = row = TopOpaqueRow(s);
            if (row >= 0) top = b.min.y + (row + 1) / s.pixelsPerUnit;
            return sr.transform.TransformPoint(new Vector3(b.center.x, top, 0f));
        }

        static readonly Dictionary<Sprite, int> headTop = new Dictionary<Sprite, int>();

        /// <summary>그림에서 불투명한 맨 윗줄(아래에서부터 몇 번째 줄). 읽을 수 없으면 -1.</summary>
        static int TopOpaqueRow(Sprite s)
        {
            Texture2D tex = s.texture;
            if (tex == null || !tex.isReadable) return -1;
            Rect r = s.textureRect;
            int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height;
            Color32[] px = tex.GetPixels32();
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                    if (px[(y0 + y) * tex.width + x0 + x].a > 40) return y;
            return -1;
        }

        /// <summary>미리보기: 손님 하나를 세우고 그 종류의 대사를 말풍선으로(CCTV를 열어 둘 수도 있다).</summary>
        public void PreviewSpeech(string customerId, string kind, bool withCctv)
        {
            CustomerView who = FindCustomerView(customerId) ?? customer;
            if (customer != null) customer.SetVisible(false);
            who.SetVisible(true);
            if (withCctv && cctv != null && cctv.panel != null) cctv.panel.SetActive(true);
            lines = null;   // 대사 파일을 다시 읽는다
            Say(who, Lines.Pick(who.customerId, kind, new System.Random(1)));
        }

        /// <summary>미리보기: 손님이 인사하고 내가 고를 대답 2개가 떠 있는 순간.</summary>
        public void PreviewChoice(string customerId)
        {
            CustomerView who = FindCustomerView(customerId) ?? customer;
            if (customer != null) customer.SetVisible(false);
            who.SetVisible(true);
            lines = null;
            CustomerLines.Greeting g = Lines.PickGreeting(who.customerId, new System.Random(1));
            if (g == null) return;
            Say(who, g.text, float.PositiveInfinity);
            if (choices != null && g.choices.Count >= 2) choices.ShowBeside(bubble, g.choices[0].reply, g.choices[1].reply);
        }

        /// <summary>미리보기: 거래 종료 뒤 나가면서 하는 말(비정상→정상이면 찢어진 웃음, 비정상→비정상이면 깨진 글자).</summary>
        public void PreviewFarewell(string customerId, bool strange, Verdict verdict)
        {
            CustomerView who = FindCustomerView(customerId) ?? customer;
            if (customer != null) customer.SetVisible(false);
            who.SetVisible(true);
            lines = null;
            var rnd = new System.Random(3);
            string bye = Lines.Pick(who.customerId, CustomerLines.Farewell(strange, verdict), rnd);
            if (strange && verdict == Verdict.Normal && grin != null) grin.Show(who, 1f);
            bool caught = strange && verdict == Verdict.Abnormal;
            if (caught && bye != null) bye = CustomerLines.Corrupt(bye, rnd);
            Say(who, bye, -1f, caught);
        }

        public void PreviewSpeechReset(string customerId)
        {
            if (grin != null) grin.Hide();
            if (choices != null) choices.Hide();
            if (bubble != null) bubble.Hide();
            if (cctv != null && cctv.panel != null) cctv.panel.SetActive(false);
            CustomerView who = FindCustomerView(customerId);
            if (who != null) who.SetVisible(false);
        }

        IEnumerator TraceReveal()
        {
            yield return new WaitForSeconds(0.6f);
            StoreLights lights = shiftEnd != null ? shiftEnd.lights : null;
            if (lights != null) yield return lights.Glitch(fate.ShowTraces);
            else fate.ShowTraces();
        }

        /// <summary>손님이 물건을 하나씩 툭툭 내려놓는다.</summary>
        IEnumerator DropItems()
        {
            var list = new List<ItemBay.Placed>(bay.Current);
            foreach (var p in list) if (p.renderer != null) p.renderer.enabled = false;
            foreach (var p in list)
            {
                if (p.renderer == null) continue;
                Transform t = p.renderer.transform;
                Vector3 home = t.position;
                p.renderer.enabled = true;
                const float fall = 0.08f;
                for (float e = 0f; e < fall; e += Time.deltaTime)
                {
                    float k = e / fall;
                    t.position = home + new Vector3(0f, Mathf.Round(6f * (1f - k * k)), 0f);
                    yield return null;
                }
                t.position = home;
                SoundFx.Play(sfx, SoundFx.Thunk, 0.8f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        /// <summary>영수증의 손님 설명(사람 수 빼고). 예: "후드 청년"</summary>
        string PersonLabel => customer != null && !string.IsNullOrEmpty(customer.receiptLabel) ? customer.receiptLabel : "손님";
        /// <summary>같이 온 사람 수(커플·일행 2).</summary>
        int Party => customer != null ? Mathf.Max(1, customer.partySize) : 1;

        /// <summary>
        /// 다 찍은 뒤 영수증을 마무리한다: 이상현상을 고르고 적용(시각·번호·손님 설명·합계·없는 상품·피·받은 돈).
        /// 프린터에 피 묻음을 알려 주고, 점검표 기록에도 남긴다(정답 확인용).
        /// </summary>
        ReceiptAnomaly FinishReceipt(int seed, ReceiptAnomaly? forced = null)
        {
            var rnd = new System.Random(seed ^ 0x0333);
            ReceiptAnomaly flags = forced ?? ChooseAnomalies(rnd);
            ReceiptAnomalies.Apply(Current, flags, rnd, PersonLabel, Party);
            if (currentEntry != null) currentEntry.receiptAnomalies = flags;
            if (printer != null) { printer.bloody = Current.bloody; printer.bloodSeed = Current.bloodSeed; }
            return flags;
        }

        ReceiptAnomaly ChooseAnomalies(System.Random rnd)
        {
            if (forceAnomalies != ReceiptAnomaly.None) return forceAnomalies;
            if (rnd.NextDouble() >= receiptAnomalyChance) return ReceiptAnomaly.None;
            return ReceiptAnomalies.Pick(rnd, rnd.NextDouble() < doubleAnomalyChance ? 2 : 1);
        }

        /// <summary>새 영수증을 연다(번호·시각을 매긴다).</summary>
        void Begin()
        {
            served++;
            int minute = startMinute + minutesPerCustomer * served;
            Current = new ReceiptData
            {
                day = day,
                number = served,
                // 영수증·점검표 시각 = 근무 시계(없으면 임시 계산)
                time = clock != null ? clock.Format() : $"{minute / 60 % 24:00}:{minute % 60:00}",
                visitorText = $"{PersonLabel} {Party}",
            };
            currentEntry = new ChecklistEntry
            {
                number = served, time = Current.time, verdict = Verdict.None,
                customerId = customer != null ? customer.customerId : "", label = $"{PersonLabel} {Party}", view = customer,
            };
            entries.Add(currentEntry);
            if (checklistView != null) checklistView.SetCurrent(served);
            if (display != null) display.Show(0);
        }

        /// <summary>물건 하나가 스캐너에 찍혔을 때(ItemDragger가 부른다).</summary>
        public void Scan(SpriteRenderer item)
        {
            if (item == null || bay == null) return;
            ItemBay.Placed placed = default;
            bool found = false;
            foreach (var p in bay.Current)
                if (p.renderer == item) { placed = p; found = true; break; }
            if (!found) return;

            ItemBay.ItemDef def = bay.Find(placed.id);
            int price = scanPriceOverride >= 0 ? scanPriceOverride : PriceOf(placed.id);
            if (Current == null) Begin();
            Current.Add(placed.id, def != null ? def.displayName : placed.id, price);
            bay.Remove(item);

            if (display != null) display.Show(Current.total);
            if (scanner != null) scanner.Flash();
            if (scanSoundOverride != null) SoundFx.Play(sfx, scanSoundOverride, 0.9f);
            else SoundFx.Play(sfx, SoundFx.Beep, 0.8f);
        }

        public void PlayPickUp() => SoundFx.Play(sfx, SoundFx.PickUp, 0.7f);

        /// <summary>금전등록기에서 나온 영수증을 클릭했을 때(핫스팟).</summary>
        public void OpenReceipt()
        {
            if (receiptView == null || printer == null || !printer.Printed) return;
            receiptView.Open();
        }

        public int PriceOf(string id)
        {
            foreach (var p in prices) if (p.id == id) return p.won;
            return 1000;
        }

        // ── 에디터 미리보기 ───────────────────────────────────
        /// <summary>
        /// 배치 모드에서 화면을 찍을 때 쓴다. stage 0: 반쯤 스캔한 모습(물건 하나를 스캐너 위로 끄는 중),
        /// 1: 다 찍고 영수증·돈이 나온 모습, 2: 영수증 크게 보기,
        /// 3: 점검표(아니오에 체크만 한 상태), 4: 점검표(예 체크 후 거래 종료하기를 누른 순간).
        /// 점검표에 기록이 보이도록 앞서 두 손님(정상 1, 비정상 1)을 받은 것으로 둔다.
        /// </summary>
        public void PreviewStage(int stage, IList<(string id, bool anomaly)> bag, int seed, Vector2 dragPos)
        {
            if (stage == 0)
            {
                customer.SetVisible(true);
                bay.Place(bag, new System.Random(seed));
                entries.Clear();
                entries.Add(new ChecklistEntry { number = 1, time = "00:17", verdict = Verdict.Normal });
                entries.Add(new ChecklistEntry { number = 2, time = "00:34", verdict = Verdict.Abnormal });
                served = 2;
                Begin();
                int scanCount = bay.Current.Count / 2;
                for (int i = 0; i < scanCount; i++) Scan(bay.Current[0].renderer);
                if (bay.Current.Count > 0)
                {
                    SpriteRenderer held = bay.Current[bay.Current.Count - 1].renderer;
                    held.transform.position = new Vector3(dragPos.x, dragPos.y, 0f);
                    held.sortingOrder = 90;
                }
                if (scanner != null) scanner.PreviewFlash(true);
            }
            else if (stage == 1)
            {
                while (bay.Current.Count > 0) Scan(bay.Current[0].renderer);
                if (scanner != null) scanner.PreviewFlash(false);
                Current.paid = Current.total;
                printer.SetPrinted(Current.lines.Count);
                tray.ShowInstant(Current.paid, seed);
            }
            else if (stage == 2)
            {
                receiptView.ShowPreview(Current);
            }
            else
            {
                receiptView.panel.SetActive(false);
                if (stage == 3) checklistView.ShowPreview(entries, day, Current.number, true, Verdict.Abnormal, Verdict.None);
                else checklistView.ShowPreview(entries, day, Current.number, true, Verdict.Normal, Verdict.Normal);
            }
        }

        /// <summary>
        /// 미리보기: 물건을 다 찍은 뒤 정해진 이상현상(flags)을 넣은 모습. 계산대 화면(피 묻은 영수증, 돈바구니)을 찍은 뒤
        /// receiptView.ShowPreview(Current)로 크게 본 영수증도 찍는다.
        /// </summary>
        public void PreviewAnomalies(IList<(string id, bool anomaly)> bag, int seed, ReceiptAnomaly flags)
        {
            PreviewReset();
            customer.SetVisible(true);
            bay.Place(bag, new System.Random(seed));
            served = 3;
            Begin();
            while (bay.Current.Count > 0) Scan(bay.Current[0].renderer);
            if (scanner != null) scanner.PreviewFlash(false);
            int ledTotal = Current.total;
            FinishReceipt(seed, flags);
            printer.SetPrinted(Current.lines.Count);
            if ((flags & ReceiptAnomaly.BodyPayment) != 0) tray.ShowBodyPartsInstant(seed);
            else tray.ShowInstant(ledTotal, seed);
        }

        /// <summary>
        /// 미리보기: 근무 끝 장면. stage 0: 교대 알바생 대사, 1: 결과 화면(가짜 기록 10명), 2: 다음 날 제목.
        /// </summary>
        public void PreviewShiftEnd(int stage)
        {
            if (shiftEnd == null) return;
            if (stage == 0)
            {
                if (shiftWorker != null) shiftWorker.SetVisible(true);
                shiftEnd.PreviewDialogue();
                return;
            }
            if (shiftWorker != null) shiftWorker.SetVisible(false);
            if (stage == 2) { shiftEnd.PreviewTitle(2); return; }

            // 가짜 하룻밤: 이상한 손님 3명, 판정은 일부러 두 개 틀리게
            var rnd = new System.Random(5);
            var plan = NightPlanner.Plan(3, 5, customers, customersPerNight);
            var records = new List<ChecklistEntry>();
            int[] times = { 15, 46, 77, 112, 139, 171, 228, 268, 313, 341 };
            for (int i = 0; i < plan.Count; i++)
            {
                Visit v = plan[i];
                string items = v.strangeItems > 0 ? (rnd.Next(2) == 0 ? "생수" : "김밥, 소주") : "";
                List<string> reasons = NightPlanner.Reasons(v.receipt, "피생수", items);
                bool strange = reasons.Count > 0;
                bool wrong = i == 2 || i == 6;
                var e = new ChecklistEntry
                {
                    number = i + 1, time = NightClock.Format(times[i]), customerId = v.customerId,
                    label = v.customer != null ? $"{v.customer.receiptLabel} {Mathf.Max(1, v.customer.partySize)}" : "?",
                    view = v.customer, strange = strange, reasons = reasons.ToArray(),
                    verdict = (strange != wrong) ? Verdict.Abnormal : Verdict.Normal,
                };
                records.Add(e);
            }
            shiftEnd.PreviewResults(3, records);
        }

        /// <summary>
        /// 미리보기: 판정 결과. 0: 흔적 1(창밖 실루엣, 하트 2), 1: 흔적 2(피범벅, 하트 1),
        /// 2: 실종 2(경찰 대사, 전단 2장), 3: 괴물이 올라오는 순간, 4: 죽음 화면, 5: 체포 화면.
        /// </summary>
        public void PreviewFate(int stage)
        {
            if (fate == null || shiftEnd == null) return;
            fate.SetLookup(FindCustomerView);
            CustomerView cop = FindCustomerView(policeId);
            var ids = new List<string>();
            foreach (var c in customers) if (c != null && (c.customerId == "office" || c.customerId == "student")) ids.Add(c.customerId);
            switch (stage)
            {
                case 0: fate.PreviewSet(1, 0, 0, new string[0]); customer.SetVisible(true); break;
                case 1: fate.PreviewSet(2, 0, 0, new string[0]); customer.SetVisible(true); break;
                case 2:
                    customer.SetVisible(false);
                    fate.PreviewSet(0, 2, 2, ids);
                    if (cop != null) cop.SetVisible(true);
                    shiftEnd.PreviewDialogue("경찰", "당신이랑 엮여 있어요. ...당신을 지켜보겠습니다.");
                    break;
                case 3:
                    if (cop != null) cop.SetVisible(false);
                    fate.PreviewSet(2, 0, 0, new string[0]);
                    shiftEnd.PreviewMonster();
                    break;
                case 4: shiftEnd.PreviewDeathButton(true); break;   // 죽으면 "메인 화면으로" 버튼(마우스를 올린 모습)
                default: shiftEnd.PreviewDeathButton(false, "체포되었습니다"); break;   // 체포도 "메인 화면으로" 버튼
            }
        }

        public void PreviewFateReset()
        {
            if (fate != null) fate.PreviewSet(0, 0, 0, new string[0]);
            if (shiftEnd != null) shiftEnd.PreviewHide();
            CustomerView cop = FindCustomerView(policeId);
            if (cop != null) cop.SetVisible(false);
            if (customer != null) customer.SetVisible(false);
        }

        /// <summary>미리보기로 바꾼 것들을 되돌린다.</summary>
        public void PreviewReset()
        {
            bay.Clear();
            tray.Clear();
            printer.Tear();
            if (receiptView.panel != null) receiptView.panel.SetActive(false);
            if (checklistView != null && checklistView.panel != null) checklistView.panel.SetActive(false);
            entries.Clear();
            currentEntry = null;
            if (scanner != null && scanner.target != null) { scanner.target.enabled = false; scanner.target.sprite = null; }
            if (display != null && display.target != null) display.target.sprite = null;
            if (printer.target != null) printer.target.sprite = null;
            customer.SetVisible(false);
            if (shiftWorker != null) shiftWorker.SetVisible(false);
            if (shiftEnd != null) shiftEnd.PreviewHide();
            Current = null;
        }
    }
}
