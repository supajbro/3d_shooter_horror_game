using UnityEngine;
using UnityEngine.AI;

public class ChargerEnemy : Enemy
{
    protected override void Update()
    {
        base.Update();

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance <= m_attackRange)
        {
            AttackPlayer();
        }
        else
        {
            ChasePlayer();
        }
    }
}