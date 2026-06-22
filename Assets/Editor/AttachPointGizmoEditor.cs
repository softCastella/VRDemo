using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttachPointGizmo))]
public class AttachPointGizmoEditor : Editor
{
    SerializedProperty m_Radius;
    SerializedProperty m_WireColor;
    SerializedProperty m_DrawFaceFill;
    SerializedProperty m_FaceColor;
    SerializedProperty m_PlayControllerLiftY;

    void OnEnable()
    {
        m_Radius = serializedObject.FindProperty("m_Radius");
        m_WireColor = serializedObject.FindProperty("m_WireColor");
        m_DrawFaceFill = serializedObject.FindProperty("m_DrawFaceFill");
        m_FaceColor = serializedObject.FindProperty("m_FaceColor");
        m_PlayControllerLiftY = serializedObject.FindProperty("m_PlayControllerLiftY");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Face Color는 Scene 뷰 기즈모 전용입니다. 알파 0.5 이상 권장.",
            MessageType.Info);

        EditorGUILayout.PropertyField(m_Radius, new GUIContent("Radius (Local)"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Gizmo Colors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_DrawFaceFill, new GUIContent("Face Fill"));
        EditorGUI.BeginDisabledGroup(!m_DrawFaceFill.boolValue);
        EditorGUILayout.PropertyField(m_FaceColor, new GUIContent("Face Color"));
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.PropertyField(m_WireColor, new GUIContent("Wire Color"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(m_PlayControllerLiftY);

        serializedObject.ApplyModifiedProperties();
    }
}
