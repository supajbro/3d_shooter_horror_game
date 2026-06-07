using StarterAssets;
using System.Collections;
using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float m_health = 25.0f;

    [Header("Pop Up")]
    [SerializeField] private float m_popHeight = 1.5f;
    [SerializeField] private float m_popDuration = 0.2f;

    [Header("Bounce")]
    [SerializeField] private float m_bounceHeight = 0.5f;
    [SerializeField] private float m_bounceDuration = 0.35f;
    [SerializeField] private int m_bounceCount = 2;

    [Header("Idle Hover")]
    [SerializeField] private float m_hoverAmplitude = 0.25f;
    [SerializeField] private float m_hoverFrequency = 2f;

    private Vector3 m_startPos;
    private bool m_isHovering;
    private float m_hoverTime;

    public void Activate()
    {
        m_startPos = transform.position;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // POP UP
        Vector3 targetUp = m_startPos + Vector3.up * m_popHeight;
        yield return MoveTo(m_startPos, targetUp, m_popDuration);

        // BOUNCE DOWN
        Vector3 groundPos = m_startPos;
        yield return Bounce(groundPos);

        // START HOVER
        m_isHovering = true;
    }

    private IEnumerator MoveTo(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = t / duration;

            transform.position = Vector3.Lerp(from, to, lerp);
            yield return null;
        }

        transform.position = to;
    }

    private IEnumerator Bounce(Vector3 groundPos)
    {
        Vector3 pos = transform.position;

        for (int i = 0; i < m_bounceCount; i++)
        {
            Vector3 peak = groundPos + Vector3.up * m_bounceHeight;

            yield return MoveTo(pos, peak, m_bounceDuration * 0.5f);
            yield return MoveTo(peak, groundPos, m_bounceDuration * 0.5f);

            // reduce bounce height each time
            m_bounceHeight *= 0.5f;
            pos = groundPos;
        }
    }

    private void Update()
    {
        if (!m_isHovering) return;

        m_hoverTime += Time.deltaTime;

        Vector3 pos = m_startPos;
        pos.y += Mathf.Sin(m_hoverTime * m_hoverFrequency) * m_hoverAmplitude;

        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            var player = other.GetComponent<FirstPersonController>();

            if(player == null || player.GetHealth() == null)
            {
                Debug.LogError("Unable to find reference to players health. Unable to increase health.");
                return;
            }

            var health          = player.GetHealth();
            var currentHealth   = health.GetHealth();
            var maxHealth       = health.GetMaxHealth();

            // We already have max health - don't modify health or destroy this health pickup.
            if(currentHealth >= maxHealth)
            {
                return;
            }

            health.SetHealthRelative(m_health);

            // TODO: Pool this
            Destroy(gameObject);
        }
    }
}