using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class Hand : MonoBehaviour
{
    const string LeftInteractionMapName = "XRI Left Interaction";
    const string RightInteractionMapName = "XRI Right Interaction";
    const string GripValueActionName = "Select Value";

    [SerializeField]
    InputDeviceCharacteristics inputDeviceCharacteristics;

    [SerializeField]
    Animator _handAnimator;

    UnityEngine.XR.InputDevice _targetDevice;
    InputAction _fallbackGripAction;
    bool _useLeftHand = true;

    void OnEnable()
    {
        EnsureDeviceCharacteristics();
        ResolveFallbackGripAction();
        _fallbackGripAction?.Enable();
    }

    void OnDisable()
    {
        _fallbackGripAction?.Disable();
    }

    void Start()
    {
        InitializeHand();
    }

    void Update()
    {
        if (!_targetDevice.isValid)
            InitializeHand();

        UpdateHand();
    }

    void EnsureDeviceCharacteristics()
    {
        if (inputDeviceCharacteristics != InputDeviceCharacteristics.None)
            return;

        var name = gameObject.name.ToLowerInvariant();
        _useLeftHand = name.Contains("left") || name.EndsWith("_l");
        inputDeviceCharacteristics = (_useLeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right)
            | InputDeviceCharacteristics.Controller;
    }

    void InitializeHand()
    {
        var devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(inputDeviceCharacteristics, devices);

        if (devices.Count > 0)
            _targetDevice = devices[0];
    }

    void ResolveFallbackGripAction()
    {
        var mapName = _useLeftHand ? LeftInteractionMapName : RightInteractionMapName;
        var managers = FindObjectsByType<InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var manager in managers)
        {
            foreach (var asset in manager.actionAssets)
            {
                if (asset == null)
                    continue;

                var action = asset.FindActionMap(mapName)?.FindAction(GripValueActionName);
                if (action == null)
                    continue;

                _fallbackGripAction = action;
                return;
            }
        }
    }

    void UpdateHand()
    {
        if (_handAnimator == null)
            return;

        var grip = ReadGripValue();
        _handAnimator.SetFloat("Grip", grip);
    }

    float ReadGripValue()
    {
        if (_targetDevice.isValid && _targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float deviceGrip))
            return deviceGrip;

        if (_fallbackGripAction != null)
            return _fallbackGripAction.ReadValue<float>();

        return 0f;
    }
}
