using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach Point 위치를 Scene 뷰에서 붉은 원으로 표시합니다.
/// Transform 이동 도구로 기즈모(구) 중심을 잡고 위치를 조정하세요.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[SelectionBase]
public class AttachPointGizmo : MonoBehaviour
{
    const float DefaultPlayControllerLiftY = 1.1176f;

    [SerializeField]
    [Tooltip("로컬 반지름입니다. Transform Scale에 따라 Scene 기즈모 크기가 함께 변합니다.")]
    float m_Radius = 0.012f;

    [FormerlySerializedAs("m_Color")]
    [SerializeField]
    Color m_WireColor = Color.red;

    [SerializeField]
    bool m_DrawFaceFill = true;

    [SerializeField]
    Color m_FaceColor = new(1f, 0f, 0f, 0.65f);

    [Tooltip("Play 시 LeftController가 올라가는 높이. XR Origin Camera Y Offset과 맞추세요.")]
    [SerializeField]
    float m_PlayControllerLiftY = DefaultPlayControllerLiftY;

    public float Radius => m_Radius;
    public float WorldRadius => AttachPointGizmoDraw.GetWorldRadius(transform, m_Radius);
    public bool DrawFaceFill => m_DrawFaceFill;
    public Color FaceColor => m_FaceColor;
    public Color WireColor => m_WireColor;

#if UNITY_EDITOR
    [ContextMenu("Align Cube To Left GrabAttach (Play Pose)")]
    void AlignCubeToLeftGrabAttach()
    {
        var grabAttach = GameObject.Find("GrabAttach");
        if (grabAttach == null)
        {
            Debug.LogWarning("GrabAttach not found in scene.", this);
            return;
        }

        var cubeRoot = transform.parent;
        if (cubeRoot == null)
        {
            Debug.LogWarning("AttachPoint needs a parent (cube root).", this);
            return;
        }

        var playGrabPos = grabAttach.transform.position + Vector3.up * m_PlayControllerLiftY;
        var attachOffsetWorld = transform.position - cubeRoot.position;
        cubeRoot.position = playGrabPos - attachOffsetWorld;

        EditorUtility.SetDirty(cubeRoot.gameObject);
        if (cubeRoot.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cubeRoot.gameObject.scene);

        Debug.Log($"Aligned {cubeRoot.name} so AttachPoint meets GrabAttach at play pose.", this);
    }
#endif

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
