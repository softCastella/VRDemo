using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HandPoseAnimationGenerator
{
    public const string DefaultOutputFolder = "Assets/Animations/Generated";
    public const string OculusLeftControllerPath = "Assets/Animations/OculusHand_L.controller";

    public static string SaveClip(AnimationClip clip, string folder, string fileName)
    {
        EnsureFolder(folder);

        var safeName = SanitizeFileName(fileName);
        var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.anim");
        AssetDatabase.CreateAsset(clip, assetPath);
        AssetDatabase.SaveAssets();
        return assetPath;
    }

    public static string GenerateTransitionClip(
        HandPoseData startPose,
        HandPoseData endPose,
        string clipName,
        HandPoseAnimationRig rig,
        string outputFolder = DefaultOutputFolder)
    {
        if (startPose == null || endPose == null)
            return null;

        var clip = HandPoseAnimationClipBuilder.BuildClip(
            new[] { startPose, endPose },
            clipName,
            rig);

        return SaveClip(clip, outputFolder, clipName);
    }

    public static (string openPath, string gripPath) GenerateOpenGripClips(
        HandPoseData openPose,
        HandPoseData gripPose,
        HandPoseAnimationRig rig,
        string outputFolder = DefaultOutputFolder,
        string openClipName = "Open_L",
        string gripClipName = "Grip_L")
    {
        var openClip = HandPoseAnimationClipBuilder.BuildSinglePoseClip(openPose, openClipName, rig);
        var gripClip = HandPoseAnimationClipBuilder.BuildSinglePoseClip(gripPose, gripClipName, rig);

        return (
            SaveClip(openClip, outputFolder, openClipName),
            SaveClip(gripClip, outputFolder, gripClipName));
    }

    public static bool TryAssignToOculusLeftBlendTree(string openClipPath, string gripClipPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(OculusLeftControllerPath);
        var openClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(openClipPath);
        var gripClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(gripClipPath);

        if (controller == null || openClip == null || gripClip == null)
            return false;

        var rootStateMachine = controller.layers[0].stateMachine;
        if (rootStateMachine.states.Length == 0)
            return false;

        var motion = rootStateMachine.states[0].state.motion;
        if (motion is not BlendTree blendTree || blendTree.children.Length < 2)
            return false;

        var children = blendTree.children;
        if (children.Length < 2)
            return false;

        children[0].motion = openClip;
        children[0].threshold = 0f;
        children[1].motion = gripClip;
        children[1].threshold = 1f;
        blendTree.children = children;

        EditorUtility.SetDirty(blendTree);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return true;
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "HandPoseClip";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (System.Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }

        return new string(chars);
    }
}
