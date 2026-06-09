using UnityEngine;

public class LevelOne : MonoBehaviour
{
    StateManager m_state = new StateManager();

    [Header("First Enemy Wave Stats")]
    [SerializeField] private float m_screamDelay = 1.5f; // <- How long it takes from the enemy screams before wave spawns in

    [Header("Tunnel Blockage")]
    [SerializeField] private BoxCollider m_tunnelBlockage;
    [SerializeField] private Animator m_tunnelBlockageAnim;

    private LevelManager m_manager;

    private void Start()
    {
        m_manager = FindFirstObjectByType<LevelManager>();
    }

    private void Update()
    {
        m_state.Update(Time.deltaTime);
    }

    public void SpawnFirstEnemyWave()
    {
        m_state.Enqueue(new ActionState(() => EnemyScream()));
        m_state.Enqueue(new DelayState(m_screamDelay));
        m_state.Enqueue(new ActionState(() => SpawnEnemy()));
    }

    private void EnemyScream()
    {
        Debug.Log("Enemy scream noise played");
    }

    private void SpawnEnemy()
    {
        if(!m_manager)
        {
            Debug.LogError("Missing reference to enemy spawner. Enemy wave will not start.");
            return;
        }
        m_manager.GetEnemySpawner().SpawnWave();
    }

    public void OpenTunnel()
    {
        if(!m_tunnelBlockage || !m_tunnelBlockageAnim)
        {
            Debug.LogError("Missing reference to the Tunnel Blockage");
            return;
        }

        // User can now progress through the tunnel.

        m_tunnelBlockage.enabled = false;
        m_tunnelBlockageAnim.SetTrigger("Play");
    }
}
