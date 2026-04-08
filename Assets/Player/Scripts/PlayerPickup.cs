using StarterAssets;
using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("References")]
    private Transform m_holdPoint; // <- child of camera
    private Camera m_camera;
    private FirstPersonController m_player;

    [Header("Settings")]
    [SerializeField] private float m_pickupRange = 3f;
    [SerializeField] private LayerMask m_pickupLayer;

    private BaseGunController m_currentGun;

    public void Init()
    {
        m_player = GetComponent<FirstPersonController>();
        m_camera = m_player.GetPlayerCamera().GetCamera();
        m_holdPoint = m_player.GetPlayerCamera().GetWeaponHoldPoint();
    }

    private void Update()
    {
        HandlePickupInput();
    }

    private void HandlePickupInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPickup();
        }
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
        if(m_holdPoint == null)
        {
            Debug.LogError("Missing reference to weapon hold point");
            return;
        }

        // Drop current gun
        if (m_currentGun != null)
        {
            Destroy(m_currentGun.gameObject);
        }

        // Attach new gun
        m_currentGun = newGun;

        newGun.transform.SetParent(m_holdPoint);
        newGun.transform.localPosition = Vector3.zero;
        newGun.transform.localRotation = Quaternion.identity;

        // Optional: reset scale
        newGun.transform.localScale = Vector3.one;
    }

    private void OnDrawGizmos()
    {
        if (m_camera == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(m_camera.transform.position, m_camera.transform.forward * m_pickupRange);
    }
}