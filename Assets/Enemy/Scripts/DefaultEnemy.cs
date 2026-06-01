using UnityEngine;

public class DefaultEnemy : Enemy
{
    protected override void Update()
    {
        base.Update();

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance <= m_attackRange)
        {
            AttackPlayer();
        }
        else if (distance <= m_chaseRange && CanSeePlayer())
        {
            ChasePlayer();
        }
        else
        {
            Idle();
        }
    }
}
