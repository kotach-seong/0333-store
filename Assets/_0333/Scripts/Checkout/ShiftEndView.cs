using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 06:00 근무 끝 장면.
    ///  1) 아침 교대 알바생이 들어와 "수고하셨습니다. 무슨 특별한 일 없었죠?"(대사창, 한 글자씩)
    ///  2) 퇴근: 화면이 어두워진다
    ///  3) 그날 손님 10명의 결과: 초상화 + 정상/비정상과 그 이유.
    ///     내 판정이 맞았으면 컬러 얼굴, 틀렸으면 회색 얼굴에 빨간 X
    ///  4) 클릭하면 "DAY 2 / 00:00"이 뜨고, 다음 밤이 준비되면 화면이 밝아진다
    /// 지금은 교대 알바생 그림이 없어서 코스프레 손님 그림을 대신 세운다.
    /// </summary>
    public class ShiftEndView : MonoBehaviour
    {
        [Header("연결")]
        public MouseLookCamera look;
        public GameObject panel;
        [Tooltip("화면 전체를 덮는 검은 막")]
        public SpriteRenderer fade;
        [Tooltip("대사창·결과 화면을 그리는 화면 크기 그림")]
        public SpriteRenderer board;
        public RectTransform textRoot;
        public AudioSource sfx;
        [Tooltip("흔적 3: 06:00에 교대 알바생 대신 나타나는 괴물(계산대 앞, 사람 크기). 그림이 없으면 코드로 그린 임시")]
        public SpriteRenderer monster;
        [Tooltip("괴물이 서는 자리")]
        public Vector3 monsterHome;
        [Tooltip("가게 형광등(흔적 3에서 하나씩 나간다). 없으면 검은 막으로 대신한다")]
        public StoreLights lights;

        [Header("죽음: 다가와서 덮친다")]
        [Tooltip("불이 깜빡일 때마다 다가오는 괴물(전신, 걷는 동작)")]
        public SpriteRenderer stalker;
        public Sprite[] stalkerWalk = new Sprite[0];
        [Tooltip("세 번 다가오는 자리(발, 월드 픽셀): 문 앞 → 통로 가운데 → 계산대 몇 걸음 앞")]
        public Vector3[] stalkerAt = new Vector3[0];
        public float[] stalkerScale = new float[0];
        [Tooltip("그 자리의 깊이(m): 받는 빛을 정한다")]
        public float[] stalkerDepth = new float[0];
        [Tooltip("계산대를 넘어 덮쳐 오는 모습(두 팔을 뻗고 입을 벌린 상반신)")]
        public Sprite lungeSprite;
        [Tooltip("뒤로 넘어져 바닥에서 올려다본 화면(괴물이 위에서 입을 벌리고 덮쳐 온다)")]
        public SpriteRenderer floorPov;
        public ScreenFx fx;

        Sprite monsterSprite;
        int monsterOrder;
        int stalkStage = -1;

        void ShowMonster()
        {
            if (monster == null) return;
            if (monster.sprite == null) monster.sprite = DrawMonster();
            monster.transform.localPosition = monsterHome;
            monster.enabled = true;
        }

        /// <summary>다가오는 괴물: 걷는 동작을 돌리고, 그 자리가 받는 빛만큼 보인다(꺼지면 시커먼 실루엣).</summary>
        IEnumerator Stalk()
        {
            float t = 0f;
            int lastStepFrame = -1;
            while (true)
            {
                int s = stalkStage;
                if (stalker != null && s >= 0 && s < stalkerAt.Length && stalkerWalk.Length > 0)
                {
                    // 그 자리에서도 조금씩 앞으로(다음 자리 쪽으로 최대 20%)
                    float drift = Mathf.Min(0.2f, t * 0.12f);
                    Vector3 next = s + 1 < stalkerAt.Length ? stalkerAt[s + 1] : stalkerAt[s];
                    float sc = s < stalkerScale.Length ? stalkerScale[s] : 1f;
                    float scNext = s + 1 < stalkerScale.Length ? stalkerScale[s + 1] : sc;
                    float scale = Mathf.Lerp(sc, scNext, drift);
                    // 걸음마다 몸이 들썩이고 좌우로 기우뚱(한 바퀴 8장 = 두 걸음)
                    float phase = t * 6f / stalkerWalk.Length * Mathf.PI * 2f;
                    var bob = new Vector3(Mathf.Round(Mathf.Sin(phase) * 2f * scale), Mathf.Round(Mathf.Abs(Mathf.Sin(phase)) * 4f * scale), 0f);
                    stalker.transform.position = Vector3.Lerp(stalkerAt[s], next, drift) + bob;
                    stalker.transform.localScale = new Vector3(scale, scale, 1f);
                    int frame = Mathf.FloorToInt(t * 6f) % stalkerWalk.Length;
                    stalker.sprite = stalkerWalk[frame];
                    float light = lights != null && s < stalkerDepth.Length ? lights.LevelAt(Mathf.Lerp(stalkerDepth[s], s + 1 < stalkerDepth.Length ? stalkerDepth[s + 1] : stalkerDepth[s], drift)) : 1f;
                    Color dark = lights != null ? lights.darkTint * 0.55f : Color.black;
                    stalker.color = Color.Lerp(new Color(dark.r, dark.g, dark.b, 1f), Color.white, light);
                    stalker.enabled = true;
                    // 걸음마다 무거운 발소리(보일 때 더 크게)
                    if ((frame == 0 || frame == stalkerWalk.Length / 2) && frame != lastStepFrame)
                        SoundFx.Play(sfx, SoundFx.HeavyStep, 0.35f + 0.35f * light);
                    lastStepFrame = frame;
                    t += Time.deltaTime;
                }
                else if (stalker != null) stalker.enabled = false;
                yield return null;
            }
        }

        /// <summary>
        /// 덮친다: 계산대를 넘어 두 팔을 뻗고 화면 가득 달려든다 → 쿵, 뒤로 넘어진다(암전) →
        /// 바닥에서 올려다본 천장 아래로 괴물이 입을 벌리고 덮쳐 온다 → 암전.
        /// </summary>
        IEnumerator Pounce()
        {
            if (monster != null && lungeSprite != null)
            {
                monsterSprite = monster.sprite;
                monsterOrder = monster.sortingOrder;
                monster.sprite = lungeSprite;
                monster.sortingOrder = 55;   // 계산대(50)를 넘어 앞으로
                monster.color = Color.white;
                SoundFx.Play(sfx, SoundFx.Roar, 1f);
                if (fx != null) fx.Flash(0.25f, 0.5f);
                Vector3 from = monsterHome + new Vector3(0f, -14f, 0f);
                Vector3 to = (look != null ? (Vector3)(Vector2)look.transform.position : monsterHome) + new Vector3(0f, -70f, 0f);
                to.z = from.z;
                const float s0 = 1.55f, time = 0.42f;
                for (float t = 0f; t < time; t += Time.deltaTime)
                {
                    float k = t / time, e = k * k;
                    float s = Mathf.Lerp(s0, s0 * 5.5f, e);
                    monster.transform.localScale = new Vector3(s, s, 1f);
                    monster.transform.localPosition = Vector3.Lerp(from, to, e);
                    if (fx != null) fx.shake = 0.5f + 0.5f * k;
                    yield return null;
                }
            }
            // 쿵: 뒤로 넘어진다
            SoundFx.Play(sfx, SoundFx.Thud, 1f);
            SetFade(1f);
            if (fx != null) fx.shake = 0f;
            RestoreMonster();
            yield return new WaitForSeconds(0.3f);
            if (floorPov == null) yield break;

            // 바닥에서 올려다본다: 부딪힌 충격으로 기울었던 화면이 바로 서고, 천장 등이 깜빡인다
            floorPov.enabled = true;
            floorPov.color = Color.white;
            SetFade(0f);
            for (float t = 0f; t < 0.32f; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.32f, 3f);
                floorPov.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(9f, 0f, k));
                floorPov.transform.localPosition = new Vector3(Mathf.Lerp(16f, 0f, k), Mathf.Lerp(-24f, 0f, k), floorPov.transform.localPosition.z);
                floorPov.transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1.02f, k);
                yield return null;
            }
            SoundFx.Play(sfx, SoundFx.Breath, 0.9f);
            for (float t = 0f; t < 1.0f; t += Time.deltaTime)
            {
                float k = t / 1.0f;
                floorPov.transform.localScale = Vector3.one * Mathf.Lerp(1.02f, 1.14f, k * k);
                float flicker = !GameSettings.ReduceFlicker && Random.value < 0.12f ? Random.Range(0.35f, 0.7f) : 1f;   // 머리 위 형광등이 지지직(깜빡임 줄이기면 없음)
                floorPov.color = new Color(flicker, flicker, flicker, 1f);
                yield return null;
            }
            // 입이 덮쳐 온다
            floorPov.color = Color.white;
            SoundFx.Play(sfx, SoundFx.Scream, 1f);
            if (fx != null) fx.Flash(0.2f, 0.7f);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                floorPov.transform.localScale = Vector3.one * Mathf.Lerp(1.14f, 1.5f, (t / 0.2f) * (t / 0.2f));   // 몸이 화면에 남아 있게 너무 들이밀지 않는다
                if (fx != null) fx.shake = 1f;
                yield return null;
            }
            SetFade(1f);
            if (fx != null) fx.shake = 0f;
            floorPov.enabled = false;
            floorPov.transform.localScale = Vector3.one;
            floorPov.transform.localRotation = Quaternion.identity;
            floorPov.transform.localPosition = new Vector3(0f, 0f, floorPov.transform.localPosition.z);
        }

        void RestoreMonster()
        {
            if (monster == null) return;
            monster.enabled = false;
            if (monsterSprite != null) { monster.sprite = monsterSprite; monster.sortingOrder = monsterOrder; monsterSprite = null; }
            monster.transform.localScale = Vector3.one;
            monster.transform.localPosition = monsterHome;
        }

        [Header("글꼴(갈무리)")]
        public Font titleFont;
        public Font bodyFont;
        public int titleSize = 12, bodySize = 10;

        [Header("대사")]
        public string workerName = "아침 알바생";
        public string workerLine = "수고하셨습니다. 무슨 특별한 일 없었죠?";
        public float charsPerSecond = 22f;

        [Header("전화 목소리")]
        [Tooltip("전화 너머 웅얼거리는 목소리. 아래 화자의 대사가 찍히는 동안 말한다")]
        public PhoneVoice phoneVoice;
        public string[] phoneSpeakers = { "점장" };
        /// <summary>켜 두면 한 통화(Say) 안에서 줄이 갈수록 목소리가 늘어지고 깨진다(3:33 점장 전화).</summary>
        [System.NonSerialized] public bool warpVoice;
        float lineWarp;

        [Header("색")]
        public Color32 cream = new Color32(237, 230, 200, 255);
        public Color32 soft = new Color32(150, 146, 138, 255);
        public Color32 red = new Color32(226, 78, 68, 255);
        public Color32 green = new Color32(150, 210, 140, 255);

        const int SW = 640, SH = 360;
        PixelTex pt;
        readonly List<Text> pool = new List<Text>();
        int used;

        public bool Playing { get; private set; }

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        // ── 순서 ──────────────────────────────────────────────
        /// <summary>근무 중 짧은 대사(경찰 등). 화면은 밝은 채, 끝나면 시선이 풀린다.</summary>
        public IEnumerator Say(string speaker, params string[] lines)
        {
            bool wasPlaying = Playing;
            if (!wasPlaying) Begin();
            // 3:33 점장 전화는 원래 빠르기(1배속) 목소리, 평소 전화는 2배속
            if (phoneVoice != null && OnPhone(speaker)) phoneVoice.UseSlow(warpVoice);
            for (int i = 0; i < lines.Length; i++)
            {
                // 3:33 점장 전화: 뒤로 갈수록 목소리가 늘어진다(마지막 줄이 가장 심하게)
                lineWarp = warpVoice && lines.Length > 1 ? Mathf.Pow((float)i / (lines.Length - 1), 1.5f) : 0f;
                yield return Dialogue(speaker, lines[i]);
            }
            lineWarp = 0f;
            if (phoneVoice != null) phoneVoice.Hush();
            if (!wasPlaying) End();
        }

        bool OnPhone(string speaker) => phoneVoice != null && phoneSpeakers != null && System.Array.IndexOf(phoneSpeakers, speaker) >= 0;

        /// <summary>흔적 3: 06:00 교대 알바생 대신, 미친 듯이 깜빡이던 불이 들어오면 괴물이 서 있다 → 죽음. 끝나면 화면은 검은 채(그날 다시).</summary>
        public IEnumerator PlayDeath(int day, System.Action onDark = null, bool fromCurrentLights = false)
        {
            Begin();
            if (look != null) look.Recenter();   // 계산대 앞을 보게
            if (lights != null)
            {
                // 형광등이 나가는 동안 세 번에 걸쳐 다가온다(문 앞 → 통로 가운데 → 계산대 몇 걸음 앞).
                // 확 꺼질 때마다 한 걸음 가까이, 깜빡이는 사이로 보인다. 3:33에서 죽으면 남아 있던 앞줄에서 시작한다
                bool cleaned = false;
                stalkStage = -1;
                Coroutine stalk = StartCoroutine(Stalk());
                yield return lights.FailApproach(fromCurrentLights, s =>
                {
                    if (!cleaned) { cleaned = true; onDark?.Invoke(); }   // 처음 꺼질 때 남은 것을 치운다(3:33 이벤트의 손님·줄 등)
                    stalkStage = s < 3 ? s : -1;
                });
                StopCoroutine(stalk);
                stalkStage = -1;
                if (stalker != null) stalker.enabled = false;
                if (!cleaned) onDark?.Invoke();
                // 완전한 어둠: 보이지 않는데 발소리가 바로 앞까지 오고, 거친 숨소리
                yield return new WaitForSeconds(0.5f);
                for (int i = 0; i < 3; i++) { SoundFx.Play(sfx, SoundFx.HeavyStep, 0.6f + 0.2f * i); yield return new WaitForSeconds(0.45f); }
                SoundFx.Play(sfx, SoundFx.Breath, 0.8f);
                yield return new WaitForSeconds(1.2f);
                // 앞줄 등이 틱 켜지는 순간 이미 계산대 바로 앞에 서 있다 → 잠깐 나를 본다
                ShowMonster();
                yield return lights.StutterOn(StoreLights.FrontRow);
                yield return new WaitForSeconds(1.1f);
                // 계산대를 넘어 덮친다 → 넘어진 나를 위에서
                yield return Pounce();
            }
            else
            {
                // 형광등 제어가 없는 씬: 검은 막으로 대신한다
                for (int i = 0; i < 12; i++)
                {
                    SetFade(Random.Range(0.8f, 1f)); yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
                    SetFade(Random.Range(0f, 0.4f)); yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
                }
                SetFade(1f); onDark?.Invoke(); yield return new WaitForSeconds(0.9f);
                ShowMonster();
                SetFade(0f); yield return new WaitForSeconds(1.4f);
            }
            SetFade(1f);   // 뚝 끊기듯
            if (monster != null) monster.enabled = false;
            if (lights != null) lights.ResetAll();   // 화면이 검은 사이에 가게 불을 되돌린다(그날 다시)
            yield return new WaitForSeconds(0.8f);
            // 죽으면 처음부터(사용자 결정): "메인 화면으로" 버튼을 누르면 DAY 1 기록으로 메인 화면에 돌아간다
            yield return TitleButton("죽었습니다", "메인 화면으로", red);
            ClearAll();
        }

        /// <summary>실종 3: 경찰이 "당신 때문이야" 하며 나를 잡아간다. 끝나면 화면은 검은 채(그날 다시).</summary>
        public IEnumerator PlayArrest(int day, CustomerView cop)
        {
            Begin();
            yield return Dialogue("경찰", "당신 때문이야.");
            yield return FadeTo(1f, 0.7f);
            if (cop != null) cop.SetVisible(false);
            yield return new WaitForSeconds(0.6f);
            // 체포도 처음부터(사용자 결정): "메인 화면으로" 버튼 → DAY 1 기록으로 메인 화면
            yield return TitleButton("체포되었습니다", "메인 화면으로", red);
            ClearAll();
        }

        /// <summary>그림이 없을 때 쓰는 임시 괴물: 검은 형체, 빛나는 눈, 이빨이 늘어선 입.</summary>
        static Sprite DrawMonster()
        {
            const int w = 128, h = 128;
            var pt = new PixelTex(w, h);
            var body = new Color32(6, 6, 9, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w * 0.5f;
                    float hx = cx / 17f, hy = (y + 0.5f - 100f) / 22f;
                    bool head = hx * hx + hy * hy <= 1f + (PixelTex.Noise(x, y, 3) - 0.5f) * 0.25f;
                    float half = Mathf.Lerp(46f, 24f, Mathf.Clamp01(y / 86f)) + (PixelTex.Noise(x / 2, y, 5) - 0.5f) * 6f;
                    bool torso = y < 86 && Mathf.Abs(cx) <= half;
                    if (head || torso) pt.Put(x, y, body);
                }
            // 눈
            foreach (int ex in new[] { 54, 70 })
            {
                pt.Fill(ex - 2, 104, ex + 4, 108, new Color32(200, 30, 30, 120));
                pt.Fill(ex - 1, 105, ex + 3, 107, new Color32(255, 246, 230, 255));
            }
            // 이빨이 늘어선 입
            for (int x = 46; x < 82; x++)
            {
                pt.Put(x, 92, new Color32(60, 10, 12, 255));
                if (x % 3 != 0) { pt.Put(x, 93, new Color32(232, 226, 210, 255)); pt.Put(x, 91, new Color32(232, 226, 210, 255)); }
                if (x % 3 == 1) { pt.Put(x, 94, new Color32(232, 226, 210, 255)); pt.Put(x, 90, new Color32(232, 226, 210, 255)); }
            }
            return pt.ToSprite(new Vector2(0.5f, 0f));
        }

        /// <summary>교대 알바생 → 퇴근 → 결과 → 다음 날 제목. 끝나면 화면은 검은 채로 남는다(FadeIn으로 밝힌다).</summary>
        public IEnumerator PlayEnd(int day, IList<ChecklistEntry> records, CustomerView worker, bool hasNextDay, string summary = "", bool endTitle = true,
                                   IEnumerator farewell = null)
        {
            Begin();
            if (worker != null)
            {
                worker.Enter();
                while (!worker.Visible || worker.Busy) yield return null;
                yield return new WaitForSeconds(0.5f);
            }
            // 마지막 날(엔딩): 교대 알바생이 처음으로 고개를 들어 나를 본다(StoryView.WorkerFarewell). 평소엔 늘 하는 인사
            if (farewell != null) yield return farewell;
            else yield return Dialogue(workerName, workerLine);

            yield return FadeTo(1f, 1.2f);
            ClearAll();
            if (worker != null) worker.SetVisible(false);
            yield return new WaitForSeconds(0.6f);

            ShowResults(day, records, summary);
            yield return WaitClick();
            ClearAll();

            if (hasNextDay)
            {
                ShowTitle($"DAY {day + 1}", "00:00");
                yield return new WaitForSeconds(2.2f);
            }
            else if (endTitle)
            {
                ShowTitle("근무 끝", "(엔딩은 아직 없어요)");
                yield return WaitClick();
            }
            ClearAll();
        }

        /// <summary>다음 밤이 준비된 뒤 화면을 밝힌다.</summary>
        public IEnumerator FadeIn()
        {
            yield return FadeTo(0f, 1.2f);
            End();
        }

        void Begin()
        {
            Playing = true;
            panel.SetActive(true);
            SetFade(0f);
            ClearAll();
            if (look != null) look.LookLocked = true;   // 시선 고정, 커서 표시(클릭으로 넘긴다)
        }

        void End()
        {
            ClearAll();
            SetFade(0f);
            panel.SetActive(false);
            if (look != null) look.LookLocked = false;
            Playing = false;
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float from = fade.color.a;
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
            if (fade.sprite == null)
            {
                // 화면 전체를 덮는 검은 막: 작은 흰 그림을 화면 크기로 늘려 색으로 칠한다
                var white = new PixelTex(4, 4);
                white.Clear(new Color32(255, 255, 255, 255));
                fade.sprite = white.ToSprite(new Vector2(0.5f, 0.5f));
                fade.transform.localScale = new Vector3((SW + 8) / 4f, (SH + 8) / 4f, 1f);
            }
            fade.color = new Color(0f, 0f, 0f, a);
            fade.enabled = a > 0.001f;
        }

        static bool Confirm() => GameInput.LeftClickDown || GameInput.SubmitDown;

        IEnumerator WaitClick()
        {
            yield return null;
            while (!Confirm()) yield return null;
            yield return null;
        }

        // ── 대사창 ────────────────────────────────────────────
        IEnumerator Dialogue(string speaker, string line)
        {
            line = Loc.T(line);   // 코드에 적힌 대사(경찰·아침 알바생)도 지금 언어로
            DrawDialogueBox();
            Add(speaker, -250, -117, 120, 14, TextAnchor.LowerLeft, cream, true);
            Text body = Add("", -246, -146, 492, 16, TextAnchor.LowerLeft, cream, true);
            Text more = null;
            yield return null;
            // 전화 너머 목소리: 글자가 찍히는 동안 웅얼거리고, 다 찍히면 숨 쉬는 자리에서 잦아든다
            bool voice = OnPhone(speaker);
            if (voice) { phoneVoice.warp = lineWarp; phoneVoice.Talk(); }
            // 한 글자씩
            float shown = 0f;
            while (shown < line.Length)
            {
                if (Confirm()) { shown = line.Length; break; }
                shown += Time.deltaTime * charsPerSecond * (voice ? Mathf.Lerp(1f, 0.6f, lineWarp) : 1f);
                body.text = line.Substring(0, Mathf.Min(line.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            body.text = line;
            if (voice) phoneVoice.Quiet();
            more = Add("▼", 234, -168, 14, 12, TextAnchor.LowerLeft, cream);
            yield return null;
            while (!Confirm())
            {
                more.enabled = Time.unscaledTime % 0.8f < 0.5f;
                yield return null;
            }
            yield return null;
            ClearAll();
        }

        void DrawDialogueBox()
        {
            BeginBoard();
            int ox = SW / 2, oy = SH / 2;
            var box = new RectInt(-260 + ox, -174 + oy, 520, 56);
            pt.Fill(box.x, box.y, box.xMax, box.yMax, new Color32(12, 12, 18, 236));
            pt.Outline(box.x, box.y, box.xMax, box.yMax, cream);
            pt.Outline(box.x + 2, box.y + 2, box.xMax - 2, box.yMax - 2, new Color32(70, 66, 60, 255));
            // 이름표
            var tag = new RectInt(-260 + ox, -118 + oy, 92, 17);
            pt.Fill(tag.x, tag.y, tag.xMax, tag.yMax, new Color32(12, 12, 18, 236));
            pt.Outline(tag.x, tag.y, tag.xMax, tag.yMax, cream);
            EndBoard();
        }

        // ── 결과 화면 ─────────────────────────────────────────
        /// <summary>그날 손님들의 결과(에디터 미리보기에서도 부른다).</summary>
        public void ShowResults(int day, IList<ChecklistEntry> records, string summary = "")
        {
            ClearAll();
            BeginBoard();
            int ox = SW / 2, oy = SH / 2;
            pt.Fill(0, 0, SW, SH, new Color32(6, 6, 9, 255));

            int correct = 0;
            foreach (var r in records) if (r.Correct) correct++;
            Add(Loc.F("DAY {0}  근무 기록", day), -200, 150, 400, 16, TextAnchor.LowerCenter, cream, true);
            string line = Loc.F("맞힌 판정 {0} / {1}", correct, records.Count);
            if (!string.IsNullOrEmpty(summary)) line += "     " + summary;
            Add(line, -250, 135, 500, 12, TextAnchor.LowerCenter, soft);
            pt.Fill(-300 + ox, 131 + oy, 300 + ox, 132 + oy, new Color32(70, 66, 60, 255));

            for (int i = 0; i < records.Count && i < 10; i++)
            {
                ChecklistEntry r = records[i];
                int col = i / 5, row = i % 5;
                int bx = col == 0 ? -305 : 15, top = 122 - row * 56;
                var box = new RectInt(bx + ox, top - 46 + oy, 58, 46);
                bool wrong = !r.Correct;
                pt.Fill(box.x, box.y, box.xMax, box.yMax, new Color32(30, 30, 38, 255));
                DrawPortrait(r.view, box.x + 1, box.y + 1, 56, 44, wrong);
                pt.Outline(box.x, box.y, box.xMax, box.yMax, wrong ? new Color32(110, 100, 96, 255) : cream);
                if (wrong) DrawX(box);

                int tx = bx + 64;
                Add($"No.{r.number:0000}   {r.time}   {Loc.T(r.label)}", tx, top - 12, 240, 12, TextAnchor.LowerLeft, soft);
                if (!r.strange)
                {
                    Add("정상인", tx, top - 27, 200, 13, TextAnchor.LowerLeft, green, true);
                }
                else
                {
                    Add("비정상", tx, top - 27, 200, 13, TextAnchor.LowerLeft, red, true);
                    for (int k = 0; k < r.reasons.Length && k < 2; k++)
                        Add("· " + r.reasons[k], tx + 2, top - 40 - k * 11, 240, 12, TextAnchor.LowerLeft, cream);
                }
            }
            Add("클릭하면 계속", -200, -176, 400, 12, TextAnchor.LowerCenter, soft);
            EndBoard();
        }

        /// <summary>손님 앞모습 그림에서 얼굴 부분을 잘라 넣는다. 틀린 판정이면 회색.</summary>
        void DrawPortrait(CustomerView view, int x0, int y0, int w, int h, bool gray)
        {
            Sprite sp = view != null && view.counterBody != null ? view.counterBody.sprite : null;
            Texture2D tex = sp != null ? sp.texture : null;
            if (tex == null || !tex.isReadable)
            {
                // 그림을 읽을 수 없으면 실루엣 대신 물음표
                pt.Fill(x0 + w / 2 - 6, y0 + 8, x0 + w / 2 + 6, y0 + h - 8, new Color32(80, 80, 90, 255));
                return;
            }
            Rect tr = sp.textureRect;
            int tw = (int)tr.width, th = (int)tr.height, tx0 = (int)tr.x, ty0 = (int)tr.y;
            Color32[] px = tex.GetPixels32();
            int top = -1;
            for (int y = th - 1; y >= 0 && top < 0; y--)
                for (int x = 0; x < tw; x++)
                    if (px[(ty0 + y) * tex.width + tx0 + x].a > 20) { top = y; break; }
            if (top < 0) return;
            int cx = tw / 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = cx - w / 2 + x, sy = top - (h - 1) + y + 1;
                    if (sx < 0 || sy < 0 || sx >= tw || sy >= th) continue;
                    Color32 c = px[(ty0 + sy) * tex.width + tx0 + sx];
                    if (c.a < 20) continue;
                    if (gray)
                    {
                        byte l = (byte)Mathf.Clamp((0.3f * c.r + 0.59f * c.g + 0.11f * c.b) * 0.75f, 0, 255);
                        c = new Color32(l, l, l, 255);
                    }
                    pt.Put(x0 + x, y0 + y, new Color32(c.r, c.g, c.b, 255));
                }
        }

        void DrawX(RectInt b)
        {
            Color32 r = red;
            for (int t = 0; t < 2; t++)
            {
                pt.Line(b.x + 2 + t, b.y + 2, b.xMax - 3 + t, b.yMax - 3, r);
                pt.Line(b.x + 2 + t, b.yMax - 3, b.xMax - 3 + t, b.y + 2, r);
            }
        }

        // ── 다음 날 제목 ──────────────────────────────────────
        // ── 제목 + 버튼(죽었을 때: "메인 화면으로") ──
        static readonly Rect TitleButtonRect = new Rect(-72f, -38f, 144f, 26f);

        /// <summary>큰 제목 아래 테두리 있는 버튼 하나. 마우스를 올리면 밝아지고, 누르거나 Space·Enter로 넘어간다.</summary>
        IEnumerator TitleButton(string big, string label, Color32 bigColor)
        {
            bool hover = false;
            DrawTitleButton(big, label, bigColor, hover);
            yield return null;
            Camera cam = look != null ? look.GetComponent<Camera>() : Camera.main;
            while (true)
            {
                bool over = false;
                if (cam != null)
                {
                    Vector2 p = (Vector2)cam.ScreenToWorldPoint(GameInput.MousePosition) - (Vector2)cam.transform.position;
                    over = TitleButtonRect.Contains(p);
                }
                if (over != hover)
                {
                    hover = over;
                    DrawTitleButton(big, label, bigColor, hover);
                    if (over) SoundFx.Play(sfx, SoundFx.Tick, 0.4f);
                }
                if ((GameInput.LeftClickDown && over) || GameInput.SubmitDown) break;
                yield return null;
            }
            SoundFx.Play(sfx, SoundFx.Tick, 0.9f);
            yield return null;
        }

        void DrawTitleButton(string big, string label, Color32 bigColor, bool hover)
        {
            ClearAll();
            BeginBoard();
            int ox = SW / 2, oy = SH / 2;
            Rect b = TitleButtonRect;
            int x0 = (int)b.x + ox, y0 = (int)b.y + oy, x1 = (int)b.xMax + ox, y1 = (int)b.yMax + oy;
            pt.Fill(x0, y0, x1, y1, hover ? new Color32(92, 26, 24, 255) : new Color32(22, 14, 16, 255));
            pt.Outline(x0, y0, x1, y1, hover ? red : soft);
            pt.Outline(x0 + 2, y0 + 2, x1 - 2, y1 - 2, hover ? new Color32(140, 52, 46, 255) : new Color32(58, 50, 50, 255));
            EndBoard();
            Text t = Add(big, -200, 14, 400, 40, TextAnchor.LowerCenter, bigColor, true);
            t.fontSize = titleSize * 3;
            Text s = Add(label, (int)b.x, (int)b.y + 7, (int)b.width, 14, TextAnchor.LowerCenter, hover ? cream : soft, true);
            s.fontSize = titleSize;
        }

        /// <summary>미리보기: 죽었을 때·체포됐을 때의 제목과 "메인 화면으로" 버튼(마우스를 올린 모습).</summary>
        public void PreviewDeathButton(bool hover, string big = "죽었습니다")
        {
            panel.SetActive(true);
            if (monster != null) monster.enabled = false;
            SetFade(1f);
            DrawTitleButton(big, "메인 화면으로", red, hover);
        }

        public void ShowTitle(string big, string small) => ShowTitle(big, small, cream);

        public void ShowTitle(string big, string small, Color32 bigColor)
        {
            ClearAll();
            Text t = Add(big, -200, 4, 400, 40, TextAnchor.LowerCenter, bigColor, true);
            t.fontSize = titleSize * 3;
            Text s = Add(small, -200, -26, 400, 26, TextAnchor.LowerCenter, soft, true);
            s.fontSize = titleSize * 2;
        }

        // ── 그림·글자 도우미 ─────────────────────────────────
        void BeginBoard()
        {
            if (pt == null) pt = new PixelTex(SW, SH);
            pt.Clear();
        }

        void EndBoard()
        {
            board.sprite = pt.ToSprite(new Vector2(0.5f, 0.5f));
            board.enabled = true;
        }

        void ClearAll()
        {
            for (int i = 0; i < pool.Count; i++) if (pool[i] != null) pool[i].gameObject.SetActive(false);
            used = 0;
            if (board != null) board.enabled = false;
        }

        Text Add(string text, int x, int y, int w, int h, TextAnchor align, Color32 color, bool title = false)
        {
            while (pool.Count <= used)
            {
                var go = new GameObject($"Text {pool.Count}", typeof(RectTransform));
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(textRoot, false);
                var rt0 = (RectTransform)go.transform;
                rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0.5f);
                rt0.pivot = Vector2.zero;
                var tx = go.AddComponent<Text>();
                tx.horizontalOverflow = HorizontalWrapMode.Overflow;
                tx.verticalOverflow = VerticalWrapMode.Overflow;
                tx.supportRichText = false;
                tx.raycastTarget = false;
                pool.Add(tx);
            }
            Text t = pool[used++];
            t.gameObject.SetActive(true);
            t.enabled = true;
            t.font = title ? titleFont : bodyFont;
            t.fontSize = title ? titleSize : bodySize;
            t.color = color;
            t.alignment = align;
            t.text = Loc.T(text);   // 화면 글자는 지금 언어로(표에 없으면 그대로)
            var rt = (RectTransform)t.transform;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return t;
        }

        // ── 에디터 미리보기 ───────────────────────────────────
        public void PreviewDialogue(string speaker = null, string line = null)
        {
            panel.SetActive(true);
            SetFade(0f);
            ClearAll();
            DrawDialogueBox();
            Add(speaker ?? workerName, -250, -117, 120, 14, TextAnchor.LowerLeft, cream, true);
            Add(line ?? workerLine, -246, -146, 492, 16, TextAnchor.LowerLeft, cream, true);
            Add("▼", 234, -168, 14, 12, TextAnchor.LowerLeft, cream);
        }

        /// <summary>미리보기: 다 나간 형광등 중 앞줄이 틱틱 켜진 순간, 괴물이 서 있다.</summary>
        public void PreviewMonster()
        {
            panel.SetActive(true);
            ClearAll();
            SetFade(0f);
            if (lights != null) { lights.SetAll(0f); lights.Set(StoreLights.FrontRow, 1f); }
            if (monster == null) return;
            if (monster.sprite == null) monster.sprite = DrawMonster();
            monster.enabled = true;
            monster.transform.localPosition = monsterHome;
        }

        /// <summary>
        /// 미리보기(죽음): 0 문 앞(다 켜짐) / 1 통로 가운데(안쪽 줄 꺼짐) / 2 계산대 몇 걸음 앞(앞줄만) /
        /// 3 계산대를 넘어 덮쳐 오는 중 / 4 바닥에서 올려다본 화면.
        /// </summary>
        public void PreviewDeath(int stage)
        {
            panel.SetActive(true);
            ClearAll();
            SetFade(0f);
            if (stalker != null) stalker.enabled = false;
            if (monster != null) monster.enabled = false;
            if (floorPov != null) floorPov.enabled = false;
            if (lights != null)
            {
                lights.SetAll(1f);
                if (stage >= 1) lights.Set(new[] { 4, 5 }, 0f);
                if (stage >= 2) lights.Set(new[] { 2, 3 }, 0f);
            }
            if (stage <= 2 && stalker != null && stage < stalkerAt.Length && stalkerWalk.Length > 0)
            {
                stalker.sprite = stalkerWalk[(stage * 3) % stalkerWalk.Length];
                stalker.transform.position = stalkerAt[stage];
                float s = stage < stalkerScale.Length ? stalkerScale[stage] : 1f;
                stalker.transform.localScale = new Vector3(s, s, 1f);
                float light = lights != null && stage < stalkerDepth.Length ? lights.LevelAt(stalkerDepth[stage]) : 1f;
                Color dark = lights != null ? lights.darkTint * 0.55f : Color.black;
                stalker.color = Color.Lerp(new Color(dark.r, dark.g, dark.b, 1f), Color.white, light);
                stalker.enabled = true;
            }
            else if (stage == 3 && monster != null && lungeSprite != null)
            {
                if (lights != null) { lights.SetAll(0f); lights.Set(StoreLights.FrontRow, 1f); }
                monster.sprite = lungeSprite;
                monster.sortingOrder = 55;
                monster.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
                monster.transform.localPosition = monsterHome + new Vector3(0f, -40f, 0f);
                monster.enabled = true;
            }
            else if (stage == 4 && floorPov != null)
            {
                floorPov.enabled = true;
                floorPov.transform.localScale = Vector3.one * 1.08f;
            }
        }

        /// <summary>죽음 미리보기를 되돌린다(괴물 그림·층·크기).</summary>
        public void PreviewDeathReset(Sprite standing, int order)
        {
            if (stalker != null) stalker.enabled = false;
            if (floorPov != null) { floorPov.enabled = false; floorPov.transform.localScale = Vector3.one; }
            if (monster != null)
            {
                monster.enabled = false;
                monster.sprite = standing;
                monster.sortingOrder = order;
                monster.transform.localScale = Vector3.one;
                monster.transform.localPosition = monsterHome;
            }
            if (lights != null) lights.SetAll(1f);
            panel.SetActive(false);
        }

        public void PreviewEndTitle(string big, string small, bool redTitle)
        {
            panel.SetActive(true);
            if (monster != null) monster.enabled = false;
            SetFade(1f);
            ShowTitle(big, small, redTitle ? red : cream);
        }

        public void PreviewResults(int day, IList<ChecklistEntry> records)
        {
            panel.SetActive(true);
            SetFade(1f);
            ShowResults(day, records);
        }

        public void PreviewTitle(int day)
        {
            panel.SetActive(true);
            SetFade(1f);
            ShowTitle($"DAY {day}", "00:00");
        }

        public void PreviewHide()
        {
            ClearAll();
            SetFade(0f);
            if (monster != null) monster.enabled = false;
            if (lights != null) lights.SetAll(1f);
            panel.SetActive(false);
        }

        /// <summary>미리보기: 형광등이 다 나간 가게(빛나는 것만 남음).</summary>
        public void PreviewDark()
        {
            panel.SetActive(true);
            ClearAll();
            SetFade(0f);
            if (monster != null) monster.enabled = false;
            if (lights != null) lights.SetAll(0f);
        }
    }
}
