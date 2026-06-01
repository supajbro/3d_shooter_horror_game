using UnityEngine;

public class RainFollow : MonoBehaviour
{
    private LevelManager m_manager;
    private Transform m_player;

    [SerializeField] private float m_followDistance = 20f;

    private void Awake()
    {
        m_manager = FindFirstObjectByType<LevelManager>();
    }

    private void LateUpdate()
    {
        if(!m_manager || !m_manager.GetPlayer())
        {
            Debug.LogError("Unable to get the player reference.");
            return;
        }

        if(m_player == null)
        {
            m_player = m_manager.GetPlayer().transform;
            return;
        }

        transform.position = new Vector3(
            m_player.position.x,
            transform.position.y,
            m_player.position.z
        );
    }
}