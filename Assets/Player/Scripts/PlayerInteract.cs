using UnityEngine;

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
        if (Input.GetKeyDown(m_interactKey))
        {
            CheckForInteractable();
        }
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
}