using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Turns a prefab into a one-hit physics projectile. It is intentionally added
/// at runtime so the placeholder enemy prefab does not need to be edited.
/// </summary>
public class ThrowableWeapon : MonoBehaviour
{
    [SerializeField, Min(0f)] private float m_throwSpeed = 18f;
    [SerializeField, Min(0f)] private float m_knockbackForce = 24f;
    [SerializeField, Min(0f)] private float m_knockbackDuration = 0.5f;
    [SerializeField, Min(0f)] private float m_lifetime = 4f;

    private Vector3 m_travelDirection;
    private Rigidbody m_rigidbody;
    private bool m_hasHit;

    public void Init(Vector3 direction)
    {
        m_travelDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : transform.forward;

        DisableEnemyBehaviour();

        m_rigidbody = GetComponent<Rigidbody>();
        if (m_rigidbody == null)
            m_rigidbody = gameObject.AddComponent<Rigidbody>();

        m_rigidbody.isKinematic = false;
        m_rigidbody.useGravity = false;
        m_rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_rigidbody.linearVelocity = m_travelDirection * m_throwSpeed;
        transform.rotation = Quaternion.LookRotation(m_travelDirection);

        Destroy(gameObject, m_lifetime);
    }

    private void DisableEnemyBehaviour()
    {
        foreach (Enemy enemy in GetComponentsInChildren<Enemy>())
            enemy.enabled = false;

        foreach (NavMeshAgent agent in GetComponentsInChildren<NavMeshAgent>())
            agent.enabled = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (m_hasHit)
            return;

        if (HeadshotHitbox.TryGetHitEnemy(collision.collider, out Enemy enemy, out _))
        {
            m_hasHit = true;
            enemy.ApplyKnockback(
                m_travelDirection * m_knockbackForce,
                m_knockbackDuration,
                allowRicochet: true,
                forceFall: true
            );
        }

        // The throwable is single-use, regardless of whether it struck an enemy or the level.
        Destroy(gameObject);
    }
}
