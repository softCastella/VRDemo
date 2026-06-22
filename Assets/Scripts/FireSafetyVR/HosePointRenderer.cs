using System.Collections.Generic;
using UnityEngine;

namespace FireSafetyVR
{
    /// <summary>
    /// 2차 구현용: 물리 세그먼트 Transform 리스트를 LineRenderer로 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public class HosePointRenderer : MonoBehaviour
    {
        [SerializeField]
        LineRenderer m_LineRenderer;

        [SerializeField]
        List<Transform> m_HosePoints = new();

        readonly Vector3[] m_Buffer = new Vector3[64];

        void Awake()
        {
            if (m_LineRenderer == null)
                m_LineRenderer = GetComponent<LineRenderer>();
        }

        void LateUpdate()
        {
            if (m_LineRenderer == null || m_HosePoints == null || m_HosePoints.Count < 2)
            {
                if (m_LineRenderer != null)
                    m_LineRenderer.positionCount = 0;
                return;
            }

            var count = 0;
            for (var i = 0; i < m_HosePoints.Count && count < m_Buffer.Length; i++)
            {
                var point = m_HosePoints[i];
                if (point == null)
                    continue;

                m_Buffer[count++] = point.position;
            }

            if (count < 2)
            {
                m_LineRenderer.positionCount = 0;
                return;
            }

            m_LineRenderer.positionCount = count;
            m_LineRenderer.SetPositions(m_Buffer);
        }

        public void SetHosePoints(IEnumerable<Transform> points)
        {
            m_HosePoints.Clear();
            if (points == null)
                return;

            m_HosePoints.AddRange(points);
        }
    }
}
