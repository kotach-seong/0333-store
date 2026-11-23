using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 근무 시작(00:00, 첫 손님 전) 계산대 위 전화기가 울린다. 받으면(클릭) 점장 대사가 나오고,
    /// 끊으면 그날 알려 준 수칙이 수첩 "야간 근무 수칙"에 적힌다. 받을 때까지 시계는 멈춰 있다.
    ///
    /// 대사 파일(Data/PhoneCalls.txt) 형식:
    ///   # 주석
    ///   [DAY 1]
    ///   점장: 대사 한 줄(대사창 한 칸)      ← "이름: 대사" 꼴이면 그 이름으로 말한다
    ///   수칙: 수첩에 번호를 붙여 적는 줄
    ///   낙서: 수첩에 번호 없이 적는 줄
    /// </summary>
    public class PhoneCall : MonoBehaviour
    {
        [Tooltip("날마다 점장 전화 대사와 수첩에 적힐 수칙(Data/PhoneCalls.txt)")]
        public TextAsset callsFile;
        [Tooltip("대사창(근무 중 짧은 대사와 같은 창)")]
        public ShiftEndView dialog;
        [Tooltip("벨·통화 잡음 전용 소리(받으면 벨을 바로 끊는다)")]
        public AudioSource source;
        [Tooltip("전화기를 조준하면 \"전화 받기\". 울릴 때만 켜진다")]
        public Hotspot hotspot;
        [Tooltip("계산대 그림에서 떼어 낸 전화기(울릴 때 흔들린다)와 그 자리를 메운 덮개. 평소엔 꺼 둔다(구워진 전화기가 보임)")]
        public SpriteRenderer body, patch;
        [Tooltip("떼어 낸 전화기: 평소 / 액정·표시등이 켜진 모습")]
        public Sprite bodyNormal, bodyLit;
        [Tooltip("부재중 표시등에 번지는 빨간 빛(전화기와 같이 흔들린다). lcd는 예전 덧그림(안 씀)")]
        public SpriteRenderer led, lcd;
        [Tooltip("표시등·액정이 화면에서 차지하는 자리(월드 픽셀)")]
        public Rect ledArea, lcdArea;
        public float ringVolume = 0.75f;
        [Tooltip("벨 한 번(따르르릉 따르르릉 + 쉼)의 길이(초)")]
        public float ringPeriod = 3.2f;

        public bool Ringing { get; private set; }
        public bool InCall { get; private set; }
        bool answered;

        class Line { public string speaker, text; }
        class Call
        {
            public int day;
            public readonly List<Line> lines = new List<Line>();
            public readonly List<(string text, bool numbered)> rules = new List<(string, bool)>();
        }
        List<Call> calls;
        Lang callsLang;

        Vector3 bodyHome;
        bool homeSet;

        void Awake()
        {
            if (hotspot != null) hotspot.enabled = false;
            RememberHome();
            EnsureSprites();
            SetLight(false, false);
            SetBody(false);
        }

        void RememberHome()
        {
            if (homeSet || body == null) return;
            bodyHome = body.transform.position;
            homeSet = true;
        }

        /// <summary>떼어 낸 전화기와 덮개를 켜고 끈다(끄면 계산대에 구워진 전화기가 그대로 보인다).</summary>
        void SetBody(bool on)
        {
            RememberHome();
            if (patch != null) patch.enabled = on && body != null;
            if (body != null)
            {
                body.enabled = on;
                body.transform.position = bodyHome;
            }
        }

        /// <summary>벨이 울리는 동안: 전화기가 책상 위에서 달달 떨린다(1픽셀씩 좌우·위로).</summary>
        void Shake(bool burst, float t)
        {
            if (body == null) return;
            Vector3 off = Vector3.zero;
            if (burst)
            {
                int k = Mathf.FloorToInt(t / 0.035f) % 4;
                off = k == 0 ? new Vector3(-1f, 0f, 0f) : k == 1 ? new Vector3(1f, 1f, 0f) : k == 2 ? new Vector3(0f, 1f, 0f) : new Vector3(1f, 0f, 0f);
            }
            body.transform.position = bodyHome + off;
        }

        /// <summary>그날 점장 전화가 있는지(대사 파일에 [DAY n]이 있는지).</summary>
        public bool HasCall(int day) => Find(day) != null;

        /// <summary>
        /// day까지 전화로 알려 준 수칙 전부(수첩에 적을 줄). 번호는 1부터 이어 붙인다.
        /// day가 0이면 빈 목록(첫 전화 전).
        /// </summary>
        public List<string> RulesUpTo(int day)
        {
            Parse();
            var list = new List<string>();
            int n = 0;
            foreach (Call c in calls)
            {
                if (c.day > day) continue;
                foreach (var r in c.rules) list.Add(r.numbered ? $"{++n}. {r.text}" : r.text);
            }
            return list;
        }

        /// <summary>전화가 울리고, 받을 때까지 기다렸다가, 점장 대사를 다 듣고 끊는다.</summary>
        public IEnumerator Ring(int day)
        {
            Call call = Find(day);
            if (call == null) yield break;

            answered = false;
            Ringing = true;
            if (hotspot != null) hotspot.enabled = true;
            SetBody(true);
            float t = 0f, next = 0.6f;
            while (!answered)
            {
                if (t >= next)
                {
                    if (source != null && Application.isPlaying) { source.Stop(); source.PlayOneShot(SoundFx.PhoneRing, ringVolume); }
                    next += ringPeriod;
                }
                // 벨이 울리는 동안 표시등이 같이 깜빡이고, 쉬는 동안엔 느리게
                float ph = Mathf.Repeat(t - 0.6f, ringPeriod);
                bool burst = t >= 0.6f && (ph < 0.42f || (ph > 0.6f && ph < 1.02f));
                bool on = burst ? Mathf.Repeat(t, 0.09f) < 0.06f : Mathf.Repeat(t, 0.8f) < 0.3f;
                SetLight(on, on);
                Shake(burst, t);
                t += Time.deltaTime;
                yield return null;
            }

            // 받았다: 벨을 끊고 달칵, 통화 잡음이 깔린다
            Ringing = false;
            InCall = true;
            if (hotspot != null) hotspot.enabled = false;
            Shake(false, 0f);
            if (source != null && Application.isPlaying)
            {
                source.Stop();
                source.PlayOneShot(SoundFx.PhoneUp, 0.9f);
                source.clip = SoundFx.PhoneLine;
                source.loop = true;
                source.volume = 0.6f;
                source.PlayDelayed(0.15f);
            }
            SetLight(true, true);
            yield return new WaitForSeconds(0.45f);

            // 같은 사람이 이어 말하는 줄은 한 번에(대사창이 깜빡이지 않게)
            int i = 0;
            while (i < call.lines.Count)
            {
                string who = call.lines[i].speaker;
                var chunk = new List<string>();
                while (i < call.lines.Count && call.lines[i].speaker == who) chunk.Add(call.lines[i++].text);
                if (dialog != null) yield return dialog.Say(who, chunk.ToArray());
            }

            // 뚝 — 뚜-
            if (source != null && Application.isPlaying)
            {
                source.Stop();
                source.loop = false;
                source.clip = null;
                source.volume = 1f;
                source.PlayOneShot(SoundFx.PhoneDown, 0.8f);
            }
            SetLight(false, false);
            SetBody(false);
            InCall = false;
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>
        /// 3:33(점장 전화): 정해진 대사로 울린다. giveUp초 동안 안 받으면 그냥 끊긴다(answered=false).
        /// 받으면 대사를 다 듣고 끊는다.
        /// </summary>
        public IEnumerator RingSpecial(string speaker, IList<string> lines, float giveUp, System.Action<bool> answeredResult)
        {
            answered = false;
            Ringing = true;
            if (hotspot != null) hotspot.enabled = true;
            SetBody(true);
            float t = 0f, next = 0.3f;
            while (!answered && t < giveUp)
            {
                if (t >= next)
                {
                    if (source != null && Application.isPlaying) { source.Stop(); source.PlayOneShot(SoundFx.PhoneRing, ringVolume); }
                    next += ringPeriod;
                }
                float ph = Mathf.Repeat(t - 0.3f, ringPeriod);
                bool burst = t >= 0.3f && (ph < 0.42f || (ph > 0.6f && ph < 1.02f));
                bool on = burst ? Mathf.Repeat(t, 0.09f) < 0.06f : Mathf.Repeat(t, 0.8f) < 0.3f;
                SetLight(on, on);
                Shake(burst, t);
                t += Time.deltaTime;
                yield return null;
            }
            Ringing = false;
            if (hotspot != null) hotspot.enabled = false;
            Shake(false, 0f);
            if (!answered)
            {
                if (source != null && Application.isPlaying) source.Stop();
                SetLight(false, false);
                SetBody(false);
                answeredResult?.Invoke(false);
                yield break;
            }
            InCall = true;
            if (source != null && Application.isPlaying)
            {
                source.Stop();
                source.PlayOneShot(SoundFx.PhoneUp, 0.9f);
                source.clip = SoundFx.PhoneLine;
                source.loop = true;
                source.volume = 0.8f;
                source.PlayDelayed(0.15f);
            }
            SetLight(true, true);
            yield return new WaitForSeconds(0.45f);
            if (dialog != null && lines != null && lines.Count > 0)
            {
                var arr = new string[lines.Count];
                lines.CopyTo(arr, 0);
                yield return dialog.Say(speaker, arr);
            }
            if (source != null && Application.isPlaying)
            {
                source.Stop();
                source.loop = false;
                source.clip = null;
                source.volume = 1f;
                source.PlayOneShot(SoundFx.PhoneDown, 0.8f);
            }
            SetLight(false, false);
            SetBody(false);
            InCall = false;
            answeredResult?.Invoke(true);
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>전화기 핫스팟 클릭.</summary>
        public void Answer()
        {
            if (Ringing) answered = true;
        }

        // ── 표시등 ──
        void SetLight(bool ledOn, bool lcdOn)
        {
            // 떼어 낸 전화기가 있으면 그 액정·표시등 자체가 켜진다(예전 납작한 덧그림은 쓰지 않는다)
            bool cut = body != null && body.enabled && bodyLit != null;
            if (cut) body.sprite = lcdOn ? bodyLit : bodyNormal;
            if (led != null) led.enabled = ledOn && (cut || body == null);
            if (lcd != null) lcd.enabled = lcdOn && body == null;
        }

        /// <summary>표시등(빨간 점 + 번진 빛)과 액정(밝은 초록 + 숫자 같은 점) 그림을 만든다.</summary>
        void EnsureSprites()
        {
            if (led != null && led.sprite == null && ledArea.width > 0f)
            {
                int w = Mathf.Max(1, Mathf.RoundToInt(ledArea.width)), h = Mathf.Max(1, Mathf.RoundToInt(ledArea.height));
                const int pad = 2;
                var pt = new PixelTex(w + pad * 2, h + pad * 2);
                for (int y = 0; y < pt.H; y++)
                    for (int x = 0; x < pt.W; x++)
                    {
                        float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - pt.W / 2f) - w / 2f), dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - pt.H / 2f) - h / 2f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= 0f) pt.Set(x, y, new Color32(255, 92, 70, 255));
                        else if (d < pad + 0.5f) pt.Put(x, y,new Color32(230, 40, 30, (byte)Mathf.RoundToInt(130f * (1f - d / (pad + 0.5f)))));
                    }
                led.sprite = pt.ToSprite(Vector2.zero);
                led.transform.position = new Vector3(Mathf.Round(ledArea.x) - pad, Mathf.Round(ledArea.y) - pad, led.transform.position.z);
            }
            if (lcd != null && lcd.sprite == null && lcdArea.width > 0f)
            {
                int w = Mathf.Max(2, Mathf.RoundToInt(lcdArea.width)), h = Mathf.Max(2, Mathf.RoundToInt(lcdArea.height));
                var pt = new PixelTex(w, h);
                var lit = new Color32(150, 214, 150, 255);
                var ink = new Color32(48, 82, 52, 255);
                pt.Fill(0, 0, w, h, lit);
                // 착신 번호처럼 보이는 점들(가운데 줄)
                int mid = h / 2;
                for (int x = 2; x < w - 2; x++)
                    if (x % 3 != 2) { pt.Set(x, mid, ink); if (h > 3 && (x * 7) % 5 < 2) pt.Set(x, mid + 1, ink); }
                lcd.sprite = pt.ToSprite(Vector2.zero);
                lcd.transform.position = new Vector3(Mathf.Round(lcdArea.x), Mathf.Round(lcdArea.y), lcd.transform.position.z);
            }
        }

        // ── 대사 파일 ──
        Call Find(int day)
        {
            Parse();
            foreach (Call c in calls) if (c.day == day) return c;
            return null;
        }

        void Parse()
        {
            if (calls != null && callsLang == Loc.Current) return;   // 언어를 바꾸면 그 언어 파일로 다시 읽는다
            callsLang = Loc.Current;
            calls = new List<Call>();
            if (callsFile == null) return;
            Call cur = null;
            foreach (string raw in Loc.Text(callsFile).Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string head = line.Substring(1, line.Length - 2).Trim().ToUpperInvariant().Replace("DAY", "").Trim();
                    cur = int.TryParse(head, out int d) ? new Call { day = d } : null;
                    if (cur != null) calls.Add(cur);
                    continue;
                }
                if (cur == null) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim(), val = line.Substring(colon + 1).Trim();
                if (val.Length == 0) continue;
                if (key == "수칙") cur.rules.Add((val, true));
                else if (key == "낙서") cur.rules.Add((val, false));
                else cur.lines.Add(new Line { speaker = key, text = val });
            }
        }

        // ── 에디터 미리보기 ──
        /// <summary>미리보기: 울리는 중(표시등·액정 켜짐)인지.</summary>
        public void PreviewRinging(bool on, bool shaken = false)
        {
            EnsureSprites();
            SetBody(on);
            SetLight(on, on);
            if (on && shaken) Shake(true, 0.04f);
        }

        /// <summary>미리보기: day 전화의 n번째 대사(대사창).</summary>
        public void PreviewLine(int day, int n)
        {
            Call c = Find(day);
            if (c == null || dialog == null || c.lines.Count == 0) return;
            Line l = c.lines[Mathf.Clamp(n, 0, c.lines.Count - 1)];
            dialog.PreviewDialogue(l.speaker, l.text);
        }
    }
}
