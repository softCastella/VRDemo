using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Samples.VisualizerSample;

/// <summary>
/// 포즈 캡처 툴이 켜져 있을 때만 스켈레톤 디버그를 표시하고,
/// 캡처 중 컨트롤러 그립으로 트래킹이 끊겨도 손 메쉬는 유지합니다.
/// Hand Visualizer 오브젝트에 붙인 인스턴스만 스켈레톤 표시를 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public class HandPoseCaptureVisibilitySupport : MonoBehaviour
{
    [SerializeField]
    bool m_KeepTrackedHandMeshWhenTrackingLost = true;

    [SerializeField]
    [Tooltip("켜면 '그립 버튼으로 캡처 대기' 중에만 스켈레톤을 표시합니다. 끄면 Hand Visualizer Inspector 설정을 따릅니다.")]
    bool m_OnlyShowSkeletonWhileCaptureListening = true;

    readonly List<XRHandMeshController> m_TrackedMeshControllers = new();
    HandVisualizer m_HandVisualizer;
    bool m_ConfiguredMeshControllers;
    bool m_ManagesSkeletonVisibility;

    void Awake()
    {
        ResolveHandVisualizer();
        m_ManagesSkeletonVisibility = m_HandVisualizer != null && ShouldOwnSkeletonManagement();
    }

    void OnEnable()
    {
        m_ConfiguredMeshControllers = false;
        ResolveHandVisualizer();
        m_ManagesSkeletonVisibility = m_HandVisualizer != null && ShouldOwnSkeletonManagement();

        TryConfigureTrackedHandMeshes();
        SyncSkeletonVisibility();
    }

    void OnDisable()
    {
        if (!Application.isPlaying || !m_ManagesSkeletonVisibility)
            return;

        if (m_OnlyShowSkeletonWhileCaptureListening)
            ApplyCaptureSkeletonVisibility(false);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (!m_ConfiguredMeshControllers)
            TryConfigureTrackedHandMeshes();

        if (m_KeepTrackedHandMeshWhenTrackingLost && HandPoseCaptureSession.IsListening)
            KeepTrackedHandMeshesVisible();

        SyncSkeletonVisibility();
    }

    void SyncSkeletonVisibility()
    {
        if (!m_ManagesSkeletonVisibility || !m_OnlyShowSkeletonWhileCaptureListening)
            return;

        ApplyCaptureSkeletonVisibility(ShouldShowCaptureSkeleton());
    }

    static bool ShouldShowCaptureSkeleton() =>
        Application.isPlaying && HandPoseCaptureSession.IsListening;

    void TryConfigureTrackedHandMeshes()
    {
        if (!m_KeepTrackedHandMeshWhenTrackingLost)
            return;

        m_TrackedMeshControllers.Clear();
        var targetHand = HandPoseCaptureSession.CaptureHandedness;

        var meshControllers = FindObjectsByType<XRHandMeshController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var meshController in meshControllers)
        {
            var handEvents = meshController.handTrackingEvents;
            if (handEvents == null || handEvents.handedness != targetHand)
                continue;

            m_TrackedMeshControllers.Add(meshController);
        }

        m_ConfiguredMeshControllers = m_TrackedMeshControllers.Count > 0;
    }

    void KeepTrackedHandMeshesVisible()
    {
        foreach (var meshController in m_TrackedMeshControllers)
        {
            if (meshController == null)
                continue;

            meshController.hideMeshWhenTrackingIsLost = false;
            if (meshController.handMeshRenderer != null)
                meshController.handMeshRenderer.enabled = true;
        }
    }

    void ApplyCaptureSkeletonVisibility(bool visible)
    {
        if (!Application.isPlaying)
            return;

        var visualizer = ResolveHandVisualizer();
        if (visualizer == null)
            return;

        visualizer.debugDrawJoints = visible;

        var rootName = HandPoseCaptureSession.CaptureHandedness == Handedness.Right
            ? "RightHandDebugDrawJoints"
            : "LeftHandDebugDrawJoints";

        SetDebugDrawRootVisible(visualizer.transform, rootName, visible);
    }

    static void SetDebugDrawRootVisible(Transform visualizerRoot, string rootName, bool visible)
    {
        if (visualizerRoot == null)
            return;

        var foundRoot = false;
        foreach (var jointRoot in visualizerRoot.GetComponentsInChildren<Transform>(true))
        {
            if (jointRoot.name != rootName)
                continue;

            foundRoot = true;
            foreach (var line in jointRoot.GetComponentsInChildren<LineRenderer>(true))
                line.enabled = visible;

            foreach (var renderer in jointRoot.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }

        if (!foundRoot && visible)
            visualizerRoot.gameObject.SetActive(true);
    }

    HandVisualizer ResolveHandVisualizer()
    {
        if (m_HandVisualizer != null)
            return m_HandVisualizer;

        m_HandVisualizer = GetComponent<HandVisualizer>();
        if (m_HandVisualizer == null)
            m_HandVisualizer = FindFirstObjectByType<HandVisualizer>(FindObjectsInactive.Include);

        return m_HandVisualizer;
    }

    bool ShouldOwnSkeletonManagement()
    {
        if (GetComponent<HandVisualizer>() != null)
            return true;

        if (m_HandVisualizer == null)
            return false;

        var onVisualizer = m_HandVisualizer.GetComponent<HandPoseCaptureVisibilitySupport>();
        return onVisualizer == null || onVisualizer == this;
    }
}
