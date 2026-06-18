using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class HandPoseNormalizer
{
    const string LeftWristBoneName = "L_Wrist";
    const string RightWristBoneName = "R_Wrist";

    public static List<HandBonePoseData> NormalizeToWristOrigin(
        IReadOnlyList<(Transform transform, XRHandJointID jointId)> joints)
    {
        var bones = new List<HandBonePoseData>();
        if (joints == null || joints.Count == 0)
            return bones;

        if (!TryFindWristTransform(joints, out var wristTransform))
            wristTransform = joints[0].transform;

        var wristPosition = wristTransform.position;
        var inverseWristRotation = Quaternion.Inverse(wristTransform.rotation);

        foreach (var (transform, jointId) in joints)
        {
            if (transform == null)
                continue;

            var relativePosition = inverseWristRotation * (transform.position - wristPosition);
            var relativeRotation = inverseWristRotation * transform.rotation;

            bones.Add(new HandBonePoseData
            {
                jointId = jointId,
                boneName = transform.name,
                localPosition = relativePosition,
                localRotation = relativeRotation
            });
        }

        return bones;
    }

    static bool TryFindWristTransform(
        IReadOnlyList<(Transform transform, XRHandJointID jointId)> joints,
        out Transform wristTransform)
    {
        wristTransform = null;

        foreach (var (transform, jointId) in joints)
        {
            if (transform == null)
                continue;

            if (jointId == XRHandJointID.Wrist ||
                transform.name == LeftWristBoneName ||
                transform.name == RightWristBoneName)
            {
                wristTransform = transform;
                return true;
            }
        }

        return false;
    }

    public static List<HandBonePoseData> NormalizeLegacyBones(IReadOnlyList<HandBonePoseData> legacyBones)
    {
        var normalized = new List<HandBonePoseData>();
        if (legacyBones == null || legacyBones.Count == 0)
            return normalized;

        if (!HandPoseBoneHierarchy.TryGetWristBoneName(legacyBones, out var wristBoneName))
            wristBoneName = legacyBones[0].boneName;

        var boneMap = new Dictionary<string, HandBonePoseData>();
        foreach (var bone in legacyBones)
            boneMap[bone.boneName] = bone;

        var worldByName = new Dictionary<string, Matrix4x4>();
        foreach (var bone in legacyBones)
            worldByName[bone.boneName] = ComputeLegacyWorldMatrix(bone.boneName, boneMap, worldByName);

        if (!worldByName.TryGetValue(wristBoneName, out var wristWorld))
            return normalized;

        var wristPosition = wristWorld.GetColumn(3);
        var inverseWristRotation = Quaternion.Inverse(wristWorld.rotation);

        foreach (var bone in legacyBones)
        {
            if (!worldByName.TryGetValue(bone.boneName, out var boneWorld))
                continue;

            normalized.Add(new HandBonePoseData
            {
                jointId = bone.jointId,
                boneName = bone.boneName,
                localPosition = inverseWristRotation * (boneWorld.GetColumn(3) - wristPosition),
                localRotation = inverseWristRotation * boneWorld.rotation
            });
        }

        return normalized;
    }

    static Matrix4x4 ComputeLegacyWorldMatrix(
        string boneName,
        IReadOnlyDictionary<string, HandBonePoseData> boneMap,
        IDictionary<string, Matrix4x4> cache)
    {
        if (cache.TryGetValue(boneName, out var cached))
            return cached;

        if (!boneMap.TryGetValue(boneName, out var bone))
            return Matrix4x4.identity;

        var localMatrix = Matrix4x4.TRS(bone.localPosition, bone.localRotation, Vector3.one);
        Matrix4x4 worldMatrix;

        if (HandPoseBoneHierarchy.TryGetParentBoneName(boneName, out var parentName) &&
            boneMap.ContainsKey(parentName))
        {
            worldMatrix = ComputeLegacyWorldMatrix(parentName, boneMap, cache) * localMatrix;
        }
        else
        {
            worldMatrix = localMatrix;
        }

        cache[boneName] = worldMatrix;
        return worldMatrix;
    }
}
