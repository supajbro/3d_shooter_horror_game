using StarterAssets;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// TODO: Rename this class. PlayerPickup only references other PickupItem classes, it doesn't actually do the _Pickup_
public class PlayerPickup : MonoBehaviour
{
    [Header("References")]
    private Transform m_holdPoint;
    private Camera m_camera;
    private FirstPersonController m_player;
    private Animator m_anim;
    private LevelManager m_manager;

    [Header("Settings")]
    [SerializeField] private float m_pickupRange = 3f;
    [SerializeField] private LayerMask m_pickupLayer;

    [Header("Pickup Prompt")]
    [SerializeField] private float m_promptMoveDistance = 30f;
    [SerializeField] private float m_promptTweenDuration = 0.25f;
    private RectTransform m_interactPromptRect;
    private Vector2 m_promptDefaultPosition;
    private CanvasGroup m_interactPrompt;
    private TMP_Text m_interactKeyText;
    public void SetInteractPrompt(CanvasGroup prompt, TextMeshProUGUI text, PlayerInteract interact)
    {
        m_interactPrompt = prompt; 
        m_interactKeyText = text;

        // responsible for setting up player interact too because i dumb and made it its own class.
        interact.SetInteractPrompt(prompt, text);

        UpdatePickupKeyText();
    }

    private Vector3 m_promptDefaultLocalPos;
    private bool m_promptVisible;

    [Header("Inventory")]
    private BaseGunController[] m_guns = new BaseGunController[2];
    private int m_activeIndex = 0;

    [Header("Input System")]
    private UnityEngine.InputSystem.PlayerInput m_playerInput;
    private InputAction m_switchWeaponAction;
    private InputAction m_dropWeaponAction;
    private InputAction m_interactAction;

    [SerializeField] private Transform m_model;
    [SerializeField] private float m_moveOffset = 0.05f;
    [SerializeField] private float m_moveLerpSpeed = 10f;
    private Vector3 m_defaultLocalPos;

    [Header("Grace Period")]
    [SerializeField] private float m_pickupGracePeriod = 1f;
    private PickupItem m_currentPickup;
    private float m_lastPickupSeenTime;

    public System.Action<int> OnWeaponChanged;

    public void Init(LevelManager manager)
    {
        m_player = GetComponent<FirstPersonController>();
        m_camera = m_player.GetPlayerCamera().GetCamera();
        m_anim = m_player.GetPlayerCamera().GetPlayerAnimator();
        m_model = m_anim.gameObject.transform;
        m_defaultLocalPos = m_model.localPosition;
        m_holdPoint = m_player.GetPlayerCamera().GetWeaponHoldPoint();
        m_manager = manager;

        m_playerInput = m_player.GetPlayerInput();

        if (m_playerInput != null)
        {
            m_switchWeaponAction = m_playerInput.actions["SwitchWeapon"];
            m_interactAction = m_playerInput.actions["Interact"];
            m_dropWeaponAction = m_playerInput.actions["DropWeapon"];
        }

        if (m_switchWeaponAction != null &&
            m_interactAction != null &&
            m_dropWeaponAction != null)
        {
            m_switchWeaponAction.performed += OnSwitchWeapon;
            m_interactAction.performed += OnTryPickup;
            m_dropWeaponAction.performed += OnDropWeapon;
        }
        else
        {
            Debug.LogError("Missing reference to input.");
        }
    }

    private void Update()
    {
        ChooseAnimation();
        CheckForPickup();
    }

    private void OnDisable()
    {
        if (m_switchWeaponAction != null)
            m_switchWeaponAction.performed -= OnSwitchWeapon;

        if (m_interactAction != null)
            m_interactAction.performed -= OnTryPickup;

        if (m_dropWeaponAction != null)
            m_dropWeaponAction.performed -= OnDropWeapon;
    }

    public void OnTryPickup(InputAction.CallbackContext context)
    {
        TryPickup();
    }

