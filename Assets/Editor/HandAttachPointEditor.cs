using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

[CustomEditor(typeof(HandAttachPoint))]
public class HandAttachPointEditor : Editor
{
    static readonly Color LeftLabelColor = new(0.2f, 0.85f, 1f);
    static readonly Color RightLabelColor = new(1f, 0.6f, 0.15f);
    static readonly Color LeftIndexLabelColor = new(0.95f, 0.9f, 0.15f);
    static readonly Color RightIndexLabelColor = new(1f, 0.35f, 0.85f);

    SerializedProperty m_Handedness;
    SerializedProperty m_Kind;
    SerializedProperty m_Radius;
    SerializedProperty m_WireColor;
    SerializedProperty m_DrawFaceFill;
    SerializedProperty m_FaceColor;

    void OnEnable()
    {
        m_Handedness = serializedObject.FindProperty("m_Handedness");
        m_Kind = serializedObject.FindProperty("m_Kind");
        m_Radius = serializedObject.FindProperty("m_Radius");
        m_WireColor = serializedObject.FindProperty("m_WireColor");
        m_DrawFaceFill = serializedObject.FindProperty("m_DrawFaceFill");
        m_FaceColor = serializedObject.FindProperty("m_FaceColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Wrist: 손목 기준점\n" +
            "Index Near: 검지 뿌리(근위골) 니어 쪽 — 물체 표면 맞춤용\n\n" +
            "Face Color는 Scene 뷰 기즈모 전용입니다.",
            MessageType.Info);

        EditorGUILayout.PropertyField(m_Kind);
        EditorGUILayout.PropertyField(m_Handedness);
        EditorGUILayout.PropertyField(m_Radius, new GUIContent("Radius (Local)"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Gizmo Colors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_DrawFaceFill, new GUIContent("Face Fill"));
        EditorGUI.BeginDisabledGroup(!m_DrawFaceFill.boolValue);
        EditorGUILayout.PropertyField(m_FaceColor, new GUIContent("Face Color"));
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.PropertyField(m_WireColor, new GUIContent("Wire Color"));

        if (GUILayout.Button("Reset Default Local Pose"))
        {
            var point = (HandAttachPoint)target;
            Undo.RecordObject(point.transform, "Reset Attach Point Pose");
            point.ApplyDefaultLocalPose();
            EditorUtility.SetDirty(point);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void OnSceneGUI()
    {
        var point = (HandAttachPoint)target;
        if (point == null)
            return;

        DrawLabel(point);

        var transform = point.transform;
        var rotation = transform.rotation;

        EditorGUI.BeginChangeCheck();

        Handles.color = GetLabelColor(point);
        var newPosition = Handles.PositionHandle(transform.position, rotation);
        var newRotation = Handles.RotationHandle(rotation, transform.position);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(transform, "Move Hand Attach Point");
            transform.SetPositionAndRotation(newPosition, newRotation);
            EditorUtility.SetDirty(transform);
            if (transform.gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(transform.gameObject.scene);
        }
    }

    static void DrawLabel(HandAttachPoint point)
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = GetLabelColor(point) }
        };

        Handles.Label(
            point.transform.position + Vector3.up * (point.WorldRadius + 0.008f),
            point.GetDisplayLabel(),
            style);
    }

    static Color GetLabelColor(HandAttachPoint point)
    {
        if (point.Kind == HandAttachPointKind.IndexNear)
            return point.Handedness == Handedness.Right ? RightIndexLabelColor : LeftIndexLabelColor;

        return point.Handedness == Handedness.Right ? RightLabelColor : LeftLabelColor;
    }
}

