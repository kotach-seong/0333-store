using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 전화 너머 목소리(알아들을 수 없게 웅얼거리는 남자 목소리, 전화기 음질). 대사창이 한 글자씩 찍히는 동안만 말한다.
    ///  - Talk: 지난번에 멈춘 자리부터 이어서 부드럽게 말하기 시작한다(대사가 바뀌어도 한 사람이 계속 말하는 것처럼).
    ///  - Quiet: 뚝 끊지 않고 다음 숨 쉬는 자리(소리가 잦아드는 곳)까지 마저 말하고 줄어든다(최대 0.35초).
    ///  - warp(0~1): 3:33 "점장 전화"처럼 목소리가 점점 늘어지고 낮아지며 가끔 같은 데를 되풀이한다.
    /// 소리: Freesound 530220 "incoherent talking"(beetlemuse)을 전화기 음질로 다듬은 것.
    ///  평소 점장 전화·오프닝·엔딩은 2배속(Audio/phone_voice.wav), 3:33 점장 전화는 원래 빠르기(Audio/phone_voice_slow.wav).
    /// </summary>
    public class PhoneVoice : MonoBehaviour
    {
        public AudioClip clip;
        [Tooltip("3:33 점장 전화용: 원래 빠르기(1배속) 목소리")]
        public AudioClip slowClip;
        [Range(0f, 1f)] public float volume = 0.75f;
        [Tooltip("이보다 작아지는 곳에서 멈춘다(최대 크기 대비)")]
        [Range(0f, 1f)] public float quietLevel = 0.18f;
        [Tooltip("숨 쉬는 자리를 찾는 최대 시간(초)")]
        public float maxTail = 0.35f;

        [HideInInspector] public float warp;

        AudioSource src;
        float[] env;            // 지금 목소리의 10ms마다 소리 크기(0~1)
        float[] envFast, envSlow;
        float resumeAt;         // 다음에 이어 말할 자리(초)
        float resumeFast, resumeSlow;
        bool slow;
        float basePitch = 1f;
        float gain, target;
        bool stopping;
        float stopBy;
        float nextStutter;

        public bool Talking => src != null && src.isPlaying && target > 0f;

        void Awake() => Init();

        void Init()
        {
            if (src != null) return;
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.clip = clip;
            src.volume = 0f;
            envFast = BuildEnvelope(clip);
            envSlow = BuildEnvelope(slowClip);
            env = envFast;
            if (clip != null) resumeAt = resumeFast = Random.Range(0f, clip.length);
            if (slowClip != null) resumeSlow = Random.Range(0f, slowClip.length);
        }

        /// <summary>
        /// 1배속(느린) 목소리로 바꾼다(3:33 점장 전화). 말하는 중이면 바로 바꾸지 않고 다음 Talk부터.
        /// 목소리마다 멈춘 자리를 따로 기억해서, 돌아오면 거기서 이어 말한다.
        /// </summary>
        public void UseSlow(bool on)
        {
            Init();
            if (on && slowClip == null) on = false;
            if (on == slow) return;
            if (src.isPlaying) { src.Stop(); target = gain = 0f; stopping = false; }
            if (slow) resumeSlow = resumeAt; else resumeFast = resumeAt;
            slow = on;
            src.clip = slow ? slowClip : clip;
            env = slow ? envSlow : envFast;
            resumeAt = slow ? resumeSlow : resumeFast;
        }

        static float[] BuildEnvelope(AudioClip c)
        {
            if (c == null || c.loadType != AudioClipLoadType.DecompressOnLoad) return null;
            var data = new float[c.samples * c.channels];
            if (!c.GetData(data, 0)) return null;
            int win = Mathf.Max(1, c.frequency / 100) * c.channels;
            var e = new float[data.Length / win + 1];
            float max = 1e-6f;
            for (int w = 0; w < e.Length; w++)
            {
                float s = 0f; int n = 0;
                for (int i = w * win; i < Mathf.Min(data.Length, (w + 1) * win); i++, n++) s += data[i] * data[i];
                e[w] = n > 0 ? Mathf.Sqrt(s / n) : 0f;
                max = Mathf.Max(max, e[w]);
            }
            for (int w = 0; w < e.Length; w++) e[w] /= max;
            return e;
        }

        float Level(float t)
        {
            if (env == null || env.Length == 0) return 0f;
            int w = Mathf.Clamp(Mathf.FloorToInt(t * 100f), 0, env.Length - 1);
            return env[w];
        }

        /// <summary>말하기 시작(이어서). pitch: 1 = 점장, 조금 높으면 다른 사람.</summary>
        public void Talk(float pitch = 1f)
        {
            if (!Application.isPlaying || clip == null) return;
            Init();
            basePitch = pitch;
            stopping = false;
            target = 1f;
            if (!src.isPlaying)
            {
                src.time = Mathf.Repeat(resumeAt, src.clip.length);
                gain = 0f;
                src.Play();
            }
        }

        /// <summary>그만 말하기: 다음 숨 쉬는 자리까지 마저 말하고 부드럽게 줄어든다.</summary>
        public void Quiet()
        {
            if (src == null || !src.isPlaying || stopping) return;
            stopping = true;
            stopBy = Time.unscaledTime + maxTail;
        }

        /// <summary>바로 줄어들며 멈춘다(대사창이 닫힐 때 등).</summary>
        public void Hush()
        {
            if (src == null || !src.isPlaying) return;
            stopping = true;
            stopBy = Time.unscaledTime;
        }

        void Update()
        {
            if (src == null || !src.isPlaying || PauseMenu.Paused) return;
            if (stopping && (Level(src.time) < quietLevel || Time.unscaledTime >= stopBy)) target = 0f;
            // 부드럽게: 켜질 때 0.05초, 꺼질 때 0.09초
            float speed = target > gain ? 20f : 11f;
            gain = Mathf.MoveTowards(gain, target, Time.unscaledDeltaTime * speed);
            src.volume = volume * gain;
            // 늘어지는 목소리: 낮아지고 느려지며 가끔 같은 자리를 되풀이
            src.pitch = basePitch * Mathf.Lerp(1f, 0.55f, warp) * (1f + (warp > 0f ? Mathf.Sin(Time.unscaledTime * 3.1f) * 0.04f * warp : 0f));
            if (warp > 0.45f && Time.unscaledTime >= nextStutter)
            {
                nextStutter = Time.unscaledTime + Random.Range(0.35f, 0.9f) / warp;
                if (Random.value < warp * 0.6f) src.time = Mathf.Max(0f, src.time - Random.Range(0.08f, 0.2f));
            }
            if (target <= 0f && gain <= 0.001f)
            {
                resumeAt = src.time;   // 다음엔 여기서 이어서
                src.Stop();
                stopping = false;
            }
        }
    }
}
