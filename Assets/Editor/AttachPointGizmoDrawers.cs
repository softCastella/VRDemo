using UnityEditor;
using UnityEngine;

static class AttachPointGizmoDrawers
{
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
    static void DrawHandAttachPoint(HandAttachPoint point, GizmoType gizmoType)
    {
        if (point == null)
            return;

        var selected = (gizmoType & GizmoType.Selected) != 0;
        AttachPointGizmoDrawEditor.Draw(
            point.transform,
            point.Radius,
            point.FaceColor,
            point.WireColor,
            point.DrawFaceFill,
            selected);
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
    static void DrawObjectAttachPoint(AttachPointGizmo point, GizmoType gizmoType)
    {
        if (point == null)
            return;

        var selected = (gizmoType & GizmoType.Selected) != 0;
        AttachPointGizmoDrawEditor.Draw(
            point.transform,
            point.Radius,
            point.FaceColor,
            point.WireColor,
            point.DrawFaceFill,
            selected);
    }
}
