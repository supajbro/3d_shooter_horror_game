using StarterAssets;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static BaseGunController;

public abstract class BaseGunController : MonoBehaviour
{
    public enum GunType
    {
        AUTORIFLE,
        SHOTGUN,
        PISTOL,
        ROCKETLAUNCHER
    }
    [SerializeField] private GunType m_gunType;
    public GunType GetGunType() { return m_gunType; }

    public enum GunAnimationState
    {
        Idle,
        Shoot,
        Reload
    }
    private GunAnimationState m_state = GunAnimationState.Idle;
    private GunAnimationState m_previousState = GunAnimationState.Idle;

    public enum FireMode
    {
        Projectile,
        Hitscan
    }
    [SerializeField] private FireMode m_fireMode = FireMode.Projectile;

    [Header("References")]
    [SerializeField] protected Transform  m_firePoint;          // <- Where bullets spawn
    [SerializeField] protected Bullet     m_bulletPrefab;
    protected LevelManager m_manager;
    protected FirstPersonController m_player;
    protected PlayerCamera m_camera;

    [Header("Gun Model")]
    [SerializeField] private Transform m_gunModel;
    [SerializeField] private float m_swayAmount = 0.05f;
    [SerializeField] private float m_swaySmooth = 8f;
    private Vector3 m_modelInitialLocalPos;

    [Header("Animation References")]
    [SerializeField] protected Animator m_anim;

    [Header("Gun Settings")]
    [SerializeField] protected float m_fireRate = 5f;           // <- Bullets per second

    [Header("Recoil")]
    [SerializeField] protected float recoilKickback     = 0.1f;     // <- How far back it moves
    [SerializeField] protected float recoilSpeed        = 10f;      // <- How fast it goes back
    [SerializeField] protected float returnSpeed        = 6f;       // <- How fast it returns
    [SerializeField] protected float crosshairRecoil    = 2.0f;     // <- Recoil added to the crosshair

    [Header("Camera Shake")]
    [SerializeField] protected float m_strength = 0.03f;
    [SerializeField] protected float m_duration = 0.05f;

    [Header("Ammo")]
    [SerializeField] protected int m_maxAmmo    = -1;   // <- Max ammo this weapon can hold
    protected int m_currentAmmo                 = 0;

    [Header("Available Ammmo")]
    [SerializeField] protected bool m_inifiniteAmmo     = false;     // <- Does this gun have infinite ammo?
    [SerializeField] protected int m_availableAmmo      = 0;         // <- How much extra ammo the gun has in storage.
    [SerializeField] protected int m_maxAvailableAmmo   = 10;        // <- Maximum amount of ammo a gun can hold.

    [Header("Reload")]
    [SerializeField] protected float m_reloadSpeed  = 1.0f;     // <- How long it takes to reload
    protected float m_reloadTimer                   = 0f;       // <- Runtime of how long time has elapsed since starting reload
    private bool m_startedReloading                 = false;
    private bool m_manualReload                     = false;    // <- Player manually started a reload

    [Header("Hitscan Settings")]                                // <- Only uses this if it is a hitscan weapon
    [SerializeField] private float m_damage         = 10.0f;
    [SerializeField] private float m_hitscanRange   = 100.0f;

    [Header("Required release")]
    [SerializeField] private bool m_requiresTriggerRelease = false; // <- Does this gun need input to be released to fire again?
    private bool m_hasReleasedTrigger = true;

    [Header("Muzzle")]
    [SerializeField] private ParticleSystem m_muzzleFlash;

    [Header("Trail")]
    [SerializeField] private GameObject m_tracerPrefab;

    private Vector3 m_initialLocalPos;
    private Vector3 m_targetLocalPos;

    [Header("Debug")]
    [SerializeField] protected bool  m_debugDraw    = true;
    [SerializeField] protected float m_debugRange   = 100f;

    protected float m_nextTimeToFire = 0f;

    [Header("Input System")]
    private UnityEngine.InputSystem.PlayerInput m_playerInput;
    private InputAction m_fireAction;
    private InputAction m_reloadAction;

