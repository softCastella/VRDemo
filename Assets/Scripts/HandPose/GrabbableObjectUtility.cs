using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class GrabbableObjectUtility
{
    public const string AttachPointName = "AttachPoint";
    public const string ColliderChildName = "COL";
    public static readonly Vector3 DefaultAttachLocalPosition = new(0f, -0.5f, 0f);

    public static bool TrySetupGrabbable(GameObject target, out string message)
    {
        message = string.Empty;
        if (target == null)
        {
            message = "대상 오브젝트가 없습니다.";
            return false;
        }

        var grab = target.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            message = $"{target.name}에 XR Grab Interactable이 없습니다.";
            return false;
        }

        var collider = FindGrabCollider(target.transform);
        if (collider == null)
        {
            message = $"{target.name} 또는 자식({ColliderChildName})에 Collider가 없습니다.";
            return false;
        }

        var attachTransform = EnsureAttachPoint(target.transform);
        EnsureGrabReferences(grab, collider, attachTransform);
        EnsureRigidbodyForGrab(target);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
        if (target.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.scene);
#endif

        message =
            $"{target.name} 설정 완료:\n" +
            $"- Collider: {collider.name}\n" +
            "- AttachPoint 연결\n" +
            "- XR Grab Interactable 참조 등록";
        return true;
    }

    public static Collider FindGrabCollider(Transform root)
    {
        if (root == null)
            return null;

        var self = root.GetComponent<Collider>();
        if (self != null)
            return self;

        var colChild = root.Find(ColliderChildName);
        if (colChild != null)
        {
            var childCollider = colChild.GetComponent<Collider>();
            if (childCollider != null)
                return childCollider;
        }

        return root.GetComponentInChildren<Collider>(true);
    }

    static void EnsureRigidbodyForGrab(GameObject target)
    {
        var rb = target.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.useGravity = false;
        rb.isKinematic = true;
    }

    public static Transform EnsureAttachPoint(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.GetComponent<AttachPointGizmo>() != null)
                return child;

            if (child.name != AttachPointName)
                continue;

            EnsureAttachPointGizmo(child.gameObject);
            return child;
        }

        var attachObject = new GameObject(AttachPointName);
#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCreatedObjectUndo(attachObject, "Create Object Attach Point");
#endif
        attachObject.transform.SetParent(root, false);
        attachObject.transform.localPosition = DefaultAttachLocalPosition;
        attachObject.transform.localRotation = Quaternion.identity;
        attachObject.transform.localScale = Vector3.one;
        EnsureAttachPointGizmo(attachObject);
        return attachObject.transform;
    }

    static void EnsureAttachPointGizmo(GameObject attachObject)
    {
        if (attachObject.GetComponent<AttachPointGizmo>() != null)
            return;

#if UNITY_EDITOR
        UnityEditor.Undo.AddComponent<AttachPointGizmo>(attachObject);
#else
        attachObject.AddComponent<AttachPointGizmo>();
#endif
    }

    public static void EnsureGrabReferences(XRGrabInteractable grab, Collider collider, Transform attachTransform)
    {
#if UNITY_EDITOR
        var serializedGrab = new UnityEditor.SerializedObject(grab);
        var colliders = serializedGrab.FindProperty("m_Colliders");
        if (colliders != null)
        {
            colliders.ClearArray();
            colliders.InsertArrayElementAtIndex(0);
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
        }

        var attachProp = serializedGrab.FindProperty("m_AttachTransform");
        if (attachProp != null && attachTransform != null)
            attachProp.objectReferenceValue = attachTransform;

        serializedGrab.ApplyModifiedPropertiesWithoutUndo();
#else
        grab.colliders.Clear();
        if (collider != null)
            grab.colliders.Add(collider);
        if (attachTransform != null)
            grab.attachTransform = attachTransform;
#endif
    }
}
