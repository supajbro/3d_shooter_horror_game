using UnityEngine;
using UnityEngine.Events;

public class WeaponSpawner : MonoBehaviour
{
    private LevelManager m_manager;

    public void Init()
    {
        m_manager = Object.FindFirstObjectByType<LevelManager>();
    }

    public void SpawnWeapon(BaseGunController.GunType gunType, Transform trans, UnityEvent onPickup)
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }
        var gun = Instantiate(m_manager.GetGunPickup(gunType), trans.position, Quaternion.identity);

        // null check as we don't always add a new listener when calling this
        if (onPickup != null)
        {
            gun.OnGunPickup.AddListener(onPickup.Invoke);
        }
    }

    public void SpawnWeaponRandom(Transform trans, UnityEvent onPickup)
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }

        var values = System.Enum.GetValues(typeof(BaseGunController.GunType));
        var randomIndex = Random.Range(0, values.Length);
        var randomGun = (BaseGunController.GunType)values.GetValue(randomIndex);
        var gun = Instantiate(m_manager.GetGunPickup(randomGun), trans.position, Quaternion.identity);

        // null check as we don't always add a new listener when calling this
        if (onPickup != null)
        {
            gun.OnGunPickup.AddListener(onPickup.Invoke);
        }
    }

    public void SpawnShotgunOrPistol(Transform trans, UnityEvent onPickup)
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing reference to the level manager.");
            return;
        }

        var values = System.Enum.GetValues(typeof(BaseGunController.GunType));
        var randomIndex = Random.Range(1, 2);
        var randomGun = (BaseGunController.GunType)values.GetValue(randomIndex);
        var gun = Instantiate(m_manager.GetGunPickup(randomGun), trans.position, Quaternion.identity);

        // null check as we don't always add a new listener when calling this
        if (onPickup != null)
        {
            gun.OnGunPickup.AddListener(onPickup.Invoke);
        }
    }
}
