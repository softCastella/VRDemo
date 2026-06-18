using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

[DisallowMultipleComponent]
public class HandPoseCaptureController : MonoBehaviour
{
    const string LeftInteractionMapName = "XRI Left Interaction";
    const string GripButtonActionName = "Select";
    const string GripValueActionName = "Select Value";
    const string LeftGripButtonBinding = "<XRController>{LeftHand}/{GripButton}";
    const string LeftGripValueBinding = "<XRController>{LeftHand}/{Grip}";

    [SerializeField]
    XRHandSkeletonDriver m_LeftHandSkeletonDriver;

    [SerializeField]
    [Range(0.5f, 1f)]
    float m_GripValueThreshold = 0.75f;

    InputAction m_GripButtonAction;
    InputAction m_GripValueAction;
    bool m_OwnsGripButtonAction;
    bool m_OwnsGripValueAction;
    bool m_WasGripPressed;

    void OnEnable()
    {
        ResolveGripActions();
        m_GripButtonAction?.Enable();
        m_GripValueAction?.Enable();
        m_WasGripPressed = IsGripPressed();
    }

    void OnDisable()
    {
        m_GripButtonAction?.Disable();
        m_GripValueAction?.Disable();

        if (m_OwnsGripButtonAction)
        {
            m_GripButtonAction?.Dispose();
            m_GripButtonAction = null;
            m_OwnsGripButtonAction = false;
        }

        if (m_OwnsGripValueAction)
        {
            m_GripValueAction?.Dispose();
            m_GripValueAction = null;
            m_OwnsGripValueAction = false;
        }
    }

    void Update()
    {
        if (!Application.isPlaying || !HandPoseCaptureSession.IsListening)
            return;

        var pressed = IsGripPressed();
        if (pressed && !m_WasGripPressed)
            CaptureNow();

        m_WasGripPressed = pressed;
    }

    public void CaptureNow()
    {
        TryCapturePose();
    }

    bool IsGripPressed()
    {
        if (m_GripButtonAction != null && m_GripButtonAction.ReadValue<float>() >= 0.5f)
            return true;

        return m_GripValueAction != null && m_GripValueAction.ReadValue<float>() >= m_GripValueThreshold;
    }

    void TryCapturePose()
    {
        var driver = ResolveLeftHandSkeletonDriver();
        if (driver == null)
        {
            HandPoseCaptureSession.SetStatus("왼손 XRHandSkeletonDriver를 찾지 못했습니다. Hand Visualizer가 활성인지 확인하세요.");
            return;
        }

        if (!TryCaptureFromSkeletonDriver(driver, out var bones))
        {
            HandPoseCaptureSession.SetStatus("트래킹된 관절이 없습니다. 왼손이 인식된 상태에서 다시 시도하세요.");
            return;
        }

        var poseName = string.IsNullOrWhiteSpace(HandPoseCaptureSession.NextPoseName)
            ? "HandPose"
            : HandPoseCaptureSession.NextPoseName.Trim();

        var snapshot = new HandPoseSnapshot(poseName, Handedness.Left, bones);
        HandPoseCaptureSession.NotifyPoseCaptured(snapshot);
        HandPoseCaptureSession.SetStatus($"캡처 완료: {poseName} ({bones.Count} bones)");
    }

    static bool TryCaptureFromSkeletonDriver(XRHandSkeletonDriver driver, out List<HandBonePoseData> bones)
    {
        var joints = new List<(Transform transform, XRHandJointID jointId)>();

        foreach (var jointReference in driver.jointTransformReferences)
        {
            if (jointReference.jointTransform == null)
                continue;

            joints.Add((jointReference.jointTransform, jointReference.xrHandJointID));
        }

        bones = HandPoseNormalizer.NormalizeToWristOrigin(joints);
        return bones.Count > 0;
    }

    XRHandSkeletonDriver ResolveLeftHandSkeletonDriver()
    {
        if (m_LeftHandSkeletonDriver != null)
            return m_LeftHandSkeletonDriver;

        var drivers = FindObjectsByType<XRHandSkeletonDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var driver in drivers)
        {
            var handEvents = driver.GetComponent<XRHandTrackingEvents>();
            if (handEvents != null && handEvents.handedness == Handedness.Left)
                return driver;
        }

        return drivers.Length > 0 ? drivers[0] : null;
    }

    void ResolveGripActions()
    {
        m_GripButtonAction = FindAction(LeftInteractionMapName, GripButtonActionName);
        m_GripValueAction = FindAction(LeftInteractionMapName, GripValueActionName);

        if (m_GripButtonAction == null)
        {
            m_GripButtonAction = new InputAction("HandPoseLeftGripButton", InputActionType.Button, LeftGripButtonBinding);
            m_OwnsGripButtonAction = true;
        }

        if (m_GripValueAction == null)
        {
            m_GripValueAction = new InputAction("HandPoseLeftGripValue", InputActionType.Value, LeftGripValueBinding);
            m_OwnsGripValueAction = true;
        }
    }

    static InputAction FindAction(string actionMapName, string actionName)
    {
        var managers = FindObjectsByType<InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var manager in managers)
        {
            foreach (var asset in manager.actionAssets)
            {
                if (asset == null)
                    continue;

                var action = asset.FindActionMap(actionMapName)?.FindAction(actionName);
                if (action != null)
                    return action;
            }
        }

        return null;
    }
}
