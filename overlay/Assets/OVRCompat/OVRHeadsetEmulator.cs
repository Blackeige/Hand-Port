// Clean-room no-op (desktop headset emulation is not needed; see Port/EditorHeadLook).
using UnityEngine;
public class OVRHeadsetEmulator : MonoBehaviour
{
    public int opMode = 1;
    public bool resetHmdPoseOnRelease = true;
    public bool resetHmdPoseByMiddleMouseButton = true;
}
