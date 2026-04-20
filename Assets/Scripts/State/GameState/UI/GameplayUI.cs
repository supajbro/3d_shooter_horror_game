using UnityEngine;
using UnityEngine.UI;

public class GameplayUI : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider m_health;

    [SerializeField] private TooManyCrosshairs.Crosshair m_autoRifleCrosshair;
    private TooManyCrosshairs.Crosshair m_crosshair;                              // <- Whatever the current selected crosshair is.

    [SerializeField] private WeaponInventory m_weapon01;
    [SerializeField] private WeaponInventory m_weapon02;

    public void Init(LevelManager manager)
    {
        manager.GetPlayer().GetHealth().OnHealthChanged += m_health.SetValueWithoutNotify;
        m_health.minValue       = 0f;
        m_health.maxValue       = manager.GetPlayer().GetHealth().GetMaxHealth();
        m_health.value          = manager.GetPlayer().GetHealth().GetHealth();
        m_health.wholeNumbers   = false;

        m_crosshair = m_autoRifleCrosshair;

        manager.GetPlayer().GetPlayerPickup().OnWeaponChanged += HandleWeaponChanged;
    }

    private void HandleWeaponChanged(int index)
    {
        if (index == 0)
        {
            m_weapon01.Equip();
            m_weapon02.Unequip();
        }
        else if (index == 1)
        {
            m_weapon01.Unequip();
            m_weapon02.Equip();
        }
    }

    public TooManyCrosshairs.Crosshair GetCrosshair()
    {
        if(m_crosshair == null)
        {
            Debug.LogError("Unable to get reference for current crosshair.");
            return null;
        }
        return m_crosshair;
    }
}

[System.Serializable]
public class WeaponInventory
{
    [SerializeField] private RectTransform m_holder;
    [SerializeField] private Image m_weaponImage;
    private bool m_active = false;

    [SerializeField] private Vector2 m_activeSize = new Vector2(200.0f, 200.0f);
    [SerializeField] private Vector2 m_inActiveSize = new Vector2(100.0f, 100.0f);

    public void Equip()
    {
        m_active = true;
        m_holder.sizeDelta = m_activeSize;
    }

    public void Unequip()
    {
        m_active = false;
        m_holder.sizeDelta = m_inActiveSize;
    }
}
