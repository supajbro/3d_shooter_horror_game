using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BaseGunController;

public class GameplayUI : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider m_health;
    [SerializeField] private TextMeshProUGUI m_healthText;

    [Header("Crosshairs")]
    [SerializeField] private TooManyCrosshairs.Crosshair m_autoRifleCrosshair;
    private TooManyCrosshairs.Crosshair m_crosshair;                              // <- Whatever the current selected crosshair is.

    [Header("Ammo UI")]
    [SerializeField] private TextMeshProUGUI m_ammoCount;

    [Header("Weapon Slots")]
    [SerializeField] private WeaponInventory m_weapon01;
    [SerializeField] private WeaponInventory m_weapon02;

    [Header("Weapon Sprites")]
    [SerializeField] private Sprite m_autorifleSprite;
    [SerializeField] private Sprite m_pistolSprite;
    [SerializeField] private Sprite m_shotgunSprite;
    [SerializeField] private Sprite m_rocketLauncherSprite;

    private PlayerPickup m_playerPickup;

    public void Init(LevelManager manager)
    {
        manager.GetPlayer().GetHealth().OnHealthChanged += m_health.SetValueWithoutNotify;
        m_health.minValue       = 0f;
        m_health.maxValue       = manager.GetPlayer().GetHealth().GetMaxHealth();
        m_health.value          = manager.GetPlayer().GetHealth().GetHealth();
        m_health.wholeNumbers   = false;

        manager.GetPlayer().GetHealth().OnHealthChanged += value =>
        {
            m_healthText.SetText(value.ToString());
        };
        m_healthText.text = manager.GetPlayer().GetHealth().GetHealth().ToString();

        m_crosshair = m_autoRifleCrosshair;

        if(m_playerPickup = manager.GetPlayer().GetPlayerPickup())
        {
            m_playerPickup.OnWeaponChanged += HandleWeaponChanged;
        }
    }

    private void HandleWeaponChanged(int index)
    {
        if(m_playerPickup == null)
        {
            Debug.LogError("Missing player pickup reference.");
            return;
        }
        var pickup = m_playerPickup;

        BaseGunController primaryWeapon    = pickup.GetGuns()[0];
        BaseGunController secondaryWeapon  = pickup.GetGuns()[1];

        // Null check as it is valid for these to be null in certain situations.

        if(primaryWeapon != null)
        {
            m_weapon01.SetWeapon(GetWeaponSprite(primaryWeapon.GetGunType()));
        }
        else
        {
            m_weapon01.SetWeapon(null);
        }

        if (secondaryWeapon != null)
        {
            m_weapon02.SetWeapon(GetWeaponSprite(secondaryWeapon.GetGunType()));
        }
        else
        {
            m_weapon02.SetWeapon(null);
        }


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

    public void SetAmmoText(string text)
    {
        if (m_ammoCount == null)
        {
            Debug.LogError("Unable to get reference to ammo count UI.");
            return;
        }
        m_ammoCount.text = text;
    }

    public TextMeshProUGUI GetAmmoText()
    {
        if (m_ammoCount == null)
        {
            Debug.LogError("Unable to get reference to ammo count UI.");
            return null;
        }
        return m_ammoCount;
    }

    public WeaponInventory GetWeaponSlotOne()
    {
        return m_weapon01;
    }

    public WeaponInventory GetWeaponSlotTwo()
    {
        return m_weapon02;
    }

    private Sprite GetWeaponSprite(GunType type)
    {
        switch (type)
        {
            case GunType.AUTORIFLE:
                return m_autorifleSprite;

            case GunType.PISTOL:
                return m_pistolSprite;

            case GunType.SHOTGUN:
                return m_shotgunSprite;

            case GunType.ROCKETLAUNCHER:
                return m_rocketLauncherSprite;
        }

        return null;
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

    public void SetWeapon(Sprite sprite)
    {
        if (m_weaponImage == null)
        {
            Debug.LogError("Missing weapon image reference.");
            return;
        }

        m_weaponImage.sprite = sprite;
        m_weaponImage.enabled = sprite != null;
    }

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
