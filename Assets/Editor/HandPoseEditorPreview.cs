using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

[InitializeOnLoad]
public static class HandPoseEditorPreview
{
    const float DefaultCameraPreviewDistance = 0.35f;

    static HandPoseData s_ActivePose;
    static Transform s_SkeletonRoot;
    static Vector3 s_PreviewOrigin = HandPoseApplier.DefaultPreviewOrigin;
    static bool s_PlaceInFrontOfCamera;
    static readonly Dictionary<string, BoneTransformSnapshot> s_OriginalPose = new();
    static readonly List<Behaviour> s_DisabledBehaviours = new();
    static string s_StatusMessage = string.Empty;

    public static HandPoseData ActivePose => s_ActivePose;
    public static string StatusMessage => s_StatusMessage;
    public static bool HasActivePreview => s_ActivePose != null && s_SkeletonRoot != null;
    public static bool PlaceInFrontOfCamera => s_PlaceInFrontOfCamera;

    static HandPoseEditorPreview()
    {
        BeginSceneViewDrawing();
    }

    public static void BeginSceneViewDrawing()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    public static void EndSceneViewDrawing()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
    }

    public static bool TryApplyPreview(
        HandPoseData pose,
        out string message,
        bool placeInFrontOfCamera = false)
    {
        message = string.Empty;
        if (pose == null)
        {
            message = "포즈 에셋이 없습니다.";
            return false;
        }

        var root = FindLeftHandSkeletonRoot();
        if (root == null)
        {
            message = "씬에서 왼손 스켈레톤(L_Wrist / LeftHand)을 찾지 못했습니다.";
            return false;
        }

        if (s_ActivePose != pose || s_SkeletonRoot != root || s_PlaceInFrontOfCamera != placeInFrontOfCamera)
            ClearPreview(restoreOriginal: true);

        if (!HasActivePreview)
        {
            HandPoseApplier.CaptureCurrentPose(root, s_OriginalPose);
            s_SkeletonRoot = root;
            s_ActivePose = pose;
            s_PlaceInFrontOfCamera = placeInFrontOfCamera;
            DisablePoseOverrides(root);
        }

        s_PreviewOrigin = ResolvePreviewOrigin(placeInFrontOfCamera);
        var applied = HandPoseApplier.Apply(pose, root, s_PreviewOrigin);
        if (applied == 0)
        {
            message = "적용할 본을 찾지 못했습니다. 본 이름이 씬 손과 일치하는지 확인하세요.";
            ClearPreview(restoreOriginal: true);
            return false;
        }

        FramePoseInSceneView(pose, s_PreviewOrigin);

        var placement = placeInFrontOfCamera ? "카메라 앞" : "월드 원점";
        s_StatusMessage = $"'{pose.PoseName}' 미리보기 ({placement}, {applied}/{pose.Bones.Count} bones)";
        message = s_StatusMessage;
        SceneView.RepaintAll();
        return true;
    }

    public static void ClearPreview(bool restoreOriginal = true)
    {
        if (restoreOriginal && s_SkeletonRoot != null && s_OriginalPose.Count > 0)
            HandPoseApplier.RestorePose(s_SkeletonRoot, s_OriginalPose);

        RestorePoseOverrides();

        s_ActivePose = null;
        s_SkeletonRoot = null;
        s_OriginalPose.Clear();
        s_PlaceInFrontOfCamera = false;
        s_PreviewOrigin = HandPoseApplier.DefaultPreviewOrigin;
        s_StatusMessage = string.Empty;
        SceneView.RepaintAll();
    }

    public static Transform FindLeftHandSkeletonRoot()
    {
        var drivers = Object.FindObjectsByType<XRHandSkeletonDriver>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var driver in drivers)
        {
            var handEvents = driver.handTrackingEvents;
            if (handEvents == null || handEvents.handedness != Handedness.Left)
                continue;

            if (driver.rootTransform != null)
                return driver.rootTransform.parent != null ? driver.rootTransform.parent : driver.rootTransform;

            return driver.transform;
        }

        var wristObject = GameObject.Find("L_Wrist");
        if (wristObject != null)
        {
            var wrist = wristObject.transform;
            return wrist.parent != null ? wrist.parent : wrist;
        }

        var leftHand = GameObject.Find("LeftHand");
        return leftHand != null ? leftHand.transform : null;
    }

    public static Vector3 ResolvePreviewOrigin(bool placeInFrontOfCamera)
    {
        if (!placeInFrontOfCamera)
            return HandPoseApplier.DefaultPreviewOrigin;

        var camera = ResolvePreviewCamera();
        if (camera == null)
            return HandPoseApplier.DefaultPreviewOrigin;

        return camera.transform.position + camera.transform.forward * DefaultCameraPreviewDistance;
    }

    static Camera ResolvePreviewCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        var cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var camera in cameras)
        {
            if (camera != null && camera.enabled && camera.gameObject.scene.IsValid())
                return camera;
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    static void DisablePoseOverrides(Transform skeletonRoot)
    {
        s_DisabledBehaviours.Clear();

        foreach (var driver in skeletonRoot.GetComponentsInChildren<XRHandSkeletonDriver>(true))
            DisableBehaviour(driver);

        foreach (var animator in skeletonRoot.GetComponentsInChildren<Animator>(true))
            DisableBehaviour(animator);
    }

    static void DisableBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled)
            return;

        behaviour.enabled = false;
        s_DisabledBehaviours.Add(behaviour);
    }

    static void RestorePoseOverrides()
    {
        for (var i = s_DisabledBehaviours.Count - 1; i >= 0; i--)
        {
            var behaviour = s_DisabledBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = true;
        }

        s_DisabledBehaviours.Clear();
    }

    static void OnEditorUpdate()
    {
        if (!HasActivePreview)
            return;

        s_PreviewOrigin = ResolvePreviewOrigin(s_PlaceInFrontOfCamera);
        HandPoseApplier.Apply(s_ActivePose, s_SkeletonRoot, s_PreviewOrigin);
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        if (s_ActivePose == null)
            return;

        DrawPoseSkeleton(s_ActivePose, s_PreviewOrigin);

        if (s_SkeletonRoot != null)
        {
            Handles.color = new Color(0.2f, 0.9f, 0.4f, 0.95f);
            Handles.Label(
                s_PreviewOrigin + Vector3.up * 0.06f,
                $"Preview: {s_ActivePose.PoseName}",
                EditorStyles.whiteBoldLabel);
        }
    }

    public static void DrawPoseSkeleton(HandPoseData pose, Vector3 previewOrigin)
    {
        if (pose == null || pose.Bones.Count == 0)
            return;

        var boneMap = new Dictionary<string, HandBonePoseData>();
        foreach (var bone in pose.Bones)
            boneMap[bone.boneName] = bone;

        var worldByName = new Dictionary<string, Matrix4x4>();
        var wristWorld = Matrix4x4.TRS(previewOrigin, Quaternion.identity, Vector3.one);

        foreach (var bone in pose.Bones)
        {
            worldByName[bone.boneName] = pose.IsWristAtOrigin()
                ? wristWorld * Matrix4x4.TRS(bone.localPosition, bone.localRotation, Vector3.one)
                : ComputeLegacyWorldMatrix(bone.boneName, boneMap, worldByName, previewOrigin);
        }

        Handles.color = new Color(0.3f, 0.75f, 1f, 0.95f);
        foreach (var bone in pose.Bones)
        {
            if (!HandPoseBoneHierarchy.TryGetParentBoneName(bone.boneName, out var parentName))
                continue;

            if (!worldByName.TryGetValue(parentName, out var parentMatrix) ||
                !worldByName.TryGetValue(bone.boneName, out var childMatrix))
                continue;

            Handles.DrawLine(parentMatrix.GetColumn(3), childMatrix.GetColumn(3));
            Handles.SphereHandleCap(0, childMatrix.GetColumn(3), Quaternion.identity, 0.008f, EventType.Repaint);
        }
    }

    static void FramePoseInSceneView(HandPoseData pose, Vector3 previewOrigin)
    {
        if (pose == null || pose.Bones.Count == 0)
            return;

        var bounds = new Bounds(previewOrigin, Vector3.one * 0.02f);
        foreach (var bone in pose.Bones)
            bounds.Encapsulate(previewOrigin + bone.localPosition);

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
            sceneView.Frame(bounds, false);
    }

    static Matrix4x4 ComputeLegacyWorldMatrix(
        string boneName,
        IReadOnlyDictionary<string, HandBonePoseData> boneMap,
        IDictionary<string, Matrix4x4> cache,
        Vector3 fallbackOrigin)
    {
        if (cache.TryGetValue(boneName, out var cached))
            return cached;

        if (!boneMap.TryGetValue(boneName, out var bone))
            return Matrix4x4.identity;

        Matrix4x4 localMatrix = Matrix4x4.TRS(bone.localPosition, bone.localRotation, Vector3.one);
        Matrix4x4 worldMatrix;

        if (HandPoseBoneHierarchy.TryGetParentBoneName(boneName, out var parentName) &&
            boneMap.ContainsKey(parentName))
        {
            var parentWorld = ComputeLegacyWorldMatrix(parentName, boneMap, cache, fallbackOrigin);
            worldMatrix = parentWorld * localMatrix;
        }
        else
        {
            worldMatrix = Matrix4x4.TRS(fallbackOrigin, Quaternion.identity, Vector3.one) * localMatrix;
        }

        cache[boneName] = worldMatrix;
        return worldMatrix;
    }
}
