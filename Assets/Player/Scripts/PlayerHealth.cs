using UnityEngine;

public class PlayerHealth : Health
{
    public override void Init()
    {
        base.Init();
        OnDied += Gameover;
    }

    private void Gameover()
    {
        GameStateManager.Instance.SetState(new GameOverState(GameStateManager.Instance));
    }
}
