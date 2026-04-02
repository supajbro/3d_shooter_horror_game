using UnityEngine;

public class TestGameStart : MonoBehaviour
{

    StateManager m_state = new StateManager();
    [SerializeField] private GameObject m_3dCube;

    private void Start()
    {
        m_state.Enqueue(new ActionState(() => ShowCube()));
        m_state.Enqueue(new DelayState(1.5f));
        m_state.Enqueue(new ActionState(() => HideCube()));
    }

    private void Update()
    {
        m_state.Update(Time.deltaTime);
    }

    private void ShowCube()
    {
        m_3dCube.SetActive(true);
    }

    private void HideCube()
    {
        m_3dCube.SetActive(false);
    }

}
