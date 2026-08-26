using UnityEngine;

public class Door : MonoBehaviour, IInteractable
{
    [Header("Door Pivot")]
    [SerializeField] private Transform m_pivot;

    [Header("Rotation")]
    [SerializeField] private float m_openAngle = 90f;
    [SerializeField] private float m_openSpeed = 180f;

    private bool m_isOpen;
    private bool m_isRotating;

    private Quaternion m_closedRotation;
    private Quaternion m_openRotation;
    private Quaternion m_targetRotation;

    private void Awake()
    {
        if (m_pivot == null)
            m_pivot = transform;

        m_closedRotation = m_pivot.localRotation;
        m_openRotation = m_closedRotation * Quaternion.Euler(0f, m_openAngle, 0f);

        m_targetRotation = m_closedRotation;
    }

    private void Update()
    {
        if (!m_isRotating)
            return;

        m_pivot.localRotation = Quaternion.RotateTowards(
            m_pivot.localRotation,
            m_targetRotation,
            m_openSpeed * Time.deltaTime
        );

        float angle = Quaternion.Angle(
            m_pivot.localRotation,
            m_targetRotation
        );

        if (angle < 0.01f)
        {
            m_pivot.localRotation = m_targetRotation;
            m_isRotating = false;
        }
    }

    public void Interact(Transform interactor)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (m_isRotating)
            return;

        m_isOpen = !m_isOpen;

        m_targetRotation = m_isOpen
            ? m_openRotation
            : m_closedRotation;

        m_isRotating = true;
    }

    public void Open()
    {
        if (m_isOpen || m_isRotating)
            return;

        m_isOpen = true;
        m_targetRotation = m_openRotation;
        m_isRotating = true;
    }

    public void Close()
    {
        if (!m_isOpen || m_isRotating)
            return;

        m_isOpen = false;
        m_targetRotation = m_closedRotation;
        m_isRotating = true;
    }
}