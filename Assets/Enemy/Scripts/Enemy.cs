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
    private Vector3 m_knockbackVelocity; // original horizontal input vector
    private float m_knockbackForce = 2.0f;
    private float m_knockbackTimer;
    private float m_knockbackDuration;
    private float m_knockbackStartY;
    private float m_knockbackVerticalStrength;

    // Ricochet / bounce configuration
    [SerializeField] private float m_ricochetRadius = 0.35f; // radius for spherecast when checking walls
    [SerializeField] private float m_ricochetBounceDamping = 0.6f; // fraction of speed kept after bounce
    [SerializeField] private float m_ricochetDrag = 5f; // how quickly horizontal velocity decays per second
    [SerializeField] private LayerMask m_ricochetLayerMask = (LayerMask)(-1); // which layers to collide with
    [SerializeField] private float m_minRicochetSpeed = 0.5f; // below this speed stop ricocheting

    // Dynamic ricochet state
    private bool m_allowRicochet = false;
    private Vector3 m_currentVelocity; // horizontal velocity used for ricochet simulation

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
    private Transform m_fallVisual;
    private Quaternion m_uprightVisualRotation;
    private Quaternion m_fallenVisualRotation;
    private bool m_testAnimatorDisabledForFall;

    protected NavMeshAgent m_agent;
    private string m_poolKey;

    private float m_lastAttackTime;

    private bool m_active = false;

    [SerializeField] private bool m_debug = false;
    private TextMeshPro m_debugStateText;

    // Vision / memory
    [Header("Vision")]
    [SerializeField] private float m_visionAngle = 90f; // total cone angle
    [SerializeField] private float m_eyeHeight = 1.5f; // origin height for raycasts
    [SerializeField] private float m_memoryMin = 3f;
    [SerializeField] private float m_memoryMax = 10f;
    protected float m_memoryTimer = 0f;

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

    private void LateUpdate()
    {
        // Animators update after Update. Apply the visual rotation here so an animation
        // clip cannot overwrite the direction the enemy was hit from.
        if (m_fallVisual == null)
            return;

        if (m_state == EnemyState.Fallen)
        {
            m_fallVisual.rotation = Quaternion.Slerp(
                m_fallVisual.rotation,
                m_fallenVisualRotation,
                Time.deltaTime * 8f);
        }
        else if (m_state == EnemyState.Recover && m_hasFallen)
        {
            m_fallVisual.localRotation = Quaternion.Slerp(
                m_fallVisual.localRotation,
                m_uprightVisualRotation,
                Time.deltaTime * 8f);
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
                PrepareFallRotation();

                // Fallover.anim owns this transform and includes a baked sideways
                // movement. Disable it so m_fallDirection is authoritative.
                if (m_testAnim != null)
                {
                    m_testAnim.enabled = false;
                    m_testAnimatorDisabledForFall = true;
                }

                m_stateTimer = 2f; // Time spent on the floor
                break;

            case EnemyState.Recover:
                m_agent.isStopped = true;

                if (m_hasFallen)
                {
/*                    m_anim.SetTrigger("GetUp");

                    if (m_testAnim != null)
                        m_testAnim.SetTrigger("GetUp");*/

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
        if (!CanSeePlayer() && m_memoryTimer <= 0.0f)
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

        HandleKnockback();

        if (m_stateTimer <= 0f)
        {
            ChangeState(EnemyState.Recover);
        }
    }

    protected virtual void UpdateRecover()
    {
        m_stateTimer -= Time.deltaTime;

        if (m_stateTimer <= 0f)
        {
            m_hasFallen = false;
            RestoreTestAnimatorAfterFall();

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

    /// <summary>
    /// Apply a knockback to the enemy.
    /// New parameter allowRicochet enables wall-bouncing behaviour when true.
    /// </summary>
    public void ApplyKnockback(Vector3 velocity, float duration, bool allowRicochet = false)
    {
        // Use the incoming velocity magnitude to scale knockback forces so different
        // weapons feel distinct. Strong hits (e.g. revolver / bullets) should feel
        // much more powerful and are more likely to make the enemy fall over.
        float incomingForce = velocity.magnitude;

        m_knockbackVelocity = new Vector3(velocity.x, 0f, velocity.z);
        m_knockbackForce = incomingForce;

        // Ensure duration scales reasonably with force so heavier hits feel heavier.
        m_knockbackDuration = Mathf.Max(duration, 0.25f + incomingForce * 0.03f);
        m_knockbackTimer = 0f;
        m_knockbackStartY = transform.position.y;

        // Vertical strength scales with force but is clamped to avoid extreme pops.
        m_knockbackVerticalStrength = Mathf.Clamp(m_knockbackForce * 0.6f, 0.5f, 8f);
        m_fallDirection = velocity.normalized;

        // Strong hits more likely (or guaranteed) to cause a fall.
        if (incomingForce >= 12f)
        {
            m_hasFallen = true; // e.g. revolver/bullet
        }
        else
        {
            // Slightly bias fall chance by force so stronger punches feel more satisfying.
            float biasedChance = Mathf.Clamp01(m_fallOverChance + (incomingForce - 3f) * 0.05f);
            m_hasFallen = Random.value <= biasedChance;
        }

        // Ricochet: prepare a horizontal velocity used for collisions and reflection.
        m_allowRicochet = allowRicochet;
        if (m_allowRicochet)
        {
            // Compute an initial horizontal speed similar to previous behavior's initial push
            float horizontalMultiplier = Mathf.Lerp(5f, 20f, Mathf.Clamp01(m_knockbackForce / 15f));
            m_currentVelocity = m_knockbackVelocity * horizontalMultiplier;
        }
        else
        {
            // keep currentVelocity zero so non-ricochet path uses legacy behaviour
            m_currentVelocity = Vector3.zero;
        }

        // Choose the reaction when the hit occurs. Stun and Fallen are now parallel
        // reactions, and both transition to Recover when their own duration ends.
        ChangeState(m_hasFallen ? EnemyState.Fallen : EnemyState.Stun);

        if (m_hasFallen && m_knockbackForce >= 12f)
        {
            // Give big hits a longer floor time
            m_stateTimer = Mathf.Max(m_stateTimer, 3.0f);
        }

        m_isKnockedBack = true;
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

            // reset ricochet state
            m_allowRicochet = false;
            m_currentVelocity = Vector3.zero;
            m_isKnockedBack = false;

            ChangeState(EnemyState.Recover);
            return;
        }

        // vertical arc (jump feel) - remains the same for both modes
        float height = Mathf.Sin(t * Mathf.PI) * m_knockbackVerticalStrength;

        if (m_allowRicochet && m_currentVelocity.sqrMagnitude > 0.0001f)
        {
            // Ricochet-enabled path: integrate horizontal velocity and handle wall collisions
            Vector3 horizontalMove = m_currentVelocity;
            float moveDistance = horizontalMove.magnitude * Time.deltaTime;

            if (moveDistance > 0f)
            {
                Vector3 dir = horizontalMove.normalized;
                Vector3 castOrigin = transform.position + Vector3.up * 0.5f; // cast around mid-height

                if (Physics.SphereCast(castOrigin, m_ricochetRadius, dir, out RaycastHit hit, moveDistance + 0.01f, m_ricochetLayerMask.value, QueryTriggerInteraction.Ignore))
                {
                    // ignore collisions with own root
                    if (hit.collider != null && hit.collider.transform.root != transform)
                    {
                        // Move to impact point (offset slightly from wall)
                        Vector3 impactPoint = hit.point + hit.normal * (m_ricochetRadius + 0.01f);
                        Vector3 pos = transform.position;
                        pos.x = impactPoint.x;
                        pos.z = impactPoint.z;
                        pos.y = m_knockbackStartY + height;
                        transform.position = pos;

                        // Reflect horizontal velocity and damp it
                        m_currentVelocity = Vector3.Reflect(m_currentVelocity, hit.normal) * m_ricochetBounceDamping;

                        // Small random deflection to avoid perfectly repeating bounces
                        float jitter = 0.05f * Mathf.Clamp01(m_knockbackForce / 15f);
                        if (jitter > 0f)
                        {
                            m_currentVelocity = Quaternion.Euler(0f, Random.Range(-jitter, jitter) * 180f, 0f) * m_currentVelocity;
                        }

                        // If speed is too low after bounce, stop ricocheting so legacy code can finish
                        if (m_currentVelocity.magnitude < m_minRicochetSpeed)
                        {
                            m_currentVelocity = Vector3.zero;
                            m_allowRicochet = false;
                        }
                    }
                    else
                    {
                        // Hit ourself (rare) - just move without reflecting
                        Vector3 pos = transform.position;
                        pos += m_currentVelocity * Time.deltaTime;
                        pos.y = m_knockbackStartY + height;
                        transform.position = pos;
                    }
                }
                else
                {
                    // No collision this frame - move normally
                    Vector3 pos = transform.position;
                    pos += m_currentVelocity * Time.deltaTime;
                    pos.y = m_knockbackStartY + height;
                    transform.position = pos;
                }
            }

            // Apply drag so horizontal velocity decays over time
            m_currentVelocity = Vector3.Lerp(m_currentVelocity, Vector3.zero, Time.deltaTime * m_ricochetDrag);
        }
        else
        {
            // Legacy fallback: smooth rotate/decay behaviour used previously.
            // horizontal decay
            float curve = Mathf.Pow(1f - t, 2f); // stronger initial push
            float horizontalMultiplier = Mathf.Lerp(5f, 20f, Mathf.Clamp01(m_knockbackForce / 15f));
            Vector3 horizontal = m_knockbackVelocity * horizontalMultiplier * curve;

            Vector3 pos = transform.position;
            pos += horizontal * Time.deltaTime;
            pos.y = m_knockbackStartY + height;

            transform.position = pos;
        }
    }

    private void PrepareFallRotation()
    {
        Animator fallAnimator = m_testAnim != null ? m_testAnim : m_anim;
        if (fallAnimator == null)
            return;

        m_fallVisual = fallAnimator.transform;
        m_uprightVisualRotation = m_fallVisual.localRotation;

        Vector3 fallDirection = m_fallDirection;
        fallDirection.y = 0f;

        if (fallDirection.sqrMagnitude <= 0.001f)
            fallDirection = transform.forward;

        fallDirection.Normalize();

        // Align the visual's up vector with the horizontal knockback direction.
        // The projectile supplies that direction, so the enemy falls away from the shot.
        m_fallenVisualRotation = Quaternion.FromToRotation(m_fallVisual.up, fallDirection) * m_fallVisual.rotation;
    }

    private void RestoreTestAnimatorAfterFall()
    {
        if (!m_testAnimatorDisabledForFall || m_testAnim == null)
            return;

        // Return to the upright pose before giving transform control back to Animator.
        m_testAnim.transform.localRotation = m_uprightVisualRotation;
        m_testAnim.enabled = true;
        m_testAnim.Rebind();
        m_testAnimatorDisabledForFall = false;
    }

    protected virtual bool CanSeePlayer()
    {
        if (m_player == null)
            return false;

        // If we still remember the player's location, count down timer and keep "seeing" them
        if (m_memoryTimer > 0f)
        {
            m_memoryTimer -= Time.deltaTime;
            return true;
        }

        Vector3 origin = transform.position + Vector3.up * m_eyeHeight;
        Vector3 toPlayer = m_player.position - origin;
        float distance = toPlayer.magnitude;

        if (distance > m_chaseRange)
            return false;

        // Angle check - don't attempt raycasts if player is outside vision cone
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        flatForward.Normalize();

        Vector3 flatToPlayer = (m_player.position - transform.position);
        flatToPlayer.y = 0f;
        flatToPlayer.Normalize();

        float angleToPlayer = Vector3.Angle(flatForward, flatToPlayer);
        if (angleToPlayer > (m_visionAngle * 0.5f))
            return false;

        // Perform multiple raycasts towards different points on the player to be more robust
        // against partial occlusion. If any ray hits the player, we have vision.
        Vector3[] targetOffsets = new Vector3[]
        {
            Vector3.up * 1.0f,                  // chest/center
            Vector3.up * 1.75f,                 // head
            Vector3.up * 0.5f,                  // lower torso
            m_player.right * 0.3f + Vector3.up * 1.0f,  // right shoulder
            -m_player.right * 0.3f + Vector3.up * 1.0f, // left shoulder
        };

        bool canSee = false;
        foreach (var offset in targetOffsets)
        {
            Vector3 targetPoint = m_player.position + offset;
            Vector3 dir = (targetPoint - origin).normalized;

            if (m_debug)
                Debug.DrawLine(origin, targetPoint, Color.red);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, m_chaseRange))
            {
                // consider the root in case colliders are on child objects
                if (hit.transform.root == m_player)
                {
                    canSee = true;
                    break;
                }
            }
        }

        if (canSee)
        {
            // remember player for a short random duration so enemy doesn't immediately lose track
            m_memoryTimer = Random.Range(m_memoryMin, m_memoryMax);
            return true;
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
