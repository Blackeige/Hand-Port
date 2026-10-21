// Clean-room passive data holder. In the Main scene no OVRGrabber exists: objects are moved purely by
// physical contact with the finger capsules, so this component carries no behaviour.
using UnityEngine;
public class OVRGrabbable : MonoBehaviour
{
    [SerializeField] protected bool m_allowOffhandGrab = true;
    [SerializeField] protected bool m_snapPosition = false;
    [SerializeField] protected bool m_snapOrientation = false;
    [SerializeField] protected Transform m_snapOffset;
    [SerializeField] protected Collider[] m_grabPoints = null;
    public bool allowOffhandGrab { get { return m_allowOffhandGrab; } }
    public bool isGrabbed { get { return false; } }
    public bool snapPosition { get { return m_snapPosition; } }
    public bool snapOrientation { get { return m_snapOrientation; } }
    public Transform snapOffset { get { return m_snapOffset; } }
    public Collider[] grabPoints { get { return m_grabPoints; } }
}
