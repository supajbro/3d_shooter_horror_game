using UnityEngine;

public class DefaultEnemy : Enemy
{
    protected override void Update()
    {
        base.Update();

        if(m_attacking)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance <= m_attackRange)
        {
            StartAttack();
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
