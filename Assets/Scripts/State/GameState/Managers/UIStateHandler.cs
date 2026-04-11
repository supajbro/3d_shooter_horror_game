using UnityEngine;

public class UIStateHandler : MonoBehaviour
{
    [SerializeField] private GameObject m_mainMenuUIPrefab;
    [SerializeField] private GameObject m_pauseUIPrefab;
    [SerializeField] private GameObject m_gameplayUIPrefab;
    [SerializeField] private GameObject m_gameOverUIPrefab;
    [SerializeField] private GameObject m_loadingUIPrefab;

    private GameObject m_mainMenuUI;
    private GameObject m_pauseUI;
    private GameObject m_gameplayUI;
    private GameObject m_gameOverUI;
    private GameObject m_loadingUI;

    private bool m_initialised = false;

    private void Start()
    {
        if (m_initialised)
        {
            return;
        }

        if (m_mainMenuUIPrefab == null || m_pauseUIPrefab == null || m_gameplayUIPrefab == null || m_gameOverUIPrefab == null || m_loadingUIPrefab == null)
        {
            Debug.LogError("Missing UI references.");
            return;
        }

        m_mainMenuUI    = Instantiate(m_mainMenuUIPrefab);
        m_pauseUI       = Instantiate(m_pauseUIPrefab);
        m_gameplayUI    = Instantiate(m_gameplayUIPrefab);
        m_gameOverUI    = Instantiate(m_gameOverUIPrefab);
        m_loadingUI     = Instantiate(m_loadingUIPrefab);

        // Make all UI persistent too
        DontDestroyOnLoad(m_mainMenuUI);
        DontDestroyOnLoad(m_pauseUI);
        DontDestroyOnLoad(m_gameplayUI);
        DontDestroyOnLoad(m_gameOverUI);
        DontDestroyOnLoad(m_loadingUI);

        GameStateManager.Instance.OnStateChanged += HandleStateChanged;

        m_initialised = true;
    }

    private void HandleStateChanged(BaseGameState state)
    {
        m_mainMenuUI.SetActive(state is MainMenuState);
        m_pauseUI.SetActive(state is PauseState);
        m_gameplayUI.SetActive(state is GameplayState);
        m_gameOverUI.SetActive(state is GameOverState);
        m_loadingUI.SetActive(state is LoadingState);
    }
}