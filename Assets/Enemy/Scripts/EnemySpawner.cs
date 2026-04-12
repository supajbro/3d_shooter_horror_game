using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawner : MonoBehaviour
{
    public static int ActiveEnemyCount = 0;

    [SerializeField] private List<Wave> m_waves;
    [SerializeField] private bool m_autoStart = true;

    private int m_currentWaveIndex = 0;

    private LevelManager m_manager;

    public void Init(LevelManager manager)
    {
        m_manager = manager;

        if (m_autoStart)
        {
            StartCoroutine(RunWaves());
        }
    }

    private IEnumerator RunWaves()
    {
        while (m_currentWaveIndex < m_waves.Count)
        {
            yield return StartCoroutine(SpawnWave(m_waves[m_currentWaveIndex]));

            // Wait until all enemies are dead
            yield return new WaitUntil(() => ActiveEnemyCount <= 0);

            m_currentWaveIndex++;
        }

        OnAllWavesComplete();
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
        if (spawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points assigned!");
            return;
        }

        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Count)];

        var obj = ObjectPooler.Instance.Spawn(key, spawn.position, spawn.rotation);
        
        // Find the enemy and activate it.
        if(obj.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.Activate(this);
        }
    }

    public void RemoveEnemy(string key, GameObject obj)
    {
        ObjectPooler.Instance.ReturnToPool(key, obj);
        ActiveEnemyCount--;

        // Find the enemy and deactivate it.
        if (obj.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.Deactivate();
        }
    }

    private void OnAllWavesComplete()
    {
        Debug.Log("All waves complete!");
        // Trigger next level, rewards, etc.
    }

    public LevelManager GetLevelManager()
    {
        if(m_manager == null)
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