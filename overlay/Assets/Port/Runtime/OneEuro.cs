using UnityEngine;

namespace HandPlayground
{
    /// <summary>One-Euro filter (Casiez et al. 2012) for a Vector3. Allocation free.</summary>
    public struct OneEuro3
    {
        Vector3 _x, _dx; bool _init;
        static float Alpha(float cutoff, float dt) { float tau = 1f / (2f * Mathf.PI * cutoff); return 1f / (1f + tau / dt); }
        public Vector3 Filter(Vector3 v, float dt, float minCutoff, float beta, float dCutoff = 1f)
        {
            if (!_init || dt <= 1e-4f) { _x = v; _dx = Vector3.zero; _init = true; return v; }
            Vector3 d = (v - _x) / dt;
            float ad = Alpha(dCutoff, dt); _dx = Vector3.Lerp(_dx, d, ad);
            float cutoff = minCutoff + beta * _dx.magnitude;
            _x = Vector3.Lerp(_x, v, Alpha(cutoff, dt));
            return _x;
        }
        public void Reset() { _init = false; }
    }
}
