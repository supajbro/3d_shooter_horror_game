using System.Collections;
using UnityEngine;

/// <summary>
/// Combines pellets from one shotgun blast into a single knockback reaction.
/// </summary>
public class ShotgunKnockbackAccumulator : MonoBehaviour
{
    private Enemy m_enemy;
    private Vector3 m_combinedVelocity;
    private float m_duration;
    private Coroutine m_combineRoutine;

    private void Awake()
    {
        m_enemy = GetComponent<Enemy>();
    }

    public void AddPellet(Vector3 velocity, float duration, float combineWindow)
    {
        m_combinedVelocity += velocity;
        m_duration = Mathf.Max(m_duration, duration);

        if (m_combineRoutine == null)
            m_combineRoutine = StartCoroutine(ApplyCombinedKnockback(combineWindow));
    }

    private IEnumerator ApplyCombinedKnockback(float combineWindow)
    {
        yield return new WaitForSeconds(combineWindow);

        Vector3 velocity = m_combinedVelocity;
        float duration = m_duration;
        m_combinedVelocity = Vector3.zero;
        m_duration = 0f;
        m_combineRoutine = null;

        if (m_enemy != null && velocity.sqrMagnitude > 0.0001f)
        {
            // A single 5-force pellet is only a small shove. Several pellets
            // naturally add together and cross Enemy's existing fall threshold.
            m_enemy.ApplyKnockback(velocity, duration, allowRicochet: true);
        }
    }

    private void OnDisable()
    {
        if (m_combineRoutine != null)
            StopCoroutine(m_combineRoutine);

        m_combineRoutine = null;
        m_combinedVelocity = Vector3.zero;
        m_duration = 0f;
    }
}
