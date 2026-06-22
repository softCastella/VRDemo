using UnityEngine;

namespace FireSafetyVR
{
    /// <summary>
    /// HoseStartPoint와 Nozzle 사이를 LineRenderer로 연결하고 처짐 곡선을 표현합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public class SimpleFireHose : MonoBehaviour
    {
        [SerializeField]
        Transform m_HoseStart;

        [SerializeField]
        Transform m_Nozzle;

        [SerializeField]
        LineRenderer m_LineRenderer;

        [SerializeField]
        int m_SegmentCount = 24;

        [SerializeField]
        float m_SagAmount = 0.3f;

        readonly Vector3[] m_Points = new Vector3[64];

        void Awake()
        {
            if (m_LineRenderer == null)
                m_LineRenderer = GetComponent<LineRenderer>();
        }

        void LateUpdate()
        {
            UpdateHoseLine();
        }

        public void SetNozzle(Transform nozzle)
        {
            m_Nozzle = nozzle;
        }

        public void SetHoseStart(Transform hoseStart)
        {
            m_HoseStart = hoseStart;
        }

        void UpdateHoseLine()
        {
            if (m_HoseStart == null || m_Nozzle == null || m_LineRenderer == null)
                return;

            var count = Mathf.Clamp(m_SegmentCount, 2, m_Points.Length);
            var start = m_HoseStart.position;
            var end = m_Nozzle.position;
            var sag = Mathf.Max(0f, m_SagAmount);

            for (var i = 0; i < count; i++)
            {
                var t = i / (count - 1f);
                var point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * sag;
                m_Points[i] = point;
            }

            m_LineRenderer.positionCount = count;
            m_LineRenderer.SetPositions(m_Points);
        }

        void OnDrawGizmosSelected()
        {
            if (m_HoseStart != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(m_HoseStart.position, 0.05f);
            }

            if (m_Nozzle != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(m_Nozzle.position, 0.05f);
            }
        }
    }
}
