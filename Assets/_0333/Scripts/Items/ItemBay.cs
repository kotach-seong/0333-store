using System;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 계산대 물품 칸. 손님이 산 물건을 칸 위에 흩뿌리듯 내려놓는다.
    ///  - 한 번에 1~8개, 같은 물건은 최대 3개(정상·이상 합쳐서).
    ///  - 정해진 자리 없이 칸 안 아무 곳에나 놓되, 물건끼리 너무 붙지는 않게 한다.
    ///  - 키 큰 물건(병·캔·컵라면)은 뒤쪽, 납작한 물건은 앞쪽으로 쏠린다(절대 규칙은 아님).
    ///  - 같은 물건이 똑같아 보이지 않게 랜덤으로 좌우를 뒤집는다.
    ///  - 뒤쪽 물건을 먼저 그려서 앞 물건이 자연스럽게 가린다.
    /// 지금은 손님 기능이 없어서, 게임을 시작하면 랜덤으로 한 번 채우고 R 키로 다시 섞어 볼 수 있다(시험용).
    /// </summary>
    public class ItemBay : MonoBehaviour
    {
        /// <summary>이상현상 모습 하나. 그림이 여러 장이면 움직이는 그림으로 돌려 보여 준다.</summary>
        [Serializable]
        public class AnomalyLook
        {
            public Sprite[] frames = new Sprite[0];
            public float fps = 5f;
        }

        [Serializable]
        public class ItemDef
        {
            public string id;
            public string displayName;
            public Sprite normal;
            [Tooltip("이상현상 모습들. 나올 때 이 중 하나가 랜덤으로 골라진다")]
            public AnomalyLook[] anomalies = new AnomalyLook[0];
            [Tooltip("병·캔·컵라면처럼 키 큰 물건(뒤쪽에 놓일 확률이 높다)")]
            public bool tall;
        }

        /// <summary>물품 칸에 놓인 물건 하나.</summary>
        public struct Placed
        {
            public string id;
            public bool anomaly;
            public SpriteRenderer renderer;
        }

        public ItemDef[] items = new ItemDef[0];

        /// <summary>3:33(없는 상품) 전용: 에러 상품·새까만 상품. 평소 손님 물건으로는 나오지 않는다.</summary>
        public const string ErrorId = "err333", BlackId = "black333";
        public Material material;
        [Tooltip("계산대(50)와 모니터 화면(51) 위에 그린다")]
        public int baseSortingOrder = 52;

        [Header("물품 칸 안쪽(미터): x는 좌우, z는 앞뒤 거리")]
        public Vector2 xRange = new Vector2(-0.40f, 0.20f);
        public Vector2 zRange = new Vector2(1.40f, 1.76f);
        [Tooltip("물건끼리 이만큼은 떨어진다(미터)")]
        public float minGapX = 0.085f, minGapZ = 0.11f;

        [Header("화면 투영(매장 그림과 같은 값)")]
        public float focal = 333f;
        public float vanishY = 6f;
        public float eyeHeight = 1.6f;
        public float surfaceHeight = 0.928f;

        [Header("시험용")]
        public bool fillOnStart = true;
        public bool rerollWithR = true;
        [Range(0f, 1f)] public float testAnomalyChance = 0f;

        readonly List<Placed> placed = new List<Placed>();
        public IReadOnlyList<Placed> Current => placed;

        void Start()
        {
            if (fillOnStart) PlaceRandom(Environment.TickCount, testAnomalyChance);
        }

        void Update()
        {
            if (rerollWithR && Application.isEditor && GameInput.RerollDown) PlaceRandom(Environment.TickCount, testAnomalyChance);   // 시험용(에디터에서만)
        }

        /// <summary>손님이 살 물건을 랜덤으로 고른다(1~8개, 같은 물건 최대 3개).</summary>
        public List<(string id, bool anomaly)> PickRandom(System.Random rnd, int minCount = 1, int maxCount = 8, float anomalyChance = 0f)
        {
            var bag = new List<(string id, bool anomaly)>();
            if (items.Length == 0) return bag;
            int count = rnd.Next(minCount, maxCount + 1);
            var perItem = new Dictionary<string, int>();
            int guard = 0;
            while (bag.Count < count && guard++ < 200)
            {
                ItemDef def = items[rnd.Next(items.Length)];
                if (def.id == ErrorId || def.id == BlackId) continue;   // 3:33 전용 상품은 평소엔 안 나온다
                perItem.TryGetValue(def.id, out int n);
                if (n >= 3) continue;
                perItem[def.id] = n + 1;
                bool anomaly = def.anomalies != null && def.anomalies.Length > 0 && rnd.NextDouble() < anomalyChance;
                bag.Add((def.id, anomaly));
            }
            return bag;
        }

        public void PlaceRandom(int seed, float anomalyChance = 0f)
        {
            var rnd = new System.Random(seed);
            Place(PickRandom(rnd, 1, 8, anomalyChance), rnd);
        }

        /// <summary>정해진 물건들을 물품 칸에 흩뿌려 놓는다. 같은 seed면 항상 같은 배치가 나온다(세이브용).</summary>
        public void Place(IList<(string id, bool anomaly)> bag, System.Random rnd)
        {
            Clear();
            var spots = new List<(ItemDef def, bool anomaly, float x, float z)>();
            foreach (var entry in bag)
            {
                ItemDef def = Find(entry.id);
                if (def == null) continue;
                for (int tryCount = 0; tryCount < 300; tryCount++)
                {
                    float x = Mathf.Lerp(xRange.x, xRange.y, (float)rnd.NextDouble());
                    float t = (float)rnd.NextDouble();
                    t = def.tall ? Mathf.Sqrt(t) : 1f - Mathf.Sqrt(t); // 키 큰 건 뒤로, 납작한 건 앞으로
                    float z = Mathf.Lerp(zRange.x, zRange.y, t);
                    bool ok = true;
                    foreach (var s in spots)
                        if (Mathf.Abs(s.x - x) < minGapX && Mathf.Abs(s.z - z) < minGapZ) { ok = false; break; }
                    if (!ok) continue;
                    spots.Add((def, entry.anomaly, x, z));
                    break;
                }
            }

            spots.Sort((a, b) => b.z.CompareTo(a.z)); // 먼 것부터
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                AnomalyLook look = s.anomaly ? s.def.anomalies[rnd.Next(s.def.anomalies.Length)] : null;
                Sprite sprite = look != null && look.frames.Length > 0 ? look.frames[0] : s.def.normal;
                if (sprite == null) continue;
                var go = new GameObject(s.def.id + (s.anomaly ? " (이상)" : ""));
                go.transform.SetParent(transform, false);
                // 그림의 기준점이 물건 바닥이라, 화면에서 물품 칸 표면 위치에 그대로 세우면 된다
                float sx = focal * s.x / s.z;
                float sy = vanishY + focal * (surfaceHeight - eyeHeight) / s.z;
                go.transform.position = new Vector3(Mathf.Round(sx), Mathf.Round(sy), 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = baseSortingOrder + i;
                sr.flipX = rnd.Next(2) == 1;
                if (look != null && look.frames.Length > 1)
                {
                    var anim = go.AddComponent<SpriteFrameAnimator>();
                    anim.frames = look.frames;
                    anim.fps = look.fps;
                }
                placed.Add(new Placed { id = s.def.id, anomaly = s.anomaly, renderer = sr });
            }
        }

        public void Clear()
        {
            foreach (var p in placed)
                if (p.renderer != null)
                {
                    if (Application.isPlaying) Destroy(p.renderer.gameObject);
                    else DestroyImmediate(p.renderer.gameObject);
                }
            placed.Clear();
        }

        /// <summary>
        /// 화면의 한 점(월드 픽셀)에 있는 물건. 위에 그려진 물건부터 보고, 그림의 투명한 곳은 지나친다.
        /// 없으면 -1.
        /// </summary>
        public int HitTest(Vector2 world)
        {
            int best = -1, bestOrder = int.MinValue;
            for (int i = 0; i < placed.Count; i++)
            {
                SpriteRenderer sr = placed[i].renderer;
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                if (sr.sortingOrder <= bestOrder || !sr.bounds.Contains(new Vector3(world.x, world.y, sr.bounds.center.z))) continue;
                if (!OpaqueAt(sr, world)) continue;
                best = i;
                bestOrder = sr.sortingOrder;
            }
            return best;
        }

        static bool OpaqueAt(SpriteRenderer sr, Vector2 world)
        {
            Sprite s = sr.sprite;
            Texture2D t = s.texture;
            if (t == null || !t.isReadable) return true; // 그림을 읽을 수 없으면 네모 영역으로만 판단
            Vector2 local = world - (Vector2)sr.transform.position;
            local = new Vector2(local.x / sr.transform.lossyScale.x, local.y / sr.transform.lossyScale.y);
            if (sr.flipX) local.x = -local.x;
            Bounds b = s.bounds;
            Rect r = s.textureRect;
            int px = Mathf.FloorToInt(r.x + (local.x - b.min.x) * r.width / b.size.x);
            int py = Mathf.FloorToInt(r.y + (local.y - b.min.y) * r.height / b.size.y);
            if (px < r.xMin || py < r.yMin || px >= r.xMax || py >= r.yMax) return false;
            // 가장자리 1픽셀 차이로 손이 미끄러지지 않게 주변도 조금 본다
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = px + dx, y = py + dy;
                    if (x < r.xMin || y < r.yMin || x >= r.xMax || y >= r.yMax) continue;
                    if (t.GetPixel(x, y).a > 0.3f) return true;
                }
            return false;
        }

        /// <summary>물건 하나를 칸에서 치운다(스캔해서 사라질 때).</summary>
        public void Remove(SpriteRenderer renderer)
        {
            for (int i = 0; i < placed.Count; i++)
                if (placed[i].renderer == renderer)
                {
                    placed.RemoveAt(i);
                    break;
                }
            if (renderer == null) return;
            if (Application.isPlaying) Destroy(renderer.gameObject);
            else DestroyImmediate(renderer.gameObject);
        }

        public ItemDef Find(string id)
        {
            foreach (var d in items) if (d.id == id) return d;
            return null;
        }
    }
}
