using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject m_playerPrefab;
    [SerializeField] private Transform m_spawnPoint;
    private GameObject m_currentPlayer;

    [Header("Enemy")]
    private EnemySpawner m_enemySpawner;

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
}