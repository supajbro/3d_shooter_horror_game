using UnityEngine;
using static GameStateManager;

public class GameplayState : BaseGameState
{
    public override GameStateType StateType => GameStateType.Gameplay;

    public GameplayState(GameStateManager manager) : base(manager) { }

    public override void Enter()
    {
        Debug.Log("Gameplay Started");

        Time.timeScale = 1f;

        // Enable player input, spawners, etc.
    }

    public override void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            m_manager.SetState(new PauseState(m_manager));
        }
    }

    public void GameOver()
    {
        m_manager.SetState(new GameOverState(m_manager));
    }
}