using System;
using UnityEngine;
using UnityEngine.UI;

namespace HandPlayground
{
    /// <summary>
    /// Gaze-driven VR menu (look at a button ~1.2 s, or use the Cardboard button / screen tap to open the menu).
    /// World-space canvas, placed 1.6 m in front of the head, text kept well inside the lens field of view.
    /// </summary>
    public sealed class PortMenu : MonoBehaviour
    {
        public CardboardRuntime cardboard; public HandTrackingService hands; public Transform head;
        Canvas _canvas; Text _status; Image _reticle; bool _open; float _dwell; Button2 _focus;
        Font _font; float _fpsT; int _fpsN; float _fps; Text _hud; Transform _hudT;

        sealed class Button2 { public RectTransform rt; public Image bg; public Text label; public Action act; public Func<string> text; }
        readonly System.Collections.Generic.List<Button2> _btns = new System.Collections.Generic.List<Button2>();

        void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            BuildHud(); BuildMenu(); SetOpen(true);
        }

        void BuildHud()
        {
            var go = new GameObject("HUD"); go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, -0.28f, 1.2f);
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 80); go.transform.localScale = Vector3.one * 0.0012f;
            _hud = MakeText(go.transform, "", 34, TextAnchor.MiddleCenter, new Vector2(600, 80)); _hudT = go.transform;
        }

        Text MakeText(Transform p, string s, int size, TextAnchor a, Vector2 sz)
        {
            var g = new GameObject("t"); g.transform.SetParent(p, false);
            var t = g.AddComponent<Text>(); t.font = _font; t.text = s; t.fontSize = size; t.alignment = a; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.sizeDelta = sz; return t;
        }

        void AddButton(string name, Func<string> text, Action act, int row)
        {
            var g = new GameObject(name); g.transform.SetParent(_canvas.transform, false);
            var img = g.AddComponent<Image>(); img.color = new Color(0.1f, 0.1f, 0.14f, 0.85f);
            var rt = g.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(560, 64); rt.anchoredPosition = new Vector2(0, 190 - row * 76);
            var lab = MakeText(g.transform, text(), 30, TextAnchor.MiddleCenter, new Vector2(540, 60));
            _btns.Add(new Button2 { rt = rt, bg = img, label = lab, act = act, text = text });
        }

        void BuildMenu()
        {
            var go = new GameObject("Menu"); _canvas = go.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(640, 560); go.transform.localScale = Vector3.one * 0.0024f;
            var bgi = new GameObject("bg"); bgi.transform.SetParent(go.transform, false);
            var im = bgi.AddComponent<Image>(); im.color = new Color(0, 0, 0, 0.55f); im.rectTransform.sizeDelta = new Vector2(640, 560);
            _status = MakeText(go.transform, "", 26, TextAnchor.UpperCenter, new Vector2(600, 110)); _status.rectTransform.anchoredPosition = new Vector2(0, 232);
            AddButton("resume", () => "Reprendre", () => SetOpen(false), 1);
            AddButton("recenter", () => "Recentrer la vue", () => cardboard.Recenter(), 2);
            AddButton("track", () => "Suivi des mains : " + (PortSettings.TrackingEnabled ? "ON" : "OFF"), () => { PortSettings.TrackingEnabled = !PortSettings.TrackingEnabled; hands.RestartSource(); }, 3);
            AddButton("quality", () => "Qualité : " + PortSettings.Quality, () => { PortSettings.Quality = (QualityPreset)(((int)PortSettings.Quality + 1) % 3); PortSettings.ApplyQuality(); }, 4);
            AddButton("fps", () => "Fréquence suivi : " + PortSettings.InferenceFps + " Hz", () => { PortSettings.InferenceFps = PortSettings.InferenceFps >= 30 ? 15 : PortSettings.InferenceFps + 15; hands.RestartSource(); }, 5);
            AddButton("smooth", () => "Lissage : " + Mathf.RoundToInt(PortSettings.Smoothing * 100) + " %", () => PortSettings.Smoothing = PortSettings.Smoothing >= 0.9f ? 0.1f : PortSettings.Smoothing + 0.2f, 6);
            AddButton("height", () => "Hauteur des yeux : " + PortSettings.EyeHeight.ToString("0.00") + " m", () => PortSettings.EyeHeight = PortSettings.EyeHeight >= 1.8f ? 1.0f : PortSettings.EyeHeight + 0.1f, 7);
            AddButton("viewer", () => "Scanner le QR du viewer", () => cardboard.ScanViewer(), 8);
            AddButton("quit", () => "Quitter", () => cardboard.Quit(), 9);
            var r = new GameObject("reticle"); r.transform.SetParent(head, false); r.transform.localPosition = new Vector3(0, 0, 1.5f);
            var rc = r.AddComponent<Canvas>(); rc.renderMode = RenderMode.WorldSpace; r.GetComponent<RectTransform>().sizeDelta = new Vector2(30, 30); r.transform.localScale = Vector3.one * 0.0015f;
            _reticle = r.AddComponent<Image>(); _reticle.sprite = CircleSprite(); _reticle.color = new Color(1, 1, 1, 0.9f); _reticle.type = Image.Type.Filled; _reticle.fillMethod = Image.FillMethod.Radial360; _reticle.fillAmount = 1f;
        }

        static Sprite CircleSprite()
        {
            const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)); px[y * n + x] = new Color32(255, 255, 255, (byte)(d < n / 2f && d > n / 2f - 10 ? 255 : 0)); }
            tex.SetPixels32(px); tex.Apply(); return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void SetOpen(bool open)
        {
            _open = open; _canvas.gameObject.SetActive(open);
            if (open) { _canvas.transform.position = head.position + Flat(head.forward) * 1.6f + Vector3.up * -0.05f; _canvas.transform.rotation = Quaternion.LookRotation(Flat(head.forward)); }
            _dwell = 0; _focus = null;
        }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized; }

        string StatusLine()
        {
            string cam = hands.Status == TrackerStatus.Running ? "caméra OK" : hands.Status == TrackerStatus.PermissionDenied ? "AUTORISATION CAMÉRA REFUSÉE" :
                hands.Status == TrackerStatus.CameraError ? "ERREUR CAMÉRA" : hands.Status == TrackerStatus.ModelError ? "ERREUR MODÈLE" : hands.Status.ToString();
            return string.Format("{0}  |  G:{1} D:{2}\n{3:0} FPS rendu  |  suivi {4:0} Hz, {5:0} ms", cam, hands.LeftTracked ? "●" : "○", hands.RightTracked ? "●" : "○", _fps, hands.AnalysisFps, hands.InferenceMs);
        }

        void Update()
        {
            _fpsT += Time.unscaledDeltaTime; _fpsN++; if (_fpsT >= 0.5f) { _fps = _fpsN / _fpsT; _fpsT = 0; _fpsN = 0; }
            if (cardboard.MenuRequested) { cardboard.MenuRequested = false; SetOpen(!_open); }
            _hud.text = _open ? "" : StatusLine().Replace("\n", "  ");
            _reticle.gameObject.SetActive(_open);
            if (!_open) return;
            _status.text = StatusLine();
            Plane pl = new Plane(-_canvas.transform.forward, _canvas.transform.position); Ray ray = new Ray(head.position, head.forward);
            Button2 hit = null; float d;
            if (pl.Raycast(ray, out d))
            {
                Vector3 p = ray.GetPoint(d);
                for (int i = 0; i < _btns.Count; i++) { Vector3 l = _btns[i].rt.InverseTransformPoint(p); if (_btns[i].rt.rect.Contains(new Vector2(l.x, l.y))) { hit = _btns[i]; break; } }
            }
            for (int i = 0; i < _btns.Count; i++) { _btns[i].bg.color = _btns[i] == hit ? new Color(0.2f, 0.4f, 0.7f, 0.95f) : new Color(0.1f, 0.1f, 0.14f, 0.85f); _btns[i].label.text = _btns[i].text(); }
            if (hit != _focus) { _focus = hit; _dwell = 0; }
            if (_focus != null) { _dwell += Time.unscaledDeltaTime; if (_dwell >= 1.2f) { var a = _focus.act; _dwell = 0; a(); } }
            _reticle.fillAmount = _focus != null ? Mathf.Clamp01(_dwell / 1.2f) : 1f;
        }
    }
}
