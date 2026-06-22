using UnityEngine;

/// <summary>
/// 포즈가 굳혀진 정적 XRHand 스냅샷의 루트 마커입니다.
/// </summary>
[DisallowMultipleComponent]
public class HandPoseSnapshotRoot : MonoBehaviour
{
    [SerializeField]
    Vector3 m_BakeReferenceScale = Vector3.one;

    public Vector3 BakeReferenceScale => m_BakeReferenceScale;

    public void SnapToParentOrigin()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    /// <summary>타깃 부모의 월드 스케일을 기록합니다. 작은 스케일 타깃에 붙일 때 참고용입니다.</summary>
    public void SetBakeReferenceScale(Transform target)
    {
        if (target == null)
            return;

        m_BakeReferenceScale = target.lossyScale;
    }
}
