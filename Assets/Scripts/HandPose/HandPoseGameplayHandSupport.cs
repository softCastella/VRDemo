using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// 왼손에 그립 애니메이션을 적용합니다.
/// Hand Visualizer(Meta L_Wrist) 또는 Left Controller의 OculusHand_L(b_l_wrist)을 자동 인식합니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class HandPoseGameplayHandSupport : MonoBehaviour
{
    const float GripAnimationThreshold = 0.02f;
    const string MetaControllerPath = "Assets/Animations/MetaHand_L.controller";
    const string OculusControllerPath = "Assets/Animations/OculusHand_L.controller";

    [SerializeField]
    RuntimeAnimatorController m_MetaHandController;

    [SerializeField]
    RuntimeAnimatorController m_OculusHandController;

    [SerializeField]
    bool m_KeepMeshVisibleWhenTrackingLost = true;

    [SerializeField]
    bool m_PlaceInFrontOfCameraWhenNotTracked = true;

    enum LeftHandRigKind
    {
        None,
        Meta,
        Oculus
    }

    LeftHandRigKind m_RigKind;
    Transform m_HandRoot;
    XRHandSkeletonDriver m_LeftSkeletonDriver;
    XRHandMeshController m_LeftMeshController;
    Animator m_LeftAnimator;
    Hand m_LeftHandGrip;
    bool m_IsSetup;
    bool m_DesktopHandPlaced;

    void OnEnable()
    {
        m_IsSetup = false;
        StartCoroutine(SetupWhenLeftHandReady());
    }

    IEnumerator SetupWhenLeftHandReady()
    {
        while (!m_IsSetup)
        {
            if (TrySetupLeftHand())
                m_IsSetup = true;
            else
                yield return null;
        }
    }

    bool TrySetupLeftHand()
    {
        if (!TryResolveLeftHand(out m_HandRoot, out m_LeftSkeletonDriver, out m_LeftMeshController, out m_RigKind))
            return false;

        m_LeftAnimator = m_HandRoot.GetComponent<Animator>();
        if (m_LeftAnimator == null)
            m_LeftAnimator = m_HandRoot.gameObject.AddComponent<Animator>();

        var controller = ResolveAnimatorController(m_RigKind);
        if (controller == null)
            return false;

        m_LeftAnimator.runtimeAnimatorController = controller;
        m_LeftAnimator.enabled = m_RigKind == LeftHandRigKind.Oculus;

        m_LeftHandGrip = m_HandRoot.GetComponent<Hand>();
        if (m_LeftHandGrip == null)
            m_LeftHandGrip = m_HandRoot.gameObject.AddComponent<Hand>();

        m_LeftHandGrip.BindAnimator(m_LeftAnimator);

        if (m_KeepMeshVisibleWhenTrackingLost && m_LeftMeshController != null)
            m_LeftMeshController.hideMeshWhenTrackingIsLost = false;

        return true;
    }

    RuntimeAnimatorController ResolveAnimatorController(LeftHandRigKind rigKind)
    {
        if (rigKind == LeftHandRigKind.Oculus)
        {
            if (m_OculusHandController != null)
                return m_OculusHandController;

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OculusControllerPath);
#else
            return null;
#endif
        }

        if (m_MetaHandController != null)
            return m_MetaHandController;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MetaControllerPath);
#else
        return null;
#endif
    }

    void LateUpdate()
    {
        if (!m_IsSetup)
            return;

        if (m_KeepMeshVisibleWhenTrackingLost)
            KeepLeftMeshVisible();

        var desktop = HandPracPlayModeSupport.IsDesktopPlayMode;
        var tracked = IsLeftHandTracked();
        var grip = ReadGripValue();
        var gripActive = grip > GripAnimationThreshold;

        if (m_RigKind == LeftHandRigKind.Oculus)
        {
            if (m_LeftAnimator != null)
            {
                m_LeftAnimator.enabled = true;
                m_LeftAnimator.SetFloat("Grip", grip);
            }

            return;
        }

        var driveAnimator = gripActive || (!tracked && desktop);

        if (m_LeftSkeletonDriver != null)
            m_LeftSkeletonDriver.enabled = tracked && !gripActive;

        if (m_LeftAnimator != null)
        {
            m_LeftAnimator.enabled = driveAnimator;
            if (driveAnimator)
                m_LeftAnimator.SetFloat("Grip", grip);
        }

        if (!tracked && desktop && m_PlaceInFrontOfCameraWhenNotTracked && !m_DesktopHandPlaced)
        {
            PlaceHandRelativeToMainCamera(m_HandRoot);
            m_DesktopHandPlaced = true;
        }
    }

    static void PlaceHandRelativeToMainCamera(Transform handRoot)
    {
        if (handRoot == null)
            return;

        var camera = Camera.main;
        if (camera == null)
            return;

        var cameraTransform = camera.transform;
        var position = cameraTransform.position + cameraTransform.forward * 0.35f + cameraTransform.up * -0.15f;
        handRoot.SetPositionAndRotation(position, cameraTransform.rotation);
    }

    void KeepLeftMeshVisible()
    {
        if (m_LeftMeshController == null)
            return;

        m_LeftMeshController.hideMeshWhenTrackingIsLost = false;
        if (m_LeftMeshController.handMeshRenderer != null)
            m_LeftMeshController.handMeshRenderer.enabled = true;
    }

    float ReadGripValue()
    {
        if (m_LeftHandGrip != null)
            return m_LeftHandGrip.CurrentGrip;

        return 0f;
    }

    static bool TryResolveLeftHand(
        out Transform handRoot,
        out XRHandSkeletonDriver skeletonDriver,
        out XRHandMeshController meshController,
        out LeftHandRigKind rigKind)
    {
        handRoot = null;
        skeletonDriver = null;
        meshController = null;
        rigKind = LeftHandRigKind.None;

        var drivers = Object.FindObjectsByType<XRHandSkeletonDriver>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var driver in drivers)
        {
            var handEvents = driver.handTrackingEvents;
            if (handEvents == null || handEvents.handedness != Handedness.Left)
                continue;

            handRoot = driver.transform;
            skeletonDriver = driver;
            meshController = driver.GetComponent<XRHandMeshController>();
            rigKind = LeftHandRigKind.Meta;
            return true;
        }

        var oculusHand = FindOculusLeftHandRoot();
        if (oculusHand != null)
        {
            handRoot = oculusHand;
            rigKind = LeftHandRigKind.Oculus;
            return true;
        }

        return false;
    }

    static Transform FindOculusLeftHandRoot()
    {
        var leftController = GameObject.Find("Left Controller");
        if (leftController != null)
        {
            var underController = leftController.transform.Find("OculusHand_L");
            if (underController != null && underController.Find("b_l_wrist") != null)
                return underController;
        }

        var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var transform in roots)
        {
            if (transform.name != "OculusHand_L")
                continue;

            if (transform.Find("b_l_wrist") != null)
                return transform;
        }

        return null;
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
}
