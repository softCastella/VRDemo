using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

/// <summary>
/// Hand_Prac Play 진입 시 씬에 배치한 XR Origin·카메라 위치를 유지합니다.
/// 헤드셋이 붙으면 트래킹만 켜고, 데스크톱 Play일 때만 TrackedPoseDriver를 끕니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class HandPracPlayModeSupport : MonoBehaviour
{
    const float XrInitWaitSeconds = 0.75f;

    [SerializeField]
    bool m_EnableHandVisualizerIfDisabled = true;

    [SerializeField]
    [Tooltip("헤드셋 없이 Play할 때 HMD 트래킹 입력만 끕니다. Origin/카메라 Transform은 건드리지 않습니다.")]
    bool m_DisableHeadTrackingWhenNoHmd = true;

    TrackedPoseDriver m_TrackedPoseDriver;

    public static bool IsDesktopPlayMode => HandPracXrUtility.IsDesktopSession;

    void OnEnable()
    {
        HandPracXrUtility.ResetSession();
        StartCoroutine(InitializeWhenXrReady());
    }

    void OnDisable()
    {
        HandPracXrUtility.ResetSession();
    }

    IEnumerator InitializeWhenXrReady()
    {
        if (m_EnableHandVisualizerIfDisabled)
            EnsureHandVisualizerEnabled();

        yield return null;
        yield return new WaitForSeconds(XrInitWaitSeconds);

        if (HandPracXrUtility.HasActiveHeadMountedDisplay())
        {
            HandPracXrUtility.LockSessionAsHeadset();
            EnsureHeadsetCameraTracking();
            yield break;
        }

        HandPracXrUtility.LockSessionAsDesktop();

        if (!m_DisableHeadTrackingWhenNoHmd)
            yield break;

        if (!TryResolveMainCamera(out var mainCamera))
            yield break;

        m_TrackedPoseDriver = mainCamera.GetComponent<TrackedPoseDriver>();
        if (m_TrackedPoseDriver != null)
            m_TrackedPoseDriver.enabled = false;
    }

    void EnsureHeadsetCameraTracking()
    {
        if (!TryResolveMainCamera(out var mainCamera))
            return;

        m_TrackedPoseDriver = mainCamera.GetComponent<TrackedPoseDriver>();
        if (m_TrackedPoseDriver != null)
            m_TrackedPoseDriver.enabled = true;
    }

    static void EnsureHandVisualizerEnabled()
    {
        var visualizerObject = GameObject.Find("Hand Visualizer");
        if (visualizerObject != null && !visualizerObject.activeSelf)
            visualizerObject.SetActive(true);

        var visualizer = Object.FindFirstObjectByType<UnityEngine.XR.Hands.Samples.VisualizerSample.HandVisualizer>(
            FindObjectsInactive.Include);
        if (visualizer != null)
            visualizer.debugDrawJoints = true;
    }

    static bool TryResolveMainCamera(out Camera mainCamera)
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
            return true;

        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var camera in cameras)
        {
            if (!camera.enabled || !camera.gameObject.name.Contains("Main"))
                continue;

            mainCamera = camera;
            return true;
        }

        return false;
    }
}

public static class HandPracXrUtility
{
    static bool s_SessionResolved;
    static bool s_HeadsetSession;

    public static bool IsDesktopSession => !IsHeadsetSession;

    public static bool IsHeadsetSession
    {
        get
        {
            if (!s_SessionResolved)
                s_HeadsetSession = HasActiveHeadMountedDisplay();

            return s_HeadsetSession;
        }
    }

    public static void LockSessionAsHeadset()
    {
        s_SessionResolved = true;
        s_HeadsetSession = true;
    }

    public static void LockSessionAsDesktop()
    {
        s_SessionResolved = true;
        s_HeadsetSession = false;
    }

    public static void ResetSession()
    {
        s_SessionResolved = false;
        s_HeadsetSession = false;
    }

    public static bool HasActiveHeadMountedDisplay()
    {
        if (XRSettings.enabled && XRSettings.isDeviceActive)
        {
            var deviceName = XRSettings.loadedDeviceName ?? string.Empty;
            if (deviceName.Length > 0 &&
                !deviceName.Contains("Mock", System.StringComparison.OrdinalIgnoreCase) &&
                !deviceName.Contains("Null", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var headDevices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
        foreach (var device in headDevices)
        {
            if (device.isValid)
                return true;
        }

        return false;
    }
}
