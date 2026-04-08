using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator m_anim;
    private EnemyHealth m_health;
    private EnemyUtils m_utils;

    [Header("Stats")]
    [SerializeField] protected float m_chaseRange = 20f;            // <- Range enemy is within player to start chasing.
    [SerializeField] protected float m_attackRange = 2f;            // <- How close enemy has to be to attack player.
    [SerializeField] protected float m_attackCooldown = 1f;         // <- How long it takes to attack player again.
    [SerializeField] protected int m_damage = 10;

    private Transform m_player;
    private NavMeshAgent m_agent;

    private float m_lastAttackTime;

    [SerializeField] private bool m_debug = false;

    void Start()
    {
        m_player = GameObject.FindGameObjectWithTag("Player").transform;
        m_agent = GetComponent<NavMeshAgent>();

        m_health = gameObject.AddComponent<EnemyHealth>();
        m_health.Init();
        m_health.OnDied += KillEnemy;

        m_utils = GetComponent<EnemyUtils>();
        m_utils.InitDebug(m_debug, m_health);
    }

    void Update()
    {
        if(m_anim == null)
        {
            Debug.LogError("Missing reference to enemy animation.");
            return;
        }

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance <= m_attackRange)
        {
            AttackPlayer();
        }
        else if (distance <= m_chaseRange && CanSeePlayer())
        {
            ChasePlayer();
        }
        else
        {
            Idle();
        }
    }

    void ChasePlayer()
    {
        m_agent.isStopped = false;
        m_agent.SetDestination(m_player.position);

        m_anim.SetTrigger("Run");
    }

    void AttackPlayer()
    {
        m_agent.isStopped = true;

        // Face player
        Vector3 lookDir = (m_player.position - transform.position).normalized;
        lookDir.y = 0;
        transform.forward = lookDir;

        if (Time.time >= m_lastAttackTime + m_attackCooldown)
        {
            m_lastAttackTime = Time.time;

            Debug.Log("Enemy attacks!");

            // TODO: call player damage function
            // player.GetComponent<PlayerHealth>().TakeDamage(damage);
        }
    }

    void Idle()
    {
        m_agent.isStopped = true;
        m_anim.SetTrigger("Idle");
    }

    bool CanSeePlayer()
    {
        Ray ray = new Ray(transform.position + Vector3.up, (m_player.position - transform.position).normalized);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, m_chaseRange))
        {
            return hit.transform == m_player;
        }

        return false;
    }

    public EnemyHealth GetHealth()
    {
        if (m_health == null)
        {
            Debug.LogError("Missing health reference to enemy.");
            return null;
        }
        return m_health;
    }

    // TODO: Change this when moving to pooling
    public void KillEnemy()
    {
        Destroy(gameObject);
    }
}