using System.Collections.Generic;
using UnityEngine;

public static class HandPoseAnimationPathUtility
{
    static readonly Dictionary<string, string> s_MetaToOculusLeft = BuildMetaToOculusMap("L_", "b_l_");
    static readonly Dictionary<string, string> s_MetaToOculusRight = BuildMetaToOculusMap("R_", "b_r_");

    static readonly Dictionary<string, string> s_OculusParentLeft = BuildOculusParentMap("b_l_");
    static readonly Dictionary<string, string> s_OculusParentRight = BuildOculusParentMap("b_r_");

    static readonly HashSet<string> s_SkipMetaLeft = new()
    {
        "L_Palm",
        "L_IndexMetacarpal",
        "L_MiddleMetacarpal",
        "L_RingMetacarpal",
        "L_IndexTip",
        "L_MiddleTip",
        "L_RingTip",
        "L_LittleTip",
    };

    static readonly HashSet<string> s_SkipMetaRight = new()
    {
        "R_Palm",
        "R_IndexMetacarpal",
        "R_MiddleMetacarpal",
        "R_RingMetacarpal",
        "R_IndexTip",
        "R_MiddleTip",
        "R_RingTip",
        "R_LittleTip",
    };

    public static bool TryGetAnimationPath(string metaBoneName, HandPoseAnimationRig rig, out string animationPath)
    {
        animationPath = null;
        if (string.IsNullOrEmpty(metaBoneName) || ShouldSkipMetaBone(metaBoneName, rig))
            return false;

        return rig switch
        {
            HandPoseAnimationRig.MetaLeftHand or HandPoseAnimationRig.MetaRightHand
                => TryBuildMetaPath(metaBoneName, out animationPath),
            HandPoseAnimationRig.OculusLeftHand or HandPoseAnimationRig.OculusRightHand
                => TryBuildOculusPath(metaBoneName, rig, out animationPath),
            _ => false
        };
    }

    static bool ShouldSkipMetaBone(string metaBoneName, HandPoseAnimationRig rig)
    {
        return HandPoseRigUtility.IsRightRig(rig)
            ? s_SkipMetaRight.Contains(metaBoneName)
            : s_SkipMetaLeft.Contains(metaBoneName);
    }

    static bool TryBuildMetaPath(string metaBoneName, out string animationPath)
    {
        animationPath = null;
        if (metaBoneName is "L_Wrist" or "R_Wrist")
        {
            animationPath = metaBoneName;
            return true;
        }

        if (!HandPoseBoneHierarchy.TryGetParentBoneName(metaBoneName, out var parentName))
            return false;

        if (!TryBuildMetaPath(parentName, out var parentPath))
            return false;

        animationPath = $"{parentPath}/{metaBoneName}";
        return true;
    }

    static bool TryBuildOculusPath(string metaBoneName, HandPoseAnimationRig rig, out string animationPath)
    {
        animationPath = null;
        var map = HandPoseRigUtility.IsRightRig(rig) ? s_MetaToOculusRight : s_MetaToOculusLeft;
        if (!map.TryGetValue(metaBoneName, out var oculusBone))
            return false;

        var parentMap = HandPoseRigUtility.IsRightRig(rig) ? s_OculusParentRight : s_OculusParentLeft;
        return TryBuildOculusBonePath(oculusBone, parentMap, out animationPath);
    }

    static bool TryBuildOculusBonePath(
        string oculusBone,
        IReadOnlyDictionary<string, string> parentMap,
        out string animationPath)
    {
        animationPath = null;
        if (string.IsNullOrEmpty(oculusBone))
            return false;

        if (!parentMap.TryGetValue(oculusBone, out var parentBone) || string.IsNullOrEmpty(parentBone))
        {
            animationPath = oculusBone;
            return true;
        }

        if (!TryBuildOculusBonePath(parentBone, parentMap, out var parentPath))
            return false;

        animationPath = $"{parentPath}/{oculusBone}";
        return true;
    }

    public static Dictionary<string, BoneLocalPose> ComputeOculusLocalPoses(
        IReadOnlyList<HandBonePoseData> wristRelativeBones,
        HandPoseAnimationRig rig)
    {
        var map = HandPoseRigUtility.IsRightRig(rig) ? s_MetaToOculusRight : s_MetaToOculusLeft;
        var parentMap = HandPoseRigUtility.IsRightRig(rig) ? s_OculusParentRight : s_OculusParentLeft;
        var wristOculusBone = HandPoseRigUtility.IsRightRig(rig) ? "b_r_wrist" : "b_l_wrist";
        return ComputeOculusLocalPosesInternal(wristRelativeBones, map, parentMap, wristOculusBone);
    }

