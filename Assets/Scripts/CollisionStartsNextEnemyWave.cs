using UnityEngine;

public class CollisionStartsNextEnemyWave : MonoBehaviour
{
    private LevelManager m_manager;
    private EnemySpawner m_enemySpawner;

    public void Init(LevelManager manager)
    {
        m_manager = manager;

        if(m_manager != null)
        {
            m_enemySpawner = m_manager.GetEnemySpawner();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(m_enemySpawner == null)
        {
            Debug.LogError("Missing reference to enemy spawner.");
            return;
        }
        m_enemySpawner.StartNextWave();
    }
}
