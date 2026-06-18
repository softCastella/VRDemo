using System.Collections.Generic;
using UnityEngine.XR.Hands;

public static class HandPoseBoneHierarchy
{
    public static bool TryGetParentBoneName(string boneName, out string parentName)
    {
        parentName = null;
        if (string.IsNullOrEmpty(boneName))
            return false;

        if (boneName is "L_Wrist" or "R_Wrist")
            return false;

        if (boneName is "L_Palm" or "R_Palm")
        {
            parentName = boneName.StartsWith("L_") ? "L_Wrist" : "R_Wrist";
            return true;
        }

        if (TryGetThumbParentBoneName(boneName, out parentName))
            return true;

        if (boneName.EndsWith("Tip"))
        {
            parentName = boneName[..^3] + "Distal";
            return true;
        }

        if (boneName.EndsWith("Distal"))
        {
            parentName = boneName[..^6] + "Intermediate";
            return true;
        }

        if (boneName.EndsWith("Intermediate"))
        {
            parentName = boneName[..^12] + "Proximal";
            return true;
        }

        if (boneName.EndsWith("Proximal"))
        {
            parentName = boneName[..^8] + "Metacarpal";
            return true;
        }

        if (boneName.EndsWith("Metacarpal"))
        {
            parentName = boneName.StartsWith("L_") ? "L_Wrist" : "R_Wrist";
            return true;
        }

        parentName = boneName.StartsWith("L_") ? "L_Wrist" : "R_Wrist";
        return true;
    }

    static bool TryGetThumbParentBoneName(string boneName, out string parentName)
    {
        parentName = null;

        if (boneName is "L_ThumbMetacarpal")
        {
            parentName = "L_Wrist";
            return true;
        }

        if (boneName is "L_ThumbProximal")
        {
            parentName = "L_ThumbMetacarpal";
            return true;
        }

        if (boneName is "L_ThumbDistal")
        {
            parentName = "L_ThumbProximal";
            return true;
        }

        if (boneName is "L_ThumbTip")
        {
            parentName = "L_ThumbDistal";
            return true;
        }

        if (boneName is "R_ThumbMetacarpal")
        {
            parentName = "R_Wrist";
            return true;
        }

        if (boneName is "R_ThumbProximal")
        {
            parentName = "R_ThumbMetacarpal";
            return true;
        }

        if (boneName is "R_ThumbDistal")
        {
            parentName = "R_ThumbProximal";
            return true;
        }

        if (boneName is "R_ThumbTip")
        {
            parentName = "R_ThumbDistal";
            return true;
        }

        return false;
    }

    public static bool TryGetWristBoneName(IReadOnlyList<HandBonePoseData> bones, out string wristBoneName)
    {
        wristBoneName = null;
        foreach (var bone in bones)
        {
            if (bone.jointId == XRHandJointID.Wrist ||
                bone.boneName is "L_Wrist" or "R_Wrist")
            {
                wristBoneName = bone.boneName;
                return true;
            }
        }

        return false;
    }
}
