using UnityEngine;
using UnityEngine.Events;

public class GunPickup : PickupItem
{
    [SerializeField] private BaseGunController.GunType m_gunType;
    [SerializeField] private BaseGunController m_gunPrefab;

    [Header("Ammo on pickup")]
    [Tooltip("Amount of available ammo this pickup grants when collecting ammo for an already owned weapon.")]
    [SerializeField] private int m_ammoAmount = 10;

    // Event to play when you pickup a weapon (can be used for level sequencing for example).
    // Gun is instantiated in so other classes add to this event when spawning us in.
    public UnityEvent OnGunPickup;

    public override void OnPickup(PlayerPickup player)
    {
        if (m_gunPrefab == null)
        {
            Debug.LogError("Missing the prefab reference for the gun.");
            return;
        }

        // Spawn gun
        BaseGunController gunInstance = Instantiate(m_gunPrefab);
        gunInstance.Init();

        // Equip it
        player.EquipGun(gunInstance);

        OnGunPickup.Invoke();

        // TODO: Pool this, dont destroy.
        // Remove pickup object
        Destroy(gameObject);
    }

    public BaseGunController.GunType GetGunType()
    {
        return m_gunType;
    }

    public int GetAmmoAmount()
    {
        return m_ammoAmount;
    }
}