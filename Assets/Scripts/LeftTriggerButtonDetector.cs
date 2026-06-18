using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Left controller input detection with Debug.Log output.
/// </summary>
[DisallowMultipleComponent]
public class LeftTriggerButtonDetector : MonoBehaviour
{
    const string LeftDeviceMapName = "XRI Left";
    const string LeftInteractionMapName = "XRI Left Interaction";
    const string TriggerButtonValueActionName = "Activate Value";
    const string GripButtonActionName = "Select";
    const string ThumbstickActionName = "Thumbstick";
    const string XButtonBinding = "<XRController>{LeftHand}/{PrimaryButton}";
    const string YButtonBinding = "<XRController>{LeftHand}/{SecondaryButton}";

    [SerializeField]
    InputActionReference m_TriggerValueAction;

    [SerializeField]
    InputActionReference m_GripButtonAction;

    [SerializeField]
    InputActionReference m_ThumbstickAction;

    [SerializeField]
    [Range(0.01f, 1f)]
    float m_PressThreshold = 0.1f;

    [SerializeField]
    [Range(0.01f, 0.5f)]
    float m_ThumbstickDeadzone = 0.1f;

    InputAction m_TriggerAction;
    InputAction m_GripButtonActionResolved;
    InputAction m_ThumbstickActionResolved;
    InputAction m_XButtonAction;
    InputAction m_YButtonAction;

    bool m_WasTriggerPressed;
    bool m_WasGripPressed;
    bool m_WasXPressed;
    bool m_WasYPressed;
    bool m_WasThumbstickActive;

    void OnEnable()
    {
        m_TriggerAction = ResolveAction(m_TriggerValueAction, LeftInteractionMapName, TriggerButtonValueActionName);
        EnableAction(m_TriggerAction, TriggerButtonValueActionName, out m_WasTriggerPressed,
            () => m_TriggerAction.ReadValue<float>() >= m_PressThreshold);

        m_GripButtonActionResolved = ResolveAction(m_GripButtonAction, LeftInteractionMapName, GripButtonActionName);
        EnableAction(m_GripButtonActionResolved, GripButtonActionName, out m_WasGripPressed, () => IsPressed(m_GripButtonActionResolved));

        m_ThumbstickActionResolved = ResolveAction(m_ThumbstickAction, LeftDeviceMapName, ThumbstickActionName);
        if (m_ThumbstickActionResolved == null)
        {
            Debug.LogWarning(
                $"{nameof(LeftTriggerButtonDetector)}: '{ThumbstickActionName}' action not found.",
                this);
        }
        else
        {
            m_ThumbstickActionResolved.Enable();
            m_WasThumbstickActive = false;
        }

        m_XButtonAction = new InputAction("LeftXButton", InputActionType.Button, XButtonBinding);
        m_YButtonAction = new InputAction("LeftYButton", InputActionType.Button, YButtonBinding);
        m_XButtonAction.Enable();
        m_YButtonAction.Enable();
        m_WasXPressed = IsPressed(m_XButtonAction);
        m_WasYPressed = IsPressed(m_YButtonAction);
    }

    void OnDisable()
    {
        m_TriggerAction?.Disable();
        m_GripButtonActionResolved?.Disable();
        m_ThumbstickActionResolved?.Disable();

        m_XButtonAction?.Disable();
        m_XButtonAction?.Dispose();
        m_XButtonAction = null;

        m_YButtonAction?.Disable();
        m_YButtonAction?.Dispose();
        m_YButtonAction = null;
    }

    void Update()
    {
        UpdateTriggerButton();
        UpdateGripButton();
        UpdateXButton();
        UpdateYButton();
        UpdateThumbstick();
    }

    void UpdateTriggerButton()
    {
        if (m_TriggerAction == null)
            return;

        var value = m_TriggerAction.ReadValue<float>();
        var pressed = value >= m_PressThreshold;

        if (pressed && !m_WasTriggerPressed)
            Debug.Log($"[LeftTriggerButton] Pressed — value: {value:F2}", this);
        else if (!pressed && m_WasTriggerPressed)
            Debug.Log("[LeftTriggerButton] Released", this);

        m_WasTriggerPressed = pressed;
    }

    void UpdateGripButton()
    {
        if (m_GripButtonActionResolved == null)
            return;

        var pressed = IsPressed(m_GripButtonActionResolved);

        if (pressed && !m_WasGripPressed)
            Debug.Log("[LeftGripButton] Pressed", this);
        else if (!pressed && m_WasGripPressed)
            Debug.Log("[LeftGripButton] Released", this);

        m_WasGripPressed = pressed;
    }

    void UpdateXButton()
    {
        if (m_XButtonAction == null)
            return;

        var pressed = IsPressed(m_XButtonAction);

        if (pressed && !m_WasXPressed)
            Debug.Log("[LeftXButton] Pressed", this);
        else if (!pressed && m_WasXPressed)
            Debug.Log("[LeftXButton] Released", this);

        m_WasXPressed = pressed;
    }

    void UpdateYButton()
    {
        if (m_YButtonAction == null)
            return;

        var pressed = IsPressed(m_YButtonAction);

        if (pressed && !m_WasYPressed)
            Debug.Log("[LeftYButton] Pressed", this);
        else if (!pressed && m_WasYPressed)
            Debug.Log("[LeftYButton] Released", this);

        m_WasYPressed = pressed;
    }

    void UpdateThumbstick()
    {
        if (m_ThumbstickActionResolved == null)
            return;

        var value = m_ThumbstickActionResolved.ReadValue<Vector2>();
        var active = IsThumbstickActive(value);

        if (active && !m_WasThumbstickActive)
            Debug.Log($"[LeftThumbstick] ({value.x:F2},{value.y:F2})", this);

        m_WasThumbstickActive = active;
    }

    bool IsThumbstickActive(Vector2 value)
    {
        return value.sqrMagnitude >= m_ThumbstickDeadzone * m_ThumbstickDeadzone;
    }

    static bool IsPressed(InputAction action)
    {
        return action.ReadValue<float>() >= 0.5f;
    }

    void EnableAction(InputAction action, string actionName, out bool wasPressed, System.Func<bool> readPressed)
    {
        if (action == null)
        {
            Debug.LogWarning($"{nameof(LeftTriggerButtonDetector)}: '{actionName}' action not found.", this);
            wasPressed = false;
            return;
        }

        action.Enable();
        wasPressed = readPressed();
    }

    InputAction ResolveAction(InputActionReference reference, string actionMapName, string actionName)
    {
        if (reference != null && reference.action != null)
            return reference.action;

        var managers = FindObjectsByType<InputActionManager>();
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
