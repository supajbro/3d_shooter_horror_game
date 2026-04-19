using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] protected float m_health       = 0.0f;
    [SerializeField] protected float m_maxHealth    = 100.0f;
    protected bool m_bIsDead                        = false;

    public Action OnDied;
    public Action<float> OnHealthChanged;

    public float GetHealth() { return m_health; }
    public float GetMaxHealth() { return m_maxHealth; }

    public virtual void Init()
    {
        m_health = m_maxHealth;
    }

    /// <summary>
    /// Overwrites what the value of health is.
    /// </summary>
    /// <param name="newValue">New value</param>
    public virtual void SetHealth(float newValue)
    {
        UpdateHealth(m_health = newValue);
    }

    /// <summary>
    /// Adds or subtracts the current health.
    /// </summary>
    /// <param name="changedValue">The value that modifies health</param>
    public virtual void SetHealthRelative(float changedValue)
    {
        UpdateHealth(m_health + changedValue);
    }

    protected virtual void UpdateHealth(float newValue)
    {
        if (m_bIsDead)
        {
            return;
        }

        m_health = Mathf.Clamp(newValue, 0f, m_maxHealth);

        OnHealthChanged?.Invoke(m_health);

        if (m_health <= 0f)
        {
            Die();
        }
    }

    public virtual void Die()
    {
        if (m_bIsDead)
        {
            return;
        }

        m_bIsDead = true;

        OnDied?.Invoke();
        OnDeath();
    }

    protected virtual void OnDeath()
    {
        // For inheritance (enemy drops loot, player triggers UI, etc.)
    }
}
