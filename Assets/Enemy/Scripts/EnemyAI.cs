using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public float chaseRange = 20f;
    public float attackRange = 2f;
    public float attackCooldown = 1f;
    public int damage = 10;

    private Transform player;
    private NavMeshAgent agent;

    private float lastAttackTime;

    private EnemyHealth m_health;
    private EnemyUtils m_utils;

    [SerializeField] private bool m_debug = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        agent = GetComponent<NavMeshAgent>();

        m_health = gameObject.AddComponent<EnemyHealth>();
        m_health.Init();

        m_utils = GetComponent<EnemyUtils>();
        m_utils.InitDebug(m_debug, m_health);
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            AttackPlayer();
        }
        else if (distance <= chaseRange && CanSeePlayer())
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
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    void AttackPlayer()
    {
        agent.isStopped = true;

        // Face player
        Vector3 lookDir = (player.position - transform.position).normalized;
        lookDir.y = 0;
        transform.forward = lookDir;

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;

            Debug.Log("Enemy attacks!");

            // TODO: call player damage function
            // player.GetComponent<PlayerHealth>().TakeDamage(damage);
        }
    }

    void Idle()
    {
        agent.isStopped = true;
    }

    bool CanSeePlayer()
    {
        Ray ray = new Ray(transform.position + Vector3.up, (player.position - transform.position).normalized);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, chaseRange))
        {
            return hit.transform == player;
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
}