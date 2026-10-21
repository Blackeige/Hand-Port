using UnityEngine;

namespace HandPlayground
{
    public enum QualityPreset { Original = 0, Balanced = 1, Light = 2 }

    /// <summary>User settings persisted with PlayerPrefs (all reads/writes are cheap and allocation free).</summary>
    public static class PortSettings
    {
        const string P = "hp_";
        public static bool TrackingEnabled { get { return PlayerPrefs.GetInt(P + "track", 1) == 1; } set { PlayerPrefs.SetInt(P + "track", value ? 1 : 0); } }
        public static int InferenceFps { get { return PlayerPrefs.GetInt(P + "fps", 30); } set { PlayerPrefs.SetInt(P + "fps", Mathf.Clamp(value, 10, 60)); } }
        public static float Smoothing { get { return PlayerPrefs.GetFloat(P + "smooth", 0.5f); } set { PlayerPrefs.SetFloat(P + "smooth", Mathf.Clamp01(value)); } }
        public static float EyeHeight { get { return PlayerPrefs.GetFloat(P + "eye", 1.5f); } set { PlayerPrefs.SetFloat(P + "eye", Mathf.Clamp(value, 0.8f, 2.0f)); } }
        public static bool UseGpuDelegate { get { return PlayerPrefs.GetInt(P + "gpu", 0) == 1; } set { PlayerPrefs.SetInt(P + "gpu", value ? 1 : 0); } }
        public static float DepthScale { get { return PlayerPrefs.GetFloat(P + "depth", 1f); } set { PlayerPrefs.SetFloat(P + "depth", Mathf.Clamp(value, 0.5f, 2f)); } }
        /// <summary>MediaPipe labels assume a mirrored (selfie) image. The rear camera is not mirrored, so swap.</summary>
        public static bool SwapHandedness { get { return PlayerPrefs.GetInt(P + "swap", 1) == 1; } set { PlayerPrefs.SetInt(P + "swap", value ? 1 : 0); } }
        /// <summary>Rear camera position relative to the head (metres, head space). Device dependent!</summary>
        public static Vector3 CameraOffset
        {
            get { return new Vector3(PlayerPrefs.GetFloat(P + "cx", -0.03f), PlayerPrefs.GetFloat(P + "cy", 0.03f), PlayerPrefs.GetFloat(P + "cz", 0.04f)); }
            set { PlayerPrefs.SetFloat(P + "cx", value.x); PlayerPrefs.SetFloat(P + "cy", value.y); PlayerPrefs.SetFloat(P + "cz", value.z); }
        }
        public static QualityPreset Quality { get { return (QualityPreset)PlayerPrefs.GetInt(P + "q", 1); } set { PlayerPrefs.SetInt(P + "q", (int)value); } }
        public static float RollOffsetLeft { get { return PlayerPrefs.GetFloat(P + "rollL", 0f); } set { PlayerPrefs.SetFloat(P + "rollL", value); } }
        public static float RollOffsetRight { get { return PlayerPrefs.GetFloat(P + "rollR", 0f); } set { PlayerPrefs.SetFloat(P + "rollR", value); } }

        /// <summary>Original = untouched project values (0.005 s step, 10/30 solver iterations).</summary>
        public static void ApplyQuality()
        {
            switch (Quality)
            {
                case QualityPreset.Original:
                    Time.fixedDeltaTime = 0.005f; Physics.defaultSolverIterations = 10; Physics.defaultSolverVelocityIterations = 30;
                    QualitySettings.shadows = ShadowQuality.All; QualitySettings.pixelLightCount = 2; QualitySettings.antiAliasing = 0;
                    Application.targetFrameRate = 60; break;
                case QualityPreset.Balanced:
                    Time.fixedDeltaTime = 1f / 120f; Physics.defaultSolverIterations = 8; Physics.defaultSolverVelocityIterations = 16;
                    QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.pixelLightCount = 1; QualitySettings.antiAliasing = 0;
                    Application.targetFrameRate = 60; break;
                default:
                    Time.fixedDeltaTime = 1f / 90f; Physics.defaultSolverIterations = 6; Physics.defaultSolverVelocityIterations = 8;
                    QualitySettings.shadows = ShadowQuality.Disable; QualitySettings.pixelLightCount = 0; QualitySettings.antiAliasing = 0;
                    Application.targetFrameRate = 45; break;
            }
            // Prevents the physics catch-up "death spiral" on slow phones.
            Time.maximumDeltaTime = 0.05f;
            QualitySettings.vSyncCount = 0;
        }
    }
}
