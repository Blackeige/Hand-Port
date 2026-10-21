// Clean-room compatibility layer (NOT Oculus code). Same GUID / serialized names as the
// original OVRSkeleton so the original prefabs and scenes load unchanged.
// Bones are driven by Port/HandTrackingService (MediaPipe), not by OVRPlugin.
using System.Collections.Generic;
using UnityEngine;

public class OVRSkeleton : MonoBehaviour
{
    public enum SkeletonType { None = -1, HandLeft = 0, HandRight = 1 }

    // Numeric values identical to the original (serialized in HandCapsules.capsules[].boneId).
    public enum BoneId
    {
        Invalid = -1,
        Hand_Start = 0,
        Hand_WristRoot = 0, Hand_ForearmStub = 1,
        Hand_Thumb0 = 2, Hand_Thumb1 = 3, Hand_Thumb2 = 4, Hand_Thumb3 = 5,
        Hand_Index1 = 6, Hand_Index2 = 7, Hand_Index3 = 8,
        Hand_Middle1 = 9, Hand_Middle2 = 10, Hand_Middle3 = 11,
        Hand_Ring1 = 12, Hand_Ring2 = 13, Hand_Ring3 = 14,
        Hand_Pinky0 = 15, Hand_Pinky1 = 16, Hand_Pinky2 = 17, Hand_Pinky3 = 18,
        Hand_MaxSkinnable = 19,
        Hand_ThumbTip = 19, Hand_IndexTip = 20, Hand_MiddleTip = 21, Hand_RingTip = 22, Hand_PinkyTip = 23,
        Hand_End = 24,
    }

    public const int BoneCount = 24;

    [SerializeField] private SkeletonType _skeletonType = SkeletonType.None;
    [SerializeField] private bool _updateRootPose = false;
    [SerializeField] private bool _updateRootScale = false;
    [SerializeField] private bool _enablePhysicsCapsules = false;

    private readonly List<OVRBone> _bones = new List<OVRBone>(BoneCount);
    public bool IsInitialized { get; private set; }
    public bool IsDataValid { get; private set; }
    public bool IsDataHighConfidence { get; private set; }
    public IList<OVRBone> Bones { get { return _bones; } }
    public SkeletonType GetSkeletonType() { return _skeletonType; }

    private void Awake() { Build(); }

    private void Build()
    {
        if (IsInitialized) return;
        for (int i = 0; i < BoneCount; i++)
        {
            var go = new GameObject("Bone_" + ((BoneId)i));
            go.transform.SetParent(transform, false);
            _bones.Add(new OVRBone((BoneId)i, (short)(i == 0 ? -1 : 0), go.transform));
        }
        IsInitialized = true;
    }

    /// <summary>Called by HandTrackingService: world-space pose for each of the 24 bones.</summary>
    public void SetPose(Vector3[] worldPos, Quaternion[] worldRot, bool valid, bool highConfidence)
    {
        if (!IsInitialized) Build();
        if (valid && worldPos != null && worldRot != null)
        {
            for (int i = 0; i < BoneCount; i++)
                _bones[i].Transform.SetPositionAndRotation(worldPos[i], worldRot[i]);
        }
        IsDataValid = valid;
        IsDataHighConfidence = highConfidence;
    }

    public void SetValidity(bool valid, bool highConfidence) { IsDataValid = valid; IsDataHighConfidence = highConfidence; }
}

public class OVRBone
{
    public OVRSkeleton.BoneId Id { get; private set; }
    public short ParentBoneIndex { get; private set; }
    public Transform Transform { get; private set; }
    public OVRBone(OVRSkeleton.BoneId id, short parentBoneIndex, Transform trans)
    { Id = id; ParentBoneIndex = parentBoneIndex; Transform = trans; }
}
