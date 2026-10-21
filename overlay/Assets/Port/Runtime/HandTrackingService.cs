using System;
using UnityEngine;

namespace HandPlayground
{
    /// <summary>
    /// Drives the OVRSkeleton shims from MediaPipe results. Tracking runs at its own rate (10-60 Hz, async on the Java side);
    /// every render frame the hand poses are re-expressed relative to the CURRENT head pose, so hands do not lag head turns.
    /// </summary>
    public sealed class HandTrackingService : MonoBehaviour
    {
        sealed class Hand
        {
            public OVRSkeleton skel; public int sign; public Collider[] colliders = new Collider[0];
            public readonly OneEuro3[] filt = new OneEuro3[HandFrame.Landmarks];
            public readonly Vector3[] lm = new Vector3[HandFrame.Landmarks];
            public readonly Vector3[] posT = new Vector3[OVRSkeleton.BoneCount], posC = new Vector3[OVRSkeleton.BoneCount], posW = new Vector3[OVRSkeleton.BoneCount];
            public readonly Quaternion[] rotT = new Quaternion[OVRSkeleton.BoneCount], rotC = new Quaternion[OVRSkeleton.BoneCount], rotW = new Quaternion[OVRSkeleton.BoneCount];
            public bool valid, everValid; public float lastSeen, lastFrameTime, reacquireUntil; public HandGesture gesture;
            public bool collidersOn = true;
        }

        public Transform head;                  // center-eye camera transform
        public float holdSeconds = 0.25f;       // keep last pose this long after detection loss
        Hand _left, _right; IHandSource _src; readonly HandFrame _frame = new HandFrame();
        TrackerStatus _status = TrackerStatus.Stopped; float _inferMs, _fps;
        public TrackerStatus Status { get { return _status; } }
        public bool LeftTracked { get { return _left != null && _left.valid; } }
        public bool RightTracked { get { return _right != null && _right.valid; } }
        public float InferenceMs { get { return _inferMs; } }
        public float AnalysisFps { get { return _fps; } }
        public HandGesture LeftGesture { get { return _left != null ? _left.gesture : default(HandGesture); } }
        public HandGesture RightGesture { get { return _right != null ? _right.gesture : default(HandGesture); } }
        public Vector3 LeftIndexTip { get { return _left != null ? _left.posW[(int)OVRSkeleton.BoneId.Hand_IndexTip] : Vector3.zero; } }
        public Vector3 RightIndexTip { get { return _right != null ? _right.posW[(int)OVRSkeleton.BoneId.Hand_IndexTip] : Vector3.zero; } }

        void Start()
        {
            foreach (var s in FindObjectsOfType<OVRSkeleton>())
            {
                var h = new Hand { skel = s };
                h.sign = s.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft ? -1 : 1;
                var ph = s.GetComponentInParent<PhysicsHand>();
                if (ph != null) h.colliders = ph.GetComponentsInChildren<Collider>(true);
                if (h.sign < 0) _left = h; else _right = h;
                SetColliders(h, false);   // hands stay physically absent until first detection
            }
            RestartSource();
        }

        public void RestartSource()
        {
            if (_src != null) { _src.Stop(); _src = null; }
            if (!PortSettings.TrackingEnabled) { _status = TrackerStatus.Stopped; return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            _src = new AndroidHandSource();
#else
            _src = new EditorSimHandSource();
#endif
            _src.Start();
        }

        void OnDisable() { if (_src != null) _src.Stop(); }
        void OnApplicationPause(bool paused)
        {
            if (paused) { if (_src != null) { _src.Stop(); _src = null; } }
            else RestartSource();
        }

        void Update()
        {
            if (head == null) return;
            float now = Time.unscaledTime;
            if (_src != null)
            {
                _status = _src.Status;
                if (_src.TryGetFrame(_frame)) { _inferMs = _frame.inferenceMs; _fps = _frame.analysisFps; Ingest(now); }
            }
            else _status = TrackerStatus.Stopped;
            Step(_left, now); Step(_right, now);
        }

        void Ingest(float now)
        {
            if (_frame.status != TrackerStatus.Running) return;
            int bestL = -1, bestR = -1;
            for (int s = 0; s < 2; s++)
            {
                if (!_frame.present[s]) continue;
                int actual = PortSettings.SwapHandedness ? 1 - _frame.label[s] : _frame.label[s];   // 0 left, 1 right
                if (actual == 0) { if (bestL < 0 || _frame.score[s] > _frame.score[bestL]) bestL = s; }
                else { if (bestR < 0 || _frame.score[s] > _frame.score[bestR]) bestR = s; }
            }
            Feed(_left, bestL, now); Feed(_right, bestR, now);
        }

        void Feed(Hand h, int slot, float now)
        {
            if (h == null || slot < 0) return;
            float dt = h.lastFrameTime > 0 ? Mathf.Clamp(now - h.lastFrameTime, 0.005f, 0.2f) : 0.033f;
            h.lastFrameTime = now;
            HandPoseSolver.ToCameraSpace(_frame, slot, PortSettings.DepthScale, h.lm);
            float s = PortSettings.Smoothing;                       // 0 = raw .. 1 = very smooth
            float minCut = Mathf.Lerp(8f, 0.8f, s), beta = Mathf.Lerp(0.2f, 12f, 1f - s) * 0.5f;
            if (!h.valid) for (int i = 0; i < h.filt.Length; i++) h.filt[i].Reset();
            for (int i = 0; i < HandFrame.Landmarks; i++) h.lm[i] = h.filt[i].Filter(h.lm[i], dt, minCut, beta);
            HandPoseSolver.Positions(h.lm, h.posT);
            HandPoseSolver.Rotations(h.lm, h.posT, h.sign, h.sign < 0 ? PortSettings.RollOffsetLeft : PortSettings.RollOffsetRight, h.rotT);
            h.gesture = GestureDetector.Evaluate(h.lm, h.gesture);
            if (!h.everValid) { Array.Copy(h.posT, h.posC, h.posT.Length); Array.Copy(h.rotT, h.rotC, h.rotT.Length); h.everValid = true; }
            else if (!h.valid) h.reacquireUntil = now + 0.2f;      // blend in instead of teleporting (protects the joints)
            h.valid = true; h.lastSeen = now;
        }

        void Step(Hand h, float now)
        {
            if (h == null) return;
            if (h.valid && now - h.lastSeen > holdSeconds) { h.valid = false; h.skel.SetValidity(false, false); SetColliders(h, false); return; }
            if (!h.valid) return;
            float tau = now < h.reacquireUntil ? 0.08f : Mathf.Lerp(0.004f, 0.03f, PortSettings.Smoothing);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / tau);
            Vector3 camPos = head.TransformPoint(PortSettings.CameraOffset); Quaternion camRot = head.rotation;
            for (int i = 0; i < OVRSkeleton.BoneCount; i++)
            {
                h.posC[i] = Vector3.Lerp(h.posC[i], h.posT[i], k);
                h.rotC[i] = Quaternion.Slerp(h.rotC[i], h.rotT[i], k);
                h.posW[i] = camPos + camRot * h.posC[i];
                h.rotW[i] = camRot * h.rotC[i];
            }
            h.skel.SetPose(h.posW, h.rotW, true, h.gesture.openIndex >= 0f);
            if (!h.collidersOn && now >= h.reacquireUntil - 0.1f) SetColliders(h, true);
        }

        static void SetColliders(Hand h, bool on)
        {
            h.collidersOn = on;
            for (int i = 0; i < h.colliders.Length; i++) if (h.colliders[i] != null) h.colliders[i].enabled = on;
        }
    }
}
