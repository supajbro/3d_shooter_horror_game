public class ActionState : State
{
    private System.Action action;

    public ActionState(System.Action action)
    {
        this.action = action;
    }

    public override void OnEnter()
    {
        action?.Invoke();
        Complete();
    }
}