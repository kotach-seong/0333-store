using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Night0333
{
    /// <summary>
    /// 화면 전체에 거는 연출(3:33 이벤트). 카메라에 붙는다.
    ///  - shake(0~1): 화면이 흔들린다(MouseLookCamera가 Offset을 더한다).
    ///  - Mirror: 가게 전체가 좌우로 뒤집혀 보인다(글씨까지). 마우스도 거꾸로 느껴진다.
    ///  - veil(0~1): 가장자리부터 붉은 막이 조여 오며 앞이 잘 안 보인다. Flash는 순간 빨개졌다 빠진다.
    ///  - 속삭임 글자: 화면 여기저기에 작은 글씨가 떴다 사라진다.
    ///  - 반복 소리 두 개(심장·이명·속삭임·잡음 등).
    /// </summary>
    [DefaultExecutionOrder(100)]   // 시선(MouseLookCamera)이 자리를 정한 뒤에 거울 시야를 맞춘다
    public class ScreenFx : MonoBehaviour
    {
        public static ScreenFx Instance { get; private set; }

        [Header("연결(씬을 만들 때 채워진다)")]
        [Tooltip("화면을 덮는 붉은 막(카메라 자식, 화면 크기)")]
        public SpriteRenderer veil;
        [Tooltip("속삭임 글자들(카메라에 붙은 캔버스)")]
        public Text[] whispers = new Text[0];

        [Header("지금 상태")]
        [Range(0f, 1f)] public float shake;
        [Range(0f, 1f)] public float veilAmount;

        Camera cam;
        Vector2 offset;
        float nextShake;
        bool mirror;
        float flashUntil, flashFrom, flashStrength;
        PixelTex veilTex;
        float lastVeil = -1f, nextVeilDraw;
        AudioSource[] loops;
        readonly Dictionary<Text, (float born, float life)> live = new Dictionary<Text, (float, float)>();

        const int VeilW = 160, VeilH = 90;

        /// <summary>화면 흔들림(월드 픽셀). MouseLookCamera가 시선에 더한다.</summary>
        public static Vector2 Offset => Instance != null ? Instance.offset : Vector2.zero;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            if (veil != null) veil.enabled = false;
            foreach (var t in whispers) if (t != null) t.enabled = false;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (PauseMenu.Paused) return;   // 일시정지: 흔들림·붉은 막·속삭임도 그 자리에 멈춘다
            // 흔들림: 30ms마다 새 자리(픽셀 단위)
            if (shake > 0.001f && !GameSettings.NoShake)   // 설정에서 화면 흔들림을 끄면 흔들리지 않는다
            {
                if (Time.unscaledTime >= nextShake)
                {
                    nextShake = Time.unscaledTime + 0.03f;
                    float a = 7f * shake;
                    offset = new Vector2(Mathf.Round(Random.Range(-a, a)), Mathf.Round(Random.Range(-a, a) * 0.7f));
                }
            }
            else offset = Vector2.zero;

            UpdateVeil();
            UpdateWhispers();
        }

        // ── 거울 ──
        public bool Mirror
        {
            get => mirror;
            set
            {
                mirror = value;
                if (cam == null) cam = GetComponent<Camera>();
                if (cam == null) return;
                cam.ResetProjectionMatrix();
                cam.ResetCullingMatrix();
                SetCanvasesWorld(mirror);
                // 화면은 늘 16:9(Letterbox): 지금 창 비율이 아니라 16:9로 시야를 만든다(창 비율이 다르면 가로로 확대돼 가장자리가 잘린다)
                float size = cam.orthographicSize, aspect = 16f / 9f;
                baseProjection = cam.orthographic
                    ? Matrix4x4.Ortho(-size * aspect, size * aspect, -size, size, cam.nearClipPlane, cam.farClipPlane)
                    : cam.projectionMatrix;
                if (mirror)
                {
                    cam.projectionMatrix = baseProjection * Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
                    UpdateCulling();
                }
            }
        }

        Matrix4x4 baseProjection;
        readonly List<Canvas> switchedCanvases = new List<Canvas>();

        /// <summary>
        /// 거울: 화면에 붙은 글자 판(점검표·영수증·말풍선 등, 카메라 기준 캔버스)은 뒤집힌 시야로 그리면 글자가 무너진다.
        /// 거울인 동안만 같은 자리·같은 크기의 "월드" 캔버스로 바꿔 그림과 함께 뒤집히게 한다(글씨도 거꾸로, 자리는 맞게).
        /// </summary>
        void SetCanvasesWorld(bool world)
        {
            if (world)
            {
                switchedCanvases.Clear();
                foreach (var c in GetComponentsInChildren<Canvas>(true))
                {
                    if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceCamera) continue;
                    float dist = c.planeDistance;
                    c.renderMode = RenderMode.WorldSpace;
                    var rt = (RectTransform)c.transform;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(640f, 360f);
                    rt.position = cam.transform.position + cam.transform.forward * dist;
                    rt.rotation = cam.transform.rotation;
                    Vector3 parentScale = rt.parent != null ? rt.parent.lossyScale : Vector3.one;
                    rt.localScale = new Vector3(1f / Mathf.Max(0.0001f, parentScale.x), 1f / Mathf.Max(0.0001f, parentScale.y), 1f);
                    var scaler = c.GetComponent<CanvasScaler>();
                    if (scaler != null) scaler.dynamicPixelsPerUnit = Mathf.Max(1f, cam.pixelHeight / 360f);
                    switchedCanvases.Add(c);
                }
            }
            else
            {
                foreach (var c in switchedCanvases)
                {
                    if (c == null) continue;
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                }
                switchedCanvases.Clear();
            }
        }

        /// <summary>뒤집힌 화면도 보이는 범위는 같다: 거르기는 뒤집기 전 시야로 해야 가장자리(시계 등)가 사라지지 않는다.</summary>
        void UpdateCulling()
        {
            if (mirror && cam != null) cam.cullingMatrix = baseProjection * cam.worldToCameraMatrix;
        }

        void LateUpdate() => UpdateCulling();

        // ── 붉은 막 ──
        /// <summary>순간 화면이 빨개졌다 빠진다(아플 때).</summary>
        public void Flash(float seconds = 0.6f, float strength = 0.85f)
        {
            flashFrom = Time.time;
            flashUntil = Time.time + seconds;
            flashStrength = GameSettings.ReduceFlicker ? strength * 0.3f : strength;   // 깜빡임 줄이기: 번쩍임을 약하게
        }

        void UpdateVeil()
        {
            if (veil == null) return;
            float f = Time.time < flashUntil ? flashStrength * (1f - (Time.time - flashFrom) / Mathf.Max(0.01f, flashUntil - flashFrom)) : 0f;
            float amount = Mathf.Max(veilAmount, 0f);
            bool on = amount > 0.001f || f > 0.001f;
            veil.enabled = on;
            if (!on) { lastVeil = -1f; return; }
            if (Time.time < nextVeilDraw && Mathf.Abs(amount + f - lastVeil) < 0.01f) return;
            nextVeilDraw = Time.time + 0.08f;
            lastVeil = amount + f;
            if (veilTex == null) veilTex = new PixelTex(VeilW, VeilH);
            float t = Time.time;
            float pulse = 0.04f * Mathf.Sin(t * 7.5f);
            for (int y = 0; y < VeilH; y++)
                for (int x = 0; x < VeilW; x++)
                {
                    float dx = (x + 0.5f - VeilW * 0.5f) / (VeilW * 0.5f), dy = (y + 0.5f - VeilH * 0.5f) / (VeilH * 0.5f);
                    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float n = PixelTex.Noise(x / 3 + (int)(t * 5f), y / 3, 61) - 0.5f;
                    // amount 0 → 가장자리만, 1 → 가운데까지 거의 다 덮는다
                    float edge = Mathf.Lerp(1.35f, 0.15f, amount) + pulse + n * 0.18f;
                    float a = Mathf.Clamp01((r - edge) / 0.45f + 0.15f) * Mathf.Lerp(0.55f, 0.92f, amount);
                    a = Mathf.Max(a, amount * 0.35f);       // 가운데도 붉게 흐려진다
                    a = Mathf.Max(a, f * (0.55f + 0.45f * Mathf.Clamp01(r)));
                    byte rr = (byte)Mathf.Lerp(150f, 70f, Mathf.Clamp01(r - 0.4f));
                    veilTex.Set(x, y, new Color32(rr, (byte)(rr * 0.06f), (byte)(rr * 0.08f), (byte)(Mathf.Clamp01(a) * 255f)));
                }
            veil.sprite = veilTex.ToSprite(new Vector2(0.5f, 0.5f));
            veil.transform.localScale = new Vector3(640f / VeilW, 360f / VeilH, 1f);
        }

        // ── 속삭임 글자 ──
        /// <summary>화면 (x, y)(가운데 기준 픽셀)에 글자 하나를 잠깐 띄운다. 빈 자리가 없으면 가장 오래된 것을 쓴다.</summary>
        public void Whisper(string text, Vector2 at, float life = 1.6f, int size = 10)
        {
            if (whispers.Length == 0) return;
            Text pick = null;
            float oldest = float.MaxValue;
            foreach (var w in whispers)
            {
                if (w == null) continue;
                if (!live.ContainsKey(w)) { pick = w; break; }
                if (live[w].born < oldest) { oldest = live[w].born; pick = w; }
            }
            if (pick == null) return;
            pick.text = Loc.T(text);
            pick.fontSize = size;
            pick.enabled = true;
            ((RectTransform)pick.transform).anchoredPosition = at;
            live[pick] = (Time.time, life);
        }

        public void ClearWhispers()
        {
            foreach (var w in whispers) if (w != null) w.enabled = false;
            live.Clear();
        }

        void UpdateWhispers()
        {
            if (live.Count == 0) return;
            var done = new List<Text>();
            foreach (var kv in live)
            {
                float k = (Time.time - kv.Value.born) / kv.Value.life;
                if (k >= 1f) { done.Add(kv.Key); continue; }
                // 스르륵 나타났다 흐려진다, 가끔 한 칸씩 떨린다
                float a = Mathf.Clamp01(k / 0.15f) * Mathf.Clamp01((1f - k) / 0.35f);
                Color c = kv.Key.color;
                kv.Key.color = new Color(c.r, c.g, c.b, a * 0.92f);
            }
            foreach (var t in done) { t.enabled = false; live.Remove(t); }
        }

        // ── 반복 소리 ──
        /// <summary>반복 소리 칸(0, 1)에 clip을 틀거나(volume>0) 끈다(clip null).</summary>
        public void Loop(int slot, AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying) return;
            if (loops == null)
            {
                loops = new AudioSource[3];
                for (int i = 0; i < loops.Length; i++)
                {
                    loops[i] = gameObject.AddComponent<AudioSource>();
                    loops[i].playOnAwake = false;
                    loops[i].loop = true;
                    loops[i].spatialBlend = 0f;
                }
            }
            if (slot < 0 || slot >= loops.Length) return;
            AudioSource s = loops[slot];
            if (clip == null) { s.Stop(); s.clip = null; return; }
            if (s.clip != clip) { s.clip = clip; s.Play(); }
            else if (!s.isPlaying) s.Play();
            s.volume = volume;
            s.pitch = pitch;
        }

        public void SetLoopVolume(int slot, float volume, float pitch = 1f)
        {
            if (loops == null || slot < 0 || slot >= loops.Length || loops[slot] == null) return;
            loops[slot].volume = volume;
            loops[slot].pitch = pitch;
        }

        /// <summary>모든 연출을 끈다(이벤트가 끝났거나 그날을 다시 할 때). keepMirror면 거울만 그대로 둔다(실패: 어둠 속에서 돌린다).</summary>
        public void ResetAll(bool keepMirror = false)
        {
            shake = 0f;
            veilAmount = 0f;
            flashUntil = 0f;
            offset = Vector2.zero;
            if (Mirror && !keepMirror) Mirror = false;
            ClearWhispers();
            if (loops != null) foreach (var s in loops) if (s != null) { s.Stop(); s.clip = null; }
            HandFx.Reset();
            if (veil != null) veil.enabled = false;
        }

        /// <summary>미리보기: 붉은 막을 그 정도로 그려 둔다(시간이 흐르지 않는 에디터용).</summary>
        public void PreviewVeil(float amount)
        {
            veilAmount = amount;
            nextVeilDraw = 0f;
            lastVeil = -1f;
            UpdateVeil();
        }

        /// <summary>미리보기: 속삭임 글자를 지금 그 자리에 바로 보이게.</summary>
        public void PreviewWhisper(int i, string text, Vector2 at, int size = 10)
        {
            if (i < 0 || i >= whispers.Length || whispers[i] == null) return;
            Text t = whispers[i];
            t.text = Loc.T(text);
            t.fontSize = size;
            t.enabled = true;
            Color c = t.color;
            t.color = new Color(c.r, c.g, c.b, 0.92f);
            ((RectTransform)t.transform).anchoredPosition = at;
        }
    }
}
