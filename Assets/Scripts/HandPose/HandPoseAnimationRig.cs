public enum HandPoseAnimationRig
{
    MetaLeftHand,
    OculusLeftHand,
    MetaRightHand,
    OculusRightHand,
}

public static class HandPoseRigUtility
{
    public static bool IsOculusRig(HandPoseAnimationRig rig) =>
        rig is HandPoseAnimationRig.OculusLeftHand or HandPoseAnimationRig.OculusRightHand;

    public static bool IsRightRig(HandPoseAnimationRig rig) =>
        rig is HandPoseAnimationRig.MetaRightHand or HandPoseAnimationRig.OculusRightHand;

    public static HandPoseAnimationRig GetDefaultMetaRig(UnityEngine.XR.Hands.Handedness handedness) =>
        handedness == UnityEngine.XR.Hands.Handedness.Right
            ? HandPoseAnimationRig.MetaRightHand
            : HandPoseAnimationRig.MetaLeftHand;

    public static string GetWristBoneName(HandPoseAnimationRig rig) =>
        IsRightRig(rig) ? "R_Wrist" : "L_Wrist";
}