    public virtual void Init()
    {
        m_initialLocalPos   = transform.localPosition;
        m_targetLocalPos    = m_initialLocalPos;
        m_currentAmmo       = m_maxAmmo;
        m_availableAmmo     = m_maxAvailableAmmo;
        m_manager           = GameStateManager.Instance.GetLevelManager();
        m_player            = m_manager.GetPlayer();
        m_camera            = m_player.GetPlayerCamera();

        if(m_gunModel != null)
        {
            m_modelInitialLocalPos = m_gunModel.localPosition;
        }

        m_playerInput = m_player.GetPlayerInput();
        if (m_playerInput != null)
        {
            m_fireAction   = m_playerInput.actions["Shoot"];
            m_reloadAction = m_playerInput.actions["Reload"];
        }
    }

    protected virtual void Update()
    {
        if (GameStateManager.Instance.GetFreezeGame())
        {
            return;
        }

        HandleInput();
        DrawDebug();
        UpdateGunSway();
        UpdateRecoil();

        UpdateAnimationState();
    }

    protected virtual void HandleInput()
    {
        CheckManualReload();
        if(IsReloading())
        {
            Reloading();
            return;
        }

        if (!IsFiring())
        {
            m_hasReleasedTrigger = true;
        }

        if (CanFire() && IsFiring())
        {
            // If this gun requires release, block until released
            if (m_requiresTriggerRelease && !m_hasReleasedTrigger)
            {
                return;
            }

            Shoot();
            m_nextTimeToFire = Time.time + (1f / m_fireRate);

            // After shooting, require release again
            if (m_requiresTriggerRelease)
            {
                m_hasReleasedTrigger = false;
            }
        }
    }

    #region - STATES -

    [SerializeField] private float m_shootAnimationLength = 0.25f; // <- Animation length of the shoot
    private float m_animationTimer;

    protected void SetAnimationState(GunAnimationState state)
    {
        if (m_state == state)
            return;

        m_previousState = m_state;
        m_state = state;

        switch (m_state)
        {
            case GunAnimationState.Idle:
                IdleState();
                break;

            case GunAnimationState.Shoot:
                ShootState();
                break;

            case GunAnimationState.Reload:
                ReloadState();
                break;
        }
    }

    private void UpdateAnimationState()
    {
        if (m_state == GunAnimationState.Idle)
            return;

        m_animationTimer -= Time.deltaTime;

        if (m_animationTimer <= 0f)
        {
            SetAnimationState(GunAnimationState.Idle);
        }
    }

    protected virtual void IdleState()
    {
        m_anim.SetTrigger("Idle");
    }

    protected virtual void ShootState()
    {
        m_animationTimer = m_shootAnimationLength;
        m_anim.SetTrigger("Shoot");
    }

    protected virtual void ReloadState()
    {
        m_animationTimer = m_reloadSpeed;
        m_anim.SetTrigger("Reload");
    }
    #endregion

    #region - HELPERS -
    protected virtual bool IsFiring()
    {
        return m_fireAction != null && m_fireAction.IsPressed();
    }

    protected virtual bool CanFire()
    {
        return (Time.time >= m_nextTimeToFire) && (m_currentAmmo > 0);
    }

    protected virtual bool IsReloading()
    {
        return (m_currentAmmo <= 0) || m_manualReload;
    }
    #endregion

    #region - SHOOTING -
    protected virtual void Shoot()
    {
        switch (m_fireMode)
        {
            case FireMode.Projectile:
                ShootProjectile();
                break;

            case FireMode.Hitscan:
                ShootHitscan();
                break;
        }

        ApplyRecoil();
        OnShoot();
    }

    private void ShootProjectile()
    {
        if (m_bulletPrefab == null || m_firePoint == null)
        {
            Debug.LogWarning("Missing bulletPrefab or firePoint");
            return;
        }

        Bullet bullet = Instantiate(m_bulletPrefab, m_firePoint.position, Quaternion.identity);

        Vector3 direction = GetShootDirection();
        bullet.Init(direction, m_player.GetPlayerVelocity());

        OnShoot(bullet, direction);
    }

