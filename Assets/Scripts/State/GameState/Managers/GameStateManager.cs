using UnityEngine;
using UnityEngine.EventSystems;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance;

    public enum GameStateType
    {
        MainMenu,
        Loading,
        Gameplay,
        Pause,
        GameOver
    }

    private BaseGameState m_currentState;
    private BaseGameState m_initialState;

    public System.Action<BaseGameState> OnStateChanged;

    // Check to see if we have frozen the game (i.e. have we paused the game and need to stop the player/enemies moving).
    private bool m_freezeGame = false;
    public void SetFreezeGame(bool val) { m_freezeGame = val; }
    public bool GetFreezeGame() { return m_freezeGame; }

    [Header("Event System")]
    [SerializeField] private EventSystem m_eventSystemPrefab;
    private EventSystem m_eventSystem;

    [Header("Debug")]
    [SerializeField] private DebugManager m_debugManagerPrefab;
    private DebugManager m_debugManager;

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
        Init();
    }

    public void Init()
    {
        m_initialState = m_initialState == null ? new MainMenuState(this) : m_initialState;
        SetState(m_initialState);

        m_eventSystem = Instantiate(m_eventSystemPrefab);

        if(m_debugManager == null)
        {
            m_debugManager = Instantiate(m_debugManagerPrefab);
            m_debugManager.Init(this);
        }
    }

    private void Update()
    {
        m_currentState?.Update();
    }

    public void SetState(BaseGameState newState)
    {
        m_currentState?.Exit();

        m_currentState = newState;

        m_currentState.Enter();

        OnStateChanged?.Invoke(m_currentState);
    }

    public GameStateType GetCurrentState()
    {
        if (m_currentState == null)
        {
            Debug.LogError("Missing reference to current state");
            return default;
        }
        return m_currentState.StateType;
    }

    // Use this if we need to initially load into something other than main menu.
    public void SetInitialState(BaseGameState state)
    {
        m_initialState = state;
    }

    public EventSystem GetEventSystem()
    {
        if(m_eventSystem == null)
        {
            Debug.LogError("Missing reference to event system");
            return null;
        }
        return m_eventSystem;
    }
}