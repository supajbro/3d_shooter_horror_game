using UnityEngine;

public class GunPickup : PickupItem
{
    [SerializeField] private BaseGunController.GunType m_gunType;
    [SerializeField] private BaseGunController m_gunPrefab;

    public override void OnPickup(PlayerPickup player)
    {
        if(m_gunPrefab == null)
        {
            Debug.LogError("Missing the prefab reference for the gun.");
            return;
        }

        // Spawn gun
        BaseGunController gunInstance = Instantiate(m_gunPrefab);
        gunInstance.Init();

        // Equip it
        player.EquipGun(gunInstance);

        // Remove pickup object
        Destroy(gameObject);
    }
}