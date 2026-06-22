using System.Collections.Generic;
using UnityEngine;

namespace FireSafetyVR
{
    /// <summary>
    /// 2차 구현용: Rigidbody 세그먼트 체인으로 호스 물리를 구성합니다. 1차 프로토타입에서는 비활성화해도 됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoseBuilder : MonoBehaviour
    {
        [SerializeField]
        Transform m_StartAnchor;

        [SerializeField]
        GameObject m_SegmentPrefab;

        [SerializeField]
        Rigidbody m_NozzleRigidbody;

        [SerializeField]
        int m_SegmentCount = 25;

        [SerializeField]
        float m_SegmentLength = 0.15f;

        [SerializeField]
        bool m_BuildOnStart;

        readonly List<Transform> m_SegmentTransforms = new();
        readonly List<Rigidbody> m_SegmentBodies = new();

        public IReadOnlyList<Transform> SegmentTransforms => m_SegmentTransforms;

        void Start()
        {
            if (m_BuildOnStart)
                BuildChain();
        }

        public void BuildChain()
        {
            ClearChain();

            if (m_StartAnchor == null)
            {
                Debug.LogWarning("[HoseBuilder] startAnchor가 없습니다.", this);
                return;
            }

            var count = Mathf.Clamp(m_SegmentCount, 1, 40);
            Rigidbody previousBody = null;

            for (var i = 0; i < count; i++)
            {
                var segment = CreateSegment(i);
                var body = segment.GetComponent<Rigidbody>();
                m_SegmentTransforms.Add(segment.transform);
                m_SegmentBodies.Add(body);

                if (i == 0)
                {
                    var fixedJoint = segment.AddComponent<FixedJoint>();
                    fixedJoint.connectedBody = GetOrCreateAnchorBody(m_StartAnchor);
                    segment.transform.position = m_StartAnchor.position + m_StartAnchor.forward * (m_SegmentLength * 0.5f);
                }
                else
                {
                    var joint = segment.AddComponent<CharacterJoint>();
                    joint.connectedBody = previousBody;
                    joint.autoConfigureConnectedAnchor = true;
                    segment.transform.position = previousBody.position + m_StartAnchor.forward * m_SegmentLength;
                }

                previousBody = body;
            }

            if (m_NozzleRigidbody != null && previousBody != null)
            {
                var nozzleJoint = m_NozzleRigidbody.gameObject.GetComponent<CharacterJoint>();
                if (nozzleJoint == null)
                    nozzleJoint = m_NozzleRigidbody.gameObject.AddComponent<CharacterJoint>();

                nozzleJoint.connectedBody = previousBody;
                nozzleJoint.autoConfigureConnectedAnchor = true;
            }
        }

        public void ClearChain()
        {
            foreach (var segment in m_SegmentTransforms)
            {
                if (segment != null)
                    Destroy(segment.gameObject);
            }

            m_SegmentTransforms.Clear();
            m_SegmentBodies.Clear();
        }

        GameObject CreateSegment(int index)
        {
            GameObject segment;
            if (m_SegmentPrefab != null)
            {
                segment = Instantiate(m_SegmentPrefab, transform);
            }
            else
            {
                segment = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                segment.transform.SetParent(transform, false);
                segment.transform.localScale = new Vector3(0.08f, m_SegmentLength * 0.5f, 0.08f);
            }

            segment.name = $"HoseSegment_{index:00}";

            if (segment.GetComponent<Rigidbody>() == null)
            {
                var body = segment.AddComponent<Rigidbody>();
                body.mass = 0.2f;
                body.linearDamping = 0.1f;
                body.angularDamping = 0.2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (segment.GetComponent<Collider>() == null)
                segment.AddComponent<CapsuleCollider>();

            return segment;
        }

        static Rigidbody GetOrCreateAnchorBody(Transform anchor)
        {
            var body = anchor.GetComponent<Rigidbody>();
            if (body != null)
                return body;

            body = anchor.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            return body;
        }
    }
}
