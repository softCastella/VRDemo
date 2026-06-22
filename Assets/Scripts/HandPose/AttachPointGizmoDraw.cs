using UnityEngine;

public static class AttachPointGizmoDraw
{
    public static float GetMaxLossyScale(Vector3 lossyScale) =>
        Mathf.Max(lossyScale.x, Mathf.Max(lossyScale.y, lossyScale.z));

    public static float GetWorldRadius(Transform transform, float localRadius) =>
        localRadius * GetMaxLossyScale(transform.lossyScale);

    public static Matrix4x4 GetLocalGizmoMatrix(Transform transform) =>
        Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

    public static void DrawSphereGizmo(
        Transform transform,
        float localRadius,
        Color faceColor,
        Color wireColor,
        bool drawFace,
        bool selected)
    {
        var previousMatrix = Gizmos.matrix;
        Gizmos.matrix = GetLocalGizmoMatrix(transform);

        // 면 채우기는 에디터 DrawGizmo + Internal-Colored 메시로 그립니다.
        // Gizmos.DrawSphere는 Scene 뷰에서 반투명 면이 보이지 않습니다.

        var wire = selected
            ? new Color(wireColor.r, wireColor.g, wireColor.b, 1f)
            : wireColor;
        Gizmos.color = wire;
        Gizmos.DrawWireSphere(Vector3.zero, localRadius);

        Gizmos.matrix = previousMatrix;
    }
}