public static class HandAttachPointSetup
{
    [MenuItem("Tools/Hand Pose/Add Hand Attach Points")]
    public static void AddHandAttachPointsToScene()
    {
        var created = 0;
        created += EnsureAttachPoint(Handedness.Left, HandAttachPointKind.Wrist);
        created += EnsureAttachPoint(Handedness.Right, HandAttachPointKind.Wrist);
        created += EnsureAttachPoint(Handedness.Left, HandAttachPointKind.IndexNear);
        created += EnsureAttachPoint(Handedness.Right, HandAttachPointKind.IndexNear);

        if (created == 0)
        {
            EditorUtility.DisplayDialog(
                "Hand Attach Point",
                "손목·검지 니어 어태치 포인트가 이미 모두 있습니다.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "Hand Attach Point",
            $"어태치 포인트 {created}개를 생성했습니다.\n" +
            "손목(청록/주황) + 검지 니어(노랑/분홍) 기즈모를 Scene 뷰에서 조정하세요.",
            "OK");
    }

    [MenuItem("Tools/Hand Pose/Select Left Wrist Attach Point")]
    static void SelectLeftWrist() => SelectAttachPoint(Handedness.Left, HandAttachPointKind.Wrist);

    [MenuItem("Tools/Hand Pose/Select Right Wrist Attach Point")]
    static void SelectRightWrist() => SelectAttachPoint(Handedness.Right, HandAttachPointKind.Wrist);

    [MenuItem("Tools/Hand Pose/Select Left Index Near Attach Point")]
    static void SelectLeftIndexNear() => SelectAttachPoint(Handedness.Left, HandAttachPointKind.IndexNear);

    [MenuItem("Tools/Hand Pose/Select Right Index Near Attach Point")]
    static void SelectRightIndexNear() => SelectAttachPoint(Handedness.Right, HandAttachPointKind.IndexNear);

    static void SelectAttachPoint(Handedness handedness, HandAttachPointKind kind)
    {
        var point = HandAttachPoint.FindInScene(handedness, kind);
        if (point == null)
        {
            EnsureAttachPoint(handedness, kind);
            point = HandAttachPoint.FindInScene(handedness, kind);
        }

        if (point == null)
        {
            EditorUtility.DisplayDialog(
                "Hand Attach Point",
                "씬에서 해당 손 본을 찾지 못했습니다.",
                "OK");
            return;
        }

        Selection.activeGameObject = point.gameObject;
        SceneView.FrameLastActiveSceneView();
    }

    static int EnsureAttachPoint(Handedness handedness, HandAttachPointKind kind)
    {
        if (HandAttachPoint.FindInScene(handedness, kind) != null)
            return 0;

        var anchor = FindAnchorBone(handedness, kind);
        if (anchor == null)
            return 0;

        if (FindExistingAttachPoint(anchor, kind) != null)
            return 0;

        var attachObject = new GameObject(
            kind == HandAttachPointKind.IndexNear ? "AttachPoint_IndexNear" : "AttachPoint_Wrist");
        Undo.RegisterCreatedObjectUndo(attachObject, "Create Hand Attach Point");
        attachObject.transform.SetParent(anchor, false);

        var component = attachObject.AddComponent<HandAttachPoint>();
        component.SetHandedness(handedness);
        component.SetKind(kind);
        component.ApplyDefaultLocalPose();
        return 1;
    }

    static Transform FindAnchorBone(Handedness handedness, HandAttachPointKind kind)
    {
        if (kind == HandAttachPointKind.Wrist)
        {
            var wristName = handedness == Handedness.Right
                ? HandPoseApplier.RightWristBoneName
                : HandPoseApplier.LeftWristBoneName;
            return FindBoneTransform(wristName);
        }

        var metaName = HandAttachPoint.GetIndexProximalBoneName(handedness);
        var meta = FindBoneTransform(metaName);
        if (meta != null)
            return meta;

        var oculusName = handedness == Handedness.Right ? "b_r_index1" : "b_l_index1";
        return FindBoneTransform(oculusName);
    }

    static HandAttachPoint FindExistingAttachPoint(Transform anchor, HandAttachPointKind kind)
    {
        foreach (Transform child in anchor)
        {
            var point = child.GetComponent<HandAttachPoint>();
            if (point != null && point.Kind == kind)
                return point;
        }

        return null;
    }

    static Transform FindBoneTransform(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return null;

        var transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        Transform controllerMatch = null;
        foreach (var transform in transforms)
        {
            if (transform.name != boneName || !transform.gameObject.scene.IsValid())
                continue;

            if (IsUnderController(transform))
                return transform;

            controllerMatch ??= transform;
        }

        return controllerMatch;
    }

    static bool IsUnderController(Transform transform)
    {
        for (var parent = transform; parent != null; parent = parent.parent)
        {
            if (parent.name.Contains("Controller"))
                return true;
        }

        return false;
    }
}
