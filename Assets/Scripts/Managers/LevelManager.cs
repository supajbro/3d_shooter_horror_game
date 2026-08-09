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

    [Header("Procedural Level")]
    [SerializeField] private ProceduralLevelGenerator m_levelGenerator;

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

        GameStateManager.Instance.SetLevelManager(this);

        // If a level generator isn't assigned in inspector, try to find one in scene
        if (m_levelGenerator == null)
            m_levelGenerator = FindObjectOfType<ProceduralLevelGenerator>();

        if (m_levelGenerator != null)
        {
            // Generate synchronously (keeps code simple). You can pass a seed if desired.
            m_levelGenerator.Generate();

            // Place spawn point at the generator's start position (if available)
            if (m_spawnPoint != null)
            {
                // preserve inspector Y if set
                Vector3 start = m_levelGenerator.StartPosition;
                start.y = m_spawnPoint.position.y;
                m_spawnPoint.position = start;
                m_spawnPoint.rotation = Quaternion.identity;
            }

            // Create an Exit trigger at the ExitPosition
            CreateExitAt(m_levelGenerator.ExitPosition);
        }

        // Now spawn player and init systems
        SpawnPlayer();

        m_enemySpawner = GetComponentInChildren<EnemySpawner>();
        if (m_enemySpawner != null)
            m_enemySpawner.Init(this);

        m_collisionStartsNextEnemyWave = FindObjectsByType<CollisionStartsNextEnemyWave>(FindObjectsSortMode.None);
        foreach (var enemyWave in m_collisionStartsNextEnemyWave)
        {
            enemyWave.Init(this);
        }

        m_weaponSpawner = gameObject.AddComponent<WeaponSpawner>();
        m_weaponSpawner.Init();

        // Spawn a few initial random weapon pickups distributed across room centers
        TrySpawnInitialPickups();

        m_weaponPads = FindObjectsByType<WeaponPad>(FindObjectsSortMode.None);
        foreach (var pad in m_weaponPads)
        {
            pad.Init(this);
        }

        m_ui = GameStateManager.Instance.GetUIStateHandler().m_gameplayUI;
        m_ui.Init(this);
    }

    private void CreateExitAt(Vector3 exitPosition)
    {
        if (m_levelGenerator == null)
            return;

        // create exit root
        GameObject exitGO = new GameObject("Exit");
        exitGO.transform.SetParent(m_levelGenerator.LevelRoot, false);
        exitGO.transform.position = exitPosition + Vector3.up * 0.5f;

        // trigger collider
        SphereCollider sc = exitGO.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.25f;

        // add exit behavior
        exitGO.AddComponent<ExitTrigger>();

        // visual marker (simple)
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "ExitMarker";
        marker.transform.SetParent(exitGO.transform, false);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localScale = new Vector3(1f, 0.15f, 1f);
        // remove marker collider (we're using the parent collider)
        Collider markerCol = marker.GetComponent<Collider>();
        if (markerCol != null)
            Destroy(markerCol);
    }

    private void TrySpawnInitialPickups()
    {
        if (m_levelGenerator == null || m_weaponSpawner == null)
            return;

        var centers = m_levelGenerator.RoomCenters;
        if (centers == null || centers.Count == 0)
            return;

        // spawn roughly one pickup per 4 rooms (min 1)
        int spawnCount = Mathf.Max(1, centers.Count / 4);
        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < spawnCount; i++)
        {
            int idx = Random.Range(0, centers.Count);
            int tries = 0;
            while (used.Contains(idx) && tries < 10)
            {
                idx = Random.Range(0, centers.Count);
                tries++;
            }
            used.Add(idx);
            Transform chosen = centers[idx];
            m_weaponSpawner.SpawnWeaponRandom(chosen, null);
        }
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

    public GameplayUI GetGameplayUI()
    {
        if (m_ui == null)
        {
            Debug.LogError("Missing Gameplay UI reference.");
            return null;
        }
        return m_ui;
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

    // Expose generator to other systems
    public ProceduralLevelGenerator GetLevelGenerator()
    {
        if (m_levelGenerator == null)
            m_levelGenerator = FindObjectOfType<ProceduralLevelGenerator>();

        if (m_levelGenerator == null)
            Debug.LogError("Missing ProceduralLevelGenerator reference.");
        return m_levelGenerator;
    }
}