    private void TryPickup()
    {
        Ray ray = new Ray(m_camera.transform.position, m_camera.transform.forward);

        // First, try the normal raycast.
        if (Physics.Raycast(ray, out RaycastHit hit, m_pickupRange, m_pickupLayer))
        {
            PickupItem item = hit.collider.GetComponent<PickupItem>();

            if (item != null)
            {
                item.OnPickup(this);

                m_currentPickup = null;
                m_lastPickupSeenTime = 0f;

                HidePickupPrompt();
                return;
            }
        }

        // Raycast failed, so try the cached pickup.
        if (m_currentPickup != null)
        {
            // Make sure the cached pickup is still within its grace period.
            if (Time.time - m_lastPickupSeenTime <= m_pickupGracePeriod)
            {
                m_currentPickup.OnPickup(this);

                m_currentPickup = null;
                m_lastPickupSeenTime = 0f;

                HidePickupPrompt();
            }
        }
    }

    private void CheckForPickup()
    {
        if (m_camera == null)
            return;

        Ray ray = new Ray(m_camera.transform.position, m_camera.transform.forward);

        PickupItem foundPickup = null;

        if (Physics.Raycast(ray, out RaycastHit hit, m_pickupRange, m_pickupLayer))
        {
            foundPickup = hit.collider.GetComponent<PickupItem>();
        }

        // Found a pickup with the raycast.
        if (foundPickup != null)
        {
            // Immediately switch to the new pickup.
            m_currentPickup = foundPickup;

            // Reset the grace period.
            m_lastPickupSeenTime = Time.time;

            ShowPickupPrompt();
            return;
        }

        // No pickup found, but keep the cached pickup
        // alive for the grace period.
        if (m_currentPickup != null)
        {
            if (Time.time - m_lastPickupSeenTime <= m_pickupGracePeriod)
            {
                ShowPickupPrompt();
                return;
            }

            // Grace period expired.
            m_currentPickup = null;
        }

        HidePickupPrompt();
    }

    public void InitPickupPrompt()
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

        UpdatePickupKeyText();

        m_interactPrompt.gameObject.SetActive(true);

        // Start slightly below its normal position
        m_interactPrompt.transform.localPosition = m_promptDefaultLocalPos - Vector3.up * m_promptMoveDistance;

        LeanTween.cancel(m_interactPrompt.gameObject);

        LeanTween.moveLocal(
            m_interactPrompt.gameObject,
            m_promptDefaultLocalPos,
            m_promptTweenDuration
        ).setEaseOutBack();

