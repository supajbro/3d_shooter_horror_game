using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject m_playerPrefab;
    [SerializeField] private Transform m_spawnPoint;

    private GameObject m_currentPlayer;

    private void Start()
    {
        SpawnPlayer();
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
}