    private void ShootHitscan()
    {
        if (m_camera == null)
            return;

        Camera cam = m_camera.GetCamera();

        // Find target point from center of screen
        Ray cameraRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        Vector3 targetPoint;

        if (Physics.Raycast(cameraRay, out RaycastHit cameraHit, m_hitscanRange))
        {
            targetPoint = cameraHit.point;
        }
        else
        {
            targetPoint = cameraRay.origin + cameraRay.direction * m_hitscanRange;
        }

        // Now shoot FROM muzzle TO target
        Vector3 shootDirection = (targetPoint - m_firePoint.position).normalized;

        Ray muzzleRay = new Ray(m_firePoint.position, shootDirection);

        Vector3 finalHitPoint = targetPoint;

        if (Physics.Raycast(muzzleRay, out RaycastHit hit, m_hitscanRange))
        {
            finalHitPoint = hit.point;

            if (hit.transform.TryGetComponent<Enemy>(out var enemy))
            {
                enemy.GetHealth().SetHealthRelative(-m_damage);
            }
        }

        SpawnTracer(finalHitPoint, m_firePoint);

        if (m_debugDraw)
        {
            Debug.DrawLine(m_firePoint.position, finalHitPoint, Color.green, 1f);
        }
    }

    protected virtual Vector3 GetShootDirection()
    {
        if (m_camera == null)
        {
            Debug.LogWarning("Missing camera reference, falling back to forward.");
            return transform.forward;
        }

        Ray ray = m_camera.GetCamera().ViewportPointToRay(new Vector3(0.5f, 0.5f));

        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.origin + ray.direction * 1000f;
        }

