using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 오프닝과 엔딩(사용자 결정: 엔딩은 하나, 7일차 06:00까지 버티면).
    /// 카메라 자식이라 화면에 붙는다. 메인 화면(400번대)보다 위(450~475).
    ///  오프닝(새로 시작): 비 오는 밤 내 방 → 휴대폰 구인 공고 → "전화하기" → 연결음 → 점장과 통화(대사창) → 뚝
    ///    → 주차장 CCTV에 우산 쓴 내가 걸어 들어가 문을 연다(MainMenu.PlayArrival) → 암전 → 1일차 점장 전화로 이어진다.
    ///  엔딩: 7일차 06:00 교대 알바생이 처음으로 고개를 들어 나를 본다(WorkerFarewell, 근무 끝 장면 안) → 결과 화면
    ///    → 비 그친 새벽 주차장 CCTV에 내가 걸어 나온다(내가 만든 실종자·내보낸 것들이 서 있다, MainMenu.PlayDawn)
    ///    → 휴대폰이 울리고 오프닝과 같은 통화를 역할만 바꿔서 → CCTV 2번: 다음 날 00:00, 계산대 뒤의 새 알바, 가게 전화벨 → 끝.
    /// 대사는 Data/Story.txt.
    /// </summary>
    public class StoryView : MonoBehaviour
    {
        [Header("연결")]
        public MouseLookCamera look;
        public Camera viewCamera;
        public AudioSource sfx;
        [Tooltip("오프닝·엔딩 대사(Data/Story.txt)")]
        public TextAsset storyFile;

        [Header("그림(씬을 만들 때 채워진다)")]
        public GameObject panel;
        public SpriteRenderer backdrop, room, feed, newHire, phone, box, fade;
        public RectTransform textRoot;
        public Font titleFont, bodyFont;
        [Tooltip("휴대폰 가운데(화면 가운데 기준 픽셀)")]
        public Vector2 phoneCenter = new Vector2(150f, 20f);

        [Header("오프닝 1인칭: 비 오는 주차장 → 자동문 → 가게 안")]
        [Tooltip("내 눈으로 본 걸음 한 장씩(광선 추적, StoryBuilder.RenderWalkInBatch)")]
        public Sprite[] walkFrames = new Sprite[0];
        [Tooltip("자동문이 열리기 시작하는 장(딸랑), 가게 안에 들어선 첫 장")]
        public int walkDoorFrame = 40, walkInsideFrame = 62;
        public float walkFps = 16f;
        [Tooltip("한 걸음(발소리 한 번)이 몇 장인지")]
        public float walkStepFrames = 6.4f;
        [Tooltip("빗줄기·우산 끝(1인칭 화면 위)")]
        public SpriteRenderer overlay;
        public AudioClip chime;

        [Header("엔딩")]
        [Tooltip("7일째 아침, 처음으로 고개를 들어 나를 보는 교대 알바생(눈물)")]
        public Sprite workerFaceUp;
        [Tooltip("마지막 CCTV 2번 화면(평소 영상)과 그 자리")]
        public Sprite feedSprite;
        public Vector2 feedAt;
        [Tooltip("CCTV 2번 화면 속 계산대 뒤의 새 알바")]
        public Sprite newHireSprite;
        public Vector2 newHireAt;
        public float charsPerSecond = 22f;
        [Tooltip("전화 너머 웅얼거리는 목소리(내가 아닌 쪽이 말할 때)")]
        public PhoneVoice phoneVoice;

        enum PhoneState { Ad, Calling, Incoming, InCall, Ended }

        static readonly Color32 Cream = new Color32(236, 228, 208, 255);
        const int PW = 144, PH = 240;
        const int SW = 640, SH = 360;

        PixelTex phoneTex, fadeTex, boxTex, plainTex, backTex;
        Sprite boxSprite, plainSprite;
        PhoneState state;
        string phoneName = "", phoneStatus = "";
        bool hoverButton, inCall, ringing;
        float callStart;
        int shownSecond = -1;
        AudioSource ambience, loop;
        Dictionary<string, List<(string who, string line)>> sections;
        Lang sectionsLang;
        readonly TextPool phoneTexts = new TextPool(), dialogTexts = new TextPool(), screenTexts = new TextPool();
        CustomerView farewellWorker;
        Sprite farewellSaved;

        /// <summary>오프닝·엔딩이 진행 중.</summary>
        public bool Playing { get; private set; }

        void Awake()
        {
            if (panel != null && !Playing) panel.SetActive(false);
        }

        // ── 오프닝 ──
        [Tooltip("Space를 이만큼(초) 누르고 있으면 오프닝을 건너뛴다")]
        public float skipSeconds = 0.8f;
        bool openingDone;
        float skipHold;
        SpriteRenderer skipBar;
        PixelTex skipTex;
        readonly TextPool hintTexts = new TextPool();

        /// <summary>
        /// 새로 시작: 내 방 → 공고 → 통화 → 가게로 걸어 들어감. 끝나면 화면은 검은 채(메인 화면이 덮고 있다).
        /// 어느 때든 Space를 길게(skipSeconds) 누르면 건너뛴다(한 번 누르는 Space는 대사 넘기기라서 길게).
        /// </summary>
        public IEnumerator PlayOpening(MainMenu menu)
        {
            openingDone = false;
            skipHold = 0f;
            StartCoroutine(OpeningBody(menu));
            while (!openingDone)
            {
                bool held = GameInput.SkipHeld && !PauseMenu.Paused;
                skipHold = held ? skipHold + Time.unscaledDeltaTime : Mathf.MoveTowards(skipHold, 0f, Time.unscaledDeltaTime * 2f);
                DrawSkip(Mathf.Clamp01(skipHold / Mathf.Max(0.1f, skipSeconds)));
                if (skipHold >= skipSeconds) { SkipOpening(menu); break; }
                yield return null;
            }
            DrawSkip(-1f);
        }

        /// <summary>건너뛰기: 진행 중인 오프닝을 모두 멈추고 소리를 끈 뒤 검은 화면으로(이어서 1일차 점장 전화).</summary>
        void SkipOpening(MainMenu menu)
        {
            StopAllCoroutines();   // 오프닝 본체와 그 안의 대사·걸음 등(이 컴포넌트에서 돌던 것) 전부
            inCall = false;
            ringing = false;
            hoverButton = false;
            if (phoneVoice != null) phoneVoice.Hush();
            Loop(null);
            Ambience(null, 0f);
            Show(overlay, false);
            if (roomSprite != null && room != null) room.sprite = roomSprite;
            End();
            if (menu != null) menu.CoverAfterSkip();
            openingDone = true;
        }

        /// <summary>오른쪽 아래 "Space 길게: 건너뛰기"와 차오르는 막대. k가 음수면 숨긴다.</summary>
        void DrawSkip(float k)
        {
            hintTexts.Clear();
            if (k < 0f || panel == null || !panel.activeSelf)
            {
                if (skipBar != null) skipBar.enabled = false;
                return;
            }
            hintTexts.Add(textRoot, "Space 길게: 건너뛰기", 120, 165, 190, 12, TextAnchor.LowerRight,
                          new Color32(200, 204, 208, (byte)(k > 0f ? 230 : 140)), bodyFont, 10);
            if (skipBar == null && box != null)
            {
                var go = new GameObject("Skip Bar");
                go.transform.SetParent(panel.transform, false);
                skipBar = go.AddComponent<SpriteRenderer>();
                skipBar.sharedMaterial = box.sharedMaterial;
                skipBar.sortingOrder = 470;
                skipTex = new PixelTex(60, 3);
            }
            if (skipBar == null) return;
            skipTex.Clear(new Color32(60, 64, 70, 160));
            int fill = Mathf.RoundToInt(60 * k);
            if (fill > 0) skipTex.Fill(0, 0, fill, 3, new Color32(236, 228, 208, 255));
            skipBar.sprite = skipTex.ToSprite(new Vector2(1f, 0f));
            skipBar.transform.localPosition = new Vector3(310f, 160f, 0f);
            skipBar.enabled = true;
        }

        IEnumerator OpeningBody(MainMenu menu)
        {
            Begin();
            Show(room, true);
            SetFade(1f);
            Ambience(SoundFx.Rain, 0.28f);
            phoneCenter = new Vector2(150f, 20f);
            DrawPhone(PhoneState.Ad, "", "");
            Show(phone, true);
            yield return FadeTo(0f, 1.8f);

            yield return WaitButton();   // "전화하기"
            SoundFx.Play(sfx, SoundFx.Tick, 0.8f);
            DrawPhone(PhoneState.Calling, Speaker("오프닝", "점장"), "연결 중…");
            Loop(SoundFx.RingBack, 0.8f);
            yield return new WaitForSeconds(5.4f);   // 뚜르르르… 두 번
            Loop(null);
            SoundFx.Play(sfx, SoundFx.PhoneUp, 0.7f);
            StartCall();
            Loop(SoundFx.PhoneLine, 0.3f);
            foreach (var l in Section("오프닝")) yield return Say(l.who, l.line);
            EndCall();
            yield return new WaitForSeconds(1.4f);
            yield return FadeTo(1f, 1.2f);
            Ambience(null, 0f);
            Show(phone, false);
            phoneTexts.Clear();
            if (walkFrames != null && walkFrames.Length > 0)
            {
                // 내 눈으로: 비 오는 주차장을 걸어 자동문을 지나 가게 안으로. 끝나면 검은 채(메인 화면이 덮고 있다)
                yield return new WaitForSeconds(0.8f);
                yield return WalkIn();
                End();
            }
            else
            {
                End();
                if (menu != null) yield return menu.PlayArrival();   // 1인칭 그림이 없으면 주차장 CCTV로
            }
            openingDone = true;
        }

        // ── 오프닝 1인칭 ──
        PixelTex overlayTex;
        readonly Vector4[] drops = new Vector4[150];
        System.Random dropRnd = new System.Random(77);
        float umbrellaK = 1f;
        bool rainOn;
        Sprite roomSprite;

        /// <summary>
        /// 우산 아래 내 눈으로 본 걸음(초당 walkFps장): 비 오는 주차장 → 1.4m쯤 앞에서 자동문이 열리고(딸랑) → 우산을 접고(푸드득)
        /// 문지방을 넘으면 빗소리가 멀어지고 형광등 웅웅 → 가게 안을 몇 걸음 → 어두워진다.
        /// </summary>
        IEnumerator WalkIn()
        {
            if (roomSprite == null && room != null) roomSprite = room.sprite;
            Show(room, true);
            room.sprite = walkFrames[0];
            umbrellaK = 1f;
            rainOn = true;
            for (int k = 0; k < drops.Length; k++) drops[k] = NewDrop(true);
            Show(overlay, true);
            DrawOverlay(0f);
            Ambience(SoundFx.Rain, 0.5f);
            SetFade(1f);
            yield return FadeTo(0f, 1.2f);
            yield return new WaitForSeconds(0.5f);

            // 시간에 맞춰 장을 고른다(프레임이 밀려도 걷는 빠르기는 같다)
            float fps = Mathf.Max(1f, walkFps);
            int foldFrames = Mathf.Max(2, Mathf.RoundToInt(fps * 0.45f));   // 우산을 접는 데 0.45초
            int lastStep = -1, shown = -1;
            bool flapped = false, entered = false;
            float start = Time.time;
            while (shown < walkFrames.Length - 1)
            {
                int i = Mathf.Min(walkFrames.Length - 1, Mathf.FloorToInt((Time.time - start) * fps));
                for (int k = shown + 1; k <= i; k++)
                {
                    // 지나친 장마다 한 번씩: 딸랑(자동문), 푸드득(우산), 문지방, 발소리
                    bool insideK = k >= walkInsideFrame;
                    if (k == walkDoorFrame && chime != null) SoundFx.Play(sfx, chime, 0.55f);
                    if (!flapped && k >= walkInsideFrame - foldFrames) { flapped = true; SoundFx.Play(sfx, SoundFx.Flap, 0.55f); }
                    if (!entered && insideK)
                    {
                        entered = true;
                        rainOn = false;
                        Ambience(SoundFx.Rain, 0.1f);          // 등 뒤로 빗소리가 멀어진다
                        Loop(SoundFx.TubeHum, 0.16f);          // 형광등 웅웅
                    }
                    int stepNo = Mathf.FloorToInt(k / Mathf.Max(1f, walkStepFrames));
                    if (stepNo != lastStep) { lastStep = stepNo; SoundFx.Play(sfx, insideK ? SoundFx.StepIn : SoundFx.Step, insideK ? 0.5f : 0.45f); }
                }
                if (i != shown) { shown = i; room.sprite = walkFrames[i]; }
                // 우산: 문 앞에서 0.45초 동안 위로 접혀 올라가 사라진다
                float target = shown >= walkInsideFrame ? 0f : Mathf.Clamp01((walkInsideFrame - shown) / (float)foldFrames);
                umbrellaK = Mathf.MoveTowards(umbrellaK, target, Time.deltaTime * fps / foldFrames);
                DrawOverlay(Time.deltaTime);
                yield return null;
            }
            yield return new WaitForSeconds(0.9f);
            yield return FadeTo(1f, 1.1f);
            Loop(null);
            Ambience(null, 0f);
            Show(overlay, false);
            if (roomSprite != null) room.sprite = roomSprite;
        }

        Vector4 NewDrop(bool anywhere) => new Vector4(dropRnd.Next(SW + 80) - 40, anywhere ? dropRnd.Next(SH) : SH + dropRnd.Next(40), 5 + dropRnd.Next(8), 320f + dropRnd.Next(220));

        /// <summary>1인칭 화면 위: 빗줄기(밖에서만)와 머리 위 우산 끝(접히면 위로 사라진다), 우산 살 끝에서 떨어지는 물방울.</summary>
        void DrawOverlay(float dt)
        {
            if (overlay == null) return;
            if (overlayTex == null) overlayTex = new PixelTex(SW, SH);
            Color32[] px = overlayTex.Px;
            System.Array.Clear(px, 0, px.Length);
            if (rainOn)
            {
                var rainC = new Color32(196, 208, 222, 90);
                for (int k = 0; k < drops.Length; k++)
                {
                    Vector4 d = drops[k];
                    d.y -= d.w * dt; d.x += d.w * dt / 4f;
                    drops[k] = d = d.y + d.z < 0 ? NewDrop(false) : d;
                    for (int j = 0; j < (int)d.z; j++)
                    {
                        int x = (int)d.x - j / 4, y = (int)d.y + j;
                        if (x >= 0 && x < SW && y >= 0 && y < SH) px[y * SW + x] = rainC;
                    }
                }
            }
            if (umbrellaK > 0.001f)
            {
                // 우산 안쪽 면: 가운데가 깊게 내려온 반원, 살과 살 사이 아랫단이 물결. 살 끝에 매달린 물방울
                float lift = (1f - umbrellaK) * 90f;
                var cloth = new Color32(22, 26, 36, 250);
                var clothHi = new Color32(34, 40, 54, 250);
                var rib = new Color32(58, 64, 82, 255);
                var rim = new Color32(84, 94, 112, 255);
                const float panel = 160f;
                for (int x = 0; x < SW; x++)
                {
                    float dx = (x - SW / 2f) / (SW * 0.56f);
                    float scallop = Mathf.Abs(Mathf.Sin((x - SW / 2f) * Mathf.PI / panel)) * 9f;
                    int edge = Mathf.RoundToInt(SH - (34f + 26f * (1f - dx * dx)) + scallop + lift);
                    for (int y = Mathf.Max(0, edge); y < SH; y++)
                    {
                        bool isRib = Mathf.Abs(Mathf.Repeat(x - SW / 2f + (y - edge) * ((x - SW / 2f) / 260f), panel) - panel * 0.5f) > panel * 0.5f - 1.2f;
                        px[y * SW + x] = y == edge ? rim : isRib ? rib : ((y - edge) % 9 < 4 ? clothHi : cloth);
                    }
                }
                // 살 끝에서 떨어지는 물방울
                float now = Time.time;
                for (int k = -2; k <= 2; k++)
                {
                    int tipX = Mathf.RoundToInt(SW / 2f + k * panel);
                    float dx = (tipX - SW / 2f) / (SW * 0.56f);
                    int tipY = Mathf.RoundToInt(SH - (34f + 26f * (1f - dx * dx)) + lift);
                    float ph = Mathf.Repeat(now * 1.3f + k * 0.37f, 1f);
                    int dy = Mathf.RoundToInt(ph * ph * 160f);
                    for (int j = 0; j < 3; j++)
                    {
                        int y = tipY - dy - j;
                        if (tipX >= 0 && tipX < SW && y >= 0 && y < SH) px[y * SW + tipX] = new Color32(170, 186, 204, 200);
                    }
                }
            }
            overlay.sprite = overlayTex.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>미리보기: 1인칭 i번째 장(빗줄기·우산 포함).</summary>
        public void PreviewWalk(int i)
        {
            Begin();
            SetFade(0f);
            if (walkFrames == null || walkFrames.Length == 0) return;
            i = Mathf.Clamp(i, 0, walkFrames.Length - 1);
            if (roomSprite == null && room != null) roomSprite = room.sprite;
            Show(room, true);
            room.sprite = walkFrames[i];
            rainOn = i < walkInsideFrame;
            int foldFrames = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(1f, walkFps) * 0.45f));
            umbrellaK = i >= walkInsideFrame ? 0f : Mathf.Clamp01((walkInsideFrame - i) / (float)foldFrames);
            dropRnd = new System.Random(77);
            for (int k = 0; k < drops.Length; k++) drops[k] = NewDrop(true);
            Show(overlay, true);
            DrawOverlay(0f);
        }

        // ── 엔딩 ──
        /// <summary>
        /// 7일차 06:00 근무 끝 장면 안에서(교대 알바생이 들어와 선 뒤): 처음으로 고개를 들어 나를 본다(눈물) → 대사.
        /// 바뀐 얼굴은 화면이 검어진 뒤 PlayEnding에서 되돌린다.
        /// </summary>
        public IEnumerator WorkerFarewell(ShiftEndView shiftEnd, CustomerView worker)
        {
            yield return new WaitForSeconds(0.9f);
            if (worker != null && worker.counterBody != null && workerFaceUp != null)
            {
                farewellWorker = worker;
                farewellSaved = worker.counterBody.sprite;
                worker.counterBody.sprite = workerFaceUp;
                yield return new WaitForSeconds(1.2f);
            }
            foreach (var l in Section("교대"))
                if (shiftEnd != null) yield return shiftEnd.Say(l.who, l.line);
        }

        /// <summary>결과 화면 뒤: 새벽 주차장 → 휴대폰 → 역할이 바뀐 통화 → 다음 날 밤 CCTV 2번 → 끝.</summary>
        public IEnumerator PlayEnding(MainMenu menu, IList<string> missingIds, IList<string> traceIds)
        {
            RestoreWorker();
            Begin();
            SetFade(0f);
            if (menu != null) yield return menu.PlayDawn(missingIds, traceIds);
            yield return new WaitForSeconds(1.8f);

            // 주머니 속 휴대폰이 울린다: 모르는 번호
            phoneCenter = new Vector2(170f, 30f);
            DrawPhone(PhoneState.Incoming, Speaker("엔딩", "모르는 번호", other: true), "수신 전화");
            Show(phone, true);
            ringing = true;
            Loop(SoundFx.MobileRing, 0.6f);
            yield return WaitButton();   // "받기"
            ringing = false;
            phone.transform.localPosition = new Vector3(phoneCenter.x, phoneCenter.y, phone.transform.localPosition.z);
            Loop(null);
            SoundFx.Play(sfx, SoundFx.PhoneUp, 0.6f);
            StartCall();
            Loop(SoundFx.PhoneLine, 0.25f);
            foreach (var l in Section("엔딩")) yield return Say(l.who, l.line);
            EndCall();
            yield return new WaitForSeconds(1.2f);
            Show(phone, false);
            phoneTexts.Clear();
            yield return new WaitForSeconds(0.8f);
            yield return FadeTo(1f, 1.6f);
            if (menu != null) menu.HideStory();

            // 다음 날 밤 00:00, CCTV 2번: 계산대 뒤에 처음 보는 알바가 서 있다. 가게 전화가 울린다
            ShowFinal(true);
            yield return new WaitForSeconds(0.8f);
            yield return FadeTo(0f, 1.4f);
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < 3; i++)
            {
                SoundFx.Play(sfx, SoundFx.PhoneRing, 0.55f);
                yield return new WaitForSeconds(2.6f);
            }
            SetFade(1f);   // 뚝
            ShowFinal(false);
            yield return new WaitForSeconds(1.6f);

            // 끝
            SetFade(0f);
            screenTexts.Clear();
            screenTexts.Add(textRoot, "03:33", -160, -2, 320, 40, TextAnchor.MiddleCenter, new Color32(255, 122, 51, 255), titleFont, 36);
            screenTexts.Add(textRoot, "끝", -160, -40, 320, 20, TextAnchor.MiddleCenter, Cream, bodyFont, 12);
            yield return WaitClick();
            screenTexts.Clear();
            End();
        }

        void RestoreWorker()
        {
            if (farewellWorker != null && farewellWorker.counterBody != null && farewellSaved != null) farewellWorker.counterBody.sprite = farewellSaved;
            farewellWorker = null;
            farewellSaved = null;
        }

        void ShowFinal(bool on)
        {
            Show(backdrop, on);
            if (feed != null) { feed.sprite = feedSprite; feed.transform.localPosition = new Vector3(feedAt.x, feedAt.y, feed.transform.localPosition.z); }
            if (newHire != null) { newHire.sprite = newHireSprite; newHire.transform.localPosition = new Vector3(newHireAt.x, newHireAt.y, newHire.transform.localPosition.z); }
            Show(feed, on && feedSprite != null);
            Show(newHire, on && newHireSprite != null);
            screenTexts.Clear();
            if (!on) return;
            screenTexts.Add(textRoot, "CAM 02  매장", -262, 138, 200, 14, TextAnchor.LowerLeft, Cream, bodyFont, 10);
            screenTexts.Add(textRoot, "● REC", 190, 138, 72, 14, TextAnchor.LowerRight, new Color32(255, 58, 48, 255), bodyFont, 10);
            screenTexts.Add(textRoot, "DAY 1   00:00:00", 112, -136, 150, 14, TextAnchor.LowerRight, Cream, bodyFont, 10);
        }

        // ── 진행 공용 ──
        void Begin()
        {
            Playing = true;
            if (panel != null) panel.SetActive(true);
            BuildSprites();
            foreach (var sr in new[] { backdrop, room, feed, newHire, phone, box }) Show(sr, false);
            phoneTexts.Clear();
            dialogTexts.Clear();
            screenTexts.Clear();
            if (look != null) look.LookLocked = true;   // 커서를 보이게(휴대폰 버튼)
        }

        void End()
        {
            DrawSkip(-1f);
            foreach (var sr in new[] { backdrop, room, feed, newHire, phone, box }) Show(sr, false);
            SetFade(0f);
            phoneTexts.Clear();
            dialogTexts.Clear();
            screenTexts.Clear();
            Loop(null);
            Ambience(null, 0f);
            if (panel != null) panel.SetActive(false);
            if (look != null) look.LookLocked = false;
            Playing = false;
        }

        static void Show(SpriteRenderer sr, bool on) { if (sr != null) sr.enabled = on; }

        void StartCall()
        {
            inCall = true;
            if (phoneVoice != null) phoneVoice.UseSlow(false);   // 오프닝·엔딩 통화는 2배속 목소리
            callStart = Time.time;
            shownSecond = -1;
            DrawPhone(PhoneState.InCall, phoneName, Loc.F("통화 중 {0:00}:{1:00}", 0, 00));
        }

        void EndCall()
        {
            inCall = false;
            if (phoneVoice != null) phoneVoice.Hush();
            Loop(null);
            SoundFx.Play(sfx, SoundFx.PhoneDown, 0.8f);
            DrawPhone(PhoneState.Ended, phoneName, "통화 종료");
        }

        void Update()
        {
            if (!Playing) return;
            if (inCall)
            {
                int s = Mathf.FloorToInt(Time.time - callStart);
                if (s != shownSecond) { shownSecond = s; DrawPhone(PhoneState.InCall, phoneName, Loc.F("통화 중 {0:00}:{1:00}", s / 60, s % 60)); }
            }
            if (ringing && phone != null)
            {
                // 진동: 0.5초마다 잠깐 부르르
                float t = Time.time % 1f;
                float dx = t < 0.35f ? (Mathf.Repeat(Time.time * 40f, 2f) < 1f ? -1f : 1f) : 0f;
                phone.transform.localPosition = new Vector3(phoneCenter.x + dx, phoneCenter.y, phone.transform.localPosition.z);
            }
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float from = fade != null ? fade.color.a : 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetFade(Mathf.Lerp(from, target, t / seconds));
                yield return null;
            }
            SetFade(target);
        }

        void SetFade(float a)
        {
            if (fade == null) return;
            fade.enabled = a > 0.001f;
            fade.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        void Ambience(AudioClip clip, float volume)
        {
            if (!Application.isPlaying) return;
            if (ambience == null) ambience = NewSource();
            if (clip == null) { ambience.Stop(); return; }
            ambience.clip = clip;
            ambience.volume = volume;
            if (!ambience.isPlaying) ambience.Play();
        }

        void Loop(AudioClip clip, float volume = 1f)
        {
            if (!Application.isPlaying) return;
            if (loop == null) loop = NewSource();
            if (clip == null) { loop.Stop(); loop.clip = null; return; }
            loop.clip = clip;
            loop.volume = volume;
            loop.Play();
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        static bool Confirm() => GameInput.LeftClickDown || GameInput.SubmitDown;

        IEnumerator WaitClick()
        {
            yield return null;
            while (!Confirm()) yield return null;
            yield return null;
        }

        /// <summary>휴대폰 아래 버튼(전화하기·받기)을 누를 때까지. 마우스를 올리면 밝아진다. 스페이스·엔터도 된다.</summary>
        IEnumerator WaitButton()
        {
            yield return null;
            while (true)
            {
                Vector2 p = Pointer();
                bool over = ButtonRect().Contains(p);
                if (over != hoverButton) { hoverButton = over; DrawPhone(state, phoneName, phoneStatus); }
                if ((GameInput.LeftClickDown && over) || GameInput.SubmitDown) break;
                yield return null;
            }
            hoverButton = false;
            yield return null;
        }

        Vector2 Pointer()
        {
            if (viewCamera == null) return new Vector2(99999f, 99999f);
            return (Vector2)viewCamera.ScreenToWorldPoint(GameInput.MousePosition) - (Vector2)viewCamera.transform.position;
        }

        /// <summary>휴대폰 버튼 자리(화면 가운데 기준 픽셀).</summary>
        Rect ButtonRect()
        {
            Vector2 o = phoneCenter - new Vector2(PW / 2f, PH / 2f);
            RectInt b = state == PhoneState.Incoming ? new RectInt(PW / 2 + 11, 29, 30, 30) : new RectInt(16, 30, PW - 32, 24);
            return new Rect(o.x + b.x, o.y + b.y, b.width, b.height);
        }

        // ── 대사창 ──
        IEnumerator Say(string speaker, string line)
        {
            line = Loc.T(line);
            bool narration = string.IsNullOrEmpty(speaker);
            Show(box, true);
            if (box != null) box.sprite = narration ? plainSprite : boxSprite;
            dialogTexts.Clear();
            if (!narration) dialogTexts.Add(textRoot, speaker, -250, -117, 120, 14, TextAnchor.LowerLeft, Cream, titleFont, 12);
            Text body = dialogTexts.Add(textRoot, "", -246, -146, 492, 16, TextAnchor.LowerLeft, Cream, bodyFont, 10);
            yield return null;
            // 전화 너머 사람이 말할 때만 웅얼거리는 목소리(내 말은 소리 없이). 엔딩의 새 알바는 조금 높은 목소리
            bool voice = inCall && !narration && speaker != "나" && phoneVoice != null;
            if (voice) { phoneVoice.warp = 0f; phoneVoice.Talk(speaker == "점장" ? 1f : 1.12f); }
            float shown = 0f;
            while (shown < line.Length)
            {
                if (Confirm()) { shown = line.Length; break; }
                shown += Time.deltaTime * charsPerSecond;
                body.text = line.Substring(0, Mathf.Min(line.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            body.text = line;
            if (voice) phoneVoice.Quiet();
            Text more = dialogTexts.Add(textRoot, "▼", 234, -168, 14, 12, TextAnchor.LowerLeft, Cream, bodyFont, 10);
            yield return null;
            while (!Confirm())
            {
                more.enabled = Time.unscaledTime % 0.8f < 0.5f;
                yield return null;
            }
            yield return null;
            dialogTexts.Clear();
            Show(box, false);
        }

        // ── 대사 파일 ──
        List<(string who, string line)> Section(string name)
        {
            if (sections == null || sectionsLang != Loc.Current)
            {
                sections = new Dictionary<string, List<(string, string)>>();
                sectionsLang = Loc.Current;
                string sec = "";
                if (storyFile != null)
                    foreach (string raw in Loc.Text(storyFile).Replace("\r", "").Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        if (line.StartsWith("[") && line.EndsWith("]")) { sec = line.Substring(1, line.Length - 2).Trim(); continue; }
                        if (!sections.TryGetValue(sec, out var list)) sections[sec] = list = new List<(string, string)>();
                        if (line.StartsWith("(")) { list.Add(("", line)); continue; }
                        int c = line.IndexOf(':');
                        if (c <= 0) continue;
                        list.Add((line.Substring(0, c).Trim(), line.Substring(c + 1).Trim()));
                    }
            }
            return sections.TryGetValue(name, out var l) ? l : new List<(string, string)>();
        }

        /// <summary>그 장면에서 "나"가 아닌 첫 화자(휴대폰 화면에 뜨는 이름). other면 "나"가 아닌 쪽.</summary>
        string Speaker(string section, string fallback, bool other = false)
        {
            foreach (var l in Section(section))
                if (!string.IsNullOrEmpty(l.who) && l.who != "나") return l.who;
            return fallback;
        }

        // ── 그림 ──
        void BuildSprites()
        {
            if (fadeTex == null && fade != null)
            {
                fadeTex = new PixelTex(1, 1);
                fadeTex.Put(0, 0, new Color32(0, 0, 0, 255));
                fade.sprite = fadeTex.ToSprite(new Vector2(0.5f, 0.5f));
                fade.transform.localScale = new Vector3(SW + 4, SH + 4, 1f);
            }
            if (backTex == null && backdrop != null)
            {
                backTex = new PixelTex(1, 1);
                backTex.Put(0, 0, new Color32(0, 0, 0, 255));
                backdrop.sprite = backTex.ToSprite(new Vector2(0.5f, 0.5f));
                backdrop.transform.localScale = new Vector3(SW + 4, SH + 4, 1f);
            }
            if (boxTex == null && box != null)
            {
                boxTex = DrawBox(true);
                boxSprite = boxTex.ToSprite(new Vector2(0.5f, 0.5f));
                plainTex = DrawBox(false);
                plainSprite = plainTex.ToSprite(new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>근무 끝 대사창과 같은 모양(화면 아래, 이름표).</summary>
        static PixelTex DrawBox(bool tag)
        {
            var pt = new PixelTex(SW, SH);
            int ox = SW / 2, oy = SH / 2;
            var b = new RectInt(-260 + ox, -174 + oy, 520, 56);
            pt.Fill(b.x, b.y, b.xMax, b.yMax, new Color32(12, 12, 18, 236));
            pt.Outline(b.x, b.y, b.xMax, b.yMax, Cream);
            pt.Outline(b.x + 2, b.y + 2, b.xMax - 2, b.yMax - 2, new Color32(70, 66, 60, 255));
            if (tag)
            {
                var t = new RectInt(-260 + ox, -118 + oy, 92, 17);
                pt.Fill(t.x, t.y, t.xMax, t.yMax, new Color32(12, 12, 18, 236));
                pt.Outline(t.x, t.y, t.xMax, t.yMax, Cream);
            }
            return pt;
        }

        /// <summary>휴대폰 한 장(128x232): 몸체·화면, 화면 안 내용은 상태마다. 글자는 캔버스에.</summary>
        void DrawPhone(PhoneState s, string name, string status)
        {
            state = s;
            phoneName = name;
            phoneStatus = status;
            if (phone == null) return;
            if (phoneTex == null) phoneTex = new PixelTex(PW, PH);
            PixelTex pt = phoneTex;
            pt.Clear();
            RoundRect(pt, 0, 0, PW, PH, 11, new Color32(16, 18, 23, 255));
            RoundRect(pt, 1, 1, PW - 1, PH - 1, 10, new Color32(34, 37, 45, 255));
            RoundRect(pt, 2, 2, PW - 2, PH - 2, 9, new Color32(16, 18, 23, 255));
            var scr = new RectInt(7, 16, PW - 14, PH - 30);
            Color32 bg = s == PhoneState.Ad ? new Color32(236, 236, 230, 255) : new Color32(22, 27, 36, 255);
            RoundRect(pt, scr.x, scr.y, scr.xMax, scr.yMax, 4, bg);
            pt.Fill(PW / 2 - 12, PH - 9, PW / 2 + 12, PH - 7, new Color32(46, 50, 58, 255));   // 수화부
            pt.Put(PW / 2 + 18, PH - 8, new Color32(60, 70, 90, 255));                          // 카메라
            // 상태 표시줄
            Color32 barC = s == PhoneState.Ad ? new Color32(40, 120, 96, 255) : new Color32(14, 18, 24, 255);
            pt.Fill(scr.x, scr.yMax - 10, scr.xMax, scr.yMax, barC);
            pt.Fill(scr.xMax - 13, scr.yMax - 7, scr.xMax - 4, scr.yMax - 3, new Color32(220, 226, 220, 255));   // 배터리
            pt.Fill(scr.xMax - 4, scr.yMax - 6, scr.xMax - 3, scr.yMax - 4, new Color32(220, 226, 220, 255));

            Vector2 o = phoneCenter - new Vector2(PW / 2f, PH / 2f);
            int ox = Mathf.RoundToInt(o.x), oy = Mathf.RoundToInt(o.y);
            phoneTexts.Clear();
            phoneTexts.Add(textRoot, s == PhoneState.Ad ? "23:41" : s == PhoneState.Incoming ? "06:14" : "", ox + scr.x + 3, oy + scr.yMax - 10, 40, 10, TextAnchor.LowerLeft, new Color32(236, 240, 236, 255), bodyFont, 8);

            if (s == PhoneState.Ad)
            {
                // 앱 머리: 구인구직
                pt.Fill(scr.x, scr.yMax - 26, scr.xMax, scr.yMax - 10, new Color32(52, 150, 118, 255));
                phoneTexts.Add(textRoot, "구인구직", ox + scr.x + 6, oy + scr.yMax - 24, 100, 14, TextAnchor.LowerLeft, new Color32(250, 252, 248, 255), titleFont, 10);
                // 공고 카드
                var card = new RectInt(scr.x + 5, 62, scr.width - 10, scr.yMax - 26 - 6 - 62);
                pt.Fill(card.x, card.y, card.xMax, card.yMax, new Color32(255, 255, 252, 255));
                pt.Outline(card.x, card.y, card.xMax, card.yMax, new Color32(200, 204, 198, 255));
                pt.Fill(card.x + 4, card.yMax - 6, card.x + 22, card.yMax - 4, new Color32(214, 64, 52, 255));   // "급구" 띠
                phoneTexts.Add(textRoot, "급구", ox + card.x + 4, oy + card.yMax - 16, 40, 10, TextAnchor.LowerLeft, new Color32(214, 64, 52, 255), bodyFont, 8);
                var ad = Section("공고");
                string title = "심야 편의점 야간 알바";
                var rows = new List<string>();
                foreach (var l in ad) { if (l.who == "제목") title = l.line; else rows.Add(l.line); }
                phoneTexts.Add(textRoot, title, ox + card.x + 4, oy + card.yMax - 34, card.width - 8, 14, TextAnchor.LowerLeft, new Color32(24, 26, 30, 255), titleFont, 10);
                for (int i = 0; i < rows.Count; i++)
                    phoneTexts.Add(textRoot, rows[i], ox + card.x + 5, oy + card.yMax - 52 - i * 13, card.width - 10, 12, TextAnchor.LowerLeft,
                                   i == 1 ? new Color32(40, 120, 96, 255) : new Color32(70, 74, 80, 255), bodyFont, 10);
                // 전화하기 버튼
                RectInt b = new RectInt(16, 30, PW - 32, 24);
                RoundRect(pt, b.x, b.y, b.xMax, b.yMax, 4, hoverButton ? new Color32(84, 200, 124, 255) : new Color32(52, 168, 92, 255));
                phoneTexts.Add(textRoot, "전화하기", ox + b.x, oy + b.y + 5, b.width, 14, TextAnchor.LowerCenter, new Color32(255, 255, 255, 255), titleFont, 10);
            }
            else
            {
                // 통화 화면: 사람 모양 동그라미, 이름, 상태
                int cx = PW / 2, cy = 176;
                for (int y = -18; y <= 18; y++)
                    for (int x = -18; x <= 18; x++)
                        if (x * x + y * y <= 18 * 18) pt.Put(cx + x, cy + y, new Color32(64, 72, 86, 255));
                for (int y = -10; y <= 10; y++)
                    for (int x = -10; x <= 10; x++)
                    {
                        bool head = x * x + (y - 4) * (y - 4) <= 30;
                        bool body = y < -4 && x * x + (y + 14) * (y + 14) <= 120 && x * x + y * y <= 18 * 18;
                        if (head || body) pt.Put(cx + x, cy + y, new Color32(150, 160, 176, 255));
                    }
                phoneTexts.Add(textRoot, name, ox, oy + 134, PW, 16, TextAnchor.LowerCenter, new Color32(240, 242, 246, 255), titleFont, 12);
                phoneTexts.Add(textRoot, status, ox, oy + 116, PW, 12, TextAnchor.LowerCenter, new Color32(160, 170, 184, 255), bodyFont, 10);
                if (s == PhoneState.Incoming)
                {
                    Circle(pt, PW / 2 - 26, 44, 12, new Color32(214, 64, 52, 255));
                    Circle(pt, PW / 2 + 26, 44, 12, hoverButton ? new Color32(84, 210, 124, 255) : new Color32(52, 178, 92, 255));
                    phoneTexts.Add(textRoot, "거절", ox + PW / 2 - 46, oy + 18, 40, 12, TextAnchor.LowerCenter, new Color32(200, 206, 214, 255), bodyFont, 10);
                    phoneTexts.Add(textRoot, "받기", ox + PW / 2 + 6, oy + 18, 40, 12, TextAnchor.LowerCenter, new Color32(200, 206, 214, 255), bodyFont, 10);
                }
                else if (s != PhoneState.Ended) Circle(pt, PW / 2, 44, 12, new Color32(214, 64, 52, 255));
            }
            phone.sprite = pt.ToSprite(new Vector2(0.5f, 0.5f));
            phone.transform.localPosition = new Vector3(phoneCenter.x, phoneCenter.y, phone.transform.localPosition.z);
        }

        static void RoundRect(PixelTex pt, int x0, int y0, int x1, int y1, int r, Color32 c)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int dx = x < x0 + r ? x0 + r - x : x >= x1 - r ? x - (x1 - r - 1) : 0;
                    int dy = y < y0 + r ? y0 + r - y : y >= y1 - r ? y - (y1 - r - 1) : 0;
                    if (dx * dx + dy * dy <= r * r) pt.Put(x, y, c);
                }
        }

        static void Circle(PixelTex pt, int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) pt.Put(cx + x, cy + y, c);
        }

        /// <summary>글자 몇 개를 돌려 쓰는 묶음.</summary>
        class TextPool
        {
            readonly List<Text> items = new List<Text>();
            int used;

            public Text Add(RectTransform root, string text, int x, int y, int w, int h, TextAnchor align, Color32 color, Font font, int size)
            {
                if (root == null) return null;
                while (items.Count <= used)
                {
                    var go = new GameObject($"Story Text {items.Count}", typeof(RectTransform));
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(root, false);
                    var rt0 = (RectTransform)go.transform;
                    rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0.5f);
                    rt0.pivot = Vector2.zero;
                    var tx = go.AddComponent<Text>();
                    tx.horizontalOverflow = HorizontalWrapMode.Overflow;
                    tx.verticalOverflow = VerticalWrapMode.Overflow;
                    tx.supportRichText = false;
                    tx.raycastTarget = false;
                    items.Add(tx);
                }
                Text t = items[used++];
                t.gameObject.SetActive(true);
                t.enabled = true;
                t.font = font;
                t.fontSize = size;
                t.color = color;
                t.alignment = align;
                t.text = Loc.T(text);   // 화면 글자는 지금 언어로
                var rt = (RectTransform)t.transform;
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta = new Vector2(w, h);
                return t;
            }

            public void Clear()
            {
                foreach (var t in items) if (t != null) t.gameObject.SetActive(false);
                used = 0;
            }
        }

        // ── 미리보기(에디터) ──
        /// <summary>미리보기: 0 방+공고, 1 방+통화 대사.</summary>
        public void PreviewOpening(int stage)
        {
            Begin();
            Show(room, true);
            SetFade(0f);
            phoneCenter = new Vector2(150f, 20f);
            if (stage == 0) { hoverButton = true; DrawPhone(PhoneState.Ad, "", ""); hoverButton = false; }
            else DrawPhone(PhoneState.InCall, Speaker("오프닝", "점장"), Loc.F("통화 중 {0:00}:{1:00}", 0, 21));
            Show(phone, true);
            DrawSkip(stage == 0 ? 0.45f : 0f);   // 건너뛰기 안내(처음 장면은 반쯤 누르고 있는 모습)
            if (stage == 1)
            {
                var l = Section("오프닝");
                PreviewSay(l.Count > 8 ? l[8].who : "점장", l.Count > 9 ? l[9].line : "");
            }
        }

        /// <summary>미리보기: 0 휴대폰이 울림, 1 역할이 바뀐 통화, 2 마지막 CCTV 2번, 3 끝.</summary>
        public void PreviewEnding(int stage)
        {
            Begin();
            SetFade(0f);
            phoneCenter = new Vector2(170f, 30f);
            if (stage == 0) { DrawPhone(PhoneState.Incoming, Speaker("엔딩", "모르는 번호"), "수신 전화"); Show(phone, true); }
            else if (stage == 1)
            {
                DrawPhone(PhoneState.InCall, Speaker("엔딩", "모르는 번호"), Loc.F("통화 중 {0:00}:{1:00}", 0, 34));
                Show(phone, true);
                var l = Section("엔딩");
                PreviewSay("나", l.Count > 9 ? l[9].line : "");
            }
            else if (stage == 2) ShowFinal(true);
            else
            {
                Show(backdrop, true);
                screenTexts.Add(textRoot, "03:33", -160, -2, 320, 40, TextAnchor.MiddleCenter, new Color32(255, 122, 51, 255), titleFont, 36);
                screenTexts.Add(textRoot, "끝", -160, -40, 320, 20, TextAnchor.MiddleCenter, Cream, bodyFont, 12);
            }
        }

        void PreviewSay(string speaker, string line)
        {
            Show(box, true);
            box.sprite = string.IsNullOrEmpty(speaker) ? plainSprite : boxSprite;
            if (!string.IsNullOrEmpty(speaker)) dialogTexts.Add(textRoot, speaker, -250, -117, 120, 14, TextAnchor.LowerLeft, Cream, titleFont, 12);
            dialogTexts.Add(textRoot, line, -246, -146, 492, 16, TextAnchor.LowerLeft, Cream, bodyFont, 10);
            dialogTexts.Add(textRoot, "▼", 234, -168, 14, 12, TextAnchor.LowerLeft, Cream, bodyFont, 10);
        }

        public void PreviewHide()
        {
            End();
        }
    }
}
