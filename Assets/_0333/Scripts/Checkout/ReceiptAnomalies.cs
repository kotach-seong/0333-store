using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Night0333
{
    /// <summary>
    /// 영수증 이상현상을 고르고 영수증 내용에 적용한다(사용자가 정한 7가지).
    ///  1) 손님 설명 이상: "?????", 글씨 깨짐, 다른 사람(후드 청년 → 민폐 아줌마), 사람 수가 다름(후드 청년 2)
    ///  2) 합계 다름: 금전등록기 LED와 영수증 합계가 다르다
    ///  3) 돈 대신 신체 부위: 돈바구니는 MoneyTray가 처리, 영수증 "받은 돈"은 글자 깨짐
    ///  4) 시각 다름: 03:33, 엉뚱한 시각, 있을 수 없는 시각, 깨진 숫자
    ///  5) 번호 이상: 앞 번호와 중복, 건너뜀, 글자 깨짐
    ///  6) 없는 상품: 신체 부위·피·쓰레기·"피생수" 같은 줄이 한 줄~여러 줄 끼어든다
    ///  7) 피 묻은 영수증
    /// 빼기로 한 것: 찍지 않은 물건 추가, 수량 다름(물건을 하나하나 기억해야 해서).
    /// </summary>
    public static class ReceiptAnomalies
    {
        public static readonly ReceiptAnomaly[] All =
        {
            ReceiptAnomaly.Visitor, ReceiptAnomaly.Total, ReceiptAnomaly.BodyPayment, ReceiptAnomaly.Time,
            ReceiptAnomaly.Number, ReceiptAnomaly.FakeItem, ReceiptAnomaly.Blood,
        };

        /// <summary>엉뚱하게 바뀌어 찍히는 다른 손님 설명(손님 시안에서).</summary>
        static readonly string[] OtherPeople =
        {
            "민폐 아줌마", "민폐 아저씨", "정장남", "대머리 정장남", "취객", "여자아이", "점잖은 할아버지",
            "단소 할아버지", "오컬트 여자", "군인", "경찰", "운동광", "얼굴 없는 남자",
        };

        /// <summary>없는 상품: (이름, 단가). 가격도 어딘가 기분 나쁘게.</summary>
        static readonly (string name, int price)[] FakeItems =
        {
            ("손가락", 3330), ("눈알", 1300), ("피", 333), ("머리카락", 500), ("이빨", 3000), ("귀", 4440),
            ("쓰레기", 0), ("피생수", 1000), ("손가락 김밥", 1500), ("눈알 푸딩", 2200), ("썩은 빵", 2000),
            ("핏물 라면", 1300), ("검은 소주", 1900), ("젖은 머리카락", 330),
        };

        /// <summary>글씨가 깨진 것처럼 보이게 끼워 넣는 글자(한글 인코딩이 깨졌을 때 흔히 보이는 모양).</summary>
        const string BrokenKo = "꾨뱶쨌먭뀈뺤궗듬땲쒖슂놁뿉껌썝덉쓣삕볦쓽쀫씠꿔뜃";
        // 영어: 글자 인코딩이 깨졌을 때 흔한 모양, 중국어: "锟斤拷·烫烫烫"처럼 흔한 깨진 글자
        const string BrokenEn = "ÃƒÂ¢â€šÅ“Ã©Â¤Â©Ã¯Â¿Â½Ð¶Ñ";
        const string BrokenZh = "锟斤拷烫屯鍦鏄鐨勬绋鈥";
        static string Broken => Loc.Current == Lang.En ? BrokenEn : Loc.Current == Lang.Zh ? BrokenZh : BrokenKo;
        const string BrokenDigit = "?#%&";

        /// <summary>count개를 겹치지 않게 고른다.</summary>
        public static ReceiptAnomaly Pick(System.Random rnd, int count)
        {
            var pool = new List<ReceiptAnomaly>(All);
            ReceiptAnomaly result = ReceiptAnomaly.None;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rnd.Next(pool.Count);
                result |= pool[k];
                pool.RemoveAt(k);
            }
            return result;
        }

        static bool Has(ReceiptAnomaly set, ReceiptAnomaly a) => (set & a) != 0;

        /// <summary>
        /// 영수증 내용에 이상현상을 적용한다. 찍힌 물건(lines)과 total은 금전등록기와 같은 상태에서 부른다.
        /// personLabel: 손님 설명(사람 수 빼고, 예: "후드 청년"), party: 같이 온 사람 수.
        /// </summary>
        public static void Apply(ReceiptData d, ReceiptAnomaly a, System.Random rnd, string personLabel, int party = 1)
        {
            d.anomalies = a;
            party = System.Math.Max(1, party);
            d.visitorText = $"{personLabel} {party}";

            if (Has(a, ReceiptAnomaly.Visitor))
            {
                switch (rnd.Next(4))
                {
                    case 0: d.visitorText = "?????"; break;
                    case 1: d.visitorText = Garble(Loc.T($"{personLabel} {party}"), rnd, 0.7f); break;   // 지금 언어 이름을 깨뜨린다
                    case 2:
                        string other;
                        do other = OtherPeople[rnd.Next(OtherPeople.Length)]; while (other == personLabel);
                        d.visitorText = $"{other} {party}";
                        break;
                    default: d.visitorText = $"{personLabel} {party + rnd.Next(1, 3)}"; break; // 실제보다 한두 명 많음
                }
            }

            if (Has(a, ReceiptAnomaly.FakeItem))
            {
                int n = rnd.Next(1, 4);
                var used = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    int k;
                    do k = rnd.Next(FakeItems.Length); while (!used.Add(k));
                    var f = FakeItems[k];
                    int qty = rnd.NextDouble() < 0.75 ? 1 : rnd.Next(2, 4);
                    d.lines.Insert(rnd.Next(d.lines.Count + 1), new ReceiptLine { id = "fake", name = f.name, unitPrice = f.price, qty = qty, fake = true });
                    d.total += f.price * qty;
                }
            }

            if (Has(a, ReceiptAnomaly.Total))
            {
                int[] deltas = { 1000, 2000, 3300, 330, 5000, 3330, -1000, -500 };
                int delta;
                do delta = deltas[rnd.Next(deltas.Length)]; while (d.total + delta <= 0);
                d.total += delta;
            }

            if (Has(a, ReceiptAnomaly.Time))
            {
                switch (rnd.Next(4))
                {
                    case 0: d.time = "03:33"; break;
                    case 1:
                        // 근무 시간 밖의 엉뚱한 시각
                        d.time = $"{(rnd.Next(2) == 0 ? rnd.Next(9, 22) : rnd.Next(7, 12)):00}:{rnd.Next(60):00}";
                        break;
                    case 2: d.time = new[] { "27:61", "99:99", "00:-3", "33:33" }[rnd.Next(4)]; break;
                    default: d.time = Garble(d.time, rnd, 0.5f); break;
                }
            }

            if (Has(a, ReceiptAnomaly.Number))
            {
                int shown;
                switch (rnd.Next(3))
                {
                    case 0: shown = d.number > 1 ? d.number - 1 : d.number + 1; break;   // 앞 손님 번호와 중복
                    case 1: shown = d.number + rnd.Next(2, 7); break;                      // 건너뜀
                    default: shown = -1; break;                                            // 글자 깨짐
                }
                if (shown >= 0)
                {
                    d.numberText = $"No.{shown:0000}";
                    d.barcodeText = $"0333-{d.day:00}-{shown:0000}";
                }
                else
                {
                    d.numberText = Garble($"No.{d.number:0000}", rnd, 0.6f);
                    d.barcodeText = Garble($"0333-{d.day:00}-{d.number:0000}", rnd, 0.4f);
                }
            }

            d.paid = d.total;
            if (Has(a, ReceiptAnomaly.BodyPayment))
                d.paidText = Garble(d.paid.ToString("N0", CultureInfo.InvariantCulture), rnd, 0.8f);

            if (Has(a, ReceiptAnomaly.Blood))
            {
                d.bloody = true;
                d.bloodSeed = rnd.Next();
            }
        }

        /// <summary>글씨 깨짐: 글자 일부를 ?나 깨진 한글로 바꾼다(띄어쓰기는 그대로). 적어도 한 글자는 바뀐다.</summary>
        public static string Garble(string s, System.Random rnd, float amount)
        {
            var sb = new StringBuilder(s.Length);
            bool changed = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool swap = !char.IsWhiteSpace(c) && (rnd.NextDouble() < amount || (!changed && i == s.Length - 1));
                if (!swap) { sb.Append(c); continue; }
                changed = true;
                if (char.IsDigit(c) || c == ',' || c == '.' || c == ':' || c == '-')
                    sb.Append(rnd.NextDouble() < 0.5 ? BrokenDigit[rnd.Next(BrokenDigit.Length)] : Broken[rnd.Next(Broken.Length)]);
                else
                    sb.Append(rnd.NextDouble() < 0.3 ? '?' : Broken[rnd.Next(Broken.Length)]);
            }
            return sb.ToString();
        }
    }
}
