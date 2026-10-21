using UnityEngine;

namespace HandPlayground
{
#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Polls the Java bridge (CameraX + MediaPipe HandLandmarker). Uses raw JNI calls with cached method ids
    /// so the per-render-frame poll (an int read) allocates nothing; a float[] (~1 KB) is only fetched when a
    /// new inference result exists (<= inference rate).
    /// Snapshot layout (floats): [0]frameId [1]status [2]tsMs [3]inferMs [4]fovH [5]fovV [6]aspect [7]fps [8..9]rsv,
    /// then 2 slots of 130: present,label,score,pad, 21*3 normalized, 21*3 world.
    /// </summary>
    public sealed class AndroidHandSource : IHandSource
    {
        AndroidJavaObject _bridge; System.IntPtr _raw, _midFrameId, _midSnap;
        readonly UnityEngine.jvalue[] _noArgs = new UnityEngine.jvalue[0];
        int _lastFrame = -1; TrackerStatus _status = TrackerStatus.Stopped;
        public TrackerStatus Status { get { return _status; } }

        public void Start()
        {
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _bridge = new AndroidJavaObject("com.handplayground.HandTrackerBridge", act);
                }
                _raw = _bridge.GetRawObject();
                var cls = AndroidJNI.GetObjectClass(_raw);
                _midFrameId = AndroidJNI.GetMethodID(cls, "getFrameId", "()I");
                _midSnap = AndroidJNI.GetMethodID(cls, "getSnapshot", "()[F");
                _bridge.Call("start", 640, 480, PortSettings.UseGpuDelegate, PortSettings.InferenceFps, 2, 0.5f, 0.5f, 0.5f);
                _status = TrackerStatus.Starting;
            }
            catch (System.Exception e) { Debug.LogError("[HandPlayground] Bridge start failed: " + e); _status = TrackerStatus.Unavailable; }
        }

        public void Stop()
        {
            try { if (_bridge != null) { _bridge.Call("stop"); _bridge.Dispose(); } } catch (System.Exception e) { Debug.LogWarning(e); }
            _bridge = null; _status = TrackerStatus.Stopped; _lastFrame = -1;
        }

        public bool TryGetFrame(HandFrame f)
        {
            if (_bridge == null) return false;
            int id = AndroidJNI.CallIntMethod(_raw, _midFrameId, _noArgs);
            if (id == _lastFrame) return false;
            System.IntPtr arr = AndroidJNI.CallObjectMethod(_raw, _midSnap, _noArgs);
            if (arr == System.IntPtr.Zero) return false;
            float[] d = AndroidJNI.FromFloatArray(arr);
            AndroidJNI.DeleteLocalRef(arr);
            if (d == null || d.Length < 10 + 260) return false;
            _lastFrame = id;
            f.frameId = (int)d[0]; _status = f.status = (TrackerStatus)(int)d[1];
            f.timestampMs = d[2]; f.inferenceMs = d[3]; f.fovH = d[4]; f.fovV = d[5]; f.aspect = d[6]; f.analysisFps = d[7];
            f.handCount = 0;
            for (int s = 0; s < 2; s++)
            {
                int o = 10 + s * 130;
                bool p = d[o] > 0.5f; f.present[s] = p;
                if (!p) continue;
                f.handCount++; f.label[s] = (int)d[o + 1]; f.score[s] = d[o + 2];
                int n = o + 4, w = o + 4 + 63;
                for (int i = 0; i < HandFrame.Landmarks; i++)
                {
                    f.norm[s][i].Set(d[n + i * 3], d[n + i * 3 + 1], d[n + i * 3 + 2]);
                    f.world[s][i].Set(d[w + i * 3], d[w + i * 3 + 1], d[w + i * 3 + 2]);
                }
            }
            return true;
        }
    }
#endif

    /// <summary>Keyboard-driven fake hand for the Unity Editor (no camera): lets physics be tested on a PC.</summary>
    public sealed class EditorSimHandSource : IHandSource
    {
        float _next; int _id; public TrackerStatus Status { get { return TrackerStatus.Running; } }
        public void Start() { } public void Stop() { }
        public bool TryGetFrame(HandFrame f)
        {
            if (Time.unscaledTime < _next) return false;
            _next = Time.unscaledTime + 1f / 30f;
            float curl = Input.GetKey(KeyCode.Space) ? 1f : 0f;
            float mx = Input.mousePosition.x / Mathf.Max(1, Screen.width), my = Input.mousePosition.y / Mathf.Max(1, Screen.height);
            f.frameId = ++_id; f.status = TrackerStatus.Running; f.timestampMs = Time.unscaledTime * 1000f; f.inferenceMs = 1f;
            f.handCount = 1; f.present[0] = true; f.present[1] = false; f.label[0] = 1; f.score[0] = 1f;
            // Straight hand, palm toward +z (away). Finger joints along -y (image up), curled by Space.
            Vector3[] n = f.norm[0], w = f.world[0];
            Vector3 wr = new Vector3(mx, 1f - my + 0.12f, 0f);
            n[0] = wr; w[0] = new Vector3(0, 0.045f, 0);
            float[] fx = { 0.0f, -0.04f, -0.02f, 0.0f, 0.02f, 0.04f };
            // thumb 1..4
            for (int i = 1; i <= 4; i++) { n[i] = wr + new Vector3(-0.03f - 0.012f * i, -0.02f * i, 0); w[i] = new Vector3(-0.02f - 0.012f * i, 0.03f - 0.02f * i, 0); }
            for (int fi = 0; fi < 4; fi++)
            {
                float xx = -0.018f + fi * 0.012f; int b = 5 + fi * 4;
                for (int j = 0; j < 4; j++)
                {
                    float len = 0.025f * (j + 1);
                    float ang = curl * 1.4f * (j > 0 ? 1f : 0.2f);
                    n[b + j] = wr + new Vector3(xx * 0.9f, -(0.05f + len * (1f - curl * 0.6f)) * 0.9f, 0);
                    w[b + j] = new Vector3(xx, 0.02f - 0.025f * (j + 1) * (1f - curl * 0.5f), -curl * 0.015f * j);
                }
            }
            f.fovH = 65f; f.fovV = 50f; f.aspect = 4f / 3f; return true;
        }
    }
}
