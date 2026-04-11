using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameStateManager;

public class LoadingState : BaseGameState
{
    private string m_sceneToLoad;

    public LoadingState(GameStateManager manager, string sceneName) : base(manager)
    {
        m_sceneToLoad = sceneName;
    }

    public override GameStateType StateType => GameStateType.Loading;

    public override void Enter()
    {
        Debug.Log("Loading Started");

        Time.timeScale = 1f;

        m_manager.StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(m_sceneToLoad);

        while (!op.isDone)
        {
            yield return null;
        }

        m_manager.SetState(new GameplayState(m_manager));
    }
}