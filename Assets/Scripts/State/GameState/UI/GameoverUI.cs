using UnityEngine;
using UnityEngine.UI;

public class GameoverUI : StateUI
{
    [SerializeField] private Button m_replayButton;
    [SerializeField] private Button m_quitToMenuButton;

    protected override void Start()
    {
        base.Start();

        GameStateManager.Instance.SetFreezeGame(true);
        Cursor.lockState    = CursorLockMode.None;
        Cursor.visible      = true;

        m_replayButton.onClick.AddListener(ReplayGame);
        m_quitToMenuButton.onClick.AddListener(QuitToMenu);
    }

    private void ReplayGame()
    {
        //GameStateManager.Instance.SetState(new LoadingState(GameStateManager.Instance, m_sceneName));
    }

    private void QuitToMenu()
    {
        GameStateManager.Instance.SetState(new MainMenuState(GameStateManager.Instance));
    }
}
