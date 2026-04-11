using UnityEngine;

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

    public System.Action<BaseGameState> OnStateChanged;

    // Check to see if we have frozen the game (i.e. have we paused the game and need to stop the player/enemies moving).
    private bool m_freezeGame = false;
    public void SetFreezeGame(bool val) { m_freezeGame = val; }
    public bool GetFreezeGame() { return m_freezeGame; }

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
        SetState(new MainMenuState(this));
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
}