using UnityEngine;
using UnityEngine.Rendering;

namespace Night0333
{
    /// <summary>
    /// 설정의 밝기: 카메라 앞에 화면을 덮는 막 두 장(모든 것 위, 일시정지 메뉴까지).
    ///  - 어둡게(50~90%): 화면 색에 곱한다.
    ///  - 밝게(110~150%): 화면 색을 최대 2배로 곱하고, 어두운 곳을 조금 들어 올린다(어둠 속이 더 잘 보이게).
    /// 선형 색 공간이라 보이는 밝기(감마)에 맞춰 값을 바꾼다. 100%면 둘 다 꺼진다.
    /// </summary>
    public class ScreenBrightness : MonoBehaviour
    {
        public static ScreenBrightness Instance { get; private set; }

        [Tooltip("Night0333/ScreenTint(곱하기·밝히기 막)")]
        public Shader shader;
        public int sortingOrder = 500;

        SpriteRenderer mul, lift;
        Material mulMat, liftMat;
        PixelTex white;

        void Awake()
        {
            Instance = this;
            Show(GameSettings.Brightness);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>지금 설정의 밝기로 다시 맞춘다.</summary>
        public static void Refresh()
        {
            var sb = Instance != null ? Instance : FindFirstObjectByType<ScreenBrightness>();
            if (sb != null) sb.Show(GameSettings.Brightness);
        }

        void Show(int step)
        {
            if (step == 0 || shader == null)
            {
                if (mul != null) mul.enabled = false;
                if (lift != null) lift.enabled = false;
                return;
            }
            Build();
            float p = 1f + 0.1f * step;
            if (step < 0)
            {
                SetBlend(mulMat, BlendMode.DstColor, BlendMode.Zero);   // 화면 × p^2.2
                mulMat.SetVector("_Value", Vector4.one * Mathf.Pow(p, 2.2f));
                lift.enabled = false;
            }
            else
            {
                SetBlend(mulMat, BlendMode.DstColor, BlendMode.One);    // 화면 × (1 + c)
                mulMat.SetVector("_Value", Vector4.one * Mathf.Min(1f, Mathf.Pow(p, 2.2f) - 1f));
                SetBlend(liftMat, BlendMode.OneMinusDstColor, BlendMode.One);   // 어두운 곳을 들어 올린다
                liftMat.SetVector("_Value", Vector4.one * Mathf.Pow(0.16f * step / 5f, 2.2f));
                lift.enabled = true;
            }
            mul.enabled = true;
        }

        void Build()
        {
            if (mul != null) return;
            white = new PixelTex(1, 1);
            white.Put(0, 0, new Color32(255, 255, 255, 255));
            mulMat = NewMat("Brightness Multiply");
            liftMat = NewMat("Brightness Lift");
            mul = Layer("Brightness Multiply", mulMat, sortingOrder);
            lift = Layer("Brightness Lift", liftMat, sortingOrder + 1);
        }

        Material NewMat(string name) => new Material(shader) { name = name, hideFlags = HideFlags.DontSave };

        SpriteRenderer Layer(string name, Material mat, int order)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 2f);
            go.transform.localScale = new Vector3(720f, 420f, 1f);   // 화면(640x360)보다 넉넉히(흔들림·거울)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white.ToSprite(new Vector2(0.5f, 0.5f));
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        static void SetBlend(Material m, BlendMode src, BlendMode dst)
        {
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
        }
    }
}
