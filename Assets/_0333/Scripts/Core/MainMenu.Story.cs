using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 메인 화면의 주차장 CCTV를 오프닝·엔딩 장면으로도 쓴다(메뉴 글자·제목·진행 표시 없이 CCTV 화면만).
    ///  - 오프닝(PlayArrival): 비 오는 밤 11시 58분, 우산 쓴 내가 주차장을 가로질러 가게로 걸어가고, 자동문이 열린다(딸랑)
    ///    → 문 쪽으로 빨려 들어가듯 확대 → 암전. CCTV라 걸음은 뚝뚝 끊긴다(초당 3장).
    ///  - 엔딩(PlayDawn): 비 그친 새벽 6시 12분, 문이 열리고 내가 걸어 나온다. 내가 만든 실종자들(창백한 얼굴)과
    ///    내가 내보낸 것들(찢어진 웃음)이 주차장·보도에 서서 이쪽을 본다. 계산대 뒤엔 교대한 사람이 서 있다.
    /// </summary>
    public partial class MainMenu
    {
        [Header("오프닝·엔딩(씬을 만들 때 채워진다)")]
        public SpriteRenderer walker;
        [Tooltip("오프닝: 주차장에서 가게 문까지 걸어가는 나(뒷모습, 우산)")]
        public Sprite[] walkIn = new Sprite[0];
        public Vector2[] walkInAt = new Vector2[0];
        [Tooltip("엔딩: 가게 문에서 걸어 나오는 나(앞모습)")]
        public Sprite[] walkOut = new Sprite[0];
        public Vector2[] walkOutAt = new Vector2[0];
        [Tooltip("엔딩: 비 그친 새벽 주차장(문 닫힘·열림)")]
        public Sprite backgroundDawn, backgroundDawnOpen;
        [Tooltip("새벽 주차장 자리: 0·1 내가 만든 실종자, 2·3 내가 내보낸 것")]
        public SpriteRenderer[] dawnSlots = new SpriteRenderer[0];
        public DawnArt[] dawnArt = new DawnArt[0];
        [Tooltip("엔딩: 문 유리 너머 계산대 뒤에 나 대신 서 있는 교대 알바생")]
        public Sprite dawnClerk;
        [Tooltip("CCTV 걸음 한 장 시간(초): 뚝뚝 끊기는 녹화 화면")]
        public float storyFrame = 0.34f;
        Sprite playerSaved;

        [Serializable]
        public class DawnArt
        {
            public string id;
            [Tooltip("실종자: 창백한 얼굴로 서 있다")]
            public Sprite missing;
            [Tooltip("내가 내보낸 것: 찢어진 웃음")]
            public Sprite trace;
        }

        bool storyScene, noRain;
        DateTime storyClock;
        float storyClockFrom;
        Sprite storyShut;

        string StoryStamp() => (storyClock + TimeSpan.FromSeconds(Time.unscaledTime - storyClockFrom)).ToString("yyyy-MM-dd  HH:mm:ss");

        /// <summary>메뉴 없이 CCTV 화면만 보이게 한다(오프닝·엔딩).</summary>
        void ShowStoryScene(Sprite bg, bool dawn)
        {
            if (storyShut == null && background != null) storyShut = background.sprite;
            storyScene = true;
            noRain = dawn;
            Showing = false;
            BuildSprites();
            SetVisible(true);
            foreach (var t in items) if (t != null) t.enabled = false;
            foreach (var t in values) if (t != null) t.enabled = false;
            if (title != null) title.enabled = false;
            if (hint != null) hint.enabled = false;
            if (traceFigure != null) traceFigure.enabled = false;
            if (bloodGlass != null) bloodGlass.enabled = false;
            foreach (var f in flyers) if (f != null) f.enabled = false;
            if (monster != null) monster.enabled = false;
            if (ghost != null) ghost.enabled = false;
            if (closeUp != null) closeUp.enabled = false;
            // 오프닝: 아직 아무도 없는 가게 / 엔딩: 계산대 뒤엔 나 대신 교대한 사람
            if (player != null)
            {
                if (playerSaved == null) playerSaved = player.sprite;
                player.sprite = dawn && dawnClerk != null ? dawnClerk : playerSaved;
                player.enabled = dawn;
            }
            foreach (var s in dawnSlots) if (s != null) s.enabled = false;
            if (walker != null) walker.enabled = false;
            if (background != null && bg != null) background.sprite = bg;
            Zoom(1f);
            if (fxTex != null) DrawFx();
            if (rec != null) rec.text = "<color=#ff3a30>●</color> REC";
            if (stamp != null) stamp.text = StoryStamp();
        }

        void EndStoryScene()
        {
            storyScene = false;
            noRain = false;
            if (walker != null) walker.enabled = false;
            foreach (var s in dawnSlots) if (s != null) s.enabled = false;
            SetVisible(false);
            if (background != null && storyShut != null) background.sprite = storyShut;
            if (player != null && playerSaved != null) player.sprite = playerSaved;
            Zoom(1f);
        }

        void PutWalker(Sprite s, Vector2 at)
        {
            if (walker == null) return;
            walker.sprite = s;
            walker.transform.localPosition = new Vector3(at.x, at.y, walker.transform.localPosition.z);
            walker.enabled = s != null;
        }

        static IEnumerator Real(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>
        /// 오프닝 끝: 비 오는 밤 주차장 CCTV. 우산 쓴 내가 가게로 걸어가 자동문이 열리고(딸랑) 들어간다
        /// → 문 쪽으로 빨려 들어가듯 확대 → 암전(Covering). 이어서 어둠 속에서 1일차 점장 전화벨이 울린다.
        /// </summary>
        public IEnumerator PlayArrival()
        {
            storyClock = DateTime.Today.AddHours(23).AddMinutes(58).AddSeconds(12);
            storyClockFrom = Time.unscaledTime;
            ShowStoryScene(storyShut != null ? storyShut : background != null ? background.sprite : null, false);
            Covering = true;
            if (fade != null) fade.enabled = true;
            SetFade(1f);
            StartLoops();
            SetLoopVolume(0f);
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                float k = t / 1.4f;
                SetFade(1f - k);
                SetLoopVolume(k);
                yield return null;
            }
            SetFade(0f);
            SetLoopVolume(1f);
            yield return Real(0.8f);

            for (int i = 0; i < walkIn.Length; i++)
            {
                PutWalker(walkIn[i], i < walkInAt.Length ? walkInAt[i] : Vector2.zero);
                // 문 앞에 다다르면 자동문이 열린다(딸랑)
                if (i == Mathf.Max(0, walkIn.Length - 3) && backgroundDoorOpen != null && background != null)
                {
                    background.sprite = backgroundDoorOpen;
                    if (chime != null) SoundFx.Play(sfx, chime, 0.5f);
                }
                yield return Real(storyFrame);
            }
            if (walker != null) walker.enabled = false;   // 들어갔다
            yield return Real(1.1f);
            if (background != null && storyShut != null) background.sprite = storyShut;
            yield return Real(1.0f);

            // 문 쪽으로 빨려 들어가듯 확대 → 암전
            SoundFx.Play(sfx, SoundFx.TubeBuzz, 0.5f);
            const float zoomTime = 1.4f;
            for (float t = 0f; t < zoomTime; t += Time.unscaledDeltaTime)
            {
                float k = t / zoomTime, e = k * k * k;
                Zoom(1f + 3.4f * e);
                if (k > 0.55f) burstUntil = Time.unscaledTime + 0.05f;
                SetFade(Mathf.Clamp01((k - 0.6f) / 0.4f));
                SetLoopVolume(1f - k);
                yield return null;
            }
            SetFade(1f);
            StopLoops();
            EndStoryScene();
            Covering = true;
        }

        /// <summary>
        /// 엔딩: 비 그친 새벽 주차장 CCTV가 밝아지고, 문이 열려 내가 걸어 나와 멈춘다(그대로 보이는 채 끝난다, HideStory로 걷는다).
        /// missingIds: 내가 만든 실종자(최대 2), traceIds: 내가 "정상"으로 내보낸 이상한 손님(최대 2).
        /// </summary>
        public IEnumerator PlayDawn(IList<string> missingIds, IList<string> traceIds)
        {
            storyClock = DateTime.Today.AddHours(6).AddMinutes(12).AddSeconds(40);
            storyClockFrom = Time.unscaledTime;
            ShowStoryScene(backgroundDawnOpen != null ? backgroundDawnOpen : backgroundDawn, true);
            ShowDawnFigures(missingIds, traceIds);
            if (fade != null) fade.enabled = true;
            SetFade(1f);
            StartLoops();
            for (float t = 0f; t < 1.8f; t += Time.unscaledDeltaTime)
            {
                float k = t / 1.8f;
                SetFade(1f - k);
                SetLoopVolume(k);
                if (rainSrc != null) rainSrc.volume = 0.05f * k;   // 비는 그쳤다: 처마에서 떨어지는 물소리만
                yield return null;
            }
            SetFade(0f);
            if (fade != null) fade.enabled = false;
            yield return Real(0.6f);

            for (int i = 0; i < walkOut.Length; i++)
            {
                PutWalker(walkOut[i], i < walkOutAt.Length ? walkOutAt[i] : Vector2.zero);
                if (i == 2 && backgroundDawn != null && background != null) background.sprite = backgroundDawn;   // 등 뒤로 문이 닫힌다
                yield return Real(storyFrame);
            }
        }

        void ShowDawnFigures(IList<string> missingIds, IList<string> traceIds)
        {
            foreach (var s in dawnSlots) if (s != null) s.enabled = false;
            Fill(missingIds, 0, a => a.missing);
            Fill(traceIds, 2, a => a.trace);

            void Fill(IList<string> ids, int first, Func<DawnArt, Sprite> pick)
            {
                if (ids == null) return;
                int n = 0;
                foreach (string id in ids)
                {
                    int slot = first + n;
                    if (n >= 2 || slot >= dawnSlots.Length) break;
                    DawnArt art = Array.Find(dawnArt, x => x != null && x.id == id);
                    Sprite s = art != null ? pick(art) : null;
                    if (s == null || dawnSlots[slot] == null) continue;
                    dawnSlots[slot].sprite = s;
                    dawnSlots[slot].enabled = true;
                    n++;
                }
            }
        }

        /// <summary>오프닝을 건너뛰었다: 주차장 장면이 돌던 중이면 멈추고 검은 화면으로 덮는다(이어서 1일차 점장 전화).</summary>
        public void CoverAfterSkip()
        {
            if (storyScene) { StopLoops(); EndStoryScene(); }
            if (fade != null) { fade.enabled = true; SetFade(1f); }
            Covering = true;
        }

        /// <summary>엔딩 새벽 장면을 걷는다(검은 화면 뒤에서).</summary>
        public void HideStory()
        {
            StopLoops();
            EndStoryScene();
            if (fade != null) fade.enabled = false;
            Covering = false;
        }

        // ── 미리보기(에디터) ──
        /// <summary>미리보기: 오프닝 주차장(걸음 frame번째, 음수면 문이 열린 채 마지막).</summary>
        public void PreviewArrival(int frame)
        {
            storyClock = DateTime.Today.AddHours(23).AddMinutes(58).AddSeconds(31);
            storyClockFrom = Time.unscaledTime;
            ShowStoryScene(storyShut != null ? storyShut : background != null ? background.sprite : null, false);
            if (fade != null) fade.enabled = false;
            int i = Mathf.Clamp(frame < 0 ? walkIn.Length - 1 : frame, 0, Mathf.Max(0, walkIn.Length - 1));
            if (walkIn.Length > 0) PutWalker(walkIn[i], i < walkInAt.Length ? walkInAt[i] : Vector2.zero);
            if ((frame < 0 || i >= walkIn.Length - 3) && backgroundDoorOpen != null && background != null) background.sprite = backgroundDoorOpen;
        }

        /// <summary>미리보기: 엔딩 새벽 주차장(걸어 나온 뒤).</summary>
        public void PreviewDawn(IList<string> missingIds, IList<string> traceIds)
        {
            storyClock = DateTime.Today.AddHours(6).AddMinutes(13).AddSeconds(5);
            storyClockFrom = Time.unscaledTime;
            ShowStoryScene(backgroundDawn, true);
            if (fade != null) fade.enabled = false;
            ShowDawnFigures(missingIds, traceIds);
            if (walkOut.Length > 0) PutWalker(walkOut[walkOut.Length - 1], walkOutAt.Length > 0 ? walkOutAt[walkOutAt.Length - 1] : Vector2.zero);
        }

        public void PreviewStoryHide()
        {
            EndStoryScene();
            if (fade != null) fade.enabled = false;
        }
    }
}
