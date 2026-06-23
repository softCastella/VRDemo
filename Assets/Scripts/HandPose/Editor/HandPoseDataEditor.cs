using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HandPoseData))]
public class HandPoseDataEditor : Editor
{
    bool m_PreviewInFrontOfCamera = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var pose = (HandPoseData)target;
        if (pose == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("본 개수", pose.Bones.Count.ToString());
        EditorGUILayout.LabelField(
            "좌표계",
            pose.IsWristAtOrigin() ? "손목 기준 (0,0,0)" : "레거시");

        if (pose.NeedsWristOriginMigration())
        {
            if (GUILayout.Button("손목 기준 (0,0,0)으로 변환"))
            {
                if (HandPoseCaptureWindow.MigratePoseAsset(pose))
                {
                    AssetDatabase.SaveAssets();
                    EditorUtility.SetDirty(pose);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("씬에 적용"))
            {
                if (HandPoseEditorPreview.TryApplyPreview(pose, out var message, m_PreviewInFrontOfCamera))
                    Debug.Log(message);
                else
                    EditorUtility.DisplayDialog("포즈 미리보기", message, "OK");
            }

            if (GUILayout.Button("미리보기 해제"))
                HandPoseEditorPreview.ClearPreview();
        }

        m_PreviewInFrontOfCamera = EditorGUILayout.Toggle(
            new GUIContent("Game View용 — Main Camera 앞에 배치"),
            m_PreviewInFrontOfCamera);

        if (!string.IsNullOrEmpty(HandPoseEditorPreview.StatusMessage))
            EditorGUILayout.HelpBox(HandPoseEditorPreview.StatusMessage, MessageType.None);
    }
}
