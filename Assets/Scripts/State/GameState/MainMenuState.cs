using UnityEngine;
using static GameStateManager;

public class MainMenuState : BaseGameState
{
    public override GameStateType StateType => GameStateType.MainMenu;

    private bool m_skipToGameplay;
    private string m_defaultScene;

    public MainMenuState(GameStateManager manager, bool skipToGameplay = false, string defaultScene = "Level_01")
    : base(manager)
    {
        m_skipToGameplay = skipToGameplay;
        m_defaultScene = defaultScene;
    }


    public override void Enter()
    {
        Debug.Log("Entered Main Menu");

        Time.timeScale = 1f;

        if (m_skipToGameplay)
        {
            Debug.Log("Skipping Main Menu for testing");
            m_manager.SetState(new LoadingState(m_manager, m_defaultScene));
            return;
        }
    }

    public void StartGame(string sceneName)
    {
        m_manager.SetState(new LoadingState(m_manager, sceneName));
    }
}