using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 바코드 스캐너 유리의 빨간 빛. 물건을 찍을 수 있는 동안은 은은하게 켜져 있고, 찍는 순간 번쩍인다.
    /// </summary>
    public class ScannerLight : MonoBehaviour
    {
        public SpriteRenderer target;
        [Tooltip("빛줄기 길이(px). 씬을 만들 때 스캐너 그림에 맞춰 정해진다")]
        public int width = 56;
        public Color32 color = new Color32(255, 70, 52, 255);
        [Range(0f, 1f)] public float idleAlpha = 0.35f;
        public float flashTime = 0.14f;

        const int Height = 9;
        PixelTex pt;
        float flashUntil = -1f;
        bool ready;

        /// <summary>스캔할 수 있는 동안 켜 둔다.</summary>
        public bool Ready
        {
            get => ready;
            set { ready = value; Refresh(); }
        }

        public void Flash()
        {
            flashUntil = Time.time + flashTime;
            Refresh();
        }

        /// <summary>에디터 미리보기용: 번쩍이는 순간으로 둔다.</summary>
        public void PreviewFlash(bool on)
        {
            Build();
            target.enabled = on;
            target.color = Color.white;
        }

        void Awake()
        {
            Build();
            Refresh();
        }

        void Update()
        {
            if (flashUntil >= 0f && Time.time >= flashUntil) { flashUntil = -1f; Refresh(); }
        }

        void Refresh()
        {
            if (target == null) return;
            Build();
            bool flashing = flashUntil >= 0f && Time.time < flashUntil;
            target.enabled = flashing || ready;
            target.color = new Color(1f, 1f, 1f, flashing ? 1f : idleAlpha);
        }

        void Build()
        {
            if (pt != null || target == null) return;
            pt = new PixelTex(width + 6, Height);
            int mid = Height / 2;
            for (int x = 0; x < pt.W; x++)
            {
                // 양 끝으로 갈수록 흐려지는 빛줄기 + 위아래 번짐
                float edge = Mathf.Clamp01(Mathf.Min(x, pt.W - 1 - x) / 4f);
                for (int y = 0; y < Height; y++)
                {
                    int d = Mathf.Abs(y - mid);
                    float a = d == 0 ? 1f : d == 1 ? 0.45f : d == 2 ? 0.18f : 0.07f;
                    Color32 c = d == 0 ? new Color32(255, 214, 196, 255) : color;
                    pt.Put(x, y, PixelTex.Alpha(c, a * edge));
                }
            }
            target.sprite = pt.ToSprite(new Vector2(0f, 0.5f));
        }
    }
}
