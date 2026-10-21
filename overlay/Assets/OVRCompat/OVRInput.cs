// Clean-room stub: the phone has no Touch controllers. Hands are the only input.
using UnityEngine;

public static class OVRInput
{
    [System.Flags]
    public enum Controller
    {
        None = 0, LTouch = 1, RTouch = 2, Touch = 3, Remote = 4, Gamepad = 16,
        LHand = 32, RHand = 64, Hands = 96, Active = -2147483648, All = -1,
    }
    public enum Button { None = 0, One = 1, Two = 2, Three = 4, Four = 8, Any = -1 }
    public enum Touch { None = 0, One = 1, Two = 2, Three = 4, Four = 8, Any = -1 }
    public enum NearTouch { None = 0, PrimaryIndexTrigger = 1, SecondaryIndexTrigger = 2, Any = -1 }
    public enum Axis1D { None = 0, PrimaryIndexTrigger = 1, SecondaryIndexTrigger = 4, PrimaryHandTrigger = 16, SecondaryHandTrigger = 64, Any = -1 }

    public static bool Get(Button b) { return false; }
    public static bool Get(Touch t) { return false; }
    public static bool Get(NearTouch n) { return false; }
    public static float Get(Axis1D a) { return 0f; }
    public static Controller GetActiveController() { return Controller.Hands; }
}
