using UnityEngine;
using static GameStateManager;

public class GameOverState : BaseGameState
{
    public override GameStateType StateType => GameStateType.GameOver;

    public GameOverState(GameStateManager manager) : base(manager) { }

    public override void Enter()
    {
        Debug.Log("Game Over");

        Time.timeScale = 0f;

        // Show game over UI
    }

    public void ReturnToMenu()
    {
        m_manager.SetState(new LoadingState(m_manager, "MainMenu"));
    }

    public void RestartLevel(string currentScene)
    {
        m_manager.SetState(new LoadingState(m_manager, currentScene));
    }
}