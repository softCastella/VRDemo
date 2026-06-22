using UnityEngine;

namespace FireSafetyVR
{
    /// <summary>
    /// 불 오브젝트 HP 관리 및 소화 완료 처리.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireTarget : MonoBehaviour
    {
        [SerializeField]
        float m_MaxHp = 100f;

        [SerializeField]
        ParticleSystem m_FireParticle;

        [SerializeField]
        GameObject m_FireVisualRoot;

        float m_CurrentHp;
        bool m_IsExtinguished;

        public float CurrentHp => m_CurrentHp;
        public float MaxHp => m_MaxHp;
        public bool IsExtinguished => m_IsExtinguished;

        void Awake()
        {
            if (m_FireParticle == null)
                m_FireParticle = GetComponentInChildren<ParticleSystem>();

            if (m_FireVisualRoot == null)
                m_FireVisualRoot = gameObject;
        }

        void Start()
        {
            ResetFire();
        }

        public void Extinguish(float amount)
        {
            if (m_IsExtinguished || amount <= 0f)
                return;

            m_CurrentHp = Mathf.Max(0f, m_CurrentHp - amount);
            if (m_CurrentHp <= 0f)
                OnExtinguished();
        }

        public void ResetFire()
        {
            m_IsExtinguished = false;
            m_CurrentHp = m_MaxHp;

            if (m_FireVisualRoot != null)
                m_FireVisualRoot.SetActive(true);

            if (m_FireParticle != null && !m_FireParticle.isPlaying)
                m_FireParticle.Play();
        }

        void OnExtinguished()
        {
            if (m_IsExtinguished)
                return;

            m_IsExtinguished = true;
            m_CurrentHp = 0f;

            if (m_FireParticle != null)
                m_FireParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (m_FireVisualRoot != null)
                m_FireVisualRoot.SetActive(false);

            Debug.Log($"[FireTarget] 소화 완료: {name}", this);
        }
    }
}
