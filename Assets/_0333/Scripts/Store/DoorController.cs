using System.Collections;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 정면 자동문. 좌우 문짝이 벽 속으로 미끄러져 열렸다가 잠시 뒤 닫힌다. 열릴 때 문 차임 소리가 난다.
    /// 문짝 그림은 벽(실내 그림)보다 뒤에 그려지므로, 옆으로 밀리면 벽에 가려 자연스럽게 사라진다.
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        public Transform leftLeaf, rightLeaf;
        [Tooltip("문짝이 옆으로 밀려나는 거리(px)")]
        public float slideDistance = 34f;
        public float openTime = 0.55f, holdTime = 1.6f, closeTime = 0.7f;

        [Header("소리")]
        public AudioSource source;
        public AudioClip chime;
        [Range(0f, 1f)] public float volume = 0.8f;

        Vector3 leftHome, rightHome;
        bool homeSaved;
        Coroutine running;

        public bool IsOpen { get; private set; }

        void Awake() => SaveHome();

        void SaveHome()
        {
            if (homeSaved) return;
            if (leftLeaf != null) leftHome = leftLeaf.localPosition;
            if (rightLeaf != null) rightHome = rightLeaf.localPosition;
            homeSaved = true;
        }

        /// <summary>문이 열리면서 차임이 울리고, 잠시 뒤 다시 닫힌다.</summary>
        public void OpenAndClose()
        {
            SaveHome();
            if (source != null && chime != null) source.PlayOneShot(chime, volume);
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Cycle());
        }

        IEnumerator Cycle()
        {
            IsOpen = true;
            float from = CurrentAmount();
            for (float t = 0f; t < openTime; t += Time.deltaTime) { SetOpenAmount(Mathf.Lerp(from, 1f, Ease(t / openTime))); yield return null; }
            SetOpenAmount(1f);
            yield return new WaitForSeconds(holdTime);
            for (float t = 0f; t < closeTime; t += Time.deltaTime) { SetOpenAmount(1f - Ease(t / closeTime)); yield return null; }
            SetOpenAmount(0f);
            IsOpen = false;
            running = null;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        float amount;
        float CurrentAmount() => amount;

        /// <summary>0이면 닫힘, 1이면 활짝 열림. 픽셀 단위로 맞춰 움직인다.</summary>
        public void SetOpenAmount(float a)
        {
            SaveHome();
            amount = Mathf.Clamp01(a);
            float d = Mathf.Round(slideDistance * amount);
            if (leftLeaf != null) leftLeaf.localPosition = leftHome + new Vector3(-d, 0f, 0f);
            if (rightLeaf != null) rightLeaf.localPosition = rightHome + new Vector3(d, 0f, 0f);
        }
    }
}
