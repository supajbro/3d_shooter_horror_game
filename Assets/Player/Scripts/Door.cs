using System.Collections;
using UnityEngine;

// Door that can be interacted with via IInteractable
public class Door : MonoBehaviour, IInteractable
{
    [Header("Hinge Settings")]
    [Tooltip("If left empty the door's own transform will be used as hinge.")]
    [SerializeField] private Transform m_hinge;

    [Header("Animation")]
    [Tooltip("If assigned, the Animator will be used (triggers: Open/Close). Otherwise the script will rotate the hinge.")]
    [SerializeField] private Animator m_animator;
    [SerializeField] private string m_openTrigger = "Open";
    [SerializeField] private string m_closeTrigger = "Close";

    [Header("Rotation (fallback)")]
    [Tooltip("Local Y angle to rotate to when opened (added to initial).")]
    [SerializeField] private float m_openAngle = 90f;
    [SerializeField] private float m_openSpeed = 6f;

    private bool m_isOpen = false;
    private Quaternion m_closedRot;
    private Quaternion m_openRot;
    private Coroutine m_rotateCoroutine;

    private void Awake()
    {
        if (m_hinge == null)
            m_hinge = transform;

        m_closedRot = m_hinge.localRotation;
        m_openRot = m_closedRot * Quaternion.Euler(0f, m_openAngle, 0f);
    }

    public void Interact(Transform interactor)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (m_animator != null)
        {
            m_isOpen = !m_isOpen;
            m_animator.SetTrigger(m_isOpen ? m_openTrigger : m_closeTrigger);
            return;
        }

        // fallback: smooth rotate
        if (m_rotateCoroutine != null)
            StopCoroutine(m_rotateCoroutine);

        m_isOpen = !m_isOpen;
        m_rotateCoroutine = StartCoroutine(RotateRoutine(m_isOpen ? m_openRot : m_closedRot));
    }

    private IEnumerator RotateRoutine(Quaternion target)
    {
        while (Quaternion.Angle(m_hinge.localRotation, target) > 0.1f)
        {
            m_hinge.localRotation = Quaternion.Slerp(m_hinge.localRotation, target, Time.deltaTime * m_openSpeed);
            yield return null;
        }

        m_hinge.localRotation = target;
        m_rotateCoroutine = null;
    }

    // Optional helpers
    public void Open()
    {
        if (m_isOpen) return;
        Toggle();
    }

    public void Close()
    {
        if (!m_isOpen) return;
        Toggle();
    }
}
