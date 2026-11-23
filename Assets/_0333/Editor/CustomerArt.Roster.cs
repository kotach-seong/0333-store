using System.Collections.Generic;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 손님 시안 목록(사용자가 정한 23종 중 PixelLab으로 이미 만든 1번 후드·16번 얼굴 가린 정장을 뺀 나머지).
    /// 14번 진상은 남·여 둘로, 11번 커플과 21번 시장+동행은 한 그림에 두 사람.
    /// 실존 인물·기존 캐릭터를 따라 그리지 않는다(21번은 가상의 정치인, 22번은 미쿠가 아닌 창작 코스프레).
    /// 미성년자(9번 아이, 23번 고등학생)는 성적인 묘사 없이 평범하게.
    /// </summary>
    public static partial class CustomerArt
    {
        // ── 무늬 ──
        static Color32 Leopard(int x, int y)
        {
            int cx = Mathf.FloorToInt(x / 5f), cy = Mathf.FloorToInt(y / 4f);
            float ox = cx * 5f + 1f + N(cx, cy, 81) * 3f, oy = cy * 4f + 1f + N(cx, cy, 82) * 2f;
            float d = Mathf.Sqrt((x + 0.5f - ox) * (x + 0.5f - ox) + (y + 0.5f - oy) * (y + 0.5f - oy) * 1.4f);
            if (d < 0.8f) return C("#A8743A");
            if (d < 1.9f && N(x, y, 83) > 0.2f) return C("#3A2414");
            return C("#C8913F");
        }

        static Color32 Camo(int x, int y)
        {
            float n = N(x / 2, y / 2, 91) * 0.55f + N(x / 5, y / 4, 92) * 0.45f;
            return n < 0.3f ? C("#3E4A33") : n < 0.52f ? C("#5B6B45") : n < 0.74f ? C("#7A7A55") : C("#8C8466");
        }

        static Color32 Plaid(int x, int y)
        {
            if (x % 12 == 3 || y % 12 == 3) return C("#7A2A30");
            if (x % 6 == 0 || y % 6 == 0) return C("#3C5A48");
            return C("#2C3550");
        }

        static Color32 Floral(int x, int y)
        {
            float n = N(x / 3, y / 3, 101);
            if (n > 0.8f && (x + y) % 3 != 0) return C("#C83A4A");
            if (n > 0.66f && (x + y) % 2 == 0) return C("#5A8A4A");
            return C("#E8C2CC");
        }

        static Vector2 HandPos(Body b, int side, Pose pose)
        {
            b.Arm(side, pose, out _, out Vector2 E, out Vector2 W);
            return W + (W - E).normalized * (b.handRy * 0.75f);
        }

        static void RedrawHand(Fig f, Body b, Vector2 at, Color32 skin)
        {
            int r = f.Region(Mat.Skin);
            f.Ellipse(r, at.x, at.y, b.handRx, b.handRy * 0.85f, Solid(skin));
        }

        static List<Spec> Roster()
        {
            var list = new List<Spec>();

            // 2. 운동광: 근육질, 민소매, 반바지, 손에 단백질 셰이커
            var gymSkin = C("#C98E68");
            list.Add(new Spec
            {
                id = "gym", label = "운동광", heightM = 1.78f, build = Build.Muscular, skin = gymSkin,
                hair = Hair.Buzz, hairCol = C("#1E1A18"), brow = Brow.Flat, mouth = Mouth.Grin, face = Face.Sweat,
                top = Top.Tank, topCol = C("#2B2F36"), bottom = Bottom.Shorts, bottomCol = C("#1F2A44"),
                shoes = Shoes.Sneakers, shoeCol = C("#E8E8E2"),
                regions = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.Down);
                    int r = f.Region();
                    f.Capsule(r, new Vector2(h.x, h.y + 3f), new Vector2(h.x, h.y - 9f), 2.8f, 2.8f, Solid(C("#1A1A1E")));
                    int lid = f.Region();
                    f.Rows(lid, h.y + 2.5f, h.y + 5.5f, y => Span(h.x, 2.4f), Solid(C("#E8E8E2")));
                    RedrawHand(f, b, h, gymSkin);
                },
            });

            // 3. 야쿠자: 올백, 선글라스, 뺨에 흉터, 검은 정장 안에 화려한 셔츠, 금목걸이
            list.Add(new Spec
            {
                id = "yakuza", label = "야쿠자", heightM = 1.75f, skin = C("#D9A27E"),
                hair = Hair.Slick, hairCol = C("#141416"), face = Face.Sunglasses | Face.Scar, mouth = Mouth.Frown, brow = Brow.Angry,
                top = Top.LoudShirtSuit, topCol = C("#1C1C20"), topCol3 = C("#7A1E22"),
                bottom = Bottom.Slacks, bottomCol = C("#18181C"), shoes = Shoes.Dress, shoeCol = C("#141414"),
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    int cx = Xi(b.cx);
                    for (int i = 0; i < 6; i++) { f.On(cx - 4 + i / 2, Xi(b.sh) - 1 - i, C("#E2C25A")); f.On(cx + 3 - i / 2, Xi(b.sh) - 1 - i, C("#C9A43A")); }
                },
            });

            // 4. 말끔한 정장 직장인: 가르마, 남색 정장, 손에 휴대폰
            var officeSkin = C("#E6B596");
            list.Add(new Spec
            {
                id = "office", label = "정장남", heightM = 1.76f, skin = officeSkin,
                hair = Hair.Parted, hairCol = C("#2A2220"), mouth = Mouth.Flat,
                top = Top.Suit, topCol = C("#2A3550"), topCol2 = C("#EEF0F2"), topCol3 = C("#3A5A8C"),
                bottom = Bottom.Slacks, bottomCol = C("#262F48"), shoes = Shoes.Dress, shoeCol = C("#141414"),
                regions = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.Down);
                    int r = f.Region(Mat.Shiny);
                    f.Rows(r, h.y - 5f, h.y + 2f, y => Span(h.x - 0.5f, 2f), Solid(C("#18181C")));
                    RedrawHand(f, b, new Vector2(h.x, h.y - 1f), officeSkin);
                },
            });

            // 5. 대머리 정장 직장인: 옆머리만 남은 대머리, 회색 정장, 지친 얼굴, 풀린 넥타이
            list.Add(new Spec
            {
                id = "bald_office", label = "대머리 정장남", heightM = 1.70f, build = Build.Chubby, skin = C("#DDA585"),
                hair = Hair.BaldFringe, hairCol = C("#3A3430"), eyes = Eyes.Tired, brow = Brow.Worried, mouth = Mouth.Frown, face = Face.Wrinkles,
                top = Top.Suit, topCol = C("#5C6066"), topCol2 = C("#E8E6DF"), topCol3 = C("#6E2A2A"),
                bottom = Bottom.Slacks, bottomCol = C("#4A4E54"), shoes = Shoes.Dress, shoeCol = C("#2A2422"),
                overlay = (f, b, back) =>
                {
                    // 번들거리는 정수리
                    int y = Xi(b.top - 2f);
                    f.On(Xi(b.cx) - 3, y, C("#F4D2BA")); f.On(Xi(b.cx) - 2, y, C("#F4D2BA")); f.On(Xi(b.cx) - 2, y - 1, C("#EEC6AA"));
                },
            });

            // 6. 취한 손님: 빨간 얼굴, 풀린 눈, 헝클어진 머리, 풀어 헤친 셔츠와 넥타이, 손에 소주병
            var drunkSkin = C("#E2A086");
            list.Add(new Spec
            {
                id = "drunk", label = "취객", heightM = 1.73f, skin = drunkSkin,
                hair = Hair.Messy, hairCol = C("#2E2622"), eyes = Eyes.Tired, brow = Brow.Worried, mouth = Mouth.Open, face = Face.Flush | Face.Stubble,
                top = Top.TShirt, topCol = C("#E4E2DA"), bottom = Bottom.Slacks, bottomCol = C("#3A3E46"), shoes = Shoes.Dress, shoeCol = C("#2A2420"),
                regions = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.Down);
                    int r = f.Region(Mat.Shiny);
                    f.Capsule(r, new Vector2(h.x, h.y - 1f), new Vector2(h.x + 1f, h.y - 11f), 2.4f, 2.6f, Solid(C("#3E8A4E")));
                    f.Capsule(r, new Vector2(h.x, h.y + 4f), new Vector2(h.x, h.y), 1.1f, 1.6f, Solid(C("#3E8A4E")));
                    RedrawHand(f, b, h, drunkSkin);
                },
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    int cx = Xi(b.cx);
                    // 풀린 넥타이(비스듬히)와 셔츠 깃
                    f.Line(cx, Xi(b.sh) - 2, cx + 3, Xi(b.sh) - 16, C("#8A2A2A"), true);
                    f.Line(cx + 1, Xi(b.sh) - 2, cx + 4, Xi(b.sh) - 15, C("#6E2020"), true);
                    f.On(cx - 4, Xi(b.sh) - 1, C("#F6F4EE")); f.On(cx - 3, Xi(b.sh) - 2, C("#F6F4EE"));
                    f.On(cx + 3, Xi(b.sh) - 1, C("#F6F4EE")); f.On(cx + 4, Xi(b.sh) - 2, C("#F6F4EE"));
                    // 소주병 상표
                    Vector2 h = HandPos(b, 1, Pose.Down);
                    for (int y = Xi(h.y) - 8; y <= Xi(h.y) - 5; y++) { f.On(Xi(h.x) - 1, y, C("#E8E8E2")); f.On(Xi(h.x), y, C("#E8E8E2")); f.On(Xi(h.x) + 1, y, C("#2A4A8A")); }
                },
            });

            // 7. 오컬트에 빠진 여자: 창백한 얼굴, 한쪽 눈을 덮은 긴 검은 머리, 검은 옷과 숄, 손에 든 부적
            var occultSkin = C("#EAD3C6");
            list.Add(new Spec
            {
                id = "occult", label = "오컬트 여자", heightM = 1.65f, build = Build.Female, skin = occultSkin,
                hair = Hair.Long, hairCol = C("#121014"), face = Face.HairOverEye | Face.Lipstick | Face.Eyeshadow, eyes = Eyes.Narrow, mouth = Mouth.Smile,
                iris = C("#6A2A3A"),
                top = Top.Robe, topCol = C("#1A161C"), topCol2 = C("#3A2442"), bottom = Bottom.Covered,
                shoes = Shoes.Boots, shoeCol = C("#1A1A1A"), pose = Pose.RaiseRight,
                regions = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.RaiseRight);
                    int r = f.Region(Mat.Flat);
                    f.Rows(r, h.y + 1f, h.y + 11f, y => Span(h.x - 1f, 2.6f), Solid(C("#E6CF6A")));
                    RedrawHand(f, b, h, occultSkin);
                },
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.RaiseRight);
                    int x = Xi(h.x - 1f), y = Xi(h.y) + 3;
                    f.VLine(x, y, y + 6, C("#B0202A")); f.Px(x - 1, y + 5, C("#B0202A")); f.Px(x + 1, y + 3, C("#B0202A")); f.Px(x - 1, y + 1, C("#B0202A"));
                },
            });

            // 8. 사이비에 빠진 아줌마: 뽀글 파마, 카디건, 지나치게 반가운 웃음, 품에 안은 전도지 책
            var cultSkin = C("#E2B092");
            list.Add(new Spec
            {
                id = "cult", label = "사이비 아줌마", heightM = 1.58f, build = Build.Female, skin = cultSkin,
                hair = Hair.Perm, hairCol = C("#3A2A24"), eyes = Eyes.Wide, mouth = Mouth.Grin, face = Face.Blush,
                top = Top.Cardigan, topCol = C("#8A7A6A"), topCol2 = C("#E6DDD0"),
                bottom = Bottom.Slacks, bottomCol = C("#4A4038"), shoes = Shoes.Dress, shoeCol = C("#3A302A"), pose = Pose.Hold,
                regions = (f, b, back) =>
                {
                    if (back) return;
                    b.Arm(1, Pose.Hold, out _, out _, out Vector2 W);
                    int r = f.Region();
                    f.Rows(r, W.y - 3f, W.y + 12f, y => Span(b.cx, 6.5f), Solid(C("#6E2A2A")));
                    RedrawHand(f, b, HandPos(b, -1, Pose.Hold), cultSkin);
                    RedrawHand(f, b, HandPos(b, 1, Pose.Hold), cultSkin);
                },
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    b.Arm(1, Pose.Hold, out _, out _, out Vector2 W);
                    int cx = Xi(b.cx), cy = Xi(W.y + 6f);
                    Color32 gold = C("#E2C25A");
                    f.Px(cx - 1, cy, gold); f.Px(cx, cy, gold); f.Px(cx - 1, cy + 1, gold); f.Px(cx, cy + 1, gold);
                    f.Px(cx - 1, cy + 3, gold); f.Px(cx, cy - 2, gold); f.Px(cx - 3, cy, gold); f.Px(cx + 2, cy + 1, gold);
                },
            });

            // 9. 어린아이(여자): 노란 우비 모자를 푹 뒤집어쓰고 빨간 장화
            list.Add(new Spec
            {
                id = "child", label = "여자아이", heightM = 1.18f, build = Build.Child, skin = C("#F0C6A8"),
                hair = Hair.Pigtails, hairCol = C("#2A1E18"), hat = Hat.Hood, hatCol = C("#E8C22E"), eyes = Eyes.Wide, mouth = Mouth.Flat,
                iris = C("#2A1E18"),
                top = Top.Raincoat, topCol = C("#E8C22E"), bottom = Bottom.Covered, shoes = Shoes.RainBoots, shoeCol = C("#B83A32"),
            });

            // 10. 안경이 뿌옇게 김 서린 사람: 비에 젖은 머리, 패딩
            list.Add(new Spec
            {
                id = "foggy", label = "안경 뿌연 남자", heightM = 1.74f, skin = C("#E3B08F"),
                hair = Hair.Messy, hairCol = C("#2A2622"), face = Face.Foggy, mouth = Mouth.Flat,
                top = Top.Puffer, topCol = C("#4E5A48"), bottom = Bottom.Jeans, bottomCol = C("#3A4A66"),
                shoes = Shoes.Sneakers, shoeCol = C("#9A9A96"),
                overlay = (f, b, back) =>
                {
                    // 머리와 어깨에 맺힌 빗방울
                    for (int i = 0; i < 14; i++)
                    {
                        int x = Xi(b.cx - b.shHalf + N(i, 1, 111) * b.shHalf * 2f), y = Xi(b.sh - 4f + N(i, 2, 111) * 6f);
                        f.On(x, y, C("#C8E2EE"));
                    }
                    for (int i = 0; i < 6; i++) f.On(Xi(b.cx - b.headRx + N(i, 3, 111) * b.headRx * 2f), Xi(b.top - 1f - N(i, 4, 111) * 3f), C("#C8E2EE"));
                },
            });

            // 11. 커플: 똑같은 하트 커플티를 입고 같이 온 두 사람
            Overlay heart = (f, b, back) =>
            {
                if (back) return;
                int cx = Xi(b.cx), cy = Xi(b.sh - b.Torso * 0.42f);
                string[] h = { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." };
                for (int r = 0; r < h.Length; r++)
                    for (int c = 0; c < h[r].Length; c++)
                        if (h[r][c] == '#') f.On(cx - 3 + c, cy - r, C("#D23A4A"));
            };
            list.Add(new Spec
            {
                id = "couple", label = "커플", heightM = 1.76f, skin = C("#E3B08F"),
                hair = Hair.Short, hairCol = C("#2A2220"), mouth = Mouth.Smile,
                top = Top.TShirt, topCol = C("#F0EEE8"), bottom = Bottom.Jeans, bottomCol = C("#41587A"),
                shoes = Shoes.Sneakers, overlay = heart.Invoke,
                partner = new Spec
                {
                    id = "couple_w", heightM = 1.62f, build = Build.Female, skin = C("#EEC6AA"),
                    hair = Hair.Long, hairCol = C("#4A3226"), mouth = Mouth.Smile, face = Face.Blush,
                    top = Top.TShirt, topCol = C("#F0EEE8"), bottom = Bottom.Jeans, bottomCol = C("#41587A"),
                    shoes = Shoes.Sneakers, overlay = heart.Invoke,
                },
            });

            // 12. 호피무늬를 걸친 부자 아줌마: 붉은 파마, 털 깃 호피 코트, 빨간 입술, 금 귀걸이, 명품 가방
            var richSkin = C("#E3B095");
            list.Add(new Spec
            {
                id = "leopard", label = "호피 아줌마", heightM = 1.60f, build = Build.Female, skin = richSkin,
                hair = Hair.Perm, hairCol = C("#6A3424"), face = Face.Lipstick | Face.Eyeshadow, mouth = Mouth.Smile, eyes = Eyes.Narrow,
                top = Top.Coat, topPaint = Leopard, topCol = C("#C8913F"), topCol2 = C("#E6D8C0"),
                bottom = Bottom.Covered, shoes = Shoes.Heels, shoeCol = C("#7A1A1A"),
                regions = (f, b, back) =>
                {
                    if (back) return;
                    b.Arm(1, Pose.Down, out _, out Vector2 E, out _);
                    int r = f.Region(Mat.Shiny);
                    f.Rows(r, E.y - 13f, E.y - 3f, y => new Vector2(E.x - 3f, E.x + 7f + (E.y - 3f - y) * 0.15f), Solid(C("#3A2418")));
                },
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    b.Arm(1, Pose.Down, out _, out Vector2 E, out _);
                    f.Px(Xi(E.x + 1f), Xi(E.y - 7f), C("#E2C25A")); f.Px(Xi(E.x + 2f), Xi(E.y - 7f), C("#E2C25A"));
                    int ey = Xi(b.headCy - b.headRy * 0.35f);
                    f.Px(Xi(b.cx - b.headRx - 0.5f), ey, C("#E2C25A")); f.Px(Xi(b.cx - b.headRx - 0.5f), ey - 1, C("#C9A43A"));
                    f.Px(Xi(b.cx + b.headRx), ey, C("#E2C25A")); f.Px(Xi(b.cx + b.headRx), ey - 1, C("#C9A43A"));
                },
            });

            // 13. 단소 할아버지: 흰 수염, 두루마기, 두 손으로 세워 든 단소
            var elderSkin = C("#D9A27E");
            list.Add(new Spec
            {
                id = "danso", label = "단소 할아버지", heightM = 1.66f, build = Build.Thin, skin = elderSkin,
                hair = Hair.Short, hairCol = C("#E2E2DC"), face = Face.Beard | Face.Mustache | Face.Wrinkles, eyes = Eyes.Tired, mouth = Mouth.Flat,
                top = Top.Hanbok, topCol = C("#CFCBBE"), topCol3 = C("#3A4A6A"), bottom = Bottom.Covered,
                shoes = Shoes.Dress, shoeCol = C("#3A3030"), pose = Pose.Hold,
                regions = (f, b, back) =>
                {
                    if (back) return;
                    b.Arm(1, Pose.Hold, out _, out _, out Vector2 W);
                    int r = f.Region();
                    f.Capsule(r, new Vector2(b.cx + 1f, W.y - 8f), new Vector2(b.cx + 1f, b.sh - 1f), 1.6f, 1.5f, Solid(C("#C9B06A")));
                    RedrawHand(f, b, new Vector2(b.cx + 1f, W.y + 1f), elderSkin);
                    RedrawHand(f, b, new Vector2(b.cx + 1f, W.y + 8f), elderSkin);
                },
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    b.Arm(1, Pose.Hold, out _, out _, out Vector2 W);
                    for (int y = Xi(W.y + 13f); y < Xi(b.sh - 2f); y += 5) f.Px(Xi(b.cx + 1f), y, C("#8A7238"));
                },
            });

            // 14-1. 진상(남): 골프 모자, 패딩 조끼, 화난 얼굴, 들어 올린 손에 비둘기("이거 교환해 줘요")
            var jinsangSkin = C("#D69A78");
            list.Add(new Spec
            {
                id = "jinsang_m", label = "민폐 아저씨", heightM = 1.70f, build = Build.Chubby, skin = jinsangSkin,
                hair = Hair.Short, hairCol = C("#2A2420"), hat = Hat.Cap, hatCol = C("#E8E6E0"), hatCol2 = C("#2A3A5A"),
                face = Face.Stubble, brow = Brow.Angry, mouth = Mouth.Open, eyes = Eyes.Narrow,
                top = Top.Vest, topCol = C("#3A6A9A"), topCol2 = C("#2A2E36"),
                bottom = Bottom.Slacks, bottomCol = C("#7A7266"), shoes = Shoes.Sneakers, pose = Pose.RaiseRight,
                regions = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.RaiseRight);
                    int body = f.Region();
                    f.Ellipse(body, h.x + 1f, h.y + 4f, 5.5f, 3.6f, Solid(C("#8A8E96")));
                    int head = f.Region();
                    f.Ellipse(head, h.x - 3.5f, h.y + 7.5f, 2.2f, 2.2f, Solid(C("#7A7E88")));
                    RedrawHand(f, b, new Vector2(h.x + 1f, h.y + 1f), jinsangSkin);
                },
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    Vector2 h = HandPos(b, 1, Pose.RaiseRight);
                    int hx = Xi(h.x - 3.5f), hy = Xi(h.y + 7.5f);
                    f.Px(hx - 1, hy, C("#E8A040")); f.Px(hx - 2, hy, C("#E8A040"));   // 부리
                    f.Px(hx, hy + 1, C("#C83A2A"));                                    // 눈
                    f.Px(hx + 1, hy - 2, C("#4A8A6A")); f.Px(hx + 2, hy - 2, C("#7A4A8A")); // 목 깃털
                    f.Px(Xi(h.x + 4f), Xi(h.y + 5f), C("#5A5E66")); f.Px(Xi(h.x + 5f), Xi(h.y + 4f), C("#5A5E66"));
                },
            });

            // 14-2. 진상(여): 선캡, 꽃무늬 블라우스, 허리에 손 올리고 "여기 사장 나오라 해"
            list.Add(new Spec
            {
                id = "jinsang_w", label = "민폐 아줌마", heightM = 1.60f, build = Build.Female, skin = C("#E6B394"),
                hair = Hair.Perm, hairCol = C("#2E2420"), hat = Hat.Visor, hatCol = C("#F2F0EA"), hatCol2 = C("#6A5A9A"),
                face = Face.Lipstick, brow = Brow.Angry, mouth = Mouth.Open,
                top = Top.Blouse, topPaint = Floral, topCol = C("#E8C2CC"),
                bottom = Bottom.Slacks, bottomCol = C("#5A4A6A"), shoes = Shoes.Sneakers, shoeCol = C("#D8D4CC"), pose = Pose.Hips,
            });

            // 15. 점잖은 할아버지: 단정한 회색 가르마, 안경, 카디건, 온화한 미소
            list.Add(new Spec
            {
                id = "gentle_elder", label = "점잖은 할아버지", heightM = 1.68f, build = Build.Thin, skin = C("#DDA584"),
                hair = Hair.Parted, hairCol = C("#9A9A96"), face = Face.Glasses | Face.Wrinkles | Face.Mustache, mouth = Mouth.Smile,
                glassesCol = C("#7A6A4A"),
                top = Top.Cardigan, topCol = C("#5A4A3E"), topCol2 = C("#E6E2D6"),
                bottom = Bottom.Slacks, bottomCol = C("#4A4A4E"), shoes = Shoes.Dress, shoeCol = C("#3A2A22"),
            });

            // 17. 씹덕(170cm, 180kg): 두건, 안경, 땀, 창작 애니 캐릭터 프린트 티, 백팩
            list.Add(new Spec
            {
                id = "otaku", label = "오타쿠", heightM = 1.70f, build = Build.Huge, skin = C("#ECC0A2"),
                hair = Hair.Messy, hairCol = C("#1E1A1A"), hat = Hat.Bandana, hatCol = C("#B8323A"),
                face = Face.Glasses | Face.Sweat | Face.DoubleChin, mouth = Mouth.Flat,
                top = Top.TShirt, topCol = C("#E8E6E0"), bottom = Bottom.Cargo, bottomCol = C("#5A5E48"),
                shoes = Shoes.Sneakers, shoeCol = C("#C8C8C0"),
                regions = (f, b, back) =>
                {
                    if (back)
                    {
                        int pack = f.Region();
                        f.Rows(pack, b.waistY + 2f, b.sh - 1f, y => Span(b.cx, b.shHalf - 3f - (y > b.sh - 4f ? 2f : 0f)), Solid(C("#3A3E4A")));
                        int pocket = f.Region();
                        f.Rows(pocket, b.waistY + 3f, b.waistY + 12f, y => Span(b.cx, b.shHalf - 9f), Solid(C("#2E3240")));
                        return;
                    }
                    int strap = f.Region();
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float sx = b.cx + side * b.shHalf * 0.55f;
                        f.Rows(strap, b.waistY + 4f, b.sh + 0.5f, y => new Vector2(sx - 1.6f + side * (b.sh - y) * 0.08f, sx + 1.6f + side * (b.sh - y) * 0.08f), Solid(C("#2A2A30")));
                    }
                },
                overlay = (f, b, back) =>
                {
                    if (back)
                    {
                        // 가방에 단 배지
                        f.Px(Xi(b.cx - 5f), Xi(b.waistY + 16f), C("#E86A9A")); f.Px(Xi(b.cx + 4f), Xi(b.waistY + 18f), C("#6AB4E8"));
                        return;
                    }
                    // 창작 캐릭터 얼굴 프린트(분홍 머리, 큰 눈)
                    int cx = Xi(b.cx), cy = Xi(b.bellyY + 4f);
                    string[] p =
                    {
                        "..PPPPP..", ".PPPPPPP.", "PPSSSSSPP", "PSWKSWKSP", "PSKKSKKSP", "PSSSRSSSP", ".PSSSSSP.", "..SSSSS..",
                    };
                    for (int r = 0; r < p.Length; r++)
                        for (int c = 0; c < p[r].Length; c++)
                        {
                            char ch = p[r][c];
                            Color32 col = ch == 'P' ? C("#E86A9A") : ch == 'S' ? C("#F6D6C0") : ch == 'W' ? C("#FFFFFF") : ch == 'K' ? C("#3A4AA8") : ch == 'R' ? C("#D84A5A") : default;
                            if (col.a > 0) f.On(cx - 4 + c, cy - r, col);
                        }
                },
            });

            // 18. 10원짜리 동전으로 계산하는 사람: 목 늘어난 얼룩진 티, 츄리닝, 삼선 슬리퍼, 손바닥 가득 동전
            var coinSkin = C("#D9A27E");
            list.Add(new Spec
            {
                id = "coins", label = "동전 손님", heightM = 1.71f, build = Build.Thin, skin = coinSkin,
                hair = Hair.Messy, hairCol = C("#2A2420"), face = Face.Stubble, eyes = Eyes.Tired, mouth = Mouth.Flat,
                top = Top.TShirt, topCol = C("#C9C6B8"), bottom = Bottom.Slacks, bottomCol = C("#2E3240"),
                shoes = Shoes.Slippers, shoeCol = C("#24345E"), pose = Pose.Hold,
                regions = (f, b, back) =>
                {
                    if (back) return;
                    // 목이 축 늘어난 티셔츠
                    int neck = f.Region(Mat.Skin);
                    f.Ellipse(neck, b.cx, b.sh - 1f, b.neckHalf + 4f, 6f, Solid(Mul(coinSkin, 0.95f)));
                    // 모은 손바닥 위 동전 더미
                    b.Arm(1, Pose.Hold, out _, out _, out Vector2 W);
                    int coins = f.Region(Mat.Shiny);
                    for (int i = 0; i < 6; i++)
                        f.Ellipse(coins, b.cx - 3f + (i % 3) * 3f, W.y + 3f + (i / 3) * 2f, 1.8f, 1.2f, Solid(C("#B8733A")));
                },
                overlay = (f, b, back) =>
                {
                    // 얼룩
                    for (int i = 0; i < 5; i++)
                    {
                        int x = Xi(b.cx - 10f + N(i, 1, 131) * 20f), y = Xi(b.waistY + 4f + N(i, 2, 131) * 16f);
                        for (int k = 0; k < 4; k++) f.On(x + k % 2, y - k / 2, new Color32(150, 120, 60, 150));
                    }
                    // 츄리닝 옆줄
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int x = Xi(b.cx + side * (b.hipHalf * 0.42f + b.thighHalf * 0.7f));
                        for (int y = Xi(b.ankleY) + 1; y < Xi(b.hipY); y++) f.On(x, y, C("#E8E8E2"));
                    }
                },
            });

            // 19. 군인: 철모, 얼룩무늬 군복, 두 손으로 든 소총(탈영병 같은 분위기: 얼굴과 옷에 흙)
            var soldierSkin = C("#D2976E");
            list.Add(new Spec
            {
                id = "soldier", label = "군인", heightM = 1.77f, skin = soldierSkin,
                hair = Hair.Buzz, hairCol = C("#1A1A18"), hat = Hat.Helmet, hatCol = C("#4A5638"),
                eyes = Eyes.Narrow, brow = Brow.Angry, mouth = Mouth.Frown, face = Face.Stubble,
                top = Top.Military, topPaint = Camo, topCol = C("#5B6B45"), bottom = Bottom.Cargo, bottomPaint = Camo, bottomCol = C("#5B6B45"),
                shoes = Shoes.Boots, shoeCol = C("#1E1E1A"), pose = Pose.Hold,
                regions = (f, b, back) =>
                {
                    if (back)
                    {
                        // 등에 멘 총
                        int sling = f.Region(Mat.Shiny);
                        f.Poly(sling, Solid(C("#26282A")), new Vector2(b.cx - 14f, b.waistY - 4f), new Vector2(b.cx - 11f, b.waistY - 6f),
                            new Vector2(b.cx + 15f, b.sh + 5f), new Vector2(b.cx + 12f, b.sh + 7f));
                        return;
                    }
                    b.Arm(1, Pose.Hold, out _, out _, out Vector2 W);
                    int gun = f.Region(Mat.Shiny);
                    Color32 g = C("#26282A");
                    // 총몸(왼쪽 아래 → 오른쪽 위), 개머리판, 탄창, 총열
                    f.Poly(gun, Solid(g), new Vector2(b.cx - 19f, W.y - 6f), new Vector2(b.cx - 17f, W.y - 9f),
                        new Vector2(b.cx + 14f, b.sh - 4f), new Vector2(b.cx + 12f, b.sh - 1f));
                    f.Poly(gun, Solid(C("#3A3026")), new Vector2(b.cx - 25f, W.y - 10f), new Vector2(b.cx - 20f, W.y - 13f),
                        new Vector2(b.cx - 15f, W.y - 6f), new Vector2(b.cx - 19f, W.y - 3f));
                    f.Poly(gun, Solid(g), new Vector2(b.cx - 2f, W.y + 2f), new Vector2(b.cx + 1f, W.y + 3f), new Vector2(b.cx + 2f, W.y - 4f), new Vector2(b.cx - 1f, W.y - 5f));
                    f.Capsule(gun, new Vector2(b.cx + 13f, b.sh - 2f), new Vector2(b.cx + 20f, b.sh + 3f), 0.9f, 0.8f, Solid(g));
                    RedrawHand(f, b, new Vector2(b.cx - 8f, W.y - 2f), soldierSkin);
                    RedrawHand(f, b, new Vector2(b.cx + 6f, W.y + 6f), soldierSkin);
                },
                overlay = (f, b, back) =>
                {
                    for (int i = 0; i < 10; i++)
                        f.On(Xi(b.cx - b.shHalf + N(i, 5, 141) * b.shHalf * 2f), Xi(b.hipY + N(i, 6, 141) * (b.sh - b.hipY)), new Color32(70, 52, 34, 160));
                    if (!back)
                    {
                        f.On(Xi(b.cx - b.headRx * 0.5f), Xi(b.headCy - b.headRy * 0.4f), new Color32(80, 58, 38, 170));
                        f.On(Xi(b.cx + b.headRx * 0.3f), Xi(b.headCy - b.headRy * 0.7f), new Color32(80, 58, 38, 150));
                    }
                },
            });

            // 20. 경찰: 정모, 남색 근무복, 가슴 무전기, 반사띠("오늘 거리에서 이상한 사람이 목격되었습니다")
            list.Add(new Spec
            {
                id = "police", label = "경찰", heightM = 1.76f, skin = C("#E0AC8A"),
                hair = Hair.Short, hairCol = C("#1E1A18"), hat = Hat.PoliceCap, hatCol = C("#1E2A44"), mouth = Mouth.Flat, brow = Brow.Flat,
                top = Top.Police, topCol = C("#2A3654"), bottom = Bottom.Slacks, bottomCol = C("#1E2638"),
                shoes = Shoes.Dress, shoeCol = C("#121214"),
                regions = (f, b, back) =>
                {
                    if (back) return;
                    int radio = f.Region(Mat.Shiny);
                    f.Rows(radio, b.sh - 16f, b.sh - 8f, y => Span(b.cx - b.chestHalf * 0.5f, 2.4f), Solid(C("#18181C")));
                },
                overlay = (f, b, back) =>
                {
                    // 반사띠(형광 연두)와 무전기 안테나
                    Color32 neon = C("#C8E85A");
                    for (int x = Xi(b.cx - b.TorsoHalf(b.waistY + 6f)) + 1; x < Xi(b.cx + b.TorsoHalf(b.waistY + 6f)) - 1; x++) f.On(x, Xi(b.waistY + 6f), neon);
                    if (!back) f.VLine(Xi(b.cx - b.chestHalf * 0.5f) + 1, Xi(b.sh - 8f), Xi(b.sh - 4f), C("#18181C"));
                },
            });

            // 21. 시장(가상의 정치인) + 동행한 여성: 회색 올백에 정치인 미소·빨간 넥타이·배지 / 빨간 드레스
            list.Add(new Spec
            {
                id = "mayor", label = "시장님 일행", heightM = 1.72f, build = Build.Chubby, skin = C("#DCA483"),
                hair = Hair.Slick, hairCol = C("#8A8A86"), face = Face.Wrinkles, mouth = Mouth.Grin,
                top = Top.Suit, topCol = C("#22242A"), topCol2 = C("#F0F0EE"), topCol3 = C("#A8283A"),
                bottom = Bottom.Slacks, bottomCol = C("#1E2026"), shoes = Shoes.Dress, shoeCol = C("#121214"),
                overlay = (f, b, back) =>
                {
                    if (back) return;
                    f.Px(Xi(b.cx - b.chestHalf * 0.55f), Xi(b.sh - 7f), C("#E2C25A")); f.Px(Xi(b.cx - b.chestHalf * 0.55f) + 1, Xi(b.sh - 7f), C("#C9A43A"));
                },
                partner = new Spec
                {
                    id = "mayor_w", heightM = 1.68f, build = Build.Female, skin = C("#EBC3A8"),
                    hair = Hair.Long, hairCol = C("#2A1A16"), face = Face.Lipstick | Face.Eyeshadow, mouth = Mouth.Smile,
                    top = Top.Gown, topCol = C("#9A1E2E"), bottom = Bottom.Covered, shoes = Shoes.Heels, shoeCol = C("#9A1E2E"),
                    overlay = (f, b, back) =>
                    {
                        if (back) return;
                        for (int i = -3; i <= 3; i++) f.On(Xi(b.cx) + i, Xi(b.sh - 3f) - (3 - Mathf.Abs(i)) / 2, C("#F2EEE2"));
                    },
                },
            });

            // 22. 코스프레 남자(창작 아이돌 의상): 분홍 쌍갈래 가발, 프릴 옷, 줄무늬 니삭스, 수염 자국
            list.Add(new Spec
            {
                id = "cosplay", label = "코스프레 남자", heightM = 1.78f, skin = C("#E0AC8A"),
                hair = Hair.TwinTails, hairCol = C("#E58CB4"), face = Face.Stubble | Face.Blush, mouth = Mouth.Smile, eyes = Eyes.Wide,
                iris = C("#3A5A8A"),
                top = Top.Frilly, topCol = C("#F2A6C4"), topCol3 = C("#D8436E"),
                bottom = Bottom.Skirt, bottomCol = C("#F2A6C4"), shoes = Shoes.Dress, shoeCol = C("#3A2A3A"),
                overlay = (f, b, back) =>
                {
                    // 줄무늬 니삭스
                    for (int y = Xi(b.ankleY) + 1; y < Xi(b.kneeY + 4f); y++)
                        for (int x = Xi(b.cx - b.hipHalf - 4f); x < Xi(b.cx + b.hipHalf + 4f); x++)
                        {
                            int rgn = f.RegAt(x, y);
                            if (rgn > 0 && f.Get(x, y).r > 150) f.Px(x, y, y % 4 < 2 ? C("#F6F2F4") : C("#E86A9A"));
                        }
                    // 머리끈
                    if (!back)
                    {
                        f.Px(Xi(b.cx - b.headRx - 2f), Xi(b.headCy + b.headRy * 0.45f), C("#FFFFFF"));
                        f.Px(Xi(b.cx + b.headRx + 1f), Xi(b.headCy + b.headRy * 0.45f), C("#FFFFFF"));
                    }
                },
            });

            // 23. 술을 사려는 고등학생: 단정한 긴 머리, 교복(남색 재킷, 빨간 리본, 체크 치마), 남색 니삭스, 불안한 눈썹
            list.Add(new Spec
            {
                id = "student", label = "고등학생", heightM = 1.62f, build = Build.Girl, skin = C("#F0C8AE"),
                hair = Hair.Long, hairCol = C("#1E1A1C"), brow = Brow.Worried, mouth = Mouth.Flat, face = Face.Blush,
                top = Top.Blazer, topCol = C("#26304E"), topCol2 = C("#F2F2EE"), topCol3 = C("#B8323A"),
                bottom = Bottom.Skirt, bottomPaint = Plaid, bottomCol = C("#2C3550"), shoes = Shoes.Dress, shoeCol = C("#2A2220"),
                overlay = (f, b, back) =>
                {
                    for (int y = Xi(b.ankleY) + 1; y < Xi(b.kneeY - 1f); y++)
                        for (int x = Xi(b.cx - b.hipHalf - 2f); x < Xi(b.cx + b.hipHalf + 2f); x++)
                            if (f.RegAt(x, y) > 0 && f.Get(x, y).r > 150) f.Px(x, y, C("#26304E"));
                },
            });

            return list;
        }

        delegate void Overlay(Fig f, Body b, bool back);
    }
}
