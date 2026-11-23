using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// CCTV 영상 위에 덧그리는 그림 하나(예: CCTV에 찍힌 손님).
    /// 자기 채널을 보고 있을 때만 보이고, 주인(손님)이 숨으면 같이 숨는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CctvOverlay : MonoBehaviour
    {
        [Tooltip("이 그림이 찍히는 채널(0부터)")]
        public int channel;
        [Tooltip("주인이 지금 매장에 있는지")]
        public bool present = true;

        public void Refresh(int currentChannel)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = present && channel == currentChannel;
        }
    }
}
