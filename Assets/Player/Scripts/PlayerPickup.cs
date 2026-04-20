using StarterAssets;
using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("References")]
    private Transform m_holdPoint;
    private Camera m_camera;
    private FirstPersonController m_player;
    private LevelManager m_manager;

    [Header("Settings")]
    [SerializeField] private float m_pickupRange = 3f;
    [SerializeField] private LayerMask m_pickupLayer;

    [Header("Inventory")]
    private BaseGunController[] m_guns = new BaseGunController[2];
    private int m_activeIndex = 0;

    public System.Action<int> OnWeaponChanged;

    public void Init(LevelManager manager)
    {
        m_player = GetComponent<FirstPersonController>();
        m_camera = m_player.GetPlayerCamera().GetCamera();
        m_holdPoint = m_player.GetPlayerCamera().GetWeaponHoldPoint();
        m_manager = manager;
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
            TryPickup();

        if (Input.GetKeyDown(KeyCode.Q))
            SwitchWeapon();

        if (Input.GetKeyDown(KeyCode.G))
            DropCurrentWeapon();
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
        SetGunActive(newGun, false);

        // Auto switch to new weapon
        m_activeIndex = slot;
        UpdateActiveWeapon();
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

    private void SwitchWeapon()
    {
        if (m_guns[0] == null && m_guns[1] == null)
            return;

        m_activeIndex = (m_activeIndex + 1) % m_guns.Length;

        // Skip empty slot
        if (m_guns[m_activeIndex] == null)
        {
            m_activeIndex = (m_activeIndex + 1) % m_guns.Length;
        }

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
        gun.gameObject.SetActive(active);
    }

    private void AttachToHoldPoint(BaseGunController gun)
    {
        gun.transform.SetParent(m_holdPoint);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
        gun.transform.localScale = Vector3.one;
    }

    private void DropCurrentWeapon()
    {
        BaseGunController gun = m_guns[m_activeIndex];
        if (gun == null) return;

        DropGun(gun);
        m_guns[m_activeIndex] = null;

        // Switch to other weapon if available
        SwitchWeapon();
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

    private void OnDrawGizmos()
    {
        if (m_camera == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(m_camera.transform.position, m_camera.transform.forward * m_pickupRange);
    }
}