        LeanTween.alphaCanvas(m_interactPrompt, 1f, m_promptTweenDuration).setEaseOutQuad();
    }

    private void HidePickupPrompt()
    {
        if (m_interactPrompt == null || !m_promptVisible)
            return;

        m_promptVisible = false;

        LeanTween.cancel(m_interactPrompt.gameObject);

        LeanTween.moveLocal(
            m_interactPrompt.gameObject,
            m_promptDefaultLocalPos - Vector3.up * m_promptMoveDistance,
            m_promptTweenDuration
        ).setEaseInBack()
        .setOnComplete(() =>
        {
            if (!m_promptVisible)
                m_interactPrompt.gameObject.SetActive(false);
        });

        LeanTween.alphaCanvas(m_interactPrompt, 0f, m_promptTweenDuration).setEaseInQuad();
    }

    private void UpdatePickupKeyText()
    {
        if (m_interactKeyText == null || m_interactAction == null)
            return;

        string binding = m_interactAction.GetBindingDisplayString(0);

        m_interactKeyText.text = binding;
    }

    /// <summary>
    /// Adds this gun to our inventory
    /// </summary>
    /// <param name="newGun">Gun that has been picked up.</param>
    public void EquipGun(BaseGunController newGun)
    {
        if (m_holdPoint == null)
        {
            Debug.LogError("Missing reference to weapon hold point");
            return;
        }

        int slot = GetEmptySlot();

        if (slot == -1)
        {
            slot = m_activeIndex;
            DropGun(m_guns[slot]);
        }

        m_guns[slot] = newGun;

        SetGunActive(newGun, false);

        int weaponCount = m_guns.Count(g => g != null);

        if (weaponCount == 1)
        {
            m_activeIndex = slot;
            UpdateActiveWeapon();
        }

        OnWeaponChanged?.Invoke(m_activeIndex);
    }

    private int GetEmptySlot()
    {
        for (int i = 0; i < m_guns.Length; i++)
        {
            if (m_guns[i] == null)
                return i;
        }

        return -1;
    }

    private void OnSwitchWeapon(InputAction.CallbackContext context)
    {
        float scroll = context.ReadValue<float>();

        if (scroll > 0)
            SwitchWeapon(1);
        else if (scroll < 0)
            SwitchWeapon(-1);
    }

    private void SwitchWeapon(int direction)
    {
        if (m_guns[0] == null && m_guns[1] == null)
        {
            var ui = m_manager.GetGameplayUI();
            ui.GetWeaponSlotOne().SetWeapon(null);
            ui.GetWeaponSlotTwo().SetWeapon(null);
            return;
        }

        int startIndex = m_activeIndex;

        do
        {
            m_activeIndex =
                (m_activeIndex + direction + m_guns.Length) % m_guns.Length;
        }
        while (m_guns[m_activeIndex] == null && m_activeIndex != startIndex);

        UpdateActiveWeapon();
        OnWeaponChanged?.Invoke(m_activeIndex);
    }

    private void UpdateActiveWeapon()
    {
        for (int i = 0; i < m_guns.Length; i++)
        {
            if (m_guns[i] == null)
                continue;

            bool isActive = i == m_activeIndex;

            SetGunActive(m_guns[i], isActive);

            if (isActive)
            {
                AttachToHoldPoint(m_guns[i]);
            }
            else
            {
                m_guns[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetGunActive(BaseGunController gun, bool active)
    {
        if (!m_manager || !m_manager.GetGameplayUI())
        {
            Debug.LogError("Missing reference to gameplay UI. Unable to make gun active.");
            return;
        }

        gun.gameObject.SetActive(active);

        if (active)
        {
            m_manager.GetGameplayUI()
                .SetAmmoText(gun.GetCurrentAmmo() + "/" + gun.GetAvailableAmmo());
        }
    }

    private void AttachToHoldPoint(BaseGunController gun)
    {
        gun.transform.SetParent(m_holdPoint);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
        gun.transform.localScale = Vector3.one;
    }

    private void OnDropWeapon(InputAction.CallbackContext context)
    {
        DropCurrentWeapon();
    }

    private void DropCurrentWeapon()
    {
        BaseGunController gun = m_guns[m_activeIndex];

        if (gun == null)
            return;

        DropGun(gun);
        m_guns[m_activeIndex] = null;

        SwitchWeapon(1);
    }

    private void DropGun(BaseGunController gun)
    {
        var pickup = Instantiate(
            m_player.GetLevelManager().GetGunPickup(gun.GetGunType())
        );

        pickup.transform.position =
            m_camera.transform.position +
            m_camera.transform.forward * 1.5f;

        Rigidbody rb = gun.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(
                m_camera.transform.forward * 5f,
                ForceMode.Impulse
            );
        }

        Destroy(gun.gameObject);
    }

    public BaseGunController[] GetGuns()
    {
        return m_guns;
    }

    public int GetGunCount()
    {
        int count = 0;

        for (int i = 0; i < m_guns.Length; i++)
        {
            if (m_guns[i] != null)
                count++;
        }

        return count;
    }

    private void ChooseAnimation()
    {
        if (m_anim == null)
        {
            Debug.LogError("Woah! Missing your animations buddy.");
            return;
        }

        if (GetGunCount() != 0)
        {
            if (m_model.gameObject.activeInHierarchy)
            {
                m_model.gameObject.SetActive(false);
            }

            return;
        }
        else if (!m_model.gameObject.activeInHierarchy)
        {
            m_model.gameObject.SetActive(true);
        }

        if (!m_player.IsAttacking() &&
            !m_player.IsDashing() &&
            !m_player.IsSliding())
        {
            Vector3 localVelocity =
                transform.InverseTransformDirection(
                    m_player.GetPlayerVelocity()
                );

            Vector3 targetOffset = Vector3.zero;

            if (localVelocity.sqrMagnitude > 0.01f)
            {
                targetOffset =
                    new Vector3(
                        localVelocity.x,
                        0f,
                        localVelocity.z
                    ).normalized * m_moveOffset;

                m_anim.SetBool("Walk_Bool", true);
                m_anim.SetBool("Idle_Bool", false);
            }
            else
            {
                m_anim.SetBool("Idle_Bool", true);
                m_anim.SetBool("Walk_Bool", false);
            }

            m_model.localPosition = Vector3.Lerp(
                m_model.localPosition,
                m_defaultLocalPos + targetOffset,
                Time.deltaTime * m_moveLerpSpeed
            );
        }
    }

    private void OnDrawGizmos()
    {
        if (m_camera == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(
            m_camera.transform.position,
            m_camera.transform.forward * m_pickupRange
        );
    }
}
