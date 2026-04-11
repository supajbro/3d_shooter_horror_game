using static GameStateManager;

public abstract class BaseGameState
{
    protected GameStateManager m_manager;
    public abstract GameStateType StateType { get; }

    public BaseGameState(GameStateManager manager)
    {
        m_manager = manager;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
}