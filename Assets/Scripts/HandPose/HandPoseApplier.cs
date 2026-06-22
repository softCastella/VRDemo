using System.Collections.Generic;
using UnityEngine;

public static class HandPoseApplier
{
    public const string LeftWristBoneName = "L_Wrist";
    public const string RightWristBoneName = "R_Wrist";
    public static readonly Vector3 DefaultPreviewOrigin = Vector3.zero;

    public static Dictionary<string, Transform> BuildBoneMap(Transform skeletonRoot)
    {
        var map = new Dictionary<string, Transform>();
        if (skeletonRoot == null)
            return map;

        foreach (var bone in skeletonRoot.GetComponentsInChildren<Transform>(true))
            map[bone.name] = bone;

        return map;
    }

    public static int Apply(
        HandPoseData pose,
        Transform skeletonRoot,
        Vector3 previewOrigin = default,
        bool applyPosition = true,
        bool applyRotation = true)
    {
        if (pose == null || skeletonRoot == null)
            return 0;

        return pose.IsWristAtOrigin()
            ? ApplyWristRelative(pose, skeletonRoot, previewOrigin, applyPosition, applyRotation)
            : ApplyLegacyLocal(pose, skeletonRoot, applyPosition, applyRotation);
    }

    public static int ApplyWristRelative(
        HandPoseData pose,
        Transform skeletonRoot,
        Vector3 previewOrigin = default,
        bool applyPosition = true,
        bool applyRotation = true)
    {
        if (pose == null || skeletonRoot == null)
            return 0;

        skeletonRoot.SetPositionAndRotation(previewOrigin, Quaternion.identity);

        var boneMap = BuildBoneMap(skeletonRoot);
        var localHierarchy = HandPoseHierarchyConverter.ToLocalHierarchy(pose.Bones);
        var applied = 0;

        foreach (var bone in pose.Bones)
        {
            if (string.IsNullOrEmpty(bone.boneName))
                continue;

            if (!boneMap.TryGetValue(bone.boneName, out var boneTransform))
                continue;

            if (!localHierarchy.TryGetValue(bone.boneName, out var localPose))
                continue;

            if (applyRotation)
                boneTransform.localRotation = localPose.localRotation;

            if (applyPosition)
                boneTransform.localPosition = localPose.localPosition;

            applied++;
        }

        if (boneMap.TryGetValue(GetWristBoneName(pose), out var wristTransform))
        {
            if (applyPosition)
                wristTransform.localPosition = Vector3.zero;

            if (applyRotation)
                wristTransform.localRotation = Quaternion.identity;
        }

        return applied;
    }

    static string GetWristBoneName(HandPoseData pose) =>
        pose != null && pose.Handedness == UnityEngine.XR.Hands.Handedness.Right
            ? RightWristBoneName
            : LeftWristBoneName;

    static int ApplyLegacyLocal(
        HandPoseData pose,
        Transform skeletonRoot,
        bool applyPosition,
        bool applyRotation)
    {
        var boneMap = BuildBoneMap(skeletonRoot);
        var applied = 0;

        foreach (var bone in pose.Bones)
        {
            if (string.IsNullOrEmpty(bone.boneName))
                continue;

            if (!boneMap.TryGetValue(bone.boneName, out var boneTransform))
                continue;

            if (applyRotation)
                boneTransform.localRotation = bone.localRotation;

            if (applyPosition)
                boneTransform.localPosition = bone.localPosition;

            applied++;
        }

        return applied;
    }

    public static void CaptureCurrentPose(Transform skeletonRoot, Dictionary<string, BoneTransformSnapshot> into)
    {
        into.Clear();
        if (skeletonRoot == null)
            return;

        foreach (var bone in skeletonRoot.GetComponentsInChildren<Transform>(true))
        {
            into[bone.name] = new BoneTransformSnapshot
            {
                localPosition = bone.localPosition,
                localRotation = bone.localRotation,
                worldPosition = bone.position,
                worldRotation = bone.rotation
            };
        }
    }

    public static void RestorePose(Transform skeletonRoot, IReadOnlyDictionary<string, BoneTransformSnapshot> snapshot)
    {
        if (skeletonRoot == null || snapshot == null)
            return;

        var boneMap = BuildBoneMap(skeletonRoot);
        foreach (var pair in snapshot)
        {
            if (!boneMap.TryGetValue(pair.Key, out var boneTransform))
                continue;

            boneTransform.SetPositionAndRotation(pair.Value.worldPosition, pair.Value.worldRotation);
        }
    }
}

public struct BoneTransformSnapshot
{
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 worldPosition;
    public Quaternion worldRotation;
}
