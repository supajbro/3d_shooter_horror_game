using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public static int ActiveEnemyCount = 0;

    public enum WaveStartMode
    {
        AutoAfterClear,
        Manual
    }

    [Header("Wave Settings")]
    [SerializeField] private List<Wave> m_waves;
    [SerializeField] private WaveStartMode m_waveStartMode = WaveStartMode.AutoAfterClear;
    [SerializeField] private float m_timeBetweenWaves = 3f;

    private int m_currentWaveIndex = 0;
    private bool m_waitingForNextWave = false;
    private bool m_running = false;

    private LevelManager m_manager;

    public event Action<int> OnWaveCompleted;
    public event Action OnAllWavesCompleted;

    public void Init(LevelManager manager)
    {
        m_manager = manager;

        if (m_running) return;

        m_running = true;
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        while (m_currentWaveIndex < m_waves.Count)
        {
            yield return StartCoroutine(SpawnWave(m_waves[m_currentWaveIndex]));

            yield return new WaitUntil(() => ActiveEnemyCount <= 0);

            OnWaveFinished();

            if (m_currentWaveIndex >= m_waves.Count)
                break;

            if (m_waveStartMode == WaveStartMode.AutoAfterClear)
            {
                yield return new WaitForSeconds(m_timeBetweenWaves);
            }
            else
            {
                m_waitingForNextWave = true;
                yield return new WaitUntil(() => m_waitingForNextWave == false);
            }

            m_currentWaveIndex++;
        }

        OnAllWavesCompleted?.Invoke();
    }

    private IEnumerator SpawnWave(Wave wave)
    {
        foreach (var group in wave.enemies)
        {
            for (int i = 0; i < group.count; i++)
            {
                SpawnEnemy(group.poolKey, wave.spawnPoints);

                ActiveEnemyCount++;

                yield return new WaitForSeconds(wave.spawnDelay);
            }
        }
    }

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

    private void OnWaveFinished()
    {
        OnWaveCompleted?.Invoke(m_currentWaveIndex);
        Debug.Log($"Wave {m_currentWaveIndex} complete");
    }

    public void StartNextWave()
    {
        if (m_waveStartMode != WaveStartMode.Manual)
            return;

        if (!m_waitingForNextWave)
            return;

        Debug.Log("Next wave has started");

        m_waitingForNextWave = false;
        m_currentWaveIndex++;
    }

    private void OnAllWavesComplete()
    {
        Debug.Log("All waves complete!");
        OnAllWavesCompleted?.Invoke();
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
}