using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Zipline : MonoBehaviour, IInteractable
{
    [Header("Zipline Points")]
    [SerializeField] private Transform m_startPoint;
    [SerializeField] private Transform m_endPoint;

    [Header("Settings")]
    [SerializeField] private float m_speed = 10f;
    [SerializeField] private bool m_allowJumpOff = true;

    private LineRenderer m_lineRenderer;

    // Player riding state
    private Transform m_rider;
    private float m_journey; // 0 = start, 1 = end
    private bool m_isRiding;

    public void Interact(Transform interactor)
    {
        StartRiding(interactor);
    }

    private void Awake()
    {
        m_lineRenderer = GetComponent<LineRenderer>();
        m_lineRenderer.positionCount = 2;
        UpdateLinePositions();
    }

    private void UpdateLinePositions()
    {
        if (m_startPoint && m_endPoint)
        {
            m_lineRenderer.SetPosition(0, m_startPoint.position);
            m_lineRenderer.SetPosition(1, m_endPoint.position);
        }
    }

    private void Update()
    {
        if (!m_isRiding || m_rider == null) return;

        // Move player along the zipline
        m_journey += Time.deltaTime * m_speed / Vector3.Distance(m_startPoint.position, m_endPoint.position);
        m_journey = Mathf.Clamp01(m_journey);
        m_rider.position = Vector3.Lerp(m_startPoint.position, m_endPoint.position, m_journey);

        // Allow jump off
        if (m_allowJumpOff && Input.GetButtonDown("Jump"))
        {
            StopRiding();
        }

        // Reached the end
        if (m_journey >= 1f)
        {
            StopRiding();
        }
    }

    public void StartRiding(Transform player)
    {
        m_rider = player;
        m_journey = 0f;
        m_isRiding = true;

        // Optional: disable player movement
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
    }

    private void StopRiding()
    {
        if (m_rider != null)
        {
            var cc = m_rider.GetComponent<CharacterController>();
            if (cc) cc.enabled = true;
        }

        m_rider = null;
        m_isRiding = false;
    }

    private void OnDrawGizmos()
    {
        if (m_startPoint && m_endPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(m_startPoint.position, m_endPoint.position);
        }
    }
}