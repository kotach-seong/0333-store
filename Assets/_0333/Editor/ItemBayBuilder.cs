using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 상품 그림(PixelLab으로 만든 것)을 가져와 물품 칸(ItemBay)을 씬에 넣는다.
    /// 그림 원본은 프로젝트 폴더의 ArtDrafts/products에 있고, 여기서 Assets/_0333/Art/Products로 복사한다.
    ///  - 정상: normal_{id}.png
    ///  - 이상현상: anomaly_{id}_1.png, anomaly_{id}_2.png, anomaly_{id}_3.png (있는 것만)
    /// 매장 그림을 다시 그리지 않으므로 빠르다. 메뉴 [0333 > 물품 칸 다시 만들기] 또는 전체 씬 생성 때 함께 실행된다.
    /// </summary>
    public static class ItemBayBuilder
    {
        const string DestDir = "Assets/_0333/Art/Products";

        static readonly (string id, string name, bool tall)[] Catalog =
        {
            ("water", "생수", true), ("gimbap", "김밥", false), ("ramen", "라면", true), ("snack", "과자", false),
            ("bread", "빵", false), ("drink", "음료수", true), ("soju", "술", true), ("pudding", "푸딩", false),
        };

        static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "products"));

        [MenuItem("0333/물품 칸 다시 만들기 (Rebuild Item Bay)")]
        static void Menu()
        {
            var scene = EditorSceneManager.OpenScene(StoreSceneBuilder.ScenePath);
            AddToScene(StoreSceneBuilder.GetOrCreateMaterial());
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[0333] 물품 칸 다시 만들기 완료");
        }

        /// <summary>배치 모드용: 물품 칸만 다시 만들고, -previewDir가 있으면 랜덤 배치 화면을 몇 장 찍는다.</summary>
        public static void RebuildBatch()
        {
            Menu();
            string dir = StoreSceneBuilder.Arg("-previewDir");
            if (string.IsNullOrEmpty(dir)) return;
            var bay = UnityEngine.Object.FindFirstObjectByType<ItemBay>();
            var cam = UnityEngine.Object.FindFirstObjectByType<MouseLookCamera>().GetComponent<Camera>();
            foreach (int seed in new[] { 11, 22, 33 })
            {
                bay.PlaceRandom(seed, 0.5f);
                PreviewRenderer.Render(cam, Path.Combine(dir, $"itembay_{seed}.png"));
            }
            bay.Clear();
            Debug.Log("[0333] 물품 칸 미리보기 저장: " + dir);
        }

        public static void AddToScene(Material mat)
        {
            var old = UnityEngine.Object.FindFirstObjectByType<ItemBay>(FindObjectsInactive.Include);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            Directory.CreateDirectory(DestDir);
            var defs = new List<ItemBay.ItemDef>();
            foreach (var c in Catalog)
            {
                Sprite normal = Import($"normal_{c.id}.png");
                if (normal == null) { Debug.LogWarning($"[0333] 상품 그림 없음: normal_{c.id}.png"); continue; }
                var anomalies = new List<ItemBay.AnomalyLook>();
                for (int k = 1; k <= 3; k++)
                {
                    // 움직이는 그림: anomaly_{id}_{k}_f0.png, _f1.png ... / 정지 그림: anomaly_{id}_{k}.png
                    var frames = new List<Sprite>();
                    for (int f = 0; f < 16; f++)
                    {
                        Sprite fr = Import($"anomaly_{c.id}_{k}_f{f}.png");
                        if (fr == null) break;
                        frames.Add(fr);
                    }
                    if (frames.Count == 0)
                    {
                        Sprite a = Import($"anomaly_{c.id}_{k}.png");
                        if (a != null) frames.Add(a);
                    }
                    if (frames.Count > 0) anomalies.Add(new ItemBay.AnomalyLook { frames = frames.ToArray(), fps = 5f });
                }
                defs.Add(new ItemBay.ItemDef { id = c.id, displayName = c.name, normal = normal, anomalies = anomalies.ToArray(), tall = c.tall });
            }
            if (defs.Count == 0) throw new Exception("상품 그림을 하나도 찾지 못함: " + SourceDir);

            var bay = new GameObject("ItemBay").AddComponent<ItemBay>();
            bay.items = defs.ToArray();
            bay.material = mat;
            bay.testAnomalyChance = 0.3f; // 시험용: R로 섞을 때 열에 셋 정도는 이상현상 물품
            // 계산 기능이 손님마다 물건을 올려놓으므로, 시작할 때 저절로 채우거나 R로 섞지 않는다
            bay.fillOnStart = false;
            bay.rerollWithR = false;
            EditorUtility.SetDirty(bay);
        }

        /// <summary>원본 폴더에서 그림을 복사해 스프라이트로 가져온다. 기준점은 물건의 바닥(맨 아래 불투명 줄).</summary>
        static Sprite Import(string file)
        {
            string src = Path.Combine(SourceDir, file);
            if (!File.Exists(src)) return null;
            string dest = $"{DestDir}/{file}";
            byte[] bytes = File.ReadAllBytes(src);
            File.WriteAllBytes(dest, bytes);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);

            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            int bottom = BottomRow(tex);
            int h = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);

            var ti = (TextureImporter)AssetImporter.GetAtPath(dest);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = true; // 물건을 집을 때 투명한 곳은 지나치도록 그림을 읽는다
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = new Vector2(0.5f, (float)bottom / h);
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(dest);
        }

        /// <summary>맨 아래 불투명 줄(텍스처는 아래쪽이 y=0). 물건이 이 줄로 물품 칸 표면에 선다.</summary>
        static int BottomRow(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    if (px[y * tex.width + x].a > 40) return y;
            return 0;
        }
    }
}
