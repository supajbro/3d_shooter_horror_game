using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using System.Collections.Generic;

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
    private bool m_enemyHit = false;

    // Ricochet / bounce configuration
    [SerializeField] private float m_ricochetRadius = 0.35f; // radius for spherecast when checking walls
    [SerializeField] private float m_ricochetBounceDamping = 0.6f; // fraction of speed kept after bounce
    [SerializeField] private float m_ricochetDrag = 5f; // how quickly horizontal velocity decays per second
    [SerializeField] private LayerMask m_ricochetLayerMask = (LayerMask)(-1); // which layers to collide with
    [SerializeField] private float m_minRicochetSpeed = 0.5f; // below this speed stop ricocheting

    // Transfer / collision tuning
    [SerializeField] private float m_transferMomentumMultiplierRicochet = 0.8f; // fraction of momentum passed in ricochet path
    [SerializeField] private float m_transferMomentumMultiplierLegacy = 0.6f; // fraction in legacy path
    [SerializeField] private float m_maxTransferSpeed = 12f; // clamp transferred speed to avoid huge forces
    [SerializeField] private float m_enemyCollisionImmunityDuration = 0.12f; // short immunity after being hit by another enemy
    [SerializeField] private float m_collisionTransferScale = 0.6f; // scale applied to transferred impact velocity (tweak to reduce force)
    [SerializeField] private float m_collisionSweepRadiusMultiplier = 1.6f; // multiplier for sweep radius to catch side grazes

    // Dynamic ricochet state
    private bool m_allowRicochet = false;
    private Vector3 m_currentVelocity; // horizontal velocity used for ricochet simulation

    // Track previous position while knocked back so we can sweep for collisions
    private Vector3 m_prevKnockbackPosition;

    private float m_enemyCollisionImmunityTimer = 0f;

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

    // Idle behavior
    [Header("Idle")]
    [SerializeField] private float m_idleRotateSpeed = 20f; // degrees per second while idle

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

        // Rotate while idle so player can spot enemies more easily.
        // Increase `m_idleRotateSpeed` to make enemies rotate faster and be easier to spot visually.
        transform.Rotate(0f, m_idleRotateSpeed * Time.deltaTime, 0f);

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

            if (CanSeePlayer() || m_enemyHit)
                ChangeState(EnemyState.Walk);
            else
                ChangeState(EnemyState.Idle);
            m_enemyHit = true;
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
    /// Added forceFall to force the enemy to fall over regardless of incoming force.
    /// </summary>
    public void ApplyKnockback(Vector3 velocity, float duration, bool allowRicochet = false, 
        bool forceFall = false, bool allowFall = true)
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
        if (forceFall)
        {
            m_hasFallen = true;
        }
        else if (incomingForce >= 12f)
        {
            m_hasFallen = true; // e.g. revolver/bullet
        }
        else
        {
            // Slightly bias fall chance by force so stronger punches feel more satisfying.
            float biasedChance = Mathf.Clamp01(m_fallOverChance + (incomingForce - 3f) * 0.05f);
            m_hasFallen = Random.value <= biasedChance;
        }

        // Override the fall state to false if this knockback can't allow falling.
        // This is useful for knockbacks that should not cause a fall, such as certain melee attacks or environmental effects.
        if (!allowFall)
        {
            m_hasFallen = false;
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

        // set prev-knockback position for movement sweep detection
        m_prevKnockbackPosition = transform.position;

        // Choose the reaction when the hit occurs. Stun and Fallen are now parallel
        // reactions, and both transition to Recover when their own duration ends.
        ChangeState(m_hasFallen ? EnemyState.Fallen : EnemyState.Stun);

        if (m_hasFallen && m_knockbackForce >= 12f)
        {
            // Give big hits a longer floor time
            m_stateTimer = Mathf.Max(m_stateTimer, 3.0f);
        }

        m_isKnockedBack = true;

        // Immediately mark player as spotted and remember their position so the
        // enemy begins pursuing the player when hit by a bullet or the player
        // dashes/slides into them.
        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                m_player = playerObj.transform;
                m_playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        // Set memory so enemy does not immediately forget the player.
        m_memoryTimer = m_memoryMax;

        m_enemyHit = true;
    }

    private void HandleKnockback()
    {
        // decrement collision immunity timer so enemies can be hit again shortly after
        if (m_enemyCollisionImmunityTimer > 0f)
            m_enemyCollisionImmunityTimer = Mathf.Max(0f, m_enemyCollisionImmunityTimer - Time.deltaTime);

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

        // Compute intended horizontal movement for this frame
        Vector3 horizontalMove;
        if (m_allowRicochet && m_currentVelocity.sqrMagnitude > 0.0001f)
        {
            horizontalMove = m_currentVelocity * Time.deltaTime;
        }
        else
        {
            float curve = Mathf.Pow(1f - t, 2f); // stronger initial push
            float horizontalMultiplier = Mathf.Lerp(5f, 20f, Mathf.Clamp01(m_knockbackForce / 15f));
            horizontalMove = m_knockbackVelocity * horizontalMultiplier * curve * Time.deltaTime;
        }

        bool transferred = false;

        float moveDist = horizontalMove.magnitude;
        Vector3 moveDir = moveDist > 0f ? horizontalMove / moveDist : Vector3.zero;

        // Sweep for collisions along the movement path so we detect hits reliably
        if (moveDist > 0.0001f)
        {
            float sweepRadius = m_ricochetRadius * m_collisionSweepRadiusMultiplier;
            Vector3 sweepOrigin = m_prevKnockbackPosition + Vector3.up * 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(sweepOrigin, sweepRadius, moveDir, moveDist + 0.01f, m_ricochetLayerMask.value, QueryTriggerInteraction.Ignore);

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (hit.collider == null)
                        continue;

                    // ignore self
                    if (hit.collider.transform.root == transform)
                        continue;

                    Enemy otherEnemy = hit.collider.GetComponentInParent<Enemy>();
                    if (otherEnemy == null || otherEnemy == this)
                        continue;

                    // don't transfer if the other enemy was just hit
                    if (otherEnemy.m_enemyCollisionImmunityTimer > 0f)
                        continue;

                    // Compute impact velocity based on actual movement this frame (world units/sec)
                    float impactSpeed = moveDist / Mathf.Max(Time.deltaTime, 1e-6f);
                    Vector3 impactVel = (moveDir.sqrMagnitude > 0.0001f ? moveDir : (otherEnemy.transform.position - transform.position).normalized) * impactSpeed;
                    impactVel.y = Mathf.Clamp(m_knockbackVerticalStrength * 0.5f, 0.2f, 3f);

                    // Start a brand-new ApplyKnockback on the other enemy — treat it as if shot
                    otherEnemy.ApplyKnockback(impactVel * m_collisionTransferScale, Mathf.Max(0.35f, m_knockbackDuration * 0.5f), m_allowRicochet, true);

                    // Give the other enemy a short immunity to further transfers
                    otherEnemy.m_enemyCollisionImmunityTimer = m_enemyCollisionImmunityDuration;

                    // Resolve overlap minimally by nudging the other enemy along hit normal
                    Vector3 otherPos = otherEnemy.transform.position + hit.normal * (sweepRadius + 0.01f);
                    otherPos.y = otherEnemy.m_knockbackStartY + Mathf.Min(otherEnemy.m_knockbackVerticalStrength, 0.5f);
                    otherEnemy.transform.position = otherPos;
                    if (otherEnemy.m_agent != null)
                        otherEnemy.m_agent.Warp(otherPos);

                    // Stop our horizontal motion after hitting another enemy so we don't keep pushing
                    if (m_allowRicochet)
                        m_currentVelocity = Vector3.zero;
                    m_allowRicochet = false;

                    // Place ourselves at the impact point (slightly offset) and update agent
                    Vector3 impactPoint = hit.point + hit.normal * (sweepRadius + 0.01f);
                    Vector3 selfPos = transform.position;
                    selfPos.x = impactPoint.x;
                    selfPos.z = impactPoint.z;
                    selfPos.y = m_knockbackStartY + height;
                    transform.position = selfPos;
                    if (m_agent != null)
                        m_agent.Warp(selfPos);

                    // Update previous position for next sweep
                    m_prevKnockbackPosition = transform.position;

                    transferred = true;
                    break; // only transfer once per frame
                }
            }
            else
            {
                // No spherecast hits: fallback to an overlap check at our current position
                Collider[] overlaps = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, sweepRadius, m_ricochetLayerMask.value, QueryTriggerInteraction.Ignore);
                if (overlaps != null && overlaps.Length > 0)
                {
                    // sort overlaps by distance to sweep origin to pick nearest
                    var ordered = overlaps
                        .Where(c => c != null && c.transform.root != transform)
                        .Select(c => new { col = c, dist = Vector3.Distance(sweepOrigin, c.ClosestPoint(sweepOrigin)) })
                        .OrderBy(x => x.dist)
                        .ToArray();

                    foreach (var entry in ordered)
                    {
                        Collider col = entry.col;
                        if (col == null) continue;

                        Enemy otherEnemy = col.GetComponentInParent<Enemy>();
                        if (otherEnemy == null || otherEnemy == this) continue;
                        if (otherEnemy.m_enemyCollisionImmunityTimer > 0f) continue;

                        // Build a pseudo-normal and impact direction from centers
                        Vector3 awayDir = (otherEnemy.transform.position - transform.position);
                        if (awayDir.sqrMagnitude <= 0.0001f)
                            awayDir = moveDir.sqrMagnitude > 0.0001f ? moveDir : transform.forward;
                        awayDir.Normalize();

                        float impactSpeed = moveDist / Mathf.Max(Time.deltaTime, 1e-6f);
                        Vector3 impactVel = awayDir * impactSpeed;
                        impactVel.y = Mathf.Clamp(m_knockbackVerticalStrength * 0.5f, 0.2f, 3f);

                        otherEnemy.ApplyKnockback(impactVel * m_collisionTransferScale, Mathf.Max(0.35f, m_knockbackDuration * 0.5f), m_allowRicochet, true);
                        otherEnemy.m_enemyCollisionImmunityTimer = m_enemyCollisionImmunityDuration;

                        Vector3 otherPos = otherEnemy.transform.position + awayDir * (sweepRadius + 0.01f);
                        otherPos.y = otherEnemy.m_knockbackStartY + Mathf.Min(otherEnemy.m_knockbackVerticalStrength, 0.5f);
                        otherEnemy.transform.position = otherPos;
                        if (otherEnemy.m_agent != null)
                            otherEnemy.m_agent.Warp(otherPos);

                        // Stop our movement
                        if (m_allowRicochet)
                            m_currentVelocity = Vector3.zero;
                        m_allowRicochet = false;

                        // nudge self
                        Vector3 selfPos = transform.position + -awayDir * (sweepRadius * 0.5f);
                        selfPos.y = m_knockbackStartY + height;
                        transform.position = selfPos;
                        if (m_agent != null)
                            m_agent.Warp(selfPos);

                        m_prevKnockbackPosition = transform.position;
                        transferred = true;
                        break;
                    }
                }
            }
        }

        if (!transferred)
        {
            // No enemy hit — perform the movement and update previous position
            Vector3 pos = transform.position + horizontalMove;
            pos.y = m_knockbackStartY + height;
            transform.position = pos;
            m_prevKnockbackPosition = transform.position;

            // apply drag for ricochet velocity
            if (m_allowRicochet)
                m_currentVelocity = Vector3.Lerp(m_currentVelocity, Vector3.zero, Time.deltaTime * m_ricochetDrag);
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
