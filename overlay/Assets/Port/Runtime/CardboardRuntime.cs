using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.XR.Cardboard;
using UnityEngine.Android;
#endif

namespace HandPlayground
{
    /// <summary>
    /// Head tracking + stereo come from the Cardboard XR plugin (XR Management loader, started automatically).
    /// This component: immersive/landscape/keep-awake, QR viewer-profile scan, recentre, camera permission,
    /// rotation-only head pose on the center-eye camera, eye height. Rotation only: NO positional (6DoF) tracking.
    /// </summary>
    public sealed class CardboardRuntime : MonoBehaviour
    {
        public OVRCameraRig rig; public HandTrackingService hands;
        public bool MenuRequested; bool _permAsked;

        void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            PortSettings.ApplyQuality();
            rig.trackingSpace.localPosition = new Vector3(0f, PortSettings.EyeHeight, 0f);
            rig.centerEyeAnchor.localPosition = Vector3.zero;
            var cam = rig.centerEyeCamera;
            var tpd = cam.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            if (tpd == null) tpd = cam.gameObject.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            tpd.SetPoseSource(UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRDevice, UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.Center);
            tpd.trackingType = UnityEngine.SpatialTracking.TrackedPoseDriver.TrackingType.RotationOnly;
            tpd.updateType = UnityEngine.SpatialTracking.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Api.HasDeviceParams()) Api.ScanDeviceParams();
#endif
        }

        public void Recenter()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Api.Recenter();
#endif
        }

        public void ScanViewer()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Api.ScanDeviceParams();
#endif
        }

        public void Quit() { Application.Quit(); }

        /// <summary>Returns false while the camera permission is missing (UI shows a readable error).</summary>
        public bool CameraPermissionGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
                return true;
#endif
            }
        }

        void Update()
        {
            rig.trackingSpace.localPosition = new Vector3(0f, PortSettings.EyeHeight, 0f);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_permAsked && !CameraPermissionGranted) { _permAsked = true; Permission.RequestUserPermission(Permission.Camera); }
            if (Api.IsGearButtonPressed) Api.ScanDeviceParams();
            if (Api.IsCloseButtonPressed) MenuRequested = true;
            if (Api.IsTriggerHeldPressed) Api.Recenter();
            if (Api.HasNewDeviceParams()) Api.ReloadDeviceParams();
            Api.UpdateScreenParams();
#endif
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) MenuRequested = true;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) { Screen.sleepTimeout = SleepTimeout.NeverSleep; if (hands != null && !_permAsked) { } }
        }
    }
}
