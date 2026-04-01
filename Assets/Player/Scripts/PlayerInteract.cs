using UnityEngine;

public interface IInteractable
{
    void Interact(Transform interactor); // The player interacting
}

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public LayerMask interactableLayer;
    public KeyCode interactKey = KeyCode.E;

    private Camera playerCamera;

    private void Awake()
    {
        playerCamera = Camera.main; // assuming main camera is the player camera
    }

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            CheckForInteractable();
        }
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(transform);
            }
        }
    }
}