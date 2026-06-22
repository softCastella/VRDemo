using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FireSafetyVR
{
    /// <summary>
    /// 노즐을 잡은 상태에서 XR Activate(트리거) 입력을 FireHydrantSystem에 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FireHoseNozzleTriggerBridge : MonoBehaviour
    {
        [SerializeField]
        FireHydrantSystem m_System;

        XRGrabInteractable m_GrabInteractable;

        void Awake()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            if (m_System == null)
                m_System = FindFirstObjectByType<FireHydrantSystem>();
        }

        void OnEnable()
        {
            if (m_GrabInteractable == null)
                return;

            m_GrabInteractable.activated.AddListener(OnActivated);
            m_GrabInteractable.deactivated.AddListener(OnDeactivated);
            m_GrabInteractable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            if (m_GrabInteractable == null)
                return;

            m_GrabInteractable.activated.RemoveListener(OnActivated);
            m_GrabInteractable.deactivated.RemoveListener(OnDeactivated);
            m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);

            if (m_System != null)
                m_System.SetTriggerPressed(false);
        }

        void OnActivated(ActivateEventArgs args)
        {
            if (m_System != null)
                m_System.SetTriggerPressed(true);
        }

        void OnDeactivated(DeactivateEventArgs args)
        {
            if (m_System != null)
                m_System.SetTriggerPressed(false);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (m_System != null)
                m_System.SetTriggerPressed(false);
        }
    }
}
