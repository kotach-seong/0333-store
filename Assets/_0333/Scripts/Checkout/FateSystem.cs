using System;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 판정의 결과(사용자가 정한 규칙). 둘 다 7일 합계로 센다.
    ///  - 흔적: 이상한 손님에게 "예(정상)" → 그것이 가게에 들어온다.
    ///      1: 창문 밖에 눈만 보이는 검은 실루엣(CCTV에는 안 보임)
    ///      2: 가게가 피범벅, 내장이 널림(손님들은 못 느낌)
    ///      3: 그날 06:00 교대 알바생 대신 괴물이 와서 나를 죽인다 → 그날 다시
    ///    목숨 하트 3개(화면 오른쪽 위)가 하나씩 깨진다.
    ///  - 실종: 평범한 손님에게 "아니오(비정상)" → 그 사람이 사라진다.
    ///      다음 근무가 시작될 때(00:00, 첫 손님보다 먼저) 경찰이 온다.
    ///      1: "이 근처에서 사람이 사라졌다" + 실종자 전단
    ///      2: (화내며) "실종자들이 당신과 엮였다… 지켜보겠다" + 전단
    ///      3: "당신 때문이야" → 체포 → 그날 다시
    /// </summary>
    public class FateSystem : MonoBehaviour
    {
        public TraceEffects effects;
        public FlyerBoard flyers;
        public int maxTraces = 3, maxMissing = 3;

        public int Traces { get; private set; }
        public int Missing { get; private set; }
        public int PoliceSeen { get; private set; }
        public int Lives => Mathf.Max(0, maxTraces - Traces);
        /// <summary>남은 "사람" 표시(실종이 하나 생길 때마다 하나씩 회색으로).</summary>
        public int MissingLeft => Mathf.Max(0, maxMissing - Missing);
        public bool DeathDue => Traces >= maxTraces;
        /// <summary>실종된 손님 id(먼저 사라진 순서).</summary>
        public IReadOnlyList<string> MissingIds => missingIds;
        public bool PoliceDue => Missing > PoliceSeen;
        public bool ArrestDue => Missing >= maxMissing;

        readonly List<string> missingIds = new List<string>();
        Func<string, CustomerView> find;

        /// <summary>손님 id로 손님(얼굴)을 찾는 방법(전단에 쓴다).</summary>
        public void SetLookup(Func<string, CustomerView> lookup) => find = lookup;

        public void LoadFrom(SaveData s)
        {
            Traces = s.traces;
            Missing = s.missing;
            PoliceSeen = s.policeSeen;
            missingIds.Clear();
            if (s.missingIds != null) missingIds.AddRange(s.missingIds);
            Refresh();
        }

        public void StoreTo(SaveData s)
        {
            s.traces = Traces;
            s.missing = Missing;
            s.policeSeen = PoliceSeen;
            s.missingIds = new List<string>(missingIds);
        }

        /// <summary>
        /// 판정을 반영한다(하트·사람 표시는 바로 줄어든다). 흔적이 새로 생기면 true —
        /// 눈에 보이는 흔적(창밖 실루엣·피)은 손님이 나간 뒤 형광등이 깜빡일 때 <see cref="ShowTraces"/>로 나타난다.
        /// </summary>
        public bool OnVerdict(ChecklistEntry e)
        {
            if (e == null || e.verdict == Verdict.None) return false;
            if (e.strange && e.verdict == Verdict.Normal)
            {
                Traces++;
                return true;
            }
            if (!e.strange && e.verdict == Verdict.Abnormal)
            {
                Missing++;
                missingIds.Add(e.customerId);
            }
            return false;
        }

        /// <summary>지금까지의 흔적을 눈에 보이게 한다(1: 창밖 실루엣, 2 이상: 피범벅).</summary>
        public void ShowTraces()
        {
            if (effects != null) effects.SetStage(Mathf.Min(Traces, 2));
        }

        /// <summary>경찰이 와서 실종을 알린 뒤: 전단이 붙는다.</summary>
        public void PoliceReported()
        {
            PoliceSeen = Missing;
            ShowFlyers();
        }

        /// <summary>지금 상태대로 눈에 보이는 것들(창밖 실루엣, 피, 전단)을 맞춘다.</summary>
        public void Refresh()
        {
            if (effects != null) effects.SetStage(Mathf.Min(Traces, 2));
            ShowFlyers();
        }

        void ShowFlyers()
        {
            if (flyers == null) return;
            var faces = new List<CustomerView>();
            for (int i = 0; i < PoliceSeen && i < missingIds.Count; i++) faces.Add(find != null ? find(missingIds[i]) : null);
            flyers.Show(faces);
        }

        /// <summary>에디터 미리보기용.</summary>
        public void PreviewSet(int traces, int missing, int policeSeen, IList<string> ids)
        {
            Traces = traces; Missing = missing; PoliceSeen = policeSeen;
            missingIds.Clear();
            missingIds.AddRange(ids);
            Refresh();
        }
    }
}
