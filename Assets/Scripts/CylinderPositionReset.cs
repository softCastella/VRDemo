using UnityEngine;

/// <summary>
/// Resets this object to the spawn position when it falls below Y = 0.
/// </summary>
[DisallowMultipleComponent]
public class CylinderPositionReset : MonoBehaviour
{
    static readonly Vector3 ResetPosition = new(-2f, 1f, 0.5f);

    Rigidbody m_Rigidbody;

    void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (transform.position.y < 0f)
            ResetToSpawnPosition();
    }

    void ResetToSpawnPosition()
    {
        transform.position = ResetPosition;

        if (m_Rigidbody == null)
            return;

        m_Rigidbody.linearVelocity = Vector3.zero;
        m_Rigidbody.angularVelocity = Vector3.zero;
    }
}
