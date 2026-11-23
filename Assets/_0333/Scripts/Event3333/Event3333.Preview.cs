using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>에디터 미리보기: 3:33 이벤트마다 가장 그 이벤트다운 한 순간을 멈춘 화면으로 세운다(시간이 흐르지 않는다).</summary>
    public partial class Event3333
    {
        /// <summary>미리보기: ev 이벤트의 대표 장면.</summary>
        public void PreviewState(Ev333 ev)
        {
            PreviewReset();
            rnd = new System.Random(3333 + (int)ev);
            tx = new Txn();
            Tonight = ev;
            clock.SetMinute(clock.eventMinute);
            // 3:33의 거래는 늘 계산대 위 앞줄 등만 켜진 어둠 속(CCTV도)
            if (lights != null) lights.SetFrontOnly();
            if (cctv != null) cctv.Night = true;
            ItemBay bay = checkout.bay;
            void Items(int n) => bay.Place(bay.PickRandom(rnd, n, n, 0f), rnd);
            CustomerView Show(string id)
            {
                CustomerView v = null;
                foreach (var c in checkout.Pool) if (c != null && c.customerId == id) v = c;
                if (v != null) { v.SetVisible(true); tx.who = v; }
                return v;
            }
            void OpenCctv(int ch)
            {
                if (cctv == null || cctv.panel == null) return;
                cctv.panel.SetActive(true);
                cctv.SetChannel(ch);
            }

            switch (ev)
            {
                case Ev333.Unmanned:
                    Items(3);
                    SayAt(counterHead, "계산이요.");
                    break;
                case Ev333.Returner:
                {
                    CustomerView v = Show("office");
                    if (v != null) SetFace(v, v.grinFace);
                    Items(1);
                    SayMine("뭐 안 사고 가셨어요?");
                    if (choices != null) choices.ShowAtWorld(MineChoiceSpot, "아까 오셨던 분 맞죠?", "왜 웃으세요?");
                    break;
                }
                case Ev333.Voice:
                    SayAt(doorTop, "저기요... 문 좀 열어 주세요..");
                    break;
                case Ev333.Queue:
                    ShowQueue(queue.Length);
                    Items(2);
                    break;
                case Ev333.Door:
                    for (int i = 0; i < footprints.Length; i++) ShowFootprint(i);
                    if (door != null) door.SetOpenAmount(0.6f);
                    Items(1);
                    break;
                case Ev333.NoProduct:
                {
                    var bag = new List<(string id, bool anomaly)>();
                    for (int i = 0; i < 8; i++) bag.Add((i % 3 == 0 ? ItemBay.BlackId : ItemBay.ErrorId, false));
                    bay.Place(bag, rnd);
                    break;
                }
                case Ev333.Phone:
                    Items(2);
                    if (phone != null) phone.PreviewRinging(true, true);
                    break;
                case Ev333.Backward:
                {
                    CustomerView v = Show("gym");
                    Items(2);
                    clock.SetMinute(clock.eventMinute - 21f);
                    ClockHud.Alarm = true;
                    if (v != null) checkout.SayBy(v, "3시가 되면... 늦어요.");
                    break;
                }
                case Ev333.Plead:
                {
                    CustomerView v = Show("leopard");
                    Items(2);
                    if (v != null) checkout.SayBy(v, "왜... 왜 안 해 줘요?");
                    break;
                }
                case Ev333.Hand:
                    Items(1);
                    if (hand != null) { hand.enabled = true; SetHand(1f); }
                    if (checkout.tray != null) checkout.tray.ShowBodyPartsInstant(5);
                    break;
                case Ev333.Receipt:
                {
                    Show("otaku");
                    Items(2);
                    checkout.BeginSpecial("03:33", 333, "오타쿠 1");
                    var names = Lines(Ev333.Receipt, "줄");
                    for (int i = 0; i < 26; i++)
                        checkout.Current.lines.Add(new ReceiptLine { id = "fake", name = names.Count > 0 ? names[i % names.Count] : "???", unitPrice = 333, qty = 1, fake = true });
                    if (checkout.printer != null) checkout.printer.PreviewEndless(1.1f);
                    break;
                }
                case Ev333.CctvStare:
                    Items(1);
                    ShowStare(1);
                    foreach (var s in stareFaces) if (s != null && s.transform.parent != (cctv != null && cctv.panel != null ? cctv.panel.transform : null)) s.enabled = true;
                    OpenCctv(0);
                    break;
                case Ev333.Bleed:
                {
                    Show("drunk");
                    Items(3);
                    foreach (var p in bay.Current) if (p.renderer != null) new BloodPool(this, p.renderer, rnd.Next()).Draw(1f);
                    break;
                }
                case Ev333.CctvNoMe:
                {
                    CustomerView v = Show("couple");
                    Items(2);
                    Present(playerCctv, false);
                    Present(notMe, true);
                    OpenCctv(1);
                    if (v != null) checkout.SayBy(v, "...거기 누구 있어요?");
                    break;
                }
                case Ev333.CctvStatic:
                {
                    Show("soldier");
                    Items(2);
                    var tex = new Dictionary<SpriteRenderer, PixelTex>();
                    UnityEngine.Random.InitState(7);
                    foreach (var s in staticScreens) if (s != null) { s.enabled = true; DrawStatic(s, tex, true); }
                    OpenCctv(2);
                    break;
                }
                case Ev333.Predecessor:
                    Items(1);
                    Present(predecessor, true);
                    OpenCctv(1);
                    SayMine("...정상으로 마감하지 마.");
                    break;
                case Ev333.Mirror:
                    Show("hoodie");
                    Items(2);
                    if (fx != null) fx.Mirror = true;
                    break;
                case Ev333.MissingBack:
                {
                    Items(1);
                    string[] ids = { "office", "student" };
                    for (int slot = 0; slot < ids.Length && slot < missingFront.Length; slot++)
                    {
                        MissingArt art = System.Array.Find(missingArt, a => a != null && a.id == ids[slot]);
                        if (art == null) continue;
                        missingFront[slot].sprite = art.front;
                        missingFront[slot].enabled = true;
                        if (slot < missingPanel.Length && slot < art.panel.Length && art.panel[slot] != null)
                        { missingPanel[slot].GetComponent<SpriteRenderer>().sprite = art.panel[slot]; Present(missingPanel[slot], true); }
                    }
                    OpenCctv(0);
                    break;
                }
                case Ev333.RedSight:
                    Items(2);
                    if (fx != null) fx.PreviewVeil(0.6f);
                    if (redShape != null) { redShape.enabled = true; redShape.color = new Color(0.12f, 0.01f, 0.02f, 0.6f); }
                    break;
                case Ev333.Whisper:
                {
                    Items(1);
                    if (fx != null)
                    {
                        string[] w = { "끝내", "왜 안 해", "눌러", "빨리", "정상이야", "마감해", "끝내", "너도 여기 남아", "눌러 눌러" };
                        var r = new System.Random(20);
                        for (int i = 0; i < w.Length; i++) fx.PreviewWhisper(i, w[i], new Vector2(r.Next(-300, 260), r.Next(-150, 150)), r.Next(9, 13));
                        ChecklistView sheet = checkout.checklistView;
                        if (sheet != null)
                        {
                            checkout.BeginSpecial("03:33", 333, "방문자 1");
                            sheet.ShowPreview(checkout.Entries, 3, 333, true, Verdict.Normal, Verdict.None);
                            RectInt e = sheet.EndButtonRect;
                            fx.PreviewWhisper(w.Length, "눌러", new Vector2(e.x + e.width / 2 - 12, e.yMax + 3), 12);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>미리보기로 바꾼 것을 되돌린다.</summary>
        public void PreviewReset()
        {
            if (bubble != null) bubble.Hide();
            if (choices != null) choices.Hide();
            if (tx != null && tx.who != null) { RestoreFace(tx.who); tx.who.SetVisible(false); }
            foreach (var c in checkout.Pool) if (c != null && c.Visible) c.SetVisible(false);
            checkout.bay.Clear();
            if (checkout.tray != null) checkout.tray.Clear();
            if (checkout.printer != null) checkout.printer.Tear();
            if (checkout.checklistView != null && checkout.checklistView.panel != null) checkout.checklistView.panel.SetActive(false);
            if (checkout.receiptView != null && checkout.receiptView.panel != null) checkout.receiptView.panel.SetActive(false);
            checkout.EndSpecial();
            if (phone != null) phone.PreviewRinging(false);
            if (cctv != null && cctv.panel != null) cctv.panel.SetActive(false);
            ClockHud.Alarm = false;
            HideAllArt();
            foreach (var g in spawned) if (g != null) DestroyImmediate(g);
            spawned.Clear();
            if (fx != null) { fx.ResetAll(); fx.PreviewVeil(0f); }
            if (lights != null) lights.SetAll(1f);
            if (cctv != null) cctv.Night = false;
            if (clock != null) clock.SetMinute(clock.eventMinute);
        }
    }
}
