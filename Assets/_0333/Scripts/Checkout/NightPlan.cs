using System;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>이상한 손님에게 붙는 이상현상 종류(영수증 7가지 + 이상한 물건 + CCTV).</summary>
    public enum OddKind { Visitor, Total, BodyPayment, Time, Number, FakeItem, Blood, Item, Cctv }

    /// <summary>
    /// CCTV 이상현상(내 눈에는 멀쩡한데 CCTV에서만 이상하다).
    /// Missing: 손님이 CCTV에 안 찍힘 / Ghost: 손님 뒤에 귀신이 붙어 있음 / Monster: 손님이 괴물로 찍힘 /
    /// Watchers: 밖에서 귀신들이 가게 안을 보고 있음.
    /// </summary>
    public enum CctvOdd { None, Missing, Ghost, Monster, Watchers }

    /// <summary>하룻밤 손님 한 명의 계획.</summary>
    [Serializable]
    public class Visit
    {
        [NonSerialized] public CustomerView customer;
        public string customerId;
        /// <summary>이상한 손님인지(정답).</summary>
        public bool strange;
        /// <summary>영수증 이상현상(0~2개).</summary>
        public ReceiptAnomaly receipt;
        /// <summary>물품 칸에 놓일 이상한 물건 개수(0이면 물건은 정상).</summary>
        public int strangeItems;
        /// <summary>CCTV 이상현상(없으면 None).</summary>
        public CctvOdd cctv;
        public int seed;
    }

    /// <summary>
    /// 하룻밤 계획을 세운다(사용자가 정한 규칙).
    ///  - 손님 10명, 하룻밤 안에서는 같은 손님이 두 번 오지 않는다(손님 수가 모자라면 예외).
    ///  - 이상한 손님 수: DAY 1 = 2명, DAY 2 = 2~4명, DAY 3부터 = 3~6명.
    ///  - 10명 중 아무나 이상할 수 있다(첫 손님, 마지막 손님, 03:33 직후 손님도).
    ///  - 이상한 손님은 이상현상이 무조건 1~2개, 평범한 손님은 0개.
    /// 같은 날·같은 seed면 같은 계획(세이브용).
    /// </summary>
    public static class NightPlanner
    {
        public static int StrangeCount(int day, System.Random rnd) => day <= 1 ? 2 : day == 2 ? rnd.Next(2, 5) : rnd.Next(3, 7);

        /// <summary>
        /// 그날까지 열린 이상현상(점장 전화로 하나씩 알려 준다, 사용자가 정한 순서).
        /// DAY 1: 물건, CCTV / 2: 영수증 합계 vs POS / 3: 영수증 시각 / 4: 손님 설명 줄, 영수증 번호 /
        /// 5부터: 없는 상품, 돈 대신 신체, 피 묻은 영수증까지 전부.
        /// </summary>
        public static List<OddKind> Unlocked(int day)
        {
            var list = new List<OddKind> { OddKind.Item, OddKind.Cctv };
            if (day >= 2) list.Add(OddKind.Total);
            if (day >= 3) list.Add(OddKind.Time);
            if (day >= 4) { list.Add(OddKind.Visitor); list.Add(OddKind.Number); }
            if (day >= 5) { list.Add(OddKind.FakeItem); list.Add(OddKind.BodyPayment); list.Add(OddKind.Blood); }
            return list;
        }

        static readonly CctvOdd[] CctvKinds = { CctvOdd.Missing, CctvOdd.Ghost, CctvOdd.Monster, CctvOdd.Watchers };

        public static ReceiptAnomaly ToReceipt(OddKind k)
        {
            switch (k)
            {
                case OddKind.Visitor: return ReceiptAnomaly.Visitor;
                case OddKind.Total: return ReceiptAnomaly.Total;
                case OddKind.BodyPayment: return ReceiptAnomaly.BodyPayment;
                case OddKind.Time: return ReceiptAnomaly.Time;
                case OddKind.Number: return ReceiptAnomaly.Number;
                case OddKind.FakeItem: return ReceiptAnomaly.FakeItem;
                case OddKind.Blood: return ReceiptAnomaly.Blood;
                default: return ReceiptAnomaly.None;
            }
        }

        public static List<Visit> Plan(int day, int seed, IList<CustomerView> pool, int count)
        {
            var rnd = new System.Random(seed * 7919 + day * 104729);
            var visits = new List<Visit>();

            // 손님 순서: 섞어서 앞에서부터(하룻밤 안에서는 겹치지 않게)
            var order = new List<CustomerView>(pool);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (int i = 0; i < count; i++)
            {
                CustomerView c = order.Count > 0 ? order[i % order.Count] : null;
                visits.Add(new Visit { customer = c, customerId = c != null ? c.customerId : "", seed = rnd.Next() });
            }

            // 이상한 손님 자리
            var slots = new List<int>();
            for (int i = 0; i < count; i++) slots.Add(i);
            for (int i = slots.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (slots[i], slots[j]) = (slots[j], slots[i]); }
            int strangeN = Mathf.Min(count, StrangeCount(day, rnd));
            for (int s = 0; s < strangeN; s++)
            {
                Visit v = visits[slots[s]];
                v.strange = true;
                var kinds = Unlocked(day);
                int k = Mathf.Min(kinds.Count, rnd.Next(1, 3));   // 1~2개
                for (int n = 0; n < k; n++)
                {
                    int pick = rnd.Next(kinds.Count);
                    OddKind kind = kinds[pick];
                    kinds.RemoveAt(pick);
                    if (kind == OddKind.Item) v.strangeItems = rnd.Next(1, 3);
                    else if (kind == OddKind.Cctv) v.cctv = CctvKinds[rnd.Next(CctvKinds.Length)];
                    else v.receipt |= ToReceipt(kind);
                }
            }
            return visits;
        }

        /// <summary>결과 화면에 적을 이유(한 줄씩). 평범하면 빈 목록.</summary>
        public static List<string> Reasons(ReceiptAnomaly receipt, string fakeNames, string strangeItemNames, CctvOdd cctv = CctvOdd.None)
        {
            var list = new List<string>();
            switch (cctv)
            {
                case CctvOdd.Missing: list.Add(Loc.T("CCTV에 손님이 안 찍힘")); break;
                case CctvOdd.Ghost: list.Add(Loc.T("CCTV에 손님 뒤 귀신이 찍힘")); break;
                case CctvOdd.Monster: list.Add(Loc.T("CCTV에 괴물로 찍힘")); break;
                case CctvOdd.Watchers: list.Add(Loc.T("CCTV에 밖에서 보는 귀신들")); break;
            }
            if ((receipt & ReceiptAnomaly.Visitor) != 0) list.Add(Loc.T("영수증 손님 설명이 다름"));
            if ((receipt & ReceiptAnomaly.Total) != 0) list.Add(Loc.T("영수증 합계가 계산기와 다름"));
            if ((receipt & ReceiptAnomaly.BodyPayment) != 0) list.Add(Loc.T("돈 대신 신체 부위를 줌"));
            if ((receipt & ReceiptAnomaly.Time) != 0) list.Add(Loc.T("영수증 시각이 다름"));
            if ((receipt & ReceiptAnomaly.Number) != 0) list.Add(Loc.T("영수증 번호가 이상함"));
            if ((receipt & ReceiptAnomaly.FakeItem) != 0) list.Add(string.IsNullOrEmpty(fakeNames) ? Loc.T("영수증에 없는 상품") : Loc.F("없는 상품: {0}", Loc.List(fakeNames)));
            if ((receipt & ReceiptAnomaly.Blood) != 0) list.Add(Loc.T("영수증에 피가 묻음"));
            if (!string.IsNullOrEmpty(strangeItemNames)) list.Add(Loc.F("이상한 물건: {0}", Loc.List(strangeItemNames)));
            return list;
        }
    }
}
