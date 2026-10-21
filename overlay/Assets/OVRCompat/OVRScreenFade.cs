// Clean-room: simple black fade-in on start (same serialized names as the original).
using UnityEngine;
public class OVRScreenFade : MonoBehaviour
{
    public float fadeTime = 2.0f;
    public Color fadeColor = new Color(0.01f, 0.01f, 0.01f, 1.0f);
    public bool fadeOnStart = true;
    public int renderQueue = 5000;
    private float _t; private Texture2D _tex; private bool _active;
    private void Start() { _tex = Texture2D.whiteTexture; _active = fadeOnStart; _t = 0f; }
    private void Update() { if (_active) { _t += Time.unscaledDeltaTime; if (_t >= fadeTime) _active = false; } }
    private void OnGUI()
    {
        if (!_active || fadeTime <= 0f) return;
        var c = fadeColor; c.a = Mathf.Clamp01(1f - _t / fadeTime);
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _tex);
        GUI.color = old;
    }
}
