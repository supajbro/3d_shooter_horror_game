using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float m_rotationSpeed = 45f;

    [Header("Floating")]
    [SerializeField] private Transform m_child;
    [SerializeField] private float m_floatHeight = 0.25f;
    [SerializeField] private float m_floatSpeed = 2f;

    private Vector3 m_startLocalPosition;
    private float m_time;

    private void Start()
    {
        if (m_child != null)
            m_startLocalPosition = m_child.localPosition;
    }

    private void Update()
    {
        // Continuously rotate the parent
        transform.Rotate(Vector3.up, m_rotationSpeed * Time.deltaTime);

        // Move the child up and down using a sine wave
        if (m_child != null)
        {
            m_time += Time.deltaTime * m_floatSpeed;

            float offset = Mathf.Sin(m_time) * m_floatHeight;

            m_child.localPosition = m_startLocalPosition + Vector3.up * offset;
        }
    }
}