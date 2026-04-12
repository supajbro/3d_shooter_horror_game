using StarterAssets;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject m_playerPrefab;
    [SerializeField] private Transform m_spawnPoint;
    private GameObject m_currentPlayer;
    private FirstPersonController m_fpsPlayer;

    [Header("Enemy")]
    private EnemySpawner m_enemySpawner;

    [Header("Weapons/Pickup")]
    [SerializeField] private GunPickup m_autoRiflePickup;
    [SerializeField] private GunPickup m_shotgunPickup;
    [SerializeField] private GunPickup m_pistolPickup;
    [SerializeField] private GunPickup m_rocketLauncherPickup;

    private void Start()
    {
        SpawnPlayer();

        m_enemySpawner = GetComponentInChildren<EnemySpawner>();
        m_enemySpawner.Init();
    }

    public void SpawnPlayer()
    {
        if (m_currentPlayer != null)
        {
            Destroy(m_currentPlayer);
        }

        m_currentPlayer = Instantiate(
            m_playerPrefab,
            m_spawnPoint.position,
            m_spawnPoint.rotation
        );
        m_fpsPlayer = m_currentPlayer.GetComponent<FirstPersonController>();

        if(m_fpsPlayer != null )
        {
            m_fpsPlayer.Init(this);
        }
        else
        {
            Debug.LogError("Unable to find players FPS Controller.");
        }
    }

    public EnemySpawner GetLevelSpawner()
    {
        if(m_enemySpawner == null)
        {
            Debug.LogError("Missing enemy spawner reference.");
            return null;
        }
        return m_enemySpawner;
    }

    public GunPickup GetGunPickup(BaseGunController.GunType gunType)
    {
        switch(gunType)
        {
            case BaseGunController.GunType.AUTORIFLE:
                return m_autoRiflePickup;
            case BaseGunController.GunType.SHOTGUN:
                return m_shotgunPickup;
            case BaseGunController.GunType.PISTOL:
                return m_pistolPickup;
            case BaseGunController.GunType.ROCKETLAUNCHER:
                return m_rocketLauncherPickup;
            default:
                Debug.LogError("Unable to find gun type.");
                return null;
        }
    }
}