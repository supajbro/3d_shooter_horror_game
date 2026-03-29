using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float m_lifetime   = 5.0f;
    [SerializeField] private float m_speed      = 5.0f;

    private Vector3 m_direction = Vector3.zero;
    private bool m_active = false;

    private void Start()
    {
        Destroy(gameObject, m_lifetime);
    }

    private void Update()
    {
        BulletUpdate();
    }

    public void BulletUpdate()
    {
/*        if(!m_active)
        {
            return;
        }*/
        transform.position += -(m_direction) * m_speed * Time.deltaTime;
    }

    public void Init(Vector3 dir)
    {
        m_direction = dir;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}