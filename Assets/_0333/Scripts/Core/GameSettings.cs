using System;
using UnityEngine;

namespace Night0333
{
    /// <summary>
    /// 설정(메인 화면 설정·일시정지 설정이 같이 쓴다). PlayerPrefs에 저장한다.
    ///  0 소리 크기 / 1 마우스 감도 / 2 화면 모드(창·전체 화면) / 3 창 크기(640x360의 정수배) / 4 밝기
    ///  5 깜빡임 줄이기 / 6 혈흔 없애기 / 7 화면 흔들림(켬·끔) / 8 뒤로
    /// 깜빡임 줄이기: 형광등이 짧게 깜빡이는 건 무시하고 꺼질 때는 서서히(StoreLights), 붉은 번쩍임은 약하게, 바닥 장면의 등 깜빡임 없음.
    /// 혈흔 없애기: 바닥·벽·계산대·유리의 피 자국은 안 보이고, 판정에 쓰는 피(영수증·피 흘리는 물건)는 검은 얼룩으로.
    /// 화면 흔들림 끔: 화면 흔들림(ScreenFx)과 손 떨림으로 시선·커서가 흔들리는 것이 없어진다.
    /// </summary>
    public static class GameSettings
    {
        public const string VolumeKey = "0333.volume", SensKey = "0333.sensitivity";
        const string FullKey = "0333.fullscreen", WindowKey = "0333.windowScale", BrightKey = "0333.brightness";
        const string FlickerKey = "0333.reduceFlicker", BloodKey = "0333.noBlood", ShakeKey = "0333.shake", LangKey = "0333.language";

        public static readonly string[] Labels = { "소리 크기", "마우스 감도", "화면 모드", "창 크기", "밝기", "깜빡임 줄이기", "혈흔 없애기", "화면 흔들림", "언어 · Language", "뒤로" };
        /// <summary>값을 바꾸는 줄 수(마지막 "뒤로" 앞까지).</summary>
        public const int Rows = 9;
        public const int LanguageRow = 8;
        /// <summary>언어 이름은 늘 그 언어로(잘못 골라도 알아볼 수 있게).</summary>
        static readonly string[] LangNames = { "한국어", "English", "简体中文" };
        public const int WindowRow = 3;
        public const int BaseW = 640, BaseH = 360;
        const int BrightMin = -5, BrightMax = 5;

        static bool loaded;
        static float volume = 0.8f, sens = 1.2f;
        static bool fullscreen = true, reduceFlicker, noBlood, shake = true;
        static Lang language = Lang.Ko;
        static int windowScale = 2, bright;

        /// <summary>설정이 바뀌었다(피·밝기처럼 이미 그려진 것을 다시 그릴 때).</summary>
        public static event Action Changed;

        public static float Volume { get { Load(); return volume; } }
        public static float Sensitivity { get { Load(); return sens; } }
        public static bool Fullscreen { get { Load(); return fullscreen; } }
        public static int WindowScale { get { Load(); return windowScale; } }
        /// <summary>밝기 단계(-5 ~ 5, 0이 기본). 한 단계에 10%.</summary>
        public static int Brightness { get { Load(); return bright; } }
        public static bool ReduceFlicker { get { Load(); return reduceFlicker; } }
        public static bool NoBlood { get { Load(); return noBlood; } }
        public static bool NoShake { get { Load(); return !shake; } }
        public static Lang Language { get { Load(); return language; } }

        /// <summary>처음 켰을 때 언어(사용자 결정): 컴퓨터 언어와 상관없이 영어. 설정에서 바꾸면 그 언어가 저장된다.</summary>
        const Lang FirstLanguage = Lang.En;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            sens = PlayerPrefs.GetFloat(SensKey, 1.2f);
            fullscreen = PlayerPrefs.HasKey(FullKey) ? PlayerPrefs.GetInt(FullKey) != 0 : Screen.fullScreenMode != FullScreenMode.Windowed;
            windowScale = Mathf.Clamp(PlayerPrefs.GetInt(WindowKey, DefaultScale()), 1, MaxScale());
            bright = Mathf.Clamp(PlayerPrefs.GetInt(BrightKey, 0), BrightMin, BrightMax);
            reduceFlicker = PlayerPrefs.GetInt(FlickerKey, 0) != 0;
            noBlood = PlayerPrefs.GetInt(BloodKey, 0) != 0;
            shake = PlayerPrefs.GetInt(ShakeKey, 1) != 0;
            language = PlayerPrefs.HasKey(LangKey) ? (Lang)Mathf.Clamp(PlayerPrefs.GetInt(LangKey), 0, 2) : FirstLanguage;
        }

        static int SystemW => Display.main != null && Display.main.systemWidth > 0 ? Display.main.systemWidth : Screen.currentResolution.width > 0 ? Screen.currentResolution.width : 1920;
        static int SystemH => Display.main != null && Display.main.systemHeight > 0 ? Display.main.systemHeight : Screen.currentResolution.height > 0 ? Screen.currentResolution.height : 1080;

        /// <summary>모니터에 들어가는 가장 큰 배수(창 크기 고르기의 끝).</summary>
        public static int MaxScale() => Mathf.Max(1, Mathf.Min(SystemW / BaseW, SystemH / BaseH));

        /// <summary>처음 창 크기: 모니터의 90% 안에 들어가는 가장 큰 배수(1920x1080 모니터면 1280x720).</summary>
        static int DefaultScale() => Mathf.Max(1, Mathf.Min(Mathf.FloorToInt(SystemW * 0.9f / BaseW), Mathf.FloorToInt(SystemH * 0.9f / BaseH)));

