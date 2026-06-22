using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Play Mode 시작 시 Grab 설정을 자동 보정합니다 (null collider 제거, Direct Interactor 보장).
/// </summary>
[DefaultExecutionOrder(-200)]
public class XrGrabRuntimeBootstrap : MonoBehaviour
{
    [SerializeField]
    bool m_DisableNearFarInteractors = true;

    [SerializeField]
    float m_DirectInteractorRadius = 0.15f;

    const string LeftControllerName = "Left Controller";
    const string RightControllerName = "Right Controller";
    const string DirectInteractorObjectName = "Direct Interactor";

    void Awake()
    {
        var manager = FindFirstObjectByType<XRInteractionManager>();
        FixGrabInteractables(manager);
        EnsureDirectInteractor(LeftControllerName, InteractorHandedness.Left, manager);
        EnsureDirectInteractor(RightControllerName, InteractorHandedness.Right, manager);
    }

    static void FixGrabInteractables(XRInteractionManager manager)
    {
        var grabs = FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var grab in grabs)
        {
            if (grab == null || !grab.gameObject.scene.IsValid())
                continue;

            for (var i = grab.colliders.Count - 1; i >= 0; i--)
            {
                if (grab.colliders[i] == null)
                    grab.colliders.RemoveAt(i);
            }

            if (manager != null && grab.interactionManager == null)
                grab.interactionManager = manager;

            var collider = GrabbableObjectUtility.FindGrabCollider(grab.transform);
            if (collider != null && !grab.colliders.Contains(collider))
                grab.colliders.Add(collider);
        }
    }

    void EnsureDirectInteractor(string controllerName, InteractorHandedness handedness, XRInteractionManager manager)
    {
        var controller = GameObject.Find(controllerName);
        if (controller == null)
            return;

        var inputSource = FindInputSource(controller.transform);
        var direct = FindDirectInteractor(controller.transform);

        if (direct == null)
            direct = CreateDirectInteractor(controller.transform, handedness);

        ConfigureDirectInteractor(direct, inputSource, manager);

        if (!m_DisableNearFarInteractors || inputSource == null)
            return;

        foreach (var nearFar in controller.GetComponentsInChildren<NearFarInteractor>(true))
        {
            if (nearFar != null && nearFar != inputSource)
                nearFar.gameObject.SetActive(false);
        }

        if (inputSource is NearFarInteractor nearFarSource)
            nearFarSource.gameObject.SetActive(false);
    }

    static XRDirectInteractor FindDirectInteractor(Transform controller)
    {
        foreach (var interactor in controller.GetComponentsInChildren<XRDirectInteractor>(true))
        {
            if (interactor != null)
                return interactor;
        }

        return null;
    }

    static XRBaseInputInteractor FindInputSource(Transform controller)
    {
        foreach (var interactor in controller.GetComponentsInChildren<XRBaseInputInteractor>(true))
        {
            if (interactor is NearFarInteractor or XRDirectInteractor)
                return interactor;
        }

        return null;
    }

    XRDirectInteractor CreateDirectInteractor(Transform controller, InteractorHandedness handedness)
    {
        var interactorObject = new GameObject(DirectInteractorObjectName);
        interactorObject.transform.SetParent(controller, false);
        interactorObject.transform.localPosition = Vector3.zero;
        interactorObject.transform.localRotation = Quaternion.identity;
        interactorObject.transform.localScale = Vector3.one;

        var sphere = interactorObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = m_DirectInteractorRadius;
        sphere.center = Vector3.zero;

        var direct = interactorObject.AddComponent<XRDirectInteractor>();
        direct.handedness = handedness;
        return direct;
    }

    void ConfigureDirectInteractor(
        XRDirectInteractor direct,
        XRBaseInputInteractor inputSource,
        XRInteractionManager manager)
    {
        if (direct == null)
            return;

        var sphere = direct.GetComponent<SphereCollider>();
        if (sphere == null)
        {
            sphere = direct.gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = m_DirectInteractorRadius;
            sphere.center = Vector3.zero;
        }
        else
        {
            sphere.isTrigger = true;
            sphere.radius = m_DirectInteractorRadius;
            sphere.center = Vector3.zero;
        }

        if (inputSource != null && inputSource != direct)
        {
            direct.selectInput = inputSource.selectInput;
            direct.activateInput = inputSource.activateInput;
            direct.selectActionTrigger = inputSource.selectActionTrigger;
        }

        if (manager != null)
            direct.interactionManager = manager;

        direct.gameObject.SetActive(true);
    }
}
