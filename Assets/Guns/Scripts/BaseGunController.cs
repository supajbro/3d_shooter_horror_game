using UnityEngine;

public abstract class BaseGunController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected Transform  m_firePoint;          // <- Where bullets spawn
    [SerializeField] protected Bullet     m_bulletPrefab;

    [Header("Animation References")]
    [SerializeField] protected Animator m_reloadAnim;

    [Header("Gun Settings")]
    [SerializeField] protected float m_fireRate = 5f;           // <- Bullets per second

    [Header("Recoil")]
    [SerializeField] protected float recoilKickback = 0.1f;     // <- How far back it moves
    [SerializeField] protected float recoilSpeed    = 10f;      // <- How fast it goes back
    [SerializeField] protected float returnSpeed    = 6f;       // <- How fast it returns

    [Header("Ammo")]
    [SerializeField] protected int m_maxAmmo    = -1;           // <- Max ammo this weapon can hold
    protected int m_currentAmmo                 = 0;

    [Header("Reload")]
    [SerializeField] protected float m_reloadSpeed  = 1.0f;     // <- How long it takes to reload
    protected float m_reloadTimer                   = 0f;       // <- Runtime of how long time has elapsed since starting reload
    private bool m_startedReloading                 = false;

    private Vector3 m_initialLocalPos;
    private Vector3 m_targetLocalPos;

    [Header("Debug")]
    [SerializeField] protected bool  m_debugDraw    = true;
    [SerializeField] protected float m_debugRange   = 100f;

    protected float m_nextTimeToFire = 0f;

    public void Init()
    {
        m_initialLocalPos   = transform.localPosition;
        m_targetLocalPos    = m_initialLocalPos;
        m_currentAmmo       = m_maxAmmo;
    }

    protected virtual void Update()
    {
        HandleInput();
        DrawDebug();
        UpdateRecoil();
    }

    protected virtual void HandleInput()
    {
        if(IsReloading())
        {
            Reloading();
            return;
        }

        if (CanFire() && IsFiring())
        {
            Shoot();
            m_nextTimeToFire = Time.time + (1f / m_fireRate);
        }
    }

    #region - HELPERS -
    protected virtual bool IsFiring()
    {
        // Default: left mouse
        return Input.GetMouseButton(0);
    }

    protected virtual bool CanFire()
    {
        return (Time.time >= m_nextTimeToFire) && (m_currentAmmo > 0);
    }

    protected virtual bool IsReloading()
    {
        return m_currentAmmo <= 0;
    }
    #endregion

    #region - SHOOTING -
    protected virtual void Shoot()
    {
        if (m_bulletPrefab == null || m_firePoint == null)
        {
            Debug.LogWarning("Missing bulletPrefab or firePoint");
            return;
        }

        // Instantiate bullet
        Bullet bullet = Instantiate(m_bulletPrefab, m_firePoint.position, Quaternion.identity);

        if(bullet == null)
        {
            Debug.LogError("You are missing the reference to the bullet.");
            return;
        }

        // Direction = player forward (not firePoint forward)
        Vector3 direction = GetShootDirection();
        bullet.Init(direction);

        ApplyRecoil();

        // Optional hook for child classes
        OnShoot(bullet, direction);
    }

    protected virtual Vector3 GetShootDirection()
    {
        return transform.forward;
    }

    protected virtual void OnShoot()
    {
        m_currentAmmo--;
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
        if(m_reloadAnim == null)
        {
            Debug.LogError("No reload animation set, reload will not start.");
            return;
        }

        if(!m_startedReloading)
        {
            m_startedReloading = true;
            m_reloadAnim.SetTrigger("Reload");
        }

        m_reloadTimer += Time.deltaTime;
        if (m_reloadTimer >= m_reloadSpeed)
        {
            m_currentAmmo = m_maxAmmo;
            m_reloadTimer = 0f;
            m_startedReloading = false;
        }
    }
    #endregion

    protected virtual void DrawDebug()
    {
        if (!m_debugDraw || m_firePoint == null) return;

        Vector3 direction = GetShootDirection();

        Debug.DrawRay(m_firePoint.position, direction * m_debugRange, Color.red);
    }
}