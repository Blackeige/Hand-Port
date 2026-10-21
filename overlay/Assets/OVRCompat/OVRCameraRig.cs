// Clean-room replacement. Finds/creates the same anchor hierarchy; only the center-eye camera
// stays enabled (Cardboard XR renders both eyes from a single camera).
using UnityEngine;

public class OVRCameraRig : MonoBehaviour
{
    public bool usePerEyeCameras = false;
    public bool useFixedUpdateForTracking = false;
    public bool disableEyeAnchorCameras = false;

    public Transform trackingSpace { get; private set; }
    public Transform leftEyeAnchor { get; private set; }
    public Transform centerEyeAnchor { get; private set; }
    public Transform rightEyeAnchor { get; private set; }
    public Camera centerEyeCamera { get; private set; }

    private Transform Ensure(Transform parent, string n)
    {
        var t = parent.Find(n);
        if (t == null) { var g = new GameObject(n); t = g.transform; t.SetParent(parent, false); }
        return t;
    }

    private void Awake()
    {
        trackingSpace = Ensure(transform, "TrackingSpace");
        leftEyeAnchor = Ensure(trackingSpace, "LeftEyeAnchor");
        centerEyeAnchor = Ensure(trackingSpace, "CenterEyeAnchor");
        rightEyeAnchor = Ensure(trackingSpace, "RightEyeAnchor");
        var l = leftEyeAnchor.GetComponent<Camera>(); if (l) l.enabled = false;
        var r = rightEyeAnchor.GetComponent<Camera>(); if (r) r.enabled = false;
        centerEyeCamera = centerEyeAnchor.GetComponent<Camera>();
        if (centerEyeCamera == null) centerEyeCamera = centerEyeAnchor.gameObject.AddComponent<Camera>();
        centerEyeCamera.enabled = true;
        centerEyeCamera.tag = "MainCamera";
    }
}
