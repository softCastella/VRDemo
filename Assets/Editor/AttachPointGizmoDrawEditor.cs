using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AttachPointGizmoDrawEditor
{
    static int s_SphereCapControlId = "AttachPointSphereCap".GetHashCode();

    public static void Draw(
        Transform transform,
        float localRadius,
        Color faceColor,
        Color wireColor,
        bool drawFace,
        bool selected)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint)
            return;

        if (drawFace)
            DrawSolidSphere(transform, localRadius, faceColor);

        DrawWireSphere(transform, localRadius, selected ? Opaque(wireColor) : wireColor);
    }

    static void DrawSolidSphere(Transform transform, float localRadius, Color faceColor)
    {
        var fillColor = faceColor;
        fillColor.a = Mathf.Clamp(fillColor.a, 0.55f, 1f);

        var matrix = AttachPointGizmoDraw.GetLocalGizmoMatrix(transform);
        var previousZTest = Handles.zTest;

        // 손 메쉬 depth에 가려지지 않도록 항상 위에 그림
        Handles.zTest = CompareFunction.Always;

        using (new Handles.DrawingScope(fillColor, matrix))
        {
            Handles.SphereHandleCap(
                s_SphereCapControlId,
                Vector3.zero,
                Quaternion.identity,
                localRadius * 2f,
                EventType.Repaint);

            Handles.DrawSolidDisc(Vector3.zero, Vector3.up, localRadius);
            Handles.DrawSolidDisc(Vector3.zero, Vector3.right, localRadius);
            Handles.DrawSolidDisc(Vector3.zero, Vector3.forward, localRadius);
        }

        var coreColor = fillColor;
        coreColor.a = Mathf.Min(1f, fillColor.a + 0.15f);
        using (new Handles.DrawingScope(coreColor, matrix * Matrix4x4.Scale(Vector3.one * 0.7f)))
        {
            Handles.SphereHandleCap(
                s_SphereCapControlId + 1,
                Vector3.zero,
                Quaternion.identity,
                localRadius * 2f,
                EventType.Repaint);
        }

        Handles.zTest = previousZTest;
    }

    static void DrawWireSphere(Transform transform, float localRadius, Color wireColor)
    {
        var matrix = AttachPointGizmoDraw.GetLocalGizmoMatrix(transform);
        var previousZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        using (new Handles.DrawingScope(wireColor, matrix))
        {
            Handles.DrawWireDisc(Vector3.zero, Vector3.up, localRadius);
            Handles.DrawWireDisc(Vector3.zero, Vector3.right, localRadius);
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, localRadius);
        }

        Handles.zTest = previousZTest;
    }

    static Color Opaque(Color color) => new(color.r, color.g, color.b, 1f);
}
