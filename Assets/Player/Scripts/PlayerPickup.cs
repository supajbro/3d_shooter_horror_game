using StarterAssets;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

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
            m_switchWeaponAction    = m_playerInput.actions["SwitchWeapon"];
            m_interactAction        = m_playerInput.actions["Interact"];
            m_dropWeaponAction      = m_playerInput.actions["DropWeapon"];
        }

        if (m_switchWeaponAction != null && m_interactAction != null && m_dropWeaponAction != null)
        {
            m_switchWeaponAction.performed  += OnSwitchWeapon;
            m_interactAction.performed      += OnTryPickup;
            m_dropWeaponAction.performed    += OnDropWeapon;
        }
        else
        {
            Debug.LogError("Missing reference to input.");
        }
    }

    private void Update()
    {
        ChooseAnimation();
    }

    private void OnDisable()
    {
        m_switchWeaponAction.performed  -= OnSwitchWeapon;
        m_interactAction.performed      -= OnTryPickup;
        m_dropWeaponAction.performed    -= OnDropWeapon;
    }

    public void OnTryPickup(InputAction.CallbackContext context)
    {
        TryPickup();
    }

    private void TryPickup()
    {
        Ray ray = new Ray(m_camera.transform.position, m_camera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, m_pickupRange, m_pickupLayer))
        {
            PickupItem item = hit.collider.GetComponent<PickupItem>();
            if (item != null)
            {
                item.OnPickup(this);
            }
        }
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

        // Try to find empty slot
        int slot = GetEmptySlot();

        // If no empty slot, replace active weapon
        if (slot == -1)
        {
            slot = m_activeIndex;
            DropGun(m_guns[slot]);
        }

        m_guns[slot] = newGun;

        // Disable the newly picked up gun by default
        SetGunActive(newGun, false);

        // Count how many weapons we now have
        int weaponCount = m_guns.Count(g => g != null);

        // Auto-equip ONLY if this is the first weapon
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
        // We have no remaining weapons, make sure we know the user has no weapons..
        if (m_guns[0] == null && m_guns[1] == null)
        {
            var ui = m_manager.GetGameplayUI();
            ui.GetWeaponSlotOne().SetWeapon(null);
            ui.GetWeaponSlotTwo().SetWeapon(null);
            return;
        }

        int startIndex = m_activeIndex;

        // loop until we find a valid weapon or come back around
        do
        {
            m_activeIndex = (m_activeIndex + direction + m_guns.Length) % m_guns.Length;
        }
        while (m_guns[m_activeIndex] == null && m_activeIndex != startIndex);

        UpdateActiveWeapon();
        OnWeaponChanged?.Invoke(m_activeIndex);
    }

    private void UpdateActiveWeapon()
    {
        for (int i = 0; i < m_guns.Length; i++)
        {
            if (m_guns[i] == null) continue;

            bool isActive = (i == m_activeIndex);
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
        if(!m_manager || !m_manager.GetGameplayUI())
        {
            Debug.LogError("Missing reference to gameplay UI. Unable to make gun active.");
            return;
        }

        gun.gameObject.SetActive(active);

        if(active)
        {
            m_manager.GetGameplayUI().SetAmmoText(gun.GetCurrentAmmo() + "/" + gun.GetAvailableAmmo());
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
        if (gun == null) return;

        DropGun(gun);
        m_guns[m_activeIndex] = null;

        // Switch to other weapon if available
        SwitchWeapon(1);
    }

    private void DropGun(BaseGunController gun)
    {
        var pickup = Instantiate(m_player.GetLevelManager().GetGunPickup(gun.GetGunType()));
        pickup.transform.position = m_camera.transform.position + m_camera.transform.forward * 1.5f;

        // If the gun pickup obj has a rigidbody then use it here (helps make the drop look juicy)
        Rigidbody rb = gun.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(m_camera.transform.forward * 5f, ForceMode.Impulse);
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
        if(m_anim == null)
        {
            Debug.LogError("Woah! Missing your animations buddy.");
            return;
        }
        //var prefix = m_guns.Length == 0 ? GetAnimationPrefix(AnimationType.MELEE) : GetAnimationPrefix(AnimationType.PISTOL);
        
        if(GetGunCount() != 0)
        {
            Debug.Log("We have a gun, don't animate these as these are for melee only.");
            return;
        }

        if (!m_player.IsAttacking())
        {
            Vector3 localVelocity = transform.InverseTransformDirection(m_player.GetPlayerVelocity());
            Vector3 targetOffset = Vector3.zero;

            if (localVelocity.sqrMagnitude > 0.01f)
            {
                targetOffset = new Vector3(
                    localVelocity.x,
                    0f,
                    localVelocity.z
                ).normalized * m_moveOffset;

                m_anim.SetTrigger("Walk");
            }
            else
            {
                m_anim.SetTrigger("Idle");
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
        if (m_camera == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(m_camera.transform.position, m_camera.transform.forward * m_pickupRange);
    }
}