using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour, IPoolable
{
    [Header("References")]
    [SerializeField] protected Animator m_anim;
    private EnemyHealth m_health;
    private EnemyUtils m_utils;
    private EnemySpawner m_enemySpawner;

    [Header("Stats")]
    [SerializeField] protected float    m_chaseRange = 20f;            // <- Range enemy is within player to start chasing.
    [SerializeField] protected float    m_attackRange = 2f;            // <- How close enemy has to be to attack player.
    [SerializeField] protected float    m_attackCooldown = 1f;         // <- How long it takes to attack player again.
    [SerializeField] protected int      m_damage = 10;

    [Header("Attacl")]
    [SerializeField] protected bool     m_attacking = false;
    [SerializeField] protected float    m_attackDelay = 1.5f;

    [Header("Health Stats")]
    [SerializeField] protected float m_maxHealth = 100.0f;

    [Header("Weapon Spawning")]
    [SerializeField] protected bool shouldSpawnWeapon = true;

    [Header("Player")]
    protected Transform m_player;
    protected PlayerHealth m_playerHealth;

    [Header("Items enemy can drop")]
    private IDropable[] m_drops;

    protected NavMeshAgent m_agent;
    private string m_poolKey;

    private float m_lastAttackTime;

    private bool m_active = false;

    [SerializeField] private bool m_debug = false;

    public virtual void Activate(EnemySpawner enemySpawner)
    {
        m_active = true;

        if(m_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                m_player = playerObj.transform;
                m_playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        if (m_agent == null)
            m_agent = GetComponent<NavMeshAgent>();

        if (m_health == null)
            m_health = gameObject.AddComponent<EnemyHealth>();

        if (m_health != null)
        {
            m_health.Init(m_maxHealth);
            m_health.OnDied += KillEnemy;
        }

        if(m_utils == null)
            m_utils = GetComponent<EnemyUtils>();

        if(m_utils != null)
            m_utils.InitDebug(m_debug, m_health);

        if (m_enemySpawner == null)
            m_enemySpawner = enemySpawner;

        if(m_drops == null)
            m_drops = GetComponents<IDropable>();
    }

    public void Deactivate()
    {
        m_active = false;
    }

    protected virtual void Update()
    {
        if (GameStateManager.Instance.GetFreezeGame())
        {
            if(!m_agent.isStopped)
            {
                m_agent.isStopped = true;
                m_anim.SetTrigger("Idle");
            }

            return;
        }

        if (!m_active)
        {
            return;
        }

        if(m_anim == null)
        {
            Debug.LogError("Missing reference to enemy animation.");
            return;
        }

        if (m_player == null)
        {
            Debug.LogError("Missing reference to player.");
            return;
        }
    }

    protected virtual void ChasePlayer()
    {
        m_agent.isStopped = false;
        m_agent.SetDestination(m_player.position);
        m_anim.SetTrigger("Run");
    }

    protected virtual void StartAttack()
    {
        m_attacking = true;
        m_agent.isStopped = true;
        m_anim.SetTrigger("Attack");
        StartCoroutine(AttackRoutine());
    }

    protected virtual void AttackPlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = m_player.position + Vector3.up * 1.0f;

        Vector3 direction = (target - origin).normalized;

        // Face player
        Vector3 lookDir = direction;
        lookDir.y = 0;
        transform.forward = lookDir;

        if (Time.time < m_lastAttackTime + m_attackCooldown)
            return;

        m_lastAttackTime = Time.time;

        Debug.DrawRay(origin, direction * m_attackRange, Color.red, 1f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, m_attackRange))
        {
            if (hit.transform.root == m_player)
            {
                Debug.Log("Attacked player");

                if (m_playerHealth != null)
                    m_playerHealth.SetHealthRelative(-m_damage);
            }
        }

        m_attacking = false;
    }

    protected virtual IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(m_attackDelay);
        AttackPlayer();
    }

    protected virtual void Idle()
    {
        m_agent.isStopped = true;
        m_anim.SetTrigger("Idle");
    }

    protected virtual bool CanSeePlayer()
    {
        Ray ray = new Ray(transform.position + Vector3.up, (m_player.position - transform.position).normalized);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, m_chaseRange))
        {
            return hit.transform == m_player;
        }

        return false;
    }

    public virtual EnemyHealth GetHealth()
    {
        if (m_health == null)
        {
            Debug.LogError("Missing health reference to enemy.");
            return null;
        }
        return m_health;
    }

    public virtual void KillEnemy()
    {
        SpawnWeapon();
        m_enemySpawner.RemoveEnemy(m_poolKey, this.gameObject);
    }

    private Vector3 GetFlatDirectionToTarget()
    {
        Vector3 dir = (m_player.position - transform.position);
        dir.y = 0;
        return dir.normalized;
    }

    protected void FaceTarget()
    {
        Vector3 dir = GetFlatDirectionToTarget();
        if (dir.sqrMagnitude > 0.001f)
            transform.forward = dir;
    }

    public void SetPoolKey(string key)
    {
        m_poolKey = key;
    }

    protected void SpawnWeapon()
    {
        foreach (IDropable drop in m_drops)
        {
            drop.Drop();
        }

        if (shouldSpawnWeapon)
        {
            m_enemySpawner.GetLevelManager().GetWeaponSpawner().SpawnWeaponRandom(transform, null);
        }
    }
}