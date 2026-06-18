using UnityEngine;
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
    float m_Radius = 0.04f;

    [SerializeField]
    Color m_Color = Color.red;

    [Tooltip("Play 시 LeftController가 올라가는 높이. XR Origin Camera Y Offset과 맞추세요.")]
    [SerializeField]
    float m_PlayControllerLiftY = DefaultPlayControllerLiftY;

    public float Radius => m_Radius;

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
        DrawSphere(false);
    }

    void OnDrawGizmosSelected()
    {
        DrawSphere(true);
    }

    void DrawSphere(bool selected)
    {
        Gizmos.color = selected ? new Color(m_Color.r, m_Color.g, m_Color.b, 1f) : m_Color;

        Gizmos.DrawWireSphere(transform.position, m_Radius);

        if (selected)
            Gizmos.DrawSphere(transform.position, m_Radius * 0.2f);
    }
}
