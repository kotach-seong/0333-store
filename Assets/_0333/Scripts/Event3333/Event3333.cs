using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>3:33 이벤트 20가지(사용자가 정한 목록과 번호가 같다).</summary>
    public enum Ev333
    {
        None = 0,
        Unmanned = 1,      // 무인 거래
        Returner = 2,      // 다시 온 손님
        Voice = 3,         // 목소리
        Queue = 4,         // 줄 선 사람들
        Door = 5,          // 자동문
        NoProduct = 6,     // 없는 상품
        Phone = 7,         // 점장 전화
        Backward = 8,      // 거꾸로 가는 시간
        Plead = 9,         // 애원하는 손님
        Hand = 10,         // 계산대 밑의 손
        Receipt = 11,      // 끊이지 않는 영수증
        CctvStare = 12,    // 나를 보는 CCTV
        Bleed = 13,        // 피 흘리는 물건
        CctvNoMe = 14,     // CCTV에 내가 안 보임
        CctvStatic = 15,   // CCTV 지지직 + 이상한 소리
        Predecessor = 16,  // 전임자
        Mirror = 17,       // 거울 가게
        MissingBack = 18,  // 돌아온 실종자
        RedSight = 19,     // 빨개지는 시야
        Whisper = 20,      // 속삭임
    }

    /// <summary>
    /// 03:33 이벤트. 밤마다 20가지 중 하나를 랜덤으로 고른다(7일 동안 같은 건 다시 안 나온다, 센 것은 늦은 날부터).
    /// 공통 규칙(사용자 결정): 3:33의 거래는 항상 "비정상"으로 체크하고 "거래 종료하기"로 끝낸다. "정상"으로 끝내면 게임 오버.
    ///  - 시작: 시계가 03:33에서 멈추고 형광등이 깜빡인다.
    ///  - 물건이 올라오고(손님이 보이든 안 보이든) 찍으면, 겁주는 연출이 끝나야 영수증이 나온다(그전엔 판정을 못 한다).
    ///  - 시간 제한이 있는 이벤트(1번 30초, 8번 03:00까지)는 못 끝내면 게임 오버.
    /// CheckoutSystem이 03:33에 Run을 부르고, 실패면 형광등이 나가며 괴물 → 그날 다시.
    /// </summary>
    public partial class Event3333 : MonoBehaviour
    {
        [Header("연결")]
        public CheckoutSystem checkout;
        public NightClock clock;
        public StoreLights lights;
        public DoorController door;
        public PhoneCall phone;
        public CctvSystem cctv;
        public SpeechBubble bubble;
        public ChoiceView choices;
        public MouseLookCamera look;
        public ScreenFx fx;
        public FateSystem fate;
        public AudioSource sfx;
        [Tooltip("이벤트 대사(Data/Event3333.txt)")]
        public TextAsset linesFile;

        [Header("말풍선 자리(월드 픽셀)")]
        [Tooltip("보이지 않는 손님의 머리가 있을 자리(계산대 앞)")]
        public Vector3 counterHead = new Vector3(0f, 0f, 0f);
        [Tooltip("자동문 위(문 밖 목소리)")]
        public Vector3 doorTop = new Vector3(0f, 34f, 0f);

        [Header("시험용")]
        [Tooltip("골라 두면 매일 이 이벤트만 나온다")]
        public Ev333 force = Ev333.None;

        /// <summary>오늘 밤 고른 이벤트.</summary>
        public Ev333 Tonight { get; private set; }
        /// <summary>이벤트가 진행 중.</summary>
        public bool Running { get; private set; }

        /// <summary>센 이벤트는 늦은 날부터(번호 순서).</summary>
        static readonly int[] MinDay = { 0, 1, 1, 1, 1, 1, 1, 1, 3, 3, 3, 3, 3, 3, 3, 3, 5, 5, 5, 5, 5 };

        /// <summary>거래 하나의 상태.</summary>
        class Txn
        {
            public bool placed, gate, fail, done, endless;
            public bool coins = true, bodyParts;
            public float deadline = -1f;
            public string visitor = "방문자 1";
            public Verdict verdict = Verdict.None;
            public CustomerView who;
        }

        Txn tx;
        System.Random rnd = new System.Random();
        bool linger;   // 3번(목소리): 끝난 뒤에도 다음 손님까지 흔들림·이명이 남는다
        readonly List<GameObject> spawned = new List<GameObject>();

        // ── 고르기 ──
        /// <summary>오늘 밤 이벤트를 고른다: 그날 열린 것 중 아직 안 나온 것(없으면 다시 전부). 실종자가 없으면 18번은 빠진다.</summary>
        public Ev333 Pick(int day, IList<int> used, int seed)
        {
            if (force != Ev333.None) return force;
            var r = new System.Random(seed);
            int missing = fate != null ? fate.MissingIds.Count : 0;
            var open = new List<Ev333>();
            for (int i = 1; i < MinDay.Length; i++)
            {
                var e = (Ev333)i;
                if (MinDay[i] > day) continue;
                if (e == Ev333.MissingBack && missing == 0) continue;
                open.Add(e);
            }
            var fresh = open.FindAll(e => used == null || !used.Contains((int)e));
            var from = fresh.Count > 0 ? fresh : open;
            return from.Count > 0 ? from[r.Next(from.Count)] : Ev333.Unmanned;
        }

        // ── 진행 ──
        /// <summary>03:33 이벤트 하나를 처음부터 끝까지. result(true)면 무사히 넘김, false면 게임 오버.</summary>
        public IEnumerator Run(Ev333 ev, int seed, Action<bool> result)
        {
            Tonight = ev;
            Running = true;
            rnd = new System.Random(seed);
            tx = new Txn();
            EndLinger();
            clock.Held = true;
            clock.SetMinute(clock.eventMinute);
            Debug.Log($"[0333] 03:33 이벤트 {(int)ev} {ev}");

            yield return Opening(ev);
            Coroutine body = StartCoroutine(Body(ev));
            yield return Transaction();
            tx.done = true;
            if (body != null) StopCoroutine(body);
            if (checkout.checklistView != null && checkout.checklistView.IsOpen) checkout.checklistView.Close();
            if (checkout.receiptView != null && checkout.receiptView.IsOpen) checkout.receiptView.Close();

            bool ok = !tx.fail && tx.verdict == Verdict.Abnormal;
            if (ok) yield return Success(ev);
            else yield return Failure(ev);
            checkout.EndSpecial();
            clock.EventPending = false;
            if (ok)
            {
                clock.Held = false;
                clock.SetMinute(clock.eventMinute + 1);
            }
            // 실패면 시계는 멈춘 채 둔다(죽는 연출 동안 흐르지 않게, 그날을 다시 할 때 풀린다)
            Running = false;
            result?.Invoke(ok);
        }

        /// <summary>
        /// 거래: 물건이 올라오면 찍게 하고, 다 찍고 연출이 끝나면(gate) 영수증이 나온다 → 점검표에서 판정.
        /// 실패 조건(시간 초과 등)이 생기면 바로 끝낸다.
        /// </summary>
        IEnumerator Transaction()
        {
            while (!tx.placed && !Failed()) yield return null;
            if (tx.fail) yield break;
            checkout.BeginSpecial("03:33", 333, tx.visitor);
            checkout.SetPhase(CheckoutSystem.Phase.Scanning);
            if (checkout.scanner != null) checkout.scanner.Ready = true;
            while (checkout.bay.Current.Count > 0 && !Failed()) yield return null;
            if (checkout.scanner != null) checkout.scanner.Ready = false;
            while (!tx.gate && !Failed()) yield return null;
            if (tx.fail) yield break;

            // 영수증 + 돈(보이지 않는 손님은 돈이 저절로 놓인다)
            checkout.SetPhase(CheckoutSystem.Phase.Paying);
            ReceiptData r = checkout.Current;
            if (checkout.receiptView != null) checkout.receiptView.Prepare(r);
            if (checkout.tray != null)
            {
                if (tx.bodyParts) { }   // 10번: 손이 직접 올려놓는다
                else if (tx.coins) StartCoroutine(checkout.tray.Pay(r.total, rnd.Next(), sfx));
            }
            if (tx.endless) StartCoroutine(checkout.printer.PrintEndless(sfx, () => tx.done));
            else yield return checkout.printer.Print(r.lines.Count, sfx);
            while (tx.endless && !checkout.printer.Printed && !Failed()) yield return null;

            checkout.SetPhase(CheckoutSystem.Phase.AwaitReceipt);
            ChecklistView sheet = checkout.checklistView;
            if (sheet != null) sheet.Ask();
            while (!Failed() && sheet != null && (sheet.Answer == Verdict.None || sheet.IsOpen)) yield return null;
            tx.verdict = sheet != null ? sheet.Answer : Verdict.Abnormal;
        }

        bool Failed()
        {
            // 시간 제한: "거래 종료하기"를 이미 눌렀으면(점검표가 닫히는 0.5초 사이) 늦은 게 아니다
            if (tx.deadline > 0f && Time.time >= tx.deadline && !Answered) tx.fail = true;
            return tx.fail;
        }

        /// <summary>점검표에서 "거래 종료하기"를 눌렀다(닫히는 중일 수 있음).</summary>
        bool Answered => checkout.checklistView != null && checkout.checklistView.Answer != Verdict.None;

        /// <summary>
        /// 03:33: 시계가 멈추고, 안쪽 줄부터 형광등이 지지직대며 하나씩 나가 계산대 위 앞줄만 남는다(CCTV도 어두운 가게).
        /// 남은 앞줄이 한 번 꺼졌다 켜진다(거울 가게는 이 어둠 속에서 뒤집힌다). 3:33의 거래는 늘 이 어둠 속에서 한다.
        /// </summary>
        IEnumerator Opening(Ev333 ev)
        {
            if (lights == null)
            {
                if (cctv != null) cctv.Night = true;
                if (ev == Ev333.Mirror && fx != null) fx.Mirror = true;
                yield break;
            }
            yield return lights.DownToFront(() => { if (cctv != null) cctv.Night = true; });
            yield return new WaitForSeconds(0.35f);
            lights.Set(StoreLights.FrontRow, 0f);
            SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.8f);
            if (GameSettings.ReduceFlicker) yield return lights.UntilDark();   // 깜빡임 줄이기: 서서히 다 꺼진 뒤에
            if (ev == Ev333.Mirror && fx != null) fx.Mirror = true;   // 꺼진 사이에 뒤집힌다
            yield return new WaitForSeconds(0.45f);
            SoundFx.Play(sfx, SoundFx.TubeTick, 0.9f);
            lights.Set(StoreLights.FrontRow, 1f);
            yield return new WaitForSeconds(0.5f);
        }

        // ── 끝 ──
        /// <summary>무사히 넘겼다: 이벤트마다의 끝맺음 → 남은 것이 사라지고 → 꺼졌던 등이 틱틱 다시 켜진다(CCTV도 평소로).</summary>
        IEnumerator Success(Ev333 ev)
        {
            yield return Ending(ev, true);
            Cleanup(keepLinger: ev == Ev333.Voice, relight: false);
            yield return new WaitForSeconds(0.5f);
            if (lights != null) yield return lights.UpFromFront(() => { if (cctv != null) cctv.Night = false; });
            if (cctv != null) cctv.Night = false;
        }

        /// <summary>
        /// 실패: 겁주던 소리·흔들림·붉은 막이 뚝 멎는다. 가게 안 모습(손님·줄·물건)은 그대로 둔 채
        /// 남은 앞줄 등마저 나가고(죽는 연출), 완전히 어두워졌을 때 ClearInDark로 치운다. 거울은 어둠 속에서 돌아온다.
        /// </summary>
        IEnumerator Failure(Ev333 ev)
        {
            if (bubble != null) bubble.Hide();
            if (choices != null) choices.Hide();
            ClockHud.Alarm = false;
            if (fx != null) fx.ResetAll(keepMirror: true);
            if (lights != null) lights.Set(StoreLights.FrontRow, 1f);   // 깜빡이던 앞줄은 켜진 채로(이제 이것마저 나간다)
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>죽는 연출에서 형광등이 다 나가 캄캄할 때: 남은 것을 치운다(불은 켜지 않는다).</summary>
        public void ClearInDark()
        {
            if (tx == null) return;
            Cleanup(false, relight: false);
        }

        /// <summary>이벤트가 끝난 뒤 남은 것을 모두 치운다(손님·물건·영수증·연출).</summary>
        void Cleanup(bool keepLinger, bool relight = true)
        {
            if (bubble != null) bubble.Hide();
            if (choices != null) choices.Hide();
            if (tx.who != null)
            {
                RestoreFace(tx.who);
                if (tx.who.Visible) tx.who.SetVisible(false);
            }
            if (checkout.printer != null) checkout.printer.Tear();
            if (checkout.tray != null) checkout.tray.Clear();
            checkout.bay.Clear();
            if (checkout.display != null) checkout.display.Show(0);
            checkout.scanSoundOverride = null;
            checkout.scanPriceOverride = -1;
            if (checkout.receiptView != null) checkout.receiptView.tailRows = 0;
            ClockHud.Alarm = false;
            if (phone != null && phone.dialog != null) phone.dialog.warpVoice = false;   // 늘어지던 점장 목소리는 다음 전화엔 평소대로
            HideAllArt();
            foreach (var g in spawned) if (g != null) Destroy(g);
            spawned.Clear();
            if (relight)
            {
                if (lights != null) lights.SetAll(1f);
                if (cctv != null) cctv.Night = false;
            }
            if (keepLinger && fx != null)
            {
                // 목소리: 다음 손님이 올 때까지 화면이 조금 흔들리고 이명이 남는다
                linger = true;
                fx.shake = 0.3f;
                fx.Loop(0, SoundFx.Tinnitus, 0.22f);
                fx.Loop(1, null);
                HandFx.Reset();
            }
            else if (fx != null) fx.ResetAll();
            checkout.SetPhase(CheckoutSystem.Phase.Idle);
        }

        /// <summary>남아 있던 이명·흔들림을 끈다(다음 손님이 올 때, 그날을 다시 할 때).</summary>
        public void EndLinger()
        {
            if (!linger) return;
            linger = false;
            if (fx != null) fx.ResetAll();
        }

        /// <summary>죽거나 그날을 다시 할 때: 진행 중인 것까지 모두 되돌린다.</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
            Running = false;
            linger = false;
            if (tx != null) Cleanup(false);
            if (fx != null) fx.ResetAll();
            if (lights != null) lights.ResetAll();
            if (cctv != null) cctv.Night = false;
            if (clock != null) clock.Held = false;
        }

        // ── 도우미 ──
        /// <summary>대사 파일에서 이벤트 번호·종류의 줄을 하나 고른다.</summary>
        string L(Ev333 ev, string key)
        {
            List<string> all = Lines(ev, key);
            return all.Count > 0 ? all[rnd.Next(all.Count)] : null;
        }

        Dictionary<string, List<string>> parsed;
        Lang parsedLang;

        List<string> Lines(Ev333 ev, string key)
        {
            if (parsed == null || parsedLang != Loc.Current)
            {
                parsed = new Dictionary<string, List<string>>();
                parsedLang = Loc.Current;
                if (linesFile != null)
                {
                    string sec = "";
                    foreach (string raw in Loc.Text(linesFile).Replace("\r", "").Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                        if (line.StartsWith("[") && line.EndsWith("]")) { sec = line.Substring(1, line.Length - 2).Trim(); continue; }
                        int c = line.IndexOf(':');
                        if (c <= 0) continue;
                        string k = sec + "/" + line.Substring(0, c).Trim(), v = line.Substring(c + 1).Trim().Replace("/", "\n");
                        if (!parsed.TryGetValue(k, out var list)) parsed[k] = list = new List<string>();
                        list.Add(v);
                    }
                }
            }
            return parsed.TryGetValue((int)ev + "/" + key, out var l) ? l : new List<string>();
        }

        /// <summary>보이지 않는 것이나 문 밖 같은 정해진 자리에서 말풍선.</summary>
        void SayAt(Vector3 at, string line, float hold = -1f, bool zalgo = false)
        {
            if (bubble != null && !string.IsNullOrEmpty(line)) bubble.Say(() => at, line, hold, zalgo);
        }

        /// <summary>내가 하는 말(화면 아래, 내 쪽에서).</summary>
        void SayMine(string line, float hold = -1f)
        {
            if (bubble == null || look == null || string.IsNullOrEmpty(line)) return;
            Transform cam = look.transform;
            bubble.Say(() => cam.position + new Vector3(70f, -150f, 0f), line, hold);
        }

        IEnumerator WaitBubble(float extra = 0f)
        {
            while (bubble != null && bubble.Typing && !tx.done) yield return null;
            if (extra > 0f) yield return new WaitForSeconds(extra);
        }

        /// <summary>손님 풀에서 하나(경찰·제외 목록 빼고). adultsOnly면 아이·학생도 뺀다.</summary>
        CustomerView PickCustomer(bool adultsOnly)
        {
            var pool = new List<CustomerView>();
            foreach (var c in checkout.Pool)
            {
                if (c == null || c.customerId == checkout.policeId) continue;
                if (adultsOnly && (c.customerId == "child" || c.customerId == "student")) continue;
                pool.Add(c);
            }
            return pool.Count > 0 ? pool[rnd.Next(pool.Count)] : null;
        }

        /// <summary>손님이 문을 열고 들어와 계산대 앞에 선다.</summary>
        IEnumerator Arrive(CustomerView who)
        {
            if (who == null) yield break;
            tx.who = who;
            tx.visitor = $"{who.receiptLabel} {Mathf.Max(1, who.partySize)}";
            who.Enter();
            while ((!who.Visible || who.Busy) && !tx.done) yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>물품 칸에 물건을 올린다(drop이면 하나씩 툭툭).</summary>
        IEnumerator Place(List<(string id, bool anomaly)> bag, bool drop = true)
        {
            checkout.bay.Place(bag, rnd);
            if (drop) yield return checkout.DropItemsRoutine();
            tx.placed = true;
        }

        IEnumerator PlaceRandom(int min, int max, bool drop = true) => Place(checkout.bay.PickRandom(rnd, min, max, 0f), drop);

        Sprite savedFace;

        void SetFace(CustomerView who, Sprite s)
        {
            if (who == null || who.counterBody == null || s == null) return;
            if (savedFace == null) savedFace = who.counterBody.sprite;
            who.counterBody.sprite = s;
        }

        void RestoreFace(CustomerView who)
        {
            if (who != null && who.counterBody != null && savedFace != null) who.counterBody.sprite = savedFace;
            savedFace = null;
        }

        static void Present(IList<CctvOverlay> list, bool on)
        {
            if (list == null) return;
            foreach (var o in list)
            {
                if (o == null) continue;
                o.present = on;
                o.Refresh(CctvSystem.CurrentChannel);
            }
        }

        static void Present(CctvOverlay o, bool on)
        {
            if (o == null) return;
            o.present = on;
            o.Refresh(CctvSystem.CurrentChannel);
        }
    }
}
