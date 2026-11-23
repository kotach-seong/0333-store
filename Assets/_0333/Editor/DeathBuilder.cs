using System.IO;
using UnityEditor;
using UnityEngine;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 죽는 장면(사용자 결정 2026-11-19): 형광등이 깜빡일 때마다 괴물이 세 번에 걸쳐 다가오고(문 앞 → 통로 가운데 → 계산대 몇 걸음 앞),
    /// 완전히 꺼졌다 켜지면 계산대 바로 앞 → 계산대를 넘어 덮친다 → 뒤로 넘어져 바닥에서 올려다본 화면에 벌어진 입 → 암전.
    /// 그림(PixelLab, ArtDrafts/fate/death): walk_1~8(전신 걷기, full_body.png에서), lunge.png(덮쳐 오는 상반신, 그려진 계산대는 지움),
    /// floor_pov.png(바닥에서 올려다본 640x360).
    /// </summary>
    public static class DeathBuilder
    {
        const string Dest = "Assets/_0333/Art/Fate/death";
        const float MonsterMeters = 2.15f;
        // 세 번 다가오는 자리: (x, 깊이 z m). 문 앞 → 통로 가운데 → 계산대 몇 걸음 앞
        static readonly Vector2[] Steps = { new Vector2(0f, 7.3f), new Vector2(0.08f, 5.2f), new Vector2(-0.05f, 3.5f) };

        static string Draft(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtDrafts", "fate", "death", name));

        public static void Build(ShiftEndView view, Transform root, Material mat)
        {
            if (view == null) return;
            Directory.CreateDirectory(Dest);

            // 걷는 괴물(전신): 발바닥이 기준점
            var frames = new System.Collections.Generic.List<Sprite>();
            int figureH = 0;
            for (int i = 1; i <= 8; i++)
            {
                string src = Draft($"walk_{i}.png");
                if (!File.Exists(src)) break;
                string dst = $"{Dest}/walk_{i}.png";
                File.Copy(src, dst, true);
                Texture2D t = CustomerBuilder.LoadPng(src);
                Color32[] px = t.GetPixels32();
                int bottom = -1, top = -1;
                for (int y = 0; y < t.height && bottom < 0; y++)
                    for (int x = 0; x < t.width; x++) if (px[y * t.width + x].a > 40) { bottom = y; break; }
                for (int y = t.height - 1; y >= 0 && top < 0; y--)
                    for (int x = 0; x < t.width; x++) if (px[y * t.width + x].a > 40) { top = y; break; }
                float pivotY = Mathf.Max(0, bottom) / (float)t.height;
                figureH = Mathf.Max(figureH, top - bottom + 1);
                Object.DestroyImmediate(t);
                frames.Add(CustomerBuilder.ImportSprite(dst, new Vector2(0.5f, pivotY)));
            }
            view.stalkerWalk = frames.ToArray();
            if (figureH <= 0) figureH = 190;

            var at = new Vector3[Steps.Length];
            var scale = new float[Steps.Length];
            var depth = new float[Steps.Length];
            for (int i = 0; i < Steps.Length; i++)
            {
                Vector2 feet = G.ToScreen(new Vector3(Steps[i].x, 0f, Steps[i].y));
                at[i] = new Vector3(Mathf.Round(feet.x), Mathf.Round(feet.y), 0.01f);
                scale[i] = MonsterMeters * G.Focal / Steps[i].y / figureH;
                depth[i] = Steps[i].y;
            }
            view.stalkerAt = at;
            view.stalkerScale = scale;
            view.stalkerDepth = depth;
            var go = new GameObject("Death Stalker");
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = 45;   // 손님(40) 위, 계산대(50) 뒤: 가까이 오면 다리가 계산대에 가린다
            sr.enabled = false;
            view.stalker = sr;

            // 계산대를 넘어 덮쳐 오는 모습
            string lunge = Draft("lunge.png");
            if (File.Exists(lunge))
            {
                File.Copy(lunge, $"{Dest}/lunge.png", true);
                view.lungeSprite = CustomerBuilder.ImportSprite($"{Dest}/lunge.png", new Vector2(0.5f, 0.5f));
            }

            // 바닥에서 올려다본 화면(근무 끝 패널 자식: 검은 막(300) 바로 밑)
            string pov = Draft("floor_pov.png");
            if (File.Exists(pov) && view.panel != null)
            {
                File.Copy(pov, $"{Dest}/floor_pov.png", true);
                var pgo = new GameObject("Floor POV");
                pgo.transform.SetParent(view.panel.transform, false);
                var psr = pgo.AddComponent<SpriteRenderer>();
                psr.sharedMaterial = mat;
                psr.sortingOrder = 298;
                psr.sprite = CustomerBuilder.ImportSprite($"{Dest}/floor_pov.png", new Vector2(0.5f, 0.5f));
                psr.enabled = false;
                view.floorPov = psr;
            }
            EditorUtility.SetDirty(view);
        }
    }
}
