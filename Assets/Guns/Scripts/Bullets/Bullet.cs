using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] protected GameObject m_bulletVisual;

    [SerializeField] protected float m_lifetime   = 5.0f;
    [SerializeField] protected float m_speed      = 5.0f;

    [SerializeField] protected float m_damage     = 10.0f;

    private Vector3 m_direction = Vector3.zero;
    private Vector3 m_inheritedVelocity = Vector3.zero;

    // Most projectiles apply their knockback immediately. Shotgun pellets can
    // opt into a short combine window so the enemy reacts to the whole blast.
    private float m_knockbackForce = 30.0f;
    private float m_knockbackCombineWindow;

    private bool m_active = false;

    public virtual void Init(Vector3 dir, Vector3 inheritedVelocity)
    {
        m_direction = dir.normalized;
        m_inheritedVelocity = inheritedVelocity;

        m_active = true;

        // Rotate bullet to face movement direction
        transform.rotation = Quaternion.LookRotation(m_direction);

        // Set the bullets lifetime.
        Destroy(gameObject, m_lifetime);
    }

    public void ConfigureKnockback(float force, float combineWindow = 0f)
    {
        m_knockbackForce = Mathf.Max(0f, force);
        m_knockbackCombineWindow = Mathf.Max(0f, combineWindow);
    }

    private void Update()
    {
        if (GameStateManager.Instance.GetFreezeGame())
        {
            return;
        }

        BulletUpdate();
    }

    public virtual void BulletUpdate()
    {
        if (!m_active)
        {
            return;
        }

        /* Update the position of the visuals to make shooting feel more natual.
         * We only want to update the visuals with the velocity of the player so it
         * looks like it is shooting out of the barrel.
         * Leave m_bulletVisual as null if you don't want this gun to be affected visually by velocity. */
 /*       if (m_bulletVisual)
        {
            Vector3 velocity = ((m_direction) * m_speed) + m_inheritedVelocity;
            m_bulletVisual.transform.position += velocity * Time.deltaTime;
        }*/

        // Move the actual bullet component.
        transform.position += (m_direction) * m_speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!m_active)
        {
            return;
        }

        bool allowKill = true;

        // Dont kill yourself!!!
        if(other == this)
        {
            allowKill = false;
        }

        // Dont kill your brothers!!!
        if(other.gameObject.tag == "Bullet")
        {
            allowKill = false;
        }

        // Enemies collision is tied to the mesh (child of enemy).
        if (other.gameObject.transform != null)
        {
            if (other.gameObject.transform.TryGetComponent<Enemy>(out var enemy))
            {
                // BulletUpdate moves along -m_direction, so this is the actual
                // incoming trajectory rather than the bullet model's forward axis.
                Vector3 dir = m_direction;
                dir.y = 0f;
                dir.Normalize();

                enemy.GetHealth().SetHealthRelative(-m_damage);

                if (m_knockbackCombineWindow > 0f)
                {
                    var accumulator = enemy.GetComponent<ShotgunKnockbackAccumulator>();
                    if (accumulator == null)
                        accumulator = enemy.gameObject.AddComponent<ShotgunKnockbackAccumulator>();

                    accumulator.AddPellet(dir * m_knockbackForce, 0.5f, m_knockbackCombineWindow);
                }
                else
                {
                    enemy.ApplyKnockback(dir * m_knockbackForce, 0.5f, true, true);
                }
            }
        }

        if(allowKill)
            Destroy(gameObject);
    }
}
