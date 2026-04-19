using UnityEngine;
using UnityEngine.UI;

public class GameplayUI : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider m_health;

    public void Init(LevelManager manager)
    {
        manager.GetPlayer().GetHealth().OnHealthChanged += m_health.SetValueWithoutNotify;
        m_health.minValue       = 0f;
        m_health.maxValue       = manager.GetPlayer().GetHealth().GetMaxHealth();
        m_health.value          = manager.GetPlayer().GetHealth().GetHealth();
        m_health.wholeNumbers   = false;
    }
}
