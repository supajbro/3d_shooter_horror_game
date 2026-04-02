public abstract class State
{
    public bool IsComplete { get; protected set; }

    public virtual void OnEnter() { }
    public virtual void OnUpdate(float delta) { }
    public virtual void OnExit() { }

    public virtual void Complete()
    {
        IsComplete = true;
    }
}