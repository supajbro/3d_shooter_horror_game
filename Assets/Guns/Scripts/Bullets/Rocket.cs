using UnityEngine;

public class Rocket : Bullet
{

    [SerializeField] private Explosion m_explosionPrefab;
    private bool m_hasExploded = false;

    public override void Init(Vector3 dir)
    {
        base.Init(dir);
    }

    protected void OnTriggerEnter(Collider other)
    {
        if (m_hasExploded) return;

        // Ignore self + other bullets
        if (other.gameObject == gameObject) return;
        if (other.CompareTag("Bullet")) return;

        Explode();
    }

    private void Explode()
    {
        if(m_explosionPrefab == null)
        {
            Debug.LogError("Missing reference to the explosion");
            return;
        }

        m_hasExploded = true;
        var explosion = Instantiate(m_explosionPrefab);
        explosion.transform.position = transform.position;
        explosion.StartExplosion();
        Destroy(gameObject); // TODO: Change this when pooling
    }
}