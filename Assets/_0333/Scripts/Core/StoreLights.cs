using System.Collections;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 가게 천장 형광등 6개. 하나씩 켜고 끌 수 있고, 켜진 등이 적을수록 가게 그림이 "불 꺼진 가게" 그림으로 바뀐다.
    ///  - 등 번호: 0·1 앞줄(계산대 쪽), 2·3 가운데 줄, 4·5 맨 안쪽 줄(4는 낡아서 누런 등).
    ///  - 불 꺼진 그림에는 비상구·냉장고·ATM·LED·모니터처럼 스스로 빛나는 것과 창밖 빛만 남는다.
    ///  - 피·전단·계산대 위 물건처럼 따로 그려진 것도 같이 어두워진다(shadedRoots 아래, 실내 층 이상).
    ///  - 시계·하트(HUD)는 가게 불과 상관없다.
    /// 흔적이 생길 때: <see cref="Glitch"/> 짧게 지지직 깜빡이다 잠깐 꺼진 사이에 흔적이 생긴다.
    /// 흔적 3(괴물): <see cref="Fail"/> 안쪽 줄부터 지지직대며 하나씩 나간다 → <see cref="StutterOn"/> 앞줄이 틱틱 켜진다.
    /// 설정의 깜빡임 줄이기: 눈에 보이는 밝기(shown)가 정한 밝기(level)를 천천히 따라간다.
    /// 아주 짧게 꺼지는 건 무시하고, 꺼질 때는 서서히 어두워지고 켜질 때는 서서히 밝아진다.
    /// 어둠 속에서 무언가 바뀌는 장면(괴물이 다가옴·흔적이 생김)은 정말 어두워질 때까지 기다린다(<see cref="UntilDark"/>).
    /// </summary>
    public class StoreLights : MonoBehaviour
    {
        [Tooltip("켜진 형광등 그림 6개(번호 순)")]
        public SpriteRenderer[] tubes;
        [Tooltip("불 켜진 가게 레이어(실내·계산대·유리·문짝)")]
        public SpriteRenderer[] litLayers;
        [Tooltip("같은 자리의 앞줄만 켜진 레이어(litLayers와 같은 순서, 반투명 레이어는 비어 있음). 켜진 레이어 밑에 깔린다")]
        public SpriteRenderer[] frontLayers;
        [Tooltip("같은 자리의 다 꺼진 레이어(litLayers와 같은 순서). 맨 밑에 깔린다")]
        public SpriteRenderer[] darkLayers;
        [Tooltip("반투명 레이어(유리·문짝)는 덮지 않고 서로 바꿔 끼운다")]
        public bool[] crossfade;
        [Tooltip("이 아래의 그림(피·전단·물건 등)은 가게가 어두워지면 같이 어두워진다. 창밖(층 10 미만)은 그대로")]
        public Transform[] shadedRoots;
        [Tooltip("계산대 위의 것(물품 칸 물건·돈바구니): 바로 위 앞줄 등을 따라 밝다(앞줄만 켜져도 거의 그대로)")]
        public Transform[] counterRoots;
        public Color darkTint = new Color(0.13f, 0.15f, 0.22f, 1f);
        [Tooltip("줄마다 가게 밝기에 기여하는 정도: 앞줄, 가운데, 안쪽")]
        public float[] rowWeight = { 1.5f, 1f, 0.55f };
        public AudioSource sfx;

        public static readonly int[] FrontRow = { 0, 1 };
        public int Count => tubes != null ? tubes.Length : 0;
        public float Brightness { get; private set; } = 1f;

        float[] level, shown, lowSince;
        bool tinted;
        AudioSource hum;

        // 깜빡임 줄이기: 이보다 짧게 어두워지는 건 무시, 초당 이만큼씩 어두워지고 밝아진다
        const float IgnoreDip = 0.07f, FadeDown = 6f, FadeUp = 4f;

        void Awake()
        {
            Init();
            Render();
        }

        void Init()
        {
            if (level != null && level.Length == Count) return;
            level = new float[Count];
            shown = new float[Count];
            lowSince = new float[Count];
            for (int i = 0; i < level.Length; i++) { level[i] = shown[i] = 1f; lowSince[i] = -1f; }
        }

        void Update()
        {
            if (level == null || shown == null) return;
            bool smooth = GameSettings.ReduceFlicker;
            bool moved = false;
            for (int k = 0; k < level.Length; k++)
            {
                if (shown[k] == level[k]) { lowSince[k] = -1f; continue; }
                if (!smooth) shown[k] = level[k];
                else if (level[k] < shown[k])
                {
                    if (lowSince[k] < 0f) lowSince[k] = Time.time;
                    if (Time.time - lowSince[k] < IgnoreDip) continue;   // 아주 짧은 깜빡임은 무시
                    shown[k] = Mathf.MoveTowards(shown[k], level[k], FadeDown * Time.deltaTime);
                }
                else
                {
                    lowSince[k] = -1f;
                    shown[k] = Mathf.MoveTowards(shown[k], level[k], FadeUp * Time.deltaTime);
                }
                moved = true;
            }
            if (moved) Render();
        }

        /// <summary>깜빡임 줄이기로 서서히 어두워지는 중이면, 눈에 보이는 불이 다 꺼질 때까지(최대 max초) 기다린다.</summary>
        public IEnumerator UntilDark(float max = 0.6f)
        {
            for (float t = 0f; t < max && Brightness > 0.02f; t += Time.deltaTime) yield return null;
        }

        /// <summary>등 하나의 밝기(0 꺼짐 ~ 1 켜짐).</summary>
        public void Set(int k, float v)
        {
            Init();
            if (k < 0 || k >= Count) return;
            level[k] = Mathf.Clamp01(v);
            Apply();
        }

        public void Set(int[] ks, float v)
        {
            Init();
            foreach (int k in ks) if (k >= 0 && k < Count) level[k] = Mathf.Clamp01(v);
            Apply();
        }

        public void SetAll(float v)
        {
            Init();
            for (int i = 0; i < level.Length; i++) level[i] = Mathf.Clamp01(v);
            Apply();
        }

        /// <summary>처음처럼 다 켜고 소리도 끈다(죽고 나서 그날을 다시 할 때).</summary>
        public void ResetAll()
        {
            StopHum();
            SetAll(1f);
        }

        /// <summary>정한 밝기(level)가 바뀌었다: 평소엔 바로 보이고, 깜빡임 줄이기면 Update가 천천히 따라간다.</summary>
        void Apply()
        {
            if (GameSettings.ReduceFlicker && Application.isPlaying) return;
            System.Array.Copy(level, shown, level.Length);
            Render();
        }

        /// <summary>눈에 보이는 밝기(shown)로 등·가게 그림·물들일 것들을 맞춘다.</summary>
        void Render()
        {
            float sum = 0f, wsum = 0f, front = 0f, back = 0f, backW = 0f;
            for (int k = 0; k < Count; k++)
            {
                float w = rowWeight.Length > 0 ? rowWeight[Mathf.Min(k / 2, rowWeight.Length - 1)] : 1f;
                sum += w * shown[k];
                wsum += w;
                if (k < 2) front += shown[k] * 0.5f;
                else { back += w * shown[k]; backW += w; }
                if (tubes[k] == null) continue;
                tubes[k].enabled = shown[k] > 0.02f;
                tubes[k].color = new Color(1f, 1f, 1f, shown[k]);
            }
            Brightness = wsum > 0f ? sum / wsum : 1f;
            back = backW > 0f ? back / backW : front;
            float b = Brightness;
            bool dim = b < 0.999f;
            // 불투명한 레이어: [다 꺼짐] 위에 [앞줄만 켜짐](앞줄 밝기만큼) 위에 [다 켜짐](안쪽 줄 밝기만큼, 앞줄이 꺼지면 덜)
            float litA = back * (0.5f + 0.5f * front);
            for (int i = 0; litLayers != null && i < litLayers.Length; i++)
            {
                SpriteRenderer f = frontLayers != null && i < frontLayers.Length ? frontLayers[i] : null;
                SpriteRenderer d = darkLayers != null && i < darkLayers.Length ? darkLayers[i] : null;
                bool cross = crossfade != null && i < crossfade.Length && crossfade[i];
                float a = f != null ? litA : b;
                if (litLayers[i] != null) litLayers[i].color = new Color(1f, 1f, 1f, a);
                if (f != null)
                {
                    f.enabled = a < 0.999f && front > 0.001f;
                    f.color = new Color(1f, 1f, 1f, front);
                }
                if (d == null) continue;
                d.enabled = dim;
                d.color = new Color(1f, 1f, 1f, cross ? 1f - b : 1f);
            }
            // 따로 그려진 것들: 어두울 때만 물들이고, 다시 밝아지면 한 번 흰색으로 되돌린다(평소에는 손대지 않는다)
            if (!dim && !tinted) return;
            tinted = dim;
            Tint(shadedRoots, Color.Lerp(darkTint, Color.white, b));
            // 계산대 위(물건·돈바구니)는 바로 위 앞줄 등이 비춘다: 앞줄이 켜져 있으면 거의 그대로 밝다
            Tint(counterRoots, Color.Lerp(darkTint, Color.white, Mathf.Max(b, front * 0.94f)));
        }

        static void Tint(Transform[] roots, Color tint)
        {
            if (roots == null) return;
            foreach (Transform root in roots)
            {
                if (root == null) continue;
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.sortingOrder >= 10) sr.color = new Color(tint.r, tint.g, tint.b, sr.color.a);
            }
        }

        // ── 소리 ──
        void StartHum(float volume)
        {
            if (!Application.isPlaying) return;
            if (hum == null)
            {
                hum = gameObject.AddComponent<AudioSource>();
                hum.playOnAwake = false;
                hum.loop = true;
                hum.spatialBlend = 0f;
                hum.clip = SoundFx.TubeHum;
            }
            hum.volume = volume;
            if (!hum.isPlaying) hum.Play();
        }

        void StopHum()
        {
            if (hum != null) hum.Stop();
        }

        void Fx(AudioClip clip, float volume = 1f) => SoundFx.Play(sfx, clip, volume);

        // ── 흔적 3: 형광등이 나간다 ──
        /// <summary>
        /// 웅웅거림이 커지면서 낡은 등부터 지지직 → 여기저기 깜빡 → 안쪽 줄부터 한 줄씩 미친 듯이 떨다가 "퍽" 나간다.
        /// 끝나면 모든 등이 꺼지고 웅웅 소리도 끊긴다(정적).
        /// </summary>
        public IEnumerator Fail(bool fromCurrent = false)
        {
            // fromCurrent: 지금 켜진 등만 나간다(3:33처럼 앞줄만 켜진 채로 죽을 때). 아니면 다 켜진 상태에서 시작
            Init();
            if (!fromCurrent) SetAll(1f);
            var start = (float[])level.Clone();
            var lit = new System.Collections.Generic.List<int>();
            for (int k = 0; k < Count; k++) if (start[k] > 0.02f) lit.Add(k);
            StartHum(0.15f);
            // 1) 낡은 등(4)이 먼저 버벅인다
            for (int i = 0; i < 5 && start.Length > 4 && start[4] > 0.02f; i++)
            {
                Set(4, Random.Range(0f, 0.35f)); Fx(SoundFx.TubeBuzz, 0.6f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
                Set(4, start[4]);
                yield return new WaitForSeconds(Random.Range(0.08f, 0.3f));
                if (hum != null) hum.volume = Mathf.Min(0.45f, hum.volume + 0.04f);
            }
            // 2) 여기저기 등이 깜빡, 점점 잦아진다
            for (float t = 0f; t < 1.8f && lit.Count > 0;)
            {
                int k = lit[Random.Range(0, lit.Count)];
                float low = Random.value < 0.5f ? 0f : Random.Range(0.2f, 0.5f);
                Set(k, low); Fx(Random.value < 0.6f ? SoundFx.TubeBuzz : SoundFx.TubeTick, 0.7f);
                float off = Random.Range(0.025f, 0.07f);
                yield return new WaitForSeconds(off);
                Set(k, start[k]);
                float gap = Mathf.Lerp(0.22f, 0.03f, t / 1.8f) * Random.Range(0.6f, 1.4f);
                yield return new WaitForSeconds(gap);
                t += off + gap;
                if (hum != null) { hum.volume = Mathf.Min(0.55f, hum.volume + 0.01f); hum.pitch = Random.Range(0.97f, 1.03f); }
            }
            // 3) 안쪽 줄부터 한 줄씩: 미친 듯이 떨다가 퍽(이미 꺼진 줄은 건너뛴다)
            for (int row = 2; row >= 0; row--)
            {
                int[] pair = { row * 2, row * 2 + 1 };
                if (row > 0 && start[pair[0]] <= 0.02f && start[pair[1]] <= 0.02f) continue;
                int n = Random.Range(5, 9);
                for (int i = 0; i < n; i++)
                {
                    Set(pair, 0f); Fx(SoundFx.TubeBuzz, 0.8f);
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.06f));
                    Set(pair, Random.Range(0.4f, 1f));
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.09f));
                }
                Set(pair, 0f);
                Fx(SoundFx.TubePop, 1f);
                if (row == 0) { StopHum(); break; }   // 마지막 등이 나가면 웅웅거림도 뚝
                if (hum != null) hum.volume *= 0.7f;
                yield return new WaitForSeconds(Random.Range(0.45f, 0.7f));
            }
        }

        // ── 죽음: 깜빡일 때마다 다가오는 괴물 ──
        /// <summary>줄마다 형광등이 달린 깊이(m): 앞줄·가운데·안쪽(가게 그림과 같다).</summary>
        static readonly float[] RowZ = { 3f, 5f, 7f };

        /// <summary>그 깊이(m)에 서 있는 것이 받는 빛(0~1): 가까운 줄의 등일수록 많이 비춘다.</summary>
        public float LevelAt(float z)
        {
            Init();
            float sum = 0f, wsum = 0f;
            for (int row = 0; row < RowZ.Length; row++)
            {
                float d = (z - RowZ[row]) / 1.6f, w = 1f / (1f + d * d);
                float lv = 0f; int n = 0;
                for (int k = row * 2; k < row * 2 + 2 && k < Count; k++) { lv += shown[k]; n++; }
                sum += w * (n > 0 ? lv / n : 0f);
                wsum += w;
            }
            return wsum > 0f ? Mathf.Clamp01(sum / wsum * 1.15f) : 0f;
        }

        /// <summary>
        /// 죽음(흔적 3, 3:33 실패): 형광등이 나가는 동안 괴물이 세 번에 걸쳐 다가온다.
        /// 매번 가게가 확 꺼진 사이에 괴물이 한 걸음 가까이 오고(moveTo(0: 문 앞, 1: 통로 가운데, 2: 계산대 몇 걸음 앞)),
        /// 다시 깜빡이며 켜지는 동안 그 자리에 보인다. 한 번 지날 때마다 안쪽 줄부터 하나씩 죽는다.
        /// 마지막엔 남은 줄이 떨다가 "퍽" — 완전한 어둠(moveTo(3)).
        /// fromCurrent면 지금 켜진 등에서 시작한다(3:33처럼 앞줄만 켜져 있을 때).
        /// </summary>
        public IEnumerator FailApproach(bool fromCurrent, System.Action<int> moveTo)
        {
            Init();
            if (!fromCurrent) SetAll(1f);
            var alive = (float[])level.Clone();
            StartHum(0.15f);
            // 낡은 등(4)이 먼저 버벅인다(짧게)
            for (int i = 0; i < 3 && alive.Length > 4 && alive[4] > 0.02f; i++)
            {
                Set(4, Random.Range(0f, 0.3f)); Fx(SoundFx.TubeBuzz, 0.6f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
                Set(4, alive[4]);
                yield return new WaitForSeconds(Random.Range(0.12f, 0.3f));
            }
            for (int stage = 0; stage < 3; stage++)
            {
                // 확 꺼진다: 그 사이에 괴물이 다가온다
                SetAll(0f); Fx(SoundFx.TubeBuzz, 0.9f);
                if (GameSettings.ReduceFlicker) yield return UntilDark();
                moveTo?.Invoke(stage);
                yield return new WaitForSeconds(0.2f + 0.12f * stage);
                // 다시 켜지며 깜빡깜빡: 그 사이로 괴물이 보인다(갈수록 꺼져 있는 시간이 길다)
                int flicks = 3 + stage;
                for (int i = 0; i < flicks; i++)
                {
                    Restore(alive);
                    if (i == 0) Fx(SoundFx.TubeTick, 1f);
                    yield return new WaitForSeconds(Random.Range(0.14f, 0.32f) * (1f - 0.22f * stage));
                    if (Random.value < 0.5f) SetAll(Random.Range(0f, 0.25f));
                    else { int k = Random.Range(0, Count); Set(k, 0f); }
                    Fx(Random.value < 0.6f ? SoundFx.TubeBuzz : SoundFx.TubeTick, 0.7f);
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.09f) * (1f + 0.6f * stage));
                    if (hum != null) hum.volume = Mathf.Min(0.55f, hum.volume + 0.02f);
                }
                Restore(alive);
                yield return new WaitForSeconds(stage < 2 ? Random.Range(0.35f, 0.6f) : 0.25f);
                // 걸음이 끝날 때마다 안쪽 줄부터 "퍽": 첫 걸음 뒤 안쪽 줄, 두 번째 뒤 가운데 줄(앞줄은 마지막까지)
                int popRow = stage == 0 ? 2 : stage == 1 ? 1 : -1;
                if (popRow > 0)
                {
                    int[] pair = { popRow * 2, popRow * 2 + 1 };
                    if (alive[pair[0]] > 0.02f || alive[pair[1]] > 0.02f)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            Set(pair, 0f); Fx(SoundFx.TubeBuzz, 0.8f);
                            yield return new WaitForSeconds(Random.Range(0.02f, 0.05f));
                            Set(pair, Random.Range(0.4f, 1f));
                            yield return new WaitForSeconds(Random.Range(0.02f, 0.07f));
                        }
                        Set(pair, 0f);
                        alive[pair[0]] = alive[pair[1]] = 0f;
                        Fx(SoundFx.TubePop, 0.9f);
                        if (hum != null) hum.volume *= 0.8f;
                    }
                }
            }
            // 남은 줄(앞줄)이 미친 듯이 떨다가 퍽 → 완전한 어둠, 웅웅 소리도 뚝
            for (int i = 0; i < 6; i++)
            {
                SetAll(0f); Fx(SoundFx.TubeBuzz, 0.8f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.06f));
                Restore(alive);
                for (int k = 0; k < Count; k++) if (level[k] > 0f) Set(k, Random.Range(0.4f, 1f));
                yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
            }
            SetAll(0f);
            Fx(SoundFx.TubePop, 1f);
            StopHum();
            if (GameSettings.ReduceFlicker) yield return UntilDark();
            moveTo?.Invoke(3);
        }

        void Restore(float[] alive)
        {
            for (int k = 0; k < Count && k < alive.Length; k++) level[k] = alive[k];
            Apply();
        }

        /// <summary>계산대 위 앞줄만 켜진 상태로 바로(미리보기용).</summary>
        public void SetFrontOnly()
        {
            Init();
            for (int k = 0; k < level.Length; k++) level[k] = k < 2 ? 1f : 0f;
            Apply();
        }

        // ── 03:33: 계산대 위 앞줄만 남는다 ──
        /// <summary>
        /// 03:33이 되면: 웅웅 소리가 커지며 안쪽 줄, 가운데 줄이 차례로 지지직 떨다가 "퍽" 나간다.
        /// 계산대 위 앞줄만 남고 낮은 웅웅 소리는 이어진다. 다 나간 순간 atFront를 부른다(CCTV도 어두운 화면으로).
        /// </summary>
        public IEnumerator DownToFront(System.Action atFront = null)
        {
            SetAll(1f);
            StartHum(0.12f);
            for (int row = 2; row >= 1; row--)
            {
                int[] pair = { row * 2, row * 2 + 1 };
                int n = Random.Range(4, 7);
                for (int i = 0; i < n; i++)
                {
                    Set(pair, 0f); Fx(SoundFx.TubeBuzz, 0.7f);
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.06f));
                    Set(pair, Random.Range(0.4f, 1f));
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
                    if (hum != null) hum.volume = Mathf.Min(0.35f, hum.volume + 0.02f);
                }
                Set(pair, 0f);
                Fx(SoundFx.TubePop, 0.9f);
                yield return new WaitForSeconds(row == 2 ? Random.Range(0.4f, 0.6f) : 0.15f);
            }
            atFront?.Invoke();
            if (hum != null) hum.volume = 0.1f;   // 남은 앞줄의 낮은 웅웅
        }

        /// <summary>3:33이 끝났다: 꺼졌던 가운데·안쪽 줄이 틱틱 하나씩 다시 켜지고(atLit) 웅웅 소리가 잦아든다.</summary>
        public IEnumerator UpFromFront(System.Action atLit = null)
        {
            Init();
            StartHum(0.18f);
            bool called = false;
            for (int k = 0; k < Count; k++)
            {
                if (level[k] >= 1f) continue;
                Fx(SoundFx.TubeTick, 0.8f); Set(k, 1f);
                yield return new WaitForSeconds(0.05f);
                Set(k, 0f);
                yield return new WaitForSeconds(0.12f);
                Fx(SoundFx.TubeTick, 0.7f); Set(k, 1f);
                if (!called) { called = true; atLit?.Invoke(); }
                yield return new WaitForSeconds(0.1f);
            }
            if (!called) atLit?.Invoke();
            SetAll(1f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (hum != null) hum.volume = Mathf.Lerp(0.18f, 0f, t / 0.6f);
                yield return null;
            }
            StopHum();
        }

        /// <summary>
        /// 흔적이 생길 때의 짧은 고장(2초쯤): 여기저기 지지직 → 가게 전체가 몇 번 깜빡 → 잠깐 완전히 꺼진 사이에
        /// atDark(흔적이 생긴다) → 틱틱 다시 켜지고 웅웅 소리가 잦아든다.
        /// </summary>
        public IEnumerator Glitch(System.Action atDark)
        {
            SetAll(1f);
            StartHum(0.16f);
            int n = Random.Range(4, 7);
            for (int i = 0; i < n; i++)
            {
                int k = Random.Range(0, Count);
                Set(k, Random.Range(0f, 0.4f)); Fx(Random.value < 0.7f ? SoundFx.TubeBuzz : SoundFx.TubeTick, 0.7f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
                Set(k, 1f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
                if (hum != null) hum.volume = Mathf.Min(0.4f, hum.volume + 0.04f);
            }
            for (int i = 0; i < 3; i++)
            {
                SetAll(0f); Fx(SoundFx.TubeBuzz, 0.9f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.08f));
                SetAll(Random.Range(0.6f, 1f));
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
            }
            // 잠깐 완전히 꺼진다(웅웅 소리도 끊긴다) → 그 사이에 흔적이 생긴다
            SetAll(0f); Fx(SoundFx.TubeBuzz, 1f);
            StopHum();
            yield return new WaitForSeconds(0.15f);
            if (GameSettings.ReduceFlicker) yield return UntilDark();
            atDark?.Invoke();
            yield return new WaitForSeconds(0.5f);
            // 틱틱 다시 켜진다
            Fx(SoundFx.TubeTick, 1f); SetAll(1f);
            yield return new WaitForSeconds(0.05f);
            SetAll(0f);
            yield return new WaitForSeconds(0.2f);
            Fx(SoundFx.TubeTick, 1f); SetAll(1f);
            StartHum(0.22f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (hum != null) hum.volume = Mathf.Lerp(0.22f, 0f, t / 1.2f);
                yield return null;
            }
            StopHum();
        }

        /// <summary>
        /// 손님이 괴물로 변할 때(2초쯤): 웅웅 → 계산대 위 앞줄 등이 지지직 → 가게 전체가 미친 듯이 깜빡이고,
        /// 다시 번쩍일 때마다 평소 모습/변한 모습이 번갈아 보인다 → "퍽" 완전히 꺼진다(꺼진 채로 끝난다. Relight로 다시 켠다).
        /// show(가게 밝기 0~1, 변한 모습을 보일지)를 바뀔 때마다 부른다.
        /// </summary>
        public IEnumerator Possess(System.Action<float, bool> show)
        {
            SetAll(1f);
            StartHum(0.18f);
            show(Brightness, false);
            // 1) 계산대 위 앞줄이 버벅인다
            for (int i = 0; i < 4; i++)
            {
                Set(FrontRow, Random.Range(0f, 0.35f)); Fx(SoundFx.TubeBuzz, 0.7f); show(Brightness, false);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
                Set(FrontRow, 1f); show(Brightness, false);
                yield return new WaitForSeconds(Random.Range(0.08f, 0.22f));
                if (hum != null) hum.volume = Mathf.Min(0.45f, hum.volume + 0.06f);
            }
            // 2) 가게 전체가 깜빡: 번쩍일 때마다 평소/변신이 번갈아(점점 빨라진다)
            bool turned = false;
            for (int i = 0; i < 8; i++)
            {
                float speed = Mathf.Lerp(1f, 0.45f, i / 7f);
                SetAll(0f); Fx(SoundFx.TubeBuzz, 0.9f); show(Brightness, turned);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.1f) * speed);
                turned = !turned;
                SetAll(Random.Range(0.55f, 1f)); show(Brightness, turned);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f) * speed);
            }
            // 3) 완전히 꺼진다(웅웅 소리도 끊긴 정적). 이 어둠 속에서 손님이 말하고 나간다
            SetAll(0f); StopHum(); Fx(SoundFx.TubePop, 0.6f); show(Brightness, true);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Possess로 꺼진 가게를 다시 켠다: 앞줄이 틱틱 켜지고, 나머지 등도 하나씩 켜지며 웅웅 소리가 잦아든다.</summary>
        public IEnumerator Relight()
        {
            Fx(SoundFx.TubeTick, 1f); Set(FrontRow, 1f);
            yield return new WaitForSeconds(0.06f);
            Set(FrontRow, 0f);
            yield return new WaitForSeconds(0.28f);
            Fx(SoundFx.TubeTick, 1f); Set(FrontRow, 1f);
            StartHum(0.25f);
            yield return new WaitForSeconds(0.4f);
            for (int k = 0; k < Count; k++)
            {
                if (level[k] >= 1f) continue;
                Set(k, 1f); Fx(SoundFx.TubeTick, 0.6f);
                yield return new WaitForSeconds(0.09f);
            }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (hum != null) hum.volume = Mathf.Lerp(0.25f, 0f, t / 0.6f);
                yield return null;
            }
            StopHum();
        }

        /// <summary>꺼진 등을 틱틱거리며 켠다(두어 번 번쩍했다 꺼진 뒤 켜진다).</summary>
        public IEnumerator StutterOn(int[] ks)
        {
            Fx(SoundFx.TubeTick, 1f);
            Set(ks, 1f);
            yield return new WaitForSeconds(0.06f);
            Set(ks, 0f);
            yield return new WaitForSeconds(0.45f);
            Fx(SoundFx.TubeTick, 1f);
            yield return new WaitForSeconds(0.12f);
            Fx(SoundFx.TubeTick, 0.8f);
            Set(ks, 0.85f);
            yield return new WaitForSeconds(0.1f);
            Set(ks, 0f);
            yield return new WaitForSeconds(0.22f);
            Fx(SoundFx.TubeTick, 1f);
            Set(ks, 1f);
            StartHum(0.25f);
        }
    }
}
