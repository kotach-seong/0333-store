using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// CCTV에서만 보이는 것들(내 눈에는 안 보인다). 이상한 손님이 계산대 앞에 서 있는 동안만 켠다.
    ///  - Ghost: 손님 뒤에 붙은 귀신(CCTV 1·2번)
    ///  - Monster: 손님 대신 괴물(CCTV 1·2번, 손님 자신은 CCTV에서 지운다)
    ///  - Watchers: 밖에서 가게 안을 보는 귀신들(CCTV 1번 창 너머, 3번 주차장)
    ///  - Missing: 손님 자신을 CCTV에서 지우기만 한다(CustomerView.cctvHidden)
    /// 그림은 큰 화면·계산대 위 모니터 둘 다에 있고, 지금 보는 채널 것만 보인다(CctvOverlay).
    /// </summary>
    public class CctvAnomalies : MonoBehaviour
    {
        public List<CctvOverlay> ghost = new List<CctvOverlay>();
        public List<CctvOverlay> monster = new List<CctvOverlay>();
        public List<CctvOverlay> watchers = new List<CctvOverlay>();

        public CctvOdd Current { get; private set; }

        /// <summary>손님이 다 올라온 뒤: 이 손님의 CCTV 이상현상을 켠다.</summary>
        public void Show(CctvOdd kind)
        {
            Current = kind;
            Set(ghost, kind == CctvOdd.Ghost);
            Set(monster, kind == CctvOdd.Monster);
            Set(watchers, kind == CctvOdd.Watchers);
        }

        public void Clear() => Show(CctvOdd.None);

        static void Set(List<CctvOverlay> list, bool on)
        {
            foreach (var o in list)
            {
                if (o == null) continue;
                o.present = on;
                o.Refresh(CctvSystem.CurrentChannel);
            }
        }
    }
}
