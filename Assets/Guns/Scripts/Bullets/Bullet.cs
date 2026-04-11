using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] protected float m_lifetime   = 5.0f;
    [SerializeField] protected float m_speed      = 5.0f;

    [SerializeField] protected float m_damage     = 10.0f;

    private Vector3 m_direction = Vector3.zero;
    private bool m_active = false;

    public virtual void Init(Vector3 dir)
    {
        m_direction = dir.normalized;
        m_active = true;

        // Rotate bullet to face movement direction
        transform.rotation = Quaternion.LookRotation(m_direction);

        // Set the bullets lifetime.
        Destroy(gameObject, m_lifetime);
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
        transform.position += -(m_direction) * m_speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!m_active)
        {
            return;
        }

        // Dont kill yourself!!!
        if(other == this)
        {
            return;
        }

        // Dont kill your brothers!!!
        if(other.gameObject.tag == "Bullet")
        {
            return;
        }

        // Enemies collision is tied to the mesh (child of enemy).
        if (other.gameObject.transform != null)
        {
            if (other.gameObject.transform.TryGetComponent<Enemy>(out var enemy))
            {
                enemy.GetHealth().SetHealthRelative(-m_damage);
            }
        }

        Destroy(gameObject);
    }
}