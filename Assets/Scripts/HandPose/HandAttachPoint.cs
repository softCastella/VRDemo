using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;

public enum HandAttachPointKind
{
    Wrist,
    IndexNear
}

/// <summary>
/// 손 어태치 포인트. 손목(Wrist) 또는 검지 뿌리 니어 쪽(IndexNear)에 붙입니다.
/// </summary>
[ExecuteAlways]
[SelectionBase]
[AddComponentMenu("XR/Hand Attach Point")]
public class HandAttachPoint : MonoBehaviour
{
    const string WristAttachPointName = "AttachPoint_Wrist";
    const string IndexNearAttachPointName = "AttachPoint_IndexNear";

    static readonly Vector3 DefaultIndexNearLocalOffset = new(0f, -0.012f, 0.018f);

    [SerializeField]
    Handedness m_Handedness = Handedness.Left;

    [SerializeField]
    HandAttachPointKind m_Kind = HandAttachPointKind.Wrist;

    [SerializeField]
    [Tooltip("로컬 반지름입니다. Transform Scale에 따라 Scene 기즈모 크기가 함께 변합니다.")]
    float m_Radius = 0.008f;

    [FormerlySerializedAs("m_Color")]
    [SerializeField]
    Color m_WireColor = new(0f, 0.85f, 1f, 0.95f);

    [SerializeField]
    bool m_DrawFaceFill = true;

    [SerializeField]
    Color m_FaceColor = new(0f, 0.85f, 1f, 0.65f);

    public Handedness Handedness => m_Handedness;
    public HandAttachPointKind Kind => m_Kind;
    public float Radius => m_Radius;
    public float WorldRadius => AttachPointGizmoDraw.GetWorldRadius(transform, m_Radius);
    public Color WireColor => m_WireColor;
    public bool DrawFaceFill => m_DrawFaceFill;
    public Color FaceColor => m_FaceColor;
    public Transform AnchorBone => transform.parent;

    public Vector3 WorldPosition => transform.position;
    public Quaternion WorldRotation => transform.rotation;

    void Reset()
    {
        m_Handedness = InferHandednessFromHierarchy();
        m_Kind = InferKindFromHierarchy();
        ApplyDefaultColor();
        ApplyDefaultLocalPose();
        gameObject.name = GetDefaultObjectName(m_Kind);
    }

    void OnValidate()
    {
        if (m_Radius < 0.001f)
            m_Radius = 0.001f;

        if (m_Kind == HandAttachPointKind.IndexNear && m_Radius > 0.01f)
            m_Radius = 0.006f;
    }

    public void SetHandedness(Handedness handedness)
    {
        m_Handedness = handedness;
        ApplyDefaultColor();
    }

    public void SetKind(HandAttachPointKind kind)
    {
        m_Kind = kind;
        ApplyDefaultColor();
        gameObject.name = GetDefaultObjectName(kind);
    }

    public void ApplyDefaultLocalPose()
    {
        if (m_Kind == HandAttachPointKind.IndexNear)
        {
            transform.localPosition = DefaultIndexNearLocalOffset;
            transform.localRotation = Quaternion.identity;
            if (m_Radius > 0.01f)
                m_Radius = 0.006f;
            return;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    void ApplyDefaultColor()
    {
        if (m_Kind == HandAttachPointKind.IndexNear)
        {
            if (m_Handedness == Handedness.Right)
            {
                m_WireColor = new Color(1f, 0.35f, 0.85f, 0.95f);
                m_FaceColor = new Color(1f, 0.35f, 0.85f, 0.65f);
            }
            else
            {
                m_WireColor = new Color(0.95f, 0.9f, 0.15f, 0.95f);
                m_FaceColor = new Color(0.95f, 0.9f, 0.15f, 0.65f);
            }

            return;
        }

        if (m_Handedness == Handedness.Right)
        {
            m_WireColor = new Color(1f, 0.55f, 0.1f, 0.95f);
            m_FaceColor = new Color(1f, 0.55f, 0.1f, 0.65f);
            return;
        }

        m_WireColor = new Color(0f, 0.85f, 1f, 0.95f);
        m_FaceColor = new Color(0f, 0.85f, 1f, 0.65f);
    }

    public string GetDisplayLabel()
    {
        var side = m_Handedness == Handedness.Right ? "Right" : "Left";
        return m_Kind == HandAttachPointKind.IndexNear
            ? $"{side} Index Near"
            : $"{side} Wrist";
    }

    static string GetDefaultObjectName(HandAttachPointKind kind) =>
        kind == HandAttachPointKind.IndexNear ? IndexNearAttachPointName : WristAttachPointName;

    Handedness InferHandednessFromHierarchy()
    {
        for (var t = transform; t != null; t = t.parent)
        {
            if (t.name.StartsWith("R_", System.StringComparison.Ordinal) ||
                t.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return Handedness.Right;

            if (t.name.StartsWith("L_", System.StringComparison.Ordinal) ||
                t.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return Handedness.Left;
        }

        return Handedness.Left;
    }

    HandAttachPointKind InferKindFromHierarchy()
    {
        var parent = transform.parent;
        if (parent == null)
            return HandAttachPointKind.Wrist;

        var parentName = parent.name;
        if (IsIndexProximalBone(parentName))
            return HandAttachPointKind.IndexNear;

        if (IsWristBone(parentName))
            return HandAttachPointKind.Wrist;

        return m_Kind;
    }

    public static bool IsWristBone(string boneName) =>
        boneName is HandPoseApplier.LeftWristBoneName or HandPoseApplier.RightWristBoneName;

    public static bool IsIndexProximalBone(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return false;

        return boneName is "L_IndexProximal" or "R_IndexProximal" or "b_l_index1" or "b_r_index1";
    }

    public static string GetIndexProximalBoneName(Handedness handedness) =>
        handedness == Handedness.Right ? "R_IndexProximal" : "L_IndexProximal";

    public static HandAttachPoint FindInScene(Handedness handedness, HandAttachPointKind kind)
    {
        var points = Object.FindObjectsByType<HandAttachPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var point in points)
        {
            if (point != null && point.m_Handedness == handedness && point.m_Kind == kind)
                return point;
        }

        return null;
    }

    public static HandAttachPoint FindWristInScene(Handedness handedness) =>
        FindInScene(handedness, HandAttachPointKind.Wrist);

    public static HandAttachPoint FindIndexNearInScene(Handedness handedness) =>
        FindInScene(handedness, HandAttachPointKind.IndexNear);

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif
        DrawSphere(false);
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif
        DrawSphere(true);
    }

    void DrawSphere(bool selected)
    {
        AttachPointGizmoDraw.DrawSphereGizmo(
            transform,
            m_Radius,
            m_FaceColor,
            m_WireColor,
            m_DrawFaceFill,
            selected);
    }
}
