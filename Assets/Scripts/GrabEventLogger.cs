using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 잡기 이벤트 로그. 씬 전체에 <b>하나만</b> 부착합니다.
/// Left/Right Controller(Controller), Cube(GrabTarget), 빈 GameObject(Custom) 중 한 곳에만 둡니다.
/// 잡힌 대상의 색상·종류는 <see cref="InteractableGrabInfo"/>에서 읽습니다.
/// </summary>
[DisallowMultipleComponent]
public class GrabEventLogger : MonoBehaviour
{
    static GrabEventLogger s_Instance;

    [SerializeField]
    GrabListenerHostRole m_HostRole = GrabListenerHostRole.Controller;

    [SerializeField]
    string m_CustomListenerName;

    [SerializeField]
    Transform m_LeftControllerRoot;

    [SerializeField]
    Transform m_RightControllerRoot;

    [SerializeField]
    List<XRGrabInteractable> m_SceneGrabTargets = new();

    readonly List<XRGrabInteractable> m_SubscribedInteractables = new();
    readonly List<XRBaseInteractor> m_SubscribedInteractors = new();

    string OwnerName =>
        string.IsNullOrWhiteSpace(m_CustomListenerName) ? gameObject.name : m_CustomListenerName;

    void OnEnable()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Debug.LogWarning(
                $"{nameof(GrabEventLogger)}는 씬에 하나만 사용합니다. 이미 '{s_Instance.OwnerName}'에 있으므로 '{OwnerName}'의 컴포넌트를 끕니다.",
                this);
            enabled = false;
            return;
        }

        s_Instance = this;
        CacheControllerRoots();
        SubscribeByHostRole();
    }

    void Start()
    {
        if (!enabled)
            return;

        if (m_HostRole == GrabListenerHostRole.GrabTarget && m_SubscribedInteractables.Count == 0)
            SubscribeGrabTarget();
    }

    void OnDisable()
    {
        UnsubscribeAll();

        if (s_Instance == this)
            s_Instance = null;
    }

    void CacheControllerRoots()
    {
        if (m_LeftControllerRoot == null)
            m_LeftControllerRoot = FindTransformByName("Left Controller");

        if (m_RightControllerRoot == null)
            m_RightControllerRoot = FindTransformByName("Right Controller");
    }

    void SubscribeByHostRole()
    {
        switch (m_HostRole)
        {
            case GrabListenerHostRole.Controller:
                SubscribeControllerInteractors();
                break;
            case GrabListenerHostRole.GrabTarget:
                SubscribeGrabTarget();
                break;
            case GrabListenerHostRole.Custom:
                SubscribeCustomTargets();
                break;
        }
    }

    void SubscribeControllerInteractors()
    {
        CacheControllerRoots();
        SubscribeInteractorsUnderRoot(m_LeftControllerRoot);
        SubscribeInteractorsUnderRoot(m_RightControllerRoot);

        if (m_SubscribedInteractors.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(GrabEventLogger)}: Left/Right Controller에서 XR Interactor를 찾지 못했습니다. Controller Root 참조를 확인하세요.",
                this);
        }
    }

    void SubscribeInteractorsUnderRoot(Transform root)
    {
        if (root == null)
            return;

        var interactors = root.GetComponentsInChildren<XRBaseInteractor>(true);
        foreach (var interactor in interactors)
        {
            if (interactor == null || m_SubscribedInteractors.Contains(interactor))
                continue;

            interactor.selectEntered.AddListener(OnSelectEntered);
            interactor.selectExited.AddListener(OnSelectExited);
            interactor.hoverEntered.AddListener(OnHoverEntered);
            interactor.hoverExited.AddListener(OnHoverExited);
            m_SubscribedInteractors.Add(interactor);
        }
    }

    void SubscribeGrabTarget()
    {
        var grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            Debug.LogWarning($"{nameof(GrabEventLogger)}: GrabTarget 모드에는 XRGrabInteractable이 필요합니다.", this);
            return;
        }

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
        m_SubscribedInteractables.Add(grabInteractable);
    }

    void SubscribeCustomTargets()
    {
        if (m_SceneGrabTargets.Count == 0)
        {
            Debug.LogWarning($"{nameof(GrabEventLogger)}: Custom 모드에는 감시할 XRGrabInteractable을 Inspector에 지정하세요.", this);
            return;
        }

        foreach (var grabInteractable in m_SceneGrabTargets)
            SubscribeInteractable(grabInteractable);
    }

    void SubscribeInteractable(XRGrabInteractable grabInteractable)
    {
        if (grabInteractable == null || m_SubscribedInteractables.Contains(grabInteractable))
            return;

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
        m_SubscribedInteractables.Add(grabInteractable);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log(BuildGrabMessage(args), this);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        Debug.Log(BuildReleaseMessage(args), this);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        Debug.Log(BuildHoverEnterMessage(args), this);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        Debug.Log(BuildHoverExitMessage(args), this);
    }

    string BuildGrabMessage(SelectEnterEventArgs args)
    {
        var interactable = args.interactableObject;
        var grabbedObject = (interactable as Component)?.gameObject;
        var grabbedName = grabbedObject != null ? grabbedObject.name : "알 수 없는 대상";
        var handSide = ResolveHandSide(args.interactorObject);
        var coloredLabel = BuildColoredObjectLabel(grabbedObject, grabbedName);

        // return $"스크립트 보유: {OwnerName} ({ToHostRoleKorean(m_HostRole)}) | " +
        //        $"잡힌 대상: {grabbedName} | 색: {ToColorNounKorean(color)} | 종류: {ToObjectTypeKorean(objectType)} | " +
        //        $"{ToHandSideKorean(handSide)}으로 잡았습니다.";
        return $"{ToHandSideEnglish(handSide)}으로 {coloredLabel}를 잡았다!";
    }

    string BuildReleaseMessage(SelectExitEventArgs args)
    {
        var interactable = args.interactableObject;
        var releasedObject = (interactable as Component)?.gameObject;
        var releasedName = releasedObject != null ? releasedObject.name : "알 수 없는 대상";
        var handSide = ResolveHandSide(args.interactorObject);
        var coloredLabel = BuildColoredObjectLabel(releasedObject, releasedName);

        return $"{ToHandSideEnglish(handSide)}으로 {coloredLabel}를 놓았다!";
    }

    string BuildHoverEnterMessage(HoverEnterEventArgs args)
    {
        var interactable = args.interactableObject;
        var hoveredObject = (interactable as Component)?.gameObject;
        var hoveredName = hoveredObject != null ? hoveredObject.name : "알 수 없는 대상";
        var handSide = ResolveHandSide(args.interactorObject);
        var coloredLabel = BuildColoredObjectLabel(hoveredObject, hoveredName);

        return $"{ToHandSideEnglish(handSide)}으로 {coloredLabel}에 닿았다!";
    }

    string BuildHoverExitMessage(HoverExitEventArgs args)
    {
        var interactable = args.interactableObject;
        var hoveredObject = (interactable as Component)?.gameObject;
        var hoveredName = hoveredObject != null ? hoveredObject.name : "알 수 없는 대상";
        var handSide = ResolveHandSide(args.interactorObject);
        var coloredLabel = BuildColoredObjectLabel(hoveredObject, hoveredName);

        return $"{ToHandSideEnglish(handSide)}으로 {coloredLabel}에서 벗어났다!";
    }

    HandSide ResolveHandSide(IXRInteractor interactor)
    {
        var interactorTransform = (interactor as Component)?.transform;
        if (interactorTransform == null)
            return HandSide.Unknown;

        if (m_LeftControllerRoot != null && interactorTransform.IsChildOf(m_LeftControllerRoot))
            return HandSide.Left;

        if (m_RightControllerRoot != null && interactorTransform.IsChildOf(m_RightControllerRoot))
            return HandSide.Right;

        var current = interactorTransform;
        while (current != null)
        {
            if (current.name.Contains("Left Controller"))
                return HandSide.Left;

            if (current.name.Contains("Right Controller"))
                return HandSide.Right;

            current = current.parent;
        }

        return HandSide.Unknown;
    }

    static InteractableObjectType GuessObjectType(string objectName)
    {
        return objectName.Contains("Cylinder")
            ? InteractableObjectType.Cylinder
            : InteractableObjectType.Cube;
    }

    static InteractableGrabInfo FindGrabInfo(GameObject targetObject)
    {
        if (targetObject == null)
            return null;

        return targetObject.GetComponent<InteractableGrabInfo>()
            ?? targetObject.GetComponentInParent<InteractableGrabInfo>()
            ?? targetObject.GetComponentInChildren<InteractableGrabInfo>();
    }

    static string BuildColoredObjectLabel(GameObject targetObject, string fallbackName)
    {
        var grabInfo = FindGrabInfo(targetObject);
        var objectType = grabInfo != null ? grabInfo.ObjectType : GuessObjectType(fallbackName);
        var color = grabInfo != null ? grabInfo.Color : InteractableColor.Red;

        return $"{ToColorNounKorean(color)}{ToObjectTypeKorean(objectType)}";
    }

    static string ToHandSideKorean(HandSide handSide)
    {
        return handSide switch
        {
            HandSide.Left => "왼손",
            HandSide.Right => "오른손",
            _ => "알 수 없는 손"
        };
    }

    static string ToHandSideEnglish(HandSide handSide)
    {
        return handSide switch
        {
            HandSide.Left => "Left Hand",
            HandSide.Right => "Right Hand",
            _ => "Unknown Hand"
        };
    }

    static string ToObjectTypeEnglish(InteractableObjectType objectType)
    {
        return objectType switch
        {
            InteractableObjectType.Cube => "Cube",
            InteractableObjectType.Cylinder => "Cylinder",
            _ => "Object"
        };
    }

    static string ToObjectTypeKorean(InteractableObjectType objectType)
    {
        return objectType switch
        {
            InteractableObjectType.Cube => "큐브",
            InteractableObjectType.Cylinder => "실린더",
            _ => "오브젝트"
        };
    }

    static string ToColorNounKorean(InteractableColor color)
    {
        return color switch
        {
            InteractableColor.Red => "빨강",
            InteractableColor.Blue => "파랑",
            InteractableColor.Green => "초록",
            InteractableColor.Yellow => "노랑",
            _ => "알 수 없음"
        };
    }

    static string ToHostRoleKorean(GrabListenerHostRole hostRole)
    {
        return hostRole switch
        {
            GrabListenerHostRole.Controller => "컨트롤러",
            GrabListenerHostRole.GrabTarget => "잡기 대상",
            GrabListenerHostRole.Custom => "커스텀",
            _ => "리스너"
        };
    }

    void UnsubscribeAll()
    {
        foreach (var interactor in m_SubscribedInteractors)
        {
            interactor.selectEntered.RemoveListener(OnSelectEntered);
            interactor.selectExited.RemoveListener(OnSelectExited);
            interactor.hoverEntered.RemoveListener(OnHoverEntered);
            interactor.hoverExited.RemoveListener(OnHoverExited);
        }

        foreach (var grabInteractable in m_SubscribedInteractables)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
            grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            grabInteractable.hoverExited.RemoveListener(OnHoverExited);
        }

        m_SubscribedInteractors.Clear();
        m_SubscribedInteractables.Clear();
    }

    static Transform FindTransformByName(string objectName)
    {
        var found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }
}
