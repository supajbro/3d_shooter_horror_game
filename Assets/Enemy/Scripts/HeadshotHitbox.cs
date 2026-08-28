using UnityEngine;

public enum HeadshotEffect
{
    None,
    DamageMultiplier,
    InstantKill
}

/// <summary>
/// A trigger placed slightly in front of an enemy's head. It is created when an
/// enemy is activated, so it works for both prefab instances and pooled enemies.
/// </summary>
public class HeadshotHitbox : MonoBehaviour
{
    private Enemy m_enemy;

    public Enemy Enemy => m_enemy;

    public static void EnsureForEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.GetComponentInChildren<HeadshotHitbox>(true) != null)
            return;

        CapsuleCollider bodyCollider = enemy.GetComponent<CapsuleCollider>();
        if (bodyCollider == null)
            return;

        GameObject hitboxObject = new GameObject("Headshot Hitbox");
        hitboxObject.layer = enemy.gameObject.layer;
        hitboxObject.transform.SetParent(enemy.transform, false);

        // Offset toward the direction the enemy faces. This keeps the head trigger
        // in front of the body capsule, so it is the first collider hit from the front.
        float headY = bodyCollider.center.y + bodyCollider.height * 0.5f - bodyCollider.radius * 0.75f;
        hitboxObject.transform.localPosition = new Vector3(0f, headY, bodyCollider.radius * 0.5f);

        SphereCollider headCollider = hitboxObject.AddComponent<SphereCollider>();
        headCollider.isTrigger = true;
        headCollider.radius = bodyCollider.radius * 0.65f;

        HeadshotHitbox hitbox = hitboxObject.AddComponent<HeadshotHitbox>();
        hitbox.m_enemy = enemy;
    }

    public static bool TryGetHitEnemy(Collider collider, out Enemy enemy, out bool isHeadshot)
    {
        HeadshotHitbox headshotHitbox = collider.GetComponent<HeadshotHitbox>();
        if (headshotHitbox != null && headshotHitbox.m_enemy != null)
        {
            enemy = headshotHitbox.m_enemy;
            isHeadshot = true;
            return true;
        }

        enemy = collider.GetComponentInParent<Enemy>();
        isHeadshot = false;
        return enemy != null;
    }

    public static void ApplyDamage(Enemy enemy, float damage, bool isHeadshot,
        HeadshotEffect effect, float damageMultiplier)
    {
        if (enemy == null)
            return;

        EnemyHealth health = enemy.GetHealth();
        if (health == null)
            return;

        if (isHeadshot && effect == HeadshotEffect.InstantKill)
        {
            health.SetHealth(0f);
            return;
        }

        float finalDamage = damage;
        if (isHeadshot && effect == HeadshotEffect.DamageMultiplier)
            finalDamage *= Mathf.Max(0f, damageMultiplier);

        health.SetHealthRelative(-finalDamage);
    }
}
