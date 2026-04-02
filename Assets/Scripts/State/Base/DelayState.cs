public class DelayState : State
{
    private float m_duration;
    private float m_timer;

    public DelayState(float duration)
    {
        this.m_duration = duration;
    }

    public override void OnUpdate(float deltaTime)
    {
        m_timer += deltaTime;

        if (m_timer >= m_duration)
        {
            Complete();
        }
    }
}