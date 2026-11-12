using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 창밖 빗줄기. 같은 빗줄기 그림 두 장을 위아래로 이어 붙여 아래로 흘려보낸다.
    /// 실내 벽 레이어 뒤에 있으므로 창문과 유리문으로만 보인다.
    /// </summary>
    public class ScrollingRain : MonoBehaviour
    {
        public float speed = 220f;   // 픽셀/초
        public float tileHeight = 186f;

        Vector3 origin;
        float offset;

        void Awake() => origin = transform.localPosition;

        void Update()
        {
            offset = Mathf.Repeat(offset + speed * Time.deltaTime, tileHeight);
            transform.localPosition = origin + new Vector3(0f, -Mathf.Round(offset), 0f);
        }
    }
}
