using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : StateUI
{
    [SerializeField] private Button m_playGameButton;
    [SerializeField] private string m_sceneName;

    protected override void Start()
    {
        base.Start();

        m_playGameButton.onClick.AddListener(PlayGame);
    }

    private void PlayGame()
    {
        GameStateManager.Instance.SetState(new LoadingState(GameStateManager.Instance, m_sceneName));
    }
}