        /// <summary>줄마다 보여 줄 값("◀  80%  ▶").</summary>
        public static string ValueText(int row)
        {
            Load();
            string v;
            switch (row)
            {
                case 0: v = $"{Mathf.RoundToInt(volume * 100f)}%"; break;
                case 1: v = $"{sens:0.0}"; break;
                case 2: v = Loc.T(fullscreen ? "전체 화면" : "창"); break;
                case 3: v = $"{BaseW * windowScale}×{BaseH * windowScale}"; break;
                case 4: v = $"{100 + bright * 10}%"; break;
                case 5: v = Loc.T(reduceFlicker ? "켬" : "끔"); break;
                case 6: v = Loc.T(noBlood ? "켬" : "끔"); break;
                case 7: v = Loc.T(shake ? "켬" : "끔"); break;
                case 8: return $"◀  {LangNames[(int)language]}  ▶";
                default: return "";
            }
            return $"◀  {v}  ▶";
        }

        /// <summary>지금은 효과가 없는 줄(전체 화면일 때 창 크기): 회색으로 보인다.</summary>
        public static bool Inactive(int row) => row == WindowRow && Fullscreen;

        /// <summary>한 줄의 값을 dir(-1 왼쪽, 1 오른쪽)만큼 바꾸고 저장·적용한다. 켬/끔 줄은 어느 쪽이든 뒤집는다.</summary>
        public static void Adjust(int row, int dir, MouseLookCamera look)
        {
            Load();
            switch (row)
            {
                case 0: volume = Mathf.Clamp01(Mathf.Round((volume + dir * 0.1f) * 10f) / 10f); PlayerPrefs.SetFloat(VolumeKey, volume); break;
                case 1: sens = Mathf.Clamp(Mathf.Round((sens + dir * 0.2f) * 10f) / 10f, 0.2f, 4f); PlayerPrefs.SetFloat(SensKey, sens); break;
                case 2: fullscreen = !fullscreen; PlayerPrefs.SetInt(FullKey, fullscreen ? 1 : 0); break;
                case 3: windowScale = Mathf.Clamp(windowScale + dir, 1, MaxScale()); PlayerPrefs.SetInt(WindowKey, windowScale); break;
                case 4: bright = Mathf.Clamp(bright + dir, BrightMin, BrightMax); PlayerPrefs.SetInt(BrightKey, bright); break;
                case 5: reduceFlicker = !reduceFlicker; PlayerPrefs.SetInt(FlickerKey, reduceFlicker ? 1 : 0); break;
                case 6: noBlood = !noBlood; PlayerPrefs.SetInt(BloodKey, noBlood ? 1 : 0); break;
                case 7: shake = !shake; PlayerPrefs.SetInt(ShakeKey, shake ? 1 : 0); break;
                case 8: language = (Lang)(((int)language + dir + 3) % 3); PlayerPrefs.SetInt(LangKey, (int)language); break;
                default: return;
            }
            PlayerPrefs.Save();
            Apply(look);
            if (row == 2 || (row == 3 && !fullscreen)) ApplyDisplay();
            Changed?.Invoke();
            if (row == LanguageRow) Loc.Notify();   // 글꼴을 바꾸고 메뉴·글자를 다시 그린다
        }

        /// <summary>소리·감도·밝기를 적용한다(게임을 켤 때와 값을 바꿀 때).</summary>
        public static void Apply(MouseLookCamera look)
        {
            Load();
            AudioListener.volume = volume;
            if (look != null) look.sensitivity = sens;
            ScreenBrightness.Refresh();
        }

        /// <summary>게임을 켤 때: 저장된 화면 모드·창 크기가 있으면 그대로(없으면 플레이어 설정 그대로 둔다).</summary>
        public static void ApplyDisplayAtStart()
        {
            if (Application.isEditor || !PlayerPrefs.HasKey(FullKey)) return;
            Load();
            bool nowFull = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (nowFull != fullscreen || (!fullscreen && (Screen.width != BaseW * windowScale || Screen.height != BaseH * windowScale)))
                ApplyDisplay();
        }

        static void ApplyDisplay()
        {
            if (Application.isEditor) return;   // 에디터 게임 창은 크기를 바꿀 수 없다
            if (fullscreen) Screen.SetResolution(SystemW, SystemH, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(BaseW * windowScale, BaseH * windowScale, FullScreenMode.Windowed);
        }

        // ── 미리보기(에디터): 저장하지 않고 값만 바꾼다 ──
        public static void PreviewSet(int brightness = 0, bool flicker = false, bool blood = false, bool shakeOn = true)
        {
            Load();
            bright = Mathf.Clamp(brightness, BrightMin, BrightMax);
            reduceFlicker = flicker;
            noBlood = blood;
            shake = shakeOn;
            ScreenBrightness.Refresh();
        }

        /// <summary>미리보기: 언어만 바꾼다(저장하지 않는다).</summary>
        public static void PreviewLanguage(Lang l)
        {
            Load();
            language = l;
            Loc.Notify();
        }

        /// <summary>미리보기가 끝나면: 저장된 값으로 되돌린다.</summary>
        public static void PreviewReset()
        {
            loaded = false;
            Load();
            ScreenBrightness.Refresh();
            Loc.Notify();
        }
    }
}
