using UnityEngine;

public class Shotgun : BaseGunController
{
    [SerializeField] private int pelletCount = 6;
    [SerializeField] private float spreadAngle = 10f;

    protected override void Shoot()
    {
        for (int i = 0; i < pelletCount; i++)
        {
            var bullet = Instantiate(m_bulletPrefab, m_firePoint.position, Quaternion.identity);

            if (bullet == null)
            {
                Debug.LogError("You are missing the reference to the bullet.");
                return;
            }

            Vector3 direction = GetSpreadDirection();
            bullet.Init(direction);

            ApplyRecoil();
            OnShoot(bullet, direction);
        }

        m_nextTimeToFire = Time.time + (1f / m_fireRate);
    }

    private Vector3 GetSpreadDirection()
    {
        Vector3 baseDir = transform.forward;

        float randomX = Random.Range(-spreadAngle, spreadAngle);
        float randomY = Random.Range(-spreadAngle, spreadAngle);

        Quaternion spread = Quaternion.Euler(randomX, randomY, 0);
        return spread * baseDir;
    }
}