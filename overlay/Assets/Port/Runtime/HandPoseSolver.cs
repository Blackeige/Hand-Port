using UnityEngine;

namespace HandPlayground
{
    /// <summary>
    /// Converts MediaPipe landmarks (monocular, 2.5D) into the 24 OVR-style bone poses.
    /// Camera space = Unity camera convention (x right, y up, z forward). Depth is ESTIMATED from the apparent size
    /// of the palm (index-MCP to pinky-MCP) versus its metric size from MediaPipe world landmarks: it is not measured.
    /// Bone convention: +X along the bone toward its child; +Y toward the back of the hand; Z = X cross Y.
    /// </summary>
    public static class HandPoseSolver
    {
        // OVR bone id -> MediaPipe landmark providing the joint position (-1 = interpolated, see Positions()).
        static readonly int[] Lm = { 0, 0, -1, 1, 2, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15, -2, 17, 18, 19, 4, 8, 12, 16, 20 };
        // OVR bone id -> OVR bone id used as the "child" to aim +X at (-1 = reuse parent direction).
        static readonly int[] Child = { 9, 9, 3, 4, 5, 19, 7, 8, 20, 10, 11, 21, 13, 14, 22, 16, 17, 18, 23, -1, -1, -1, -1, -1 };
        static readonly int[] Parent = { -1, 0, 0, 2, 3, 4, 0, 6, 7, 0, 9, 10, 0, 12, 13, 0, 15, 16, 17, 5, 8, 11, 14, 18 };

        /// <summary>Landmarks (camera space) from a frame hand. <paramref name="outCam"/> has 21 entries.</summary>
        public static void ToCameraSpace(HandFrame f, int slot, float depthScale, Vector3[] outCam)
        {
            Vector3[] n = f.norm[slot], w = f.world[slot];
            float tanH = Mathf.Tan(f.fovH * 0.5f * Mathf.Deg2Rad), tanV = Mathf.Tan(f.fovV * 0.5f * Mathf.Deg2Rad);
            float dW = (w[5] - w[17]).magnitude;
            Vector2 a = new Vector2(n[5].x * f.aspect, n[5].y), b = new Vector2(n[17].x * f.aspect, n[17].y);
            float dN = Mathf.Max(1e-3f, (a - b).magnitude);                 // in units of image height
            float fy = 0.5f / tanV;                                         // focal length in image-height units
            float zw = Mathf.Clamp(fy * dW / dN * depthScale, 0.15f, 1.2f);
            for (int i = 0; i < HandFrame.Landmarks; i++)
            {
                float z = Mathf.Clamp(zw + (w[i].z - w[0].z) * depthScale, 0.08f, 1.5f);
                outCam[i].Set((n[i].x - 0.5f) * 2f * tanH * z, -(n[i].y - 0.5f) * 2f * tanV * z, z);
            }
        }

        public static void Positions(Vector3[] lm, Vector3[] bonePos)
        {
            for (int i = 0; i < OVRSkeleton.BoneCount; i++)
            {
                int l = Lm[i];
                if (l >= 0) bonePos[i] = lm[l];
                else if (l == -1) bonePos[i] = Vector3.Lerp(lm[0], lm[1], 0.5f);     // thumb trapezium
                else bonePos[i] = Vector3.Lerp(lm[0], lm[17], 0.5f);                 // pinky metacarpal
            }
        }

        /// <param name="handSign">+1 right hand, -1 left hand (flips the palm normal).</param>
        public static void Rotations(Vector3[] lm, Vector3[] bonePos, int handSign, float rollDeg, Quaternion[] outRot)
        {
            Vector3 dorsal = handSign * Vector3.Cross(lm[5] - lm[0], lm[17] - lm[0]);
            if (dorsal.sqrMagnitude < 1e-8f) dorsal = Vector3.back;
            dorsal.Normalize();
            for (int i = 0; i < OVRSkeleton.BoneCount; i++)
            {
                Vector3 dir;
                int c = Child[i];
                if (c >= 0) dir = bonePos[c] - bonePos[i];
                else { int p = Parent[i]; dir = bonePos[i] - bonePos[p]; }
                if (dir.sqrMagnitude < 1e-10f) dir = Vector3.up;
                Vector3 x = dir.normalized;
                Vector3 y = dorsal - x * Vector3.Dot(dorsal, x);
                if (y.sqrMagnitude < 1e-6f) y = Vector3.Cross(x, Mathf.Abs(x.x) > 0.9f ? Vector3.up : Vector3.right);
                y.Normalize();
                Vector3 z = Vector3.Cross(x, y);
                // Rotation whose local axes are (x,y,z): build from forward=z, up=y.
                Quaternion q = Quaternion.LookRotation(z, y);
                outRot[i] = rollDeg != 0f ? q * Quaternion.AngleAxis(rollDeg, Vector3.right) : q;
            }
        }
    }

    public struct HandGesture
    {
        public float openIndex, openMiddle, openRing, openPinky, pinch;
        public bool grasp, pinching;
    }

    public static class GestureDetector
    {
        static float Open(Vector3[] p, int mcp, int tip)
        {
            float r = (p[tip] - p[0]).magnitude / Mathf.Max(1e-4f, (p[mcp] - p[0]).magnitude);
            return Mathf.Clamp01((r - 1.0f) / 0.8f);
        }
        /// <summary>Applies hysteresis using the previous gesture. Input: camera-space landmarks.</summary>
        public static HandGesture Evaluate(Vector3[] p, HandGesture prev)
        {
            HandGesture g = new HandGesture();
            g.openIndex = Open(p, 5, 8); g.openMiddle = Open(p, 9, 12); g.openRing = Open(p, 13, 16); g.openPinky = Open(p, 17, 20);
            float mean = (g.openIndex + g.openMiddle + g.openRing + g.openPinky) * 0.25f;
            g.grasp = prev.grasp ? mean < 0.45f : mean < 0.25f;
            float span = Mathf.Max(1e-4f, (p[9] - p[0]).magnitude);
            g.pinch = (p[4] - p[8]).magnitude / span;
            g.pinching = prev.pinching ? g.pinch < 0.45f : g.pinch < 0.30f;
            return g;
        }
    }
}
