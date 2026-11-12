using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Group = Night0333.EditorTools.StoreArtGenerator.Group;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 메뉴 [0333 > 정면 편의점 씬 생성] 한 번으로
    /// 픽셀아트 생성 → 스프라이트 임포트 설정 → Store 씬 구성 → 빌드 목록 등록까지 처리한다.
    /// 다시 실행하면 그림과 씬을 새로 만든다(Store.unity를 덮어씀).
    /// </summary>
    public static class StoreSceneBuilder
    {
        const string Root = "Assets/_0333";
        const string ArtDir = Root + "/Art/Generated";
        const string MatPath = Root + "/Materials/PixelSpriteUnlit.mat";
        public const string ScenePath = Root + "/Scenes/Store.unity";

        [MenuItem("0333/정면 편의점 씬 생성 (Build Store Scene)")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(null);
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>배치 모드용: Unity.exe -batchmode -executeMethod ...BuildBatch -previewDir 경로</summary>
        public static void BuildBatch() => Build(Arg("-previewDir"));

        static void Build(string previewDir)
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Scenes");

            List<StoreArtGenerator.Layer> layers = StoreArtGenerator.Generate();
            foreach (var l in layers) l.Canvas.SavePng($"{ArtDir}/{l.Name}.png");

            // 이번에 만들지 않은 예전 그림은 지운다
            var keep = new HashSet<string>();
            foreach (var l in layers) keep.Add(l.Name);
            foreach (string file in Directory.GetFiles(ArtDir, "*.png"))
                if (!keep.Contains(Path.GetFileNameWithoutExtension(file)))
                    AssetDatabase.DeleteAsset($"{ArtDir}/{Path.GetFileName(file)}");
            AssetDatabase.Refresh();
            foreach (var l in layers) ConfigureSprite($"{ArtDir}/{l.Name}.png");

            Material mat = GetOrCreateMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCameras();

            var store = new GameObject("Store").transform;
            var groups = new Dictionary<Group, Transform>();
            foreach (Group g in Enum.GetValues(typeof(Group)))
            {
                var t = new GameObject(g.ToString()).transform;
                t.SetParent(store, false);
                groups[g] = t;
            }

            foreach (var l in layers)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{l.Name}.png");
                if (sprite == null) throw new Exception($"스프라이트를 불러오지 못함: {l.Name}");
                RectInt r = l.Canvas.Rect;
                var center = new Vector3(r.x + r.width * 0.5f, r.y + r.height * 0.5f, 0f);

                if (l.Name == "outside_rain")
                {
                    var rain = new GameObject("Rain");
                    rain.transform.SetParent(groups[l.Group], false);
                    rain.transform.localPosition = center;
                    var scroll = rain.AddComponent<ScrollingRain>();
                    scroll.tileHeight = r.height;
                    scroll.speed = 170f;
                    CreateSprite("Rain A", rain.transform, Vector3.zero, sprite, mat, l.Order);
                    CreateSprite("Rain B", rain.transform, new Vector3(0f, r.height, 0f), sprite, mat, l.Order);
                }
                else
                {
                    CreateSprite(l.Name, groups[l.Group], center, sprite, mat, l.Order);
                }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previewDir)) WritePreviews(layers, previewDir);
            Debug.Log($"[0333] Store 씬 생성 완료: {ScenePath} (레이어 {layers.Count}개)");
        }

        static void CreateCameras()
        {
            var bgGo = new GameObject("Letterbox Background Camera");
            var bg = bgGo.AddComponent<Camera>();
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = Color.black;
            bg.cullingMask = 0;
            bg.depth = -10;
            bg.orthographic = true;

            Vector2 home = StoreArtGenerator.Home;
            RectInt world = StoreArtGenerator.WorldRect;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(home.x, home.y, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = StoreArtGenerator.ViewH * 0.5f; // 화면 높이 270px, 1 유닛 = 1 픽셀
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.depth = 0;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Letterbox>();
            var look = camGo.AddComponent<MouseLookCamera>();
            look.worldBounds = new Rect(world.x, world.y, world.width, world.height);
            look.home = home;
        }

        static void CreateSprite(string name, Transform parent, Vector3 localPos, Sprite sprite, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
        }

        static void ConfigureSprite(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }

        static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader == null) throw new Exception("스프라이트용 Unlit 셰이더를 찾지 못함");
            mat = new Material(shader) { name = "PixelSpriteUnlit" };
            AssetDatabase.CreateAsset(mat, MatPath);
            return mat;
        }

        /// <summary>검증용: 레이어를 CPU로 합성해 전체 그림과 시선 방향별 화면(640x360)을 PNG로 저장.</summary>
        static void WritePreviews(List<StoreArtGenerator.Layer> layers, string dir)
        {
            Directory.CreateDirectory(dir);
            PixelCanvas all = StoreArtGenerator.Composite(layers);
            all.SavePng(Path.Combine(dir, "world_full.png"), 2);

            RectInt w = StoreArtGenerator.WorldRect;
            int vw = StoreArtGenerator.ViewW, vh = StoreArtGenerator.ViewH;
            int hx = (int)StoreArtGenerator.Home.x, hy = (int)StoreArtGenerator.Home.y;
            int left = w.xMin + vw / 2, right = w.xMax - vw / 2, down = w.yMin + vh / 2, up = w.yMax - vh / 2;
            var views = new (string name, int cx, int cy)[]
            {
                ("view_home", hx, hy), ("view_up", hx, up), ("view_down", hx, down),
                ("view_left", left, hy), ("view_right", right, hy),
                ("view_down_left", left, down), ("view_down_right", right, down),
            };
            foreach (var v in views)
            {
                var view = new PixelCanvas(new RectInt(v.cx - vw / 2, v.cy - vh / 2, vw, vh));
                view.Composite(all, Vector2Int.zero);
                view.SavePng(Path.Combine(dir, v.name + ".png"), 3);
            }
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
