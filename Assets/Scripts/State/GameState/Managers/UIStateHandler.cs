using UnityEngine;

public class UIStateHandler : MonoBehaviour
{
    public static UIStateHandler Instance;

    [SerializeField] private MainMenuUI m_mainMenuUIPrefab;
    [SerializeField] private GameObject m_pauseUIPrefab;
    [SerializeField] private GameplayUI m_gameplayUIPrefab;
    [SerializeField] private GameObject m_gameOverUIPrefab;
    [SerializeField] private GameObject m_loadingUIPrefab;

    public MainMenuUI m_mainMenuUI;
    public GameObject m_pauseUI;
    public GameplayUI m_gameplayUI;
    public GameObject m_gameOverUI;
    public GameObject m_loadingUI;

    private bool m_initialised = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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

        // Child to UIStateHandler so they persist through scenes.
        m_mainMenuUI    = Instantiate(m_mainMenuUIPrefab,   transform);
        m_pauseUI       = Instantiate(m_pauseUIPrefab,      transform);
        m_gameplayUI    = Instantiate(m_gameplayUIPrefab,   transform);
        m_gameOverUI    = Instantiate(m_gameOverUIPrefab,   transform);
        m_loadingUI     = Instantiate(m_loadingUIPrefab,    transform);

        HandleStateChanged(GameStateManager.Instance.GetCurrentState());
        GameStateManager.Instance.OnStateChanged += HandleStateChanged;

        m_initialised = true;
    }

    private void HandleStateChanged(BaseGameState state)
    {
        m_mainMenuUI.gameObject.SetActive(state is MainMenuState);
        m_pauseUI.SetActive(state is PauseState);
        m_gameplayUI.gameObject.SetActive(state is GameplayState);
        m_gameOverUI.SetActive(state is GameOverState);
        m_loadingUI.SetActive(state is LoadingState);
    }
}