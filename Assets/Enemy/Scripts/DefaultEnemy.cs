using UnityEngine;

public class DefaultEnemy : Enemy
{
    protected override void UpdateWalk()
    {
        if (!CanSeePlayer())
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        m_agent.SetDestination(m_player.position);
        FaceTarget();

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance <= m_attackRange)
        {
            StartAttack();
        }
    }
}