using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 이상한 손님을 "예"로 통과시켰을 때: 손님의 눈과 입이 소름 끼치게 찢어지며 웃는다.
    ///  - 손님 그림에서 살색 덩어리를 찾아 얼굴 자리를 정한다(커플·시장님 일행은 두 얼굴). 살색이 없으면(얼굴 없는 손님) 머리 쪽 가운데.
    ///  - 손님 그림 위에 같은 크기의 덧그림을 붙여(손님 그림의 자식) 가라앉으며 나갈 때도 같이 움직인다.
    /// </summary>
    public class CreepyGrin : MonoBehaviour
    {
        public float tearTime = 0.8f;
        public AudioSource sfx;
        [Tooltip("가게 형광등: 변신할 때 미친 듯이 깜빡인다(없으면 손님 그림만 깜빡)")]
        public StoreLights lights;
        [Tooltip("불이 꺼진 동안 변한 손님이 보이는 밝기(0 = 거의 안 보임, 1 = 평소)")]
        [Range(0f, 1f)] public float darkGlow = 0.4f;

        /// <summary>변신하면서 가게 불을 꺼 둔 채 끝났는지(손님이 나간 뒤 흔적을 만들고 Relight로 다시 켜야 한다).</summary>
        public bool LeftDark { get; set; }

        SpriteRenderer overlay;
        PixelTex pt;
        Sprite forSprite;
        int party = 1;

        static readonly Dictionary<Sprite, List<RectInt>> faceCache = new Dictionary<Sprite, List<RectInt>>();

        static readonly Color32 Hole = new Color32(10, 6, 8, 255);
        static readonly Color32 Gum = new Color32(46, 6, 10, 255);
        static readonly Color32 Tooth = new Color32(232, 226, 204, 255);
        static readonly Color32 ToothShade = new Color32(176, 166, 140, 255);
        static readonly Color32 Torn = new Color32(128, 12, 16, 255);
        static readonly Color32 Blood = new Color32(160, 18, 22, 255);
        static readonly Color32 Pupil = new Color32(255, 244, 226, 255);
        static readonly Color32 PupilRed = new Color32(232, 40, 40, 255);

        CustomerView swapped;   // 변신 그림으로 바꿔 둔 손님(Hide에서 되돌린다)
        Sprite original;
        Coroutine looping;      // 움직이는 변신(촉수가 꿈틀댐)

        static bool HasTransforms(CustomerView who) =>
            who != null && who.counterBody != null && who.transforms != null && who.transforms.Length > 0 && who.transforms[0] != null;

        /// <summary>
        /// 변신 연출. 손님에게 변신 그림(PixelLab으로 그린 손님별 2가지)이 있으면 그중 하나로:
        /// 평소 모습과 변신 모습이 점점 빠르게 번갈아 깜빡이다가 변신 모습으로 멈춘다(움직이는 변신이면 계속 꿈틀댄다).
        /// 변신 그림이 없으면 예전처럼 눈과 입이 찢어지는 덧그림. 끝난 모습은 Hide로 되돌린다.
        /// </summary>
        public IEnumerator Play(CustomerView who)
        {
            Hide();
            if (HasTransforms(who))
            {
                var pick = new List<int>();
                for (int i = 0; i < who.transforms.Length; i++) if (who.transforms[i] != null) pick.Add(i);
                yield return Turn(who, pick[Random.Range(0, pick.Count)]);
                yield break;
            }
            if (!Attach(who)) yield break;
            SoundFx.Play(sfx, SoundFx.Squish, 0.8f);
            bool tore = false;
            for (float t = 0f; t < tearTime; t += Time.deltaTime)
            {
                float k = t / tearTime;
                Draw(1f - (1f - k) * (1f - k));
                if (!tore && k > 0.45f) { tore = true; SoundFx.Play(sfx, SoundFx.Tear, 1f); }
                yield return null;
            }
            Draw(1f);
        }

        IEnumerator Turn(CustomerView who, int index)
        {
            SpriteRenderer body = who.counterBody;
            Sprite turn = who.transforms[index];
            swapped = who;
            original = body.sprite;
            if (lights != null)
            {
                // 형광등이 미친 듯이 깜빡이는 사이에 변한다. 손님도 가게 밝기만큼 어두워진다(불이 꺼지면 실루엣)
                bool wasTurned = false;
                // 깜빡임 줄이기: 평소/변신을 번갈아 보이지 않고, 손님 밝기는 천천히 바뀌는 가게 불을 매 프레임 따라간다(다 꺼진 뒤 변한다)
                bool calm = GameSettings.ReduceFlicker;
                Coroutine follow = calm ? StartCoroutine(FollowLight(body)) : null;
                yield return lights.Possess((b, on) =>
                {
                    if (!calm)
                    {
                        body.sprite = on ? turn : original;
                        body.color = Color.Lerp(lights.darkTint, Color.white, b);
                    }
                    if (on && !wasTurned && !calm) SoundFx.Play(sfx, SoundFx.Squish, 0.35f);
                    wasTurned = on;
                });
                if (follow != null) StopCoroutine(follow);
                // 꺼진 채로 둔다: 어둠 속에서 변한 모습이 희미하게 보이며 말하고 나간다(CheckoutSystem이 흔적을 만들고 다시 켠다)
                body.color = Color.Lerp(lights.darkTint, Color.white, darkGlow);
                LeftDark = true;
            }
            else
            {
                SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.7f);
                // 지지직: 평소 ↔ 변신이 점점 빠르게 번갈아
                float[] steps = GameSettings.ReduceFlicker ? new float[0] : new[] { 0.14f, 0.09f, 0.12f, 0.06f, 0.09f, 0.045f, 0.07f, 0.035f, 0.05f };
                bool on = false;
                foreach (float s in steps)
                {
                    on = !on;
                    body.sprite = on ? turn : original;
                    if (on) SoundFx.Play(sfx, SoundFx.Squish, 0.35f);
                    yield return new WaitForSeconds(s);
                }
            }
            body.sprite = turn;
            SoundFx.Play(sfx, SoundFx.Tear, 1f);
            CustomerView.SpriteLoop loop = who.transformLoops != null && index < who.transformLoops.Length ? who.transformLoops[index] : null;
            if (loop != null && loop.frames != null && loop.frames.Length > 1) looping = StartCoroutine(Loop(body, loop));
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>깜빡임 줄이기: 손님 밝기가 가게 불(서서히 바뀐다)을 매 프레임 따라간다.</summary>
        IEnumerator FollowLight(SpriteRenderer body)
        {
            while (body != null && lights != null)
            {
                body.color = Color.Lerp(lights.darkTint, Color.white, lights.Brightness);
                yield return null;
            }
        }

        /// <summary>변신한 채로 프레임을 계속 돌린다(Hide가 멈춘다).</summary>
        IEnumerator Loop(SpriteRenderer body, CustomerView.SpriteLoop loop)
        {
            float wait = 1f / Mathf.Max(1f, loop.fps);
            for (int i = 0; ; i = (i + 1) % loop.frames.Length)
            {
                if (body == null) yield break;
                if (loop.frames[i] != null) body.sprite = loop.frames[i];
                yield return new WaitForSeconds(wait);
            }
        }

        /// <summary>미리보기: 찢어진 정도 t(0~1)로 바로 그린다. 변신 그림이 있으면 variant번째(음수면 첫 번째)로 바꿔 둔다.</summary>
        public void Show(CustomerView who, float t) => Show(who, t, -1);

        public void Show(CustomerView who, float t, int variant)
        {
            Hide();
            if (HasTransforms(who))
            {
                if (t < 0.5f) return;
                int i = Mathf.Clamp(variant, 0, who.transforms.Length - 1);
                swapped = who;
                original = who.counterBody.sprite;
                who.counterBody.sprite = who.transforms[i];
                return;
            }
            if (Attach(who)) Draw(t);
        }

        /// <summary>미리보기: 움직이는 변신의 frame번째 그림으로 바꿔 둔다(없으면 false).</summary>
        public bool ShowLoopFrame(CustomerView who, int variant, int frame)
        {
            Hide();
            if (!HasTransforms(who) || who.transformLoops == null || variant >= who.transformLoops.Length) return false;
            var loop = who.transformLoops[variant];
            if (loop == null || loop.frames == null || frame >= loop.frames.Length) return false;
            swapped = who;
            original = who.counterBody.sprite;
            who.counterBody.sprite = loop.frames[frame];
            return true;
        }

        public void Hide()
        {
            if (looping != null) { StopCoroutine(looping); looping = null; }
            if (overlay != null) overlay.enabled = false;
            if (swapped != null && swapped.counterBody != null) swapped.counterBody.color = Color.white;
            if (swapped != null && swapped.counterBody != null && original != null) swapped.counterBody.sprite = original;
            swapped = null;
            original = null;
        }

        bool Attach(CustomerView who)
        {
            SpriteRenderer body = who != null ? who.counterBody : null;
            if (body == null || body.sprite == null) return false;
            if (overlay == null)
            {
                var go = new GameObject("Creepy Grin");
                go.hideFlags = HideFlags.DontSave;
                overlay = go.AddComponent<SpriteRenderer>();
            }
            overlay.transform.SetParent(body.transform, false);
            overlay.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            overlay.transform.localRotation = Quaternion.identity;
            float ppu = body.sprite.pixelsPerUnit;
            overlay.transform.localScale = new Vector3(1f / ppu, 1f / ppu, 1f);
            overlay.sharedMaterial = body.sharedMaterial;
            overlay.sortingLayerID = body.sortingLayerID;
            overlay.sortingOrder = body.sortingOrder + 1;
            overlay.flipX = body.flipX;
            forSprite = body.sprite;
            party = Mathf.Max(1, who.partySize);
            Rect r = forSprite.rect;
            if (pt == null || pt.W != (int)r.width || pt.H != (int)r.height) pt = new PixelTex((int)r.width, (int)r.height);
            overlay.enabled = true;
            return true;
        }

        void Draw(float t)
        {
            pt.Clear();
            foreach (RectInt f in Faces(forSprite, party)) DrawFace(f, t);
            Vector2 pivot = new Vector2(forSprite.pivot.x / forSprite.rect.width, forSprite.pivot.y / forSprite.rect.height);
            overlay.sprite = pt.ToSprite(pivot);
        }

        /// <summary>얼굴 하나: 위로 찢어진 검은 눈 + 귀까지 찢어진 웃는 입.</summary>
        void DrawFace(RectInt f, float t)
        {
            float w = f.width, h = f.height;
            float cx = f.x + w * 0.5f;
            // 눈: 얼굴 위에서 42% 높이, 바깥쪽이 치켜 올라간 검은 구멍과 작은 눈동자
            float eyeY = f.y + h * 0.58f;
            foreach (float side in new[] { -1f, 1f })
            {
                float ex = cx + side * w * 0.21f;
                float rx = Mathf.Max(1.6f, w * 0.15f) * (1f + 0.3f * t);
                float ry = 0.9f + 2.6f * t;
                for (int y = Mathf.FloorToInt(eyeY - ry - 3); y <= Mathf.CeilToInt(eyeY + ry + 3); y++)
                    for (int x = Mathf.FloorToInt(ex - rx - 1); x <= Mathf.CeilToInt(ex + rx + 1); x++)
                    {
                        float dx = (x + 0.5f - ex) / rx;
                        float tilt = side * dx * 0.9f * t;                // 바깥쪽 끝이 위로
                        float dy = (y + 0.5f - eyeY - tilt) / ry;
                        if (dx * dx + dy * dy <= 1f) pt.Put(x, y, Hole);
                    }
                Color32 pupil = t > 0.5f ? PupilRed : Pupil;
                pt.Put(Mathf.RoundToInt(ex - 0.5f) + (side < 0 ? 1 : 0), Mathf.RoundToInt(eyeY - 0.5f), pupil);
            }
            // 입: 얼굴 아래쪽, 귀를 넘어 찢어지는 초승달 웃음. 이가 빼곡히 드러난다
            float mouthY = f.y + h * 0.22f;
            float half = w * (0.22f + 0.48f * t);                       // t=1이면 얼굴 너비의 1.4배
            float lift = h * 0.34f * t;                                // 입꼬리가 올라가는 높이
            float open = 1f + 2.4f * t;                                // 가운데 벌어진 정도(이 두 줄 + 틈)
            int x0 = Mathf.FloorToInt(cx - half), x1 = Mathf.CeilToInt(cx + half);
            for (int x = x0; x <= x1; x++)
            {
                float u = (x + 0.5f - cx) / half;                        // -1 ~ 1
                if (Mathf.Abs(u) > 1f) continue;
                float top = mouthY + lift * u * u * Mathf.Abs(u);        // 윗입술: 양끝이 확 올라간다
                int thick = Mathf.Max(1, Mathf.RoundToInt(open * (1f - u * u * 0.85f)));
                int yTop = Mathf.RoundToInt(top), yBot = yTop - thick;
                bool gap = (x - x0) % 2 == 1 && thick >= 2;
                for (int y = yBot; y <= yTop; y++)
                {
                    int fromTop = yTop - y;
                    Color32 c = Gum;
                    if (thick >= 2)
                    {
                        if (fromTop == 0) c = gap ? Hole : Tooth;                         // 윗니
                        else if (fromTop == 1 && thick >= 3) c = gap ? Hole : ToothShade;
                        else if (y == yBot && thick >= 3) c = (x - x0) % 2 == 0 ? Hole : ToothShade;   // 아랫니
                    }
                    pt.Put(x, y, c);
                }
                pt.Put(x, yTop + 1, Hole);                               // 윗입술 선
                pt.Put(x, yBot - 1, Hole);                               // 아랫입술 선
            }
            // 찢어진 자국: 입꼬리에서 귀 쪽으로 갈라진 금 + 핏방울
            if (t > 0.35f)
            {
                float k = (t - 0.35f) / 0.65f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float sx = cx + side * half, sy = mouthY + lift;
                    int len = Mathf.RoundToInt(h * 0.18f * k) + 1;
                    for (int i = 0; i < len; i++)
                        pt.Put(Mathf.RoundToInt(sx + side * (i * 0.7f)), Mathf.RoundToInt(sy + i * 0.9f), Torn);
                    int drip = Mathf.RoundToInt(2f + 3f * k);
                    for (int i = 1; i <= drip; i++) pt.Put(Mathf.RoundToInt(sx), Mathf.RoundToInt(sy - i - 1), GameSettings.NoBlood ? Hole : Blood);
                }
            }
        }

        // ── 얼굴 찾기 ──
        /// <summary>그림 속 얼굴 자리(그림 픽셀 좌표, 왼쪽 아래 원점). 결과는 그림마다 한 번만 계산한다.</summary>
        public static List<RectInt> Faces(Sprite s, int party = 1)
        {
            if (faceCache.TryGetValue(s, out var cached)) return cached;
            var faces = FindFaces(s, party);
            faceCache[s] = faces;
            return faces;
        }

        /// <summary>
        /// 살색: 아주 어두운 머리카락과 무채색 옷(베이지·회색 셔츠)은 빼고, 그늘진 얼굴·붉게 달아오른 얼굴은 넣는다.
        /// </summary>
        static bool Skin(Color32 c)
        {
            if (c.a <= 200 || c.r < 95 || c.g < 45 || c.b < 28) return false;
            if (c.r <= c.g || c.g < c.b - 6) return false;
            float sat = (c.r - Mathf.Min(c.g, c.b)) / (float)c.r;
            return sat >= 0.2f && c.r - c.b <= 170 && c.r - c.g <= 120;
        }

        /// <summary>
        /// 얼굴 찾기: 사람마다(일행이면 왼쪽·오른쪽 절반) 머리 꼭대기의 가운데를 잡고, 그 아래 머리 띠(키의 24%) 안에서
        /// 머리 가운데 근처의 살색이 처음 나오는 줄부터 얼굴 높이(키의 13%)만큼의 살색을 모아 얼굴 칸으로 삼는다.
        /// 셔츠·손·들어 올린 물건은 머리 가운데에서 멀거나 아래쪽이라 빠진다. 살색이 없으면(검은 얼굴·가면) 머리 가운데.
        /// </summary>
        static List<RectInt> FindFaces(Sprite s, int party)
        {
            var result = new List<RectInt>();
            Texture2D tex = s.texture;
            Rect r = s.textureRect;
            int ox = (int)r.x, oy = (int)r.y, w = (int)r.width, h = (int)r.height;
            if (tex == null || !tex.isReadable) { result.Add(new RectInt(w / 2 - 8, h - 30, 16, 18)); return result; }
            Color32[] all = tex.GetPixels32();
            Color32 At(int x, int y) => all[(oy + y) * tex.width + ox + x];

            // 그려진 범위
            int top = -1, bottom = h, left = w, right = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (At(x, y).a > 40) { top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y); left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            if (top < 0) { result.Add(new RectInt(w / 2 - 8, h - 30, 16, 18)); return result; }
            int tall = top - bottom + 1;
            int bandRows = Mathf.RoundToInt(tall * 0.24f), faceRows = Mathf.Max(8, Mathf.RoundToInt(tall * 0.13f));
            int capRows = Mathf.Max(3, Mathf.RoundToInt(tall * 0.06f)), half = Mathf.Max(8, Mathf.RoundToInt(tall * 0.12f));
            int midX = (left + right) / 2;
            int parts = party <= 1 ? 1 : 2;

            for (int p = 0; p < parts; p++)
            {
                int x0 = parts == 1 || p == 0 ? left : midX + 1;
                int x1 = parts == 1 || p == 1 ? right : midX;
                // 이 사람의 머리 꼭대기(일행은 키가 다르다)
                int ptop = -1;
                for (int y = top; y >= bottom && ptop < 0; y--)
                    for (int x = x0; x <= x1; x++)
                        if (At(x, y).a > 40) { ptop = y; break; }
                if (ptop < 0) continue;
                // 머리 가운데: 맨 위 몇 줄(머리카락·모자)의 가운데
                long sum = 0; int n = 0;
                for (int y = ptop; y > ptop - capRows && y >= bottom; y--)
                    for (int x = x0; x <= x1; x++)
                        if (At(x, y).a > 40) { sum += x; n++; }
                int hcx = n > 0 ? (int)(sum / n) : (x0 + x1) / 2;
                int ax0 = Mathf.Max(x0, hcx - half), ax1 = Mathf.Min(x1, hcx + half);
                int floor = Mathf.Max(bottom, ptop - bandRows);

                // 살색이 두 칸 이상 나오는 첫 줄
                int y0 = -1;
                for (int y = ptop; y >= floor && y0 < 0; y--)
                {
                    int cnt = 0;
                    for (int x = ax0; x <= ax1; x++) if (Skin(At(x, y))) cnt++;
                    if (cnt >= 2) y0 = y;
                }
                bool found = false;
                if (y0 >= 0)
                {
                    int bx0 = int.MaxValue, bx1 = -1, by0 = int.MaxValue, by1 = -1, cnt = 0;
                    for (int y = y0; y > y0 - faceRows && y >= floor; y--)
                        for (int x = ax0; x <= ax1; x++)
                            if (Skin(At(x, y)))
                            {
                                bx0 = Mathf.Min(bx0, x); bx1 = Mathf.Max(bx1, x); by0 = Mathf.Min(by0, y); by1 = Mathf.Max(by1, y); cnt++;
                            }
                    if (cnt >= 10)
                    {
                        result.Add(Shape(new RectInt(bx0, by0, bx1 - bx0 + 1, by1 - by0 + 1)));
                        found = true;
                    }
                }
                if (!found)
                {
                    // 얼굴이 안 보이는 손님(검은 형체·가면·그늘): 머리 가운데
                    int fw = Mathf.Max(12, Mathf.RoundToInt(tall * 0.14f)), fh = Mathf.Max(12, Mathf.RoundToInt(tall * 0.13f));
                    result.Add(new RectInt(hcx - fw / 2, ptop - Mathf.RoundToInt(tall * 0.06f) - fh, fw, fh));
                }
            }
            if (result.Count == 0)
            {
                int fw = Mathf.Max(12, Mathf.RoundToInt(tall * 0.14f)), fh = Mathf.Max(12, Mathf.RoundToInt(tall * 0.13f));
                result.Add(new RectInt(midX - fw / 2, top - Mathf.RoundToInt(tall * 0.06f) - fh, fw, fh));
            }
            return result;
        }

        /// <summary>살색 덩어리를 얼굴 칸으로: 너무 납작하면(앞머리·선글라스로 가려짐) 아래로 늘린다.</summary>
        static RectInt Shape(RectInt b)
        {
            int hgt = Mathf.Max(b.height, Mathf.RoundToInt(b.width * 1.05f));
            return new RectInt(b.x, b.y + b.height - hgt, b.width, hgt);
        }
    }
}
