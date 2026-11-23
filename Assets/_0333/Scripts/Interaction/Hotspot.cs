using UnityEngine;
using UnityEngine.Events;

namespace Night0333
{
    /// <summary>
    /// 클릭할 수 있는 자리(핫스팟). 매장 그림 위의 네모 영역 하나를 뜻한다.
    /// 화면 가운데 조준점이 이 영역 안에 들어오면 테두리와 이름이 뜨고, 왼쪽 클릭하면 onInteract가 실행된다.
    /// </summary>
    public class Hotspot : MonoBehaviour
    {
        [Tooltip("클릭되는 영역(월드 픽셀)")]
        public Rect area;
        [Tooltip("조준했을 때 화면에 뜨는 이름")]
        public string label;
        public UnityEvent onInteract = new UnityEvent();

        public bool Contains(Vector2 worldPoint) => isActiveAndEnabled && area.Contains(worldPoint);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
