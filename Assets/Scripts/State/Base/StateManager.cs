using System.Collections.Generic;

public class StateManager
{
    private State m_currentState;
    private Queue<State> m_queue = new Queue<State>();

    public bool IsRunning => m_currentState != null || m_queue.Count > 0;

    public void Update(float delta)
    {
        if (m_currentState == null)
        {
            TryNextState();
            return;
        }

        m_currentState.OnUpdate(delta);

        if (m_currentState.IsComplete)
        {
            m_currentState.OnExit();
            m_currentState = null;
        }
    }

    public void Enqueue(State state)
    {
        m_queue.Enqueue(state);
    }

    public void ClearQueue()
    {
        m_queue.Clear();
    }

    private void TryNextState()
    {
        if (m_queue.Count == 0)
        {
            return;
        }

        m_currentState = m_queue.Dequeue();
        m_currentState.OnEnter();
    }
}