        return -(targetPoint - m_firePoint.position).normalized;
    }

    protected virtual void OnShoot()
    {
        m_currentAmmo--;

        if (m_muzzleFlash == null)
        {
            Debug.LogWarning("Missing reference to muzzle flash");
        }
        else
        {
            m_muzzleFlash.Stop();
            m_muzzleFlash.Play();
        }

        // Update the ammo count UI
        m_manager.GetGameplayUI().SetAmmoText(m_currentAmmo + "/" + GetAvailableAmmo());
        SetAnimationState(GunAnimationState.Shoot);
    }

    protected virtual void OnShoot(Bullet bullet, Vector3 direction)
    {
        m_currentAmmo--;
    }
    #endregion

    #region - RECOIL -
    protected void ApplyRecoil()
    {
        // move backwards in local space
        m_targetLocalPos += Vector3.forward * recoilKickback;
        m_manager.GetGameplayUI().GetCrosshair().ExpandCrosshair(crosshairRecoil);
    }

    protected void UpdateRecoil()
    {
        // move toward recoil target quickly
        transform.localPosition = Vector3.Lerp(transform.localPosition, m_targetLocalPos, Time.deltaTime * recoilSpeed);

        // return target back to original position
        m_targetLocalPos = Vector3.Lerp(m_targetLocalPos, m_initialLocalPos, Time.deltaTime * returnSpeed);
    }
    #endregion

    #region - RELOAD -
    protected virtual void Reloading()
    {
        if(m_anim == null)
        {
            Debug.LogError("No reload animation set, reload will not start.");
            return;
        }

        // Can't reload, no available ammo.
        if(!m_inifiniteAmmo && m_availableAmmo == 0)
        {
            m_reloadTimer = 0f;
            m_startedReloading = false;
            m_manualReload = false;
            return;
        }

        if(!m_startedReloading)
        {
            m_startedReloading = true;
            SetAnimationState(GunAnimationState.Reload);
        }

        m_reloadTimer += Time.deltaTime;
        if (m_reloadTimer >= m_reloadSpeed)
        {
            // Decrease the bullets available by the ammo used
            var ammoUsed = m_maxAmmo - m_currentAmmo;

            m_currentAmmo = Mathf.Clamp(m_currentAmmo + m_availableAmmo, 0, m_maxAmmo);

            if(!m_inifiniteAmmo)
            {
                m_availableAmmo = Mathf.Clamp(m_availableAmmo - ammoUsed, 0, m_maxAvailableAmmo);
            }

            m_reloadTimer = 0f;
            m_startedReloading = false;
            m_manualReload = false;

            // Reset the ammo count UI
            m_manager.GetGameplayUI().SetAmmoText(m_currentAmmo + "/" + GetAvailableAmmo());
        }
    }

    protected virtual void CheckManualReload()
    {
        if(m_currentAmmo >= m_maxAmmo)
        {
            return;
        }

        if (m_reloadAction != null && m_reloadAction.WasPressedThisFrame())
        {
            m_manualReload = true;
        }
    }
    #endregion

    #region - TRAIL -
    private void SpawnTracer(Vector3 end, Transform muzzle)
    {
        GameObject tracer = Instantiate(m_tracerPrefab, muzzle);

        tracer.transform.localPosition = Vector3.zero;
        var pos = transform.transform.localPosition;
        pos.y += 0.1f;
        tracer.transform.localPosition = pos;

        tracer.transform.localRotation = Quaternion.identity;

        Vector3 localEnd = muzzle.InverseTransformPoint(end);

        float distance = localEnd.magnitude;
        Vector3 localDir = localEnd.normalized;

        tracer.transform.localRotation = Quaternion.LookRotation(localDir);

        Vector3 scale = tracer.transform.localScale;
        scale.z = distance;
        tracer.transform.localScale = scale;

        // Push the tracer forward by half its length
        tracer.transform.localPosition += Vector3.forward * (distance * 0.5f);

        StartCoroutine(DestroyTracer(tracer, 0.075f));
    }

    private IEnumerator DestroyTracer(GameObject tracer, float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(tracer);
    }
    #endregion

    #region - GUN SWAY -
    protected virtual void UpdateGunSway()
    {
        if (m_gunModel == null || m_player == null)
        {
            Debug.LogError("Unable to reference important objects.");
            return;
        }

        Vector3 velocity = m_player.GetPlayerVelocity();

        // Ignore vertical movement
        velocity.y = 0f;

        float speed = velocity.magnitude;

        // Safety check
        if (speed < 0.001f)
        {
            // Smooth return to rest position when idle
            m_gunModel.localPosition = Vector3.Lerp(
                m_gunModel.localPosition,
                m_modelInitialLocalPos,
                Time.deltaTime * m_swaySmooth
            );
            return;
        }

        // Get movement direction relative to player
        Vector3 localVelocity = m_player.transform.InverseTransformDirection(velocity.normalized);

        // Build sway offset (always scales with actual movement speed)
        Vector3 targetOffset = new Vector3(
            -localVelocity.x,
            0f,
            -localVelocity.z * 0.5f
        ) * m_swayAmount * speed * 0.1f;

        Vector3 targetPos = m_modelInitialLocalPos + targetOffset;

        // Smooth movement
        m_gunModel.localPosition = Vector3.Lerp(
            m_gunModel.localPosition,
            targetPos,
            Time.deltaTime * m_swaySmooth
        );
    }
    #endregion

    #region - GETTERS -
    public int GetCurrentAmmo()
    {
        return m_currentAmmo;
    }

    public int GetMaxAmmo()
    {
        return m_maxAmmo;
    }

    public int GetAvailableAmmo()
    {
        // Only show max ammo if this gun has infinite ammo.
        var value = m_inifiniteAmmo ? m_maxAmmo : m_availableAmmo;
        return value;
    }
    #endregion

    protected virtual void DrawDebug()
    {
        if (!m_debugDraw || m_firePoint == null) return;

        Vector3 direction = GetShootDirection();

        Debug.DrawRay(m_firePoint.position, direction * m_debugRange, Color.red);
    }
}