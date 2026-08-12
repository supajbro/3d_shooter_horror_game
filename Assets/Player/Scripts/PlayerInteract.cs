using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public interface IInteractable
{
    void Interact(Transform interactor); // The player interacting
}

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float m_interactDistance = 3f;
    public LayerMask m_interactableLayer;
    public KeyCode m_interactKey = KeyCode.E;

    private Camera m_playerCamera;

    public void Init(PlayerCamera playerCamera)
    {
        m_playerCamera = playerCamera.GetCamera();
    }

    private void Update()
    {
//#if !ENABLE_INPUT_SYSTEM
        // TODO: Change this to use new input system
        if (Input.GetKeyDown(m_interactKey))
        {
            CheckForInteractable();
        }
//#endif
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(m_playerCamera.transform.position, m_playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, m_interactDistance, m_interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(transform);
            }
        }
    }

    // Public method so input system callbacks or other callers can attempt an interaction
    public void TryInteract()
    {
        if (m_playerCamera == null)
            return;

        CheckForInteractable();
    }

/*#if ENABLE_INPUT_SYSTEM
    // Optional callback for Unity Input System "Interact" action. Hook this in the PlayerInput component.
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryInteract();
    }
#endif*/
}