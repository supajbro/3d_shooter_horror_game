using UnityEngine;

public class Hover : MonoBehaviour
{
    [SerializeField] private float m_height = 0.2f;
    [SerializeField] private float m_speed = 2f;

    private Vector3 m_startPos;

    private void Start()
    {
        m_startPos = transform.localPosition;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * m_speed) * m_height;
        transform.localPosition = m_startPos + new Vector3(0, y, 0);
    }
}