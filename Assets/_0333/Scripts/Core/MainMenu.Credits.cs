using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 메인 화면 → 크레딧(사용자 결정: 설정과 종료 사이). 주차장 CCTV가 어두워지고 크레딧이 아래에서 천천히 올라온다.
    /// 마지막 인사("플레이 해주셔서 감사합니다")가 화면 가운데에 오면 멈춘다.
    /// Space·Enter(사용자 요청, 화면에 안내)나 클릭·Esc·오른쪽 클릭이면 메인 화면 메뉴로 돌아간다. 내용은 Data/Credits.txt.
    /// </summary>
    public partial class MainMenu
    {
        [Header("크레딧")]
        [Tooltip("크레딧 내용(Data/Credits.txt)")]
        public TextAsset creditsFile;
        public Text creditsText;
        [Tooltip("크레딧 뒤를 어둡게 덮는 막(CCTV 화면 위, 글자 아래)")]
        public SpriteRenderer creditsDim;
        public float creditsSpeed = 26f;

        bool credits;
        float creditsY;
        PixelTex creditsDimTex;
        const string CreditsHint = "Space · Enter: 메인으로";

        void OpenCredits()
        {
            credits = true;
            foreach (var t in items) if (t != null) t.enabled = false;
            foreach (var t in values) if (t != null) t.enabled = false;
            if (title != null) title.enabled = false;
            HideIdle();
            if (creditsDim != null)
            {
                if (creditsDimTex == null)
                {
                    creditsDimTex = new PixelTex(1, 1);
                    creditsDimTex.Put(0, 0, new Color32(0, 0, 0, 200));
                    creditsDim.sprite = creditsDimTex.ToSprite(Vector2.zero);
                    creditsDim.transform.localScale = new Vector3(W, H, 1f);
                }
                creditsDim.enabled = true;
            }
            if (creditsText != null)
            {
                creditsText.text = BuildCreditsText();
                creditsText.enabled = true;
                creditsY = -H / 2f - 6f;   // 화면 아래에서 시작
                creditsText.rectTransform.anchoredPosition = new Vector2(0f, creditsY);
            }
            if (hint != null) { hint.enabled = true; hint.text = Loc.T(CreditsHint); }
        }

        void CloseCredits()
        {
            HideCredits();
            if (title != null) title.enabled = true;
            if (hint != null) hint.text = "";
            selected = 3;
            hover = -1;
            foreach (var t in items) if (t != null) t.enabled = true;
            Layout();
        }

        void HideCredits()
        {
            credits = false;
            if (creditsText != null) creditsText.enabled = false;
            if (creditsDim != null) creditsDim.enabled = false;
        }

        void UpdateCredits()
        {
            if (creditsText == null) { CloseCredits(); return; }
            // 마지막 줄이 화면 가운데쯤 오면 멈춘다
            float end = creditsText.preferredHeight - 12f;
            creditsY = Mathf.Min(end, creditsY + Time.unscaledDeltaTime * creditsSpeed);
            creditsText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(creditsY));
            // Space·Enter·클릭(또는 Esc·오른쪽 클릭)이면 메인 화면 메뉴로
            if (GameInput.SubmitDown || GameInput.LeftClickDown || GameInput.EscapeDown || GameInput.RightClickDown)
            {
                SoundFx.Play(sfx, SoundFx.Tick, 0.9f);
                CloseCredits();
            }
        }

        /// <summary>Data/Credits.txt를 글자 크기·색이 다른 여러 줄로(@큰 제목, ## 작은 제목, % 안내문, ! 마지막 인사).</summary>
        string BuildCreditsText()
        {
            string src = creditsFile != null ? Loc.Text(creditsFile) : "@03:33\n\n!" + Loc.T("플레이 해주셔서 감사합니다");
            var sb = new StringBuilder();
            foreach (string raw in src.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("#") && !line.StartsWith("##")) continue;   // 설명
                if (sb.Length > 0) sb.Append('\n');
                if (line.Length == 0) { sb.Append("<size=8> </size>"); continue; }
                if (line.StartsWith("@")) sb.Append($"<size=33><color=#ff7a33>{line.Substring(1).Trim()}</color></size>");
                else if (line.StartsWith("##")) sb.Append($"<color=#8e989e>{line.Substring(2).Trim()}</color>");
                else if (line.StartsWith("%")) sb.Append($"<color=#8e989e>{line.Substring(1).Trim()}</color>");
                else if (line.StartsWith("!")) sb.Append($"<size=22><color=#ece4d0>{line.Substring(1).Trim()}</color></size>");
                else sb.Append($"<color=#eef2f2>{line.Trim()}</color>");
            }
            return sb.ToString();
        }

        // ── 미리보기(에디터) ──
        /// <summary>미리보기: 크레딧이 k(0~1)만큼 올라간 모습(1이면 마지막 인사가 가운데에 멈춘 모습).</summary>
        public void PreviewCredits(float k)
        {
            PreviewShow(3);
            Zoom(1f);
            OpenCredits();
            if (creditsText == null) return;
            float end = creditsText.preferredHeight - 12f;
            creditsY = Mathf.Lerp(-H / 2f - 6f, end, k);
            creditsText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(creditsY));
        }
    }
}
