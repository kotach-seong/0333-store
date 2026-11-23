using System;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 계산할 때 나는 작은 효과음들을 코드로 만든다(파일 없이). 나중에 진짜 녹음 소리가 생기면 바꿔 끼우면 된다.
    /// </summary>
    public static class SoundFx
    {
        const int Rate = 44100;

        static AudioClip beep, print, thunk, pickUp, bill, coin, tear, tick, squish;

        /// <summary>바코드 스캐너 "삑".</summary>
        public static AudioClip Beep => Get(ref beep, "fx_beep", 0.12f, (t, r) =>
        {
            float env = Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((0.12f - t) / 0.02f);
            float s = Mathf.Sin(2f * Mathf.PI * 2350f * t) * 0.7f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 2350f * t)) * 0.12f;
            return s * env * 0.55f;
        });

        /// <summary>영수증 프린터가 종이를 뽑는 "지이익" (끊어 가며 밀어낸다).</summary>
        public static AudioClip Print => Get(ref print, "fx_print", 1.0f, (t, r) =>
        {
            float chunk = Mathf.Repeat(t, 0.055f) < 0.042f ? 1f : 0.25f;
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((1.0f - t) / 0.06f);
            float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t)) * 0.35f + Mathf.Sin(2f * Mathf.PI * 760f * t) * 0.25f;
            float noise = (float)(r.NextDouble() * 2.0 - 1.0) * 0.5f;
            return (buzz + noise) * chunk * env * 0.22f;
        });

        /// <summary>물건을 물품 칸에 내려놓는 "툭".</summary>
        public static AudioClip Thunk => Get(ref thunk, "fx_thunk", 0.09f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 55f);
            float body = Mathf.Sin(2f * Mathf.PI * (150f - 500f * t) * t);
            float noise = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 160f);
            return (body * 0.8f + noise * 0.35f) * env * 0.6f;
        });

        /// <summary>물건을 집어 드는 가벼운 "스윽".</summary>
        public static AudioClip PickUp => Get(ref pickUp, "fx_pickup", 0.07f, (t, r) =>
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.07f));
            return (float)(r.NextDouble() * 2.0 - 1.0) * env * 0.12f;
        });

        /// <summary>지폐를 내려놓는 "사락".</summary>
        public static AudioClip Bill => Get(ref bill, "fx_bill", 0.14f, (t, r) =>
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.14f)) * (0.6f + 0.4f * Mathf.Sin(t * 180f));
            return (float)(r.NextDouble() * 2.0 - 1.0) * env * 0.22f;
        });

        /// <summary>동전이 떨어지는 "짤랑".</summary>
        public static AudioClip Coin => Get(ref coin, "fx_coin", 0.22f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 22f);
            float s = Mathf.Sin(2f * Mathf.PI * 3150f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 4720f * t) * 0.3f
                      + Mathf.Sin(2f * Mathf.PI * 6080f * t) * 0.2f;
            float click = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 400f);
            return (s * env + click * 0.4f) * 0.3f;
        });

        /// <summary>축축한 것이 떨어지는 "철퍽"(돈 대신 신체 부위를 놓을 때).</summary>
        public static AudioClip Squish => Get(ref squish, "fx_squish", 0.22f, (t, r) =>
        {
            float env = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-t * 16f);
            float body = Mathf.Sin(2f * Mathf.PI * (95f - 120f * t) * t);
            float wet = (float)(r.NextDouble() * 2.0 - 1.0) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 33f * t));
            return (body * 0.7f + wet * 0.45f) * env * 0.6f;
        });

        /// <summary>영수증을 뜯는 "찍".</summary>
        public static AudioClip Tear => Get(ref tear, "fx_tear", 0.16f, (t, r) =>
        {
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 18f);
            float crackle = r.NextDouble() < 0.35 ? (float)(r.NextDouble() * 2.0 - 1.0) : 0f;
            return crackle * env * 0.35f;
        });

        /// <summary>펜으로 체크하는 "슥".</summary>
        public static AudioClip Tick => Get(ref tick, "fx_tick", 0.08f, (t, r) =>
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.08f));
            float scratch = (float)(r.NextDouble() * 2.0 - 1.0) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 90f * t));
            return scratch * env * 0.18f;
        });

        static AudioClip blip;

        /// <summary>말풍선 글자가 찍힐 때의 작은 "똑"(0.03초).</summary>
        public static AudioClip Blip => Get(ref blip, "fx_blip", 0.03f, (t, r) =>
        {
            float env = Mathf.Clamp01(t / 0.002f) * Mathf.Exp(-t * 140f);
            return Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.35f;
        });

        static AudioClip rain;

        /// <summary>창밖 빗소리(2초, 반복): 낮게 깔리는 쏴― 소리 + 가끔 툭 떨어지는 빗방울.</summary>
        public static AudioClip Rain
        {
            get
            {
                if (rain != null) return rain;
                const float seconds = 2f;
                int n = Mathf.CeilToInt(seconds * Rate);
                var data = new float[n];
                var rnd = new System.Random(3333);
                float brown = 0f, hiss = 0f;
                for (int i = 0; i < n; i++)
                {
                    float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    brown = brown * 0.985f + white * 0.06f;            // 낮은 쏴―
                    hiss = hiss * 0.6f + white * 0.4f;                  // 높은 사―
                    data[i] = brown * 0.55f + hiss * 0.12f;
                }
                for (int k = 0; k < 70; k++)                          // 빗방울
                {
                    int at = rnd.Next(n - 900);
                    float amp = 0.08f + (float)rnd.NextDouble() * 0.18f;
                    for (int j = 0; j < 900; j++) data[at + j] += (float)(rnd.NextDouble() * 2.0 - 1.0) * amp * Mathf.Exp(-j / 120f);
                }
                // 이음새가 튀지 않게 끝을 처음으로 섞는다
                int blend = Rate / 20;
                for (int j = 0; j < blend; j++) { float t = j / (float)blend; data[n - blend + j] = Mathf.Lerp(data[n - blend + j], data[j], t); }
                for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i] * 0.8f, -1f, 1f);
                rain = AudioClip.Create("fx_rain", n, 1, Rate, false);
                rain.SetData(data, 0);
                rain.hideFlags = HideFlags.DontSave;
                return rain;
            }
        }

        // ── 형광등 ──
        static AudioClip tubeHum, tubeBuzz, tubeTick, tubePop;

        /// <summary>형광등 안정기의 "웅―"(1초, 끊김 없이 반복). 120Hz와 배음, 살짝 찌그러진 소리.</summary>
        public static AudioClip TubeHum => Get(ref tubeHum, "fx_tube_hum", 1f, (t, r) =>
        {
            float a = Mathf.Sin(2f * Mathf.PI * 120f * t);
            float s = a * 0.55f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.12f
                      + Mathf.Sin(2f * Mathf.PI * 1080f * t) * 0.04f;
            s = Mathf.Clamp(s * 1.6f, -1f, 1f) * 0.7f;                          // 찌그러진 웅웅거림
            float hiss = (float)(r.NextDouble() * 2.0 - 1.0) * 0.05f;
            return (s + hiss) * 0.35f;
        });

        /// <summary>깜빡일 때 "지지직"(0.22초). 120Hz로 떨리는 잡음 + 튀는 소리.</summary>
        public static AudioClip TubeBuzz => Get(ref tubeBuzz, "fx_tube_buzz", 0.22f, (t, r) =>
        {
            float env = Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((0.22f - t) / 0.05f);
            float am = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 60f * t));
            float noise = (float)(r.NextDouble() * 2.0 - 1.0);
            float crackle = r.NextDouble() < 0.02 ? (float)(r.NextDouble() * 2.0 - 1.0) * 2.2f : 0f;
            float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 120f * t)) * 0.3f;
            return (noise * am * 0.45f + crackle + tone * am) * env * 0.3f;
        });

        /// <summary>점등관의 "틱"(0.06초).</summary>
        public static AudioClip TubeTick => Get(ref tubeTick, "fx_tube_tick", 0.06f, (t, r) =>
        {
            float click = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 700f);
            float ring = Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 160f);
            return (click * 0.8f + ring * 0.35f) * 0.5f;
        });

        /// <summary>형광등이 나가는 "퍽"(0.4초): 낮게 꺼지는 소리 + 유리가 튀는 소리.</summary>
        public static AudioClip TubePop => Get(ref tubePop, "fx_tube_pop", 0.4f, (t, r) =>
        {
            float thump = Mathf.Sin(2f * Mathf.PI * (85f - 60f * t) * t) * Mathf.Exp(-t * 14f);
            float burst = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 55f);
            float glass = Mathf.Sin(2f * Mathf.PI * 5200f * t) * Mathf.Exp(-t * 90f) * 0.25f;
            return (thump * 0.8f + burst * 0.6f + glass) * 0.55f;
        });

        // ── 전화기 ──
        static AudioClip phoneRing, phoneUp, phoneDown, phoneLine;

        /// <summary>
        /// 낡은 유선 전화 벨 한 번(2.4초): "따르르릉 따르르릉" 두 번 울리고 쉰다.
        /// 두 음(1.3k·1.6k)을 빠르게 번갈아 떨게 해서 옛날 벨처럼 거칠게.
        /// </summary>
        public static AudioClip PhoneRing => Get(ref phoneRing, "fx_phone_ring", 2.4f, (t, r) =>
        {
            float burst = t < 0.42f ? 1f : t > 0.6f && t < 1.02f ? 1f : 0f;
            if (burst == 0f) return 0f;
            float local = t < 0.6f ? t : t - 0.6f;
            float env = Mathf.Clamp01(local / 0.01f) * Mathf.Clamp01((0.42f - local) / 0.03f);
            float f = Mathf.Repeat(t, 1f / 22f) < 0.5f / 22f ? 1320f : 1620f;   // 22번/초로 떨린다
            float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.18f;
            s += Mathf.Sin(2f * Mathf.PI * 2.03f * f * t) * 0.12f;
            return s * env * 0.42f;
        });

        /// <summary>수화기를 드는 "달칵".</summary>
        public static AudioClip PhoneUp => Get(ref phoneUp, "fx_phone_up", 0.12f, (t, r) =>
        {
            float click = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 120f);
            float knock = Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-t * 60f);
            float second = t > 0.05f ? (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-(t - 0.05f) * 160f) * 0.6f : 0f;
            return (click * 0.6f + knock * 0.5f + second) * 0.6f;
        });

        /// <summary>상대가 끊는 "뚝" 다음 신호음 "뚜-".</summary>
        public static AudioClip PhoneDown => Get(ref phoneDown, "fx_phone_down", 1.1f, (t, r) =>
        {
            float click = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 90f) * 0.7f;
            float tone = t > 0.35f ? Mathf.Sin(2f * Mathf.PI * 425f * t) * Mathf.Clamp01((t - 0.35f) / 0.01f) * Mathf.Clamp01((1.1f - t) / 0.05f) * 0.22f : 0f;
            return click + tone;
        });

        /// <summary>통화 중 깔리는 낮은 잡음(1초, 반복해서 쓴다).</summary>
        public static AudioClip PhoneLine => Get(ref phoneLine, "fx_phone_line", 1f, (t, r) =>
        {
            float hiss = (float)(r.NextDouble() * 2.0 - 1.0) * 0.05f;
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.03f;
            float crackle = r.NextDouble() < 0.0006 ? (float)(r.NextDouble() * 2.0 - 1.0) * 0.5f : 0f;
            return hiss + hum + crackle;
        });

        // ── 오프닝·엔딩(휴대폰) ──
        static AudioClip ringBack, mobileRing, step, stepIn, flap;

        /// <summary>젖은 보도 위 발소리: 낮게 "툭" + 물 튀는 "찰".</summary>
        public static AudioClip Step => Get(ref step, "fx_step", 0.18f, (t, r) =>
        {
            float thud = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 45f) * 0.5f;
            float splash = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-Mathf.Abs(t - 0.02f) * 60f) * 0.35f;
            return thud + splash;
        });

        /// <summary>가게 바닥 타일 위 발소리: 짧고 딱딱한 "톡".</summary>
        public static AudioClip StepIn => Get(ref stepIn, "fx_step_in", 0.1f, (t, r) =>
        {
            float click = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 140f) * 0.4f;
            float body = Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 70f) * 0.35f;
            return click + body;
        });

        /// <summary>우산을 접으며 털어 내는 "푸드득".</summary>
        public static AudioClip Flap => Get(ref flap, "fx_flap", 0.45f, (t, r) =>
        {
            float a = 0f;
            foreach (float at in new[] { 0f, 0.13f, 0.26f })
            {
                float l = t - at;
                if (l >= 0f) a += Mathf.Exp(-l * 30f);
            }
            return (float)(r.NextDouble() * 2.0 - 1.0) * a * 0.35f;
        });

        /// <summary>전화를 걸었을 때 들리는 연결음 "뚜르르르―"(3초: 1초 울리고 2초 쉰다, 반복해서 쓴다).</summary>
        public static AudioClip RingBack => Get(ref ringBack, "fx_ring_back", 3f, (t, r) =>
        {
            if (t >= 1f) return 0f;
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((1f - t) / 0.03f);
            float s = Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t);   // 두 음이 맞부딪혀 떨린다
            return s * env * 0.16f;
        });

        /// <summary>휴대폰 벨소리(2초): 맑은 세 음 "띠리링" 두 번 → 쉼. 반복해서 쓴다.</summary>
        public static AudioClip MobileRing => Get(ref mobileRing, "fx_mobile_ring", 2f, (t, r) =>
        {
            float[] notes = { 1318.5f, 1568f, 2093f };   // 미·솔·도
            for (int rep = 0; rep < 2; rep++)
                for (int n = 0; n < 3; n++)
                {
                    float start = rep * 0.5f + n * 0.13f, local = t - start;
                    if (local < 0f || local > 0.12f) continue;
                    float env = Mathf.Clamp01(local / 0.004f) * Mathf.Exp(-local * 22f);
                    float f = notes[n];
                    return (Mathf.Sin(2f * Mathf.PI * f * local) + 0.3f * Mathf.Sin(2f * Mathf.PI * 2f * f * local)) * env * 0.3f;
                }
            return 0f;
        });

        // ── 죽음: 다가오는 괴물 ──
        static AudioClip heavyStep, breath, roar, thud;

        /// <summary>무겁고 젖은 맨발 걸음 "쿵…철벅".</summary>
        public static AudioClip HeavyStep => Get(ref heavyStep, "fx_heavy_step", 0.35f, (t, r) =>
        {
            float thud = Mathf.Sin(2f * Mathf.PI * (58f - 20f * t) * t) * Mathf.Exp(-t * 18f) * 0.8f;
            float wet = t > 0.03f ? (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-(t - 0.03f) * 22f) * 0.35f : 0f;
            return thud + wet * (0.6f + 0.4f * Mathf.Sin(t * 900f));
        });

        /// <summary>어둠 속 거칠고 젖은 숨(1.6초: 들이쉬고 내쉰다).</summary>
        public static AudioClip Breath => Get(ref breath, "fx_breath", 1.6f, (t, r) =>
        {
            float inhale = t < 0.7f ? Mathf.Sin(t / 0.7f * Mathf.PI) : 0f;
            float exhale = t > 0.8f ? Mathf.Sin((t - 0.8f) / 0.8f * Mathf.PI) : 0f;
            float env = inhale * 0.55f + exhale * 0.9f;
            float noise = (float)(r.NextDouble() * 2.0 - 1.0);
            float rasp = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 34f * t);   // 목에서 그르렁
            return noise * env * rasp * 0.32f;
        });

        /// <summary>덮칠 때의 포효: 낮게 떨리는 울음이 찢어지며 높아진다(1.2초).</summary>
        public static AudioClip Roar => Get(ref roar, "fx_roar", 1.2f, (t, r) =>
        {
            float f = Mathf.Lerp(70f, 140f, Mathf.Clamp01(t / 0.5f)) * (1f + 0.06f * Mathf.Sin(t * 60f));
            float saw = Mathf.Repeat(f * t, 1f) * 2f - 1f;
            float grit = (float)(r.NextDouble() * 2.0 - 1.0) * 0.6f;
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1.2f - t) / 0.4f);
            float v = (saw * 0.7f + grit * (0.4f + 0.3f * Mathf.Sin(t * 37f))) * env;
            return Mathf.Clamp(v * 1.6f, -1f, 1f) * 0.55f;
        });

        /// <summary>뒤로 넘어져 바닥에 부딪히는 "쿵".</summary>
        public static AudioClip Thud => Get(ref thud, "fx_thud", 0.5f, (t, r) =>
        {
            float body = Mathf.Sin(2f * Mathf.PI * (48f - 18f * t) * t) * Mathf.Exp(-t * 9f);
            float hit = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 60f) * 0.6f;
            return (body + hit) * 0.7f;
        });

        // ── 3:33 이벤트 ──
        static AudioClip knock, laugh, heartbeat, tinnitus, whisper, staticNoise, voice, scream;

        /// <summary>유리문을 두드리는 "쿵 쿵"(0.5초, 두 번).</summary>
        public static AudioClip Knock => Get(ref knock, "fx_knock", 0.5f, (t, r) =>
        {
            float s = 0f;
            foreach (float at in new[] { 0f, 0.22f })
            {
                float l = t - at;
                if (l < 0f) continue;
                float env = Mathf.Clamp01(l / 0.002f) * Mathf.Exp(-l * 38f);
                s += (Mathf.Sin(2f * Mathf.PI * (110f - 90f * l) * l) * 0.8f + Mathf.Sin(2f * Mathf.PI * 340f * l) * 0.25f
                      + (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-l * 260f) * 0.6f) * env;
            }
            return s * 0.7f;
        });

        /// <summary>
        /// 낮게 킬킬대는 웃음(1.1초): "흐흐흐흐" — 성대 떨림(톱니파)을 입 모양 울림(포먼트)으로 거른 소리를 짧게 끊어 낸다.
        /// 끝으로 갈수록 음이 내려가고 숨소리가 섞인다.
        /// </summary>
        public static AudioClip Laugh
        {
            get
            {
                if (laugh != null) return laugh;
                laugh = Buffer("fx_laugh", 1.1f, (data, rnd) =>
                {
                    var f1 = new Reso(650f, 120f); var f2 = new Reso(1100f, 160f); var f3 = new Reso(2600f, 260f);
                    float phase = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float syl = Mathf.Repeat(t, 0.17f);
                        float env = syl < 0.11f ? Mathf.Sin(Mathf.PI * syl / 0.11f) : 0f;
                        env *= Mathf.Clamp01((1.1f - t) / 0.2f);
                        float pitch = Mathf.Lerp(230f, 150f, t / 1.1f) * (1f + 0.04f * Mathf.Sin(t * 37f));
                        phase += pitch / Rate;
                        float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                        float breath = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Lerp(0.25f, 0.7f, t / 1.1f);
                        float x = saw * 0.6f + breath * 0.4f;
                        float y = f1.Run(x) * 1.0f + f2.Run(x) * 0.7f + f3.Run(x) * 0.25f;
                        data[i] = y * env * 0.9f;
                    }
                });
                return laugh;
            }
        }

        /// <summary>심장 소리 "쿵쿵"(0.85초, 반복해서 쓴다).</summary>
        public static AudioClip Heartbeat => Get(ref heartbeat, "fx_heart", 0.85f, (t, r) =>
        {
            float s = 0f;
            foreach (var (at, amp) in new[] { (0f, 1f), (0.24f, 0.7f) })
            {
                float l = t - at;
                if (l < 0f) continue;
                s += Mathf.Sin(2f * Mathf.PI * (62f - 30f * l) * l) * Mathf.Clamp01(l / 0.008f) * Mathf.Exp(-l * 22f) * amp;
            }
            return s * 0.85f;
        });

        /// <summary>귀가 먹먹한 이명 "삐―"(2초, 반복). 높은 음 두 개가 살짝 어긋나 맥놀이가 생긴다.</summary>
        public static AudioClip Tinnitus => Get(ref tinnitus, "fx_tinnitus", 2f, (t, r) =>
            (Mathf.Sin(2f * Mathf.PI * 6200f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 6203.5f * t) * 0.4f) * 0.16f);

        /// <summary>알아들을 수 없는 속삭임(3초, 반복): 거른 숨소리를 음절처럼 끊고 "스" "ㅎ" 같은 높은 바람 소리를 섞는다.</summary>
        public static AudioClip Whisper
        {
            get
            {
                if (whisper != null) return whisper;
                whisper = Buffer("fx_whisper", 3f, (data, rnd) =>
                {
                    var f1 = new Reso(900f, 400f); var f2 = new Reso(2400f, 700f); var hiss = new Reso(5200f, 1500f);
                    float env = 0f, target = 0f, next = 0f, sib = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float t = i / (float)Rate;
                        if (t >= next)
                        {
                            target = rnd.NextDouble() < 0.75 ? 0.35f + (float)rnd.NextDouble() * 0.65f : 0f;
                            sib = rnd.NextDouble() < 0.3 ? 1f : 0f;
                            next = t + 0.06f + (float)rnd.NextDouble() * 0.16f;
                        }
                        env += (target - env) * 0.0015f;
                        float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                        float y = f1.Run(n) * 0.9f + f2.Run(n) * 0.6f + hiss.Run(n) * sib * 0.9f;
                        data[i] = y * env * 0.5f;
                    }
                    Loop(data);
                });
                return whisper;
            }
        }

        /// <summary>CCTV 잡음 "치이익"(1초, 반복): 거친 백색 잡음 + 60Hz 떨림 + 튀는 소리.</summary>
        public static AudioClip Static => Get(ref staticNoise, "fx_static", 1f, (t, r) =>
        {
            float n = (float)(r.NextDouble() * 2.0 - 1.0);
            float am = 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 60f * t));
            float pop = r.NextDouble() < 0.004 ? (float)(r.NextDouble() * 2.0 - 1.0) * 2f : 0f;
            return (n * am * 0.35f + pop) * 0.4f;
        });

        /// <summary>잡음 사이로 들리는 낮고 늘어진 목소리 같은 소리(2초): 아주 낮은 톱니파를 "오―" 하는 울림으로 거르고 흔든다.</summary>
        public static AudioClip Voice
        {
            get
            {
                if (voice != null) return voice;
                voice = Buffer("fx_voice", 2f, (data, rnd) =>
                {
                    var f1 = new Reso(420f, 90f); var f2 = new Reso(800f, 120f);
                    float phase = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float pitch = 58f + 14f * Mathf.Sin(t * 2.3f) + 6f * Mathf.Sin(t * 11f);
                        phase += pitch / Rate;
                        float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                        float x = saw + (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.15f;
                        float y = f1.Run(x) + f2.Run(x) * 0.6f;
                        float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((2f - t) / 0.4f) * (0.7f + 0.3f * Mathf.Sin(t * 5.1f));
                        data[i] = Mathf.Clamp(y * 3f, -1f, 1f) * env * 0.5f;
                    }
                });
                return voice;
            }
        }

        /// <summary>아파서 지르는 날카로운 비명(0.8초): 높은 톱니파에 떨림, 끝이 갈라진다.</summary>
        public static AudioClip Scream
        {
            get
            {
                if (scream != null) return scream;
                scream = Buffer("fx_scream", 0.8f, (data, rnd) =>
                {
                    var f1 = new Reso(950f, 200f); var f2 = new Reso(2900f, 400f);
                    float phase = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float pitch = Mathf.Lerp(620f, 430f, t / 0.8f) * (1f + 0.05f * Mathf.Sin(t * 48f));
                        phase += pitch / Rate;
                        float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                        float x = saw * 0.7f + (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Lerp(0.15f, 0.6f, t / 0.8f);
                        float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((0.8f - t) / 0.25f);
                        data[i] = Mathf.Clamp((f1.Run(x) + f2.Run(x) * 0.8f) * 2f, -1f, 1f) * env * 0.45f;
                    }
                });
                return scream;
            }
        }

        /// <summary>2차 공진 필터(입 모양 울림). 톱니파·잡음을 목소리처럼 거를 때 쓴다.</summary>
        struct Reso
        {
            readonly float a0, b1, b2;
            float y1, y2;
            public Reso(float freq, float bandwidth)
            {
                float rr = Mathf.Exp(-Mathf.PI * bandwidth / Rate);
                b1 = -2f * rr * Mathf.Cos(2f * Mathf.PI * freq / Rate);
                b2 = rr * rr;
                a0 = 1f - rr;
                y1 = y2 = 0f;
            }
            public float Run(float x)
            {
                float y = a0 * x - b1 * y1 - b2 * y2;
                y2 = y1; y1 = y;
                return y;
            }
        }

        /// <summary>반복해서 틀 소리: 끝을 처음으로 섞어 이음새가 튀지 않게.</summary>
        static void Loop(float[] data)
        {
            int blend = Rate / 20;
            for (int j = 0; j < blend; j++) { float t = j / (float)blend; data[data.Length - blend + j] = Mathf.Lerp(data[data.Length - blend + j], data[j], t); }
        }

        static AudioClip Buffer(string name, float seconds, Action<float[], System.Random> fill)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            fill(data, new System.Random(name.GetHashCode()));
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        public static void Play(AudioSource source, AudioClip clip, float volume = 1f)
        {
            if (source != null && clip != null && Application.isPlaying) source.PlayOneShot(clip, volume);
        }

        // 유니티 오브젝트는 ??= 대신 이렇게 확인해야 지워진 소리를 다시 만든다
        static AudioClip Get(ref AudioClip cache, string name, float seconds, Func<float, System.Random, float> wave)
        {
            if (cache == null) cache = Make(name, seconds, wave);
            return cache;
        }

        static AudioClip Make(string name, float seconds, Func<float, System.Random, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate, rnd), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }
    }
}
