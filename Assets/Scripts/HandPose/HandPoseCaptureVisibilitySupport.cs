using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Quest 등에서 컨트롤러를 잡으면 손 트래킹이 끊기면서 Hand Visualizer가 손/스켈레톤을 숨깁니다.
/// 포즈 캡처 시 그립 버튼을 누르기 위해 컨트롤러를 들어도 왼손이 마지막 포즈로 유지되게 합니다.
/// </summary>
[DisallowMultipleComponent]
public class HandPoseCaptureVisibilitySupport : MonoBehaviour
{
    const string LeftDebugDrawJointsName = "LeftHandDebugDrawJoints";

    [SerializeField]
    bool m_KeepLeftHandMeshWhenTrackingLost = true;

    [SerializeField]
    bool m_KeepLeftHandSkeletonWhenTrackingLost = true;

    readonly List<XRHandMeshController> m_LeftMeshControllers = new();
    Transform m_LeftDebugDrawRoot;
    bool m_ConfiguredMeshControllers;

    void OnEnable()
    {
        m_ConfiguredMeshControllers = false;
        m_LeftDebugDrawRoot = null;
        TryConfigureLeftHandMeshes();
    }

    void LateUpdate()
    {
        if (!m_KeepLeftHandMeshWhenTrackingLost && !m_KeepLeftHandSkeletonWhenTrackingLost)
            return;

        if (!m_ConfiguredMeshControllers)
            TryConfigureLeftHandMeshes();

        if (!m_KeepLeftHandSkeletonWhenTrackingLost)
            return;

        if (IsLeftHandTracked())
            return;

        var debugRoot = ResolveLeftDebugDrawRoot();
        if (debugRoot == null)
            return;

        SetRenderersEnabled(debugRoot, true);
    }

    void TryConfigureLeftHandMeshes()
    {
        if (!m_KeepLeftHandMeshWhenTrackingLost)
            return;

        m_LeftMeshControllers.Clear();

        var meshControllers = FindObjectsByType<XRHandMeshController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var meshController in meshControllers)
        {
            var handEvents = meshController.handTrackingEvents;
            if (handEvents == null || handEvents.handedness != Handedness.Left)
                continue;

            meshController.hideMeshWhenTrackingIsLost = false;

            if (meshController.handMeshRenderer != null)
                meshController.handMeshRenderer.enabled = true;

            m_LeftMeshControllers.Add(meshController);
        }

        m_ConfiguredMeshControllers = m_LeftMeshControllers.Count > 0;
    }

    Transform ResolveLeftDebugDrawRoot()
    {
        if (m_LeftDebugDrawRoot != null)
            return m_LeftDebugDrawRoot;

        var found = GameObject.Find(LeftDebugDrawJointsName);
        if (found != null)
            m_LeftDebugDrawRoot = found.transform;

        return m_LeftDebugDrawRoot;
    }

    static bool IsLeftHandTracked()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var subsystem in subsystems)
        {
            if (subsystem.running && subsystem.leftHand.isTracked)
                return true;
        }

        return false;
    }

    static void SetRenderersEnabled(Transform root, bool enabled)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
            renderer.enabled = enabled;
    }
}
