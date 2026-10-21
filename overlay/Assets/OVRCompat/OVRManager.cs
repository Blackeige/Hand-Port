// Clean-room no-op (Quest runtime manager has no equivalent on a phone).
using UnityEngine;
public class OVRManager : MonoBehaviour
{
    public static OVRManager instance { get; private set; }
    public static bool isHmdPresent { get { return true; } }
    private void Awake() { instance = this; }
}
