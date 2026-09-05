using TMPro;
using UnityEngine;

/// <summary>
/// Spawns pooled world-space combat feedback. Replace the generated templates
/// with your own FloatingCombatText prefabs through this component if desired.
/// </summary>
public class DamageTextSpawner : MonoBehaviour
{
    private const string SmallKey = "DAMAGE_TEXT_SMALL";
    private const string MediumKey = "DAMAGE_TEXT_MEDIUM";
    private const string LargeKey = "DAMAGE_TEXT_LARGE";
    private const string MassiveKey = "DAMAGE_TEXT_MASSIVE";
    private const string HeadshotKey = "DAMAGE_TEXT_HEADSHOT";

    [Header("Optional prefab overrides")]
    [SerializeField] private FloatingCombatText m_smallDamagePrefab;
    [SerializeField] private FloatingCombatText m_mediumDamagePrefab;
    [SerializeField] private FloatingCombatText m_largeDamagePrefab;
    [SerializeField] private FloatingCombatText m_massiveDamagePrefab;
    [SerializeField] private FloatingCombatText m_headshotPrefab;
    [SerializeField, Min(1)] private int m_initialPoolSize = 12;

    public static DamageTextSpawner s_instance;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void ShowDamage(Vector3 hitPoint, float damage, bool isHeadshot, Camera displayCamera = null)
    {
        if (ObjectPooler.Instance == null)
        {
            Debug.LogWarning("Damage text needs an active ObjectPooler.");
            return;
        }

        DamageTextSpawner spawner = GetOrCreate();
        spawner.EnsurePools();

        // Most callers can use the tagged main camera, but first-person melee
        // already has the exact camera that produced the hit ray. Accept it so
        // combat text always faces and offsets toward the camera the player sees.
        Camera camera = displayCamera != null ? displayCamera : Camera.main;
        if (camera == null)
            return;

        string damageKey = GetDamageKey(damage);
        FloatingCombatText damageText = ObjectPooler.Instance.Spawn(
            damageKey, hitPoint + camera.transform.right * 0.15f, Quaternion.identity)
            ?.GetComponent<FloatingCombatText>();
        damageText?.Configure(Mathf.RoundToInt(damage).ToString(), hitPoint + camera.transform.right * 0.15f,
            camera.transform.right, camera);

        if (!isHeadshot)
            return;

        FloatingCombatText headshotText = ObjectPooler.Instance.Spawn(
            HeadshotKey, hitPoint + camera.transform.up * 0.22f, Quaternion.identity)
            ?.GetComponent<FloatingCombatText>();
        headshotText?.Configure("Headshot", hitPoint + camera.transform.up * 0.22f, camera.transform.up, camera);
    }

    private static DamageTextSpawner GetOrCreate()
    {
        if (s_instance != null)
            return s_instance;

        GameObject spawnerObject = new GameObject("Damage Text Spawner");
        s_instance = spawnerObject.AddComponent<DamageTextSpawner>();
        return s_instance;
    }

    private void EnsurePools()
    {
        RegisterIfNeeded(SmallKey, m_smallDamagePrefab, 24, Color.white, new Vector3(0.05f, 0f, 0f));
        RegisterIfNeeded(MediumKey, m_mediumDamagePrefab, 34, new Color(1f, 0.88f, 0.25f), new Vector3(0.05f, 0f, 0f));
        RegisterIfNeeded(LargeKey, m_largeDamagePrefab, 46, new Color(1f, 0.45f, 0.12f), new Vector3(0.05f, 0f, 0f));
        RegisterIfNeeded(MassiveKey, m_massiveDamagePrefab, 60, new Color(1f, 0.15f, 0.1f), new Vector3(0.05f, 0f, 0f));
        RegisterIfNeeded(HeadshotKey, m_headshotPrefab, 38, new Color(1f, 0.25f, 0.25f), new Vector3(0f, 0.05f, 0f));
    }

    private void RegisterIfNeeded(string key, FloatingCombatText prefab, float fontSize, Color color, Vector3 scale)
    {
        if (ObjectPooler.Instance.HasPool(key))
            return;

        if (prefab == null)
            prefab = Resources.Load<FloatingCombatText>("CombatText/" + key);

        if (prefab == null)
            prefab = CreateDefaultPrefab(key, fontSize, color, scale);

        ObjectPooler.Instance.RegisterPool(key, prefab.gameObject, m_initialPoolSize);
    }

    private static FloatingCombatText CreateDefaultPrefab(string name, float fontSize, Color color, Vector3 scale)
    {
        GameObject template = new GameObject(name + " Prefab");
        template.AddComponent<TextMeshPro>();
        template.transform.localScale = scale;
        FloatingCombatText combatText = template.AddComponent<FloatingCombatText>();
        combatText.SetVisualStyle(fontSize, color);
        template.SetActive(false);
        return combatText;
    }

    private static string GetDamageKey(float damage)
    {
        if (damage <= 25f) return SmallKey;
        if (damage <= 50f) return MediumKey;
        if (damage < 100f) return LargeKey;
        return MassiveKey;
    }
}
