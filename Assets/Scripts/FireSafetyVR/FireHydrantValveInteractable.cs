using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FireSafetyVR
{
    /// <summary>
    /// 밸브 오브젝트에 붙여 Select 시 밸브를 토글합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class FireHydrantValveInteractable : MonoBehaviour
    {
        [SerializeField]
        FireHydrantSystem m_System;

        XRSimpleInteractable m_Interactable;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
            if (m_System == null)
                m_System = FindFirstObjectByType<FireHydrantSystem>();
        }

        void OnEnable()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (m_System != null)
                m_System.ToggleValve();
        }
    }
}
