using StarterAssets;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class LevelManager : MonoBehaviour
{
    [Header("Game State Manager")]
    [SerializeField] private GameStateManager m_managerPrefab; // <- This is spawned in the level manager if non existing already
    private GameStateManager m_manager;

    [Header("Player")]
    [SerializeField] private GameObject m_playerPrefab;
    [SerializeField] private Transform m_spawnPoint;
    private GameObject m_currentPlayer;
    private FirstPersonController m_fpsPlayer;

    [Header("Enemy")]
    private EnemySpawner m_enemySpawner;
    private CollisionStartsNextEnemyWave[] m_collisionStartsNextEnemyWave; // <- array is inserted at runtime

    [Header("Weapons/Pickup")]
    [SerializeField] private GunPickup m_autoRiflePickup;
    [SerializeField] private GunPickup m_shotgunPickup;
    [SerializeField] private GunPickup m_pistolPickup;
    [SerializeField] private GunPickup m_rocketLauncherPickup;
    private WeaponSpawner m_weaponSpawner; // <- Objects in this scene can reference to spawn a weapon pickup.

    [Header("Weapons Pads")]
    private WeaponPad[] m_weaponPads;

    [Header("UI")]
    [SerializeField] private GameplayUI m_ui;

    private void Start()
    {
        m_manager = GameStateManager.Instance == null ? Instantiate(m_managerPrefab) : GameStateManager.Instance;
        m_manager.SetInitialState(new GameplayState(m_manager));

        SpawnPlayer();

        m_enemySpawner = GetComponentInChildren<EnemySpawner>();
        m_enemySpawner.Init(this);

        m_collisionStartsNextEnemyWave = FindObjectsByType<CollisionStartsNextEnemyWave>(FindObjectsSortMode.None);
        foreach (var enemyWave in m_collisionStartsNextEnemyWave)
        {
            enemyWave.Init(this);
        }

        m_weaponSpawner = gameObject.AddComponent<WeaponSpawner>();
        m_weaponSpawner.Init();

        m_weaponPads = FindObjectsByType<WeaponPad>(FindObjectsSortMode.None);
        foreach (var pad in m_weaponPads)
        {
            pad.Init(this);
        }

        m_ui = GameStateManager.Instance.GetUIStateHandler().m_gameplayUI;
        m_ui.Init(this);
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

    public FirstPersonController GetPlayer()
    {
        if(m_fpsPlayer == null)
        {
            Debug.LogError("Missing reference to FPS player.");
            return null;
        }
        return m_fpsPlayer;
    }

    public EnemySpawner GetEnemySpawner()
    {
        if(m_enemySpawner == null)
        {
            Debug.LogError("Missing enemy spawner reference.");
            return null;
        }
        return m_enemySpawner;
    }

    public WeaponSpawner GetWeaponSpawner()
    {
        if (m_weaponSpawner == null)
        {
            Debug.LogError("Missing weapon spawner reference.");
            return null;
        }
        return m_weaponSpawner;
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