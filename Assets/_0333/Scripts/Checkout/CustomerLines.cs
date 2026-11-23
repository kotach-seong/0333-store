using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 손님 대사 묶음(Data/Dialogue.txt). [손님 id] 아래에 "종류: 대사" 한 줄씩.
    /// 같은 종류가 여러 줄이면 그중 하나를 고른다. 대사 안의 "/"는 줄바꿈.
    /// </summary>
    public class CustomerLines
    {
        public const string Greet = "인사", Choice = "선택", Cctv = "CCTV", Slow1 = "늦음1", Slow2 = "늦음2";

        // 깨진 글자 대신 들어가는 네모·L자·굵은 막대(갈무리 글꼴에 다 있다)
        const string Blocks = "□■▣▤▥▦▧▨▩▮▬▀▄█▌▐▙▛▜▟└┘┌┐┗┛┏┓┃━┣┫┳┻╋";

        /// <summary>
        /// 들킨 손님의 말: 글자 일부(amount 비율)를 네모·L자·굵은 막대로 바꾼다. 띄어쓰기·문장부호는 그대로라 대충 읽힌다.
        /// 말풍선에서는 여기에 위아래로 꿈틀거리는 획(잘고)이 더해진다.
        /// </summary>
        public static string Corrupt(string s, System.Random rnd, float amount = 0.4f)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            int lastLetter = -1;
            for (int i = 0; i < s.Length; i++) if (char.IsLetterOrDigit(s[i])) lastLetter = i;
            bool changed = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool swap = char.IsLetterOrDigit(c) && (rnd.NextDouble() < amount || (!changed && i == lastLetter));
                if (!swap) { sb.Append(c); continue; }
                changed = true;
                sb.Append(Blocks[rnd.Next(Blocks.Length)]);
            }
            return sb.ToString();
        }

        /// <summary>나가면서 하는 말의 종류: 손님이 실제로 정상인지 × 내가 고른 판정.</summary>
        public static string Farewell(bool strange, Verdict verdict) =>
            (strange ? "비정상" : "정상") + "→" + (verdict == Verdict.Abnormal ? "비정상" : "정상");

        /// <summary>인사 한마디와, 거기에 내가 고를 대답(reply) → 손님 답변(answer).</summary>
        public class Greeting
        {
            public string text;
            public readonly List<(string reply, string answer)> choices = new List<(string reply, string answer)>();
        }

        readonly Dictionary<string, Dictionary<string, List<string>>> byCustomer =
            new Dictionary<string, Dictionary<string, List<string>>>();
        readonly Dictionary<string, List<Greeting>> greetings = new Dictionary<string, List<Greeting>>();

        public static CustomerLines Parse(string text)
        {
            var lines = new CustomerLines();
            if (string.IsNullOrEmpty(text)) return lines;
            Dictionary<string, List<string>> current = null;
            string currentId = null;
            Greeting lastGreeting = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("["))
                {
                    int close = line.IndexOf(']');
                    if (close < 0) continue;
                    currentId = line.Substring(1, close - 1).Trim();
                    lastGreeting = null;
                    if (!lines.byCustomer.TryGetValue(currentId, out current))
                    {
                        current = new Dictionary<string, List<string>>();
                        lines.byCustomer[currentId] = current;
                    }
                    continue;
                }
                int colon = line.IndexOf(':');
                if (current == null || colon <= 0) continue;
                string kind = line.Substring(0, colon).Trim();
                string say = line.Substring(colon + 1).Trim().Replace(" / ", "\n").Replace("/", "\n");
                if (say.Length == 0) continue;
                if (kind == Choice)
                {
                    // 선택: 내 대답 > 손님 답변 (바로 위 인사에 붙는다)
                    if (lastGreeting == null) continue;
                    int arrow = say.IndexOf('>');
                    string reply = (arrow >= 0 ? say.Substring(0, arrow) : say).Trim();
                    string answer = arrow >= 0 ? say.Substring(arrow + 1).Trim() : "";
                    if (reply.Length > 0) lastGreeting.choices.Add((reply, answer));
                    continue;
                }
                if (kind == Greet)
                {
                    lastGreeting = new Greeting { text = say };
                    if (!lines.greetings.TryGetValue(currentId, out List<Greeting> gl)) lines.greetings[currentId] = gl = new List<Greeting>();
                    gl.Add(lastGreeting);
                }
                if (!current.TryGetValue(kind, out List<string> list)) current[kind] = list = new List<string>();
                list.Add(say);
            }
            return lines;
        }

        /// <summary>그 손님의 인사 하나(선택지 포함). 없으면 null.</summary>
        public Greeting PickGreeting(string customerId, System.Random rnd)
        {
            if (customerId == null || !greetings.TryGetValue(customerId, out List<Greeting> gl) || gl.Count == 0) return null;
            return gl[rnd.Next(gl.Count)];
        }

        public bool Has(string customerId, string kind) =>
            customerId != null && byCustomer.TryGetValue(customerId, out var d) && d.TryGetValue(kind, out var l) && l.Count > 0;

        /// <summary>그 손님의 그 종류 대사 중 하나(없으면 null). except와 다른 것을 고르려 한다.</summary>
        public string Pick(string customerId, string kind, System.Random rnd, string except = null)
        {
            if (!Has(customerId, kind)) return null;
            List<string> l = byCustomer[customerId][kind];
            if (l.Count == 1) return l[0];
            for (int tries = 0; tries < 4; tries++)
            {
                string s = l[rnd.Next(l.Count)];
                if (s != except) return s;
            }
            return l[0];
        }
    }
}
