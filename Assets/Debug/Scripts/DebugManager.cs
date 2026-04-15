using System.Collections.Generic;
using Unity.Android.Gradle.Manifest;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance;
    private GameStateManager m_gameManager;

    private List<DebugFloat> _floats = new List<DebugFloat>();

    [SerializeField] private GameObject m_debugPanel;
    [SerializeField] private DebugSliderUI m_sliderPrefab;
    [SerializeField] private DebugCategoryUI m_categoryPrefab;

    [Header("Parenting")]
    [SerializeField] private Transform m_headerContentParent;
    [SerializeField] private Transform m_contentParent;

    private bool m_menuOpen = false;

    [Header("Categories")]
    private Dictionary<string, DebugCategoryUI> m_categories = new();           // <- All of the categories
    private Dictionary<string, List<GameObject>> m_categoryItems = new();       // <- All of the items in each category
    private string m_currentCategory = "General";                               // <- Current selected category

    public void Init(GameStateManager manager)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        m_debugPanel.SetActive(false);
        m_gameManager = manager;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            m_menuOpen = !m_menuOpen;
            m_debugPanel.SetActive(m_menuOpen);

            Cursor.lockState = m_menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = m_menuOpen;

            m_gameManager.SetFreezeGame(m_menuOpen);
        }
    }

    public void RegisterFloat(DebugFloat debugFloat, string category = "General")
    {
        // Ensure category exists
        if (!m_categoryItems.ContainsKey(category))
        {
            m_categoryItems[category] = new List<GameObject>();

            // Create button
            var debugCategory = Instantiate(m_categoryPrefab, m_headerContentParent);
            debugCategory.Setup(category, () => SelectCategory(category));

            m_categories.Add(category, debugCategory);
        }

        // Create slider (always under same parent)
        var slider = Instantiate(m_sliderPrefab, m_contentParent);
        slider.SetupFloat(debugFloat);

        // Track it
        m_categoryItems[category].Add(slider.gameObject);

        // Set initial visibility
        slider.gameObject.SetActive(category == m_currentCategory);
    }

    public void RegisterInt(DebugInt debugInt, string category = "General")
    {
        // Ensure category exists
        if (!m_categoryItems.ContainsKey(category))
        {
            m_categoryItems[category] = new List<GameObject>();

            // Create button
            var debugCategory = Instantiate(m_categoryPrefab, m_headerContentParent);
            debugCategory.Setup(category, () => SelectCategory(category));

            m_categories.Add(category, debugCategory);
        }

        // Create slider (always under same parent)
        var slider = Instantiate(m_sliderPrefab, m_contentParent);
        slider.SetupInt(debugInt);

        // Track it
        m_categoryItems[category].Add(slider.gameObject);

        // Set initial visibility
        slider.gameObject.SetActive(category == m_currentCategory);
    }

    private void SelectCategory(string category)
    {
        m_currentCategory = category;

        foreach (var kvp in m_categoryItems)
        {
            bool isActiveCategory = kvp.Key == category;

            foreach (var item in kvp.Value)
            {
                item.SetActive(isActiveCategory);
            }
        }
    }
}