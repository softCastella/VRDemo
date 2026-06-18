using UnityEngine;

/// <summary>
/// 잡을 수 있는 오브젝트에 붙여 종류/색상 메타데이터를 지정합니다.
/// </summary>
[DisallowMultipleComponent]
public class InteractableGrabInfo : MonoBehaviour
{
    [SerializeField]
    InteractableObjectType m_ObjectType = InteractableObjectType.Cube;

    [SerializeField]
    InteractableColor m_Color = InteractableColor.Red;

    public InteractableObjectType ObjectType => m_ObjectType;
    public InteractableColor Color => m_Color;

    void Awake()
    {
        if (gameObject.name.Contains("Cube_1"))
            m_Color = InteractableColor.Blue;
        else if (gameObject.name.Contains("Cube_0"))
            m_Color = InteractableColor.Red;
    }
}
