using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>게임 언어(설정 → 언어). 글자표(Data/Lang/Strings.txt)의 en·zh 줄과 짝.</summary>
    public enum Lang { Ko = 0, En = 1, Zh = 2 }

    /// <summary>
    /// 언어 바꾸기(사용자 결정: 한국어 / English / 简体中文).
    ///  - 코드 속 화면 글자: 한국어 원문을 열쇠로 글자표에서 찾는다(<see cref="T"/>, 형식 문자열은 <see cref="F"/>). 표에 없으면 원문 그대로.
    ///    글자를 화면에 내보내는 곳(점검표·결과 화면·영수증·메뉴·말풍선 등)에서 한 번 거친다.
    ///  - 대사 파일: 언어마다 따로 둔 파일(Dialogue.en.txt, Dialogue.zh.txt …)을 고른다(<see cref="Text"/>). 열쇠(인사:, 점장: 같은 앞부분)는 한국어 그대로.
    ///  - 중국어: 갈무리에 간체 글자가 많이 없어서 모든 글자를 퓨전 픽셀 글꼴(OFL)로 바꾼다(<see cref="ApplyFonts"/>).
    /// 언어가 바뀌면 <see cref="Changed"/>: 메뉴는 다시 그리고, 대사는 다음에 읽을 때 새 언어 파일을 읽는다.
    /// </summary>
    public static class Loc
    {
        public static Lang Current => GameSettings.Language;
        public static event Action Changed;

        static Dictionary<string, string[]> table;
        static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>한국어 원문 → 지금 언어. 표에 없으면 원문 그대로(이미 번역된 대사 파일 글자도 그대로 지나간다).</summary>
        public static string T(string ko)
        {
            if (string.IsNullOrEmpty(ko) || Current == Lang.Ko) return ko;
            Load();
            if (table.TryGetValue(ko, out string[] row))
            {
                string s = row[(int)Current - 1];
                if (!string.IsNullOrEmpty(s)) return s;
            }
            // "1  아까 오셨던 분 맞죠?"처럼 앞에 번호가 붙은 글자: 번호 뒤만 번역
            int lead = 0;
            while (lead < ko.Length && char.IsDigit(ko[lead])) lead++;
            if (lead > 0 && lead < ko.Length && ko[lead] == ' ')
            {
                int body = lead;
                while (body < ko.Length && ko[body] == ' ') body++;
                if (body < ko.Length && table.TryGetValue(ko.Substring(body), out string[] numbered) && !string.IsNullOrEmpty(numbered[(int)Current - 1]))
                    return ko.Substring(0, body) + numbered[(int)Current - 1];
            }
            // "후드 청년 1", "방문자 3"처럼 이름 뒤에 사람 수가 붙은 글자: 이름만 번역
            int sp = ko.LastIndexOf(' ');
            if (sp > 0 && sp < ko.Length - 1 && IsDigits(ko, sp + 1) && table.TryGetValue(ko.Substring(0, sp).TrimEnd(), out string[] named))
            {
                string s = named[(int)Current - 1];
                if (!string.IsNullOrEmpty(s)) return s + ko.Substring(sp);
            }
#if UNITY_EDITOR
            if (HasHangul(ko) && missing.Add(ko)) Debug.LogWarning($"[0333] 번역 없음({Current}): {ko}");
#endif
            return ko;
        }

        /// <summary>형식 문자열(한국어 원문, {0} 자리)을 번역해서 채운다.</summary>
        public static string F(string koFormat, params object[] args)
        {
            try { return string.Format(T(koFormat), args); }
            catch (FormatException) { return string.Format(koFormat, args); }
        }

        /// <summary>쉼표로 이어 붙인 이름들(물건·상품 이름)을 하나씩 번역한다.</summary>
        public static string List(string koNames)
        {
            if (string.IsNullOrEmpty(koNames) || Current == Lang.Ko) return koNames;
            string[] parts = koNames.Split(new[] { ", " }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++) parts[i] = T(parts[i].Trim());
            return string.Join(Current == Lang.Zh ? "、" : ", ", parts);
        }

        /// <summary>대사 파일: 지금 언어 파일(이름.en / 이름.zh)이 있으면 그것, 없으면 한국어 원본.</summary>
        public static string Text(TextAsset ko)
        {
            if (ko == null) return "";
            if (Current == Lang.Ko) return ko.text;
            LocAssets a = LocAssets.Find();
            string want = ko.name + (Current == Lang.En ? ".en" : ".zh");
            if (a != null && a.files != null)
                foreach (TextAsset t in a.files) if (t != null && t.name == want) return t.text;
            return ko.text;
        }

        static bool IsDigits(string s, int from)
        {
            for (int i = from; i < s.Length; i++) if (!char.IsDigit(s[i])) return false;
            return from < s.Length;
        }

        static bool HasHangul(string s)
        {
            foreach (char c in s) if (c >= 0xAC00 && c <= 0xD7A3) return true;
            return false;
        }

        static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, string[]>();
            LocAssets a = LocAssets.Find();
            if (a == null || a.strings == null) return;
            // 묶음 하나: "ko: 원문" / "en: English" / "zh: 中文" (빈 줄로 나눈다, \n은 줄바꿈)
            string[] row = null;
            foreach (string raw in a.strings.text.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("#")) continue;
                if (raw.StartsWith("ko: ")) { row = new string[2]; table[Unescape(raw.Substring(4))] = row; }
                else if (row != null && raw.StartsWith("en: ")) row[0] = Unescape(raw.Substring(4));
                else if (row != null && raw.StartsWith("zh: ")) row[1] = Unescape(raw.Substring(4));
            }
        }

        static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");

        /// <summary>설정에서 언어를 바꿨다: 글꼴을 바꾸고 알린다.</summary>
        public static void Notify()
        {
            ApplyFonts();
            Changed?.Invoke();
        }

        // ── 글꼴 ──
        static readonly Dictionary<Type, FieldInfo[]> fontFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>이 언어에서 쓸 글꼴(중국어면 갈무리 → 퓨전 픽셀, 아니면 그 반대).</summary>
        public static Font Font(Font f)
        {
            LocAssets a = LocAssets.Find();
            if (f == null || a == null) return f;
            bool zh = Current == Lang.Zh;
            if (zh && f == a.galmuri11 && a.zh12 != null) return a.zh12;
            if (zh && f == a.galmuri9 && a.zh10 != null) return a.zh10;
            if (!zh && f == a.zh12 && a.galmuri11 != null) return a.galmuri11;
            if (!zh && f == a.zh10 && a.galmuri9 != null) return a.galmuri9;
            return f;
        }

        /// <summary>씬의 모든 글자(Text)와 우리 컴포넌트의 글꼴 칸을 지금 언어 글꼴로 바꾼다.</summary>
        public static void ApplyFonts()
        {
            if (LocAssets.Find() == null) return;
            foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                Font f = Font(t.font);
                if (f != t.font) t.font = f;
            }
            foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.scene.IsValid()) continue;
                Type type = mb.GetType();
                if (type.Namespace != "Night0333") continue;
                foreach (FieldInfo fi in FontFields(type))
                {
                    if (fi.FieldType == typeof(Font))
                    {
                        var f = (Font)fi.GetValue(mb);
                        Font g = Font(f);
                        if (g != f) fi.SetValue(mb, g);
                    }
                    else if (fi.GetValue(mb) is Font[] arr)
                        for (int i = 0; i < arr.Length; i++) arr[i] = Font(arr[i]);
                }
            }
        }

        static FieldInfo[] FontFields(Type type)
        {
            if (fontFields.TryGetValue(type, out FieldInfo[] list)) return list;
            var found = new List<FieldInfo>();
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (fi.FieldType == typeof(Font) || fi.FieldType == typeof(Font[])) found.Add(fi);
            return fontFields[type] = found.ToArray();
        }

        /// <summary>미리보기(에디터)·언어 파일을 고친 뒤: 글자표를 다시 읽는다.</summary>
        public static void Reload()
        {
            table = null;
            missing.Clear();
        }
    }
}
