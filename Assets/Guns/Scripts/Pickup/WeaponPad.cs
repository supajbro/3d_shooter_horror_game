using System;
using UnityEngine;

public class WeaponPad : MonoBehaviour
{
    private LevelManager m_manager;

    [Header("Weapon choices")]
    [SerializeField] private BaseGunController.GunType m_weaponIndex = 0; // <- Set this to choose what weapon you want
    [SerializeField] private bool m_spawnWeaponRandom = false;

    [Header("Multiple weapon spawns")]
    [SerializeField] private bool m_spawnMultipleWeapons = false; // <- Will this weapon pad spawn multiple weapons over its lifetime?

    [Header("References")]
    [SerializeField] private Transform m_spawnPosition;

    public void Init(LevelManager manager)
    {
        m_manager = manager;

        if(m_spawnWeaponRandom)
        {
            SpawnWeaponRandom();
        }
        else
        {
            SpawnWeapon();
        }
    }

    private void SpawnWeapon()
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }
        m_manager.GetWeaponSpawner().SpawnWeapon(m_weaponIndex, m_spawnPosition);
    }

    private void SpawnWeaponRandom()
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }
        m_manager.GetWeaponSpawner().SpawnWeaponRandom(m_spawnPosition);
    }
}
