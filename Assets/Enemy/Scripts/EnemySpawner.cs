using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    private int m_activeEnemyCount = 0;

    public enum WaveStartMode
    {
        AutoAfterClear,
        Manual
    }

    private enum WaveState
    {
        Idle,
        Spawning,
        WaitingForClear,
        WaitingForNextWave,
        Completed
    }

    [Header("Wave Settings")]
    [SerializeField] private List<Wave> m_waves;
    [SerializeField] private WaveStartMode m_waveStartMode = WaveStartMode.AutoAfterClear;
    [SerializeField] private float m_timeBetweenWaves = 3f;
    [SerializeField] private bool m_spawnOnLoad = false;

    [Header("Platform Spawn")]
    [Tooltip("Chance (0-1) that a given platform/room will host a single enemy at level start.")]
    [Range(0f, 1f)]
    [SerializeField] private float m_platformSpawnChance = 0.25f;

    [Header("Procedural Spawner")]
    [SerializeField] private bool m_useProceduralSpawner = true;
    public enum Difficulty { Easy, Normal, Hard }
    [SerializeField] private Difficulty m_difficulty = Difficulty.Normal;
    [Tooltip("Minimum distance from player that a tile must be to allow spawning there (prevents visible pop-in).")]
    [SerializeField] private float m_minDistanceFromPlayer = 8f;
    [Tooltip("Delay between procedural respawns when threshold is reached.")]
    [SerializeField] private float m_procRespawnDelay = 0.5f;

    private float m_procCoverage = 0.15f;
    private float m_procRespawnThreshold = 0.75f;

    private int m_initialTargetCount = 0; // desired active enemies for procedural mode

    private int m_currentWaveIndex = 0;
    private bool m_waitingForNextWave = false;
    private bool m_running = false;

    private WaveState m_state = WaveState.Idle;

    // Spawning state data
    private int m_groupIndex;
    private int m_enemyIndexInGroup;
    private float m_spawnTimer;

    // Delay timer
    private float m_stateTimer;

    private LevelManager m_manager;

    // occupancy tracking: ensure only one enemy per spawn center
    private HashSet<Transform> m_occupiedSpawnCenters = new HashSet<Transform>();
    private Dictionary<GameObject, Transform> m_enemyToSpawnCenter = new Dictionary<GameObject, Transform>();

    // Procedural data
    private List<Transform> m_proceduralAvailableCenters = new List<Transform>();
    private List<string> m_proceduralPoolKeys = new List<string>();
    private float m_procRespawnTimer = 0f;

    public UnityEvent OnAllWavesCompleted;

    public void Init(LevelManager manager)
    {
        m_manager = manager;

        try
        {
            var gen = m_manager?.GetLevelGenerator();
            if (gen != null && gen.RoomCenters != null && gen.RoomCenters.Count > 0 && m_waves != null)
            {
                // Build list of valid centers excluding start and exit room centers
                List<Transform> validCenters = new List<Transform>();
                foreach (var t in gen.RoomCenters)
                {
                    if (t == null) continue;
                    // exclude start and exit (use small tolerance)
                    if (Vector3.Distance(t.position, gen.StartPosition) < 0.1f) continue;
                    if (Vector3.Distance(t.position, gen.ExitPosition) < 0.1f) continue;
                    validCenters.Add(t);
                }

                if (validCenters.Count == 0)
                {
                    // fallback to all centers if filtering removed them all
                    Debug.LogWarning("EnemySpawner.Init: no valid room centers after excluding start/exit — using all centers as fallback.");
                    validCenters = new List<Transform>(gen.RoomCenters);
                }

                // Assign all valid centers to waves that have no spawnPoints configured
                foreach (var wave in m_waves)
                {
                    if (wave.spawnPoints == null || wave.spawnPoints.Count == 0)
                    {
                        // copy the list to avoid referencing the generator's list directly
                        wave.spawnPoints = new List<Transform>(validCenters);
                    }

                    // Auto-fill enemy counts proportional to available centers for groups with non-positive counts
                    if (validCenters.Count > 0 && wave.enemies != null && wave.enemies.Count > 0)
                    {
                        int totalRooms = validCenters.Count;
                        int groups = wave.enemies.Count;
                        int baseCount = Mathf.Max(1, totalRooms / groups);
                        int remainder = totalRooms % groups;

                        for (int i = 0; i < wave.enemies.Count; i++)
                        {
                            var group = wave.enemies[i];
                            if (group.count <= 0)
                            {
                                int assign = baseCount + (remainder > 0 ? 1 : 0);
                                if (remainder > 0) remainder--;
                                group.count = assign;
                            }
                        }
                    }
                }

                // Collect available pool keys to use for initial procedural spawns
                m_proceduralPoolKeys.Clear();
                foreach (var wave in m_waves)
                {
                    if (wave.enemies == null) continue;
                    foreach (var g in wave.enemies)
                    {
                        if (!string.IsNullOrEmpty(g.poolKey) && !m_proceduralPoolKeys.Contains(g.poolKey))
                            m_proceduralPoolKeys.Add(g.poolKey);
                    }
                }

                // If procedural mode is enabled, compute coverage and initial targets and spawn
                if (m_useProceduralSpawner && m_proceduralPoolKeys.Count > 0)
                {
                    // set difficulty parameters
                    switch (m_difficulty)
                    {
                        case Difficulty.Easy:
                            m_procCoverage = 0.15f;
                            m_procRespawnThreshold = 0.5f; // spawn more when only 50% are alive
                            break;
                        case Difficulty.Normal:
                            m_procCoverage = 0.25f;
                            m_procRespawnThreshold = 0.75f;
                            break;
                        case Difficulty.Hard:
                            m_procCoverage = 0.50f;
                            m_procRespawnThreshold = 0.75f;
                            break;
                    }

                    // filter centers to exclude those near player
                    Vector3 playerPos = Vector3.zero;
                    var player = m_manager?.GetPlayer();
                    if (player != null)
                        playerPos = player.transform.position;

                    m_proceduralAvailableCenters = validCenters.Where(t => t != null && Vector3.Distance(t.position, playerPos) >= m_minDistanceFromPlayer).ToList();

                    // if filtering removed too many centres, fallback to unfiltered list (still exclude start/exit already handled)
                    if (m_proceduralAvailableCenters.Count == 0)
                        m_proceduralAvailableCenters = new List<Transform>(validCenters);

                    int total = m_proceduralAvailableCenters.Count;
                    m_initialTargetCount = Mathf.Max(1, Mathf.RoundToInt(total * m_procCoverage));

                    // Shuffle centers and pick initialTargetCount distinct centers
                    var rnd = new System.Random();
                    var indices = Enumerable.Range(0, total).OrderBy(x => rnd.Next()).Take(m_initialTargetCount).ToList();

                    foreach (var idx in indices)
                    {
                        var center = m_proceduralAvailableCenters[idx];
                        // ensure we don't spawn in occupied
                        if (m_occupiedSpawnCenters.Contains(center))
                            continue;

                        // pick random pool key
                        string key = m_proceduralPoolKeys[UnityEngine.Random.Range(0, m_proceduralPoolKeys.Count)];

                        bool spawned = SpawnEnemy(key, new List<Transform> { center });
                        if (spawned)
                        {
                            m_activeEnemyCount++;
                        }
                    }
                }

                // If procedural is not used, keep the previous platform spawn behavior
                else
                {
                    // Initial randomized spawns on platforms (approx m_platformSpawnChance per center)
                    // Collect available pool keys to use for initial spawns
                    List<string> poolKeys = new List<string>();
                    foreach (var wave in m_waves)
                    {
                        if (wave.enemies == null) continue;
                        foreach (var g in wave.enemies)
                        {
                            if (!string.IsNullOrEmpty(g.poolKey) && !poolKeys.Contains(g.poolKey))
                                poolKeys.Add(g.poolKey);
                        }
                    }

                    // If no pool keys found, skip initial spawning
                    if (poolKeys.Count > 0)
                    {
                        foreach (var center in validCenters)
                        {
                            if (center == null) continue;
                            if (UnityEngine.Random.value > m_platformSpawnChance)
                                continue;

                            // pick random pool key
                            string key = poolKeys[UnityEngine.Random.Range(0, poolKeys.Count)];

                            // attempt to spawn specifically at this center
                            bool spawned = SpawnEnemy(key, new List<Transform> { center });
                            if (spawned)
                            {
                                m_activeEnemyCount++;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"EnemySpawner: failed to auto-assign spawn points or initial spawns from generator: {ex.Message}");
        }

        if (m_running) return;

        m_running = true;
        m_currentWaveIndex = 0;

        if(m_spawnOnLoad)
            StartWave();
    }

    private void Update()
    {
        if (!m_running || m_state == WaveState.Idle || m_state == WaveState.Completed)
            return;

        // Procedural respawn handling (independent of wave system)
        if (m_useProceduralSpawner && m_initialTargetCount > 0)
        {
            // If active enemies have dropped below the respawn threshold of the initial target,
            // start spawning more until we reach the target count.
            int thresholdCount = Mathf.CeilToInt(m_initialTargetCount * m_procRespawnThreshold);

            if (m_activeEnemyCount <= thresholdCount && m_activeEnemyCount < m_initialTargetCount)
            {
                m_procRespawnTimer -= Time.deltaTime;
                if (m_procRespawnTimer <= 0f)
                {
                    TryProceduralRespawnOnce();
                    m_procRespawnTimer = m_procRespawnDelay;
                }
            }
            else
            {
                // reset timer when not respawning
                m_procRespawnTimer = 0f;
            }
        }

        switch (m_state)
        {
            case WaveState.Spawning:
                UpdateSpawning();
                break;

            case WaveState.WaitingForClear:
                if (m_activeEnemyCount <= 0)
                {
                    OnWaveFinished(m_waves[m_currentWaveIndex]);
                    EnterNextWaveDelay();
                }
                break;

            case WaveState.WaitingForNextWave:
                UpdateNextWaveWait();
                break;
        }
    }

    /// <summary>
    /// Other classes can reference this to start a new wave.
    /// </summary>
    public void SpawnWave()
    {
        StartWave();
    }

    #region --- WAVE FLOW ---

    private void StartWave()
    {
        if (m_currentWaveIndex >= m_waves.Count)
        {
            m_state = WaveState.Completed;
            OnAllWavesComplete();
            return;
        }

        StartSpawning(m_waves[m_currentWaveIndex]);
    }

    private void EnterNextWaveDelay()
    {
        if (m_waveStartMode == WaveStartMode.AutoAfterClear)
        {
            m_stateTimer = m_timeBetweenWaves;
            m_state = WaveState.WaitingForNextWave;
        }
        else
        {
            m_waitingForNextWave = true;
            m_state = WaveState.WaitingForNextWave;
        }
    }

    private void UpdateNextWaveWait()
    {
        if (m_waveStartMode == WaveStartMode.AutoAfterClear)
        {
            m_stateTimer -= Time.deltaTime;

            if (m_stateTimer <= 0f)
            {
                m_currentWaveIndex++;
                StartWave();
            }
        }
        else
        {
            if (!m_waitingForNextWave)
            {
                m_currentWaveIndex++;
                StartWave();
            }
        }
    }

    #endregion

    #region --- SPAWNING ---

    private void StartSpawning(Wave wave)
    {
        m_groupIndex = 0;
        m_enemyIndexInGroup = 0;
        m_spawnTimer = 0f;

        m_state = WaveState.Spawning;
    }

    private void UpdateSpawning()
    {
        if (true)
            return;

        Wave wave = m_waves[m_currentWaveIndex];

        if (wave.enemies == null || wave.enemies.Count == 0)
        {
            m_state = WaveState.WaitingForClear;
            return;
        }

        m_spawnTimer -= Time.deltaTime;

        if (m_spawnTimer > 0f)
            return;

        if (m_groupIndex >= wave.enemies.Count)
        {
            m_state = WaveState.WaitingForClear;
            return;
        }

        var group = wave.enemies[m_groupIndex];

        // Attempt to spawn enemy; only increment counters if spawn succeeded
        bool spawned = SpawnEnemy(group.poolKey, wave.spawnPoints);
        if (spawned)
        {
            m_activeEnemyCount++;
            m_enemyIndexInGroup++;
            m_spawnTimer = wave.spawnDelay;

            // Move to next group if done
            if (m_enemyIndexInGroup >= group.count)
            {
                m_groupIndex++;
                m_enemyIndexInGroup = 0;
            }
        }
        else
        {
            // no free spawn point available right now — retry shortly
            m_spawnTimer = 0.1f;
        }
    }

    #endregion

    #region --- ENEMY HANDLING ---

    /// <summary>
    /// Attempts to spawn an enemy at a free spawn center. Returns true if a spawn occurred.
    /// This method will avoid spawn centers that are too close to the player.
    /// </summary>
    private bool SpawnEnemy(string key, List<Transform> spawnPoints)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points assigned!");
            return false;
        }

        // choose from free spawn points only
        var free = spawnPoints.Where(s => s != null && !m_occupiedSpawnCenters.Contains(s)).ToList();
        if (free.Count == 0)
        {
            // nothing free right now
            return false;
        }

        // Exclude any spawn point that is too close to the player to avoid visible pop-in.
        Transform playerTransform = null;
        var fps = m_manager?.GetPlayer();
        if (fps != null)
            playerTransform = fps.transform;
        else
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null)
                playerTransform = pgo.transform;
        }

        if (playerTransform != null && m_minDistanceFromPlayer > 0f)
        {
            free = free.Where(s => Vector3.Distance(s.position, playerTransform.position) >= m_minDistanceFromPlayer).ToList();
            if (free.Count == 0)
            {
                // No valid spawn points after applying distance rule
                return false;
            }
        }

        Transform spawn = free[UnityEngine.Random.Range(0, free.Count)];

        var obj = ObjectPooler.Instance.Spawn(key, spawn.position, spawn.rotation);
        if (obj == null)
            return false;

        // mark occupancy and map
        m_occupiedSpawnCenters.Add(spawn);
        m_enemyToSpawnCenter[obj] = spawn;

        if (obj.TryGetComponent<Enemy>(out var enemy))
        {
            // inform enemy of its spawn center so it can generate patrols around it
            enemy.SetSpawnCenter(spawn);
            enemy.Activate(this);
            // also provide pool key so enemy can return itself if needed
            enemy.SetPoolKey(key);

            // Ensure NavMeshAgent is positioned correctly so it does not "snap" to the nearest navmesh (which
            // can appear as a teleport into the player's area). Warp the agent to the intended spawn position.
            var agent = obj.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                try
                {
                    agent.Warp(spawn.position);
                }
                catch (Exception)
                {
                    // In some Unity versions calling Warp immediately can throw if agent isn't ready.
                    // Best-effort only; swallowing exception to avoid breaking spawn flow.
                }
            }
        }

        return true;
    }

    // Tries a single procedural respawn step: pick a free center (not near player) and spawn a random pool key
    private void TryProceduralRespawnOnce()
    {
        if (m_proceduralAvailableCenters == null || m_proceduralAvailableCenters.Count == 0 || m_proceduralPoolKeys == null || m_proceduralPoolKeys.Count == 0)
            return;

        // build free centers list (also ensure still not too close to player)
        Vector3 playerPos = Vector3.zero;
        var player = m_manager?.GetPlayer();
        if (player != null)
            playerPos = player.transform.position;

        var free = m_proceduralAvailableCenters.Where(t => t != null && !m_occupiedSpawnCenters.Contains(t) && Vector3.Distance(t.position, playerPos) >= m_minDistanceFromPlayer).ToList();
        if (free.Count == 0)
        {
            // Nothing free to spawn right now
            return;
        }

        Transform spawn = free[UnityEngine.Random.Range(0, free.Count)];
        string key = m_proceduralPoolKeys[UnityEngine.Random.Range(0, m_proceduralPoolKeys.Count)];

        bool spawned = SpawnEnemy(key, new List<Transform> { spawn });
        if (spawned)
        {
            m_activeEnemyCount++;
        }
    }

    public void RemoveEnemy(string key, GameObject obj)
    {
        // free occupied center if present
        if (obj != null && m_enemyToSpawnCenter.TryGetValue(obj, out var center))
        {
            m_enemyToSpawnCenter.Remove(obj);
            if (center != null)
                m_occupiedSpawnCenters.Remove(center);
        }

        ObjectPooler.Instance.ReturnToPool(key, obj);
        m_activeEnemyCount--;
        if (obj.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.Deactivate();
        }
    }

    #endregion

    #region --- EVENTS ---

    private void OnWaveFinished(Wave currentWave)
    {
        currentWave.OnWaveCompleted?.Invoke(m_currentWaveIndex);
        Debug.Log($"Wave {m_currentWaveIndex} complete");
    }

    private void OnAllWavesComplete()
    {
        Debug.Log("All waves complete!");
        OnAllWavesCompleted?.Invoke();
    }

    #endregion

    #region --- PUBLIC API ---

    public void StartNextWave()
    {
        if (m_waveStartMode != WaveStartMode.Manual)
            return;

        if (!m_waitingForNextWave)
            return;

        Debug.Log("Next wave has started");

        m_waitingForNextWave = false;
    }

    public LevelManager GetLevelManager()
    {
        if (m_manager == null)
        {
            Debug.LogError("Missing level manager reference.");
            return null;
        }
        return m_manager;
    }

    #endregion

    #region --- NEW API ---

    /// <summary>
    /// Deregisters an enemy from the spawner without returning it to the pool.
    /// Use this when you want the enemy GameObject to remain in the scene (eg. a dead body).
    /// This frees the spawn center and decrements the active count but does not call ReturnToPool.
    /// </summary>
    /// <param name="obj">The enemy GameObject to deregister.</param>
    public void DeregisterEnemy(GameObject obj)
    {
        // free occupied center if present
        if (obj != null && m_enemyToSpawnCenter.TryGetValue(obj, out var center))
        {
            m_enemyToSpawnCenter.Remove(obj);
            if (center != null)
                m_occupiedSpawnCenters.Remove(center);
        }

        // just decrement active count. Do NOT deactivate the enemy here - leaving it
        // active allows knockback/corpse behaviour to continue after death.
        m_activeEnemyCount = Mathf.Max(0, m_activeEnemyCount - 1);

        // Keep this object active in the scene as a corpse. Do not call enemy.Deactivate().
        // (If callers truly need the Enemy reference they can still GetComponent<Enemy>().)
    }

    #endregion
}

[System.Serializable]
public class Wave
{
    public string waveName;

    [System.Serializable]
    public class EnemyGroup
    {
        public string poolKey;
        public int count;
    }
    
    public List<EnemyGroup> enemies;

    public List<Transform> spawnPoints;

    public float spawnDelay = 0.5f;

    public UnityEvent<int> OnWaveCompleted;
}