using System.Collections.Generic;
using UnityEngine;

public static class HandPoseAnimationPathUtility
{
    static readonly Dictionary<string, string> s_MetaToOculusBone = new()
    {
        { "L_Wrist", "b_l_wrist" },
        { "L_ThumbMetacarpal", "b_l_thumb0" },
        { "L_ThumbProximal", "b_l_thumb1" },
        { "L_ThumbDistal", "b_l_thumb2" },
        { "L_ThumbTip", "b_l_thumb3" },
        { "L_IndexProximal", "b_l_index1" },
        { "L_IndexIntermediate", "b_l_index2" },
        { "L_IndexDistal", "b_l_index3" },
        { "L_MiddleProximal", "b_l_middle1" },
        { "L_MiddleIntermediate", "b_l_middle2" },
        { "L_MiddleDistal", "b_l_middle3" },
        { "L_RingProximal", "b_l_ring1" },
        { "L_RingIntermediate", "b_l_ring2" },
        { "L_RingDistal", "b_l_ring3" },
        { "L_LittleMetacarpal", "b_l_pinky0" },
        { "L_LittleProximal", "b_l_pinky1" },
        { "L_LittleIntermediate", "b_l_pinky2" },
        { "L_LittleDistal", "b_l_pinky3" }
    };

    static readonly Dictionary<string, string> s_OculusParent = new()
    {
        { "b_l_wrist", null },
        { "b_l_thumb0", "b_l_wrist" },
        { "b_l_thumb1", "b_l_thumb0" },
        { "b_l_thumb2", "b_l_thumb1" },
        { "b_l_thumb3", "b_l_thumb2" },
        { "b_l_index1", "b_l_wrist" },
        { "b_l_index2", "b_l_index1" },
        { "b_l_index3", "b_l_index2" },
        { "b_l_middle1", "b_l_wrist" },
        { "b_l_middle2", "b_l_middle1" },
        { "b_l_middle3", "b_l_middle2" },
        { "b_l_ring1", "b_l_wrist" },
        { "b_l_ring2", "b_l_ring1" },
        { "b_l_ring3", "b_l_ring2" },
        { "b_l_pinky0", "b_l_wrist" },
        { "b_l_pinky1", "b_l_pinky0" },
        { "b_l_pinky2", "b_l_pinky1" },
        { "b_l_pinky3", "b_l_pinky2" }
    };

    static readonly HashSet<string> s_SkipMetaBones = new()
    {
        "L_Palm",
        "L_IndexMetacarpal",
        "L_MiddleMetacarpal",
        "L_RingMetacarpal",
        "L_IndexTip",
        "L_MiddleTip",
        "L_RingTip",
        "L_LittleTip"
    };

    public static bool TryGetAnimationPath(string metaBoneName, HandPoseAnimationRig rig, out string animationPath)
    {
        animationPath = null;
        if (string.IsNullOrEmpty(metaBoneName) || s_SkipMetaBones.Contains(metaBoneName))
            return false;

        return rig switch
        {
            HandPoseAnimationRig.MetaLeftHand => TryBuildMetaPath(metaBoneName, out animationPath),
            HandPoseAnimationRig.OculusLeftHand => TryBuildOculusPath(metaBoneName, out animationPath),
            _ => false
        };
    }

    static bool TryBuildMetaPath(string metaBoneName, out string animationPath)
    {
        animationPath = null;
        if (metaBoneName == "L_Wrist")
        {
            animationPath = "L_Wrist";
            return true;
        }

        if (!HandPoseBoneHierarchy.TryGetParentBoneName(metaBoneName, out var parentName))
            return false;

        if (!TryBuildMetaPath(parentName, out var parentPath))
            return false;

        animationPath = $"{parentPath}/{metaBoneName}";
        return true;
    }

    static bool TryBuildOculusPath(string metaBoneName, out string animationPath)
    {
        animationPath = null;
        if (!s_MetaToOculusBone.TryGetValue(metaBoneName, out var oculusBone))
            return false;

        return TryBuildOculusBonePath(oculusBone, out animationPath);
    }

    static bool TryBuildOculusBonePath(string oculusBone, out string animationPath)
    {
        animationPath = null;
        if (string.IsNullOrEmpty(oculusBone))
            return false;

        if (!s_OculusParent.TryGetValue(oculusBone, out var parentBone) || string.IsNullOrEmpty(parentBone))
        {
            animationPath = oculusBone;
            return true;
        }

        if (!TryBuildOculusBonePath(parentBone, out var parentPath))
            return false;

        animationPath = $"{parentPath}/{oculusBone}";
        return true;
    }

    public static Dictionary<string, BoneLocalPose> ComputeOculusLocalPoses(IReadOnlyList<HandBonePoseData> wristRelativeBones)
    {
        var result = new Dictionary<string, BoneLocalPose>();
        if (wristRelativeBones == null || wristRelativeBones.Count == 0)
            return result;

        var wristRel = new Dictionary<string, Matrix4x4>();
        foreach (var bone in wristRelativeBones)
            wristRel[bone.boneName] = Matrix4x4.TRS(bone.localPosition, bone.localRotation, Vector3.one);

        var oculusToMeta = new Dictionary<string, string>();
        foreach (var pair in s_MetaToOculusBone)
            oculusToMeta[pair.Value] = pair.Key;

        foreach (var pair in s_MetaToOculusBone)
        {
            var metaBone = pair.Key;
            var oculusBone = pair.Value;
            if (!wristRel.TryGetValue(metaBone, out _))
                continue;

            Matrix4x4 localMatrix;
            if (!s_OculusParent.TryGetValue(oculusBone, out var parentOculus) || string.IsNullOrEmpty(parentOculus))
            {
                localMatrix = wristRel[metaBone];
            }
            else if (parentOculus == "b_l_wrist")
            {
                localMatrix = wristRel[metaBone];
            }
            else if (!oculusToMeta.TryGetValue(parentOculus, out var parentMeta) ||
                     !wristRel.TryGetValue(parentMeta, out var parentMatrix))
            {
                localMatrix = wristRel[metaBone];
            }
            else
            {
                localMatrix = parentMatrix.inverse * wristRel[metaBone];
            }

            result[metaBone] = new BoneLocalPose
            {
                localPosition = localMatrix.GetColumn(3),
                localRotation = localMatrix.rotation
            };
        }

        return result;
    }
}
