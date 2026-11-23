using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// Windows 실행 파일(exe) 빌드. 결과: 프로젝트/Build/Windows/0333.exe와 그 옆에
    /// 플레이어용 안내(README.txt), 글꼴·소리 라이선스(Licenses/). 배포하지 않는 "_DoNotShip" 폴더는 지운다.
    /// 메뉴 0333 → Windows 빌드 만들기, 배치: -executeMethod Night0333.EditorTools.GameBuilder.BuildWindowsBatch [-buildDir 폴더]
    /// </summary>
    public static class GameBuilder
    {
        const string DefaultDir = "Build/Windows";
        const string ExeName = "0333.exe";

        [MenuItem("0333/Windows 빌드 만들기 (Build Windows exe)")]
        public static void BuildWindowsMenu()
        {
            if (BuildWindows(DefaultDir)) EditorUtility.RevealInFinder(Path.Combine(DefaultDir, ExeName));
        }

        public static void BuildWindowsBatch()
        {
            if (!BuildWindows(Arg("-buildDir") ?? DefaultDir)) EditorApplication.Exit(1);
        }

        public static bool BuildWindows(string dir)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[0333] 빌드할 장면이 없음(Build Settings)"); return false; }

            // 예전 빌드만 지운다(다른 폴더를 실수로 지우지 않게, exe가 있던 폴더이거나 빈 폴더일 때만)
            if (Directory.Exists(dir))
            {
                bool ours = File.Exists(Path.Combine(dir, ExeName)) || !Directory.EnumerateFileSystemEntries(dir).Any();
                if (!ours) { Debug.LogError("[0333] 빌드 폴더에 다른 파일이 있어 멈춤: " + Path.GetFullPath(dir)); return false; }
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[0333] 빌드 결과: {s.result}, 크기 {s.totalSize / (1024f * 1024f):0.0}MB, 걸린 시간 {s.totalTime.TotalSeconds:0}초, 경고 {s.totalWarnings}, 오류 {s.totalErrors}");
            if (s.result != BuildResult.Succeeded) return false;

            foreach (string d in Directory.GetDirectories(dir, "*_DoNotShip")) Directory.Delete(d, true);

            string lic = Path.Combine(dir, "Licenses");
            Directory.CreateDirectory(lic);
            CopyTo("Assets/_0333/Fonts/Galmuri-OFL.md", Path.Combine(lic, "Galmuri-OFL.md"));
            CopyTo("Assets/_0333/Fonts/FusionPixel-OFL.txt", Path.Combine(lic, "FusionPixel-OFL.txt"));
            CopyTo("Assets/_0333/Audio/CREDITS.txt", Path.Combine(lic, "Audio-CREDITS.txt"));
            // 메모장에서 한글·중국어가 깨지지 않게 BOM을 붙인다
            File.WriteAllText(Path.Combine(dir, "README.txt"), PlayerReadme.Replace("\n", "\r\n"), new UTF8Encoding(true));
            Debug.Log("[0333] 빌드 완료: " + Path.GetFullPath(Path.Combine(dir, ExeName)));
            return true;
        }

        static void CopyTo(string src, string dst)
        {
            if (File.Exists(src)) File.Copy(src, dst, true);
            else Debug.LogWarning("[0333] 라이선스 파일이 없음: " + src);
        }

        static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        const string PlayerReadme =
@"03:33 — Late-Night Convenience Store
====================================

Run 0333.exe to play. The game starts in English;
you can change the language in Settings (Language: English / 한국어 / 简体中文).

You work the night shift at a 24-hour store for 7 nights.
Check each customer's items, receipt and CCTV, then mark the sale as normal or not.
Every night at 3:33 a.m., whatever comes to the counter, never close it as ""Normal.""

Controls
  Mouse ............... look around
  Left click .......... open the receipt / checklist / CCTV / notebook / phone, press buttons
  Drag down ........... scan an item
  1 / 2 ............... choose a reply
  Space / Enter ....... next line, confirm
  Hold Space .......... skip the opening
  Esc / Right click ... close a window / pause menu

Progress is saved automatically at the end of each night.

------------------------------------------------------------

03:33 — 심야 편의점

0333.exe를 실행하세요. 처음에는 영어로 나옵니다.
설정 → Language에서 한국어로 바꿀 수 있습니다.

조작: 마우스로 둘러보기, 왼쪽 클릭으로 영수증·점검표·CCTV·수첩·전화 열기,
물건을 끌어내려 스캔, 1·2로 대답 고르기, Space·Enter로 대사 넘기기,
Space 길게 누르면 오프닝 건너뛰기, Esc로 일시정지.

------------------------------------------------------------

Credits
  Made by kotach
  Fonts: Galmuri (c) 2019-2025 Lee Minseo (quiple), Fusion Pixel Font (c) 2022 TakWolf
         — both SIL Open Font License 1.1 (see Licenses/)
  Phone voice source: beetlemuse (Freesound, CC0)
  Made with Unity

All stories and characters in this game are fictional.
";
    }
}
