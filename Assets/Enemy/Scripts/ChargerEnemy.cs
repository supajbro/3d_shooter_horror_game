using UnityEngine;
using UnityEngine.AI;

public class ChargerEnemy : MonoBehaviour, IPoolable
{
    [Header("References")]
    [SerializeField] private Animator m_anim;

    private EnemyHealth m_health;
    private EnemyUtils m_utils;
    private EnemySpawner m_enemySpawner;

    [Header("Charge Settings")]
    [SerializeField] private float m_windupTime = 0.8f;
    [SerializeField] private float m_chargeDuration = 0.6f;
    [SerializeField] private float m_cooldown = 2f;
    [SerializeField] private int m_damage = 25;

    [Header("Player")]
    private Transform m_target;
    private PlayerHealth m_playerHealth;

    private NavMeshAgent m_agent;
    private Rigidbody m_rb;

    private string m_poolKey;

    private enum State
    {
        Idle,
        Windup,
        Charging,
        Cooldown
    }

    private State m_state;
    private float m_stateTime;

    private bool m_active;

    public void Activate(EnemySpawner enemySpawner)
    {
        m_active = true;
        m_enemySpawner = enemySpawner;

        if (m_agent == null)
            m_agent = GetComponent<NavMeshAgent>();

        if (m_rb == null)
            m_rb = GetComponent<Rigidbody>();

        if (m_health == null)
        {
            m_health = gameObject.AddComponent<EnemyHealth>();
            m_health.Init();
            m_health.OnDied += KillEnemy;
        }

        if (m_target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                m_target = playerObj.transform;
                m_playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        if (m_utils == null)
            m_utils = GetComponent<EnemyUtils>();

        m_state = State.Charging;
        m_stateTime = 0f;

        if (m_agent != null)
        {
            m_agent.isStopped = false;
        }
    }

    public void Deactivate()
    {
        m_active = false;
    }

    private void Update()
    {
        if (!m_active || m_target == null)
            return;

        if (GameStateManager.Instance.GetFreezeGame())
        {
            if (m_agent != null) m_agent.isStopped = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, m_target.position);

        switch (m_state)
        {
            case State.Idle:
                HandleIdle(distance);
                break;

            case State.Windup:
                HandleWindup();
                break;

            case State.Charging:
                HandleCharge();
                break;

            case State.Cooldown:
                HandleCooldown();
                break;
        }
    }

    private void HandleIdle(float distance)
    {
        //if (distance <= m_detectionRange)
        {
            m_state = State.Windup;
            m_stateTime = 0f;

            if (m_agent != null)
                m_agent.isStopped = true;

            m_anim?.SetTrigger("Windup");
        }
    }

    private void HandleWindup()
    {
        FaceTarget();

        m_stateTime += Time.deltaTime;

        if (m_stateTime >= m_windupTime)
        {
            BeginCharge();
        }
    }

    private void BeginCharge()
    {
        m_state = State.Charging;
        m_stateTime = 0f;

        m_agent.SetDestination(m_target.position);

        m_anim?.SetTrigger("Charge");
    }

    private void HandleCharge()
    {
        m_stateTime += Time.deltaTime;

        FaceTarget();

        m_agent.SetDestination(m_target.position);

        if (m_stateTime >= m_chargeDuration)
        {
            //EndCharge();
        }
    }

    private void EndCharge()
    {
        m_state = State.Cooldown;
        m_stateTime = 0f;

        if (m_rb != null)
            m_rb.linearVelocity = Vector3.zero;

        if (m_agent != null)
            m_agent.enabled = true;

        m_anim?.SetTrigger("Idle");
    }

    private void HandleCooldown()
    {
        m_stateTime += Time.deltaTime;

        if (m_stateTime >= m_cooldown)
        {
            m_state = State.Idle;
        }
    }

    private Vector3 GetFlatDirectionToTarget()
    {
        Vector3 dir = (m_target.position - transform.position);
        dir.y = 0;
        return dir.normalized;
    }

    private void FaceTarget()
    {
        Vector3 dir = GetFlatDirectionToTarget();
        if (dir.sqrMagnitude > 0.001f)
            transform.forward = dir;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (m_state != State.Charging)
            return;

        if (collision.transform == m_target)
        {
            if (m_playerHealth != null)
            {
                m_playerHealth.SetHealthRelative(-m_damage);
            }

            EndCharge();
        }
    }

    public void KillEnemy()
    {
        m_enemySpawner.RemoveEnemy(m_poolKey, gameObject);
    }

    public void SetPoolKey(string key)
    {
        m_poolKey = key;
    }
}