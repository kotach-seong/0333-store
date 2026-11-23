using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using G = Night0333.EditorTools.StoreArtGenerator;

namespace Night0333.EditorTools
{
    /// <summary>
    /// 피범벅(흔적 2)을 가게 전체에 그리기 위한 준비: 둘러볼 수 있는 가게 전체(월드 1140x640)를
    /// 손님·흔적·화면 겹그림 없이 불 켜진 상태 그대로 1픽셀 = 1월드 픽셀로 찍는다.
    /// 이 그림을 256px 조각으로 나눠 PixelLab으로 피범벅을 그린 뒤 피만 떼어 Art/Fate/blood_world*.png로 넣는다.
    /// </summary>
    public static class BloodWorldBuilder
    {
        /// <summary>배치 모드: -previewDir 폴더에 base_world.png(1140x640)를 저장한다.</summary>
        public static void RenderBaseBatch()
        {
            string dir = StoreSceneBuilder.Arg("-previewDir");
            if (string.IsNullOrEmpty(dir)) throw new System.Exception("-previewDir가 필요함");
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(StoreSceneBuilder.ScenePath);

            var look = Object.FindFirstObjectByType<MouseLookCamera>();
            Camera cam = look.GetComponent<Camera>();
            // 화면에 붙은 것(시계·패널·메뉴)은 잠깐 끈다
            var hidden = new List<GameObject>();
            foreach (Transform t in cam.transform) if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
            foreach (var v in Object.FindObjectsByType<CustomerView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) v.SetVisible(false);
            var lights = Object.FindFirstObjectByType<StoreLights>(FindObjectsInactive.Include);
            if (lights != null) lights.ResetAll();

            RectInt w = G.WorldRect;
            Vector3 oldPos = cam.transform.position;
            float oldSize = cam.orthographicSize;
            cam.transform.position = new Vector3(w.x + w.width / 2f, w.y + w.height / 2f, oldPos.z);
            cam.orthographicSize = w.height / 2f;
            Texture2D shot = PreviewRenderer.Capture(cam, w.width, w.height);
            File.WriteAllBytes(Path.Combine(dir, "base_world.png"), shot.EncodeToPNG());
            Object.DestroyImmediate(shot);
            cam.transform.position = oldPos;
            cam.orthographicSize = oldSize;
            foreach (var g in hidden) g.SetActive(true);
            Debug.Log("[0333] 가게 전체 그림 저장: " + Path.Combine(dir, "base_world.png"));
        }
    }
}
