using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;

public class Enemy : MonoBehaviour, IPoolable
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Stun,
        Fallen,
        Recover
    }
    private EnemyState m_state = EnemyState.Idle;
    private float m_stateTimer;

    [Header("References")]
    [SerializeField] protected Animator m_anim;
    [SerializeField] protected Animator m_testAnim; // <- What will be the future animator when i make my own model.
    private EnemyHealth m_health;
    private EnemyUtils m_utils;
    private EnemySpawner m_enemySpawner;

    [Header("Stats")]
    [SerializeField] protected float    m_chaseRange = 20f;            // <- Range enemy is within player to start chasing.
    [SerializeField] protected float    m_attackRange = 2f;            // <- How close enemy has to be to attack player.
    [SerializeField] protected float    m_attackCooldown = 1f;         // <- How long it takes to attack player again.
    [SerializeField] protected int      m_damage = 10;

    [Header("Attack")]
    [SerializeField] protected bool     m_attacking = false;
    [SerializeField] protected float    m_attackDelay = 1.5f;

    [Header("Knockback")]
    private bool m_isKnockedBack;
    private Vector3 m_knockbackVelocity;
    private float m_knockbackForce = 2.0f;
    private float m_knockbackTimer;
    private float m_knockbackDuration;
    private float m_knockbackStartY;
    private float m_knockbackVerticalStrength;

    [Header("Health Stats")]
    [SerializeField] protected float m_maxHealth = 100.0f;

    [Header("Weapon Spawning")]
    [SerializeField] protected bool shouldSpawnWeapon = true;

    [Header("Player")]
    protected Transform m_player;
    protected PlayerHealth m_playerHealth;

    [Header("Items enemy can drop")]
    private IDropable[] m_drops;

    [Header("Fallover")]
    [SerializeField] private float m_fallOverChance = 0.25f;
    [SerializeField] private float m_getUpDuration = 1.5f;
    private bool m_hasFallen;
    private Vector3 m_fallDirection;

    protected NavMeshAgent m_agent;
    private string m_poolKey;

    private float m_lastAttackTime;

    private bool m_active = false;

    [SerializeField] private bool m_debug = false;
    private TextMeshPro m_debugStateText;

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

        if (m_debug)
        {
            CreateDebugStateText();
        }
    }

    public void Deactivate()
    {
        m_active = false;
    }

    protected virtual void Update()
    {
        if (!m_active)
            return;

        if (GameStateManager.Instance.GetFreezeGame())
        {
            m_agent.isStopped = true;
            return;
        }

        switch (m_state)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;

            case EnemyState.Walk:
                UpdateWalk();
                break;

            case EnemyState.Stun:
                UpdateStun();
                break;

            case EnemyState.Fallen:
                UpdateFallen();
                break;

            case EnemyState.Recover:
                UpdateRecover();
                break;
        }

        if (m_debugStateText != null && Camera.main != null)
        {
            m_debugStateText.transform.forward = Camera.main.transform.forward;
        }
    }

    protected void ChangeState(EnemyState newState)
    {
        if (m_state == newState)
            return;

        m_state = newState;

        switch (m_state)
        {
            case EnemyState.Idle:
                m_agent.isStopped = true;
                m_anim.SetTrigger("Idle");
                break;

            case EnemyState.Walk:
                m_agent.isStopped = false;
                m_anim.SetTrigger("Run");
                break;

            case EnemyState.Stun:
                m_agent.isStopped = true;
                break;

            case EnemyState.Fallen:
                m_agent.isStopped = true;
                m_anim.SetTrigger("Fall");
                m_stateTimer = 2f; // Time spent on the floor
                break;

            case EnemyState.Recover:
                m_agent.isStopped = true;

                if (m_hasFallen)
                {
                    m_anim.SetTrigger("GetUp");
                    m_stateTimer = m_getUpDuration;
                }
                else
                {
                    m_anim.SetTrigger("Recover");
                    m_stateTimer = 0.35f;
                }

                break;
        }

        if (m_debugStateText != null)
            m_debugStateText.text = m_state.ToString();
    }

    protected virtual void UpdateIdle()
    {
        if (CanSeePlayer())
        {
            ChangeState(EnemyState.Walk);
        }

        m_testAnim.SetTrigger("Idle");

        // TODO:
        // Patrol waypoint reached?
        // Wait?
        // Choose next waypoint?
    }

    protected virtual void UpdateWalk()
    {
        if (!CanSeePlayer())
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        m_agent.SetDestination(m_player.position);

        FaceTarget();

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance <= m_attackRange)
        {
            StartAttack();
        }
    }

    protected virtual void UpdateStun()
    {
        HandleKnockback();
    }

    protected virtual void UpdateFallen()
    {
        m_stateTimer -= Time.deltaTime;

        if (m_stateTimer <= 0f)
        {
            m_hasFallen = false;
            ChangeState(EnemyState.Recover);
        }

        // Lay the enemy onto the ground in the direction they were knocked.
        Vector3 fallDir = m_fallDirection;
        fallDir.y = 0f;

        if (fallDir.sqrMagnitude > 0.001f)
        {
            Quaternion face = Quaternion.LookRotation(fallDir);

            // Rotate so the enemy lies on its back/side.
            // Change the axis/angle depending on your model's forward axis.
            Quaternion fallenRotation = face * Quaternion.Euler(90f, 0f, 0f);

            m_testAnim.gameObject.transform.rotation = Quaternion.Slerp(
                 m_testAnim.gameObject.transform.rotation,
                fallenRotation,
                Time.deltaTime * 8f);
        }
    }

    protected virtual void UpdateRecover()
    {
        if (m_hasFallen)
        {
            ChangeState(EnemyState.Fallen);

            Quaternion fallenRotation = Quaternion.Euler(0f, 0f, 0f);
            m_testAnim.gameObject.transform.rotation = Quaternion.Slerp(
             m_testAnim.gameObject.transform.rotation,
            fallenRotation,
            Time.deltaTime * 8f);

            return;
        }

        m_stateTimer -= Time.deltaTime;

        if (m_stateTimer <= 0f)
        {
            if (CanSeePlayer())
                ChangeState(EnemyState.Walk);
            else
                ChangeState(EnemyState.Idle);
        }
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

    public void ApplyKnockback(Vector3 velocity, float duration)
    {
        ChangeState(EnemyState.Stun);

        m_knockbackVelocity = new Vector3(velocity.x, 0f, velocity.z);

        m_knockbackDuration = duration;
        m_knockbackTimer = 0f;

        m_knockbackStartY = transform.position.y;
        m_knockbackVerticalStrength = m_knockbackForce * 0.5f;

        m_fallDirection = velocity.normalized;
    }

    private void HandleKnockback()
    {
        m_knockbackTimer += Time.deltaTime;

        float t = m_knockbackTimer / m_knockbackDuration;

        if (t >= 1f)
        {
            Vector3 finalPos = transform.position;
            finalPos.y = m_knockbackStartY;
            transform.position = finalPos;

            if (m_agent != null)
            {
                m_agent.Warp(transform.position);
                m_agent.isStopped = false;
            }

            m_hasFallen = Random.value <= m_fallOverChance;

            ChangeState(EnemyState.Recover);
            return;
        }

        // horizontal decay
        float curve = Mathf.Pow(1f - t, 2f); // stronger initial push
        Vector3 horizontal = m_knockbackVelocity * 7.5f * curve;

        // vertical arc (jump feel)
        float height = Mathf.Sin(t * Mathf.PI) * m_knockbackVerticalStrength;

        Vector3 pos = transform.position;
        pos += horizontal * Time.deltaTime;
        pos.y = m_knockbackStartY + height;

        transform.position = pos;
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

    private void CreateDebugStateText()
    {
        if (m_debugStateText != null)
            return;

        GameObject go = new GameObject("Debug State");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.up * 2.5f;

        m_debugStateText = go.AddComponent<TextMeshPro>();

        m_debugStateText.fontSize = 20;
        m_debugStateText.alignment = TextAlignmentOptions.Center;
        m_debugStateText.color = Color.white;
        m_debugStateText.text = m_state.ToString();

        MeshRenderer renderer = m_debugStateText.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}