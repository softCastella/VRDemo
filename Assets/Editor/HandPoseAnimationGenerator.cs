using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HandPoseAnimationGenerator
{
    public const string DefaultOutputFolder = "Assets/Animations/Generated";
    public const string MetaLeftControllerPath = "Assets/Animations/MetaHand_L.controller";
    public const string MetaRightControllerPath = "Assets/Animations/MetaHand_R.controller";
    public const string OculusLeftControllerPath = "Assets/Animations/OculusHand_L.controller";
    public const string OculusRightControllerPath = "Assets/Animations/OculusHand_R.controller";
    public const string DefaultOpenClipNameLeft = "Open_L";
    public const string DefaultGripClipNameLeft = "HalfGrip_L";
    public const string DefaultOpenClipNameRight = "Open_R";
    public const string DefaultGripClipNameRight = "HalfGrip_R";

    public static string GetControllerPath(HandPoseAnimationRig rig) => rig switch
    {
        HandPoseAnimationRig.OculusLeftHand => OculusLeftControllerPath,
        HandPoseAnimationRig.OculusRightHand => OculusRightControllerPath,
        HandPoseAnimationRig.MetaRightHand => MetaRightControllerPath,
        _ => MetaLeftControllerPath,
    };

    public static string GetDefaultOpenClipName(HandPoseAnimationRig rig) =>
        HandPoseRigUtility.IsRightRig(rig) ? DefaultOpenClipNameRight : DefaultOpenClipNameLeft;

    public static string GetDefaultGripClipName(HandPoseAnimationRig rig) =>
        HandPoseRigUtility.IsRightRig(rig) ? DefaultGripClipNameRight : DefaultGripClipNameLeft;

    public static string InferClipNameFromPose(HandPoseData pose)
    {
        if (pose == null)
            return "HandPose";

        var suffix = pose.Handedness == UnityEngine.XR.Hands.Handedness.Right ? "_R" : "_L";
        var name = pose.PoseName ?? string.Empty;

        if (name.IndexOf("HalfGrip", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("GripHalf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "HalfGrip" + suffix;

        if (name.IndexOf("Open", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "Open" + suffix;

        if (name.IndexOf("Grip", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "Grip" + suffix;

        return SanitizeFileName(name);
    }

    public static string SaveClip(AnimationClip clip, string folder, string fileName, bool overwrite = true)
    {
        EnsureFolder(folder);

        var safeName = SanitizeFileName(fileName);
        var assetPath = $"{folder}/{safeName}.anim";

        if (overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);
        }
        else
        {
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        }

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
        string openClipName = null,
        string gripClipName = null)
    {
        openClipName ??= GetDefaultOpenClipName(rig);
        gripClipName ??= GetDefaultGripClipName(rig);

        var openClip = HandPoseAnimationClipBuilder.BuildSinglePoseClip(openPose, openClipName, rig);
        var gripClip = HandPoseAnimationClipBuilder.BuildSinglePoseClip(gripPose, gripClipName, rig);

        return (
            SaveClip(openClip, outputFolder, openClipName),
            SaveClip(gripClip, outputFolder, gripClipName));
    }

    public static bool EnsureHandController(HandPoseAnimationRig rig)
    {
        var controllerPath = GetControllerPath(rig);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            return true;

        var sourcePath = rig switch
        {
            HandPoseAnimationRig.MetaLeftHand => OculusLeftControllerPath,
            HandPoseAnimationRig.MetaRightHand => AssetDatabase.LoadAssetAtPath<AnimatorController>(MetaLeftControllerPath) != null
                ? MetaLeftControllerPath
                : OculusRightControllerPath,
            HandPoseAnimationRig.OculusLeftHand => OculusLeftControllerPath,
            HandPoseAnimationRig.OculusRightHand => OculusRightControllerPath,
            _ => OculusLeftControllerPath,
        };

        if (!AssetDatabase.CopyAsset(sourcePath, controllerPath))
            return false;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            return false;

        controller.name = System.IO.Path.GetFileNameWithoutExtension(controllerPath);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return true;
    }

    public static bool TryAssignToHandBlendTree(
        HandPoseAnimationRig rig,
        string openClipPath,
        string gripClipPath)
    {
        EnsureHandController(rig);
        return TryAssignToBlendTree(GetControllerPath(rig), openClipPath, gripClipPath);
    }

    public static bool TryAssignToBlendTree(string controllerPath, string openClipPath, string gripClipPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
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

    public static bool TryAssignToOculusLeftBlendTree(string openClipPath, string gripClipPath) =>
        TryAssignToBlendTree(OculusLeftControllerPath, openClipPath, gripClipPath);

    public static Animator FindHandAnimator(HandPoseAnimationRig rig)
    {
        var wristBone = HandPoseRigUtility.GetWristBoneName(rig);
        var oculusWrist = HandPoseRigUtility.IsRightRig(rig) ? "b_r_wrist" : "b_l_wrist";

        var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Animator fallback = null;

        foreach (var animator in animators)
        {
            if (animator.transform.Find(wristBone) == null)
                continue;

            if (HandPoseRigUtility.IsOculusRig(rig) && animator.transform.Find(oculusWrist) != null)
                return animator;

            if (!HandPoseRigUtility.IsOculusRig(rig) && animator.transform.Find(oculusWrist) == null)
                return animator;

            fallback = animator;
        }

        return fallback;
    }

    public static Animator FindLeftHandAnimator() => FindHandAnimator(HandPoseAnimationRig.MetaLeftHand);

    public static bool TryAssignControllerToSceneHand(HandPoseAnimationRig rig)
    {
        var animator = FindHandAnimator(rig);
        if (animator == null)
            return false;

        if (!EnsureHandController(rig))
            return false;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GetControllerPath(rig));
        if (controller == null)
            return false;

        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(animator);
        if (animator.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);

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
