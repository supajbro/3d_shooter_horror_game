using System.Collections.Generic;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance;

    private List<DebugFloat> _floats = new List<DebugFloat>();

    [SerializeField] private GameObject m_debugPanel;
    [SerializeField] private Transform m_contentParent;
    [SerializeField] private DebugSliderUI m_sliderPrefab;

    private bool m_menuOpen = false;

    private void Awake()
    {
        Instance = this;
        m_debugPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            m_menuOpen = !m_menuOpen;
            m_debugPanel.SetActive(m_menuOpen);

            if (m_menuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void RegisterFloat(DebugFloat debugFloat)
    {
        _floats.Add(debugFloat);

        var slider = Instantiate(m_sliderPrefab, m_contentParent);
        slider.Setup(debugFloat);
    }
}