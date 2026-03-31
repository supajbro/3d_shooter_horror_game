using UnityEngine;

public class GunPickup : PickupItem
{
    [SerializeField] private BaseGunController m_gunPrefab;

    public override void OnPickup(PlayerPickup player)
    {
        // Spawn gun
        BaseGunController gunInstance = Instantiate(m_gunPrefab);
        gunInstance.Init();

        // Equip it
        player.EquipGun(gunInstance);

        // Remove pickup object
        Destroy(gameObject);
    }
}