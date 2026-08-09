using UnityEngine;

public class ChargerEnemy : Enemy
{
    public override void Activate(EnemySpawner enemySpawner)
    {
        base.Activate(enemySpawner);
        ChangeState(EnemyState.Idle);
    }

    protected override void UpdateWalk()
    {
        if (!CanSeePlayer() && m_memoryTimer <= 0.0f)
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