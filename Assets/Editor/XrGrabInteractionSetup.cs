using UnityEditor;
using UnityEngine;

public static class XrGrabInteractionSetup
{
    [MenuItem("Tools/Hand Pose/Setup Scene For XR Grab")]
    public static void SetupSceneForXrGrab()
    {
        Undo.SetCurrentGroupName("Setup Scene For XR Grab");
        var undoGroup = Undo.GetCurrentGroup();

        XrGrabInteractionUtility.SetupSceneForGrab();
        EditorSceneManagerMarkDirty();

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "XR Grab Setup",
            "씬 Grab 설정을 갱신했습니다.\n\n" +
            "- XR Interaction Manager 연결\n" +
            "- Direct Interactor (손 닿으면 잡기)\n" +
            "- NearFar Interactor 비활성화\n" +
            "- Grab Collider null 항목 제거",
            "OK");
    }

    [MenuItem("Tools/Hand Pose/Setup Scene For XR Grab", true)]
    static bool SetupSceneForXrGrabValidate() => !Application.isPlaying;

    static void EditorSceneManagerMarkDirty()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
}
