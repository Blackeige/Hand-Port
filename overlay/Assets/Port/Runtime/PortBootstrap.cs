using UnityEngine;

namespace HandPlayground
{
    /// <summary>Attaches the port runtime to the original Main scene without editing the scene file.</summary>
    public static class PortBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var rig = Object.FindObjectOfType<OVRCameraRig>();
            if (rig == null) { Debug.Log("[HandPlayground] No OVRCameraRig in this scene; port runtime not started."); return; }
            var go = new GameObject("HandPlaygroundPort");
            var hands = go.AddComponent<HandTrackingService>();
            var cb = go.AddComponent<CardboardRuntime>();
            cb.rig = rig; cb.hands = hands;
            hands.head = rig.centerEyeAnchor;
            var menu = go.AddComponent<PortMenu>();
            menu.cardboard = cb; menu.hands = hands; menu.head = rig.centerEyeAnchor;
#if UNITY_EDITOR
            rig.centerEyeAnchor.gameObject.AddComponent<EditorHeadLook>();
#endif
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor only: hold right mouse to look around (the Cardboard XR head pose is unavailable in the Editor).</summary>
    public sealed class EditorHeadLook : MonoBehaviour
    {
        float yaw, pitch;
        void Update()
        {
            if (!Input.GetMouseButton(1)) return;
            yaw += Input.GetAxis("Mouse X") * 3f; pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3f, -85f, 85f);
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
#endif
}
