using System;
using System.Collections.Generic;

namespace Night0333
{
    /// <summary>영수증 한 줄(같은 물건은 한 줄에 수량으로 묶는다).</summary>
    [Serializable]
    public class ReceiptLine
    {
        public string id;
        public string name;
        public int unitPrice;
        public int qty;
        [UnityEngine.Tooltip("이상현상으로 끼어든, 실제로는 팔지 않은 상품 줄")]
        public bool fake;
        public int Amount => unitPrice * qty;
    }

    /// <summary>영수증 이상현상 종류. 한 영수증에 여러 개가 겹칠 수 있다.</summary>
    [Flags]
    public enum ReceiptAnomaly
    {
        None = 0,
        /// <summary>손님 설명이 이상함: ?????, 글씨 깨짐, 다른 사람, 사람 수가 다름.</summary>
        Visitor = 1 << 0,
        /// <summary>금전등록기(LED) 숫자와 영수증 합계가 다름.</summary>
        Total = 1 << 1,
        /// <summary>돈 대신 손가락·눈·뇌·장기를 돈바구니에 줌. 영수증 "받은 돈"은 글자 깨짐.</summary>
        BodyPayment = 1 << 2,
        /// <summary>영수증 시각이 다름.</summary>
        Time = 1 << 3,
        /// <summary>영수증 번호 중복/건너뜀/글자 깨짐.</summary>
        Number = 1 << 4,
        /// <summary>없는 상품 줄(신체 부위, 피, 쓰레기, 피생수 등)이 끼어 있음.</summary>
        FakeItem = 1 << 5,
        /// <summary>영수증에 피가 묻어 있음.</summary>
        Blood = 1 << 6,
    }

    /// <summary>점검표의 판정: 아직 안 고름 / 정상(예) / 비정상(아니오).</summary>
    public enum Verdict { None, Normal, Abnormal }

    /// <summary>점검표 한 줄(손님 한 명의 거래).</summary>
    [Serializable]
    public class ChecklistEntry
    {
        public int number;
        public string time;
        public Verdict verdict;
        [UnityEngine.Tooltip("이 거래의 영수증에 실제로 들어간 이상현상(나중에 판정이 맞았는지 볼 때 쓴다)")]
        public ReceiptAnomaly receiptAnomalies;
        public string customerId;
        /// <summary>손님 설명(예: "후드 청년 1").</summary>
        public string label;
        /// <summary>정답: 이상한 손님이었는지.</summary>
        public bool strange;
        /// <summary>결과 화면에 적을 이상현상 이유(한 줄씩).</summary>
        public string[] reasons = new string[0];
        /// <summary>결과 화면 초상화용(저장하지 않음).</summary>
        [NonSerialized] public CustomerView view;

        /// <summary>판정이 맞았는지: 예(정상)↔평범한 손님, 아니오(비정상)↔이상한 손님.</summary>
        public bool Correct => verdict != Verdict.None && (verdict == Verdict.Abnormal) == strange;
    }

    /// <summary>
    /// 영수증 한 장의 내용. 이상현상은 이 값을 바꿔서 만든다(ReceiptAnomalies).
    /// 글자로 찍히는 값(시각, 번호, 손님 설명, 받은 돈)은 실제 값과 다르게 찍힐 수 있다.
    /// </summary>
    [Serializable]
    public class ReceiptData
    {
        public string storeName = "24시 편의점";
        public string branch = "3호점";
        public int day = 1;
        /// <summary>영수증에 찍힌 시각.</summary>
        public string time = "00:00";
        /// <summary>실제 영수증 번호(점검표와 같음).</summary>
        public int number = 1;
        /// <summary>손님 설명 줄(예: "후드 청년 1").</summary>
        public string visitorText = "손님 1";
        /// <summary>영수증에 찍힌 번호 글자. 비어 있으면 No.0001 꼴.</summary>
        public string numberText;
        /// <summary>바코드 아래 숫자. 비어 있으면 0333-01-0001 꼴.</summary>
        public string barcodeText;
        /// <summary>받은 돈 글자. 비어 있으면 paid를 숫자로.</summary>
        public string paidText;
        public List<ReceiptLine> lines = new List<ReceiptLine>();
        /// <summary>영수증에 찍힌 합계(이상현상이면 금전등록기와 다를 수 있음).</summary>
        public int total;
        public int paid;
        public bool bloody;
        public int bloodSeed;
        /// <summary>이 영수증에 들어간 이상현상.</summary>
        public ReceiptAnomaly anomalies;

        public string NumberLabel => string.IsNullOrEmpty(numberText) ? $"No.{number:0000}" : numberText;
        public string BarcodeLabel => string.IsNullOrEmpty(barcodeText) ? $"0333-{day:00}-{number:0000}" : barcodeText;

        public int ItemCount
        {
            get { int n = 0; foreach (var l in lines) n += l.qty; return n; }
        }

        public void Add(string id, string name, int price)
        {
            foreach (var l in lines)
                if (l.id == id && l.unitPrice == price && !l.fake) { l.qty++; total += price; return; }
            lines.Add(new ReceiptLine { id = id, name = name, unitPrice = price, qty = 1 });
            total += price;
        }
    }
}
