using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 언어 바꾸기에 쓰는 파일들(씬을 만들 때 채워진다): 글자표, 언어별 대사 파일, 중국어 글꼴.
    /// 게임을 켜면 저장된 언어로 글꼴을 맞춘다. 다른 것보다 먼저 깨어난다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LocAssets : MonoBehaviour
    {
        static LocAssets instance;

        [Tooltip("Data/Lang/Strings.txt: ko·en·zh 세 줄 묶음")]
        public TextAsset strings;
        [Tooltip("언어별 대사 파일(이름.en.txt / 이름.zh.txt)")]
        public TextAsset[] files = new TextAsset[0];
        [Tooltip("갈무리(한국어·영어)")]
        public Font galmuri11, galmuri9;
        [Tooltip("퓨전 픽셀 간체(중국어): 12px ↔ 갈무리11, 10px ↔ 갈무리9")]
        public Font zh12, zh10;

        public static LocAssets Find()
        {
            if (instance == null) instance = FindFirstObjectByType<LocAssets>(FindObjectsInactive.Include);
            return instance;
        }

        void Awake()
        {
            instance = this;
            Loc.Reload();
            Loc.ApplyFonts();
        }

        void Start() => Loc.ApplyFonts();   // 다른 것들이 Awake에서 만든 글자까지

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
