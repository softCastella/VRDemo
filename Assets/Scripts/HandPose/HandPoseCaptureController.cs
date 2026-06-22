using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

[DisallowMultipleComponent]
public class HandPoseCaptureController : MonoBehaviour
{
    const string GripButtonActionName = "Select";
    const string GripValueActionName = "Select Value";

    [SerializeField]
    Handedness m_CaptureHandedness = Handedness.Left;

    [SerializeField, FormerlySerializedAs("m_LeftHandSkeletonDriver")]
    XRHandSkeletonDriver m_HandSkeletonDriver;

    [SerializeField]
    [Range(0.5f, 1f)]
    float m_GripValueThreshold = 0.75f;

    InputAction m_GripButtonAction;
    InputAction m_GripValueAction;
    bool m_OwnsGripButtonAction;
    bool m_OwnsGripValueAction;
    bool m_WasGripPressed;
    Handedness m_ResolvedHandedness;

    void OnEnable()
    {
        m_ResolvedHandedness = HandPoseCaptureSession.CaptureHandedness;
        ResolveGripActions(m_ResolvedHandedness);
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

        var handedness = HandPoseCaptureSession.CaptureHandedness;
        if (handedness != m_ResolvedHandedness)
        {
            m_ResolvedHandedness = handedness;
            ResolveGripActions(handedness);
            m_GripButtonAction?.Enable();
            m_GripValueAction?.Enable();
            m_WasGripPressed = IsGripPressed();
        }

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
        var handedness = HandPoseCaptureSession.CaptureHandedness;
        var driver = ResolveHandSkeletonDriver(handedness);
        if (driver == null)
        {
            HandPoseCaptureSession.SetStatus(
                $"{GetHandLabel(handedness)} XRHandSkeletonDriver를 찾지 못했습니다. Hand Visualizer가 활성인지 확인하세요.");
            return;
        }

        if (!TryCaptureFromSkeletonDriver(driver, out var bones))
        {
            HandPoseCaptureSession.SetStatus(
                $"트래킹된 관절이 없습니다. {GetHandLabel(handedness)}이 인식된 상태에서 다시 시도하세요.");
            return;
        }

        var poseName = string.IsNullOrWhiteSpace(HandPoseCaptureSession.NextPoseName)
            ? "HandPose"
            : HandPoseCaptureSession.NextPoseName.Trim();

        var snapshot = new HandPoseSnapshot(poseName, handedness, bones);
        HandPoseCaptureSession.NotifyPoseCaptured(snapshot);
        HandPoseCaptureSession.SetStatus($"캡처 완료 ({GetHandLabel(handedness)}): {poseName} ({bones.Count} bones)");
    }

    static string GetHandLabel(Handedness handedness) =>
        handedness == Handedness.Right ? "오른손" : "왼손";

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

    XRHandSkeletonDriver ResolveHandSkeletonDriver(Handedness handedness)
    {
        if (m_HandSkeletonDriver != null)
        {
            var events = m_HandSkeletonDriver.GetComponent<XRHandTrackingEvents>();
            if (events == null || events.handedness == handedness)
                return m_HandSkeletonDriver;
        }

        var drivers = FindObjectsByType<XRHandSkeletonDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var driver in drivers)
        {
            var handEvents = driver.GetComponent<XRHandTrackingEvents>();
            if (handEvents != null && handEvents.handedness == handedness)
                return driver;
        }

        return null;
    }

    void ResolveGripActions(Handedness handedness)
    {
        DisposeOwnedGripActions();

        var interactionMapName = handedness == Handedness.Right
            ? "XRI Right Interaction"
            : "XRI Left Interaction";

        var handDevice = handedness == Handedness.Right ? "{RightHand}" : "{LeftHand}";

        m_GripButtonAction = FindAction(interactionMapName, GripButtonActionName);
        m_GripValueAction = FindAction(interactionMapName, GripValueActionName);

        if (m_GripButtonAction == null)
        {
            m_GripButtonAction = new InputAction(
                $"HandPose{(handedness == Handedness.Right ? "Right" : "Left")}GripButton",
                InputActionType.Button,
                $"<XRController>{handDevice}/{{GripButton}}");
            m_OwnsGripButtonAction = true;
        }

        if (m_GripValueAction == null)
        {
            m_GripValueAction = new InputAction(
                $"HandPose{(handedness == Handedness.Right ? "Right" : "Left")}GripValue",
                InputActionType.Value,
                $"<XRController>{handDevice}/{{Grip}}");
            m_OwnsGripValueAction = true;
        }
    }

    void DisposeOwnedGripActions()
    {
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
