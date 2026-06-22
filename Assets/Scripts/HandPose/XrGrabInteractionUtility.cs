using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class XrGrabInteractionUtility
{
    public const string InteractionManagerName = "XR Interaction Manager";
    public const string LeftControllerName = "Left Controller";
    public const string RightControllerName = "Right Controller";
    public const string DirectInteractorObjectName = "Direct Interactor";
    public const string DirectInteractorPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/Interactors/Direct Interactor.prefab";
    public const string LeftNearFarPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/Interactors/Left_NearFarInteractor.prefab";
    public const string RightNearFarPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/Interactors/Right_NearFarInteractor.prefab";

    const float DirectInteractorSphereRadius = 0.15f;

    public static XRInteractionManager EnsureInteractionManager()
    {
        var managers = Object.FindObjectsByType<XRInteractionManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var manager in managers)
        {
            if (manager != null && manager.gameObject.scene.IsValid())
                return manager;
        }

        var managerObject = new GameObject(InteractionManagerName);
#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCreatedObjectUndo(managerObject, "Create XR Interaction Manager");
#endif
        return managerObject.AddComponent<XRInteractionManager>();
    }

    public static void WireInteractionManager(XRInteractionManager manager)
    {
        if (manager == null)
            return;

#if UNITY_EDITOR
        foreach (var interactor in Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AssignInteractionManager(interactor, manager);

        foreach (var interactable in Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AssignInteractionManager(interactable, manager);
#endif
    }

    public static bool EnsureDirectInteractorOnController(string controllerName, Handedness handedness)
    {
        var controller = GameObject.Find(controllerName);
        if (controller == null)
            return false;

        var existingDirect = FindDirectInteractor(controller.transform);
        if (existingDirect != null)
        {
            ConfigureDirectInteractor(existingDirect, handedness, controller.transform);
            DisableNearFarInteractors(controller.transform);
            return false;
        }

#if UNITY_EDITOR
        var inputSource = FindInputSourceInteractor(controller.transform, handedness);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DirectInteractorPrefabPath);
        if (prefab == null)
            return false;

        var instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, controller.transform) as GameObject;
        if (instance == null)
            return false;

        instance.name = DirectInteractorObjectName;
        UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Add Direct Interactor");
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        var direct = instance.GetComponent<XRDirectInteractor>();
        if (direct == null)
            return false;

        if (inputSource != null)
            CopySelectInput(inputSource, direct);

        ConfigureDirectInteractor(direct, handedness, controller.transform);
        DisableNearFarInteractors(controller.transform);
        return true;
#else
        return false;
#endif
    }

    public static int SetupAllGrabInteractablesInScene()
    {
        var count = 0;
        var grabs = Object.FindObjectsByType<XRGrabInteractable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var grab in grabs)
        {
            if (grab == null || !grab.gameObject.scene.IsValid())
                continue;

            if (GrabbableObjectUtility.TrySetupGrabbable(grab.gameObject, out _))
                count++;
        }

        return count;
    }

    public static void SetupSceneForGrab()
    {
        var manager = EnsureInteractionManager();
        EnsureDirectInteractorOnController(LeftControllerName, Handedness.Left);
        EnsureDirectInteractorOnController(RightControllerName, Handedness.Right);
        WireInteractionManager(manager);
        SetupAllGrabInteractablesInScene();
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

    static XRBaseInteractor FindInputSourceInteractor(Transform controller, Handedness handedness)
    {
        foreach (var interactor in controller.GetComponentsInChildren<XRBaseInteractor>(true))
        {
            if (interactor is NearFarInteractor)
                return interactor;
        }

#if UNITY_EDITOR
        var prefabPath = handedness == Handedness.Right ? RightNearFarPrefabPath : LeftNearFarPrefabPath;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return prefab != null ? prefab.GetComponent<XRBaseInteractor>() : null;
#else
        return null;
#endif
    }

    static void ConfigureDirectInteractor(XRDirectInteractor direct, Handedness handedness, Transform controller)
    {
        if (direct == null)
            return;

        var sphere = direct.GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = direct.gameObject.AddComponent<SphereCollider>();

        sphere.isTrigger = true;
        sphere.radius = DirectInteractorSphereRadius;
        sphere.center = Vector3.zero;

#if UNITY_EDITOR
        var serialized = new UnityEditor.SerializedObject(direct);
        serialized.FindProperty("m_Handedness").enumValueIndex = (int)handedness;
        serialized.ApplyModifiedPropertiesWithoutUndo();
#endif

        direct.gameObject.SetActive(true);
    }

    static void DisableNearFarInteractors(Transform controller)
    {
        foreach (var interactor in controller.GetComponentsInChildren<NearFarInteractor>(true))
        {
            if (interactor != null)
                interactor.gameObject.SetActive(false);
        }
    }

#if UNITY_EDITOR
    static void AssignInteractionManager(Object target, XRInteractionManager manager)
    {
        var serialized = new UnityEditor.SerializedObject(target);
        var managerProp = serialized.FindProperty("m_InteractionManager");
        if (managerProp == null)
            return;

        managerProp.objectReferenceValue = manager;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CopySelectInput(XRBaseInteractor source, XRBaseInteractor target)
    {
        var sourceObject = new UnityEditor.SerializedObject(source);
        var targetObject = new UnityEditor.SerializedObject(target);
        var sourceSelect = sourceObject.FindProperty("m_SelectInput");
        var targetSelect = targetObject.FindProperty("m_SelectInput");
        if (sourceSelect == null || targetSelect == null)
            return;

        targetSelect.FindPropertyRelative("m_InputSourceMode").intValue =
            sourceSelect.FindPropertyRelative("m_InputSourceMode").intValue;
        targetSelect.FindPropertyRelative("m_InputActionReferencePerformed").objectReferenceValue =
            sourceSelect.FindPropertyRelative("m_InputActionReferencePerformed").objectReferenceValue;
        targetSelect.FindPropertyRelative("m_InputActionReferenceValue").objectReferenceValue =
            sourceSelect.FindPropertyRelative("m_InputActionReferenceValue").objectReferenceValue;
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }
#endif
}
