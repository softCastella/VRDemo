using System.Collections.Generic;
using UnityEngine;

public static class HandPoseHierarchyConverter
{
    public static Dictionary<string, BoneLocalPose> ToLocalHierarchyForRig(
        IReadOnlyList<HandBonePoseData> wristRelativeBones,
        HandPoseAnimationRig rig)
    {
        return rig == HandPoseAnimationRig.OculusLeftHand
            ? HandPoseAnimationPathUtility.ComputeOculusLocalPoses(wristRelativeBones)
            : ToLocalHierarchy(wristRelativeBones);
    }

    public static Dictionary<string, BoneLocalPose> ToLocalHierarchy(IReadOnlyList<HandBonePoseData> wristRelativeBones)
    {
        var localByName = new Dictionary<string, BoneLocalPose>();
        if (wristRelativeBones == null || wristRelativeBones.Count == 0)
            return localByName;

        var boneMap = new Dictionary<string, HandBonePoseData>();
        foreach (var bone in wristRelativeBones)
            boneMap[bone.boneName] = bone;

        var wristRelativeWorld = new Dictionary<string, Matrix4x4>();
        foreach (var bone in wristRelativeBones)
            wristRelativeWorld[bone.boneName] = GetWristRelativeMatrix(bone);

        foreach (var bone in wristRelativeBones)
        {
            var boneWorld = wristRelativeWorld[bone.boneName];
            Matrix4x4 localMatrix;

            if (!HandPoseBoneHierarchy.TryGetParentBoneName(bone.boneName, out var parentName) ||
                !boneMap.ContainsKey(parentName))
            {
                localMatrix = boneWorld;
            }
            else
            {
                var parentWorld = wristRelativeWorld[parentName];
                localMatrix = parentWorld.inverse * boneWorld;
            }

            localByName[bone.boneName] = new BoneLocalPose
            {
                localPosition = localMatrix.GetColumn(3),
                localRotation = localMatrix.rotation
            };
        }

        return localByName;
    }

    static Matrix4x4 GetWristRelativeMatrix(HandBonePoseData bone)
    {
        return Matrix4x4.TRS(bone.localPosition, bone.localRotation, Vector3.one);
    }
}

public struct BoneLocalPose
{
    public Vector3 localPosition;
    public Quaternion localRotation;
}
