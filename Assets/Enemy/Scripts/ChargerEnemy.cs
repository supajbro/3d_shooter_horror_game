using UnityEngine;
using UnityEngine.AI;

public class ChargerEnemy : Enemy
{
    [Header("Charge Settings")]
    [SerializeField] private float m_windupTime = 0.8f;
    [SerializeField] private float m_chargeDuration = 0.6f;
    [SerializeField] private float m_cooldown = 2f;

    private enum State
    {
        Windup,
        Charging,
    }

    private State m_state;
    private float m_stateTime;

    public override void Activate(EnemySpawner enemySpawner)
    {
        base.Activate(enemySpawner);
        m_state = State.Windup;
    }

    protected override void Update()
    {
        base.Update();

        float distance = Vector3.Distance(transform.position, m_player.position);

        switch (m_state)
        {
            case State.Windup:
                HandleWindup();
                break;

            case State.Charging:
                HandleCharge();
                break;
        }
    }

    private void HandleWindup()
    {
        FaceTarget();

        m_stateTime += Time.deltaTime;

        if (m_stateTime >= m_windupTime)
        {
            BeginCharge();
        }
    }

    private void BeginCharge()
    {
        m_state = State.Charging;
        m_stateTime = 0f;

        m_agent.SetDestination(m_player.position);

        m_anim?.SetTrigger("Charge");
    }

    private void HandleCharge()
    {
        m_stateTime += Time.deltaTime;

        FaceTarget();

        m_agent.SetDestination(m_player.position);
    }
}