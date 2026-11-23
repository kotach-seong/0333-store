using UnityEngine;

namespace Night0333
{
    /// <summary>스프라이트 몇 장을 일정한 빠르기로 돌려 보여 준다(숨 쉬듯 부푸는 캔, 연기 나는 소주 등).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        public Sprite[] frames = new Sprite[0];
        [Tooltip("초당 장면 수")]
        public float fps = 5f;
        [Tooltip("시작 장면을 랜덤으로 해서 여러 개가 똑같이 움직이지 않게")]
        public bool randomStart = true;

        SpriteRenderer sr;
        float t;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (randomStart && frames.Length > 0) t = Random.value * frames.Length / Mathf.Max(0.01f, fps);
        }

        void Update()
        {
            if (frames.Length == 0) return;
            t += Time.deltaTime;
            int i = (int)(t * fps) % frames.Length;
            if (sr.sprite != frames[i]) sr.sprite = frames[i];
        }
    }
}
