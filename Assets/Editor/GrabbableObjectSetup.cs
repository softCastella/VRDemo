using UnityEditor;
using UnityEngine;

public static class GrabbableObjectSetup
{
    [MenuItem("Tools/Hand Pose/Setup Grabbable Attach Point")]
    public static void SetupSelectedGrabbable()
    {
        var target = Selection.activeGameObject;
        if (target == null)
        {
            EditorUtility.DisplayDialog(
                "Grabbable Setup",
                "Hierarchy에서 Cube 등 잡을 오브젝트를 선택하세요.",
                "OK");
            return;
        }

        Undo.SetCurrentGroupName("Setup Grabbable Attach Point");
        var undoGroup = Undo.GetCurrentGroup();

        if (!GrabbableObjectUtility.TrySetupGrabbable(target, out var message))
        {
            EditorUtility.DisplayDialog("Grabbable Setup", message, "OK");
            return;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.DisplayDialog("Grabbable Setup", message, "OK");
    }

    [MenuItem("Tools/Hand Pose/Setup Grabbable Attach Point", true)]
    static bool SetupSelectedGrabbableValidate() => Selection.activeGameObject != null;

    [MenuItem("GameObject/XR/Setup Grabbable Object", false, 0)]
    static void SetupGrabbableContextMenu()
    {
        if (Selection.activeGameObject == null)
            return;

        if (GrabbableObjectUtility.TrySetupGrabbable(Selection.activeGameObject, out _))
            EditorSceneManagerMarkDirty();
    }

    [MenuItem("GameObject/XR/Setup Grabbable Object", true)]
    static bool SetupGrabbableContextMenuValidate() =>
        Selection.activeGameObject != null &&
        Selection.activeGameObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null;

    static void EditorSceneManagerMarkDirty()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
}
