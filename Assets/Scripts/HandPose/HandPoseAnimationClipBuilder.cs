using System.Collections.Generic;
using UnityEngine;

public static class HandPoseAnimationClipBuilder
{
    public static AnimationClip BuildClip(
        IReadOnlyList<HandPoseData> posesInOrder,
        string clipName,
        HandPoseAnimationRig rig,
        float frameRate = 60f,
        bool useEulerAngles = true,
        bool writePositionCurves = false)
    {
        var clip = new AnimationClip { name = clipName, frameRate = frameRate };
        if (posesInOrder == null || posesInOrder.Count == 0)
            return clip;

        var duration = Mathf.Max(posesInOrder.Count - 1, 1);
        var curveSets = new Dictionary<string, TransformCurves>();

        for (var frameIndex = 0; frameIndex < posesInOrder.Count; frameIndex++)
        {
            var pose = posesInOrder[frameIndex];
            if (pose == null)
                continue;

            var time = frameIndex / duration;
            var localHierarchy = HandPoseHierarchyConverter.ToLocalHierarchyForRig(pose.Bones, rig);

            foreach (var pair in localHierarchy)
            {
                if (!HandPoseAnimationPathUtility.TryGetAnimationPath(pair.Key, rig, out var animationPath))
                    continue;

                if (!curveSets.TryGetValue(animationPath, out var curves))
                {
                    curves = new TransformCurves();
                    curveSets.Add(animationPath, curves);
                }

                if (useEulerAngles)
                {
                    var euler = pair.Value.localRotation.eulerAngles;
                    curves.EulerX.AddKey(time, euler.x);
                    curves.EulerY.AddKey(time, euler.y);
                    curves.EulerZ.AddKey(time, euler.z);
                }
                else
                {
                    curves.RotX.AddKey(time, pair.Value.localRotation.x);
                    curves.RotY.AddKey(time, pair.Value.localRotation.y);
                    curves.RotZ.AddKey(time, pair.Value.localRotation.z);
                    curves.RotW.AddKey(time, pair.Value.localRotation.w);
                }

                if (!writePositionCurves)
                    continue;

                curves.PosX.AddKey(time, pair.Value.localPosition.x);
                curves.PosY.AddKey(time, pair.Value.localPosition.y);
                curves.PosZ.AddKey(time, pair.Value.localPosition.z);
            }
        }

        foreach (var pair in curveSets)
        {
            var path = pair.Key;
            var curves = pair.Value;

            if (useEulerAngles)
            {
                clip.SetCurve(path, typeof(Transform), "localEulerAngles.x", curves.EulerX);
                clip.SetCurve(path, typeof(Transform), "localEulerAngles.y", curves.EulerY);
                clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", curves.EulerZ);
            }
            else
            {
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", curves.RotX);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", curves.RotY);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", curves.RotZ);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", curves.RotW);
            }

            if (!writePositionCurves)
                continue;

            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", curves.PosX);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", curves.PosY);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", curves.PosZ);
        }

        clip.EnsureQuaternionContinuity();
        return clip;
    }

    public static AnimationClip BuildSinglePoseClip(
        HandPoseData pose,
        string clipName,
        HandPoseAnimationRig rig,
        float frameRate = 60f,
        bool useEulerAngles = true)
    {
        return BuildClip(new[] { pose }, clipName, rig, frameRate, useEulerAngles, writePositionCurves: false);
    }

    sealed class TransformCurves
    {
        public AnimationCurve EulerX = new();
        public AnimationCurve EulerY = new();
        public AnimationCurve EulerZ = new();
        public AnimationCurve RotX = new();
        public AnimationCurve RotY = new();
        public AnimationCurve RotZ = new();
        public AnimationCurve RotW = new();
        public AnimationCurve PosX = new();
        public AnimationCurve PosY = new();
        public AnimationCurve PosZ = new();
    }
}
