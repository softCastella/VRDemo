using UnityEngine;

namespace FireSafetyVR
{
    /// <summary>
    /// 노즐 물 분사, Raycast 소화 판정을 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireHoseNozzle : MonoBehaviour
    {
        [SerializeField]
        Transform m_ShootPoint;

        [SerializeField]
        ParticleSystem m_WaterParticle;

        [SerializeField]
        float m_Range = 8f;

        [SerializeField]
        float m_ExtinguishPower = 10f;

        [SerializeField]
        LayerMask m_HitMask = ~0;

        [SerializeField]
        bool m_DrawDebugRay = true;

        [SerializeField]
        float m_EmissionRate = 120f;

        bool m_ValveOpened;
        bool m_IsSpraying;

        public bool IsSpraying => m_IsSpraying;

        void Awake()
        {
            if (m_ShootPoint == null)
                m_ShootPoint = transform;

            if (m_WaterParticle == null)
                m_WaterParticle = GetComponentInChildren<ParticleSystem>();
        }

        void Update()
        {
            if (!m_IsSpraying)
                return;

            Spray();
        }

        public void SetSpray(bool value)
        {
            if (value && !m_ValveOpened)
            {
                SetSprayInternal(false);
                return;
            }

            SetSprayInternal(value);
        }

        public void SetValveOpened(bool value)
        {
            m_ValveOpened = value;
            if (!m_ValveOpened)
                SetSprayInternal(false);
        }

        void SetSprayInternal(bool value)
        {
            m_IsSpraying = value;

            if (m_WaterParticle == null)
                return;

            var emission = m_WaterParticle.emission;
            emission.rateOverTime = m_IsSpraying ? m_EmissionRate : 0f;

            if (m_IsSpraying)
            {
                if (!m_WaterParticle.isPlaying)
                    m_WaterParticle.Play();
            }
            else if (m_WaterParticle.isPlaying)
            {
                m_WaterParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Spray()
        {
            var origin = m_ShootPoint != null ? m_ShootPoint.position : transform.position;
            var direction = m_ShootPoint != null ? m_ShootPoint.forward : transform.forward;

            if (m_DrawDebugRay)
                Debug.DrawRay(origin, direction * m_Range, Color.cyan, 0f, false);

            if (!Physics.Raycast(origin, direction, out var hit, m_Range, m_HitMask, QueryTriggerInteraction.Ignore))
                return;

            var fireTarget = hit.collider.GetComponentInParent<FireTarget>();
            if (fireTarget == null)
                return;

            fireTarget.Extinguish(m_ExtinguishPower * Time.deltaTime);
        }
    }
}
