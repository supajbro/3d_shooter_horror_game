using UnityEngine;
using static GameStateManager;

public class PauseState : BaseGameState
{
    public PauseState(GameStateManager manager) : base(manager) { }

    public override GameStateType StateType => GameStateType.Pause;

    public override void Enter()
    {
        Debug.Log("Game Paused");
        GameStateManager.Instance.SetFreezeGame(true);
    }

    public override void Exit()
    {
        GameStateManager.Instance.SetFreezeGame(false);
    }

    public override void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            m_manager.SetState(new GameplayState(m_manager));
        }
    }
}