using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float m_explosionRadius = 2f;
    [SerializeField] private float m_maxRadius = 6f;
    [SerializeField] private float m_expandSpeed = 10f;

    [Header("Damage Settings")]
    [SerializeField] private float m_explosionDamage = 50f;
    [SerializeField] private float m_damageInterval = 0.5f;

    [Header("Visuals")]
    [SerializeField] private Transform m_visual;

    [SerializeField] private LayerMask m_hitLayers;

    private HashSet<EnemyAI> m_enemiesInRange = new HashSet<EnemyAI>();

    public void StartExplosion()
    {
        if(m_visual == null)
        {
            Debug.LogError("Missing reference to the explosions mesh.");
            return;
        }

        StartCoroutine(ExplosionRoutine());
        StartCoroutine(DamageRoutine());
    }

    private IEnumerator ExplosionRoutine()
    {
        float currentRadius = m_explosionRadius;

        while (currentRadius < m_maxRadius)
        {
            float diameter = currentRadius * 2f;
            m_visual.localScale = new Vector3(diameter, diameter, diameter);

            Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius, m_hitLayers);

            foreach (var hit in hits)
            {
                if (hit.transform.parent != null)
                {
                    if (hit.transform.parent.TryGetComponent<EnemyAI>(out var enemy))
                    {
                        m_enemiesInRange.Add(enemy);
                    }
                }
            }

            currentRadius += m_expandSpeed * Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator DamageRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(m_damageInterval);

            foreach (var enemy in m_enemiesInRange)
            {
                if (enemy != null)
                {
                    enemy.GetHealth().SetHealthRelative(-m_explosionDamage);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_maxRadius);
    }
}
