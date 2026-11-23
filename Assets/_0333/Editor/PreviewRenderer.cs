using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Night0333.EditorTools
{
    /// <summary>검증용: 실제 카메라로 지금 씬을 렌더링해 PNG로 저장한다(배치 모드에서도 동작).</summary>
    public static class PreviewRenderer
    {
        public static void Render(Camera cam, string path, int w = 1920, int h = 1080)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Texture2D tex = Capture(cam, w, h);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>실제 카메라 화면을 그림으로 받아 온다(다 쓰면 DestroyImmediate).</summary>
        public static Texture2D Capture(Camera cam, int w, int h)
        {
            Rect oldRect = cam.rect;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            cam.rect = oldRect;
            rt.Release();
            Object.DestroyImmediate(rt);
            return tex;
        }
    }
}