    static Dictionary<string, BoneLocalPose> ComputeOculusLocalPosesInternal(
        IReadOnlyList<HandBonePoseData> wristRelativeBones,
        IReadOnlyDictionary<string, string> metaToOculus,
        IReadOnlyDictionary<string, string> oculusParent,
        string wristOculusBone)
    {
        var result = new Dictionary<string, BoneLocalPose>();
        if (wristRelativeBones == null || wristRelativeBones.Count == 0)
            return result;

        var wristRel = new Dictionary<string, Matrix4x4>();
        foreach (var bone in wristRelativeBones)
            wristRel[bone.boneName] = Matrix4x4.TRS(bone.localPosition, bone.localRotation, Vector3.one);

        var oculusToMeta = new Dictionary<string, string>();
        foreach (var pair in metaToOculus)
            oculusToMeta[pair.Value] = pair.Key;

        foreach (var pair in metaToOculus)
        {
            var metaBone = pair.Key;
            var oculusBone = pair.Value;
            if (!wristRel.TryGetValue(metaBone, out _))
                continue;

            Matrix4x4 localMatrix;
            if (!oculusParent.TryGetValue(oculusBone, out var parentOculus) || string.IsNullOrEmpty(parentOculus))
            {
                localMatrix = wristRel[metaBone];
            }
            else if (parentOculus == wristOculusBone)
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

    static Dictionary<string, string> BuildMetaToOculusMap(string metaPrefix, string oculusPrefix)
    {
        return new Dictionary<string, string>
        {
            { $"{metaPrefix}Wrist", $"{oculusPrefix}wrist" },
            { $"{metaPrefix}ThumbMetacarpal", $"{oculusPrefix}thumb0" },
            { $"{metaPrefix}ThumbProximal", $"{oculusPrefix}thumb1" },
            { $"{metaPrefix}ThumbDistal", $"{oculusPrefix}thumb2" },
            { $"{metaPrefix}ThumbTip", $"{oculusPrefix}thumb3" },
            { $"{metaPrefix}IndexProximal", $"{oculusPrefix}index1" },
            { $"{metaPrefix}IndexIntermediate", $"{oculusPrefix}index2" },
            { $"{metaPrefix}IndexDistal", $"{oculusPrefix}index3" },
            { $"{metaPrefix}MiddleProximal", $"{oculusPrefix}middle1" },
            { $"{metaPrefix}MiddleIntermediate", $"{oculusPrefix}middle2" },
            { $"{metaPrefix}MiddleDistal", $"{oculusPrefix}middle3" },
            { $"{metaPrefix}RingProximal", $"{oculusPrefix}ring1" },
            { $"{metaPrefix}RingIntermediate", $"{oculusPrefix}ring2" },
            { $"{metaPrefix}RingDistal", $"{oculusPrefix}ring3" },
            { $"{metaPrefix}LittleMetacarpal", $"{oculusPrefix}pinky0" },
            { $"{metaPrefix}LittleProximal", $"{oculusPrefix}pinky1" },
            { $"{metaPrefix}LittleIntermediate", $"{oculusPrefix}pinky2" },
            { $"{metaPrefix}LittleDistal", $"{oculusPrefix}pinky3" },
        };
    }

    static Dictionary<string, string> BuildOculusParentMap(string oculusPrefix)
    {
        var wrist = $"{oculusPrefix}wrist";
        return new Dictionary<string, string>
        {
            { wrist, null },
            { $"{oculusPrefix}thumb0", wrist },
            { $"{oculusPrefix}thumb1", $"{oculusPrefix}thumb0" },
            { $"{oculusPrefix}thumb2", $"{oculusPrefix}thumb1" },
            { $"{oculusPrefix}thumb3", $"{oculusPrefix}thumb2" },
            { $"{oculusPrefix}index1", wrist },
            { $"{oculusPrefix}index2", $"{oculusPrefix}index1" },
            { $"{oculusPrefix}index3", $"{oculusPrefix}index2" },
            { $"{oculusPrefix}middle1", wrist },
            { $"{oculusPrefix}middle2", $"{oculusPrefix}middle1" },
            { $"{oculusPrefix}middle3", $"{oculusPrefix}middle2" },
            { $"{oculusPrefix}ring1", wrist },
            { $"{oculusPrefix}ring2", $"{oculusPrefix}ring1" },
            { $"{oculusPrefix}ring3", $"{oculusPrefix}ring2" },
            { $"{oculusPrefix}pinky0", wrist },
            { $"{oculusPrefix}pinky1", $"{oculusPrefix}pinky0" },
            { $"{oculusPrefix}pinky2", $"{oculusPrefix}pinky1" },
            { $"{oculusPrefix}pinky3", $"{oculusPrefix}pinky2" },
        };
    }
}
