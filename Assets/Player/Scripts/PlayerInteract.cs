using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public interface IInteractable
{
    void Interact(Transform interactor);
}

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float m_interactDistance = 3f;
    public LayerMask m_interactableLayer;
    public KeyCode m_interactKey = KeyCode.E;

    [Header("Interaction Prompt")]
    private CanvasGroup m_interactPrompt;
    private TMPro.TMP_Text m_interactKeyText;
    private InputAction m_interactAction;
    public void SetInteractPrompt(CanvasGroup prompt, TextMeshProUGUI text) 
    { 
        m_interactPrompt = prompt; 
        m_interactKeyText = text;
        InitPickupPrompt();
    }

    [SerializeField] private float m_promptTweenDuration = 0.15f;
    [SerializeField] private float m_promptMoveDistance = 20f;

    private Camera m_playerCamera;

    private bool m_promptVisible;
    private Vector3 m_promptDefaultLocalPos;

    public void Init(PlayerCamera playerCamera, UnityEngine.InputSystem.PlayerInput playerInput)
    {
        m_playerCamera = playerCamera.GetCamera();
        m_interactAction = playerInput.actions["Interact"];
    }

    private void Update()
    {
        UpdateInteractPrompt();

        //#if !ENABLE_INPUT_SYSTEM
        // TODO: Change this to use new input system
        if (Input.GetKeyDown(m_interactKey))
        {
            TryInteract();
        }
        //#endif
    }

    private void UpdateInteractPrompt()
    {
        if (m_playerCamera == null || m_interactPrompt == null)
            return;

        Ray ray = new Ray(
            m_playerCamera.transform.position,
            m_playerCamera.transform.forward
        );

        bool hasInteractable = Physics.Raycast(
            ray,
            out RaycastHit hit,
            m_interactDistance,
            m_interactableLayer
        );

        if (hasInteractable)
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                ShowPickupPrompt();
                return;
            }
        }

        HidePickupPrompt();
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(
            m_playerCamera.transform.position,
            m_playerCamera.transform.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            m_interactDistance,
            m_interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                interactable.Interact(transform);
            }
        }
    }

    public void TryInteract()
    {
        if (m_playerCamera == null)
            return;

        CheckForInteractable();
    }

    private void InitPickupPrompt()
    {
        m_interactPrompt.alpha = 0f;
        m_interactPrompt.gameObject.SetActive(false);

        m_promptDefaultLocalPos = m_interactPrompt.transform.localPosition;
    }

    private void ShowPickupPrompt()
    {
        if (m_interactPrompt == null)
            return;

        if (m_promptVisible)
            return;

        m_promptVisible = true;

        UpdateInteractKeyText();

        m_interactPrompt.gameObject.SetActive(true);

        // Start slightly below its normal position
        m_interactPrompt.transform.localPosition =
            m_promptDefaultLocalPos -
            Vector3.up * m_promptMoveDistance;

        LeanTween.cancel(m_interactPrompt.gameObject);

        LeanTween.moveLocal(
            m_interactPrompt.gameObject,
            m_promptDefaultLocalPos,
            m_promptTweenDuration
        ).setEaseOutBack();

        LeanTween.alphaCanvas(
            m_interactPrompt,
            1f,
            m_promptTweenDuration
        ).setEaseOutQuad();
    }

    private void HidePickupPrompt()
    {
        if (m_interactPrompt == null || !m_promptVisible)
            return;

        m_promptVisible = false;

        LeanTween.cancel(m_interactPrompt.gameObject);

        LeanTween.moveLocal(
            m_interactPrompt.gameObject,
            m_promptDefaultLocalPos -
            Vector3.up * m_promptMoveDistance,
            m_promptTweenDuration
        )
        .setEaseInBack()
        .setOnComplete(() =>
        {
            if (!m_promptVisible)
                m_interactPrompt.gameObject.SetActive(false);
        });

        LeanTween.alphaCanvas(
            m_interactPrompt,
            0f,
            m_promptTweenDuration
        ).setEaseInQuad();
    }

    private void UpdateInteractKeyText()
    {
        if (m_interactKeyText == null || m_interactAction == null)
            return;

        string binding = m_interactAction.GetBindingDisplayString(0);

        m_interactKeyText.text = binding;
    }
}
