using UnityEngine;

public abstract class BaseGunController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected Transform  m_firePoint;         // <- Where bullets spawn
    [SerializeField] protected Bullet     m_bulletPrefab;

    [Header("Gun Settings")]
    [SerializeField] protected float m_fireRate = 5f;          // <- Bullets per second

    [Header("Debug")]
    [SerializeField] protected bool  m_debugDraw    = true;
    [SerializeField] protected float m_debugRange   = 100f;

    protected float m_nextTimeToFire = 0f;

    protected virtual void Update()
    {
        HandleInput();
        DrawDebug();
    }

    protected virtual void HandleInput()
    {
        if (CanFire() && IsFiring())
        {
            Shoot();
            m_nextTimeToFire = Time.time + (1f / m_fireRate);
        }
    }

    protected virtual bool IsFiring()
    {
        // Default: left mouse
        return Input.GetMouseButton(0);
    }

    protected virtual bool CanFire()
    {
        return Time.time >= m_nextTimeToFire;
    }

    protected virtual void Shoot()
    {
        if (m_bulletPrefab == null || m_firePoint == null)
        {
            Debug.LogWarning("Missing bulletPrefab or firePoint");
            return;
        }

        // Instantiate bullet
        Bullet bullet = Instantiate(m_bulletPrefab, m_firePoint.position, Quaternion.identity);

        // Direction = player forward (not firePoint forward)
        Vector3 direction = GetShootDirection();
        bullet.Init(direction);

        // Optional hook for child classes
        OnShoot(bullet, direction);
    }

    protected virtual Vector3 GetShootDirection()
    {
        return transform.forward;
    }

    protected virtual void OnShoot(Bullet bullet, Vector3 direction)
    {
        // For subclasses (spread, recoil, etc.)
    }

    protected virtual void DrawDebug()
    {
        if (!m_debugDraw || m_firePoint == null) return;

        Vector3 direction = GetShootDirection();

        Debug.DrawRay(m_firePoint.position, direction * m_debugRange, Color.red);
    }
}