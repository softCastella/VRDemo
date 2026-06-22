using UnityEngine;
using UnityEngine.Events;

namespace FireSafetyVR
{
    /// <summary>
    /// 밸브 상태와 노즐 분사를 통합 제어합니다. UnityEvent / XR 이벤트에서 호출하기 쉽게 public API를 제공합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireHydrantSystem : MonoBehaviour
    {
        [SerializeField]
        bool m_ValveOpened;

        [SerializeField]
        FireHoseNozzle m_Nozzle;

        [SerializeField]
        UnityEvent m_OnValveOpened;

        [SerializeField]
        UnityEvent m_OnValveClosed;

        public bool ValveOpened => m_ValveOpened;

        void Awake()
        {
            if (m_Nozzle == null)
                m_Nozzle = FindFirstObjectByType<FireHoseNozzle>();

            ApplyValveStateToNozzle();
        }

        public void OpenValve()
        {
            m_ValveOpened = true;
            ApplyValveStateToNozzle();
            m_OnValveOpened?.Invoke();
        }

        public void CloseValve()
        {
            m_ValveOpened = false;
            ApplyValveStateToNozzle();
            m_OnValveClosed?.Invoke();
        }

        public void ToggleValve()
        {
            if (m_ValveOpened)
                CloseValve();
            else
                OpenValve();
        }

        public void SetTriggerPressed(bool pressed)
        {
            if (m_Nozzle == null)
            {
                Debug.LogWarning("[FireHydrantSystem] Nozzle 참조가 없습니다.", this);
                return;
            }

            if (!m_ValveOpened)
            {
                m_Nozzle.SetSpray(false);
                return;
            }

            m_Nozzle.SetSpray(pressed);
        }

        void ApplyValveStateToNozzle()
        {
            if (m_Nozzle == null)
                return;

            m_Nozzle.SetValveOpened(m_ValveOpened);
            if (!m_ValveOpened)
                m_Nozzle.SetSpray(false);
        }
    }
}
