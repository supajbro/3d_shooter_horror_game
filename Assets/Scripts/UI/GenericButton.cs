using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GenericButton : Button, IPointerEnterHandler, IPointerExitHandler
{
    public enum ButtonState
    {
        Inactive,
        Animating,
        Active
    }

    [Header("Animation")]
    [SerializeField] private float m_hoverScale = 1.1f;
    [SerializeField] private float m_animationSpeed = 10f;

    private Vector3 m_defaultScale;
    private Vector3 m_targetScale;

    private ButtonState m_state = ButtonState.Active;

    protected override void Awake()
    {
        base.Awake();

        m_defaultScale = transform.localScale;
        m_targetScale = m_defaultScale;
    }

    private void Update()
    {
        if (m_state == ButtonState.Inactive)
        {
            return;
        }

        if (Vector3.Distance(transform.localScale, m_targetScale) > 0.001f)
        {
            m_state = ButtonState.Animating;

            transform.localScale = Vector3.Lerp(
                transform.localScale,
                m_targetScale,
                Time.unscaledDeltaTime * m_animationSpeed);

            if (Vector3.Distance(transform.localScale, m_targetScale) < 0.01f)
            {
                transform.localScale = m_targetScale;
                m_state = ButtonState.Active;
            }
        }
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);

        if (m_state == ButtonState.Inactive)
        {
            return;
        }

        m_targetScale = m_defaultScale * m_hoverScale;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);

        if (m_state == ButtonState.Inactive)
        {
            return;
        }

        m_targetScale = m_defaultScale;
    }

    public void SetInactive(bool inactive)
    {
        m_state = inactive
            ? ButtonState.Inactive
            : ButtonState.Active;

        interactable = !inactive;

        if (inactive)
        {
            transform.localScale = m_defaultScale;
        }
    }

    public ButtonState GetState()
    {
        return m_state;
    }
}