using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>3:33 이벤트 20가지의 연출(Body)과 끝맺음(Ending).</summary>
    public partial class Event3333
    {
        [Header("4 줄 선 사람들(씬을 만들 때 채워진다)")]
        [Tooltip("계산대 앞부터 문 밖까지 한 줄(가까운 사람부터). 손님 24명")]
        public SpriteRenderer[] queue = new SpriteRenderer[0];
        [Tooltip("줄 선 사람들이 CCTV에 찍힌 모습")]
        public CctvOverlay[] queueCctv = new CctvOverlay[0];
        [Tooltip("queueCctv 하나하나가 줄의 몇 번째 사람인지")]
        public int[] queueCctvOwner = new int[0];
        [Tooltip("줄 선 사람마다 서 있는 깊이(m). 계산대 위 등에서 멀수록 어둠에 묻힌다")]
        public float[] queueDepth = new float[0];

        [Header("5 자동문")]
        [Tooltip("문에서 계산대까지 다가오는 젖은 발자국(문 쪽부터)")]
        public SpriteRenderer[] footprints = new SpriteRenderer[0];
        public float[] footprintDepth = new float[0];

        /// <summary>
        /// 3:33엔 계산대 위 앞줄 등만 켜져 있다: 그 깊이(m)에 있는 것의 밝기. 계산대 앞(2.6m까지)은 그대로,
        /// 문 쪽으로 갈수록 어둠에 묻히고, 문 밖은 바깥 불빛을 등진 실루엣.
        /// </summary>
        Color DepthTint(float z, float alpha = 1f)
        {
            float k = z > 8.3f ? 1f : Mathf.Clamp01((z - 2.6f) / 4.2f);
            k = Mathf.SmoothStep(0f, 1f, k);
            var dark = new Color(0.1f, 0.11f, 0.16f);
            Color c = Color.Lerp(Color.white, dark, k);
            c.a = alpha;
            return c;
        }

        void ShowFootprint(int i, float alpha = 1f)
        {
            SpriteRenderer f = i >= 0 && i < footprints.Length ? footprints[i] : null;
            if (f == null) return;
            f.enabled = true;
            f.color = DepthTint(i < footprintDepth.Length ? footprintDepth[i] : 5f, alpha);
        }

        [Header("10 계산대 밑의 손")]
        public SpriteRenderer hand;
        public Hotspot handHotspot;
        [Tooltip("손이 다 올라왔을 때 자리(월드 픽셀)")]
        public Vector3 handUp;
        public float handRise = 70f;
        [Tooltip("이 높이(월드 픽셀) 아래는 계산대 뒤라 안 보인다")]
        public float handClipY;

        [Header("12 나를 보는 CCTV")]
        public CctvOverlay[] stare = new CctvOverlay[0];
        [Tooltip("stare 하나하나의 단계(0 멀리 → 2 가까이)")]
        public int[] stareStage = new int[0];
        [Tooltip("마지막: 모니터와 큰 화면을 꽉 채운 얼굴")]
        public SpriteRenderer[] stareFaces = new SpriteRenderer[0];

        [Header("14·16 CCTV 2번의 나")]
        [Tooltip("평소 CCTV 2번에 서 있는 나(14번에서 사라진다)")]
        public CctvOverlay[] playerCctv = new CctvOverlay[0];
        [Tooltip("14번: 가끔 한 프레임 내 자리에 서 있는 다른 것")]
        public CctvOverlay[] notMe = new CctvOverlay[0];
        [Tooltip("16번: 내 바로 뒤에 선 전임자")]
        public CctvOverlay[] predecessor = new CctvOverlay[0];

        [Header("15 CCTV 지지직")]
        [Tooltip("큰 화면·계산대 모니터를 덮는 잡음(가운데 기준점)")]
        public SpriteRenderer[] staticScreens = new SpriteRenderer[0];
        [Tooltip("staticScreens 하나하나가 덮을 크기(월드 픽셀)")]
        public Vector2[] staticSizes = new Vector2[0];
        [Tooltip("잡음이 한순간 얼굴 모양으로 뭉칠 때 쓰는 얼굴(밝기만 본다)")]
        public Texture2D staticFace;

        [Header("18 돌아온 실종자")]
        [Tooltip("창밖 자리(정면 화면). 실종자 얼굴을 여기에 넣는다")]
        public SpriteRenderer[] missingFront = new SpriteRenderer[0];
        [Tooltip("자리마다 CCTV 1번(큰 화면, 모니터)")]
        public CctvOverlay[] missingPanel = new CctvOverlay[0];
        public CctvOverlay[] missingMonitor = new CctvOverlay[0];
        public MissingArt[] missingArt = new MissingArt[0];

        [Serializable]
        public class MissingArt
        {
            public string id;
            public Sprite front;
            [Tooltip("자리마다 CCTV 1번 큰 화면·모니터 그림")]
            public Sprite[] panel = new Sprite[0];
            public Sprite[] monitor = new Sprite[0];
        }

        [Header("19 빨개지는 시야")]
        [Tooltip("붉은 막 너머 계산대 앞에 희미하게 서 있는 것")]
        public SpriteRenderer redShape;

        Vector3 handHome;
        bool handSet;
        float handHurtUntil;

        void HideAllArt()
        {
            foreach (var q in queue) if (q != null) q.enabled = false;
            Present(queueCctv, false);
            foreach (var f in footprints) if (f != null) { f.enabled = false; f.color = Color.white; }
            if (hand != null) hand.enabled = false;
            if (handHotspot != null) handHotspot.enabled = false;
            handHurting = false;
            Present(stare, false);
            foreach (var s in stareFaces) if (s != null) s.enabled = false;
            Present(playerCctv, true);
            Present(notMe, false);
            Present(predecessor, false);
            foreach (var s in staticScreens) if (s != null) s.enabled = false;
            foreach (var m in missingFront) if (m != null) m.enabled = false;
            Present(missingPanel, false);
            Present(missingMonitor, false);
            if (redShape != null) redShape.enabled = false;
            if (door != null) door.SetOpenAmount(0f);
        }

        IEnumerator Body(Ev333 ev)
        {
            switch (ev)
            {
                case Ev333.Unmanned: return Unmanned();
                case Ev333.Returner: return Returner();
                case Ev333.Voice: return Voice();
                case Ev333.Queue: return Queue();
                case Ev333.Door: return DoorFrenzy();
                case Ev333.NoProduct: return NoProduct();
                case Ev333.Phone: return Phone();
                case Ev333.Backward: return Backward();
                case Ev333.Plead: return Plead();
                case Ev333.Hand: return Hand();
                case Ev333.Receipt: return EndlessReceipt();
                case Ev333.CctvStare: return CctvStare();
                case Ev333.Bleed: return Bleed();
                case Ev333.CctvNoMe: return CctvNoMe();
                case Ev333.CctvStatic: return CctvStatic();
                case Ev333.Predecessor: return Predecessor();
                case Ev333.Mirror: return MirrorShop();
                case Ev333.MissingBack: return MissingBack();
                case Ev333.RedSight: return RedSight();
                default: return Whispers();
            }
        }

        // ── 1 무인 거래 ──
        // 문이 열리고(차임) 아무도 없는데 물건이 생기며 "계산이요". 시간을 끌거나 CCTV를 보면 화내면서 화면이 흔들리고 손이 떨린다.
        // 30초 안에 비정상으로 끝내지 못하면 게임 오버.
        IEnumerator Unmanned()
        {
            tx.visitor = "방문자 1";
            if (door != null) door.OpenAndClose();
            yield return new WaitForSeconds(1.1f);
            yield return PlaceRandom(2, 4);
            SayAt(counterHead, L(Ev333.Unmanned, "계산"), 2.4f);
            tx.gate = true;
            tx.deadline = Time.time + 30f;

            float anger = 0f, lastProgress = Time.time, nextLine = 0f;
            int lastCount = checkout.bay.Current.Count;
            bool lastPrinted = false;
            while (!tx.done)
            {
                int count = checkout.bay.Current.Count;
                bool printed = checkout.printer != null && checkout.printer.Printed;
                if (count != lastCount || printed != lastPrinted) { lastProgress = Time.time; lastCount = count; lastPrinted = printed; }
                bool watching = cctv != null && cctv.IsOpen;
                bool stalling = Time.time - lastProgress > 6f;
                if (watching || stalling) anger = Mathf.Min(1f, anger + Time.deltaTime * (watching ? 0.35f : 0.18f));
                else anger = Mathf.Max(0f, anger - Time.deltaTime * 0.05f);
                if (fx != null) fx.shake = anger * 0.85f;
                HandFx.Tremble = anger;
                if ((watching || stalling) && Time.time >= nextLine && (bubble == null || !bubble.Showing))
                {
                    string key = watching ? "CCTV" : "화남";
                    SayAt(counterHead, L(Ev333.Unmanned, key), 2f, anger > 0.55f);
                    nextLine = Time.time + 3.5f;
                    SoundFx.Play(sfx, SoundFx.Knock, 0.5f * anger);
                }
                yield return null;
            }
        }

        // ── 2 다시 온 손님 ──
        // 오늘 이미 보낸 손님이 다시 와 있다. 내가 말을 걸어도(대사 고르기) 대답 없이 찢어지게 웃기만. 전등이 살짝씩 깜빡인다.
        IEnumerator Returner()
        {
            CustomerView who = null;
            foreach (var e in checkout.Entries)
                if (e.view != null && e.verdict == Verdict.Normal && e.view.grinFace != null) who = e.view;
            if (who == null)
                foreach (var e in checkout.Entries)
                    if (e.view != null && e.view.grinFace != null) who = e.view;
            if (who == null) who = PickCustomer(false);
            yield return Arrive(who);
            SetFace(who, who != null ? who.grinFace : null);
            yield return PlaceRandom(1, 2);
            StartCoroutine(SoftFlicker());

            var pool = new List<string>(Lines(Ev333.Returner, "나"));
            int said = 0;
            while (!tx.done)
            {
                if (pool.Count < 2) pool = new List<string>(Lines(Ev333.Returner, "나"));
                if (pool.Count < 2 || choices == null) break;
                string a = pool[rnd.Next(pool.Count)]; pool.Remove(a);
                string b = pool[rnd.Next(pool.Count)]; pool.Remove(b);
                choices.ShowAtWorld(MineChoiceSpot, a, b);
                float until = Time.time + 12f;
                while (!tx.done && choices.Showing && choices.Answer < 0 && Time.time < until) yield return null;
                int pick = choices.Answer;
                choices.Hide();
                if (tx.done) break;
                if (pick >= 0)
                {
                    SayMine(pick == 0 ? a : b, 2.2f);
                    said++;
                    yield return WaitBubble(1.4f);
                    // 대답은 없다. 웃는 얼굴만
                    if (said >= 2) tx.gate = true;
                }
                else tx.gate = true;
                yield return new WaitForSeconds(0.4f);
            }
        }

        /// <summary>내가 할 말 고르기 버튼 자리(월드 픽셀, 오른쪽 위): 계산대 앞 손님 얼굴 왼쪽. 그쪽을 보면 조준으로 고른다.</summary>
        Vector2 MineChoiceSpot => (look != null ? look.home : new Vector2(0f, -34f)) + new Vector2(-48f, 28f);

        /// <summary>앞줄 형광등이 가끔 살짝 어두워졌다 돌아온다(심하지 않게).</summary>
        IEnumerator SoftFlicker()
        {
            while (!tx.done && lights != null)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.8f, 2.4f));
                if (tx.done) break;
                lights.Set(StoreLights.FrontRow, UnityEngine.Random.Range(0.45f, 0.75f));
                SoundFx.Play(sfx, SoundFx.TubeTick, 0.3f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.14f));
                lights.Set(StoreLights.FrontRow, 1f);
            }
        }

        // ── 3 목소리 ──
        // 아무도 없는데 노크 → 문 밖에서 "문 좀 열어 주세요.." → "안에 있는 거 다 알아.." → 말풍선이 사라지고 2초 뒤
        // 문도 안 열렸는데 물건 하나 → "왜 안 열어 줬어...?" → 노크가 미친 듯이, 화면이 흔들리고 이명. 다음 손님쯤 풀린다.
        IEnumerator Voice()
        {
            tx.visitor = "방문자 1";
            for (int i = 0; i < 3; i++) { SoundFx.Play(sfx, SoundFx.Knock, 0.7f); yield return new WaitForSeconds(1.2f); }
            SayAt(doorTop, L(Ev333.Voice, "부탁"), 1.6f);
            yield return WaitBubble(1.6f);
            SoundFx.Play(sfx, SoundFx.Knock, 0.5f);
            SayAt(doorTop, L(Ev333.Voice, "앎"), 1.6f);
            yield return WaitBubble(1.4f);
            if (bubble != null) bubble.Hide();
            yield return new WaitForSeconds(2f);
            yield return PlaceRandom(1, 1);
            yield return new WaitForSeconds(0.6f);
            SayAt(counterHead, L(Ev333.Voice, "왜"), 2.2f);
            yield return WaitBubble(1f);
            // 노크 폭주
            float end = Time.time + 4f;
            while (Time.time < end && !tx.done)
            {
                float k = 1f - (end - Time.time) / 4f;
                if (fx != null) fx.shake = Mathf.Lerp(0.4f, 1f, k);
                SoundFx.Play(sfx, SoundFx.Knock, 1f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.06f, 0.16f));
            }
            // 이명만 남는다(흔들림도 조금)
            if (fx != null) { fx.shake = 0.35f; fx.Loop(0, SoundFx.Tinnitus, 0.28f); }
            tx.gate = true;
        }

        // ── 4 줄 선 사람들 ──
        // 한 명이 서 있다가 한 명씩 늘어 24명이 다 와서 한 줄로 선다(CCTV에도 보인다). 아무도 말이 없다.
        // 비정상으로 끝내는 순간 불이 깜빡이며 다 사라진다.
        IEnumerator Queue()
        {
            tx.visitor = $"방문자 {Mathf.Max(1, queue.Length)}";
            ShowQueue(1);
            yield return new WaitForSeconds(0.8f);
            yield return PlaceRandom(1, 3);
            for (int n = 2; n <= queue.Length && !tx.done; n++)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.8f, 1.3f));
                ShowQueue(n);
                SoundFx.Play(sfx, SoundFx.TubeTick, 0.35f);
                if (lights != null && n % 4 == 0) { lights.Set(StoreLights.FrontRow, 0.6f); yield return new WaitForSeconds(0.06f); lights.Set(StoreLights.FrontRow, 1f); }
            }
            tx.gate = true;
        }

        void ShowQueue(int n)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] == null) continue;
                queue[i].enabled = i < n;
                queue[i].color = DepthTint(i < queueDepth.Length ? queueDepth[i] : 2.2f);
            }
            for (int i = 0; i < queueCctv.Length; i++)
                Present(queueCctv[i], i < queueCctvOwner.Length && queueCctvOwner[i] < n);
        }

        // ── 5 자동문 ──
        // 문이 혼자 엄청 빠르게 열렸다 닫히고, 젖은 발자국이 문에서 계산대 쪽으로 한 걸음씩 다가와 멈추면 물건이 올라온다.
        // 그사이에도 문은 계속 열렸다 닫힌다.
        IEnumerator DoorFrenzy()
        {
            tx.visitor = "방문자 1";
            StartCoroutine(DoorLoop());
            yield return new WaitForSeconds(1.2f);
            for (int i = 0; i < footprints.Length; i++)
            {
                if (tx.done) yield break;
                ShowFootprint(i);
                SoundFx.Play(sfx, SoundFx.Squish, 0.35f);
                yield return new WaitForSeconds(0.75f);
            }
            yield return new WaitForSeconds(0.8f);
            yield return PlaceRandom(1, 1);
            tx.gate = true;
        }

        IEnumerator DoorLoop()
        {
            if (door == null) yield break;
            while (!tx.done)
            {
                if (door.source != null && door.chime != null && Application.isPlaying) door.source.PlayOneShot(door.chime, 0.55f);
                for (float t = 0f; t < 0.11f; t += Time.deltaTime) { door.SetOpenAmount(t / 0.11f); yield return null; }
                door.SetOpenAmount(1f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.02f, 0.1f));
                for (float t = 0f; t < 0.11f; t += Time.deltaTime) { door.SetOpenAmount(1f - t / 0.11f); yield return null; }
                door.SetOpenAmount(0f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.03f, 0.12f));
            }
            door.SetOpenAmount(0f);
        }

        // ── 6 없는 상품 ──
        // 에러 상품·검정 상품이 잔뜩(물품 칸 8칸). 찍으면 "삑" 대신 웃음소리.
        IEnumerator NoProduct()
        {
            tx.visitor = "방문자 1";
            checkout.scanSoundOverride = SoundFx.Laugh;
            checkout.scanPriceOverride = 333;
            var bag = new List<(string id, bool anomaly)>();
            for (int i = 0; i < 8; i++) bag.Add((rnd.Next(2) == 0 ? ItemBay.ErrorId : ItemBay.BlackId, false));
            yield return Place(bag);
            tx.gate = true;
        }

        // ── 7 점장 전화 ──
        // 전화가 울리고, 받으면 점장 목소리가 "그 거래 지금 마감해. 빨리." 점점 늘어지고 깨진다. 안 받아도 괜찮다(33초 내내 울림).
        IEnumerator Phone()
        {
            tx.visitor = "방문자 1";
            yield return PlaceRandom(1, 2);
            if (phone == null) { tx.gate = true; yield break; }
            var lines = Lines(Ev333.Phone, "점장");
            var broken = new List<string>();
            for (int i = 0; i < lines.Count; i++)
                broken.Add(i == lines.Count - 1 ? CustomerLines.Corrupt(lines[i], rnd) : lines[i]);
            // 받으면: 말할수록 목소리가 늘어지고 낮아지며 깨진다
            if (phone.dialog != null) phone.dialog.warpVoice = true;
            yield return phone.RingSpecial("점장", broken, 33f, null);
            if (phone.dialog != null) phone.dialog.warpVoice = false;
            tx.gate = true;
        }

        // ── 8 거꾸로 가는 시간 ──
        // 손님이 서 있고 시계가 03:33 → 03:00으로 빠르게 거꾸로 간다(약 20초). 영수증 시각도 같이. 03:10부터 시계가 빨갛게 깜빡.
        // 03:00이 되기 전에 비정상으로 보내야 한다.
        IEnumerator Backward()
        {
            yield return Arrive(PickCustomer(true));
            yield return PlaceRandom(1, 3);
            if (tx.who != null) checkout.SayBy(tx.who, L(Ev333.Backward, "늦음"), 3f);
            tx.gate = true;
            float start = Time.time;
            const float seconds = 20f;
            int last = -1;
            while (!tx.done)
            {
                if (Answered) { yield return null; continue; }   // 거래 종료하기를 눌렀다: 시계가 그 자리에 멈춘다
                float m = Mathf.Lerp(clock.eventMinute, clock.eventMinute - 33f, (Time.time - start) / seconds);
                clock.SetMinute(m);
                ClockHud.Alarm = m <= clock.eventMinute - 23f;
                int whole = Mathf.FloorToInt(m);
                if (whole != last)
                {
                    last = whole;
                    if (checkout.Current != null) checkout.Current.time = clock.Format();
                    if (checkout.receiptView != null) checkout.receiptView.Rerender();
                    SoundFx.Play(sfx, SoundFx.Tick, ClockHud.Alarm ? 0.5f : 0.25f);
                }
                if (m <= clock.eventMinute - 33f + 0.001f) { tx.fail = true; yield break; }
                yield return null;
            }
        }

        // ── 9 애원하는 손님 ──
        // 평범한 손님이 울면서 점점 절박하게 말한다. 마우스가 무거워진다. 다 듣고 나서야 영수증이 나온다.
        IEnumerator Plead()
        {
            yield return Arrive(PickCustomer(true));
            yield return PlaceRandom(1, 3);
            HandFx.Heavy = 0.85f;
            foreach (string line in Lines(Ev333.Plead, "애원"))
            {
                if (tx.done) yield break;
                if (tx.who != null) checkout.SayBy(tx.who, line, 2.6f);
                yield return WaitBubble(2.4f);
            }
            tx.gate = true;
        }

        // ── 10 계산대 밑의 손 ──
        // 계산대 앞 가장자리에서 창백한 손이 빠르게 올라와 돈바구니에 손가락·눈알을 올려놓는다.
        // 손을 클릭하면 화면이 빨개지며 손이 아파한다(하트는 안 깨진다).
        IEnumerator Hand()
        {
            tx.visitor = "방문자 1";
            tx.coins = false;
            tx.bodyParts = true;
            yield return PlaceRandom(1, 2);
            if (hand == null) { tx.gate = true; yield break; }
            if (!handSet) { handHome = handUp; handSet = true; }
            handHurting = false;
            int drops = 0;
            while (!tx.done)
            {
                // 빠르게 올라와 → 하나 내려놓고 → 쑥 내려간다. 도중에 맞으면(클릭) 아파하며 들어갔다가 한참 뒤 다시
                ShowHand(true);
                yield return MoveHand(0f, 1f, 0.14f);
                if (!handHurting)
                {
                    if (checkout.tray != null) StartCoroutine(checkout.tray.DropOnePart(rnd.Next(), sfx));
                    drops++;
                    for (float w = UnityEngine.Random.Range(0.35f, 0.7f); w > 0f && !handHurting; w -= Time.deltaTime) yield return null;
                }
                if (!handHurting) yield return MoveHand(1f, 0f, 0.2f);
                while (handHurting && !tx.done) yield return null;
                ShowHand(false);
                if (drops >= 3) tx.gate = true;
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.6f, 1.6f));
            }
        }

        float handK;
        bool handHurting;

        void ShowHand(bool on)
        {
            if (hand != null) hand.enabled = on;
            if (handHotspot != null) handHotspot.enabled = on && !handHurting;
        }

        IEnumerator MoveHand(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (handHurting) yield break;   // 맞았다: 움직임은 HurtHand가 맡는다
                SetHand(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            if (!handHurting) SetHand(to);
        }

        void SetHand(float k)
        {
            handK = k;
            if (hand == null) return;
            if (!handSet) { handHome = handUp; handSet = true; }
            Vector3 p = handHome + new Vector3(0f, -Mathf.Round(handRise * (1f - k)), 0f);
            if (Time.time < handHurtUntil) p += new Vector3(UnityEngine.Random.Range(-2, 3), UnityEngine.Random.Range(-1, 2), 0f);
            hand.transform.position = p;
            ClipHand(p);
            if (handHotspot != null && hand.sprite != null)
            {
                Bounds b = hand.bounds;
                float y0 = Mathf.Max(b.min.y, handClipY);
                handHotspot.area = Rect.MinMaxRect(b.min.x, y0, b.max.x, Mathf.Max(y0 + 4f, b.max.y));
            }
        }

        Color32[] handPx;
        int handW, handH;
        PixelTex handTex;

        /// <summary>계산대 끝보다 아래(계산대 뒤)로 내려간 팔은 지운다.</summary>
        void ClipHand(Vector3 pos)
        {
            if (handPx == null)
            {
                Texture2D tex = hand.sprite != null ? hand.sprite.texture : null;
                if (tex == null || !tex.isReadable) return;
                handPx = tex.GetPixels32();
                handW = tex.width;
                handH = tex.height;
                handTex = new PixelTex(handW, handH);
            }
            float scale = Mathf.Max(0.01f, hand.transform.localScale.y);
            int cut = Mathf.Clamp(Mathf.CeilToInt((handClipY - pos.y) / scale), 0, handH);
            for (int y = 0; y < handH; y++)
                for (int x = 0; x < handW; x++)
                    handTex.Px[y * handW + x] = y >= cut ? handPx[y * handW + x] : default;
            hand.sprite = handTex.ToSprite(new Vector2(0.5f, 0f));
        }

        /// <summary>손을 클릭했다: 화면이 빨개지고, 손이 비명을 지르며 꿈틀대다 쑥 들어간다.</summary>
        public void OnHandClicked()
        {
            if (Tonight != Ev333.Hand || hand == null || !hand.enabled || handHurting || tx == null || tx.done) return;
            if (fx != null) fx.Flash(0.7f, 0.9f);
            SoundFx.Play(sfx, SoundFx.Scream, 0.9f);
            StartCoroutine(HurtHand());
        }

        /// <summary>맞은 손: 그 자리에서 바들바들 떨다가 쑥 들어가 한동안 안 나온다.</summary>
        IEnumerator HurtHand()
        {
            handHurting = true;
            if (handHotspot != null) handHotspot.enabled = false;
            float k = handK;
            handHurtUntil = Time.time + 0.55f;
            for (float t = 0f; t < 0.55f; t += Time.deltaTime) { SetHand(k); yield return null; }
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) { SetHand(Mathf.Lerp(k, 0f, t / 0.12f)); yield return null; }
            SetHand(0f);
            if (hand != null) hand.enabled = false;
            yield return new WaitForSeconds(1.2f);
            handHurting = false;
        }

        // ── 11 끊이지 않는 영수증 ──
        // 영수증이 멈추지 않고 나와 계산대 아래로 늘어진다. 줄마다 사라진 손님·지난 날짜·모르는 이름. 열면 끝없이 올라간다.
        IEnumerator EndlessReceipt()
        {
            yield return Arrive(PickCustomer(false));
            yield return PlaceRandom(1, 3);
            tx.endless = true;
            if (checkout.receiptView != null) checkout.receiptView.tailRows = 13;
            tx.gate = true;
            while (checkout.Current == null || checkout.printer == null || !checkout.printer.Printing) { if (tx.done) yield break; yield return null; }
            var names = new List<string>(Lines(Ev333.Receipt, "줄"));
            if (fate != null)
                foreach (string id in fate.MissingIds)
                {
                    CustomerView v = null;
                    foreach (var c in checkout.Pool) if (c != null && c.customerId == id) v = c;
                    if (v != null) names.Insert(0, $"{v.receiptLabel}");
                }
            int n = 0;
            while (!tx.done)
            {
                string name = names.Count > 0 ? names[n % names.Count] : "???";
                if (rnd.NextDouble() < 0.3) name = $"{2000 + rnd.Next(10, 25)}.{rnd.Next(1, 13):00}.{rnd.Next(1, 29):00}";
                checkout.Current.lines.Add(new ReceiptLine { id = "fake", name = name, unitPrice = 333, qty = 1, fake = true });
                if (checkout.receiptView != null) checkout.receiptView.Rerender();
                n++;
                yield return new WaitForSeconds(0.35f);
            }
        }

        // ── 12 나를 보는 CCTV ──
        // 세 채널 모두 화면 속 누군가가 카메라를 정면으로 본다. 채널을 바꾸거나 몇 초 지날 때마다 한 단계씩 가까워지고,
        // 마지막엔 계산대 위 모니터(와 큰 화면)를 얼굴이 꽉 채운다.
        IEnumerator CctvStare()
        {
            tx.visitor = "방문자 1";
            yield return PlaceRandom(1, 2);
            int stage = 0;
            ShowStare(stage);
            float next = Time.time + 4.5f;
            int ch = CctvSystem.CurrentChannel;
            while (!tx.done && stage < 3)
            {
                if (Time.time >= next || CctvSystem.CurrentChannel != ch)
                {
                    ch = CctvSystem.CurrentChannel;
                    stage++;
                    ShowStare(stage);
                    SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.5f);
                    next = Time.time + 4.5f;
                }
                yield return null;
            }
            SoundFx.Play(sfx, SoundFx.Static, 0.6f);
            tx.gate = true;
        }

        void ShowStare(int stage)
        {
            for (int i = 0; i < stare.Length; i++) Present(stare[i], i < stareStage.Length && stareStage[i] == stage);
            foreach (var s in stareFaces) if (s != null) s.enabled = stage >= 3;
        }

        // ── 13 피 흘리는 물건 ──
        // 물품 칸 물건마다 피가 새어 나와 계산대 위로 번진다(찍어서 없어져도 피는 남는다).
        IEnumerator Bleed()
        {
            yield return Arrive(PickCustomer(false));
            yield return PlaceRandom(2, 4);
            var pools = new List<BloodPool>();
            foreach (var p in checkout.bay.Current)
                if (p.renderer != null) pools.Add(new BloodPool(this, p.renderer, rnd.Next()));
            float start = Time.time;
            const float grow = 11f;
            while (!tx.done)
            {
                float k = Mathf.Clamp01((Time.time - start) / grow);
                foreach (var b in pools) b.Draw(k);
                if (k >= 1f) tx.gate = true;
                if (UnityEngine.Random.value < Time.deltaTime * 1.5f) SoundFx.Play(sfx, SoundFx.Squish, 0.15f);
                yield return new WaitForSeconds(0.08f);
            }
        }

        /// <summary>물건 밑에서 번지는 핏물(계산대 윗면에 납작하게, 앞쪽으로 흘러내리는 줄기).</summary>
        class BloodPool
        {
            readonly SpriteRenderer sr;
            readonly PixelTex pt;
            readonly int seed;
            const int W = 72, H = 40;

            public BloodPool(Event3333 owner, SpriteRenderer item, int seed)
            {
                this.seed = seed;
                var go = new GameObject("Blood Pool");
                owner.spawned.Add(go);
                Bounds b = item.bounds;
                go.transform.position = new Vector3(Mathf.Round(b.center.x - W / 2f), Mathf.Round(b.min.y - H + 10f), item.transform.position.z + 0.01f);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = item.sharedMaterial;
                sr.sortingOrder = 51;   // 계산대(50) 위, 물건(52~) 밑
                pt = new PixelTex(W, H);
            }

            public void Draw(float k)
            {
                pt.Clear();
                var r = new System.Random(seed);
                float rx = Mathf.Lerp(4f, 30f, k), ry = Mathf.Lerp(1.5f, 7f, k);
                int cx = W / 2, cy = H - 12;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry;
                        float n = PixelTex.Noise(x / 2, y, seed & 255) * 0.5f;
                        if (dx * dx + dy * dy > 1f + n - 0.25f) continue;
                        bool shine = dy > 0.3f && Mathf.Abs(dx) < 0.4f && (x + y) % 5 == 0;
                        byte v = (byte)(r.Next(18));
                        pt.Put(x, y, GameSettings.NoBlood   // 혈흔 없애기: 검은 액체
                            ? (shine ? new Color32(70, 66, 62, 255) : new Color32((byte)(24 + v / 2), (byte)(22 + v / 2), (byte)(21 + v / 2), 255))
                            : (shine ? new Color32(150, 30, 34, 255) : new Color32((byte)(78 + v), 6, 10, 255)));
                    }
                // 앞쪽(화면 아래)으로 흘러내리는 줄기
                int drips = 2 + seed % 3;
                for (int i = 0; i < drips; i++)
                {
                    int x = cx + Mathf.RoundToInt((float)(r.NextDouble() * 2 - 1) * rx * 0.8f);
                    int len = Mathf.RoundToInt(Mathf.Lerp(0f, 8f + r.Next(14), Mathf.Clamp01(k * 1.6f - 0.4f)));
                    Color32 drip = GameSettings.NoBlood ? new Color32(20, 18, 18, 255) : new Color32(70, 4, 8, 255);
                    for (int y = 0; y < len; y++) { pt.Put(x, cy - (int)ry - y, drip); if (y == len - 1) pt.Put(x + 1, cy - (int)ry - y, drip); }
                }
                sr.sprite = pt.ToSprite(Vector2.zero);
            }
        }

        // ── 14 CCTV에 내가 안 보임 ──
        // CCTV 2번에 늘 서 있던 내가 없다. 앞 손님이 "…거기 누구 있어요?" 가끔 한 프레임 내 자리에 다른 것이 서 있다.
        IEnumerator CctvNoMe()
        {
            yield return Arrive(PickCustomer(false));
            yield return PlaceRandom(1, 3);
            Present(playerCctv, false);
            float gateAt = Time.time + 12f, nextFlash = Time.time + 2.5f;
            bool asked = false;
            while (!tx.done)
            {
                if (!asked && (Time.time > gateAt - 8f || (cctv != null && cctv.IsOpen)))
                {
                    asked = true;
                    if (tx.who != null) checkout.SayBy(tx.who, L(Ev333.CctvNoMe, "누구"), 3f);
                }
                if (Time.time >= nextFlash)
                {
                    Present(notMe, true);
                    yield return new WaitForSeconds(0.08f);
                    Present(notMe, false);
                    nextFlash = Time.time + UnityEngine.Random.Range(1.8f, 3.6f);
                }
                if (Time.time >= gateAt) tx.gate = true;
                yield return null;
            }
        }

        // ── 15 CCTV 지지직 + 이상한 소리 ──
        // 모니터가 전부 노이즈로 덮이고 지지직 사이로 이상한 소리. 큰 화면을 열면 노이즈가 한순간 얼굴 모양으로 뭉친다.
        IEnumerator CctvStatic()
        {
            yield return Arrive(PickCustomer(false));
            yield return PlaceRandom(1, 3);
            if (fx != null) fx.Loop(1, SoundFx.Static, 0.3f);
            foreach (var s in staticScreens) if (s != null) s.enabled = true;
            float gateAt = Time.time + 12f, nextVoice = Time.time + 2f, faceUntil = 0f;
            bool wasOpen = false;
            var tex = new Dictionary<SpriteRenderer, PixelTex>();
            while (!tx.done)
            {
                bool open = cctv != null && cctv.IsOpen;
                if (open && !wasOpen) { faceUntil = Time.time + 0.35f; SoundFx.Play(sfx, SoundFx.Voice, 0.8f); }
                wasOpen = open;
                foreach (var s in staticScreens) if (s != null) DrawStatic(s, tex, Time.time < faceUntil);
                if (Time.time >= nextVoice) { SoundFx.Play(sfx, SoundFx.Voice, 0.55f); nextVoice = Time.time + UnityEngine.Random.Range(3.5f, 6f); }
                if (Time.time >= gateAt) tx.gate = true;
                yield return new WaitForSeconds(0.05f);
            }
        }

        /// <summary>잡음 한 장(화면 크기의 1/3로 그려 3배로 키운다). face면 얼굴 모양으로 뭉친다.</summary>
        void DrawStatic(SpriteRenderer s, Dictionary<SpriteRenderer, PixelTex> cache, bool face)
        {
            int idx = Array.IndexOf(staticScreens, s);
            Vector2 size = idx >= 0 && idx < staticSizes.Length ? staticSizes[idx] : new Vector2(60f, 34f);
            int w = Mathf.Max(8, Mathf.RoundToInt(size.x / 3f)), h = Mathf.Max(6, Mathf.RoundToInt(size.y / 3f));
            if (!cache.TryGetValue(s, out var pt) || pt.W != w || pt.H != h) cache[s] = pt = new PixelTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = UnityEngine.Random.value;
                    if (face && staticFace != null)
                    {
                        float fv = staticFace.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h).grayscale;
                        v = Mathf.Lerp(v, fv, 0.75f);
                    }
                    bool band = (y + (int)(Time.time * 40f)) % 23 < 2;
                    byte g = (byte)Mathf.Clamp(v * 230f + (band ? 40f : 0f), 0f, 255f);
                    pt.Set(x, y, new Color32(g, g, (byte)Mathf.Min(255, g + 8), 255));
                }
            s.sprite = pt.ToSprite(new Vector2(0.5f, 0.5f));
            // 스프라이트 한 칸이 화면 3픽셀쯤: 정해진 크기에 맞게 늘린다
            s.transform.localScale = new Vector3(size.x / w, size.y / h, 1f);
        }

        // ── 16 전임자 ──
        // 앞에는 아무도 없고 CCTV 2번에만 내 바로 뒤에 피 묻은 유니폼 알바가 서 있다. 내 뒤(화면 아래)에서 말풍선만.
        IEnumerator Predecessor()
        {
            tx.visitor = "방문자 1";
            yield return PlaceRandom(1, 1);
            Present(predecessor, true);
            yield return new WaitForSeconds(3.2f);
            SayMine(L(Ev333.Predecessor, "말"), 3f);
            SoundFx.Play(sfx, SoundFx.Whisper, 0.3f);
            yield return new WaitForSeconds(5f);
            tx.gate = true;
        }

        // ── 17 거울 가게 ──
        // 화면 전체가 좌우로 뒤집힌다(수첩·영수증 글씨도 거꾸로, 마우스도 거꾸로 느껴진다). 깜빡일 때 뒤집혔다.
        IEnumerator MirrorShop()
        {
            yield return Arrive(PickCustomer(false));
            yield return PlaceRandom(1, 3);
            yield return new WaitForSeconds(12f);
            tx.gate = true;
        }

        // ── 18 돌아온 실종자 ──
        // 내가 "아니오"로 보낸 정상 손님들(실종자)이 회색 얼굴로 창밖에 붙어 있다. CCTV 1번에도.
        IEnumerator MissingBack()
        {
            tx.visitor = "방문자 1";
            yield return PlaceRandom(1, 1);
            int slot = 0;
            if (fate != null)
                foreach (string id in fate.MissingIds)
                {
                    if (slot >= missingFront.Length) break;
                    MissingArt art = Array.Find(missingArt, a => a != null && a.id == id);
                    if (art == null) continue;
                    if (missingFront[slot] != null) { missingFront[slot].sprite = art.front; missingFront[slot].enabled = art.front != null; }
                    if (slot < missingPanel.Length && missingPanel[slot] != null && slot < art.panel.Length)
                    { missingPanel[slot].GetComponent<SpriteRenderer>().sprite = art.panel[slot]; Present(missingPanel[slot], true); }
                    if (slot < missingMonitor.Length && missingMonitor[slot] != null && slot < art.monitor.Length)
                    { missingMonitor[slot].GetComponent<SpriteRenderer>().sprite = art.monitor[slot]; Present(missingMonitor[slot], true); }
                    slot++;
                }
            for (int i = 0; i < 4 && !tx.done; i++) { SoundFx.Play(sfx, SoundFx.Knock, 0.25f); yield return new WaitForSeconds(2.8f); }
            tx.gate = true;
        }

        // ── 19 빨개지는 시야 ──
        // 가장자리부터 붉은 막이 조여 오고 시야가 흐려지며 심장 소리가 점점 빨라진다. 붉은 막 너머로 계산대 앞에 무언가가 희미하게.
        IEnumerator RedSight()
        {
            tx.visitor = "방문자 1";
            yield return PlaceRandom(1, 2);
            if (fx != null) fx.Loop(0, SoundFx.Heartbeat, 0.4f, 0.9f);
            float start = Time.time;
            const float seconds = 15f;
            while (!tx.done)
            {
                float k = Mathf.Clamp01((Time.time - start) / seconds);
                // 가게가 이미 어두우니(앞줄 등만) 붉은 막은 예전보다 덜 짙게
                if (fx != null) { fx.veilAmount = Mathf.Lerp(0.05f, 0.7f, k); fx.SetLoopVolume(0, Mathf.Lerp(0.4f, 0.95f, k), Mathf.Lerp(0.9f, 1.7f, k)); }
                if (redShape != null)
                {
                    redShape.enabled = k > 0.35f;
                    float a = Mathf.Clamp01((k - 0.35f) * 1.6f) * (0.5f + 0.15f * Mathf.Sin(Time.time * 6f));
                    redShape.color = new Color(0.12f, 0.01f, 0.02f, a);
                }
                if (k >= 1f) tx.gate = true;
                yield return null;
            }
        }

        // ── 20 속삭임 ──
        // 속삭임이 들리고 화면 여기저기에 작은 글씨("끝내", "왜 안 해", "눌러")가 뜬다. 끝으로 갈수록 많아지고,
        // 마지막엔 점검표를 열면 "거래 종료하기" 바로 위에 "눌러".
        IEnumerator Whispers()
        {
            tx.visitor = "방문자 1";
            yield return PlaceRandom(1, 2);
            if (fx != null) fx.Loop(0, SoundFx.Whisper, 0.25f);
            var words = Lines(Ev333.Whisper, "말");
            if (words.Count == 0) words.Add("눌러");
            float start = Time.time, next = 0f;
            const float seconds = 15f;
            while (!tx.done)
            {
                float k = Mathf.Clamp01((Time.time - start) / seconds);
                if (fx != null) fx.SetLoopVolume(0, Mathf.Lerp(0.25f, 0.8f, k));
                if (Time.time >= next && fx != null)
                {
                    next = Time.time + Mathf.Lerp(0.9f, 0.12f, k);
                    ChecklistView sheet = checkout.checklistView;
                    if (k >= 1f && sheet != null && sheet.IsOpen && sheet.CanAnswer && rnd.NextDouble() < 0.45)
                    {
                        RectInt e = sheet.EndButtonRect;
                        fx.Whisper("눌러", new Vector2(e.x + e.width / 2 - 12 + rnd.Next(-30, 31), e.yMax + 2 + rnd.Next(0, 8)), 0.9f, 12);
                    }
                    else
                        fx.Whisper(words[rnd.Next(words.Count)], new Vector2(rnd.Next(-300, 270), rnd.Next(-160, 160)), Mathf.Lerp(1.8f, 1f, k), rnd.Next(9, 13));
                }
                if (k >= 1f) tx.gate = true;
                yield return null;
            }
        }

        // ── 끝맺음(무사히 넘겼을 때) ──
        IEnumerator Ending(Ev333 ev, bool ok)
        {
            if (bubble != null) bubble.Hide();
            if (choices != null) choices.Hide();
            switch (ev)
            {
                case Ev333.Queue:
                    // 남은 앞줄 등이 깜빡이며 다 사라진다
                    for (int i = 0; i < 5; i++)
                    {
                        if (lights != null) lights.Set(StoreLights.FrontRow, 0f);
                        SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.8f);
                        if (i == 3 && GameSettings.ReduceFlicker && lights != null) yield return lights.UntilDark();
                        if (i == 3) ShowQueue(0);
                        yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.1f));
                        if (lights != null) lights.Set(StoreLights.FrontRow, 1f);
                        yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.14f));
                    }
                    break;
                case Ev333.Plead:
                    // 사라지기 직전 한 프레임만 변신 모습 → "…다음엔 해 줄 거지?"
                    if (tx.who != null && tx.who.transforms.Length > 0)
                    {
                        SetFace(tx.who, tx.who.transforms[rnd.Next(tx.who.transforms.Length)]);
                        yield return new WaitForSeconds(0.12f);
                        RestoreFace(tx.who);
                    }
                    if (tx.who != null) { checkout.SayBy(tx.who, L(Ev333.Plead, "다음"), 1.6f); yield return WaitBubble(1.2f); }
                    break;
                case Ev333.Mirror:
                    if (lights != null) lights.Set(StoreLights.FrontRow, 0f);
                    SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.8f);
                    if (GameSettings.ReduceFlicker && lights != null) yield return lights.UntilDark();
                    if (fx != null) fx.Mirror = false;
                    yield return new WaitForSeconds(0.3f);
                    if (lights != null) lights.Set(StoreLights.FrontRow, 1f);
                    break;
                case Ev333.RedSight:
                {
                    float from = fx != null ? fx.veilAmount : 0f;
                    for (float t = 0f; t < 1.4f && fx != null; t += Time.deltaTime) { fx.veilAmount = Mathf.Lerp(from, 0f, t / 1.4f); yield return null; }
                    break;
                }
                case Ev333.Door:
                    if (door != null) door.SetOpenAmount(0f);
                    for (float t = 0f; t < 1f; t += Time.deltaTime)
                    {
                        for (int i = 0; i < footprints.Length; i++) if (footprints[i] != null && footprints[i].enabled) ShowFootprint(i, 1f - t);
                        yield return null;
                    }
                    break;
            }
            // 손님은 조용히 가라앉아 사라진다(비정상)
            if (tx.who != null && tx.who.Visible)
            {
                RestoreFace(tx.who);
                tx.who.Leave(silent: true);
                while (tx.who.Busy) yield return null;
            }
            SoundFx.Play(sfx, SoundFx.Tear, 0.8f);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
