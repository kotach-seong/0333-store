using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 언어 바꾸기(한국어 / English / 简体中文): 계산대 오브젝트에 LocAssets를 붙이고
    /// 글자표(Data/Lang/Strings.txt), 언어별 대사 파일(Data/*.en.txt, *.zh.txt), 글꼴(갈무리·퓨전 픽셀)을 채운다.
    /// CheckoutBuilder.AddToScene에서 부른다.
    /// </summary>
    public static class LocBuilder
    {
        public const string StringsPath = "Assets/_0333/Data/Lang/Strings.txt";
        const string DataDir = "Assets/_0333/Data";
        const string Galmuri11 = "Assets/_0333/Fonts/Galmuri11.ttf", Galmuri9 = "Assets/_0333/Fonts/Galmuri9.ttf";
        const string Zh12 = "Assets/_0333/Fonts/FusionPixel12-zh_hans.ttf", Zh10 = "Assets/_0333/Fonts/FusionPixel10-zh_hans.ttf";

        public static LocAssets Build(CheckoutSystem checkout)
        {
            var loc = checkout.GetComponent<LocAssets>();
            if (loc == null) loc = checkout.gameObject.AddComponent<LocAssets>();
            AssetDatabase.ImportAsset(StringsPath);
            loc.strings = AssetDatabase.LoadAssetAtPath<TextAsset>(StringsPath);
            if (loc.strings == null) Debug.LogError("[0333] 글자표를 찾지 못함: " + StringsPath);

            var files = new List<TextAsset>();
            foreach (string path in Directory.GetFiles(DataDir, "*.txt"))
            {
                string p = path.Replace('\\', '/');
                if (!p.EndsWith(".en.txt") && !p.EndsWith(".zh.txt")) continue;
                AssetDatabase.ImportAsset(p);
                var t = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
                if (t != null) files.Add(t);
            }
            loc.files = files.ToArray();

            loc.galmuri11 = AssetDatabase.LoadAssetAtPath<Font>(Galmuri11);
            loc.galmuri9 = AssetDatabase.LoadAssetAtPath<Font>(Galmuri9);
            loc.zh12 = AssetDatabase.LoadAssetAtPath<Font>(Zh12);
            loc.zh10 = AssetDatabase.LoadAssetAtPath<Font>(Zh10);
            if (loc.zh12 == null || loc.zh10 == null) Debug.LogError("[0333] 중국어 글꼴(퓨전 픽셀)을 찾지 못함");
            Debug.Log($"[0333] 언어 파일 {files.Count}개, 글자표 {(loc.strings != null ? "있음" : "없음")}");
            Loc.Reload();
            EditorUtility.SetDirty(loc);
            return loc;
        }
    }
}
