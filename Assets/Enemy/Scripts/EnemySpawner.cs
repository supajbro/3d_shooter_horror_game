using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemySpawner : MonoBehaviour
{
    public static int ActiveEnemyCount = 0;

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

    public UnityEvent OnAllWavesCompleted;

    public void Init(LevelManager manager)
    {
        m_manager = manager;

        if (m_running) return;

        m_running = true;
        m_currentWaveIndex = 0;

        StartWave();
    }

    private void Update()
    {
        if (!m_running || m_state == WaveState.Idle || m_state == WaveState.Completed)
            return;

        switch (m_state)
        {
            case WaveState.Spawning:
                UpdateSpawning();
                break;

            case WaveState.WaitingForClear:
                if (ActiveEnemyCount <= 0)
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

        // Spawn enemy
        SpawnEnemy(group.poolKey, wave.spawnPoints);
        ActiveEnemyCount++;

        m_enemyIndexInGroup++;
        m_spawnTimer = wave.spawnDelay;

        // Move to next group if done
        if (m_enemyIndexInGroup >= group.count)
        {
            m_groupIndex++;
            m_enemyIndexInGroup = 0;
        }
    }

    #endregion

    #region --- ENEMY HANDLING ---

    private void SpawnEnemy(string key, List<Transform> spawnPoints)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points assigned!");
            return;
        }

        Transform spawn = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];

        var obj = ObjectPooler.Instance.Spawn(key, spawn.position, spawn.rotation);

        if (obj.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.Activate(this);
        }
    }

    public void RemoveEnemy(string key, GameObject obj)
    {
        ObjectPooler.Instance.ReturnToPool(key, obj);
        ActiveEnemyCount--;

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